using System;
using System.Collections.Generic;
using SkyGate.Core;
using SkyGate.Rendering.Avionics;
using UnityEngine;
using UnityEngine.UI;

namespace SkyGate.Rendering.Controls
{
    public enum YokeSource { Wheel, Tilt, Both }

    /// <summary>
    /// Điều khiển cảm ứng kiểu buồng lái, vẽ bằng uGUI Overlay nhưng TỰ xử lý đa chạm (không cần EventSystem):
    ///  - Cần ga hình cần gạt chữ T với các nấc REV / IDLE / CLB / FLX-MCT / TOGA (kéo để đặt % động cơ).
    ///  - Yoke: kéo-xoay vô lăng trên màn hình (ngang = roll, dọc = pitch, tự về giữa) HOẶC nghiêng điện thoại (tilt).
    ///  - Cần gear (UP/DOWN) và cần flap 5 nấc (0, 1+F, 2, 3, FULL), cần speedbrake.
    /// Kết quả ghi vào PilotInputBus.Current để mô phỏng đọc. Bàn phím dùng khi thử trong Editor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TouchFlightControls : MonoBehaviour
    {
        public static TouchFlightControls Instance { get; private set; }

        public AircraftStateBus bus;
        public YokeSource yokeSource = YokeSource.Wheel;
        public bool invertPitch;
        public bool invertTilt;
        [Tooltip("Góc xoay (độ) của điện thoại ứng với roll tối đa (bị ghi đè bởi mức nhạy trong CÀI ĐẶT)")] public float tiltRollRangeDeg = 35f;
        [Tooltip("Độ lệch trọng lực (g) ứng với pitch tối đa (bị ghi đè bởi mức nhạy trong CÀI ĐẶT)")] public float tiltPitchRangeG = 0.35f;
        public bool useKeyboard = true;

        // ---- cài đặt được lưu (PlayerPrefs)
        const string PrefYoke = "sg.ctl.yoke", PrefInvPitch = "sg.ctl.invPitch", PrefInvTilt = "sg.ctl.invTilt", PrefSens = "sg.ctl.tiltSens";
        static readonly float[] SensRoll = { 50f, 35f, 25f };     // góc nghiêng cho roll tối đa: thấp / vừa / cao
        static readonly float[] SensPitch = { 0.50f, 0.35f, 0.25f };
        static readonly string[] SensName = { "THẤP", "VỪA", "CAO" };
        int _sens = 1;

        enum Ctl { None, Throttle, Yoke, Gear, Flap, Speedbrake, Button }

        // nấc cần ga trên đường trượt (0 = đáy, 1 = đỉnh)
        const float TIdle = 0.20f, TClimb = 0.60f, TFlex = 0.80f, TToga = 1.0f;

        // --- trạng thái điều khiển
        float _throttleT = TIdle;       // vị trí cần ga trên đường trượt
        float _touchRoll, _touchPitch;  // yoke chạm, tự về 0
        float _tiltRoll, _tiltPitch;
        float _tiltZeroRoll, _tiltZeroG;
        float _gearT = 0f;              // 1 = UP, 0 = DOWN
        int _flap;
        float _speedbrake;

        // --- UI
        RectTransform _thrArea, _thrTrack, _thrHandle, _yokeArea, _yokeVisual, _gearArea, _gearTrack, _gearHandle,
            _flapArea, _flapTrack, _flapHandle, _sbArea, _sbTrack, _sbHandle;
        Text _thrReadout, _yokeReadout;
        Image[] _gearLights = new Image[3];
        readonly List<HudButton> _buttons = new List<HudButton>();
        Canvas _canvas;
        RectTransform _panel;
        bool _panelOpen;
        static readonly HudButton PanelBlocker = new HudButton();   // chạm vào nền bảng cài đặt: nuốt cú chạm

        sealed class HudButton
        {
            public RectTransform rt; public Text text; public Action onClick; public Func<string> labelFn; public bool inPanel;
        }

        readonly PointerState[] _buf = new PointerState[SkyInput.MaxPointers];
        readonly int[] _ownerId = new int[SkyInput.MaxPointers];
        readonly Ctl[] _ownerCtl = new Ctl[SkyInput.MaxPointers];
        readonly bool[] _seen = new bool[SkyInput.MaxPointers];

        public float ThrottleValue => ThrottleFromT(_throttleT);

        // ------------------------------------------------------------------ khởi tạo
        void Awake()
        {
            Instance = this;
            for (int i = 0; i < _ownerId.Length; i++) { _ownerId[i] = int.MinValue; _ownerCtl[i] = Ctl.None; }
            if (bus == null) bus = FindFirstObjectByType<AircraftStateBus>();
            LoadSettings();
            BuildUi();
            var c = PilotInputBus.Current;
            c.parkingBrake = false;
            PilotInputBus.Current = c;
            CalibrateTilt();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static RectTransform Corner(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static readonly Color PanelCol = new Color(0.05f, 0.07f, 0.1f, 0.62f);
        static readonly Color Metal = new Color(0.78f, 0.80f, 0.84f);

        void BuildUi()
        {
            var go = new GameObject("TouchHud", typeof(Canvas), typeof(CanvasScaler));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;

            // ---------- cần ga (trái dưới)
            _thrArea = Corner(root, "Throttle", Vector2.zero, new Vector2(40, 40), new Vector2(190, 470));
            UIFactory.Box(_thrArea, "bg", Vector2.zero, _thrArea.sizeDelta, PanelCol);
            const float trackH = 380f;
            _thrTrack = UIFactory.Rect("track", _thrArea, new Vector2(0, -8), new Vector2(16, trackH));
            UIFactory.Box(_thrTrack, "rail", Vector2.zero, new Vector2(10, trackH), new Color(0.02f, 0.02f, 0.03f, 1f));
            Detent(_thrTrack, trackH, 0f, "REV", AirbusColors.Amber);
            Detent(_thrTrack, trackH, TIdle, "IDLE", Color.white);
            Detent(_thrTrack, trackH, TClimb, "CLB", Color.white);
            Detent(_thrTrack, trackH, TFlex, "FLX/MCT", Color.white);
            Detent(_thrTrack, trackH, TToga, "TOGA", AirbusColors.Amber);
            _thrHandle = UIFactory.Rect("handle", _thrTrack, Vector2.zero, new Vector2(96, 34));
            UIFactory.Box(_thrHandle, "bar", Vector2.zero, new Vector2(96, 30), Metal);
            UIFactory.Box(_thrHandle, "boss", Vector2.zero, new Vector2(30, 42), new Color(0.35f, 0.37f, 0.42f));
            UIFactory.Box(_thrHandle, "grip", new Vector2(0, 0), new Vector2(70, 6), new Color(0.2f, 0.2f, 0.24f));
            _thrReadout = UIFactory.Label(_thrArea, "ro", new Vector2(0, 220), new Vector2(180, 30), 22, AirbusColors.Green, TextAnchor.MiddleCenter, "N1 --");

            // ---------- yoke (phải dưới)
            _yokeArea = Corner(root, "Yoke", new Vector2(1, 0), new Vector2(-40, 40), new Vector2(430, 430));
            _yokeArea.pivot = new Vector2(1, 0);
            UIFactory.Box(_yokeArea, "bg", Vector2.zero, _yokeArea.sizeDelta, new Color(0.05f, 0.07f, 0.1f, 0.45f), UIFactory.Circle());
            _yokeVisual = UIFactory.Rect("wheel", _yokeArea, Vector2.zero, new Vector2(300, 300));
            UIFactory.Box(_yokeVisual, "ring", Vector2.zero, new Vector2(300, 300), new Color(0.75f, 0.78f, 0.82f, 0.9f), UIFactory.Ring());
            UIFactory.Box(_yokeVisual, "bar", Vector2.zero, new Vector2(270, 16), new Color(0.6f, 0.63f, 0.68f, 0.95f));
            UIFactory.Box(_yokeVisual, "gripL", new Vector2(-135, 20), new Vector2(34, 90), new Color(0.15f, 0.16f, 0.2f, 0.95f));
            UIFactory.Box(_yokeVisual, "gripR", new Vector2(135, 20), new Vector2(34, 90), new Color(0.15f, 0.16f, 0.2f, 0.95f));
            UIFactory.Box(_yokeVisual, "hub", Vector2.zero, new Vector2(60, 60), new Color(0.3f, 0.32f, 0.37f, 1f), UIFactory.Circle());
            UIFactory.Box(_yokeVisual, "mark", new Vector2(0, 142), new Vector2(8, 20), AirbusColors.Amber);
            _yokeReadout = UIFactory.Label(_yokeArea, "ro", new Vector2(0, -195), new Vector2(300, 26), 20, Color.white, TextAnchor.MiddleCenter, "");

            // ---------- gear (phải, giữa)
            _gearArea = Corner(root, "Gear", new Vector2(1, 0), new Vector2(-500, 40), new Vector2(110, 300));
            UIFactory.Box(_gearArea, "bg", Vector2.zero, _gearArea.sizeDelta, PanelCol);
            UIFactory.Label(_gearArea, "t", new Vector2(0, 130), new Vector2(100, 24), 18, Color.white, TextAnchor.MiddleCenter, "GEAR");
            _gearTrack = UIFactory.Rect("track", _gearArea, new Vector2(0, -5), new Vector2(14, 190));
            UIFactory.Box(_gearTrack, "rail", Vector2.zero, new Vector2(10, 190), new Color(0.02f, 0.02f, 0.03f, 1f));
            UIFactory.Label(_gearTrack, "up", new Vector2(-38, 80), new Vector2(40, 20), 16, Color.white, TextAnchor.MiddleCenter, "UP");
            UIFactory.Label(_gearTrack, "dn", new Vector2(-38, -80), new Vector2(40, 20), 16, Color.white, TextAnchor.MiddleCenter, "DN");
            _gearHandle = UIFactory.Rect("handle", _gearTrack, Vector2.zero, new Vector2(56, 36));
            UIFactory.Box(_gearHandle, "knob", Vector2.zero, new Vector2(56, 36), Metal);
            UIFactory.Box(_gearHandle, "wheel", Vector2.zero, new Vector2(22, 22), new Color(0.2f, 0.2f, 0.24f), UIFactory.Circle());
            for (int i = 0; i < 3; i++)
                _gearLights[i] = UIFactory.Box(_gearArea, "lamp" + i, new Vector2(-30 + i * 30, -125), new Vector2(18, 18), Color.black, UIFactory.Circle());

            // ---------- flap (phải, giữa)
            _flapArea = Corner(root, "Flap", new Vector2(1, 0), new Vector2(-620, 40), new Vector2(110, 300));
            UIFactory.Box(_flapArea, "bg", Vector2.zero, _flapArea.sizeDelta, PanelCol);
            UIFactory.Label(_flapArea, "t", new Vector2(0, 130), new Vector2(100, 24), 18, Color.white, TextAnchor.MiddleCenter, "FLAPS");
            _flapTrack = UIFactory.Rect("track", _flapArea, new Vector2(0, -5), new Vector2(14, 220));
            UIFactory.Box(_flapTrack, "rail", Vector2.zero, new Vector2(10, 220), new Color(0.02f, 0.02f, 0.03f, 1f));
            string[] fl = { "0", "1+F", "2", "3", "FULL" };
            for (int i = 0; i < 5; i++)
            {
                float y = (0.5f - i / 4f) * 220f;
                UIFactory.Box(_flapTrack, "d" + i, new Vector2(0, y), new Vector2(30, 2), Color.white);
                UIFactory.Label(_flapTrack, "l" + i, new Vector2(-42, y), new Vector2(50, 20), 16, Color.white, TextAnchor.MiddleRight, fl[i]);
            }
            _flapHandle = UIFactory.Rect("handle", _flapTrack, Vector2.zero, new Vector2(48, 30));
            UIFactory.Box(_flapHandle, "knob", Vector2.zero, new Vector2(48, 30), Metal);

            // ---------- speedbrake (trái giữa)
            _sbArea = Corner(root, "Speedbrake", Vector2.zero, new Vector2(250, 40), new Vector2(100, 300));
            UIFactory.Box(_sbArea, "bg", Vector2.zero, _sbArea.sizeDelta, PanelCol);
            UIFactory.Label(_sbArea, "t", new Vector2(0, 130), new Vector2(96, 24), 16, Color.white, TextAnchor.MiddleCenter, "SPD BRK");
            _sbTrack = UIFactory.Rect("track", _sbArea, new Vector2(0, -8), new Vector2(14, 220));
            UIFactory.Box(_sbTrack, "rail", Vector2.zero, new Vector2(10, 220), new Color(0.02f, 0.02f, 0.03f, 1f));
            _sbHandle = UIFactory.Rect("handle", _sbTrack, Vector2.zero, new Vector2(54, 28));
            UIFactory.Box(_sbHandle, "knob", Vector2.zero, new Vector2(54, 28), Metal);

            // ---------- nút trên cùng
            AddButton("CÀI ĐẶT", true, 0, () => _panelOpen = !_panelOpen, () => _panelOpen ? "ĐÓNG CÀI ĐẶT" : "CÀI ĐẶT");
            AddButton("ZERO TILT", true, 1, CalibrateTilt);

            // ---------- bảng cài đặt điều khiển
            _panel = Corner(root, "Settings", new Vector2(1, 1), new Vector2(-30, -90), new Vector2(500, 12 + 5 * 64));
            UIFactory.Box(_panel, "bg", Vector2.zero, _panel.sizeDelta, new Color(0.04f, 0.06f, 0.09f, 0.92f));
            AddPanelButton(0, () => CycleYokeSource(), () => "YOKE: " + YokeName(yokeSource) + (yokeSource != YokeSource.Wheel && !SkyInput.HasTiltSensor ? " (KHÔNG CÓ CẢM BIẾN)" : ""));
            AddPanelButton(1, () => { invertPitch = !invertPitch; SaveSettings(); }, () => "PITCH: " + (invertPitch ? "ĐẢO" : "THƯỜNG"));
            AddPanelButton(2, () => { invertTilt = !invertTilt; SaveSettings(); }, () => "CHIỀU NGHIÊNG: " + (invertTilt ? "ĐẢO" : "THƯỜNG"));
            AddPanelButton(3, () => { _sens = (_sens + 1) % 3; ApplySensitivity(); SaveSettings(); }, () => "ĐỘ NHẠY NGHIÊNG: " + SensName[_sens]);
            AddPanelButton(4, CalibrateTilt, () => "ZERO TILT (lấy tư thế đang cầm làm giữa)");
            _panel.gameObject.SetActive(false);
            ApplyLayoutRefresh();
        }

        static string YokeName(YokeSource s) => s == YokeSource.Wheel ? "CẦN/VÔ LĂNG TRÊN MÀN HÌNH" : s == YokeSource.Tilt ? "NGHIÊNG ĐIỆN THOẠI" : "CẢ HAI";

        void AddPanelButton(int row, Action onClick, Func<string> labelFn)
        {
            var rt = Corner(_panel, "Opt" + row, new Vector2(0, 1), new Vector2(12, -(12 + row * 64)), new Vector2(476, 54));
            UIFactory.Box(rt, "bg", Vector2.zero, rt.sizeDelta, new Color(0.10f, 0.16f, 0.24f, 0.95f));
            var txt = UIFactory.Label(rt, "t", Vector2.zero, rt.sizeDelta, 21, Color.white, TextAnchor.MiddleCenter, labelFn());
            _buttons.Add(new HudButton { rt = rt, text = txt, onClick = onClick, labelFn = labelFn, inPanel = true });
        }

        // ------------------------------------------------------------------ lưu / nạp cài đặt
        void LoadSettings()
        {
            try
            {
                yokeSource = (YokeSource)Mathf.Clamp(PlayerPrefs.GetInt(PrefYoke, (int)yokeSource), 0, 2);
                invertPitch = PlayerPrefs.GetInt(PrefInvPitch, invertPitch ? 1 : 0) != 0;
                invertTilt = PlayerPrefs.GetInt(PrefInvTilt, invertTilt ? 1 : 0) != 0;
                _sens = Mathf.Clamp(PlayerPrefs.GetInt(PrefSens, 1), 0, 2);
            }
            catch (Exception e) { Debug.LogWarning("[SkyGate] Không đọc được cài đặt điều khiển: " + e.Message); }
            ApplySensitivity();
        }

        void SaveSettings()
        {
            try
            {
                PlayerPrefs.SetInt(PrefYoke, (int)yokeSource);
                PlayerPrefs.SetInt(PrefInvPitch, invertPitch ? 1 : 0);
                PlayerPrefs.SetInt(PrefInvTilt, invertTilt ? 1 : 0);
                PlayerPrefs.SetInt(PrefSens, _sens);
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("[SkyGate] Không lưu được cài đặt điều khiển: " + e.Message); }
        }

        void ApplySensitivity() { tiltRollRangeDeg = SensRoll[_sens]; tiltPitchRangeG = SensPitch[_sens]; }

        /// <summary>Đặt nguồn điều khiển yoke từ code/menu khác (có lưu lại).</summary>
        public void SetYokeSource(YokeSource s)
        {
            yokeSource = s;
            if (s != YokeSource.Wheel) CalibrateTilt();
            SaveSettings();
        }

        void ApplyLayoutRefresh()
        {
            SetHandle(_thrHandle, _thrTrack, _throttleT);
            SetHandle(_gearHandle, _gearTrack, _gearT);
            SetHandle(_flapHandle, _flapTrack, 1f - _flap / 4f);
            SetHandle(_sbHandle, _sbTrack, 1f - _speedbrake);
        }

        static void Detent(RectTransform track, float h, float t, string label, Color col)
        {
            float y = (t - 0.5f) * h;
            UIFactory.Box(track, "d" + label, new Vector2(0, y), new Vector2(34, 2), col);
            UIFactory.Label(track, "l" + label, new Vector2(-72, y), new Vector2(90, 22), 17, col, TextAnchor.MiddleRight, label);
        }

        static void SetHandle(RectTransform handle, RectTransform track, float t)
        {
            float h = track.sizeDelta.y;
            handle.anchoredPosition = new Vector2(0, (Mathf.Clamp01(t) - 0.5f) * h);
        }

        // ------------------------------------------------------------------ nút HUD (dùng chung cho DebugHud)
        /// <summary>Thêm nút ở thanh trên cùng. right = true: căn phải. index: thứ tự từ mép màn hình vào trong.</summary>
        public HudButtonHandle AddButton(string label, bool right, int index, Action onClick, Func<string> labelFn = null)
        {
            var root = (RectTransform)_canvas.transform;
            Vector2 anchor = right ? new Vector2(1, 1) : new Vector2(0, 1);
            float x = (right ? -1f : 1f) * (30f + index * 250f);
            var rt = Corner(root, "Btn_" + label, anchor, new Vector2(x, -24f), new Vector2(236, 54));
            UIFactory.Box(rt, "bg", Vector2.zero, rt.sizeDelta, new Color(0.08f, 0.12f, 0.18f, 0.8f));
            var txt = UIFactory.Label(rt, "t", Vector2.zero, rt.sizeDelta, 22, Color.white, TextAnchor.MiddleCenter, labelFn != null ? labelFn() : label);
            var b = new HudButton { rt = rt, text = txt, onClick = onClick, labelFn = labelFn };
            _buttons.Add(b);
            return new HudButtonHandle { text = txt };
        }

        public struct HudButtonHandle { public Text text; }

        // ------------------------------------------------------------------ hiệu chỉnh nghiêng
        public void CalibrateTilt()
        {
            ReadTilt(out float roll, out float g);
            _tiltZeroRoll = roll; _tiltZeroG = g;
        }

        static void ReadTilt(out float rollDeg, out float gz)
        {
            Vector3 g = SkyInput.Gravity();
            bool right = Screen.orientation == ScreenOrientation.LandscapeRight;
            // trục màn hình: landscape-left: u = -y, v = +x; landscape-right: u = +y, v = -x (hệ thiết bị theo chiều dọc)
            float gu = right ? g.y : -g.y;
            float gv = right ? -g.x : g.x;
            rollDeg = Mathf.Atan2(gu, -gv) * Mathf.Rad2Deg;   // + = vô lăng xoay theo chiều kim đồng hồ
            gz = g.z;                                         // + = đỉnh màn hình nghiêng về phía người (kéo cần)
        }

        void CycleYokeSource()
        {
            SetYokeSource((YokeSource)(((int)yokeSource + 1) % 3));
            if (yokeSource != YokeSource.Wheel && !SkyInput.HasTiltSensor)
                Debug.LogWarning("[SkyGate] Thiết bị không có cảm biến gia tốc, chế độ nghiêng sẽ không hoạt động.");
        }

        // ------------------------------------------------------------------ ánh xạ cần ga
        static readonly float[] DetT = { TIdle, TClimb, TFlex, TToga };
        static readonly float[] DetV = { PilotInputBus.ThrIdle, PilotInputBus.ThrClimb, PilotInputBus.ThrFlexMct, PilotInputBus.ThrToga };

        public static float ThrottleFromT(float t)
        {
            if (t < TIdle) return -(TIdle - t) / TIdle;
            for (int i = 0; i < DetT.Length - 1; i++)
                if (t <= DetT[i + 1])
                    return Mathf.Lerp(DetV[i], DetV[i + 1], Mathf.InverseLerp(DetT[i], DetT[i + 1], t));
            return 1f;
        }

        public static float TFromThrottle(float v)
        {
            if (v < 0f) return TIdle + v * TIdle;
            for (int i = 0; i < DetV.Length - 1; i++)
                if (v <= DetV[i + 1])
                    return Mathf.Lerp(DetT[i], DetT[i + 1], Mathf.InverseLerp(DetV[i], DetV[i + 1], v));
            return TToga;
        }

        void SetThrottleT(float t)
        {
            bool canReverse = bus == null || bus.state.onGround;
            if (!canReverse && t < TIdle) t = TIdle;
            foreach (float d in DetT) if (Mathf.Abs(t - d) < 0.025f) { t = d; break; }
            _throttleT = Mathf.Clamp01(t);
        }

        // ------------------------------------------------------------------ vòng lặp
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            int n = SkyInput.GetPointers(_buf);
            for (int i = 0; i < _seen.Length; i++) _seen[i] = false;

            for (int i = 0; i < n; i++)
            {
                var p = _buf[i];
                if (p.id <= -2) continue; // chuột phải dành cho camera
                if (p.pressed && OwnerSlot(p.id) < 0)
                {
                    var c = HitTest(p.pos, out HudButton btn);
                    if (c == Ctl.Button) btn.onClick?.Invoke();
                    else if (c != Ctl.None) Assign(p.id, c);
                }
                int slot = OwnerSlot(p.id);
                if (slot < 0) continue;
                _seen[slot] = true;
                var ctl = _ownerCtl[slot];
                if (p.down) Drag(ctl, p.pos);
                if (p.released) { Release(ctl); Free(slot); }
            }
            for (int s = 0; s < _ownerId.Length; s++)
                if (_ownerCtl[s] != Ctl.None && !_seen[s]) { Release(_ownerCtl[s]); Free(s); }

            // yoke tự về giữa khi không chạm
            bool holding = false;
            for (int s = 0; s < _ownerCtl.Length; s++) if (_ownerCtl[s] == Ctl.Yoke) holding = true;
            if (!holding)
            {
                _touchRoll = Mathf.MoveTowards(_touchRoll, 0f, 3.5f * dt);
                _touchPitch = Mathf.MoveTowards(_touchPitch, 0f, 3.5f * dt);
            }

            Keyboard(dt);
            UpdateTilt(dt);
            Publish();
            RefreshUi();
        }

        int OwnerSlot(int id)
        {
            for (int s = 0; s < _ownerId.Length; s++) if (_ownerCtl[s] != Ctl.None && _ownerId[s] == id) return s;
            return -1;
        }

        void Assign(int id, Ctl c)
        {
            for (int s = 0; s < _ownerId.Length; s++)
                if (_ownerCtl[s] == Ctl.None) { _ownerId[s] = id; _ownerCtl[s] = c; return; }
        }

        void Free(int s) { _ownerCtl[s] = Ctl.None; _ownerId[s] = int.MinValue; }

        public bool IsPointerOwned(int id) => OwnerSlot(id) >= 0;

        /// <summary>Điểm chạm có nằm trên một điều khiển hoặc nút HUD không (camera dùng để bỏ qua cử chỉ).</summary>
        public bool BlocksPointer(Vector2 screenPos) => HitTest(screenPos, out _) != Ctl.None;

        static bool Hit(RectTransform rt, Vector2 pos) => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, null);

        Ctl HitTest(Vector2 pos, out HudButton button)
        {
            button = null;
            foreach (var b in _buttons)
            {
                if (b.inPanel && !_panelOpen) continue;
                if (Hit(b.rt, pos)) { button = b; return Ctl.Button; }
            }
            if (_panelOpen && Hit(_panel, pos)) { button = PanelBlocker; return Ctl.Button; }
            if (Hit(_thrArea, pos)) return Ctl.Throttle;
            if (yokeSource != YokeSource.Tilt && Hit(_yokeArea, pos)) return Ctl.Yoke;   // chế độ NGHIÊNG: vô lăng chỉ để hiển thị
            if (Hit(_gearArea, pos)) return Ctl.Gear;
            if (Hit(_flapArea, pos)) return Ctl.Flap;
            if (Hit(_sbArea, pos)) return Ctl.Speedbrake;
            return Ctl.None;
        }

        static float TrackT(RectTransform track, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, screen, null, out Vector2 local);
            float h = track.sizeDelta.y;
            return Mathf.Clamp01((local.y + h * 0.5f) / h);
        }

