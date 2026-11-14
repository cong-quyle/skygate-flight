using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyGate.Rendering.Environment
{
    /// <summary>
    /// Dịch toàn bộ thế giới về gốc khi máy bay đi xa (chống rung hình do sai số float).
    /// Chỉ dịch X và Z (mặt đất luôn ở y = 0). Offset cộng dồn kiểu double để tính toạ độ địa lý.
    /// Hệ hạt/trail dùng world space và mọi cache vị trí phải nghe <see cref="Shifted"/>.
    /// </summary>
    public sealed class FloatingOrigin : MonoBehaviour
    {
        public Transform target;
        public float threshold = 2000f;

        public static event Action<Vector3> Shifted;
        /// <summary>Toạ độ thế giới thật của gốc Unity hiện tại (m).</summary>
        public static double OriginX { get; private set; }
        public static double OriginZ { get; private set; }

        readonly System.Collections.Generic.List<GameObject> _roots = new System.Collections.Generic.List<GameObject>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Shifted = null; OriginX = OriginZ = 0.0; }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 p = target.position;
            if (p.x * p.x + p.z * p.z < threshold * threshold) return;
            Shift(new Vector3(-p.x, 0f, -p.z));
        }

        public void Shift(Vector3 delta)
        {
            _roots.Clear();
            SceneManager.GetActiveScene().GetRootGameObjects(_roots);
            for (int i = 0; i < _roots.Count; i++) _roots[i].transform.position += delta;
            OriginX -= delta.x;
            OriginZ -= delta.z;
            Physics.SyncTransforms();
            Shifted?.Invoke(delta);
        }
    }
}
