using SkyGate.Core;
using UnityEngine;

namespace SkyGate.Rendering.Procedural
{
    /// <summary>Hình học các mặt khí động A320 (số liệu mục 4 của plan). Toạ độ máy bay: +Z mũi, +Y lên, +X cánh phải.</summary>
    public static class A320Geometry
    {
        public const float WingRootX = 0.8f;
        public const float WingTipX = A320Data.WingSemiSpan;   // 17.05
        const float Kink = 6.3f;

        public static float WingChord(float x) =>
            x <= Kink ? Mathf.Lerp(7.2f, 4.35f, x / Kink) : Mathf.Lerp(4.35f, 1.45f, (x - Kink) / (WingTipX - Kink));

        public static Vector3 WingLe(float x)
        {
            float zqc = 3.03f - x * Mathf.Tan(A320Data.WingSweepQcDeg * Mathf.Deg2Rad);
            float y = 2.55f + x * Mathf.Tan(A320Data.WingDihedralDeg * Mathf.Deg2Rad);
            return new Vector3(x, y, zqc + 0.25f * WingChord(x));
        }

        public static Planform Wing()
        {
            var p = new Planform { thickDir = Vector3.up, camberM = 0.02f, camberP = 0.4f };
            float[] xs = { WingRootX, Kink, WingTipX };
            float[] th = { 0.15f, 0.12f, 0.105f };
            for (int i = 0; i < xs.Length; i++)
                p.Add(xs[i], WingLe(xs[i]), WingChord(xs[i]), 2f - 3f * xs[i] / WingTipX, th[i]);
            return p;
        }

        public const float SharkletLen = 2.5f;
        public static readonly Vector3 SharkletAxis = new Vector3(Mathf.Sin(15f * Mathf.Deg2Rad), Mathf.Cos(15f * Mathf.Deg2Rad), 0f);

        public static Planform Sharklet()
        {
            Vector3 le0 = WingLe(WingTipX);
            Vector3 le1 = le0 + SharkletAxis * SharkletLen + new Vector3(0f, 0f, -SharkletLen * Mathf.Tan(35f * Mathf.Deg2Rad));
            var p = new Planform { thickDir = new Vector3(SharkletAxis.y, -SharkletAxis.x, 0f), camberM = 0f };
            p.Add(0f, le0, 1.45f, 0f, 0.10f);
            p.Add(SharkletLen, le1, 0.55f, 0f, 0.08f);
            return p;
        }

        public const float HStabRootX = 0.9f, HStabTipX = 6.225f;

        public static Planform HStab()
        {
            float dx = HStabTipX - HStabRootX;
            var root = new Vector3(HStabRootX, 4.47f, A320Data.NoseZ - 31.5f);
            var tip = new Vector3(HStabTipX, 4.47f + dx * Mathf.Tan(6f * Mathf.Deg2Rad),
                root.z - dx * Mathf.Tan(32f * Mathf.Deg2Rad));
            var p = new Planform { thickDir = Vector3.up, camberM = 0f };
            p.Add(HStabRootX, root, 3.9f, 0f, 0.10f);
            p.Add(HStabTipX, tip, 1.35f, 0f, 0.09f);
            return p;
        }

        public const float VStabRootY = 5.6f, VStabTipY = 11.5f;

        public static Planform VStab()
        {
            float h = VStabTipY - VStabRootY;
            var root = new Vector3(0f, VStabRootY, A320Data.NoseZ - 26.5f);
            var tip = new Vector3(0f, VStabTipY, root.z - h * Mathf.Tan(34f * Mathf.Deg2Rad));
            var p = new Planform { thickDir = Vector3.right, camberM = 0f };
            p.Add(VStabRootY, root, 5.9f, 0f, 0.11f);
            p.Add(VStabTipY, tip, 2.1f, 0f, 0.10f);
            return p;
        }

        // Dải trải của đuôi đứng trong texture đuôi (m), dùng cho cả mesh UV và LiveryBaker
        public static void TailUvBounds(out float zMin, out float zMax, out float yMin, out float yMax)
        {
            var p = VStab();
            var r = p.Sample(VStabRootY); var t = p.Sample(VStabTipY);
            zMax = r.le.z;
            zMin = Mathf.Min(r.le.z - r.chord, t.le.z - t.chord);
            yMin = VStabRootY; yMax = VStabTipY;
        }

        /// <summary>
        /// UV cho đuôi đứng: nửa trái texture [0;0,5] cho mặt -X, nửa phải [0,5;1] cho mặt +X.
        /// Mặt trái đọc từ mũi về đuôi (z giảm), mặt phải đọc từ đuôi về mũi (z tăng) để chữ không bị ngược.
        /// </summary>
        public static Vector2 TailUv(Vector3 p, Vector3 n)
        {
            TailUvBounds(out float zMin, out float zMax, out float yMin, out float yMax);
            float v = Mathf.InverseLerp(yMin, yMax, p.y);
            if (n.x < 0f)
            {
                float u = Mathf.InverseLerp(zMax, zMin, p.z);
                return new Vector2(0.5f * u, v);
            }
            else
            {
                float u = Mathf.InverseLerp(zMin, zMax, p.z);
                return new Vector2(0.5f + 0.5f * u, v);
            }
        }
    }
}
