namespace SkyGate.Rendering.Aircraft
{
    /// <summary>Tên node theo "hợp đồng model" (plan mục 5.2). Model thật phải dùng đúng tên này.</summary>
    public static class AircraftPartNames
    {
        public const string Fuselage = "Fuselage";
        public static string Wing(char side) => "Wing_" + side;
        public static string Slat(char side, int i) => "Slat_" + side + "_" + i;
        public static string FlapIn(char side) => "FlapIn_" + side;
        public static string FlapOut(char side) => "FlapOut_" + side;
        public static string Aileron(char side) => "Aileron_" + side;
        public static string Spoiler(char side, int i) => "Spoiler_" + side + "_" + i;
        public static string HStab(char side) => "HStab_" + side;
        public static string Elevator(char side) => "Elevator_" + side;
        public const string VStab = "VStab", Rudder = "Rudder";
        public static string Engine(int i) => "Engine_" + i;
        public static string Fan(int i) => "Fan_" + i;
        public static string Reverser(int i) => "Reverser_" + i;
        public static string MainGear(char side) => "MainGear_" + side;
        public static string MainGearWheels(char side) => "MainGear_" + side + "_Wheels";
        public const string NoseGear = "NoseGear", NoseGearSteer = "NoseGear_Steer", NoseGearWheels = "NoseGear_Wheels";
        public const string DoorL1 = "Door_L1", DoorR1 = "Door_R1", DoorL4 = "Door_L4", DoorR4 = "Door_R4",
            DoorCargoFwd = "Door_CargoFwd", DoorCargoAft = "Door_CargoAft";
        public const string Interior = "Interior", Cockpit = "Cockpit", Cabin = "Cabin", Anchors = "Anchors";
        public const string EyeCpt = "Eye_CPT", EyeFo = "Eye_FO";
        public const string CamCabin12A = "Cam_Cabin_12A", CamCabin20F = "Cam_Cabin_20F", CamCabinAisle = "Cam_Cabin_Aisle";

        public static readonly string[] Attach =
        {
            "Attach_Towbar", "Attach_JetBridge_L1", "Attach_Stairs_L1", "Attach_Stairs_L4", "Attach_GPU",
            "Attach_Fuel_R", "Attach_CargoFwd", "Attach_CargoAft", "Attach_Catering_R1", "Attach_Catering_R4",
        };

        public static readonly string[] Required =
        {
            Fuselage, "Wing_L", "Wing_R", "FlapIn_L", "FlapIn_R", "FlapOut_L", "FlapOut_R", "Aileron_L", "Aileron_R",
            "HStab_L", "HStab_R", "Elevator_L", "Elevator_R", VStab, Rudder, "Engine_1", "Engine_2",
            "MainGear_L", "MainGear_R", NoseGear, NoseGearSteer, NoseGearWheels,
            Interior, Anchors, EyeCpt,
        };
    }
}
