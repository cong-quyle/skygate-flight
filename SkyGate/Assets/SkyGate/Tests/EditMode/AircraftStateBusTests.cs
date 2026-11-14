using NUnit.Framework;
using SkyGate.Core;
using UnityEngine;

namespace SkyGate.Tests.EditMode
{
    public class AircraftStateBusTests
    {
        GameObject _go;
        AircraftStateBus _bus;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StateBus");
            _bus = _go.AddComponent<AircraftStateBus>();
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(_go); }

        [Test]
        public void NewBus_StartsParked()
        {
            Assert.That(_bus.state.onGround, Is.True);
            Assert.That(_bus.state.gearPos, Is.EqualTo(1f));
            Assert.That(_bus.livery == null, Is.True);
        }

        [Test]
        public void NewBus_HasDefaultFmaStrings()
        {
            Assert.That(_bus.fmaThrust, Is.EqualTo("MAN TOGA"));
            Assert.That(_bus.fmaVertical, Is.EqualTo("SRS"));
            Assert.That(_bus.fmaLateral, Is.EqualTo("RWY"));
            Assert.That(_bus.fmaApproach, Is.Empty);
            Assert.That(_bus.fmaEngage, Is.EqualTo("FD2"));
        }

        [Test]
        public void State_CanBeWrittenAndReadBack()
        {
            var s = _bus.state;
            s.iasKt = 250f; s.altitudeFt = 10000f; s.onGround = false;
            _bus.state = s;
            Assert.That(_bus.state.iasKt, Is.EqualTo(250f));
            Assert.That(_bus.state.altitudeFt, Is.EqualTo(10000f));
            Assert.That(_bus.state.onGround, Is.False);
        }

        [Test]
        public void Bus_IsOnePerAircraft()
        {
            Assert.That(typeof(AircraftStateBus).IsDefined(typeof(DisallowMultipleComponent), false), Is.True);
        }
    }
}
