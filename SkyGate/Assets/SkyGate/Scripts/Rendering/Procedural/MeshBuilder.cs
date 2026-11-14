using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkyGate.Rendering.Procedural
{
    /// <summary>
    /// Bộ dựng mesh nhỏ gọn cho model placeholder.
    /// Quy ước quan trọng: MỌI đỉnh đều có normal do người gọi cung cấp; AddTriangle tự đảo thứ tự đỉnh
    /// cho mặt trước khớp với normal (Unity: mặt trước có pháp tuyến = Cross(b-a, c-a)).
    /// Nhờ vậy không cần nhớ chiều quấn tam giác, và mesh mặt trong (nội thất) chỉ cần normal hướng vào trong.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _v = new List<Vector3>(1024);
        readonly List<Vector3> _n = new List<Vector3>(1024);
        readonly List<Vector2> _uv = new List<Vector2>(1024);
        readonly List<List<int>> _sub = new List<List<int>> { new List<int>(2048) };
        int _cur;

        public int VertexCount => _v.Count;
        public int SubmeshCount => _sub.Count;

        public int TriangleCount
        {
            get { int t = 0; foreach (var s in _sub) t += s.Count / 3; return t; }
        }

        public MeshBuilder Submesh(int index)
        {
            while (_sub.Count <= index) _sub.Add(new List<int>(256));
            _cur = index;
            return this;
        }

        public int AddVertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(p);
            _n.Add(n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up);
            _uv.Add(uv);
            return _v.Count - 1;
        }

        public Vector3 Position(int i) => _v[i];

        public void AddTriangle(int a, int b, int c)
        {
            Vector3 pa = _v[a], pb = _v[b], pc = _v[c];
            Vector3 fn = Vector3.Cross(pb - pa, pc - pa);
            if (fn.sqrMagnitude < 1e-14f) return; // tam giác suy biến (chóp mũi...)
            Vector3 hint = _n[a] + _n[b] + _n[c];
            var list = _sub[_cur];
            list.Add(a);
            if (Vector3.Dot(fn, hint) >= 0f) { list.Add(b); list.Add(c); }
            else { list.Add(c); list.Add(b); }
        }

        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        /// <summary>Lưới rows×cols: rows = trạm dọc (span/chiều dài), cols = điểm trên tiết diện.</summary>
        public void AddGrid(Vector3[,] p, Vector3[,] n, Vector2[,] uv)
        {
            int rows = p.GetLength(0), cols = p.GetLength(1);
            int start = _v.Count;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    AddVertex(p[r, c], n[r, c], uv[r, c]);
            for (int r = 0; r < rows - 1; r++)
                for (int c = 0; c < cols - 1; c++)
                {
                    int i0 = start + r * cols + c;
                    int i1 = i0 + 1;
                    int i2 = i0 + cols + 1;
                    int i3 = i0 + cols;
                    AddQuad(i0, i1, i2, i3);
                }
        }

        /// <summary>Mặt phẳng lồi/gần lồi: quạt tam giác từ tâm, một normal phẳng.</summary>
        public void AddPolygon(IList<Vector3> pts, Vector3 normal, Vector2 uvScale)
        {
            if (pts.Count < 3) return;
            Vector3 c = Vector3.zero;
            foreach (var p in pts) c += p;
            c /= pts.Count;
            Quaternion toPlane = Quaternion.Inverse(Quaternion.LookRotation(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up));
            int ic = AddVertex(c, normal, Vector2.zero);
            int first = _v.Count;
            foreach (var p in pts)
            {
                Vector3 lp = toPlane * (p - c);
                AddVertex(p, normal, new Vector2(lp.x * uvScale.x, lp.y * uvScale.y));
            }
            for (int i = 0; i < pts.Count; i++)
                AddTriangle(ic, first + i, first + (i + 1) % pts.Count);
        }

        /// <summary>Hộp cạnh cứng 24 đỉnh. UV theo mét (tiện cho texture lặp).</summary>
        public void AddBox(Vector3 center, Vector3 size, Quaternion rot)
        {
            Vector3 h = size * 0.5f;
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
            {
                Vector3 u = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(d, u);
                float hd = Mathf.Abs(Vector3.Dot(h, d));
                float hu = Mathf.Abs(Vector3.Dot(h, u));
                float hw = Mathf.Abs(Vector3.Dot(h, w));
                Vector3 fc = d * hd;
                Vector3 n = rot * d;
                int i0 = AddVertex(center + rot * (fc - u * hu - w * hw), n, new Vector2(-hw, -hu));
                int i1 = AddVertex(center + rot * (fc - u * hu + w * hw), n, new Vector2(hw, -hu));
                int i2 = AddVertex(center + rot * (fc + u * hu + w * hw), n, new Vector2(hw, hu));
                int i3 = AddVertex(center + rot * (fc + u * hu - w * hw), n, new Vector2(-hw, hu));
                AddQuad(i0, i1, i2, i3);
            }
        }

        public void AddBox(Vector3 center, Vector3 size) => AddBox(center, size, Quaternion.identity);

        /// <summary>Khối 8 góc bất kỳ: 0..3 mặt dưới, 4..7 mặt trên (cùng thứ tự vòng).</summary>
        public void AddHexahedron(Vector3[] c)
        {
            Vector3 centroid = Vector3.zero;
            foreach (var p in c) centroid += p;
            centroid /= 8f;
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 },
                new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 },
            };
            foreach (var f in faces)
            {
                Vector3 a = c[f[0]], b = c[f[1]], cc = c[f[2]], d = c[f[3]];
                Vector3 n = Vector3.Cross(b - a, cc - a) + Vector3.Cross(cc - a, d - a);
                Vector3 fc = (a + b + cc + d) * 0.25f;
                if (Vector3.Dot(n, fc - centroid) < 0f) n = -n;
                int i0 = AddVertex(a, n, new Vector2(0, 0));
                int i1 = AddVertex(b, n, new Vector2(1, 0));
                int i2 = AddVertex(cc, n, new Vector2(1, 1));
                int i3 = AddVertex(d, n, new Vector2(0, 1));
                AddQuad(i0, i1, i2, i3);
            }
        }

        /// <summary>
        /// Tiện xoay quanh trục Z cục bộ. profile.x = vị trí dọc trục, profile.y = bán kính.
        /// Normal = (t.r, -t.z) theo chiều đi của profile; đổi chiều đi (hoặc flip) để lật mặt.
        /// Quy tắc nhớ: đi về phía -Z ở mặt ngoài => normal hướng ra ngoài.
        /// </summary>
        public void AddLathe(IList<Vector2> profile, int segments, Matrix4x4 xf, bool flip = false, float vScale = 1f)
        {
            int rows = profile.Count;
            int cols = segments + 1;
            var p = new Vector3[rows, cols];
            var n = new Vector3[rows, cols];
            var uv = new Vector2[rows, cols];
            float acc = 0f;
            for (int r = 0; r < rows; r++)
            {
                Vector2 prev = profile[Mathf.Max(r - 1, 0)];
                Vector2 next = profile[Mathf.Min(r + 1, rows - 1)];
                Vector2 t = (next - prev);
                if (t.sqrMagnitude < 1e-12f) t = new Vector2(-1f, 0f);
                t.Normalize();
                Vector2 n2 = new Vector2(t.y, -t.x); // (z, r)
                if (flip) n2 = -n2;
                if (r > 0) acc += Vector2.Distance(profile[r], profile[r - 1]);
                for (int c = 0; c < cols; c++)
                {
                    float a = c / (float)segments * Mathf.PI * 2f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    Vector3 lp = new Vector3(profile[r].y * ca, profile[r].y * sa, profile[r].x);
                    Vector3 ln = new Vector3(n2.y * ca, n2.y * sa, n2.x);
                    p[r, c] = xf.MultiplyPoint3x4(lp);
                    n[r, c] = xf.MultiplyVector(ln).normalized;
                    uv[r, c] = new Vector2(c / (float)segments, acc * vScale);
                }
            }
            AddGrid(p, n, uv);
        }

        /// <summary>Ống từ a đến b (hai bán kính), có nắp tuỳ chọn.</summary>
        public void AddTube(Vector3 a, Vector3 b, float ra, float rb, int segments, bool caps = true)
        {
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len < 1e-5f) return;
            Quaternion q = Quaternion.LookRotation(axis / len, Mathf.Abs(axis.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
            Matrix4x4 xf = Matrix4x4.TRS(a, q, Vector3.one);
            // đi theo -Z ở mặt ngoài => normal ra ngoài; ở đây đi theo +Z nên flip
            AddLathe(new[] { new Vector2(0f, ra), new Vector2(len, rb) }, segments, xf, flip: true);
            if (caps)
            {
                AddDisk(a, -axis / len, ra, segments);
                AddDisk(b, axis / len, rb, segments);
            }
        }

        public void AddDisk(Vector3 center, Vector3 normal, float radius, int segments)
        {
            var pts = new Vector3[segments];
            Quaternion q = Quaternion.LookRotation(normal, Mathf.Abs(normal.y) > 0.95f ? Vector3.forward : Vector3.up);
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                pts[i] = center + q * new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }
            AddPolygon(pts, normal.normalized, Vector2.one);
        }

        public void AddSphere(Vector3 center, float radius, int segments)
        {
            int rings = Mathf.Max(3, segments / 2);
            var prof = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.PI * i / rings; // 0 = +Z đầu, đi về -Z
                prof[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            }
            AddLathe(prof, segments, Matrix4x4.Translate(center));
        }

        /// <summary>Gộp builder khác vào (giữ chỉ số submesh).</summary>
        public void Append(MeshBuilder src, Matrix4x4 xf, int subOffset = 0)
        {
            int baseIndex = _v.Count;
            for (int i = 0; i < src._v.Count; i++)
            {
                _v.Add(xf.MultiplyPoint3x4(src._v[i]));
                _n.Add(xf.MultiplyVector(src._n[i]).normalized);
                _uv.Add(src._uv[i]);
            }
            for (int s = 0; s < src._sub.Count; s++)
            {
                Submesh(s + subOffset);
                var dst = _sub[s + subOffset];
                foreach (int idx in src._sub[s]) dst.Add(idx + baseIndex);
            }
            _cur = 0;
        }

        /// <summary>Lật qua mặt phẳng X = 0 (cánh phải -> cánh trái). Tự đảo chiều quấn.</summary>
        public void MirrorX()
        {
            for (int i = 0; i < _v.Count; i++)
            {
                var p = _v[i]; p.x = -p.x; _v[i] = p;
                var n = _n[i]; n.x = -n.x; _n[i] = n;
            }
            foreach (var s in _sub)
                for (int i = 0; i < s.Count; i += 3) { int t = s[i + 1]; s[i + 1] = s[i + 2]; s[i + 2] = t; }
        }

        /// <summary>Đổi toạ độ máy bay -> toạ độ cục bộ của pivot (bản lề, trục quay).</summary>
        public void Localize(Vector3 origin, Quaternion rotation)
        {
            Quaternion inv = Quaternion.Inverse(rotation);
            for (int i = 0; i < _v.Count; i++)
            {
                _v[i] = inv * (_v[i] - origin);
                _n[i] = inv * _n[i];
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.indexFormat = _v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.subMeshCount = _sub.Count;
            for (int i = 0; i < _sub.Count; i++) m.SetTriangles(_sub[i], i, false);
            m.RecalculateBounds();
            if (_v.Count > 0) m.RecalculateTangents();
            return m;
        }

        /// <summary>Hệ trục cho pivot: trục X = bản lề, Y gần với yHint.</summary>
        public static Quaternion FrameFromAxes(Vector3 xAxis, Vector3 yHint)
        {
            Vector3 x = xAxis.normalized;
            Vector3 z = Vector3.Cross(x, yHint).normalized;
            Vector3 y = Vector3.Cross(z, x);
            return Quaternion.LookRotation(z, y);
        }
    }
}
