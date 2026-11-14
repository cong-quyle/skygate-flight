using UnityEngine;
using UnityEngine.Rendering;

namespace SkyGate.Rendering.Environment
{
    /// <summary>
    /// Mặt trời, bầu trời procedural, ambient (SH tự tính, không cần bake) và sương mù theo giờ trong ngày.
    /// NightFactor (0 = ban ngày, 1 = đêm) được các hệ khác dùng (hào quang đèn, nội thất).
    /// </summary>
    [ExecuteAlways]
    public sealed class SunController : MonoBehaviour
    {
        public Light sun;
        [Tooltip("Material Skybox/Procedural dùng làm mẫu (để shader vào bản build)")] public Material skyTemplate;
        [Range(0f, 24f)] public float hour = 10f;
        public float azimuthOffset = 200f;
        public float shadowDistanceHigh = 200f;

        public static float NightFactor { get; private set; }
        public static float SunElevation { get; private set; } = 40f;

        Material _sky;
        float _lastHour = -1f;

        void OnEnable() { Apply(true); }

        void OnDisable()
        {
            if (_sky != null)
            {
                if (Application.isPlaying) Destroy(_sky); else DestroyImmediate(_sky);
            }
        }

        void Update()
        {
            if (Mathf.Abs(hour - _lastHour) > 0.001f) Apply(false);
        }

        public void SetHour(float h) { hour = Mathf.Repeat(h, 24f); Apply(false); }

        public void Apply(bool force)
        {
            _lastHour = hour;
            float elev = Mathf.Sin((hour - 6f) / 24f * Mathf.PI * 2f) * 68f;
            SunElevation = elev;
            float az = azimuthOffset + hour / 24f * 360f;
            NightFactor = 1f - Mathf.Clamp01((elev + 6f) / 12f);
            NightFactor = NightFactor * NightFactor * (3f - 2f * NightFactor);

            if (sun != null)
            {
                // pitch dương = ánh sáng hướng xuống. Dưới đường chân trời giữ 2° để tắt dần bằng intensity.
                sun.transform.rotation = Quaternion.Euler(Mathf.Max(elev, 2f), az, 0f);
                float k = Mathf.Clamp01(elev / 8f);
                sun.intensity = Mathf.Lerp(0f, 1.25f, k);
                sun.color = Color.Lerp(new Color(1f, 0.55f, 0.30f), new Color(1f, 0.96f, 0.90f), Mathf.Clamp01(elev / 30f));
                sun.shadows = LightShadows.Soft;
                sun.enabled = elev > -1f;
                RenderSettings.sun = sun;
            }

            Color skyC = Color.Lerp(new Color(0.55f, 0.72f, 0.95f), new Color(0.03f, 0.05f, 0.10f), NightFactor);
            Color eqC = Color.Lerp(new Color(0.62f, 0.66f, 0.70f), new Color(0.03f, 0.04f, 0.07f), NightFactor);
            Color grC = Color.Lerp(new Color(0.30f, 0.28f, 0.25f), new Color(0.02f, 0.02f, 0.03f), NightFactor);
            RenderSettings.ambientMode = AmbientMode.Custom;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(eqC * 0.5f);
            sh.AddDirectionalLight(Vector3.up, skyC * 0.5f, 1f);
            sh.AddDirectionalLight(Vector3.down, grC * 0.5f, 1f);
            RenderSettings.ambientProbe = sh;

            if (_sky == null)
            {
                // Dùng material mẫu (asset) để shader chắc chắn có trong bản build; Shader.Find chỉ là dự phòng khi chạy Editor
                if (skyTemplate != null) _sky = new Material(skyTemplate) { name = "SkyGate_Sky(Runtime)", hideFlags = HideFlags.DontSave };
                else
                {
                    var sh2 = Shader.Find("Skybox/Procedural");
                    if (sh2 != null) _sky = new Material(sh2) { name = "SkyGate_Sky(Runtime)", hideFlags = HideFlags.DontSave };
                }
            }
            if (_sky != null)
            {
                _sky.SetFloat("_Exposure", Mathf.Lerp(1.3f, 0.12f, NightFactor));
                _sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1.0f, 0.6f, NightFactor));
                _sky.SetFloat("_SunSize", NightFactor > 0.9f ? 0f : 0.04f);
                RenderSettings.skybox = _sky;
            }

            // phản chiếu lấy từ skybox (rẻ hơn probe realtime, không cần bake); cập nhật khi đổi giờ
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 128;
            if (Application.isPlaying) DynamicGI.UpdateEnvironment();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00012f;
            RenderSettings.fogColor = Color.Lerp(new Color(0.72f, 0.80f, 0.90f), new Color(0.02f, 0.03f, 0.06f), NightFactor);
        }
    }
}
