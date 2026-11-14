using System.Collections.Generic;
using SkyGate.Core;
using SkyGate.Rendering.Aircraft;
using SkyGate.Rendering.Avionics;
using SkyGate.Rendering.Controls;
using SkyGate.Rendering.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkyGate.Rendering.Cameras
{
    public enum CameraMode { Exterior, Cockpit, Cabin, Tower }

    /// <summary>
    /// MỘT camera chính, đổi vị trí/culling/near-far/FOV theo chế độ (plan mục 10).
    /// - Exterior: orbit quanh máy bay (kéo xoay, pinch/cuộn zoom).
    /// - Cockpit/Cabin: nhìn quanh từ điểm mắt (kéo), pinch/cuộn đổi FOV.
    /// - Tower: đứng ở điểm tháp nhìn theo máy bay, FOV tự co.
    /// Trong chế độ nhìn từ trong: bỏ layer ExteriorHull khỏi culling và tắt đổ bóng vỏ ngoài để nắng lọt qua cửa sổ.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CameraDirector : MonoBehaviour
    {
        public Camera cam;
        public AircraftStateBus bus;
        public Transform tower;
        public CameraMode mode = CameraMode.Exterior;

        [Header("Exterior")]
        public float distance = 75f, minDistance = 15f, maxDistance = 300f;
        public float yaw = 150f, pitch = 14f;
        [Header("Nhìn quanh (độ / pixel)")]
        public float lookSensitivity = 0.18f;
        public float fovCockpit = 62f;

        Transform _aircraft, _eyeCpt;
        Transform[] _cabinCams;
        int _cabinIndex;
        Renderer[] _hull;
        AvionicsSystem _avionics;
        float _lookYaw, _lookPitch;
        float _dist, _yawS, _pitchS;
        Vector3 _smoothTarget;
        bool _init;

        readonly PointerState[] _buf = new PointerState[SkyInput.MaxPointers];
        readonly List<int> _mine = new List<int>(2);
        float _prevPinch = -1f;

        static readonly (float yaw, float pitch)[] Presets = { (0f, 12f), (0f, -55f), (0f, 45f), (-85f, 5f), (85f, 5f) };
        int _preset;

        void OnEnable() { FloatingOrigin.Shifted += OnShift; }
        void OnDisable() { FloatingOrigin.Shifted -= OnShift; }
        void OnShift(Vector3 d) { _smoothTarget += d; }

        void Start() { Init(); SetMode(mode); }

        void Init()
        {
            if (_init) return;
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (bus == null) bus = FindFirstObjectByType<AircraftStateBus>();
            if (bus != null)
            {
                _aircraft = bus.transform;
                _eyeCpt = A320RigDefaults.FindDeep(_aircraft, AircraftPartNames.EyeCpt);
                var list = new List<Transform>();
                foreach (string n in new[] { AircraftPartNames.CamCabin12A, AircraftPartNames.CamCabin20F, AircraftPartNames.CamCabinAisle })
                {
                    var t = A320RigDefaults.FindDeep(_aircraft, n);
                    if (t != null) list.Add(t);
                }
                _cabinCams = list.ToArray();
                var hull = A320RigDefaults.FindDeep(_aircraft, AircraftPartNames.Fuselage);
                _hull = hull != null ? hull.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                _avionics = _aircraft.GetComponent<AvionicsSystem>();
            }
            _dist = distance; _yawS = yaw; _pitchS = pitch;
            if (_aircraft != null) _smoothTarget = _aircraft.position + Vector3.up * 3f;
            _init = true;
        }

        public void Cycle() => SetMode((CameraMode)(((int)mode + 1) % 4));

        public void CycleSub()
        {
            if (mode == CameraMode.Cabin && _cabinCams != null && _cabinCams.Length > 0)
            {
                _cabinIndex = (_cabinIndex + 1) % _cabinCams.Length;
                _lookYaw = _lookPitch = 0f;
            }
            else if (mode == CameraMode.Cockpit)
            {
                _preset = (_preset + 1) % Presets.Length;
                _lookYaw = Presets[_preset].yaw; _lookPitch = Presets[_preset].pitch;
            }
        }

        public void SetMode(CameraMode m)
        {
            Init();
            mode = m;
            bool inside = m == CameraMode.Cockpit || m == CameraMode.Cabin;
            int avionicsBit = SkyLayers.Bit(SkyLayers.Avionics);
            int interiorBit = SkyLayers.Bit(SkyLayers.Interior);
            int hullBit = SkyLayers.Bit(SkyLayers.Hull);
            int all = ~0;
            if (cam != null)
            {
                cam.cullingMask = inside ? all & ~avionicsBit & ~hullBit : all & ~avionicsBit & ~interiorBit;
                cam.nearClipPlane = inside ? 0.05f : 0.3f;
                cam.farClipPlane = inside ? 15000f : 30000f;
                if (inside) cam.fieldOfView = fovCockpit;
                else if (m == CameraMode.Exterior) cam.fieldOfView = 50f;
            }
            if (_hull != null)
                foreach (var r in _hull)
                    if (r != null) r.shadowCastingMode = inside ? ShadowCastingMode.Off : ShadowCastingMode.On;
            if (_avionics != null) _avionics.SetVisible(m == CameraMode.Cockpit);
            _lookYaw = _lookPitch = 0f;
            _prevPinch = -1f;
            _mine.Clear();
        }

        // ------------------------------------------------------------------ input
        void ReadInput(out Vector2 look, out float zoomRatio)
        {
            look = Vector2.zero;
            zoomRatio = 1f;
            int n = SkyInput.GetPointers(_buf);
            var controls = TouchFlightControls.Instance;
            for (int i = 0; i < n; i++)
            {
                var p = _buf[i];
                if (p.pressed && !_mine.Contains(p.id))
                {
                    bool blocked = controls != null && (controls.BlocksPointer(p.pos) || controls.IsPointerOwned(p.id));
                    if (!blocked && _mine.Count < 2) _mine.Add(p.id);
                }
                if (p.released) _mine.Remove(p.id);
            }
            // loại các con trỏ không còn trong danh sách đang giữ
            for (int k = _mine.Count - 1; k >= 0; k--)
            {
                bool found = false;
                for (int i = 0; i < n; i++) if (_buf[i].id == _mine[k] && _buf[i].down) { found = true; break; }
                if (!found) _mine.Remove(_mine[k]);
            }

            if (_mine.Count == 1)
            {
                for (int i = 0; i < n; i++) if (_buf[i].id == _mine[0]) look = _buf[i].delta;
                _prevPinch = -1f;
            }
            else if (_mine.Count == 2)
            {
                Vector2 a = Vector2.zero, b = Vector2.zero;
                for (int i = 0; i < n; i++)
                {
                    if (_buf[i].id == _mine[0]) a = _buf[i].pos;
                    if (_buf[i].id == _mine[1]) b = _buf[i].pos;
                }
                float d = Vector2.Distance(a, b);
                if (_prevPinch > 0f && d > 1f) zoomRatio = d / _prevPinch;
                _prevPinch = d;
            }
            else _prevPinch = -1f;

            float scroll = SkyInput.Scroll();
            if (Mathf.Abs(scroll) > 0.001f) zoomRatio *= 1f + scroll * 0.1f;

            if (SkyInput.KeyDown(SkyKey.C)) Cycle();
            if (SkyInput.KeyDown(SkyKey.V)) CycleSub();
        }

        // ------------------------------------------------------------------ cập nhật
        void LateUpdate()
        {
            if (!_init) Init();
            if (cam == null || _aircraft == null) return;
            ReadInput(out Vector2 look, out float zoom);
            float dt = Time.unscaledDeltaTime;

            switch (mode)
            {
                case CameraMode.Exterior: UpdateExterior(look, zoom, dt); break;
                case CameraMode.Cockpit: UpdateInside(_eyeCpt, look, zoom); break;
                case CameraMode.Cabin:
                    UpdateInside(_cabinCams != null && _cabinCams.Length > 0 ? _cabinCams[_cabinIndex] : _eyeCpt, look, zoom);
                    break;
                case CameraMode.Tower: UpdateTower(); break;
            }
        }

        void UpdateExterior(Vector2 look, float zoom, float dt)
        {
            yaw += look.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, -5f, 85f);
            distance = Mathf.Clamp(distance / Mathf.Max(0.2f, zoom), minDistance, maxDistance);
            float k = 1f - Mathf.Exp(-10f * dt);
            _yawS = Mathf.LerpAngle(_yawS, yaw, k);
            _pitchS = Mathf.Lerp(_pitchS, pitch, k);
            _dist = Mathf.Lerp(_dist, distance, k);
            _smoothTarget = _aircraft.position + Vector3.up * 3f;
            Quaternion rot = Quaternion.Euler(_pitchS, _yawS, 0f);
            Vector3 pos = _smoothTarget - rot * Vector3.forward * _dist;
            if (pos.y < 1f) pos.y = 1f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(_smoothTarget - pos, Vector3.up));
        }

        void UpdateInside(Transform anchor, Vector2 look, float zoom)
        {
            if (anchor == null) return;
            _lookYaw = Mathf.Clamp(_lookYaw - look.x * lookSensitivity, -150f, 150f);
            _lookPitch = Mathf.Clamp(_lookPitch - look.y * lookSensitivity, -70f, 80f);
            fovCockpit = Mathf.Clamp(fovCockpit / Mathf.Max(0.2f, zoom), 30f, 75f);
            cam.fieldOfView = fovCockpit;
            cam.transform.SetPositionAndRotation(anchor.position, anchor.rotation * Quaternion.Euler(_lookPitch, _lookYaw, 0f));
        }

        void UpdateTower()
        {
            Vector3 towerPos = tower != null ? tower.position : _aircraft.position + new Vector3(-400f, 25f, -250f);
            Vector3 target = _aircraft.position + Vector3.up * 3f;
            Vector3 dir = target - towerPos;
            float dist = Mathf.Max(30f, dir.magnitude);
            cam.transform.SetPositionAndRotation(towerPos, Quaternion.LookRotation(dir, Vector3.up));
            cam.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(66f / dist) * Mathf.Rad2Deg, 4f, 40f);
        }
    }
}
