using UnityEngine;

namespace SkyGate.Rendering.Aircraft
{
    /// <summary>Bảng binding mặc định cho A320 (khớp AircraftPartNames). Model thật dùng cùng tên node thì chạy ngay.</summary>
    public static class A320RigDefaults
    {
        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static void Add(AircraftVisualRig rig, string node, Ch ch, Vector3 axis, float scale, float scaleNeg = 0f,
            bool translate = false, bool spin = false, bool hide = false)
        {
            var t = FindDeep(rig.transform, node);
            if (t == null) return;
            rig.bindings.Add(new PartBinding
            {
                node = t, channel = ch, axis = axis, scale = scale, scaleNeg = scaleNeg,
                translate = translate, spin = spin, hideWhenZero = hide,
            });
        }

        public static void Populate(AircraftVisualRig rig)
        {
            rig.bindings.Clear();
            Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;
            foreach (char sc in new[] { 'L', 'R' })
            {
                bool left = sc == 'L';
                for (int i = 1; i <= 5; i++)
                {
                    Add(rig, AircraftPartNames.Slat(sc, i), Ch.SlatDeg, X, 1f);
                    Add(rig, AircraftPartNames.Spoiler(sc, i), left ? Ch.SpoilerL : Ch.SpoilerR, X, 50f);
                }
                Add(rig, AircraftPartNames.FlapIn(sc), Ch.FlapDeg, X, -1f);
                Add(rig, AircraftPartNames.FlapIn(sc), Ch.FlapNorm, -Z, 0.65f, 0f, translate: true);
                Add(rig, AircraftPartNames.FlapOut(sc), Ch.FlapDeg, X, -1f);
                Add(rig, AircraftPartNames.FlapOut(sc), Ch.FlapNorm, -Z, 0.50f, 0f, translate: true);
                Add(rig, AircraftPartNames.Aileron(sc), Ch.Aileron, X, left ? -25f : 25f);
                Add(rig, AircraftPartNames.Elevator(sc), Ch.Elevator, X, 30f, 17f);
                Add(rig, AircraftPartNames.MainGear(sc), Ch.GearRetract, Z, left ? 90f : -90f);
                Add(rig, AircraftPartNames.MainGearWheels(sc), Ch.WheelMainDps, X, 1f, 0f, false, true);
            }
            Add(rig, AircraftPartNames.Rudder, Ch.Rudder, Y, -30f);
            Add(rig, AircraftPartNames.NoseGear, Ch.GearRetract, X, -95f);
            Add(rig, AircraftPartNames.NoseGearSteer, Ch.NoseSteer, Y, 1f);
            Add(rig, AircraftPartNames.NoseGearWheels, Ch.WheelNoseDps, X, 1f, 0f, false, true);

            Add(rig, AircraftPartNames.Fan(1), Ch.Fan1Dps, Z, 1f, 0f, false, true);
            Add(rig, AircraftPartNames.Fan(2), Ch.Fan2Dps, Z, 1f, 0f, false, true);
            Add(rig, AircraftPartNames.Reverser(1), Ch.Reverser1, -Z, 0.45f, 0f, true);
            Add(rig, AircraftPartNames.Reverser(2), Ch.Reverser2, -Z, 0.45f, 0f, true);

            Add(rig, AircraftPartNames.DoorL1, Ch.DoorL1, Y, 70f, 0f, false, false, true);
            Add(rig, AircraftPartNames.DoorR1, Ch.DoorR1, Y, -70f, 0f, false, false, true);
            Add(rig, AircraftPartNames.DoorL4, Ch.DoorL4, Y, 70f, 0f, false, false, true);
            Add(rig, AircraftPartNames.DoorR4, Ch.DoorR4, Y, -70f, 0f, false, false, true);
            Add(rig, AircraftPartNames.DoorCargoFwd, Ch.DoorCargoFwd, Y, -70f, 0f, false, false, true);
            Add(rig, AircraftPartNames.DoorCargoAft, Ch.DoorCargoAft, Y, -70f, 0f, false, false, true);

            // buồng lái
            foreach (string stick in new[] { "Sidestick_CPT", "Sidestick_FO" })
            {
                Add(rig, stick, Ch.Elevator, X, -12f);
                Add(rig, stick, Ch.Aileron, Z, -15f);
            }
            Add(rig, "ThrottleLever_1", Ch.Thrust1, X, 30f, 25f);
            Add(rig, "ThrottleLever_2", Ch.Thrust2, X, 30f, 25f);
            Add(rig, "FlapLever", Ch.FlapLever, X, -6f);
            Add(rig, "SpeedbrakeLever", Ch.SpeedbrakeLever, X, -25f);
            Add(rig, "GearLever", Ch.GearLever, -Y, 0.035f, 0f, true);
        }
    }
}
