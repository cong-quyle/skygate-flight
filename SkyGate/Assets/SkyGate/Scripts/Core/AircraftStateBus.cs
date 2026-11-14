using UnityEngine;

namespace SkyGate.Core
{
    /// <summary>
    /// Điểm nối duy nhất giữa mô phỏng và hình ảnh của MỘT máy bay. Đặt ở root prefab máy bay.
    /// Mô phỏng ghi <see cref="state"/> và các chuỗi FMA; rendering đọc qua tham chiếu tới bus này.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftStateBus : MonoBehaviour
    {
        public AircraftVisualState state = AircraftVisualState.Parked();
        public LiveryDefinition livery;

        [Header("FMA (5 cột PFD)")]
        public string fmaThrust = "MAN TOGA";
        public string fmaVertical = "SRS";
        public string fmaLateral = "RWY";
        public string fmaApproach = "";
        public string fmaEngage = "FD2";
    }
}
