using UnityEngine;

namespace SkyGate.Core
{
    /// <summary>
    /// Số liệu công khai của A320 dùng cho hình ảnh và làm mốc hiệu chỉnh.
    /// HỆ TOẠ ĐỘ MÁY BAY (Unity): 1 đơn vị = 1 m, +Z = mũi, +Y = lên, +X = cánh phải.
    /// Gốc: y = 0 là mặt đất khi càng thả, x = 0 là trục giữa, z = 0 gần trọng tâm (~25% MAC).
    /// "s" = khoảng cách dọc thân tính từ chóp mũi (0) ra đuôi (Length). z = NoseZ - s.
    /// "yRel" = độ cao so với tâm thân (FuselageCenterY).
    /// Chỉ dùng trong game, không dùng cho bay thật.
    /// </summary>
    public static class A320Data
    {
        public const float Length = 37.57f;
        public const float Wingspan = 35.80f;        // có sharklet
        public const float Height = 11.76f;
        public const float WingArea = 122.6f;        // m²
        public const float FuselageHalfWidth = 1.975f;
        public const float FuselageHalfHeight = 2.07f;
        public const float FuselageCenterY = 3.95f;
        public const float NoseZ = 17.0f;
        public const float CabinFloorRel = -0.70f;

        public const float WingSemiSpan = 17.05f;    // không tính sharklet
        public const float WingSweepQcDeg = 25f;
        public const float WingDihedralDeg = 5.1f;
        public const float EngineSpanX = 5.75f;
        public const float MainGearTrack = 7.59f;
        public const float Wheelbase = 12.64f;
        public const float MainGearZ = -1.5f;

        public const float AileronMaxDeg = 25f;
        public const float ElevatorUpDeg = 30f;
        public const float ElevatorDownDeg = 17f;
        public const float RudderMaxDeg = 30f;
        public const float SpoilerMaxDeg = 50f;
        public const float NoseSteerMaxDeg = 75f;
        public const float N1RpmAt100 = 5000f;       // CFM56-5B, chỉ để quay quạt

        public static float SToZ(float s) => NoseZ - s;
        public static float ZToS(float z) => NoseZ - z;

        public struct FlapConfig
        {
            public string name; public float slatDeg; public float flapDeg;
            public FlapConfig(string n, float slat, float flap) { name = n; slatDeg = slat; flapDeg = flap; }
        }

        /// <summary>Bảng CONF A320. Chỉ số 1 dùng 1+F (cất cánh).</summary>
        public static readonly FlapConfig[] FlapConfigs =
        {
            new FlapConfig("0", 0f, 0f),
            new FlapConfig("1+F", 18f, 10f),
            new FlapConfig("2", 22f, 15f),
            new FlapConfig("3", 22f, 20f),
            new FlapConfig("FULL", 27f, 40f),
        };

        public static void FlapAngles(float flapPos, out float slatDeg, out float flapDeg)
        {
            flapPos = Mathf.Clamp(flapPos, 0f, FlapConfigs.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(flapPos), FlapConfigs.Length - 2);
            float t = flapPos - i;
            slatDeg = Mathf.Lerp(FlapConfigs[i].slatDeg, FlapConfigs[i + 1].slatDeg, t);
            flapDeg = Mathf.Lerp(FlapConfigs[i].flapDeg, FlapConfigs[i + 1].flapDeg, t);
        }

        public static string FlapName(float lever) =>
            FlapConfigs[Mathf.Clamp(Mathf.RoundToInt(lever), 0, FlapConfigs.Length - 1)].name;
    }
}
