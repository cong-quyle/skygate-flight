using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyGate.Rendering.Procedural
{
    /// <summary>
    /// Mặt khí động dạng loft (cánh, đuôi, sharklet, đuôi đứng) theo các trạm (station).
    /// - eta: tham số dọc sải (m). Cánh/đuôi ngang: eta = x. Đuôi đứng: eta = y. Sharklet: eta = chiều dài dọc trục.
    /// - Mỗi trạm có điểm mép trước (le), dây cung, góc xoắn (độ, + = mép trước ngẩng lên) và độ dày tương đối.
    /// - Dây cung luôn hướng về -Z (đuôi). thickDir là hướng "lên" của biên dạng ở bên PHẢI (x &gt; 0);
    ///   bên trái lấy đối xứng qua mặt phẳng x = 0.
    /// </summary>
    public sealed class Planform
    {
        public struct Station
        {
            public float eta; public Vector3 le; public float chord, twistDeg, thick;
        }

        public readonly List<Station> stations = new List<Station>();
        public Vector3 thickDir = Vector3.up;
        public float camberM = 0.02f, camberP = 0.4f;

        public Planform Add(float eta, Vector3 le, float chord, float twistDeg, float thick)
        {
            stations.Add(new Station { eta = eta, le = le, chord = chord, twistDeg = twistDeg, thick = thick });
            return this;
        }

        public Station Sample(float eta)
        {
            if (stations.Count == 1) return stations[0];
            int i = 0;
            while (i < stations.Count - 2 && eta > stations[i + 1].eta) i++;
            var a = stations[i]; var b = stations[i + 1];
            float t = Mathf.Clamp01((eta - a.eta) / Mathf.Max(1e-6f, b.eta - a.eta));
            return new Station
            {
                eta = eta,
                le = Vector3.Lerp(a.le, b.le, t),
                chord = Mathf.Lerp(a.chord, b.chord, t),
                twistDeg = Mathf.Lerp(a.twistDeg, b.twistDeg, t),
                thick = Mathf.Lerp(a.thick, b.thick, t),
            };
        }

        Vector3 ThickDirSide(float side)
        {
            var td = thickDir; td.x *= side; return td;
        }

        /// <summary>Điểm 3D trên mặt cắt: xc = vị trí dọc dây cung (0..1), yc = độ cao theo đơn vị dây cung.</summary>
        public Vector3 Point(float side, float eta, float xc, float yc)
        {
            var s = Sample(eta);
            Vector3 le = s.le; le.x *= side;
            Vector3 off = Vector3.back * (xc * s.chord) + ThickDirSide(side) * (yc * s.chord);
            if (Mathf.Abs(s.twistDeg) > 1e-4f)
            {
                Vector3 q = Vector3.back * (0.25f * s.chord);
                off = q + Quaternion.Euler(-s.twistDeg, 0f, 0f) * (off - q);
            }
            return le + off;
        }

        public float UpperY(float eta, float x) =>
            Airfoil.Camber(x, camberM, camberP) + Airfoil.HalfThickness(x, Sample(eta).thick);

        public float LowerY(float eta, float x) =>
            Airfoil.Camber(x, camberM, camberP) - Airfoil.HalfThickness(x, Sample(eta).thick);

        public List<float> Rows(float e0, float e1, float step)
        {
            var bp = new List<float> { e0 };
            foreach (var s in stations)
                if (s.eta > e0 + 1e-4f && s.eta < e1 - 1e-4f) bp.Add(s.eta);
            bp.Add(e1);
            var rows = new List<float> { e0 };
            for (int i = 0; i < bp.Count - 1; i++)
            {
                float a = bp[i], b = bp[i + 1];
                int n = Mathf.Max(1, Mathf.CeilToInt((b - a) / step));
                for (int k = 1; k <= n; k++) rows.Add(Mathf.Lerp(a, b, k / (float)n));
            }
            return rows;
        }

        /// <summary>Đường bản lề (tại dây cung chordFrac, cao yc) từ eta e0 đến e1: trả về điểm giữa và trục (hướng +X nếu là cánh).</summary>
        public void HingeFrame(float side, float e0, float e1, float chordFrac, float yc, out Vector3 origin, out Vector3 axis)
        {
            Vector3 h0 = Point(side, e0, chordFrac, yc);
            Vector3 h1 = Point(side, e1, chordFrac, yc);
            origin = (h0 + h1) * 0.5f;
            axis = h1 - h0;
            if (axis.x < 0f) axis = -axis;
            axis.Normalize();
        }

        Vector3 Pt(float side, float eta, float x, bool upper)
        {
            float y = Airfoil.Camber(x, camberM, camberP) + (upper ? 1f : -1f) * Airfoil.HalfThickness(x, Sample(eta).thick);
            return Point(side, eta, x, y);
        }

        void Strip(MeshBuilder mb, List<float> etas, Vector3[] center, int cols, Func<int, int, Vector3> pt,
            Func<Vector3, Vector3, Vector2> uvFn)
        {
            int R = etas.Count;
            var p = new Vector3[R, cols];
            var uv = new Vector2[R, cols];
            for (int r = 0; r < R; r++)
                for (int c = 0; c < cols; c++)
                {
                    p[r, c] = pt(r, c);
                    uv[r, c] = new Vector2(c / (float)(cols - 1), etas[r] * 0.1f);
                }
            SurfaceUtil.AddSmoothGrid(mb, p, uv, center, uvFn);
        }

        /// <summary>
        /// Dựng một mảnh: dải eta [e0,e1] × dây cung [a,b]. Tạo mặt trên, mặt dưới, mặt bịt trước (a &gt; 0), mặt bịt sau và nắp hai đầu.
        /// Dùng cho thân cánh, slat, flap, aileron, đuôi, sharklet.
        /// </summary>
        public void BuildPiece(MeshBuilder mb, float side, float e0, float e1, float a, float b,
            float step = 2f, int nChord = 10, bool capA = true, bool capB = true,
            Func<Vector3, Vector3, Vector2> uvFn = null)
        {
            var etas = Rows(e0, e1, step);
            int R = etas.Count;
            float[] xs = Airfoil.Spacing(a, b, nChord);
            int N = xs.Length;
            bool wrap = a < 1e-4f;
            float xm = (a + b) * 0.5f;
            var center = new Vector3[R];
            for (int r = 0; r < R; r++)
                center[r] = Point(side, etas[r], xm, Airfoil.Camber(xm, camberM, camberP));

            if (wrap)
            {
                int cols = 2 * (N - 1) + 1;
                Strip(mb, etas, center, cols,
                    (r, c) => c <= N - 1 ? Pt(side, etas[r], xs[N - 1 - c], false) : Pt(side, etas[r], xs[c - (N - 1)], true),
                    uvFn);
            }
            else
            {
                Strip(mb, etas, center, N, (r, c) => Pt(side, etas[r], xs[c], true), uvFn);
                Strip(mb, etas, center, N, (r, c) => Pt(side, etas[r], xs[N - 1 - c], false), uvFn);
                Strip(mb, etas, center, 2, (r, c) => Pt(side, etas[r], xs[0], c == 1), uvFn);
            }
            Strip(mb, etas, center, 2, (r, c) => Pt(side, etas[r], xs[N - 1], c == 0), uvFn);

            if (capA) Cap(mb, side, e0, e0, e1, xs, wrap, -1f);
            if (capB) Cap(mb, side, e1, e0, e1, xs, wrap, +1f);
        }

        void Cap(MeshBuilder mb, float side, float eta, float e0, float e1, float[] xs, bool wrap, float sign)
        {
            int N = xs.Length;
            var ring = new List<Vector3>();
            if (wrap)
            {
                for (int i = N - 1; i >= 0; i--) ring.Add(Pt(side, eta, xs[i], false));
                for (int i = 1; i < N; i++) ring.Add(Pt(side, eta, xs[i], true));
            }
            else
            {
                for (int i = 0; i < N; i++) ring.Add(Pt(side, eta, xs[i], true));
                for (int i = N - 1; i >= 0; i--) ring.Add(Pt(side, eta, xs[i], false));
            }
            Vector3 n = Vector3.Cross(Vector3.back, ThickDirSide(side)).normalized;
            Vector3 probe = Point(side, e1, 0.5f, 0f) - Point(side, e0, 0.5f, 0f);
            if (Vector3.Dot(n, probe) < 0f) n = -n;
            mb.AddPolygon(ring, n * sign, new Vector2(1f, 1f));
        }
    }
}
