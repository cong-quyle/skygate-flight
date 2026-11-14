using System.Collections.Generic;
using SkyGate.Core;
using UnityEngine;

namespace SkyGate.Rendering.Procedural
{
    /// <summary>
    /// Biên dạng thân A320 theo tham số (s, theta). Dùng chung cho mesh ngoại thất, vỏ trong, livery và cửa.
    /// s: m tính từ chóp mũi. theta: 0 = đáy, tăng về phía +X (phải), PI = đỉnh, 2PI quay về đáy từ phía -X.
    /// Tiết diện là elip: x = hw*sin(theta), y = cy - h*cos(theta) (h = nửa cao phía trên hoặc dưới).
    /// UV thân: u = s/L, v = theta/2PI.
    /// </summary>
    public static class FuselageProfile
    {
        public const float L = A320Data.Length;
        public const float NoseLen = 6.2f;
        public const float TailStart = 25.07f;
        const float HW = A320Data.FuselageHalfWidth;
        const float TopY = 6.02f, BotY = 1.88f, TipY = 3.40f;
        const float EndHW = 0.28f;

        static float SuperF(float t, float p) => Mathf.Pow(1f - Mathf.Pow(1f - Mathf.Clamp01(t), p), 1f / p);

        /// <summary>Bướu bụng (belly fairing) s ∈ [12; 23,5].</summary>
        public static float Bump(float s)
        {
            const float a = 12f, b = 23.5f;
            if (s <= a || s >= b) return 0f;
            float k = Mathf.Sin(Mathf.PI * (s - a) / (b - a));
            return k * k;
        }

        public static void Dims(float s, out float hw, out float top, out float bot)
        {
            s = Mathf.Clamp(s, 0f, L);
            if (s < NoseLen)
            {
                float t = s / NoseLen;
                hw = HW * SuperF(t, 2.2f);
                top = TipY + (TopY - TipY) * SuperF(t, 2.0f);
                bot = TipY - (TipY - BotY) * SuperF(t, 2.6f);
            }
            else if (s < TailStart)
            {
                hw = HW; top = TopY; bot = BotY;
            }
            else
            {
                float t = (s - TailStart) / (L - TailStart);
                float k = t * t * (3f - 2f * t);
                hw = HW * Mathf.Lerp(1f, EndHW / HW, Mathf.Pow(k, 0.9f));
                top = Mathf.Lerp(TopY, 5.28f, t * t);
                bot = Mathf.Lerp(BotY, 4.72f, Mathf.Pow(t, 1.25f));
            }
            bot -= 0.30f * Bump(s);
        }

        public static float CenterY(float s)
        {
            Dims(s, out _, out float top, out float bot);
            return (top + bot) * 0.5f;
        }

        public static Vector3 Surface(float s, float theta)
        {
            Dims(s, out float hw, out float top, out float bot);
            float cy = (top + bot) * 0.5f, ht = (top - bot) * 0.5f;
            float sn = Mathf.Sin(theta), cs = Mathf.Cos(theta);
            float x = hw * sn;
            if (cs > 0f) x *= 1f + 0.08f * Bump(s); // nửa dưới phình ra ở bướu bụng
            float y = cy - ht * cs;
            return new Vector3(x, y, A320Data.NoseZ - Mathf.Clamp(s, 0f, L));
        }

        public static Vector3 Normal(float s, float theta)
        {
            const float ds = 0.02f, dt = 0.01f;
            Vector3 ps = Surface(Mathf.Min(s + ds, L), theta) - Surface(Mathf.Max(s - ds, 0f), theta);
            Vector3 pt = Surface(s, theta + dt) - Surface(s, theta - dt);
            Vector3 n = Vector3.Cross(pt, ps);
            if (n.sqrMagnitude < 1e-10f) return s < L * 0.5f ? Vector3.forward : Vector3.back;
            Vector3 p = Surface(s, theta);
            Vector3 hint = p - new Vector3(0f, CenterY(s), p.z);
            if (Vector3.Dot(n, hint) < 0f) n = -n;
            return n.normalized;
        }

        /// <summary>Nửa bề rộng của elip tại độ cao tuyệt đối yAbs (0 nếu ngoài tiết diện).</summary>
        public static float HalfWidthAt(float s, float yAbs)
        {
            Dims(s, out float hw, out float top, out float bot);
            float cy = (top + bot) * 0.5f;
            float h = yAbs >= cy ? top - cy : cy - bot;
            if (h < 1e-4f) return 0f;
            float k = (yAbs - cy) / h;
            return hw * Mathf.Sqrt(Mathf.Max(0f, 1f - k * k));
        }

        /// <summary>theta ứng với độ cao tuyệt đối yAbs ở phía side (+1 = phải, -1 = trái).</summary>
        public static float ThetaForY(float s, float yAbs, int side)
        {
            Dims(s, out _, out float top, out float bot);
            float cy = (top + bot) * 0.5f, ht = (top - bot) * 0.5f;
            float c = Mathf.Clamp((cy - yAbs) / Mathf.Max(ht, 1e-4f), -1f, 1f);
            float th = Mathf.Acos(c);
            return side >= 0 ? th : 2f * Mathf.PI - th;
        }

        /// <summary>Các trạm s dọc thân. quality: 0 = chi tiết, 1 = trung bình, 2 = thô.</summary>
        public static float[] Stations(int quality)
        {
            int nNose = quality == 0 ? 36 : quality == 1 ? 18 : 8;
            int nMid = quality == 0 ? 19 : quality == 1 ? 10 : 5;
            int nTail = quality == 0 ? 28 : quality == 1 ? 14 : 7;
            var list = new List<float>();
            for (int i = 0; i <= nNose; i++) list.Add(NoseLen * Mathf.Pow(i / (float)nNose, 1.7f));
            for (int i = 1; i <= nMid; i++) list.Add(Mathf.Lerp(NoseLen, TailStart, i / (float)nMid));
            for (int i = 1; i <= nTail; i++)
            {
                float t = i / (float)nTail;
                list.Add(TailStart + (L - TailStart) * (1f - Mathf.Pow(1f - t, 1.5f)));
            }
            return list.ToArray();
        }

        public static int Segments(int quality) => quality == 0 ? 72 : quality == 1 ? 28 : 14;
    }
}
