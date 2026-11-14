using System;
using UnityEngine;

namespace SkyGate.Rendering.Procedural
{
    public static class SurfaceUtil
    {
        /// <summary>
        /// Thêm một lưới điểm rows×cols vào MeshBuilder với normal tính bằng sai phân hữu hạn.
        /// Chiều normal được chọn theo hướng "ra ngoài" = (điểm - rowCenter[row]).
        /// Nhờ vậy không cần quan tâm chiều quấn lưới hay mặt trái/phải.
        /// uvFn (tuỳ chọn): tính UV từ (vị trí, normal) thay cho uv truyền vào.
        /// </summary>
        public static void AddSmoothGrid(MeshBuilder mb, Vector3[,] p, Vector2[,] uv, Vector3[] rowCenter,
            Func<Vector3, Vector3, Vector2> uvFn = null)
        {
            int rows = p.GetLength(0), cols = p.GetLength(1);
            var n = new Vector3[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                int r0 = Mathf.Max(r - 1, 0), r1 = Mathf.Min(r + 1, rows - 1);
                for (int c = 0; c < cols; c++)
                {
                    int c0 = Mathf.Max(c - 1, 0), c1 = Mathf.Min(c + 1, cols - 1);
                    Vector3 dr = p[r1, c] - p[r0, c];
                    Vector3 dc = p[r, c1] - p[r, c0];
                    Vector3 nn = Vector3.Cross(dc, dr);
                    Vector3 hint = p[r, c] - rowCenter[r];
                    if (nn.sqrMagnitude < 1e-14f) nn = hint;
                    else if (Vector3.Dot(nn, hint) < 0f) nn = -nn;
                    if (nn.sqrMagnitude < 1e-14f) nn = Vector3.up;
                    n[r, c] = nn.normalized;
                    if (uvFn != null) uv[r, c] = uvFn(p[r, c], n[r, c]);
                }
            }
            mb.AddGrid(p, n, uv);
        }
    }
}
