using System.Reflection;
using NUnit.Framework;
using SkyGate.Core;
using SkyGate.Rendering.Controls;

namespace SkyGate.Tests.EditMode
{
    /// <summary>Ánh xạ vị trí cần ga trên đường trượt (0..1) sang giá trị cần ga (-1..1) và ngược lại.</summary>
    public class ThrottleMappingTests
    {
        const float Eps = 1e-4f;

        // nấc trên đường trượt (TouchFlightControls): IDLE 0.20, CLB 0.60, FLX/MCT 0.80, TOGA 1.0
        [TestCase(0.00f, -1.00f)]   // đáy: đảo chiều tối đa
        [TestCase(0.10f, -0.50f)]
        [TestCase(0.20f, 0.00f)]    // IDLE
        [TestCase(0.40f, 0.35f)]
        [TestCase(0.60f, 0.70f)]    // CLB
        [TestCase(0.70f, 0.80f)]
        [TestCase(0.80f, 0.90f)]    // FLX/MCT
        [TestCase(0.90f, 0.95f)]
        [TestCase(1.00f, 1.00f)]    // TOGA
        [TestCase(1.50f, 1.00f)]    // quá đỉnh vẫn là TOGA
        public void ThrottleFromT_MapsSliderToLever(float t, float expected)
        {
            Assert.That(TouchFlightControls.ThrottleFromT(t), Is.EqualTo(expected).Within(Eps));
        }

        [TestCase(-1.00f, 0.00f)]
        [TestCase(-0.50f, 0.10f)]
        [TestCase(0.00f, 0.20f)]
        [TestCase(0.35f, 0.40f)]
        [TestCase(0.70f, 0.60f)]
        [TestCase(0.80f, 0.70f)]
        [TestCase(0.90f, 0.80f)]
        [TestCase(0.95f, 0.90f)]
        [TestCase(1.00f, 1.00f)]
        [TestCase(2.00f, 1.00f)]
        public void TFromThrottle_MapsLeverToSlider(float v, float expected)
        {
            Assert.That(TouchFlightControls.TFromThrottle(v), Is.EqualTo(expected).Within(Eps));
        }

        [Test]
        public void Detents_UseThePilotInputBusConstants()
        {
            Assert.That(TouchFlightControls.ThrottleFromT(0.20f), Is.EqualTo(PilotInputBus.ThrIdle).Within(Eps));
            Assert.That(TouchFlightControls.ThrottleFromT(0.60f), Is.EqualTo(PilotInputBus.ThrClimb).Within(Eps));
            Assert.That(TouchFlightControls.ThrottleFromT(0.80f), Is.EqualTo(PilotInputBus.ThrFlexMct).Within(Eps));
            Assert.That(TouchFlightControls.ThrottleFromT(1.00f), Is.EqualTo(PilotInputBus.ThrToga).Within(Eps));
        }

        [Test]
        public void RoundTrip_SliderToLeverAndBack_IsIdentity()
        {
            for (float t = 0f; t <= 1.0001f; t += 0.05f)
                Assert.That(TouchFlightControls.TFromThrottle(TouchFlightControls.ThrottleFromT(t)), Is.EqualTo(t).Within(1e-3f), "t=" + t);
        }

        [Test]
        public void ThrottleFromT_NeverDecreasesAsTheSliderRises()
        {
            float prev = float.NegativeInfinity;
            for (float t = 0f; t <= 1f; t += 0.01f)
            {
                float v = TouchFlightControls.ThrottleFromT(t);
                Assert.That(v, Is.GreaterThanOrEqualTo(prev - 1e-5f), "t=" + t);
                prev = v;
            }
        }

        // ---- đường cong yoke (hàm private Shape: vùng chết 5% rồi lũy thừa 1.35)
        static float Shape(float v)
        {
            var m = typeof(TouchFlightControls).GetMethod("Shape", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(m, Is.Not.Null, "TouchFlightControls.Shape đã bị đổi tên");
            return (float)m.Invoke(null, new object[] { v });
        }

        [TestCase(0f)]
        [TestCase(0.03f)]
        [TestCase(-0.05f)]
        public void Shape_DeadZone_IsZero(float v)
        {
            Assert.That(Shape(v), Is.EqualTo(0f));
        }

        [TestCase(1f, 1f)]
        [TestCase(-1f, -1f)]
        [TestCase(0.5f, 0.3646f)]
        public void Shape_Curve(float v, float expected)
        {
            Assert.That(Shape(v), Is.EqualTo(expected).Within(3e-3f));
        }

        [Test]
        public void Shape_IsOddAndMonotonic()
        {
            float prev = 0f;
            for (float v = 0.06f; v <= 1f; v += 0.02f)
            {
                float s = Shape(v);
                Assert.That(Shape(-v), Is.EqualTo(-s).Within(1e-5f));
                Assert.That(s, Is.GreaterThanOrEqualTo(prev));
                prev = s;
            }
        }
    }
}
