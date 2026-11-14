using SkyGate.Core;
using SkyGate.Rendering.Aircraft;
using SkyGate.Rendering.Avionics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SkyGate.Rendering.Quality
{
    public enum QualityTier { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// Mức đồ họa và tối ưu (plan mục 11). Mặc định High, chỉnh cho Redmi Turbo 4 Pro (Adreno 825, 120 Hz) nhắm 60 fps:
    ///  - đổi URP asset theo tier (MSAA, bóng, đèn phụ...);
    ///  - render scale động theo thời gian GPU (FrameTimingManager), chỉ chạy trên thiết bị;
    ///  - hạ cấp theo nhiệt (PowerManager.getThermalHeadroom, Android 11+).
    /// </summary>
    public sealed class GraphicsQualityManager : MonoBehaviour
    {
        public static GraphicsQualityManager Instance { get; private set; }

        public QualityTier tier = QualityTier.High;
        [Tooltip("Thứ tự: Low, Medium, High")] public UniversalRenderPipelineAsset[] tierAssets = new UniversalRenderPipelineAsset[3];
        public bool dynamicResolution = true;
        public bool thermalThrottle = true;

        struct TierParams
        {
            public float renderScale, minScale, maxScale; public int fps, avionicsHz; public float lodBias;
            public AircraftLightFlags realLights; public float gpuHighMs, gpuLowMs;
        }

        static readonly TierParams[] P =
        {
            new TierParams { renderScale = 0.65f, minScale = 0.55f, maxScale = 0.75f, fps = 30, avionicsHz = 12, lodBias = 0.7f, realLights = AircraftLightFlags.None, gpuHighMs = 28f, gpuLowMs = 18f },
            new TierParams { renderScale = 0.75f, minScale = 0.60f, maxScale = 0.85f, fps = 30, avionicsHz = 20, lodBias = 1.0f, realLights = AircraftLightFlags.Landing, gpuHighMs = 28f, gpuLowMs = 18f },
            new TierParams { renderScale = 0.80f, minScale = 0.70f, maxScale = 0.90f, fps = 60, avionicsHz = 30, lodBias = 1.5f,
                realLights = AircraftLightFlags.Landing | AircraftLightFlags.Taxi | AircraftLightFlags.RunwayTurnoff, gpuHighMs = 13.5f, gpuLowMs = 9f },
        };

        UniversalRenderPipelineAsset _asset;
        float _scale;
        float _ema;
        float _highTimer, _lowTimer;
        readonly FrameTiming[] _ft = new FrameTiming[1];

        // nhiệt
        int _thermalLevel;
        float _thermalPoll, _thermalCoolTimer;
        float _origShadowDist;
        int _origMsaa;

        public float CurrentRenderScale => _scale;
        public int ThermalLevel => _thermalLevel;
        public float GpuMs => _ema;

        void Awake()
        {
            Instance = this;
            QualitySettings.vSyncCount = 0;
            ApplyTier(tier);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RestoreAsset();
        }

        public void CycleTier() => ApplyTier((QualityTier)(((int)tier + 1) % 3));

        public void ApplyTier(QualityTier t)
        {
            RestoreAsset();
            tier = t;
            var tp = P[(int)t];
            var asset = tierAssets != null && tierAssets.Length > (int)t ? tierAssets[(int)t] : null;
            if (asset != null)
            {
                GraphicsSettings.defaultRenderPipeline = asset;
                QualitySettings.renderPipeline = asset;
                _asset = asset;
            }
            else _asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            if (_asset != null)
            {
                _origShadowDist = _asset.shadowDistance;
                _origMsaa = _asset.msaaSampleCount;
            }
            _scale = tp.renderScale;
            SetScale(_scale);
            _thermalLevel = 0;
            Application.targetFrameRate = tp.fps;
            QualitySettings.vSyncCount = 0;
            QualitySettings.lodBias = tp.lodBias;
            QualitySettings.globalTextureMipmapLimit = t == QualityTier.Low ? 1 : 0;
            AvionicsSystem.TargetHz = tp.avionicsHz;
            AircraftLights.RealLightMask = tp.realLights;
            _ema = 0f; _highTimer = _lowTimer = 0f;
        }

        void SetScale(float s)
        {
#if UNITY_EDITOR
            // Trong Editor không ghi vào asset để khỏi làm bẩn file; chỉ áp dụng trên thiết bị
            return;
#else
            if (_asset != null) _asset.renderScale = s;
#endif
        }

        void RestoreAsset()
        {
            if (_asset == null) return;
#if !UNITY_EDITOR
            return;
#else
            _asset.shadowDistance = _origShadowDist > 0f ? _origShadowDist : _asset.shadowDistance;
            if (_origMsaa > 0) _asset.msaaSampleCount = _origMsaa;
#endif
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            float ms = dt * 1000f;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _ft) > 0 && _ft[0].gpuFrameTime > 0.01) ms = (float)_ft[0].gpuFrameTime;
            _ema = _ema <= 0f ? ms : Mathf.Lerp(_ema, ms, 0.05f);

            if (dynamicResolution) UpdateDynamicScale(dt);
            if (thermalThrottle) UpdateThermal(dt);
        }

        void UpdateDynamicScale(float dt)
        {
            var tp = P[(int)tier];
            if (_ema > tp.gpuHighMs) { _highTimer += dt; _lowTimer = 0f; }
            else if (_ema < tp.gpuLowMs) { _lowTimer += dt; _highTimer = 0f; }
            else { _highTimer = _lowTimer = 0f; }

            if (_highTimer > 2f && _scale > tp.minScale + 0.001f)
            {
                _scale = Mathf.Max(tp.minScale, _scale - 0.05f); SetScale(_scale); _highTimer = 0f;
            }
            else if (_lowTimer > 5f && _scale < tp.maxScale - 0.001f)
            {
                _scale = Mathf.Min(tp.maxScale, _scale + 0.05f); SetScale(_scale); _lowTimer = 0f;
            }
        }

        // ------------------------------------------------------------------ nhiệt
        void UpdateThermal(float dt)
        {
            _thermalPoll += dt;
            if (_thermalPoll < 5f) return;
            _thermalPoll = 0f;
            float headroom = ReadThermalHeadroom();
            if (float.IsNaN(headroom)) return;
            int target = headroom > 1.0f ? 3 : headroom > 0.92f ? 2 : headroom > 0.85f ? 1 : 0;
            if (target > _thermalLevel) { SetThermal(target); _thermalCoolTimer = 0f; }
            else if (headroom < 0.70f && _thermalLevel > 0)
            {
                _thermalCoolTimer += 5f;
                if (_thermalCoolTimer >= 60f) { SetThermal(_thermalLevel - 1); _thermalCoolTimer = 0f; }
            }
        }

        void SetThermal(int level)
        {
            _thermalLevel = level;
            var tp = P[(int)tier];
            AvionicsSystem.TargetHz = level >= 1 ? Mathf.Min(tp.avionicsHz, 20) : tp.avionicsHz;
            Application.targetFrameRate = level >= 2 ? Mathf.Min(tp.fps, 45) : tp.fps;
#if !UNITY_EDITOR
            if (_asset != null)
            {
                _asset.shadowDistance = level >= 1 ? _origShadowDist * 0.6f : _origShadowDist;
                _asset.msaaSampleCount = level >= 2 ? Mathf.Min(_origMsaa, 2) : _origMsaa;
            }
#endif
            if (level >= 3 && tier == QualityTier.High) ApplyTier(QualityTier.Medium);
            Debug.Log("[SkyGate] Hạ cấp theo nhiệt: mức " + level);
        }

        static float ReadThermalHeadroom()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var pm = act.Call<AndroidJavaObject>("getSystemService", "power"))
                    return pm.Call<float>("getThermalHeadroom", 10);
            }
            catch { return float.NaN; }
#else
            return float.NaN;
#endif
        }
    }
}
