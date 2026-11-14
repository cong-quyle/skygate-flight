using UnityEngine;

namespace SkyGate.Core
{
    /// <summary>
    /// Dữ liệu sơn và nội thất của một hãng. Đổi hãng = tạo asset mới, không sửa code.
    /// Màu mặc định là ƯỚC LƯỢNG kiểu VietJet (đỏ/vàng/trắng): cần đối chiếu ảnh thật rồi chỉnh trong Inspector.
    /// Logo và chữ hãng là thương hiệu của VietJet: người dùng tự cung cấp texture, chỉ dùng phi thương mại,
    /// không đưa vào repo công khai.
    /// Vị trí dùng "s" (m tính từ mũi) và "yRel" (m so với tâm thân), xem A320Data.
    /// </summary>
    [CreateAssetMenu(menuName = "SkyGate/Livery Definition", fileName = "Livery_New")]
    public sealed class LiveryDefinition : ScriptableObject
    {
        [Header("Nhận diện")]
        public string displayName = "VietJet-style (placeholder)";
        public string airlineIcao = "VJC";
        public string callsign = "VIETJET";
        public string registration = "VN-A699";   // số đăng ký giả định

        [Header("Thân")]
        public Color fuselageBase = Color.white;
        public Color bellyColor = new Color(0.94f, 0.94f, 0.95f);
        public float bellySplitYRel = -1.10f;
        public Color radomeColor = new Color(0.92f, 0.92f, 0.93f);
        public Color doorLineColor = new Color(0.55f, 0.57f, 0.60f);
        public Color windowColor = new Color(0.05f, 0.06f, 0.08f);

        [Header("Dải trang trí phía đuôi")]
        public bool rearSwoosh = true;
        public Color swooshColor = new Color(0.93f, 0.11f, 0.15f);   // đỏ ~#EE1C25
        public Color swooshAccent = new Color(1.00f, 0.82f, 0.00f);  // vàng ~#FFD200
        public float swooshStartS = 21.5f;
        public float swooshWidth = 0.55f;

        [Header("Chữ hãng trên thân")]
        [Tooltip("Texture chữ/logo do người dùng cung cấp (nền trong suốt). Để trống thì vẽ titleText bằng font khối.")]
        public Texture2D fuselageWordmark;
        public string titleText = "VIETJET AIR";
        public Color titleColor = new Color(0.93f, 0.11f, 0.15f);
        public float titleCenterS = 12.5f;
        public float titleCenterYRel = 0.95f;
        public float titleHeight = 0.85f;

        [Header("Số đăng ký")]
        public Color registrationColor = new Color(0.12f, 0.12f, 0.14f);
        public float registrationS = 30.2f;
        public float registrationYRel = 0.75f;
        public float registrationHeight = 0.42f;

        [Header("Đuôi đứng")]
        public Color tailBase = new Color(0.93f, 0.11f, 0.15f);
        public Color tailAccent = new Color(1.00f, 0.82f, 0.00f);
        public Texture2D tailLogo;
        public float tailLogoScale = 0.55f;

        [Header("Cánh / động cơ")]
        public Color wingColor = new Color(0.80f, 0.82f, 0.84f);
        public Color stabColor = new Color(0.92f, 0.92f, 0.93f);
        public Color nacelleColor = new Color(0.93f, 0.11f, 0.15f);
        public Color sharkletColor = new Color(0.93f, 0.11f, 0.15f);

        [Header("Nội thất")]
        public Color cabinSeatColor = new Color(0.70f, 0.08f, 0.10f);
        public Color cabinHeadrestColor = new Color(0.98f, 0.80f, 0.10f);
        public Color cabinCarpetColor = new Color(0.22f, 0.22f, 0.26f);
        public Color cabinWallColor = new Color(0.93f, 0.93f, 0.91f);
        public Color cabinLightColor = new Color(1.00f, 0.93f, 0.82f);
        public Color cockpitPanelColor = new Color(0.30f, 0.36f, 0.42f);  // xám xanh kiểu Airbus
    }
}
