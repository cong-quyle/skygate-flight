using UnityEngine;

namespace SkyGate.Rendering.Procedural
{
    /// <summary>NACA 4 chữ số. x tính theo đơn vị dây cung (0 = mép trước, 1 = mép sau), kết quả y cũng theo dây cung.</summary>
    public static class Airfoil
    {
        public static float HalfThickness(float x, float t)
        {
            x = Mathf.Clamp01(x);
            return 5f * t * (0.2969f * Mathf.Sqrt(x) - 0.1260f * x - 0.3516f * x * x
                             + 0.2843f * x * x * x - 0.1015f * x * x * x * x);
        }

        public static float Camber(float x, float m, float p)
        {
            if (m <= 0f) return 0f;
            x = Mathf.Clamp01(x);
            if (x < p) return m / (p * p) * (2f * p * x - x * x);
            return m / ((1f - p) * (1f - p)) * ((1f - 2f * p) + 2f * p * x - x * x);
        }

        /// <summary>Các vị trí dây cung từ a đến b (n+1 điểm). Gần mép trước (a = 0) dùng phân bố cosine cho mượt.</summary>
        public static float[] Spacing(float a, float b, int n)
        {
            var xs = new float[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = i / (float)n;
                if (a < 1e-4f) s = 1f - Mathf.Cos(0.5f * Mathf.PI * s);
                xs[i] = Mathf.Lerp(a, b, s);
            }
            return xs;
        }
    }
}
