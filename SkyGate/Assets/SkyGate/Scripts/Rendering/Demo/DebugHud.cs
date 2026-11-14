using SkyGate.Rendering.Avionics;
using SkyGate.Rendering.Cameras;
using SkyGate.Rendering.Controls;
using SkyGate.Rendering.Environment;
using SkyGate.Rendering.Quality;
using UnityEngine;
using UnityEngine.UI;

namespace SkyGate.Rendering.Demo
{
    /// <summary>
    /// HUD debug: nút đổi camera / camera phụ / tier đồ họa / ngày đêm / mở cửa, và dòng thông số fps, GPU ms, render scale.
    /// Dùng lại hệ nút của TouchFlightControls (không cần EventSystem).
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        public CameraDirector director;
        public GraphicsQualityManager quality;
        public SunController sun;
        public DemoStateDriver demo;

        Text _stats;
        float _acc, _frames;
        float _fps;
        bool _night;

        void Start()
        {
            if (director == null) director = FindFirstObjectByType<CameraDirector>();
            if (quality == null) quality = FindFirstObjectByType<GraphicsQualityManager>();
            if (sun == null) sun = FindFirstObjectByType<SunController>();
            if (demo == null) demo = FindFirstObjectByType<DemoStateDriver>();
            var tc = TouchFlightControls.Instance;
            if (tc == null) { enabled = false; return; }

            tc.AddButton("CAM", false, 0, () => director?.Cycle(), () => director != null ? "CAM: " + ModeName(director.mode) : "CAM");
            tc.AddButton("CAM +", false, 1, () => director?.CycleSub());
            tc.AddButton("TIER", false, 2, () => quality?.CycleTier(), () => quality != null ? "ĐỒ HỌA: " + quality.tier.ToString().ToUpperInvariant() : "ĐỒ HỌA");
            tc.AddButton("NGÀY/ĐÊM", false, 3, () => { _night = !_night; sun?.SetHour(_night ? 22f : 10f); });

            var go = new GameObject("StatsText", typeof(RectTransform));
            go.transform.SetParent(tc.transform.GetChild(0), false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(30, -92);
            rt.sizeDelta = new Vector2(900, 30);
            _stats = go.AddComponent<Text>();
            _stats.font = UIFactory.DefaultFont;
            _stats.fontSize = 20;
            _stats.color = new Color(1f, 1f, 0.6f);
            _stats.alignment = TextAnchor.MiddleLeft;
            _stats.raycastTarget = false;
            _stats.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        static string ModeName(CameraMode m)
        {
            switch (m)
            {
                case CameraMode.Exterior: return "NGOÀI";
                case CameraMode.Cockpit: return "BUỒNG LÁI";
                case CameraMode.Cabin: return "KHOANG";
                default: return "THÁP";
            }
        }

        void Update()
        {
            if (_stats == null) return;
            _acc += Time.unscaledDeltaTime; _frames++;
            if (_acc < 0.5f) return;
            _fps = _frames / _acc; _acc = 0f; _frames = 0f;
            string thermal = quality != null && quality.ThermalLevel > 0 ? "  NHIỆT " + quality.ThermalLevel : "";
            _stats.text = Mathf.RoundToInt(_fps) + " fps   GPU " + (quality != null ? quality.GpuMs.ToString("0.0") : "-") + " ms   scale "
                          + (quality != null ? quality.CurrentRenderScale.ToString("0.00") : "-") + thermal;
        }
    }
}
