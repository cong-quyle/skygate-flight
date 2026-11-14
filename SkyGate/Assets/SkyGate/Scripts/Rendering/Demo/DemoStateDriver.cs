using SkyGate.Core;
using SkyGate.Rendering.Controls;
using SkyGate.Rendering.Environment;
using UnityEngine;

namespace SkyGate.Rendering.Demo
{
    /// <summary>
    /// Thay thế TẠM cho FlightModel/AircraftSystems: đọc PilotInputBus (cảm ứng/bàn phím) và ghi AircraftVisualState
    /// để thử toàn bộ phần đồ họa (bề mặt điều khiển, càng, flap, đèn, cửa, avionics, camera...). Mô hình bay thật thay vào sau
    /// mà không sửa phần rendering. Động lực học ở đây chỉ là đồ chơi, KHÔNG dùng cho hiệu chỉnh.
    /// Phím: L = đèn theo giai đoạn, D = mở/đóng cửa, N = ngày/đêm, P = dừng/chạy chuyển động.
    /// </summary>
    public sealed class DemoStateDriver : MonoBehaviour
    {
        public AircraftStateBus bus;
        public bool moveAircraft = true;
        public SunController sun;

        static readonly AircraftLightFlags[] LightPresets =
        {
            AircraftLightFlags.Nav | AircraftLightFlags.Cabin | AircraftLightFlags.Logo,                                          // đỗ
            AircraftLightFlags.Nav | AircraftLightFlags.Cabin | AircraftLightFlags.Logo | AircraftLightFlags.Beacon | AircraftLightFlags.Taxi | AircraftLightFlags.Wing | AircraftLightFlags.CockpitFlood, // lăn
            AircraftLightFlags.Nav | AircraftLightFlags.Cabin | AircraftLightFlags.Logo | AircraftLightFlags.Beacon | AircraftLightFlags.Strobe
                | AircraftLightFlags.Landing | AircraftLightFlags.Takeoff | AircraftLightFlags.RunwayTurnoff,                    // cất cánh
            AircraftLightFlags.Nav | AircraftLightFlags.Cabin | AircraftLightFlags.Logo | AircraftLightFlags.Beacon | AircraftLightFlags.Strobe, // bay bằng
            AircraftLightFlags.None,
        };
        int _lightIdx;
        bool _doorsOpen;
        float _door;
        float _timeSun = 10f;
        float _prevIas;

        void Awake()
        {
            if (bus == null) bus = GetComponent<AircraftStateBus>();
            if (sun == null) sun = FindFirstObjectByType<SunController>();
            var s = bus.state;
            s.onGround = true; s.gearPos = 1f; s.gearLever = 1f; s.altitudeFt = 0f;
            bus.state = s;
        }

        void Update()
        {
            if (bus == null) return;
            float dt = Time.deltaTime;
            var s = bus.state;
            var c = PilotInputBus.Current;

            // --- phím demo
            if (Controls.SkyInput.KeyDown(Controls.SkyKey.L)) { _lightIdx = (_lightIdx + 1) % LightPresets.Length; s.lights = LightPresets[_lightIdx]; }
            if (Controls.SkyInput.KeyDown(Controls.SkyKey.D)) _doorsOpen = !_doorsOpen;
            if (Controls.SkyInput.KeyDown(Controls.SkyKey.P)) moveAircraft = !moveAircraft;
            if (Controls.SkyInput.KeyDown(Controls.SkyKey.N) && sun != null) { _timeSun = _timeSun < 18f && _timeSun > 6f ? 22f : 10f; sun.SetHour(_timeSun); }

            // --- bề mặt điều khiển (giới hạn tốc độ như thật)
            s.aileron = Mathf.MoveTowards(s.aileron, c.roll, 3f * dt);
            s.elevator = Mathf.MoveTowards(s.elevator, c.pitch, 3f * dt);
            s.rudder = Mathf.MoveTowards(s.rudder, 0f, 2f * dt);
            s.speedbrake = Mathf.MoveTowards(s.speedbrake, c.speedbrake, 0.6f * dt);
            s.flapLever = c.flapLever;
            s.flapPos = Mathf.MoveTowards(s.flapPos, c.flapLever, 0.35f * dt);
            s.gearLever = c.gearDown ? 1f : 0f;
            s.gearPos = Mathf.MoveTowards(s.gearPos, s.gearLever, dt / 8f);

            // --- cần ga và động cơ
            s.thrustLever1 = Mathf.MoveTowards(s.thrustLever1, c.throttle1, 1.2f * dt);
            s.thrustLever2 = Mathf.MoveTowards(s.thrustLever2, c.throttle2, 1.2f * dt);
            s.n1Eng1 = Engine(s.n1Eng1, s.thrustLever1, dt);
            s.n1Eng2 = Engine(s.n1Eng2, s.thrustLever2, dt);
            s.egtEng1 = 300f + Mathf.Max(0f, s.n1Eng1 - 22f) * 7.5f; s.egtEng2 = 300f + Mathf.Max(0f, s.n1Eng2 - 22f) * 7.5f;
            s.ffEng1 = 350f + s.n1Eng1 * 22f; s.ffEng2 = 350f + s.n1Eng2 * 22f;
            bool rev = s.onGround && s.thrustLever1 < -0.05f;
            s.reverserEng1 = Mathf.MoveTowards(s.reverserEng1, rev ? 1f : 0f, 1.5f * dt);
            s.reverserEng2 = Mathf.MoveTowards(s.reverserEng2, rev ? 1f : 0f, 1.5f * dt);
            s.groundSpoilers = Mathf.MoveTowards(s.groundSpoilers, (s.onGround && s.gsKt > 60f && rev) ? 1f : 0f, 2f * dt);

            // --- cửa
            _door = Mathf.MoveTowards(_door, _doorsOpen && s.onGround && s.gsKt < 1f ? 1f : 0f, 0.4f * dt);
            s.doorL1 = s.doorR1 = s.doorL4 = s.doorR4 = s.doorCargoFwd = s.doorCargoAft = _door;

            if (moveAircraft) Dynamics(ref s, c, dt);

            // --- bánh xe và avionics
            float rpm = s.gsKt * 0.5144f / (2f * Mathf.PI * 0.585f) * 60f;
            s.wheelRpmMain = rpm; s.wheelRpmNose = rpm * 0.585f / 0.38f;
            s.tasKt = s.iasKt * (1f + s.altitudeFt / 50000f);
            s.mach = s.tasKt / (661f - s.altitudeFt * 0.0015f);
            s.iasTrendKt = (s.iasKt - _prevIas) / Mathf.Max(dt, 0.001f) * 10f;
            _prevIas = s.iasKt;
            s.radioAltFt = s.altitudeFt;
            s.fuelKg = Mathf.Max(0f, s.fuelKg - (s.ffEng1 + s.ffEng2) / 3600f * dt);
            s.grossWeightKg = 57600f + s.fuelKg;
            bus.state = s;
        }

