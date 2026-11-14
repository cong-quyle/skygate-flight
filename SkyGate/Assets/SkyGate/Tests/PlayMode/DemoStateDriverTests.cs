using System.Collections;
using NUnit.Framework;
using SkyGate.Core;
using SkyGate.Rendering.Demo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SkyGate.Tests.PlayMode
{
    /// <summary>
    /// DemoStateDriver là lớp "cầu" tạm: PilotInputBus -> AircraftVisualState. Test này giữ nguyên hợp đồng đó
    /// để khi FlightModel thật thay vào, rendering vẫn nhận đúng dữ liệu.
    /// </summary>
    public class DemoStateDriverTests : InputFixtureBase
    {
        GameObject _go;
        AircraftStateBus _bus;
        DemoStateDriver _driver;

        [TearDown]
        public void TearDown() { if (_go != null) Object.Destroy(_go); }

        void Create()
        {
            _go = new GameObject("DemoDriverTest");
            _bus = _go.AddComponent<AircraftStateBus>();
            _driver = _go.AddComponent<DemoStateDriver>();
        }

        static void Pilot(float throttle = 0f, bool gearDown = true, int flap = 0, bool parkingBrake = false,
            float pitch = 0f, float roll = 0f, float speedbrake = 0f)
        {
            var c = PilotControls.Default();
            c.throttle1 = c.throttle2 = throttle;
            c.gearDown = gearDown; c.flapLever = flap; c.parkingBrake = parkingBrake;
            c.pitch = pitch; c.roll = roll; c.speedbrake = speedbrake;
            PilotInputBus.Current = c;
        }

        [UnityTest]
        public IEnumerator Awake_PutsTheAircraftOnTheGround()
        {
            Create();
            yield return null;
            Assert.That(_bus.state.onGround, Is.True);
            Assert.That(_bus.state.gearPos, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(_bus.state.altitudeFt, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Update_WithoutBus_DoesNothing()
        {
            Create();
            _driver.bus = null;
            yield return null;
            Assert.That(_go != null, Is.True);
        }

        [UnityTest]
        public IEnumerator GearLever_FollowsThePilot_AndGearMovesGradually()
        {
            Create();
            Pilot(gearDown: false);
            yield return null;
            Assert.That(_bus.state.gearLever, Is.EqualTo(0f));
            Assert.That(_bus.state.gearPos, Is.LessThan(1f));
            Assert.That(_bus.state.gearPos, Is.GreaterThan(0.5f), "càng không thu tức thời (mất ~8 s)");
        }

        [UnityTest]
        public IEnumerator FlapLever_IsCopied_AndFlapsFollowSlowly()
        {
            Create();
            Pilot(flap: 3);
            yield return new WaitForSeconds(0.2f);
            Assert.That(_bus.state.flapLever, Is.EqualTo(3f));
            Assert.That(_bus.state.flapPos, Is.GreaterThan(0f));
            Assert.That(_bus.state.flapPos, Is.LessThan(3f));
        }

        [UnityTest]
        public IEnumerator ControlSurfaces_FollowPilotInput()
        {
            Create();
            Pilot(roll: 1f, pitch: -1f, speedbrake: 1f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(_bus.state.aileron, Is.GreaterThan(0.3f));
            Assert.That(_bus.state.elevator, Is.LessThan(-0.3f));
            Assert.That(_bus.state.speedbrake, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator Throttle_SpoolsUpTheEngines_AndBurnsFuel()
        {
            Create();
            float fuel0 = _bus.state.fuelKg;
            Pilot(throttle: 1f);
            yield return new WaitForSeconds(0.4f);
            Assert.That(_bus.state.thrustLever1, Is.GreaterThan(0f));
            Assert.That(_bus.state.n1Eng1, Is.GreaterThan(0f));
            Assert.That(_bus.state.egtEng1, Is.GreaterThanOrEqualTo(300f));
            Assert.That(_bus.state.fuelKg, Is.LessThan(fuel0));
            Assert.That(_bus.state.grossWeightKg, Is.EqualTo(57600f + _bus.state.fuelKg).Within(1e-2f));
        }

        [UnityTest]
        public IEnumerator ParkingBrake_KeepsTheAircraftStationary()
        {
            Create();
            Pilot(throttle: 1f, parkingBrake: true);
            yield return new WaitForSeconds(0.3f);
            Assert.That(_bus.state.gsKt, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator ReleasedBrakes_WithPower_AcceleratesAlongTheRunway()
        {
            Create();
            var s = _bus.state; s.n1Eng1 = s.n1Eng2 = 90f; s.thrustLever1 = s.thrustLever2 = 1f; _bus.state = s;
            Pilot(throttle: 1f);
            yield return new WaitForSeconds(0.4f);
            Assert.That(_bus.state.gsKt, Is.GreaterThan(0f));
            Assert.That(_bus.state.iasKt, Is.EqualTo(_bus.state.gsKt).Within(1e-3f));
            Assert.That(_bus.state.wheelRpmMain, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator ReverseThrust_DeploysReversersOnTheGround()
        {
            Create();
            Pilot(throttle: -1f);
            yield return new WaitForSeconds(0.5f);
            Assert.That(_bus.state.thrustLever1, Is.LessThan(-0.05f));
            Assert.That(_bus.state.reverserEng1, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator MoveAircraftFalse_FreezesTheTransform()
        {
            Create();
            _driver.moveAircraft = false;
            Vector3 p0 = _go.transform.position;
            var s = _bus.state; s.n1Eng1 = s.n1Eng2 = 90f; s.gsKt = 50f; _bus.state = s;
            Pilot(throttle: 1f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(_go.transform.position, Is.EqualTo(p0));
        }

        [UnityTest]
        public IEnumerator Rotation_AboveVr_TakesOff()
        {
            Create();
            var s = _bus.state; s.gsKt = 150f; s.iasKt = 150f; s.pitchDeg = 8f; _bus.state = s;
            Pilot(throttle: 1f, pitch: 1f);
            yield return null;
            yield return null;
            Assert.That(_bus.state.onGround, Is.False);
            Assert.That(_bus.state.vsFpm, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator Airborne_DescendingToZeroAltitude_Lands()
        {
            Create();
            var s = _bus.state; s.onGround = false; s.altitudeFt = 0.5f; s.iasKt = 150f; s.gsKt = 150f; s.pitchDeg = 0f; _bus.state = s;
            Pilot(pitch: 0f);
            yield return new WaitForSeconds(0.6f);
            Assert.That(_bus.state.onGround, Is.True);
            Assert.That(_bus.state.altitudeFt, Is.EqualTo(0f));
            Assert.That(_bus.state.vsFpm, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Airborne_Roll_TurnsTheHeading()
        {
            Create();
            var s = _bus.state; s.onGround = false; s.altitudeFt = 5000f; s.iasKt = 250f; s.gsKt = 250f; s.pitchDeg = 3f; s.headingDeg = 90f; _bus.state = s;
            Pilot(roll: 1f, throttle: 0.7f);
            yield return new WaitForSeconds(1.0f);
            Assert.That(_bus.state.rollDeg, Is.GreaterThan(5f));
            Assert.That(_bus.state.headingDeg, Is.GreaterThan(90f));
            Assert.That(_bus.state.headingDeg, Is.InRange(0f, 360f));
        }

        [UnityTest]
        public IEnumerator AircraftState_NeverContainsNaN()
        {
            Create();
            Pilot(throttle: 1f, roll: 0.5f, pitch: 0.5f);
            yield return new WaitForSeconds(0.5f);
            var s = _bus.state;
            foreach (float v in new[] { s.iasKt, s.tasKt, s.gsKt, s.mach, s.iasTrendKt, s.altitudeFt, s.vsFpm, s.pitchDeg, s.rollDeg, s.headingDeg, s.wheelRpmMain, s.wheelRpmNose })
                Assert.That(float.IsNaN(v) || float.IsInfinity(v), Is.False);
        }

        // ---------------------------------------------------------------- phím demo
        [UnityTest]
        public IEnumerator Key_L_CyclesLightPresets()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            Create();
            Input.Press(kb.lKey, queueEventOnly: true); yield return null;
            Input.Release(kb.lKey, queueEventOnly: true); yield return null;
            Assert.That(_bus.state.HasLight(AircraftLightFlags.Taxi), Is.True, "nấc 1 = lăn");
            Assert.That(_bus.state.HasLight(AircraftLightFlags.Beacon), Is.True);

            Input.Press(kb.lKey, queueEventOnly: true); yield return null;
            Input.Release(kb.lKey, queueEventOnly: true); yield return null;
            Assert.That(_bus.state.HasLight(AircraftLightFlags.Takeoff), Is.True, "nấc 2 = cất cánh");
            Assert.That(_bus.state.HasLight(AircraftLightFlags.Strobe), Is.True);
        }

        [UnityTest]
        public IEnumerator Key_D_OpensDoors_OnlyWhenParked()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            Create();
            Input.Press(kb.dKey, queueEventOnly: true); yield return null;
            Input.Release(kb.dKey, queueEventOnly: true);
            yield return new WaitForSeconds(0.5f);
            Assert.That(_bus.state.doorL1, Is.GreaterThan(0f));
            Assert.That(_bus.state.doorCargoAft, Is.EqualTo(_bus.state.doorL1));
        }

        [UnityTest]
        public IEnumerator Key_D_DoesNotOpenDoorsWhileMoving()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            Create();
            _driver.moveAircraft = false;
            var s = _bus.state; s.gsKt = 20f; _bus.state = s;
            Input.Press(kb.dKey, queueEventOnly: true); yield return null;
            Input.Release(kb.dKey, queueEventOnly: true);
            yield return new WaitForSeconds(0.3f);
            Assert.That(_bus.state.doorL1, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Key_P_TogglesMovement()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            Create();
            Assert.That(_driver.moveAircraft, Is.True);
            Input.Press(kb.pKey, queueEventOnly: true); yield return null;
            Input.Release(kb.pKey, queueEventOnly: true); yield return null;
            Assert.That(_driver.moveAircraft, Is.False);
        }
    }
}
