using System;
using UnityEngine;

namespace SkyGate.Core
{
    /// <summary>
    /// Lệnh điều khiển của phi công (từ cảm ứng, nghiêng máy, bàn phím, tay cầm).
    /// Lớp INPUT ghi vào đây; FlightModel/AircraftSystems đọc. Rendering không đọc trực tiếp (chỉ đọc AircraftVisualState).
    /// </summary>
    [Serializable]
    public struct PilotControls
    {
        public float pitch;      // -1..1, + = kéo cần (mũi lên)
        public float roll;       // -1..1, + = nghiêng phải
        public float yaw;        // -1..1, + = đạp phải
        public float throttle1;  // -1 (đảo chiều tối đa) .. 0 (IDLE) .. 1 (TOGA)
        public float throttle2;
        public bool gearDown;
        public int flapLever;    // 0..4 = CONF 0, 1+F, 2, 3, FULL
        public float speedbrake; // 0..1
        public float brake;      // 0..1
        public bool parkingBrake;

        public static PilotControls Default() => new PilotControls { gearDown = true, parkingBrake = true };
    }

    public static class PilotInputBus
    {
        public static PilotControls Current = PilotControls.Default();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = PilotControls.Default();

        /// <summary>Vị trí cần ga ứng với các nấc (giá trị cần ga -1..1).</summary>
        public const float ThrIdle = 0f, ThrClimb = 0.70f, ThrFlexMct = 0.90f, ThrToga = 1.0f;
    }
}
