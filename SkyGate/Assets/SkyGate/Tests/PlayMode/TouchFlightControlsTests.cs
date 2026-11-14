using System.Collections;
using NUnit.Framework;
using SkyGate.Core;
using SkyGate.Rendering.Controls;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SkyGate.Tests.PlayMode
{
    /// <summary>
    /// Test giao diện điều khiển cảm ứng. Cảm ứng ảo được đặt vào đúng toạ độ màn hình của từng phần tử uGUI
    /// (canvas Overlay: toạ độ thế giới = pixel), nên test đúng với mọi độ phân giải màn hình.
    /// Sự kiện được xếp hàng (queueEventOnly) để MonoBehaviour thấy "pressed this frame" trong Update của frame kế.
    /// </summary>
    public class TouchFlightControlsTests : InputFixtureBase
    {
        static readonly string[] PrefKeys = { "sg.ctl.yoke", "sg.ctl.invPitch", "sg.ctl.invTilt", "sg.ctl.tiltSens" };

        GameObject _go;
        AircraftStateBus _bus;
        TouchFlightControls _ctl;
        Keyboard _kb;

        [SetUp]
        public void SetUp()
        {
            ClearPrefs();
            _kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            InputSystem.AddDevice<Touchscreen>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.Destroy(_go);
            ClearPrefs();
        }

        static void ClearPrefs() { foreach (var k in PrefKeys) PlayerPrefs.DeleteKey(k); }

        IEnumerator Create()
        {
            _go = new GameObject("TouchControlsTest");
            _bus = _go.AddComponent<AircraftStateBus>();
            _ctl = _go.AddComponent<TouchFlightControls>();
            yield return null;      // cho CanvasScaler áp dụng tỉ lệ trước khi tính toạ độ
            yield return null;
        }

        RectTransform Find(string path)
        {
            var t = _go.transform.Find("TouchHud/" + path);
            Assert.That(t, Is.Not.Null, "Không tìm thấy " + path + ": tên phần tử trong TouchFlightControls.BuildUi đã đổi?");
            return (RectTransform)t;
        }

        static Vector2 At(RectTransform rt, Vector2 localFromCenter)
            => rt.TransformPoint(rt.rect.center + localFromCenter);

        static Vector2 TrackTop(RectTransform track) => At(track, new Vector2(0, track.rect.height * 0.5f - 1f));
        static Vector2 TrackBottom(RectTransform track) => At(track, new Vector2(0, -track.rect.height * 0.5f + 1f));

        // ---------------------------------------------------------------- khởi tạo
        [UnityTest]
        public IEnumerator Awake_RegistersInstance_AndPublishesNeutralControls()
        {
            yield return Create();
            Assert.That(TouchFlightControls.Instance, Is.SameAs(_ctl));
            var c = PilotInputBus.Current;
            Assert.That(c.parkingBrake, Is.False, "nhả phanh đỗ khi vào game");
            Assert.That(c.gearDown, Is.True);
            Assert.That(c.flapLever, Is.EqualTo(0));
            Assert.That(c.speedbrake, Is.EqualTo(0f));
            Assert.That(c.throttle1, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(c.roll, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(c.pitch, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(_ctl.ThrottleValue, Is.EqualTo(0f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator Destroy_ClearsInstance()
        {
            yield return Create();
            Object.Destroy(_go);
            yield return null;
            Assert.That(TouchFlightControls.Instance == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Ui_HasAllControlAreas()
        {
            yield return Create();
            foreach (var p in new[] { "Throttle/track", "Yoke", "Gear/track", "Flap/track", "Speedbrake/track", "Settings" })
                Find(p);
        }

        // ---------------------------------------------------------------- cài đặt lưu/nạp
        [UnityTest]
        public IEnumerator Settings_AreLoadedFromPlayerPrefs()
        {
            PlayerPrefs.SetInt("sg.ctl.yoke", 2);
            PlayerPrefs.SetInt("sg.ctl.invPitch", 1);
            PlayerPrefs.SetInt("sg.ctl.invTilt", 1);
            PlayerPrefs.SetInt("sg.ctl.tiltSens", 2);
            yield return Create();
            Assert.That(_ctl.yokeSource, Is.EqualTo(YokeSource.Both));
            Assert.That(_ctl.invertPitch, Is.True);
            Assert.That(_ctl.invertTilt, Is.True);
            Assert.That(_ctl.tiltRollRangeDeg, Is.EqualTo(25f));
            Assert.That(_ctl.tiltPitchRangeG, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator Settings_OutOfRangeValues_AreClamped()
        {
            PlayerPrefs.SetInt("sg.ctl.yoke", 99);
            PlayerPrefs.SetInt("sg.ctl.tiltSens", -5);
            yield return Create();
            Assert.That(_ctl.yokeSource, Is.EqualTo(YokeSource.Both));
            Assert.That(_ctl.tiltRollRangeDeg, Is.EqualTo(50f));
        }

        [UnityTest]
        public IEnumerator SetYokeSource_IsSaved()
        {
            yield return Create();
            _ctl.SetYokeSource(YokeSource.Tilt);
            Assert.That(PlayerPrefs.GetInt("sg.ctl.yoke"), Is.EqualTo((int)YokeSource.Tilt));
            Assert.That(_ctl.yokeSource, Is.EqualTo(YokeSource.Tilt));
        }

        // ---------------------------------------------------------------- bàn phím (thử trong Editor)
        [UnityTest]
        public IEnumerator Keyboard_G_TogglesGear()
        {
            yield return Create();
            Input.Press(_kb.gKey, queueEventOnly: true);
            yield return null;
            Input.Release(_kb.gKey, queueEventOnly: true);
            yield return null;
            Assert.That(PilotInputBus.Current.gearDown, Is.False, "G lần 1: thu càng");

            Input.Press(_kb.gKey, queueEventOnly: true);
            yield return null;
            Input.Release(_kb.gKey, queueEventOnly: true);
            yield return null;
            Assert.That(PilotInputBus.Current.gearDown, Is.True, "G lần 2: thả càng");
        }

        [UnityTest]
        public IEnumerator Keyboard_F_RaisesFlaps_AndZF_LowersThem()
        {
            yield return Create();
            for (int i = 0; i < 6; i++)   // quá 4 nấc để kiểm tra chặn trên
            {
                Input.Press(_kb.fKey, queueEventOnly: true); yield return null;
                Input.Release(_kb.fKey, queueEventOnly: true); yield return null;
            }
            Assert.That(PilotInputBus.Current.flapLever, Is.EqualTo(4));

            Input.Press(_kb.zKey, queueEventOnly: true); yield return null;
            Input.Press(_kb.fKey, queueEventOnly: true); yield return null;
            Input.Release(_kb.fKey, queueEventOnly: true); Input.Release(_kb.zKey, queueEventOnly: true); yield return null;
            Assert.That(PilotInputBus.Current.flapLever, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Keyboard_B_TogglesSpeedbrake()
        {
            yield return Create();
            Input.Press(_kb.bKey, queueEventOnly: true); yield return null;
            Input.Release(_kb.bKey, queueEventOnly: true); yield return null;
            Assert.That(PilotInputBus.Current.speedbrake, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Keyboard_RightArrow_RollsRight_AndLeftArrowRollsLeft()
        {
            yield return Create();
            Input.Press(_kb.rightArrowKey, queueEventOnly: true);
            yield return new WaitForSeconds(0.5f);
            Assert.That(PilotInputBus.Current.roll, Is.GreaterThan(0.5f));
            Input.Release(_kb.rightArrowKey, queueEventOnly: true);

            Input.Press(_kb.leftArrowKey, queueEventOnly: true);
            yield return new WaitForSeconds(1.2f);
            Assert.That(PilotInputBus.Current.roll, Is.LessThan(-0.5f));
        }

        [UnityTest]
        public IEnumerator Keyboard_ShiftRaisesThrottle()
        {
            yield return Create();
            Input.Press(_kb.leftShiftKey, queueEventOnly: true);
            yield return new WaitForSeconds(0.5f);
            Assert.That(PilotInputBus.Current.throttle1, Is.GreaterThan(0.1f));
            Assert.That(PilotInputBus.Current.throttle2, Is.EqualTo(PilotInputBus.Current.throttle1));
        }

        [UnityTest]
        public IEnumerator Keyboard_Disabled_IsIgnored()
        {
            yield return Create();
            _ctl.useKeyboard = false;
            Input.Press(_kb.rightArrowKey, queueEventOnly: true);
            Input.Press(_kb.gKey, queueEventOnly: true);
            yield return new WaitForSeconds(0.3f);
            Assert.That(PilotInputBus.Current.roll, Is.EqualTo(0f).Within(1e-4f));
        }

        // ---------------------------------------------------------------- cảm ứng: cần ga
        [UnityTest]
        public IEnumerator Touch_ThrottleTop_SetsToga_AndStaysAfterRelease()
        {
            yield return Create();
            var pos = TrackTop(Find("Throttle/track"));
            Input.BeginTouch(1, pos, queueEventOnly: true);
            yield return null;
            Assert.That(_ctl.IsPointerOwned(1), Is.True);
            Assert.That(PilotInputBus.Current.throttle1, Is.EqualTo(1f).Within(1e-3f));

            Input.EndTouch(1, pos, queueEventOnly: true);
            yield return null;
            Assert.That(_ctl.IsPointerOwned(1), Is.False);
            Assert.That(PilotInputBus.Current.throttle1, Is.EqualTo(1f).Within(1e-3f), "cần ga giữ nguyên khi nhả tay");
        }

        [UnityTest]
        public IEnumerator Touch_ThrottleBottom_OnGround_AllowsReverse()
        {
            yield return Create();    // Parked: onGround = true
            Input.BeginTouch(1, TrackBottom(Find("Throttle/track")), queueEventOnly: true);
            yield return null;
            Assert.That(PilotInputBus.Current.throttle1, Is.LessThan(-0.9f));
        }

        [UnityTest]
        public IEnumerator Touch_ThrottleBottom_InFlight_IsHeldAtIdle()
        {
            yield return Create();
            var s = _bus.state; s.onGround = false; _bus.state = s;
            Input.BeginTouch(1, TrackBottom(Find("Throttle/track")), queueEventOnly: true);
            yield return null;
            Assert.That(PilotInputBus.Current.throttle1, Is.EqualTo(0f).Within(1e-3f), "REV bị chặn khi đang bay");
        }

        // ---------------------------------------------------------------- cảm ứng: gear / flap / speedbrake
        [UnityTest]
        public IEnumerator Touch_GearUp_SnapsAndRetractsGear()
        {
            yield return Create();
            var pos = TrackTop(Find("Gear/track"));
            Input.BeginTouch(1, pos, queueEventOnly: true); yield return null;
            Input.EndTouch(1, pos, queueEventOnly: true); yield return null;
            Assert.That(PilotInputBus.Current.gearDown, Is.False);
        }

        [UnityTest]
        public IEnumerator Touch_GearReleasedBelowMiddle_SnapsBackDown()
        {
            yield return Create();
            var track = Find("Gear/track");
            var top = TrackTop(track);
            Input.BeginTouch(1, top, queueEventOnly: true); yield return null;
            Input.EndTouch(1, top, queueEventOnly: true); yield return null;
            Assert.That(PilotInputBus.Current.gearDown, Is.False, "tiền điều kiện: càng đã thu");

            var low = At(track, new Vector2(0, -track.rect.height * 0.3f));
            Input.BeginTouch(2, low, queueEventOnly: true); yield return null;
            Input.EndTouch(2, low, queueEventOnly: true); yield return null;
            Assert.That(PilotInputBus.Current.gearDown, Is.True);
        }

        [UnityTest]
        public IEnumerator Touch_FlapBottom_IsFull_AndTopIsZero()
        {
            yield return Create();
            var track = Find("Flap/track");
            Input.BeginTouch(1, TrackBottom(track), queueEventOnly: true); yield return null;
            Assert.That(PilotInputBus.Current.flapLever, Is.EqualTo(4));
            Input.MoveTouch(1, TrackTop(track), queueEventOnly: true); yield return null;
            Assert.That(PilotInputBus.Current.flapLever, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Touch_SpeedbrakeBottom_IsFullDeployed()
        {
            yield return Create();
            Input.BeginTouch(1, TrackBottom(Find("Speedbrake/track")), queueEventOnly: true); yield return null;
            Assert.That(PilotInputBus.Current.speedbrake, Is.EqualTo(1f).Within(0.01f));
        }

        // ---------------------------------------------------------------- cảm ứng: yoke
        [UnityTest]
        public IEnumerator Touch_YokeCentre_IsNeutral()
        {
            // Hồi quy: vùng yoke có pivot (1,0); toạ độ local phải được tính từ TÂM, nếu không chạm giữa = lệch hết cỡ.
            yield return Create();
            Input.BeginTouch(1, At(Find("Yoke"), Vector2.zero), queueEventOnly: true);
            yield return null;
            Assert.That(PilotInputBus.Current.roll, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(PilotInputBus.Current.pitch, Is.EqualTo(0f).Within(1e-3f));
        }

        [UnityTest]
        public IEnumerator Touch_YokeRightAndDown_IsRollRightAndPullUp()
        {
            yield return Create();
            var yoke = Find("Yoke");
            float r = yoke.sizeDelta.x * 0.36f;
            Input.BeginTouch(1, At(yoke, new Vector2(r, -r)), queueEventOnly: true);
            yield return null;
            Assert.That(PilotInputBus.Current.roll, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(PilotInputBus.Current.pitch, Is.EqualTo(1f).Within(1e-3f), "kéo xuống = kéo cần (mũi lên)");
        }

        [UnityTest]
        public IEnumerator Touch_YokeInvertPitch_FlipsPitchOnly()
        {
            yield return Create();
            _ctl.invertPitch = true;
            var yoke = Find("Yoke");
            float r = yoke.sizeDelta.x * 0.36f;
            Input.BeginTouch(1, At(yoke, new Vector2(r, -r)), queueEventOnly: true);
            yield return null;
            Assert.That(PilotInputBus.Current.roll, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(PilotInputBus.Current.pitch, Is.EqualTo(-1f).Within(1e-3f));
        }

        [UnityTest]
        public IEnumerator Yoke_ReturnsToCentreWhenReleased()
        {
            yield return Create();
            var yoke = Find("Yoke");
            float r = yoke.sizeDelta.x * 0.36f;
            var pos = At(yoke, new Vector2(r, 0));
            Input.BeginTouch(1, pos, queueEventOnly: true); yield return null;
            Assert.That(PilotInputBus.Current.roll, Is.GreaterThan(0.9f));
            Input.EndTouch(1, pos, queueEventOnly: true);
            yield return new WaitForSeconds(0.6f);       // tự về giữa 3.5/s
            Assert.That(PilotInputBus.Current.roll, Is.EqualTo(0f).Within(1e-3f));
        }

        [UnityTest]
        public IEnumerator YokeTiltMode_IgnoresTouchOnWheel_AndStaysCentredWithoutMotion()
        {
            yield return Create();
            _ctl.SetYokeSource(YokeSource.Tilt);
            var centre = At(Find("Yoke"), Vector2.zero);
            Assert.That(_ctl.BlocksPointer(centre), Is.False, "chế độ NGHIÊNG: vô lăng chỉ hiển thị");
            yield return new WaitForSeconds(0.2f);
            Assert.That(PilotInputBus.Current.roll, Is.EqualTo(0f).Within(1e-2f));
            Assert.That(PilotInputBus.Current.pitch, Is.EqualTo(0f).Within(1e-2f));
        }

        // ---------------------------------------------------------------- chặn con trỏ cho camera, nút HUD
        [UnityTest]
        public IEnumerator BlocksPointer_TrueOnControls_FalseOnEmptySky()
        {
            yield return Create();
            Assert.That(_ctl.BlocksPointer(At(Find("Throttle"), Vector2.zero)), Is.True);
            Assert.That(_ctl.BlocksPointer(At(Find("Yoke"), Vector2.zero)), Is.True);
            Assert.That(_ctl.BlocksPointer(new Vector2(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.6f)), Is.False);
        }

        [UnityTest]
        public IEnumerator AddButton_InvokesCallbackWhenTapped()
        {
            yield return Create();
            int clicks = 0;
            _ctl.AddButton("TEST", true, 4, () => clicks++);
            var btn = Find("Btn_TEST");
            Input.BeginTouch(1, At(btn, Vector2.zero), queueEventOnly: true);
            yield return null;
            Assert.That(clicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SettingsButton_TogglesPanel()
        {
            yield return Create();
            var panel = Find("Settings");
            Assert.That(panel.gameObject.activeSelf, Is.False);

            var open = Find("Btn_CÀI ĐẶT");
            var pos = At(open, Vector2.zero);
            Input.BeginTouch(1, pos, queueEventOnly: true); yield return null;
            Input.EndTouch(1, pos, queueEventOnly: true); yield return null;
            Assert.That(panel.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator PanelOption_CyclesYokeSource_AndSavesIt()
        {
            yield return Create();
            var open = Find("Btn_CÀI ĐẶT");
            var p0 = At(open, Vector2.zero);
            Input.BeginTouch(1, p0, queueEventOnly: true); yield return null;
            Input.EndTouch(1, p0, queueEventOnly: true); yield return null;

            var opt = Find("Settings/Opt0");
            var p1 = At(opt, Vector2.zero);
            Input.BeginTouch(2, p1, queueEventOnly: true); yield return null;
            Input.EndTouch(2, p1, queueEventOnly: true); yield return null;

            Assert.That(_ctl.yokeSource, Is.EqualTo(YokeSource.Tilt));
            Assert.That(PlayerPrefs.GetInt("sg.ctl.yoke"), Is.EqualTo((int)YokeSource.Tilt));
        }

        // ---------------------------------------------------------------- đa chạm
        [UnityTest]
        public IEnumerator MultiTouch_ThrottleAndYokeAtTheSameTime()
        {
            yield return Create();
            var yoke = Find("Yoke");
            float r = yoke.sizeDelta.x * 0.36f;
            Input.BeginTouch(1, TrackTop(Find("Throttle/track")), queueEventOnly: true);
            Input.BeginTouch(2, At(yoke, new Vector2(r, 0)), queueEventOnly: true);
            yield return null;
            Assert.That(PilotInputBus.Current.throttle1, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(PilotInputBus.Current.roll, Is.GreaterThan(0.9f));
        }

        [UnityTest]
        public IEnumerator CalibrateTilt_DoesNotThrow()
        {
            yield return Create();
            Assert.DoesNotThrow(() => _ctl.CalibrateTilt());
        }
    }
}
