using System;
using NUnit.Framework;
using SkyGate.Core;

namespace SkyGate.Tests.EditMode
{
    public class AircraftVisualStateTests
    {
        [Test]
        public void Parked_IsOnGround_WithGearDown_AndParkingLights()
        {
            var s = AircraftVisualState.Parked();
            Assert.That(s.onGround, Is.True);
            Assert.That(s.gearPos, Is.EqualTo(1f));
            Assert.That(s.gearLever, Is.EqualTo(1f));
            Assert.That(s.lights, Is.EqualTo(AircraftLightFlags.Nav | AircraftLightFlags.Cabin | AircraftLightFlags.Logo));
            Assert.That(s.baroHpa, Is.EqualTo(1013f));
            Assert.That(s.headingDeg, Is.EqualTo(s.trackDeg));
            Assert.That(s.selHeadingDeg, Is.EqualTo(s.headingDeg));
        }

        [Test]
        public void Parked_HasConsistentSpeedsAndWeights()
        {
            var s = AircraftVisualState.Parked();
            Assert.That(s.v1Kt, Is.LessThanOrEqualTo(s.vrKt));
            Assert.That(s.vrKt, Is.LessThanOrEqualTo(s.v2Kt));
            Assert.That(s.fuelKg, Is.GreaterThan(0f));
            Assert.That(s.grossWeightKg, Is.GreaterThan(s.fuelKg));
            Assert.That(s.iasKt, Is.EqualTo(0f));
            Assert.That(s.n1Eng1, Is.EqualTo(0f));
        }

        [Test]
        public void DefaultStruct_HasNoLights_AndIsNotOnGround()
        {
            var s = default(AircraftVisualState);
            Assert.That(s.lights, Is.EqualTo(AircraftLightFlags.None));
            Assert.That(s.onGround, Is.False);
        }

        [Test]
        public void HasLight_ReportsOnlyTheFlagsThatAreSet()
        {
            var s = AircraftVisualState.Parked();
            Assert.That(s.HasLight(AircraftLightFlags.Nav), Is.True);
            Assert.That(s.HasLight(AircraftLightFlags.Strobe), Is.False);
            Assert.That(s.HasLight(AircraftLightFlags.None), Is.False);
        }

        [Test]
        public void HasLight_WithCombinedMask_MeansAnyOfThem()
        {
            var s = AircraftVisualState.Parked();   // Nav, Cabin, Logo
            Assert.That(s.HasLight(AircraftLightFlags.Nav | AircraftLightFlags.Beacon), Is.True);
            Assert.That(s.HasLight(AircraftLightFlags.Beacon | AircraftLightFlags.Strobe), Is.False);
        }

        [Test]
        public void LightFlags_AreDistinctSingleBits()
        {
            int seen = 0;
            foreach (AircraftLightFlags f in Enum.GetValues(typeof(AircraftLightFlags)))
            {
                if (f == AircraftLightFlags.None) continue;
                int v = (int)f;
                Assert.That(v & (v - 1), Is.EqualTo(0), f + " is not a single bit");
                Assert.That(seen & v, Is.EqualTo(0), f + " overlaps another flag");
                seen |= v;
            }
        }

        [Test]
        public void Struct_IsCopiedByValue()
        {
            var a = AircraftVisualState.Parked();
            var b = a;
            b.gearPos = 0f;
            b.lights = AircraftLightFlags.None;
            Assert.That(a.gearPos, Is.EqualTo(1f));
            Assert.That(a.HasLight(AircraftLightFlags.Nav), Is.True);
        }
    }
}
