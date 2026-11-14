using UnityEngine;

namespace SkyGate.Rendering
{
    /// <summary>
    /// Layer dùng cho rendering. Menu "SkyGate/0. Thiết lập dự án" tạo đúng tên ở đúng chỉ số.
    /// - Avionics: canvas màn hình buồng lái, chỉ camera atlas nhìn thấy.
    /// - Interior: buồng lái + khoang khách, chỉ vẽ khi camera ở trong máy bay.
    /// - ExteriorHull: vỏ thân, KHÔNG vẽ (và không đổ bóng) khi camera ở trong, để nắng lọt qua lỗ cửa sổ.
    /// </summary>
    public static class SkyLayers
    {
        public const string AvionicsName = "Avionics";
        public const string InteriorName = "Interior";
        public const string HullName = "ExteriorHull";
        public const int AvionicsDefault = 8, InteriorDefault = 9, HullDefault = 10;

        static int _avionics = -2, _interior = -2, _hull = -2;

        public static int Avionics => Resolve(ref _avionics, AvionicsName, AvionicsDefault);
        public static int Interior => Resolve(ref _interior, InteriorName, InteriorDefault);
        public static int Hull => Resolve(ref _hull, HullName, HullDefault);

        static int Resolve(ref int cache, string name, int fallback)
        {
            if (cache != -2) return cache;
            int l = LayerMask.NameToLayer(name);
            cache = l >= 0 ? l : fallback;
            return cache;
        }

        public static int Bit(int layer) => 1 << layer;

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }
    }
}
