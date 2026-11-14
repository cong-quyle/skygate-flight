using SkyGate.Core;
using SkyGate.Rendering.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkyGate.Rendering.Aircraft
{
    /// <summary>
    /// Đèn ngoài của máy bay: hào quang billboard (quad additive, luôn quay về camera, có kích thước tối thiểu theo khoảng cách)
    /// và Light thật (Spot) cho landing/taxi/turnoff theo cấp chất lượng. Chỉ đọc AircraftStateBus.lights.
    /// Node đèn (Light_*) do generator tạo; model thật chỉ cần có node cùng tên.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftLights : MonoBehaviour
    {
        public AircraftStateBus bus;
        public Material glowMaterial;
        /// <summary>Cờ đèn được phép dùng Light thật (GraphicsQualityManager đặt theo tier).</summary>
        public static AircraftLightFlags RealLightMask = AircraftLightFlags.Landing | AircraftLightFlags.Taxi | AircraftLightFlags.RunwayTurnoff;

        enum Pattern { Steady, Beacon, Strobe }

        struct Def
        {
            public string node; public AircraftLightFlags flag; public Color color; public Pattern pattern;
            public float size, phase; public bool real; public float range, angle, intensity;
        }

        static readonly Color Red = new Color(1f, 0.08f, 0.05f), Green = new Color(0.1f, 1f, 0.3f),
            White = new Color(1f, 1f, 1f), Warm = new Color(1f, 0.93f, 0.8f);

        static Def D(string n, AircraftLightFlags f, Color c, Pattern p, float size, float phase = 0f,
            bool real = false, float range = 0f, float angle = 0f, float inten = 0f) =>
            new Def { node = n, flag = f, color = c, pattern = p, size = size, phase = phase, real = real, range = range, angle = angle, intensity = inten };

        static readonly Def[] Defs =
        {
            D("Light_NavL", AircraftLightFlags.Nav, Red, Pattern.Steady, 0.35f),
            D("Light_NavR", AircraftLightFlags.Nav, Green, Pattern.Steady, 0.35f),
            D("Light_NavTail", AircraftLightFlags.Nav, White, Pattern.Steady, 0.30f),
            D("Light_BeaconTop", AircraftLightFlags.Beacon, Red, Pattern.Beacon, 0.55f, 0f),
            D("Light_BeaconBottom", AircraftLightFlags.Beacon, Red, Pattern.Beacon, 0.55f, 0.5f),
            D("Light_StrobeL", AircraftLightFlags.Strobe, White, Pattern.Strobe, 0.75f),
            D("Light_StrobeR", AircraftLightFlags.Strobe, White, Pattern.Strobe, 0.75f),
            D("Light_StrobeTail", AircraftLightFlags.Strobe, White, Pattern.Strobe, 0.6f),
            D("Light_LandL", AircraftLightFlags.Landing, Warm, Pattern.Steady, 0.9f, 0f, true, 600f, 14f, 28f),
            D("Light_LandR", AircraftLightFlags.Landing, Warm, Pattern.Steady, 0.9f, 0f, true, 600f, 14f, 28f),
            D("Light_Taxi", AircraftLightFlags.Taxi | AircraftLightFlags.Takeoff, Warm, Pattern.Steady, 0.5f, 0f, true, 150f, 32f, 12f),
            D("Light_TakeOff", AircraftLightFlags.Takeoff, Warm, Pattern.Steady, 0.5f),
            D("Light_TurnoffL", AircraftLightFlags.RunwayTurnoff, Warm, Pattern.Steady, 0.4f, 0f, true, 80f, 60f, 8f),
            D("Light_TurnoffR", AircraftLightFlags.RunwayTurnoff, Warm, Pattern.Steady, 0.4f, 0f, true, 80f, 60f, 8f),
            D("Light_LogoL", AircraftLightFlags.Logo, White, Pattern.Steady, 0.3f),
            D("Light_LogoR", AircraftLightFlags.Logo, White, Pattern.Steady, 0.3f),
            D("Light_WingL", AircraftLightFlags.Wing, White, Pattern.Steady, 0.3f),
            D("Light_WingR", AircraftLightFlags.Wing, White, Pattern.Steady, 0.3f),
        };

        sealed class Runtime
        {
            public Transform glow; public MeshRenderer glowRenderer; public Light light;
            public MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        }

        Runtime[] _rt;
        Camera _cam;
        static Mesh _quad;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "GlowQuad(Runtime)", hideFlags = HideFlags.HideAndDontSave };
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateBounds();
            return _quad;
        }

        void Awake()
        {
            if (bus == null) bus = GetComponentInParent<AircraftStateBus>();
            _rt = new Runtime[Defs.Length];
            for (int i = 0; i < Defs.Length; i++)
            {
                var node = A320RigDefaults.FindDeep(transform, Defs[i].node);
                if (node == null) continue;
                var r = new Runtime();
                var g = new GameObject("Glow") { hideFlags = HideFlags.None };
                g.transform.SetParent(node, false);
                g.AddComponent<MeshFilter>().sharedMesh = Quad();
                var mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = glowMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.glow = g.transform; r.glowRenderer = mr;
                if (Defs[i].real)
                {
                    var lg = new GameObject("RealLight");
                    lg.transform.SetParent(node, false);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.range = Defs[i].range; l.spotAngle = Defs[i].angle; l.innerSpotAngle = Defs[i].angle * 0.6f;
                    l.intensity = Defs[i].intensity; l.color = Defs[i].color;
                    l.shadows = LightShadows.None;
                    l.enabled = false;
                    r.light = l;
                }
                _rt[i] = r;
            }
        }

        static bool PatternOn(Pattern p, float t, float phase)
        {
            switch (p)
            {
                case Pattern.Beacon: return Mathf.Repeat(t + phase, 1.0f) < 0.1f;
                case Pattern.Strobe:
                    float k = Mathf.Repeat(t, 1.0f);
                    return k < 0.05f || (k > 0.15f && k < 0.20f);
                default: return true;
            }
        }

        void LateUpdate()
        {
            if (bus == null) return;
            if (_cam == null) _cam = Camera.main;
            var s = bus.state;
            float t = Time.time;
            float night = SunController.NightFactor;
            float dayGain = Mathf.Lerp(0.30f, 1f, night);
            Vector3 camPos = _cam != null ? _cam.transform.position : Vector3.zero;
            Quaternion camRot = _cam != null ? _cam.transform.rotation : Quaternion.identity;

            for (int i = 0; i < Defs.Length; i++)
            {
                var r = _rt[i];
                if (r == null) continue;
                var d = Defs[i];
                bool on = (s.lights & d.flag) != 0 && PatternOn(d.pattern, t, d.phase);
                if ((d.flag & (AircraftLightFlags.Taxi | AircraftLightFlags.Takeoff | AircraftLightFlags.RunwayTurnoff)) != 0 && s.gearPos < 0.9f) on = false;
                r.glowRenderer.enabled = on;
                if (on)
                {
                    float dist = Vector3.Distance(camPos, r.glow.position);
                    float size = Mathf.Max(d.size, dist * 0.006f);
                    r.glow.localScale = Vector3.one * size;
                    r.glow.rotation = camRot;
                    Color c = d.color * dayGain; c.a = 1f;
                    r.mpb.SetColor(BaseColorId, c);
                    r.mpb.SetColor(ColorId, c);
                    r.glowRenderer.SetPropertyBlock(r.mpb);
                }
                if (r.light != null)
                    r.light.enabled = on && ((RealLightMask & d.flag) != 0) && night > 0.05f;
            }
        }
    }
}
