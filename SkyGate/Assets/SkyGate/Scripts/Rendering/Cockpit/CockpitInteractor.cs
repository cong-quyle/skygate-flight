using SkyGate.Rendering.Cameras;
using SkyGate.Rendering.Controls;
using UnityEngine;

namespace SkyGate.Rendering.Cockpit
{
    /// <summary>
    /// Raycast từ camera chính tới nút/cần trong buồng lái (CockpitControl) khi chạm. Chỉ hoạt động ở chế độ Cockpit.
    /// Bỏ qua điểm chạm nằm trên HUD cảm ứng hoặc đã là cử chỉ kéo (nhìn quanh): chỉ coi là "bấm" khi nhả tay gần chỗ chạm xuống.
    /// Sự kiện phát qua CockpitControl.Pressed(id), mô phỏng tự lắng nghe.
    /// </summary>
    public sealed class CockpitInteractor : MonoBehaviour
    {
        public CameraDirector director;
        public float maxDistance = 3f;
        public float tapMaxPixels = 18f;
        public LayerMask mask = ~0;

        readonly PointerState[] _buf = new PointerState[SkyInput.MaxPointers];
        Vector2 _downPos; int _downId = int.MinValue;

        void Update()
        {
            if (director == null) director = FindFirstObjectByType<CameraDirector>();
            if (director == null || director.mode != CameraMode.Cockpit || director.cam == null) return;
            int n = SkyInput.GetPointers(_buf);
            var hud = TouchFlightControls.Instance;
            for (int i = 0; i < n; i++)
            {
                var p = _buf[i];
                if (p.id <= -2) continue;
                if (p.pressed && (hud == null || !hud.BlocksPointer(p.pos))) { _downId = p.id; _downPos = p.pos; }
                if (p.released && p.id == _downId)
                {
                    _downId = int.MinValue;
                    if ((p.pos - _downPos).magnitude > tapMaxPixels) continue;
                    Ray ray = director.cam.ScreenPointToRay(p.pos);
                    if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask, QueryTriggerInteraction.Collide))
                    {
                        var ctrl = hit.collider.GetComponentInParent<CockpitControl>();
                        if (ctrl != null) ctrl.Press();
                    }
                }
            }
        }
    }
}
