using UnityEngine;
#if ENABLE_INPUT_SYSTEM && SKYGATE_INPUTSYSTEM
using UnityEngine.InputSystem;
#endif

namespace SkyGate.Rendering.Controls
{
    public enum SkyKey
    {
        G, F, L, D, B, R, N, P, C, V, H,
        Num1, Num2, Num3,
        Up, Down, Left, Right,
        W, S, A, Q, E, Z, X, LShift, LCtrl, Space, Tab, Esc,
    }

    public struct PointerState
    {
        public int id;               // >= 0: ngón tay; -1: chuột trái; -2: chuột phải
        public Vector2 pos;          // pixel màn hình, gốc ở góc dưới trái
        public Vector2 delta;
        public bool pressed;         // vừa chạm trong frame này
        public bool released;        // vừa nhả trong frame này
        public bool down;            // đang giữ
    }

    /// <summary>
    /// Lớp bọc input: dùng Input System nếu có (SKYGATE_INPUTSYSTEM), không thì Input cũ.
    /// Cung cấp: danh sách con trỏ (đa chạm + chuột), phím, cuộn chuột và vector nghiêng máy (tilt).
    /// </summary>
    public static class SkyInput
    {
        public const int MaxPointers = 6;

        public static int GetPointers(PointerState[] buf)
        {
            int n = 0;
#if ENABLE_INPUT_SYSTEM && SKYGATE_INPUTSYSTEM
            var ts = Touchscreen.current;
            if (ts != null)
            {
                var touches = ts.touches;
                for (int i = 0; i < touches.Count && n < buf.Length; i++)
                {
                    var t = touches[i];
                    bool pressed = t.press.wasPressedThisFrame, released = t.press.wasReleasedThisFrame, down = t.press.isPressed;
                    if (!pressed && !released && !down) continue;
                    buf[n++] = new PointerState
                    {
                        id = t.touchId.ReadValue(), pos = t.position.ReadValue(), delta = t.delta.ReadValue(),
                        pressed = pressed, released = released, down = down,
                    };
                }
            }
            var m = Mouse.current;
            if (m != null && n < buf.Length)
            {
                var l = m.leftButton;
                if (l.isPressed || l.wasReleasedThisFrame)
                    buf[n++] = new PointerState { id = -1, pos = m.position.ReadValue(), delta = m.delta.ReadValue(), pressed = l.wasPressedThisFrame, released = l.wasReleasedThisFrame, down = l.isPressed };
                var r = m.rightButton;
                if (n < buf.Length && (r.isPressed || r.wasReleasedThisFrame))
                    buf[n++] = new PointerState { id = -2, pos = m.position.ReadValue(), delta = m.delta.ReadValue(), pressed = r.wasPressedThisFrame, released = r.wasReleasedThisFrame, down = r.isPressed };
            }
#else
            for (int i = 0; i < UnityEngine.Input.touchCount && n < buf.Length; i++)
            {
                var t = UnityEngine.Input.GetTouch(i);
                buf[n++] = new PointerState
                {
                    id = t.fingerId, pos = t.position, delta = t.deltaPosition,
                    pressed = t.phase == TouchPhase.Began,
                    released = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled,
                    down = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled,
                };
            }
            if (UnityEngine.Input.touchCount == 0)
            {
                Vector2 mp = UnityEngine.Input.mousePosition;
                for (int b = 0; b < 2 && n < buf.Length; b++)
                {
                    bool d = UnityEngine.Input.GetMouseButton(b), p = UnityEngine.Input.GetMouseButtonDown(b), r = UnityEngine.Input.GetMouseButtonUp(b);
                    if (!d && !p && !r) continue;
                    buf[n++] = new PointerState
                    {
                        id = -1 - b, pos = mp, delta = new Vector2(UnityEngine.Input.GetAxis("Mouse X"), UnityEngine.Input.GetAxis("Mouse Y")) * 12f,
                        pressed = p, released = r, down = d,
                    };
                }
            }
#endif
            return n;
        }

        public static float Scroll()
        {
#if ENABLE_INPUT_SYSTEM && SKYGATE_INPUTSYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
            return UnityEngine.Input.mouseScrollDelta.y;
#endif
        }

        public static bool KeyDown(SkyKey k)
        {
#if ENABLE_INPUT_SYSTEM && SKYGATE_INPUTSYSTEM
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].wasPressedThisFrame;
#else
            return UnityEngine.Input.GetKeyDown(MapLegacy(k));
#endif
        }

