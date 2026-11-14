using SkyGate.Core;
using UnityEngine;

namespace SkyGate.EditorTools
{
    /// <summary>Bộ material của A320 theo "slot" (plan mục 5.2 và 6.2). Tạo qua Create(); tạo lại thì cập nhật tại chỗ.</summary>
    public sealed class A320Materials
    {
        // Ngoại thất
        public Material fuselage, tail, wing, stab, nacelle, sharklet, metal, engineDark, gear, tire, doorPlate;
        public Material lensRed, lensGreen, lensWhite, glow;
        // Buồng lái
        public Material cockpitPanel, cockpitShell, cockpitDisplay, cockpitPlain, cockpitButton, cockpitSeat;
        // Khoang khách
        public Material cabinWall, cabinSeat, cabinHeadrest, cabinFrame, cabinCarpet, cabinBin, cabinLight;

        public static A320Materials Create(LiveryDefinition liv, Texture2D fuselageTex, Texture2D tailTex, Texture2D shellTex)
        {
            var m = new A320Materials();
            // Thân dùng texture livery bake sẵn nên màu gốc = trắng
            m.fuselage = MaterialFactory.Lit("A320_Fuselage", Color.white, 0.62f, 0.05f, fuselageTex);
            m.tail = MaterialFactory.Lit("A320_Tail", Color.white, 0.62f, 0.05f, tailTex);
            m.wing = MaterialFactory.Lit("A320_Wing", liv.wingColor, 0.45f, 0.25f);
            m.stab = MaterialFactory.Lit("A320_Stab", liv.stabColor, 0.50f, 0.15f);
            m.nacelle = MaterialFactory.Lit("A320_Nacelle", liv.nacelleColor, 0.60f, 0.05f);
            m.sharklet = MaterialFactory.Lit("A320_Sharklet", liv.sharkletColor, 0.60f, 0.05f);
            m.metal = MaterialFactory.Lit("A320_Metal", new Color(0.75f, 0.77f, 0.80f), 0.75f, 0.90f);
            m.engineDark = MaterialFactory.Lit("A320_EngineDark", new Color(0.10f, 0.10f, 0.11f), 0.30f, 0.40f);
            m.gear = MaterialFactory.Lit("A320_Gear", new Color(0.72f, 0.74f, 0.76f), 0.40f, 0.30f);
            m.tire = MaterialFactory.Lit("A320_Tire", new Color(0.04f, 0.04f, 0.04f), 0.15f, 0f);
            m.doorPlate = MaterialFactory.Lit("A320_DoorPlate", new Color(0.86f, 0.87f, 0.89f), 0.55f, 0.05f);
            m.lensRed = MaterialFactory.Lit("A320_LightLens_Red", new Color(0.4f, 0.02f, 0.02f), 0.8f, 0f, null, new Color(1.6f, 0.05f, 0.05f));
            m.lensGreen = MaterialFactory.Lit("A320_LightLens_Green", new Color(0.02f, 0.3f, 0.05f), 0.8f, 0f, null, new Color(0.05f, 1.4f, 0.2f));
            m.lensWhite = MaterialFactory.Lit("A320_LightLens_White", new Color(0.9f, 0.9f, 0.9f), 0.8f, 0f, null, new Color(1.5f, 1.5f, 1.4f));
            m.glow = MaterialFactory.Glow("M_Glow", MaterialFactory.GlowTexture());

            m.cockpitPanel = MaterialFactory.Lit("Cockpit_Panel", liv.cockpitPanelColor, 0.30f, 0.05f);
            m.cockpitShell = MaterialFactory.Lit("Cockpit_Shell", liv.cabinWallColor, 0.20f, 0f, shellTex, null, true, 0.5f, false);
            m.cockpitDisplay = MaterialFactory.Lit("Cockpit_Display", Color.black, 0.85f, 0f, null, Color.white);
            m.cockpitPlain = MaterialFactory.Lit("Cockpit_Plain", new Color(0.08f, 0.09f, 0.10f), 0.25f, 0.1f);
            m.cockpitButton = MaterialFactory.Lit("Cockpit_Button", new Color(0.55f, 0.58f, 0.62f), 0.35f, 0.3f, null, Color.black);
            m.cockpitSeat = MaterialFactory.Lit("Cockpit_Seat", new Color(0.12f, 0.12f, 0.13f), 0.20f, 0f);

            m.cabinWall = MaterialFactory.Lit("Cabin_Wall", liv.cabinWallColor, 0.20f, 0f, shellTex, null, true, 0.5f, false);
            m.cabinSeat = MaterialFactory.Lit("Cabin_Seat", liv.cabinSeatColor, 0.20f, 0f);
            m.cabinHeadrest = MaterialFactory.Lit("Cabin_Headrest", liv.cabinHeadrestColor, 0.20f, 0f);
            m.cabinFrame = MaterialFactory.Lit("Cabin_Frame", new Color(0.12f, 0.12f, 0.13f), 0.35f, 0.5f);
            m.cabinCarpet = MaterialFactory.Lit("Cabin_Carpet", liv.cabinCarpetColor, 0.05f, 0f);
            m.cabinBin = MaterialFactory.Lit("Cabin_Bin", liv.cabinWallColor * 0.97f, 0.25f, 0f);
            m.cabinLight = MaterialFactory.Lit("Cabin_Light", liv.cabinLightColor, 0.1f, 0f, null, liv.cabinLightColor * 1.4f);
            return m;
        }
    }
}
