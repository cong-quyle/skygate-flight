using System;

namespace SkyGate.Core
{
    /// <summary>Cờ đèn ngoài/trong máy bay. AircraftSystems ghi, AircraftLights đọc.</summary>
    [Flags]
    public enum AircraftLightFlags
    {
        None = 0,
        Nav = 1 << 0,
        Beacon = 1 << 1,
        Strobe = 1 << 2,
        Landing = 1 << 3,
        Taxi = 1 << 4,
        Takeoff = 1 << 5,
        Logo = 1 << 6,
        Wing = 1 << 7,
        RunwayTurnoff = 1 << 8,
        Cabin = 1 << 9,
        CockpitFlood = 1 << 10,
    }

    /// <summary>
    /// Toàn bộ dữ liệu mà lớp HÌNH ẢNH cần để vẽ một chiếc máy bay.
    /// Quy tắc: lớp mô phỏng (FlightModel, AircraftSystems...) GHI vào struct này;
    /// lớp rendering (rig, đèn, avionics, camera) CHỈ ĐỌC. Không component rendering nào ghi ngược vào mô phỏng.
    /// Đơn vị hàng không: kt, ft, fpm, độ, %, kg.
    /// </summary>
    [Serializable]
    public struct AircraftVisualState
    {
        // --- Tư thế và dữ liệu khí (avionics) ---
        public float pitchDeg;        // + mũi lên
        public float rollDeg;         // + nghiêng phải
        public float headingDeg;      // 0..360 từ Bắc, theo chiều kim đồng hồ
        public float trackDeg;
        public float iasKt, tasKt, gsKt, mach;
        public float iasTrendKt;      // tốc độ dự đoán sau 10 s trừ tốc độ hiện tại
        public float altitudeFt, vsFpm, radioAltFt;
        public float baroHpa;

        // --- FCU / mốc tốc độ ---
        public float selSpeedKt, selHeadingDeg, selAltitudeFt;
        public float v1Kt, vrKt, v2Kt;

        // --- Bề mặt điều khiển (chuẩn hoá) ---
        public float aileron;         // -1..1, + = lệnh nghiêng phải
        public float elevator;        // -1..1, + = kéo cần (mũi lên)
        public float rudder;          // -1..1, + = đạp phải (mũi sang phải)
        public float speedbrake;      // 0..1
        public float groundSpoilers;  // 0..1
        public float flapPos;         // 0..4 liên tục: CONF 0, 1+F, 2, 3, FULL (đã nội suy theo thời gian)
        public float flapLever;       // 0..4 vị trí cần flap trong buồng lái
        public float stabTrimDeg;

        // --- Càng và bánh ---
        public float gearPos;         // 0 = thu hết, 1 = thả hết (đã nội suy theo thời gian)
        public float gearLever;       // 0 = UP, 1 = DOWN
        public float noseSteerDeg;    // + = rẽ phải
        public float wheelRpmNose, wheelRpmMain;

        // --- Động cơ (1 = trái, 2 = phải) ---
        public float n1Eng1, n1Eng2;             // %
        public float egtEng1, egtEng2;           // °C
        public float ffEng1, ffEng2;             // kg/h
        public float reverserEng1, reverserEng2; // 0..1
        public float thrustLever1, thrustLever2; // -1 (reverse max) .. 0 (idle) .. 1 (TOGA)
        public float fuelKg;
        public float grossWeightKg;

        // --- Cửa: 0 = đóng, 1 = mở ---
        public float doorL1, doorR1, doorL4, doorR4, doorCargoFwd, doorCargoAft;

        public AircraftLightFlags lights;
        public bool onGround;

        public bool HasLight(AircraftLightFlags f) => (lights & f) != 0;

        public static AircraftVisualState Parked()
        {
            return new AircraftVisualState
            {
                headingDeg = 290f, trackDeg = 290f, baroHpa = 1013f,
                selSpeedKt = 147f, selHeadingDeg = 290f, selAltitudeFt = 5000f,
                v1Kt = 141f, vrKt = 143f, v2Kt = 147f,
                gearPos = 1f, gearLever = 1f,
                fuelKg = 6400f, grossWeightKg = 64000f,
                egtEng1 = 15f, egtEng2 = 15f,
                lights = AircraftLightFlags.Nav | AircraftLightFlags.Cabin | AircraftLightFlags.Logo,
                onGround = true,
            };
        }
    }
}
