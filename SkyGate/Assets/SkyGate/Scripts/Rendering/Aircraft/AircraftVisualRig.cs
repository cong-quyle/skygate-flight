using System;
using System.Collections.Generic;
using SkyGate.Core;
using UnityEngine;

namespace SkyGate.Rendering.Aircraft
{
    /// <summary>Kênh dữ liệu mà rig tính từ AircraftVisualState mỗi frame (không cấp phát).</summary>
    public enum Ch
    {
        None = 0,
        Aileron, Elevator, Rudder,           // -1..1
        SlatDeg, FlapDeg, FlapNorm,          // độ / 0..1
        SpoilerL, SpoilerR,                  // 0..1
        GearRetract,                         // 0 = thả, 1 = thu
        NoseSteer,                           // độ
        WheelNoseDps, WheelMainDps,          // độ/giây
        Fan1Dps, Fan2Dps,
        Reverser1, Reverser2,                // 0..1
        DoorL1, DoorR1, DoorL4, DoorR4, DoorCargoFwd, DoorCargoAft,
        Thrust1, Thrust2,                    // -1..1
        FlapLever, SpeedbrakeLever, GearLever, StabTrim,
        Count
    }

    /// <summary>
    /// Một ràng buộc: kênh -> xoay (hoặc tịnh tiến, hoặc quay liên tục) của một node.
    /// Model thật chỉ cần chỉnh trục/dấu ở đây, không sửa code.
    /// Góc = giá trị * scale (giá trị âm dùng scaleNeg nếu khác 0).
    /// </summary>
    [Serializable]
    public sealed class PartBinding
    {
        public Transform node;
        public Ch channel;
        public Vector3 axis = Vector3.right;
        public float scale = 1f;
        public float scaleNeg = 0f;
        public bool translate;      // true: dịch theo axis * giá trị (m) thay vì xoay
        public bool spin;           // true: giá trị là độ/giây, cộng dồn góc
        public bool hideWhenZero;   // ẩn renderer khi kênh ≈ 0 (tấm cửa)

        [NonSerialized] public float spinAngle;
        [NonSerialized] public Renderer renderer;
    }

    /// <summary>
    /// Ánh xạ trạng thái hình ảnh -> chuyển động các bộ phận. CHỈ ĐỌC AircraftStateBus.
    /// Quy ước dấu (đã kiểm tra): mép sau lên = +góc quanh trục bản lề +X; slat chúc xuống = +góc;
    /// rudder +góc = mép sau sang trái; càng chính phải thu = -góc quanh +Z, trái = +góc; càng mũi thu = -góc quanh +X;
    /// lái bánh mũi sang phải = +góc quanh +Y; bánh lăn tới = +góc quanh +X; cửa trái mở = +góc quanh +Y, cửa phải = -góc.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftVisualRig : MonoBehaviour
    {
        public AircraftStateBus bus;
        public List<PartBinding> bindings = new List<PartBinding>();

        readonly float[] _ch = new float[(int)Ch.Count];
        Transform[] _nodes;
        Vector3[] _restPos;
        Quaternion[] _restRot;
        readonly Dictionary<Transform, int> _nodeIndex = new Dictionary<Transform, int>();
        bool _ready;

        void Reset()
        {
            bus = GetComponent<AircraftStateBus>();
            A320RigDefaults.Populate(this);
        }

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            if (bus == null) bus = GetComponentInParent<AircraftStateBus>();
            var list = new List<Transform>();
            foreach (var b in bindings)
            {
                if (b == null || b.node == null) continue;
                if (!_nodeIndex.ContainsKey(b.node)) { _nodeIndex[b.node] = list.Count; list.Add(b.node); }
                if (b.hideWhenZero) b.renderer = b.node.GetComponent<Renderer>();
            }
            _nodes = list.ToArray();
            _restPos = new Vector3[_nodes.Length];
            _restRot = new Quaternion[_nodes.Length];
            for (int i = 0; i < _nodes.Length; i++)
            {
                _restPos[i] = _nodes[i].localPosition;
                _restRot[i] = _nodes[i].localRotation;
            }
            _ready = true;
        }

