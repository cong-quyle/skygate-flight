using SkyGate.Core;
using SkyGate.Rendering.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkyGate.Rendering.Aircraft
{
    /// <summary>
    /// Ánh sáng nền (ambient) riêng cho nội thất. Máy bay là vật thể động nên lightmap tĩnh và light probe của thế giới không dùng được:
    /// mọi renderer nội thất dùng LightProbeUsage.CustomProvided với SH tự tính từ đèn cabin, đèn buồng lái và ánh sáng ngày lọt qua cửa sổ.
    /// Nắng trực tiếp xuyên cửa sổ do vỏ trong (TwoSided shadow, có lỗ alpha) lo, không nằm ở đây.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorAmbient : MonoBehaviour
    {
        public AircraftStateBus bus;
        [Tooltip("Màu đèn cabin. Lấy từ LiveryDefinition nếu để trắng ấm mặc định.")]
        public Color cabinLightColor = new Color(1f, 0.93f, 0.82f);
        public float interval = 0.25f;

        Renderer[] _cabin, _cockpit;
        readonly SphericalHarmonicsL2[] _shCabin = new SphericalHarmonicsL2[1];
        readonly SphericalHarmonicsL2[] _shCockpit = new SphericalHarmonicsL2[1];
        MaterialPropertyBlock _mpb;
        float _next;
        float _lastKey = -999f;

        void Awake()
        {
            if (bus == null) bus = GetComponentInParent<AircraftStateBus>();
            if (bus != null && bus.livery != null) cabinLightColor = bus.livery.cabinLightColor;
            _mpb = new MaterialPropertyBlock();
            var cabin = A320RigDefaults.FindDeep(transform, AircraftPartNames.Cabin);
            var cockpit = A320RigDefaults.FindDeep(transform, AircraftPartNames.Cockpit);
            var shell = A320RigDefaults.FindDeep(transform, "Shell");
            var cabinList = new System.Collections.Generic.List<Renderer>();
            if (cabin != null) cabinList.AddRange(cabin.GetComponentsInChildren<Renderer>(true));
            if (shell != null) cabinList.AddRange(shell.GetComponentsInChildren<Renderer>(true));
            _cabin = cabinList.ToArray();
            _cockpit = cockpit != null ? cockpit.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            foreach (var r in _cabin) r.lightProbeUsage = LightProbeUsage.CustomProvided;
            foreach (var r in _cockpit) r.lightProbeUsage = LightProbeUsage.CustomProvided;
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + interval;
            var flags = bus != null ? bus.state.lights : AircraftLightFlags.Cabin;
            bool cabinOn = (flags & AircraftLightFlags.Cabin) != 0;
            bool flood = (flags & AircraftLightFlags.CockpitFlood) != 0;
            float night = SunController.NightFactor;
            float key = (cabinOn ? 1f : 0f) + (flood ? 2f : 0f) + Mathf.Round(night * 50f) * 10f;
            if (Mathf.Approximately(key, _lastKey)) return;
            _lastKey = key;

            // ánh sáng ngày lọt qua cửa sổ (xanh nhạt), mất dần khi tối
            Color daylight = new Color(0.55f, 0.60f, 0.66f) * (0.35f * (1f - night));
            Color cabinCol = daylight + (cabinOn ? cabinLightColor * 0.75f : cabinLightColor * 0.05f);
            Color cockpitCol = daylight * 1.4f + new Color(0.10f, 0.12f, 0.14f) * 0.5f
                               + (flood ? new Color(0.9f, 0.85f, 0.75f) * 0.45f : Color.black)
                               + (cabinOn ? cabinLightColor * 0.12f : Color.black);
            Build(ref _shCabin[0], cabinCol);
            Build(ref _shCockpit[0], cockpitCol);
            Push(_cabin, _shCabin);
            Push(_cockpit, _shCockpit);
        }

        static void Build(ref SphericalHarmonicsL2 sh, Color c)
        {
            sh.Clear();
            sh.AddAmbientLight(c * 0.8f);
            sh.AddDirectionalLight(Vector3.up, c * 0.35f, 1f);   // trần sáng hơn
            sh.AddDirectionalLight(Vector3.down, c * 0.15f, 1f); // sàn phản xạ nhẹ
        }

        void Push(Renderer[] list, SphericalHarmonicsL2[] sh)
        {
            for (int i = 0; i < list.Length; i++)
            {
                var r = list[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.CopySHCoefficientArraysFrom(sh);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