        public static bool KeyHeld(SkyKey k)
        {
#if ENABLE_INPUT_SYSTEM && SKYGATE_INPUTSYSTEM
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].isPressed;
#else
            return UnityEngine.Input.GetKey(MapLegacy(k));
#endif
        }

        /// <summary>
        /// Hướng trọng lực trong hệ trục thiết bị (đơn vị g): máy dựng dọc = (0,-1,0), nằm ngang màn hình hướng lên = (0,0,-1).
        /// Dùng Input cũ nếu bật (dấu đã biết rõ), không thì Accelerometer của Input System (có thể phải bật "Đảo" trong cài đặt).
        /// </summary>
        public static Vector3 Gravity()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.acceleration;
#elif ENABLE_INPUT_SYSTEM && SKYGATE_INPUTSYSTEM
            var a = Accelerometer.current;
            if (a == null) return new Vector3(0f, -1f, 0f);
            if (!a.enabled) InputSystem.EnableDevice(a);
            return -a.acceleration.ReadValue() / 9.81f;
#else
            return new Vector3(0f, -1f, 0f);
#endif
        }

        public static bool HasTiltSensor
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && SKYGATE_INPUTSYSTEM
                return Accelerometer.current != null || SystemInfo.supportsAccelerometer;
#else
                return SystemInfo.supportsAccelerometer;
#endif
            }
        }

#if ENABLE_INPUT_SYSTEM && SKYGATE_INPUTSYSTEM
        static Key Map(SkyKey k)
        {
            switch (k)
            {
                case SkyKey.G: return Key.G; case SkyKey.F: return Key.F; case SkyKey.L: return Key.L;
                case SkyKey.D: return Key.D; case SkyKey.B: return Key.B; case SkyKey.R: return Key.R;
                case SkyKey.N: return Key.N; case SkyKey.P: return Key.P; case SkyKey.C: return Key.C;
                case SkyKey.V: return Key.V; case SkyKey.H: return Key.H;
                case SkyKey.Num1: return Key.Digit1; case SkyKey.Num2: return Key.Digit2; case SkyKey.Num3: return Key.Digit3;
                case SkyKey.Up: return Key.UpArrow; case SkyKey.Down: return Key.DownArrow;
                case SkyKey.Left: return Key.LeftArrow; case SkyKey.Right: return Key.RightArrow;
                case SkyKey.W: return Key.W; case SkyKey.S: return Key.S; case SkyKey.A: return Key.A;
                case SkyKey.Q: return Key.Q; case SkyKey.E: return Key.E;
                case SkyKey.Z: return Key.Z; case SkyKey.X: return Key.X;
                case SkyKey.LShift: return Key.LeftShift; case SkyKey.LCtrl: return Key.LeftCtrl;
                case SkyKey.Space: return Key.Space; case SkyKey.Tab: return Key.Tab; case SkyKey.Esc: return Key.Escape;
            }
            return Key.None;
        }
#else
        static KeyCode MapLegacy(SkyKey k)
        {
            switch (k)
            {
                case SkyKey.G: return KeyCode.G; case SkyKey.F: return KeyCode.F; case SkyKey.L: return KeyCode.L;
                case SkyKey.D: return KeyCode.D; case SkyKey.B: return KeyCode.B; case SkyKey.R: return KeyCode.R;
                case SkyKey.N: return KeyCode.N; case SkyKey.P: return KeyCode.P; case SkyKey.C: return KeyCode.C;
                case SkyKey.V: return KeyCode.V; case SkyKey.H: return KeyCode.H;
                case SkyKey.Num1: return KeyCode.Alpha1; case SkyKey.Num2: return KeyCode.Alpha2; case SkyKey.Num3: return KeyCode.Alpha3;
                case SkyKey.Up: return KeyCode.UpArrow; case SkyKey.Down: return KeyCode.DownArrow;
                case SkyKey.Left: return KeyCode.LeftArrow; case SkyKey.Right: return KeyCode.RightArrow;
                case SkyKey.W: return KeyCode.W; case SkyKey.S: return KeyCode.S; case SkyKey.A: return KeyCode.A;
                case SkyKey.Q: return KeyCode.Q; case SkyKey.E: return KeyCode.E;
                case SkyKey.Z: return KeyCode.Z; case SkyKey.X: return KeyCode.X;
                case SkyKey.LShift: return KeyCode.LeftShift; case SkyKey.LCtrl: return KeyCode.LeftControl;
                case SkyKey.Space: return KeyCode.Space; case SkyKey.Tab: return KeyCode.Tab; case SkyKey.Esc: return KeyCode.Escape;
            }
            return KeyCode.None;
        }
#endif
    }
}
