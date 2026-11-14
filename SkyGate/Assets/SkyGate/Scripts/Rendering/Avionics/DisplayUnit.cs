using UnityEngine;

namespace SkyGate.Rendering.Avionics
{
    /// <summary>
    /// Đánh dấu một màn hình (DU) trong buồng lái. cell = ô trong atlas render texture:
    /// 0 = PFD, 1 = ND, 2 = E/WD, 3 = SD. AvionicsSystem gán material runtime cho mọi DU của máy bay.
    /// </summary>
    public sealed class DisplayUnit : MonoBehaviour
    {
        public int cell;
        public Renderer screen;
        /// <summary>Chỉ số material của màn hình trong renderer (0 = viền, 1 = mặt sáng).</summary>
        public int screenMaterialIndex = 1;
    }
}