        static float Engine(float n1, float lever, float dt)
        {
            float target = lever >= 0f ? 22f + 78f * Mathf.Pow(lever, 0.9f) : 22f + 40f * -lever;
            float rate = target > n1 ? 18f : 25f;
            return Mathf.MoveTowards(n1, target, rate * dt);
        }

        void Dynamics(ref AircraftVisualState s, PilotControls c, float dt)
        {
            float n1 = (s.n1Eng1 + s.n1Eng2) * 0.5f;
            float thrustNorm = Mathf.InverseLerp(22f, 100f, n1);
            if (s.onGround)
            {
                float accel = thrustNorm * 4.4f - 0.015f * s.gsKt;
                if (s.reverserEng1 > 0.5f) accel -= 4.5f * Mathf.InverseLerp(22f, 62f, n1);
                if (c.parkingBrake) accel = -Mathf.Min(s.gsKt / Mathf.Max(dt, 0.001f), 20f);
                else if (c.brake > 0f) accel -= 6f * c.brake;
                s.gsKt = Mathf.Max(0f, s.gsKt + accel * dt);
                s.iasKt = s.gsKt;
                s.noseSteerDeg = c.roll * (s.gsKt < 40f ? 60f : 6f);
                float hdgRate = s.gsKt * 0.5144f * Mathf.Tan(s.noseSteerDeg * Mathf.Deg2Rad) / A320Data.Wheelbase * Mathf.Rad2Deg;
                s.headingDeg = Mathf.Repeat(s.headingDeg + hdgRate * dt, 360f);
                float pitchTarget = s.iasKt > s.vrKt - 3f ? Mathf.Max(0f, c.pitch) * 14f : 0f;
                s.pitchDeg = Mathf.MoveTowards(s.pitchDeg, pitchTarget, 3f * dt);
                s.rollDeg = Mathf.MoveTowards(s.rollDeg, 0f, 10f * dt);
                s.vsFpm = 0f;
                if (s.iasKt > s.vrKt && s.pitchDeg > 6f) { s.onGround = false; s.vsFpm = 1500f; s.noseSteerDeg = 0f; }
            }
            else
            {
                float iasTarget = 125f + 150f * thrustNorm - 12f * s.speedbrake;
                s.iasKt = Mathf.Lerp(s.iasKt, iasTarget, 0.12f * dt);
                s.gsKt = s.iasKt;
                s.rollDeg = Mathf.MoveTowards(s.rollDeg, c.roll * 33f, 12f * dt);
                float pitchTarget = c.pitch * 16f + 2.5f;
                s.pitchDeg = Mathf.MoveTowards(s.pitchDeg, pitchTarget, 5f * dt);
                s.vsFpm = s.iasKt * 101.27f * Mathf.Sin((s.pitchDeg - 3f) * Mathf.Deg2Rad);
                s.altitudeFt += s.vsFpm / 60f * dt;
                float rate = 9.81f * Mathf.Tan(s.rollDeg * Mathf.Deg2Rad) / Mathf.Max(60f, s.iasKt * 0.5144f) * Mathf.Rad2Deg;
                s.headingDeg = Mathf.Repeat(s.headingDeg + rate * dt, 360f);
                if (s.altitudeFt <= 0f)
                {
                    s.altitudeFt = 0f; s.onGround = true; s.vsFpm = 0f; s.pitchDeg = Mathf.Min(s.pitchDeg, 3f);
                }
            }
            s.trackDeg = s.headingDeg;
            s.selHeadingDeg = s.headingDeg;

            // chuyển động thật của transform (floating origin lo phần dịch gốc)
            Quaternion rot = Quaternion.Euler(-s.pitchDeg * (s.onGround ? 0.0f : 1f), s.headingDeg, -s.rollDeg);
            Vector3 fwd = Quaternion.Euler(0f, s.headingDeg, 0f) * Vector3.forward;
            Vector3 pos = bus.transform.position + fwd * (s.gsKt * 0.5144f * dt);
            pos.y = s.altitudeFt * 0.3048f;
            bus.transform.SetPositionAndRotation(pos, rot);
        }
    }
}
