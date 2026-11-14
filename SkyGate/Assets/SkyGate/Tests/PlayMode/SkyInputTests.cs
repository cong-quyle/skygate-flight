using System;
using System.Collections.Generic;
using NUnit.Framework;
using SkyGate.Rendering.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SkyGate.Tests.PlayMode
{
    /// <summary>
    /// Kiểm tra lớp bọc input bằng thiết bị ảo (chuột, cảm ứng, bàn phím).
    /// Gọi đồng bộ ngay sau Press/BeginTouch nên không cần chờ frame (InputTestFixture tự Update).
    /// </summary>
    public class SkyInputTests : InputFixtureBase
    {
        Mouse _mouse;
        Keyboard _kb;
        Touchscreen _touch;

        [SetUp]
        public void AddDevices()
        {
            _mouse = InputSystem.AddDevice<Mouse>();
            _kb = InputSystem.AddDevice<Keyboard>();
            _touch = InputSystem.AddDevice<Touchscreen>();
        }

        // ---------------------------------------------------------------- con trỏ
        [Test]
        public void GetPointers_WithNothingPressed_ReturnsZero()
        {
            Assert.That(SkyInput.GetPointers(new PointerState[SkyInput.MaxPointers]), Is.EqualTo(0));
        }

        [Test]
        public void GetPointers_LeftMouse_IsPointerMinusOne()
        {
            Input.Set(_mouse.position, new Vector2(100, 200));
            Input.Press(_mouse.leftButton);

            var buf = new PointerState[SkyInput.MaxPointers];
            int n = SkyInput.GetPointers(buf);

            Assert.That(n, Is.EqualTo(1));
            Assert.That(buf[0].id, Is.EqualTo(-1));
            Assert.That(buf[0].pos, Is.EqualTo(new Vector2(100, 200)));
            Assert.That(buf[0].pressed, Is.True);
            Assert.That(buf[0].down, Is.True);
            Assert.That(buf[0].released, Is.False);
        }

        [Test]
        public void GetPointers_ReleasedMouse_ReportsReleaseOnce()
        {
            Input.Press(_mouse.leftButton);
            Input.Release(_mouse.leftButton);

            var buf = new PointerState[SkyInput.MaxPointers];
            Assert.That(SkyInput.GetPointers(buf), Is.EqualTo(1));
            Assert.That(buf[0].released, Is.True);
            Assert.That(buf[0].down, Is.False);
        }

        [Test]
        public void GetPointers_RightMouse_IsPointerMinusTwo()
        {
            Input.Press(_mouse.rightButton);
            var buf = new PointerState[SkyInput.MaxPointers];
            Assert.That(SkyInput.GetPointers(buf), Is.EqualTo(1));
            Assert.That(buf[0].id, Is.EqualTo(-2));
            Assert.That(buf[0].down, Is.True);
        }

        [Test]
        public void GetPointers_Touch_ReportsIdPositionAndPhases()
        {
            Input.BeginTouch(7, new Vector2(50, 60));
            var buf = new PointerState[SkyInput.MaxPointers];
            Assert.That(SkyInput.GetPointers(buf), Is.EqualTo(1));
            Assert.That(buf[0].id, Is.EqualTo(7));
            Assert.That(buf[0].pos, Is.EqualTo(new Vector2(50, 60)));
            Assert.That(buf[0].pressed, Is.True);
            Assert.That(buf[0].down, Is.True);

            Input.EndTouch(7, new Vector2(50, 60));
            Assert.That(SkyInput.GetPointers(buf), Is.EqualTo(1));
            Assert.That(buf[0].released, Is.True);
            Assert.That(buf[0].down, Is.False);
        }

        [Test]
        public void GetPointers_MultiTouch_ReportsEveryFinger()
        {
            Input.BeginTouch(1, new Vector2(10, 10));
            Input.BeginTouch(2, new Vector2(900, 10));
            var buf = new PointerState[SkyInput.MaxPointers];
            Assert.That(SkyInput.GetPointers(buf), Is.EqualTo(2));
            var ids = new HashSet<int> { buf[0].id, buf[1].id };
            Assert.That(ids, Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public void GetPointers_NeverWritesPastTheBuffer()
        {
            Input.BeginTouch(1, new Vector2(10, 10));
            Input.Press(_mouse.leftButton);
            Input.Press(_mouse.rightButton);
            Assert.That(SkyInput.GetPointers(new PointerState[1]), Is.EqualTo(1));
            Assert.That(SkyInput.GetPointers(new PointerState[2]), Is.EqualTo(2));
            Assert.That(SkyInput.GetPointers(new PointerState[SkyInput.MaxPointers]), Is.EqualTo(3));
        }

        // ---------------------------------------------------------------- cuộn
        [Test]
        public void Scroll_IsNormalisedByOneHundredTwenty()
        {
            Input.Set(_mouse.scroll, new Vector2(0, 240));
            Assert.That(SkyInput.Scroll(), Is.EqualTo(2f).Within(1e-4f));
        }

        // ---------------------------------------------------------------- phím
        static readonly Dictionary<SkyKey, Key> Expected = new Dictionary<SkyKey, Key>
        {
            { SkyKey.G, Key.G }, { SkyKey.F, Key.F }, { SkyKey.L, Key.L }, { SkyKey.D, Key.D }, { SkyKey.B, Key.B },
            { SkyKey.R, Key.R }, { SkyKey.N, Key.N }, { SkyKey.P, Key.P }, { SkyKey.C, Key.C }, { SkyKey.V, Key.V },
            { SkyKey.H, Key.H },
            { SkyKey.Num1, Key.Digit1 }, { SkyKey.Num2, Key.Digit2 }, { SkyKey.Num3, Key.Digit3 },
            { SkyKey.Up, Key.UpArrow }, { SkyKey.Down, Key.DownArrow }, { SkyKey.Left, Key.LeftArrow }, { SkyKey.Right, Key.RightArrow },
            { SkyKey.W, Key.W }, { SkyKey.S, Key.S }, { SkyKey.A, Key.A }, { SkyKey.Q, Key.Q }, { SkyKey.E, Key.E },
            { SkyKey.Z, Key.Z }, { SkyKey.X, Key.X },
            { SkyKey.LShift, Key.LeftShift }, { SkyKey.LCtrl, Key.LeftCtrl },
            { SkyKey.Space, Key.Space }, { SkyKey.Tab, Key.Tab }, { SkyKey.Esc, Key.Escape },
        };

        [Test]
        public void EverySkyKey_HasAnExpectedMapping()
        {
            foreach (SkyKey k in Enum.GetValues(typeof(SkyKey)))
                Assert.That(Expected.ContainsKey(k), Is.True, k + " thiếu trong bảng kỳ vọng của test");
        }

        [Test]
        public void EverySkyKey_IsMappedToItsPhysicalKey()
        {
            foreach (var kv in Expected)
            {
                Assert.That(SkyInput.KeyHeld(kv.Key), Is.False, kv.Key + " held before press");
                Input.Press(_kb[kv.Value]);
                Assert.That(SkyInput.KeyHeld(kv.Key), Is.True, kv.Key + " held");
                Assert.That(SkyInput.KeyDown(kv.Key), Is.True, kv.Key + " down");
                Input.Release(_kb[kv.Value]);
                Assert.That(SkyInput.KeyHeld(kv.Key), Is.False, kv.Key + " held after release");
            }
        }

        [Test]
        public void Keys_AreNotCrossWired()
        {
            Input.Press(_kb.gKey);
            Assert.That(SkyInput.KeyHeld(SkyKey.G), Is.True);
            Assert.That(SkyInput.KeyHeld(SkyKey.F), Is.False);
            Assert.That(SkyInput.KeyDown(SkyKey.F), Is.False);
        }

        // ---------------------------------------------------------------- cảm biến
        [Test]
        public void Gravity_IsAFiniteVector()
        {
            Vector3 g = SkyInput.Gravity();
            Assert.That(float.IsNaN(g.x) || float.IsNaN(g.y) || float.IsNaN(g.z), Is.False);
            Assert.That(float.IsInfinity(g.x) || float.IsInfinity(g.y) || float.IsInfinity(g.z), Is.False);
        }

        [Test]
        public void HasTiltSensor_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => { bool _ = SkyInput.HasTiltSensor; });
        }
    }

    /// <summary>Không có thiết bị nào được cắm: mọi hàm phải trả về giá trị trung tính, không ném lỗi.</summary>
    public class SkyInputNoDeviceTests : InputFixtureBase
    {
        [Test]
        public void WithoutDevices_NothingIsPressed()
        {
            Assert.That(SkyInput.GetPointers(new PointerState[SkyInput.MaxPointers]), Is.EqualTo(0));
            Assert.That(SkyInput.Scroll(), Is.EqualTo(0f));
            Assert.That(SkyInput.KeyDown(SkyKey.G), Is.False);
            Assert.That(SkyInput.KeyHeld(SkyKey.LShift), Is.False);
        }
    }
}