        void LateUpdate()
        {
            if (bus == null) return;
            if (!_ready) Init();
            Compute(bus.state);
            Apply(Time.deltaTime);
        }

        void Compute(in AircraftVisualState s)
        {
            var c = _ch;
            c[(int)Ch.Aileron] = s.aileron;
            c[(int)Ch.Elevator] = s.elevator;
            c[(int)Ch.Rudder] = s.rudder;
            A320Data.FlapAngles(s.flapPos, out float slat, out float flap);
            c[(int)Ch.SlatDeg] = slat;
            c[(int)Ch.FlapDeg] = flap;
            c[(int)Ch.FlapNorm] = flap / 40f;

            float assist = Mathf.Clamp01((Mathf.Abs(s.aileron) - 0.15f) * 1.2f);
            float baseSp = Mathf.Max(s.speedbrake * 0.8f, s.groundSpoilers);
            c[(int)Ch.SpoilerL] = Mathf.Max(baseSp, s.aileron < 0f ? assist : 0f);
            c[(int)Ch.SpoilerR] = Mathf.Max(baseSp, s.aileron > 0f ? assist : 0f);

            c[(int)Ch.GearRetract] = 1f - Mathf.Clamp01(s.gearPos);
            c[(int)Ch.NoseSteer] = s.noseSteerDeg;
            c[(int)Ch.WheelNoseDps] = s.wheelRpmNose * 6f;
            c[(int)Ch.WheelMainDps] = s.wheelRpmMain * 6f;
            c[(int)Ch.Fan1Dps] = Mathf.Min(1500f, s.n1Eng1 * 0.01f * A320Data.N1RpmAt100 * 6f);
            c[(int)Ch.Fan2Dps] = Mathf.Min(1500f, s.n1Eng2 * 0.01f * A320Data.N1RpmAt100 * 6f);
            c[(int)Ch.Reverser1] = s.reverserEng1;
            c[(int)Ch.Reverser2] = s.reverserEng2;
            c[(int)Ch.DoorL1] = s.doorL1; c[(int)Ch.DoorR1] = s.doorR1;
            c[(int)Ch.DoorL4] = s.doorL4; c[(int)Ch.DoorR4] = s.doorR4;
            c[(int)Ch.DoorCargoFwd] = s.doorCargoFwd; c[(int)Ch.DoorCargoAft] = s.doorCargoAft;
            c[(int)Ch.Thrust1] = s.thrustLever1; c[(int)Ch.Thrust2] = s.thrustLever2;
            c[(int)Ch.FlapLever] = s.flapLever;
            c[(int)Ch.SpeedbrakeLever] = s.speedbrake;
            c[(int)Ch.GearLever] = s.gearLever;
            c[(int)Ch.StabTrim] = s.stabTrimDeg;
        }

        void Apply(float dt)
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                _nodes[i].localPosition = _restPos[i];
                _nodes[i].localRotation = _restRot[i];
            }
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b == null || b.node == null) continue;
                float v = _ch[(int)b.channel];
                if (b.hideWhenZero && b.renderer != null) b.renderer.enabled = v > 0.002f;
                float a = (v < 0f && b.scaleNeg != 0f) ? v * b.scaleNeg : v * b.scale;
                if (b.spin)
                {
                    b.spinAngle = Mathf.Repeat(b.spinAngle + a * dt, 360f);
                    a = b.spinAngle;
                }
                int ni = _nodeIndex[b.node];
                if (b.translate) b.node.localPosition += _restRot[ni] * (b.axis * a);
                else b.node.localRotation = b.node.localRotation * Quaternion.AngleAxis(a, b.axis);
            }
        }
    }
}
