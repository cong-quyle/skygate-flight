using UnityEngine;

namespace SkyGate.Rendering.Procedural
{
    /// <summary>
    /// Vị trí cửa và cửa sổ A320. Dùng chung cho LiveryBaker (sơn ngoài) và vỏ trong (lỗ alpha),
    /// nên cửa sổ nhìn từ trong luôn trùng ô sơn bên ngoài.
    /// Toạ độ: s (m từ mũi), yRel (m so với tâm thân, +Y lên). Hàm *Sdf trả về khoảng cách có dấu (m), dương = bên trong.
    /// </summary>
    public static class A320Windows
    {
        public struct DoorDef
        {
            public string name; public float s, w, h, yBot; public int side; // -1 trái, +1 phải, 0 cả hai
        }

        public static readonly DoorDef[] Doors =
        {
            new DoorDef { name = "L1", s = 6.1f, w = 0.81f, h = 1.85f, yBot = -0.70f, side = -1 },
            new DoorDef { name = "R1", s = 6.1f, w = 0.81f, h = 1.85f, yBot = -0.70f, side = +1 },
            new DoorDef { name = "ExitA", s = 15.9f, w = 0.51f, h = 1.02f, yBot = -0.08f, side = 0 },
            new DoorDef { name = "ExitB", s = 16.75f, w = 0.51f, h = 1.02f, yBot = -0.08f, side = 0 },
            new DoorDef { name = "L4", s = 31.0f, w = 0.81f, h = 1.85f, yBot = -0.70f, side = -1 },
            new DoorDef { name = "R4", s = 31.0f, w = 0.81f, h = 1.85f, yBot = -0.70f, side = +1 },
            new DoorDef { name = "CargoFwd", s = 8.6f, w = 1.82f, h = 1.24f, yBot = -1.75f, side = +1 },
            new DoorDef { name = "CargoAft", s = 24.5f, w = 1.82f, h = 1.24f, yBot = -1.75f, side = +1 },
        };

        public const float PaxFirstS = 7.25f, PaxPitch = 0.533f, PaxLastS = 30.2f;
        public const float PaxYRel = 0.30f, PaxHalfW = 0.115f, PaxHalfH = 0.165f;

        static readonly Vector2[][] CockpitPanes =
        {
            new[] { new Vector2(1.00f, 0.32f), new Vector2(1.00f, 0.78f), new Vector2(1.92f, 1.12f), new Vector2(1.92f, 0.30f) },
            new[] { new Vector2(1.99f, 0.30f), new Vector2(1.99f, 1.00f), new Vector2(2.55f, 1.00f), new Vector2(2.55f, 0.30f) },
            new[] { new Vector2(2.62f, 0.30f), new Vector2(2.62f, 0.95f), new Vector2(3.05f, 0.80f), new Vector2(3.05f, 0.30f) },
        };

        /// <summary>Hộp bo góc. dx, dy: lệch so với tâm; hx, hy: nửa kích thước; r: bán kính bo.</summary>
        public static float RoundedBoxSdf(float dx, float dy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(dx) - (hx - r), qy = Mathf.Abs(dy) - (hy - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return -(outside + inside - r);
        }

        public static float PassengerWindowSdf(float s, float yRel)
        {
            if (s < PaxFirstS - 0.4f || s > PaxLastS + 0.4f) return -1f;
            int n = Mathf.RoundToInt((PaxLastS - PaxFirstS) / PaxPitch);
            int k = Mathf.Clamp(Mathf.RoundToInt((s - PaxFirstS) / PaxPitch), 0, n);
            float cs = PaxFirstS + k * PaxPitch;
            return RoundedBoxSdf(s - cs, yRel - PaxYRel, PaxHalfW, PaxHalfH, 0.07f);
        }

        public static float DoorSdf(DoorDef d, float s, float yRel)
        {
            return RoundedBoxSdf(s - d.s, yRel - (d.yBot + d.h * 0.5f), d.w * 0.5f, d.h * 0.5f, 0.12f);
        }

        /// <summary>side của điểm trên thân: x &lt; 0 = trái (-1), x &gt;= 0 = phải (+1).</summary>
        public static bool DoorAppliesToSide(DoorDef d, float x) => d.side == 0 || (d.side < 0) == (x < 0f);

        public static float CockpitWindowSdf(float s, float yRel, float x)
        {
            float best = -10f;
            var p = new Vector2(s, yRel);
            for (int i = 0; i < CockpitPanes.Length; i++)
            {
                float d = PolySdf(CockpitPanes[i], p);
                if (i == 0) d = Mathf.Min(d, Mathf.Abs(x) - 0.07f); // trụ giữa kính trước
                best = Mathf.Max(best, d);
            }
            return best;
        }

        /// <summary>SDF của đa giác lồi/lõm đơn giản. Dương = bên trong.</summary>
        public static float PolySdf(Vector2[] poly, Vector2 p)
        {
            float minD = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                minD = Mathf.Min(minD, (p - (a + ab * t)).magnitude);
                if (((a.y > p.y) != (b.y > p.y)) && (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)) inside = !inside;
            }
            return inside ? minD : -minD;
        }

        /// <summary>
        /// Độ phủ lỗ cửa sổ cho vỏ trong (1 = có lỗ). Gồm cửa sổ khách và kính buồng lái.
        /// texelM: kích thước texel theo mét, để khử răng cưa.
        /// </summary>
        public static float HoleCoverage(float s, float yRel, float x, float texelM)
        {
            float d = Mathf.Max(PassengerWindowSdf(s, yRel), CockpitWindowSdf(s, yRel, x));
            return Mathf.Clamp01(0.5f + d / texelM);
        }
    }
}
