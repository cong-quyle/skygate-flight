using System.Reflection;
using NUnit.Framework;
using SkyGate.Core;

namespace SkyGate.Tests.EditMode
{
    public class PilotControlsTests
    {
        PilotControls _saved;

        [SetUp] public void SetUp() { _saved = PilotInputBus.Current; }
        [TearDown] public void TearDown() { PilotInputBus.Current = _saved; }

        [Test]
        public void Default_StartsParked_WithGearDown_AndNeutralControls()
        {
            var c = PilotControls.Default();
            Assert.That(c.gearDown, Is.True);
            Assert.That(c.parkingBrake, Is.True);
            Assert.That(c.pitch, Is.EqualTo(0f));
            Assert.That(c.roll, Is.EqualTo(0f));
            Assert.That(c.yaw, Is.EqualTo(0f));
            Assert.That(c.throttle1, Is.EqualTo(0f));
            Assert.That(c.throttle2, Is.EqualTo(0f));
            Assert.That(c.flapLever, Is.EqualTo(0));
            Assert.That(c.speedbrake, Is.EqualTo(0f));
            Assert.That(c.brake, Is.EqualTo(0f));
        }

        [Test]
        public void ThrottleDetents_AreOrdered_AndSpanIdleToToga()
        {
            Assert.That(PilotInputBus.ThrIdle, Is.EqualTo(0f));
            Assert.That(PilotInputBus.ThrToga, Is.EqualTo(1f));
            Assert.That(PilotInputBus.ThrIdle, Is.LessThan(PilotInputBus.ThrClimb));
            Assert.That(PilotInputBus.ThrClimb, Is.LessThan(PilotInputBus.ThrFlexMct));
            Assert.That(PilotInputBus.ThrFlexMct, Is.LessThan(PilotInputBus.ThrToga));
        }

        [Test]
        public void Bus_IsAStaticSharedValue()
        {
            var c = PilotInputBus.Current;
            c.pitch = 0.5f;
            PilotInputBus.Current = c;
            Assert.That(PilotInputBus.Current.pitch, Is.EqualTo(0.5f));
        }

        [Test]
        public void ResetStatics_RestoresDefault()
        {
            // Hàm này là [RuntimeInitializeOnLoadMethod] (private): gọi bằng reflection để kiểm chứng.
            var m = typeof(PilotInputBus).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(m, Is.Not.Null, "PilotInputBus.ResetStatics đã bị đổi tên hoặc xoá");

            var c = PilotInputBus.Current;
            c.gearDown = false; c.parkingBrake = false; c.throttle1 = 1f; c.flapLever = 4;
            PilotInputBus.Current = c;

            m.Invoke(null, null);

            var d = PilotControls.Default();
            Assert.That(PilotInputBus.Current.gearDown, Is.EqualTo(d.gearDown));
            Assert.That(PilotInputBus.Current.parkingBrake, Is.EqualTo(d.parkingBrake));
            Assert.That(PilotInputBus.Current.throttle1, Is.EqualTo(0f));
            Assert.That(PilotInputBus.Current.flapLever, Is.EqualTo(0));
        }
    }
}