        void Drag(Ctl c, Vector2 pos)
        {
            switch (c)
            {
                case Ctl.Throttle: SetThrottleT(TrackT(_thrTrack, pos)); break;
                case Ctl.Gear: _gearT = TrackT(_gearTrack, pos); break;
                case Ctl.Flap: _flap = Mathf.Clamp(Mathf.RoundToInt((1f - TrackT(_flapTrack, pos)) * 4f), 0, 4); break;
                case Ctl.Speedbrake: _speedbrake = 1f - TrackT(_sbTrack, pos); break;
                case Ctl.Yoke:
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_yokeArea, pos, null, out Vector2 local);
                    local -= _yokeArea.rect.center;   // toạ độ local tính từ PIVOT (1,0) của vùng yoke, không phải từ tâm
                    float r = _yokeArea.sizeDelta.x * 0.36f;
                    float x = Mathf.Clamp(local.x / r, -1f, 1f), y = Mathf.Clamp(local.y / r, -1f, 1f);
                    _touchRoll = Shape(x);
                    _touchPitch = Shape(-y) * (invertPitch ? -1f : 1f); // kéo xuống = kéo cần (mũi lên)
                    break;
            }
        }

        void Release(Ctl c)
        {
            if (c == Ctl.Gear) _gearT = _gearT >= 0.5f ? 1f : 0f;
        }

        static float Shape(float v)
        {
            float a = Mathf.Abs(v);
            if (a < 0.05f) return 0f;
            a = (a - 0.05f) / 0.95f;
            return Mathf.Sign(v) * Mathf.Pow(a, 1.35f);
        }

        // ------------------------------------------------------------------ bàn phím (thử trong Editor)
        float _kbRoll, _kbPitch;

        void Keyboard(float dt)
        {
            if (!useKeyboard) { _kbRoll = _kbPitch = 0f; return; }
            float tr = (SkyInput.KeyHeld(SkyKey.Right) ? 1f : 0f) - (SkyInput.KeyHeld(SkyKey.Left) ? 1f : 0f);
            float tp = (SkyInput.KeyHeld(SkyKey.Down) || SkyInput.KeyHeld(SkyKey.S) ? 1f : 0f)
                       - (SkyInput.KeyHeld(SkyKey.Up) || SkyInput.KeyHeld(SkyKey.W) ? 1f : 0f);
            _kbRoll = Mathf.MoveTowards(_kbRoll, tr, 3f * dt);
            _kbPitch = Mathf.MoveTowards(_kbPitch, tp, 3f * dt);
            if (SkyInput.KeyHeld(SkyKey.LShift)) SetThrottleT(_throttleT + 0.45f * dt);
            if (SkyInput.KeyHeld(SkyKey.LCtrl)) SetThrottleT(_throttleT - 0.45f * dt);
            if (SkyInput.KeyDown(SkyKey.G)) _gearT = _gearT >= 0.5f ? 0f : 1f;
            if (SkyInput.KeyDown(SkyKey.F))
                _flap = SkyInput.KeyHeld(SkyKey.Z) ? Mathf.Max(0, _flap - 1) : Mathf.Min(4, _flap + 1);
            if (SkyInput.KeyDown(SkyKey.B)) _speedbrake = _speedbrake > 0.5f ? 0f : 1f;
        }

        void UpdateTilt(float dt)
        {
            if (yokeSource == YokeSource.Wheel) { _tiltRoll = _tiltPitch = 0f; return; }
            ReadTilt(out float roll, out float gz);
            float dr = Mathf.DeltaAngle(_tiltZeroRoll, roll);
            float tr = Mathf.Clamp(dr / tiltRollRangeDeg, -1f, 1f);
            float tp = Mathf.Clamp((gz - _tiltZeroG) / tiltPitchRangeG, -1f, 1f);
            if (invertTilt) { tr = -tr; tp = -tp; }
            if (invertPitch) tp = -tp;
            float k = 1f - Mathf.Exp(-12f * dt);
            _tiltRoll = Mathf.Lerp(_tiltRoll, Shape(tr), k);
            _tiltPitch = Mathf.Lerp(_tiltPitch, Shape(tp), k);
        }

        void Publish()
        {
            float roll = _kbRoll, pitch = _kbPitch;
            if (yokeSource != YokeSource.Tilt) { roll += _touchRoll; pitch += _touchPitch; }
            if (yokeSource != YokeSource.Wheel) { roll += _tiltRoll; pitch += _tiltPitch; }
            var c = PilotInputBus.Current;
            c.roll = Mathf.Clamp(roll, -1f, 1f);
            c.pitch = Mathf.Clamp(pitch, -1f, 1f);
            c.throttle1 = c.throttle2 = ThrottleFromT(_throttleT);
            c.gearDown = _gearT < 0.5f;
            c.flapLever = _flap;
            c.speedbrake = _speedbrake;
            PilotInputBus.Current = c;
        }

        // ------------------------------------------------------------------ hiển thị
        void RefreshUi()
        {
            var c = PilotInputBus.Current;
            if (_panel.gameObject.activeSelf != _panelOpen) _panel.gameObject.SetActive(_panelOpen);
            SetHandle(_thrHandle, _thrTrack, _throttleT);
            SetHandle(_gearHandle, _gearTrack, _gearT);
            SetHandle(_flapHandle, _flapTrack, 1f - _flap / 4f);
            SetHandle(_sbHandle, _sbTrack, 1f - _speedbrake);

            _yokeVisual.localRotation = Quaternion.Euler(0, 0, -c.roll * 90f);
            float sc = 1f + 0.16f * c.pitch;
            _yokeVisual.localScale = new Vector3(sc, sc, 1f);
            _yokeVisual.anchoredPosition = new Vector2(0, -c.pitch * 18f);

            if (bus != null)
            {
                var s = bus.state;
                float thr = c.throttle1;
                _thrReadout.text = thr < -0.01f ? "REV " + Mathf.RoundToInt(-thr * 100f) + "%" : "N1 " + Mathf.RoundToInt(s.n1Eng1) + "%";
                _thrReadout.color = thr < -0.01f ? AirbusColors.Amber : AirbusColors.Green;
                float g = s.gearPos;
                Color lamp = g > 0.99f ? AirbusColors.Green : (g < 0.01f ? new Color(0.08f, 0.08f, 0.08f) : AirbusColors.Red);
                for (int i = 0; i < 3; i++) _gearLights[i].color = lamp;
            }
            _yokeReadout.text = "ROLL " + Mathf.RoundToInt(c.roll * 100f) + "%   PITCH " + Mathf.RoundToInt(c.pitch * 100f) + "%";
            foreach (var b in _buttons)
                if (b.labelFn != null) { string l = b.labelFn(); if (b.text.text != l) b.text.text = l; }
        }
    }
}
