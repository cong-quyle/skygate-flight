using System.Collections.Generic;
using SkyGate.Core;
using SkyGate.Rendering;
using SkyGate.Rendering.Aircraft;
using SkyGate.Rendering.Avionics;
using SkyGate.Rendering.Cockpit;
using SkyGate.Rendering.Procedural;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkyGate.EditorTools
{
    /// <summary>
    /// G4: nội thất placeholder của A320: vỏ trong có lỗ cửa sổ, buồng lái (bảng, 6 màn hình, FCU, pedestal, overhead, ghế),
    /// khoang khách 180 ghế (3-3), hộc hành lý, bếp/toilet. Toạ độ theo (x, s, yRel), xem A320Data.
    /// </summary>
    public static class A320InteriorGenerator
    {
        const float FC = A320Data.FuselageCenterY;
        const float FloorY = FC + A320Data.CabinFloorRel;

        static Vector3 P(float x, float s, float yRel) => new Vector3(x, FC + yRel, A320Data.NoseZ - s);

        static Transform Node(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            return go.transform;
        }

        static MeshRenderer Attach(Transform t, Mesh mesh, ShadowCastingMode shadow, params Material[] mats)
        {
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = shadow;
            return r;
        }

        static Mesh Save(MeshBuilder mb, string name) => AssetIO.SaveMesh(mb.ToMesh(name), "A320_" + name);

        // ------------------------------------------------------------------ điểm vào
        public static Transform Build(Transform root, A320Materials m)
        {
            var interior = Node(root, AircraftPartNames.Interior, Vector3.zero, Quaternion.identity);
            var cockpit = Node(interior, AircraftPartNames.Cockpit, Vector3.zero, Quaternion.identity);
            var cabin = Node(interior, AircraftPartNames.Cabin, Vector3.zero, Quaternion.identity);
            Shell(interior, m);
            CabinParts(cabin, m);
            CockpitParts(cockpit, m);
            InteriorAnchors(root);
            SkyLayers.SetLayerRecursive(interior.gameObject, SkyLayers.Interior);
            return interior;
        }

        // ------------------------------------------------------------------ vỏ trong
        static void Shell(Transform parent, A320Materials m)
        {
            const float s0 = 0.75f, s1 = 32f, inset = 0.09f;
            var st = new List<float> { s0 };
            foreach (float s in FuselageProfile.Stations(1)) if (s > s0 + 0.05f && s < s1 - 0.05f) st.Add(s);
            st.Add(s1);
            const int seg = 48;
            int rows = st.Count, cols = seg + 1;
            var p = new Vector3[rows, cols];
            var n = new Vector3[rows, cols];
            var uv = new Vector2[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    float th = c / (float)seg * Mathf.PI * 2f;
                    Vector3 nrm = FuselageProfile.Normal(st[r], th);
                    p[r, c] = FuselageProfile.Surface(st[r], th) - nrm * inset;
                    n[r, c] = -nrm;
                    uv[r, c] = new Vector2(st[r] / FuselageProfile.L, c / (float)seg);
                }
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddGrid(p, n, uv);
            var front = new List<Vector3>(); var rear = new List<Vector3>();
            for (int c = 0; c < seg; c++) { front.Add(p[0, c]); rear.Add(p[rows - 1, c]); }
            mb.AddPolygon(front, Vector3.back, Vector2.one);     // vách trước buồng lái (quay về phía sau)
            mb.AddPolygon(rear, Vector3.forward, Vector2.one);   // vách cuối khoang (quay về phía mũi)
            // vách ngăn buồng lái / khoang khách ở s = 4,3 (hai mặt)
            var wall = new List<Vector3>();
            for (int c = 0; c < seg; c++)
            {
                float th = c / (float)seg * Mathf.PI * 2f;
                wall.Add(FuselageProfile.Surface(4.3f, th) - FuselageProfile.Normal(4.3f, th) * inset);
            }
            mb.AddPolygon(wall, Vector3.forward, Vector2.one);
            mb.AddPolygon(wall, Vector3.back, Vector2.one);

            var t = Node(parent, "Shell", Vector3.zero, Quaternion.identity);
            Attach(t, Save(mb, "Shell"), ShadowCastingMode.TwoSided, m.cabinWall);
        }

        // ------------------------------------------------------------------ khoang khách
        static Mesh FloorMesh(float sFrom, float sTo, int steps, string name)
        {
            int rows = steps + 1;
            var p = new Vector3[rows, 3];
            var n = new Vector3[rows, 3];
            var uv = new Vector2[rows, 3];
            for (int r = 0; r < rows; r++)
            {
                float s = Mathf.Lerp(sFrom, sTo, r / (float)steps);
                float hw = Mathf.Max(0.1f, FuselageProfile.HalfWidthAt(s, FloorY) - 0.10f);
                float z = A320Data.NoseZ - s;
                for (int c = 0; c < 3; c++)
                {
                    float x = (c - 1) * hw;
                    p[r, c] = new Vector3(x, FloorY, z);
                    n[r, c] = Vector3.up;
                    uv[r, c] = new Vector2(x * 0.5f, z * 0.5f);
                }
            }
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddGrid(p, n, uv);
            return Save(mb, name);
        }

        static void AddSeat(MeshBuilder mb, float x, float z)
        {
            Quaternion back = Quaternion.Euler(-10f, 0f, 0f);
            mb.Submesh(0);
            mb.AddBox(new Vector3(x, FloorY + 0.38f, z), new Vector3(0.44f, 0.10f, 0.46f));
            mb.AddBox(new Vector3(x, FloorY + 0.72f, z - 0.22f), new Vector3(0.44f, 0.55f, 0.09f), back);
            mb.Submesh(1);
            mb.AddBox(new Vector3(x, FloorY + 1.04f, z - 0.255f), new Vector3(0.30f, 0.15f, 0.07f), back);
            mb.Submesh(2);
            mb.AddBox(new Vector3(x - 0.17f, FloorY + 0.17f, z), new Vector3(0.05f, 0.34f, 0.30f));
            mb.AddBox(new Vector3(x + 0.17f, FloorY + 0.17f, z), new Vector3(0.05f, 0.34f, 0.30f));
            mb.AddBox(new Vector3(x - 0.225f, FloorY + 0.58f, z), new Vector3(0.035f, 0.035f, 0.34f));
            mb.AddBox(new Vector3(x + 0.225f, FloorY + 0.58f, z), new Vector3(0.035f, 0.035f, 0.34f));
        }

        public static float RowS(int row0Based)
        {
            float s = 7.6f + row0Based * 0.737f;
            if (row0Based >= 10) s += 0.12f;
            if (row0Based >= 11) s += 0.12f;
            return s;
        }

        static void CabinParts(Transform cabin, A320Materials m)
        {
            var fcockpit = Node(cabin, "Floor_Cockpit", Vector3.zero, Quaternion.identity);
            Attach(fcockpit, FloorMesh(1.0f, 4.3f, 6, "Floor_Cockpit"), ShadowCastingMode.Off, m.cockpitPlain);
            var fcabin = Node(cabin, "Floor_Cabin", Vector3.zero, Quaternion.identity);
            Attach(fcabin, FloorMesh(4.3f, 32f, 24, "Floor_Cabin"), ShadowCastingMode.Off, m.cabinCarpet);

            float[] xs = { -1.43f, -0.97f, -0.51f, 0.51f, 0.97f, 1.43f };
            for (int chunk = 0; chunk < 3; chunk++)
            {
                var mb = new MeshBuilder();
                for (int r = chunk * 10; r < chunk * 10 + 10; r++)
                {
                    float z = A320Data.NoseZ - RowS(r);
                    foreach (float x in xs) AddSeat(mb, x, z);
                }
                var t = Node(cabin, "Seats_" + (chunk + 1), Vector3.zero, Quaternion.identity);
                Attach(t, Save(mb, "Seats_" + (chunk + 1)), ShadowCastingMode.On, m.cabinSeat, m.cabinHeadrest, m.cabinFrame);
            }

            // hộc hành lý + dải đèn, bếp, toilet
            var bins = new MeshBuilder();
            float sMid = (7.2f + 31f) * 0.5f, len = 31f - 7.2f;
            float binY = FloorY + 1.795f;
            bins.Submesh(0);
            foreach (float sx in new[] { -1f, 1f })
            {
                bins.AddBox(new Vector3(sx * 1.15f, binY, A320Data.NoseZ - sMid), new Vector3(0.60f, 0.35f, len));
                bins.AddBox(new Vector3(sx * 1.35f, FloorY + 1.0f, A320Data.NoseZ - 4.95f), new Vector3(0.9f, 2.0f, 0.9f));
                bins.AddBox(new Vector3(sx * 1.20f, FloorY + 1.0f, A320Data.NoseZ - 31.2f), new Vector3(1.0f, 2.0f, 1.6f));
            }
            bins.Submesh(1);
            foreach (float sx in new[] { -1f, 1f })
                bins.AddBox(new Vector3(sx * 0.86f, binY - 0.19f, A320Data.NoseZ - sMid), new Vector3(0.03f, 0.02f, len));
            var bt = Node(cabin, "Bins", Vector3.zero, Quaternion.identity);
            Attach(bt, Save(bins, "Bins"), ShadowCastingMode.On, m.cabinBin, m.cabinLight);
        }

        // ------------------------------------------------------------------ buồng lái
        static Mesh LeverMesh(string name, float length, float knobW, float knobH)
        {
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddTube(Vector3.zero, new Vector3(0f, length, 0f), 0.012f, 0.012f, 8);
            mb.AddBox(new Vector3(0f, length + knobH * 0.5f, 0f), new Vector3(knobW, knobH, knobW * 1.2f));
            return Save(mb, name);
        }

        static Transform Control(Transform parent, string name, Vector3 localPos, Quaternion rot, Mesh mesh, Material mat,
            string id, ControlKind kind, Vector3 colSize, Vector3 colCenter)
        {
            var t = Node(parent, name, localPos, rot);
            Attach(t, mesh, ShadowCastingMode.Off, mat);
            var bc = t.gameObject.AddComponent<BoxCollider>();
            bc.size = colSize; bc.center = colCenter;
            if (!string.IsNullOrEmpty(id))
            {
                var cc = t.gameObject.AddComponent<CockpitControl>();
                cc.id = id; cc.kind = kind;
            }
            return t;
        }

        static Mesh BoxMesh(string name, Vector3 size)
        {
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddBox(Vector3.zero, size);
            return Save(mb, name);
        }

        static Mesh DisplayMesh(int cell)
        {
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddBox(new Vector3(0f, 0f, -0.065f), new Vector3(0.22f, 0.22f, 0.03f));
            mb.Submesh(1);
            int col = cell % 2, row = cell / 2;
            float u0 = col * 0.5f + 0.004f, u1 = col * 0.5f + 0.5f - 0.004f;
            float v0 = 0.5f * (1 - row) + 0.004f, v1 = 0.5f * (1 - row) + 0.5f - 0.004f;
            Vector3 nrm = Vector3.back;
            const float h = 0.095f, z = -0.0815f;
            int a = mb.AddVertex(new Vector3(-h, -h, z), nrm, new Vector2(u0, v0));
            int b = mb.AddVertex(new Vector3(h, -h, z), nrm, new Vector2(u1, v0));
            int c = mb.AddVertex(new Vector3(h, h, z), nrm, new Vector2(u1, v1));
            int d = mb.AddVertex(new Vector3(-h, h, z), nrm, new Vector2(u0, v1));
            mb.AddQuad(a, b, c, d);
            return Save(mb, "DU_Cell" + cell);
        }

        static void CockpitParts(Transform cp, A320Materials m)
        {
            // --- bảng đồng hồ chính, ngả 12° về phía phi công
            var panel = Node(cp, "Panel", P(0f, 2.0f, -0.04f), Quaternion.Euler(12f, 0f, 0f));
            Attach(panel, BoxMesh("Panel", new Vector3(2.0f, 0.52f, 0.10f)), ShadowCastingMode.Off, m.cockpitPanel);

            var meshes = new Dictionary<int, Mesh>();
            void DU(string name, int cell, float x, float y)
            {
                if (!meshes.TryGetValue(cell, out var mesh)) { mesh = DisplayMesh(cell); meshes[cell] = mesh; }
                var t = Node(panel, name, new Vector3(x, y, 0f), Quaternion.identity);
                var r = Attach(t, mesh, ShadowCastingMode.Off, m.cockpitPlain, m.cockpitDisplay);
                var du = t.gameObject.AddComponent<DisplayUnit>();
                du.cell = cell; du.screen = r;
            }
            DU("DU_CPT_PFD", 0, -0.70f, 0.08f);
            DU("DU_CPT_ND", 1, -0.46f, 0.08f);
            DU("DU_EWD", 2, 0f, 0.10f);
            DU("DU_SD", 3, 0f, -0.13f);
            DU("DU_FO_ND", 1, 0.46f, 0.08f);
            DU("DU_FO_PFD", 0, 0.70f, 0.08f);

            // cần càng trên bảng giữa
            var gearMesh = LeverMesh("GearLever", 0.0f, 0.03f, 0.05f);
            Control(panel, "GearLever", new Vector3(0.22f, -0.15f, -0.06f), Quaternion.Euler(-90f, 0f, 0f), gearMesh, m.cockpitButton,
                "GEAR_LEVER", ControlKind.Lever, new Vector3(0.10f, 0.12f, 0.10f), new Vector3(0f, 0.03f, 0f));

            // --- glareshield + FCU
            var glare = Node(cp, "Glareshield", P(0f, 2.0f, 0.26f), Quaternion.identity);
            Attach(glare, BoxMesh("Glareshield", new Vector3(2.0f, 0.08f, 0.30f)), ShadowCastingMode.Off, m.cockpitPanel);
            var fcu = Node(cp, "FCU", P(0f, 2.02f, 0.325f), Quaternion.identity);
            Attach(fcu, BoxMesh("FCU", new Vector3(0.9f, 0.05f, 0.14f)), ShadowCastingMode.Off, m.cockpitPlain);
            var knob = Mesh_Knob();
            string[] fcuIds = { "FCU_SPD", "FCU_HDG", "FCU_ALT", "FCU_VS" };
            for (int i = 0; i < 4; i++)
                Control(fcu, fcuIds[i] + "_Knob", new Vector3(-0.30f + i * 0.20f, 0.04f, 0f), Quaternion.identity, knob, m.cockpitButton,
                    fcuIds[i], ControlKind.Knob, new Vector3(0.09f, 0.07f, 0.09f), new Vector3(0f, 0.015f, 0f));

            // --- pedestal
            var ped = Node(cp, "Pedestal", P(0f, 2.7f, -0.25f), Quaternion.identity);
            Attach(ped, BoxMesh("Pedestal", new Vector3(0.50f, 0.50f, 1.30f)), ShadowCastingMode.Off, m.cockpitPanel);
            var mcdu = BoxMesh("MCDU", new Vector3(0.18f, 0.03f, 0.22f));
            foreach (float sx in new[] { -1f, 1f })
            {
                var t = Node(cp, sx < 0 ? "MCDU_CPT" : "MCDU_FO", P(sx * 0.13f, 2.3f, 0.01f), Quaternion.Euler(-12f, 0f, 0f));
                Attach(t, mcdu, ShadowCastingMode.Off, m.cockpitPlain);
            }
            var throttle = LeverMesh("ThrottleLever", 0.13f, 0.035f, 0.05f);
            Control(cp, "ThrottleLever_1", P(-0.06f, 2.75f, 0.0f), Quaternion.identity, throttle, m.cockpitButton,
                "TL_1", ControlKind.Lever, new Vector3(0.09f, 0.25f, 0.09f), new Vector3(0f, 0.10f, 0f));
            Control(cp, "ThrottleLever_2", P(0.06f, 2.75f, 0.0f), Quaternion.identity, throttle, m.cockpitButton,
                "TL_2", ControlKind.Lever, new Vector3(0.09f, 0.25f, 0.09f), new Vector3(0f, 0.10f, 0f));
            var flap = LeverMesh("FlapLever", 0.08f, 0.03f, 0.04f);
            Control(cp, "FlapLever", P(0.18f, 2.9f, 0.0f), Quaternion.identity, flap, m.cockpitButton,
                "FLAP_LEVER", ControlKind.Lever, new Vector3(0.09f, 0.18f, 0.09f), new Vector3(0f, 0.07f, 0f));
            Control(cp, "SpeedbrakeLever", P(-0.18f, 2.9f, 0.0f), Quaternion.identity, flap, m.cockpitButton,
                "SPDBRK", ControlKind.Lever, new Vector3(0.09f, 0.18f, 0.09f), new Vector3(0f, 0.07f, 0f));

            // --- bàn bên + sidestick
            foreach (float sx in new[] { -1f, 1f })
            {
                var con = Node(cp, sx < 0 ? "SideConsole_CPT" : "SideConsole_FO", P(sx * 1.22f, 2.35f, -0.12f), Quaternion.identity);
                Attach(con, BoxMesh("SideConsole", new Vector3(0.45f, 0.14f, 0.80f)), ShadowCastingMode.Off, m.cockpitPanel);
            }
            var stick = StickMesh();
            Control(cp, "Sidestick_CPT", P(-1.18f, 2.35f, -0.05f), Quaternion.identity, stick, m.cockpitPlain,
                null, ControlKind.Lever, new Vector3(0.12f, 0.25f, 0.12f), new Vector3(0f, 0.12f, 0f));
            Control(cp, "Sidestick_FO", P(1.18f, 2.35f, -0.05f), Quaternion.identity, stick, m.cockpitPlain,
                null, ControlKind.Lever, new Vector3(0.12f, 0.25f, 0.12f), new Vector3(0f, 0.12f, 0f));

            // --- overhead
            var oh = Node(cp, "Overhead", P(0f, 2.625f, 1.15f), Quaternion.identity);
            Attach(oh, BoxMesh("Overhead", new Vector3(0.96f, 0.20f, 1.35f)), ShadowCastingMode.Off, m.cockpitPanel);
            Overhead(cp, m);

            // --- ghế phi công
            var seat = new MeshBuilder();
            seat.Submesh(0);
            seat.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(0.50f, 0.10f, 0.50f));
            seat.AddBox(new Vector3(0f, 0.20f, 0.27f), new Vector3(0.50f, 0.90f, 0.10f));
            seat.AddBox(new Vector3(0f, 0.72f, 0.27f), new Vector3(0.30f, 0.20f, 0.08f));
            var seatMesh = Save(seat, "CockpitSeat");
            foreach (float sx in new[] { -1f, 1f })
            {
                var t = Node(cp, sx < 0 ? "Seat_CPT" : "Seat_FO", P(sx * 0.53f, 3.05f, 0f) , Quaternion.identity);
                // mesh dựng với "lưng ghế" ở +Z cục bộ nên đảo về phía đuôi: lưng ghế nằm phía sau (z nhỏ hơn)
                t.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Attach(t, seatMesh, ShadowCastingMode.Off, m.cockpitSeat);
            }
        }

        static Mesh Mesh_Knob()
        {
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddTube(Vector3.zero, new Vector3(0f, 0.03f, 0f), 0.022f, 0.022f, 14);
            return Save(mb, "FcuKnob");
        }

        static Mesh StickMesh()
        {
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddTube(Vector3.zero, new Vector3(0f, 0.15f, 0f), 0.015f, 0.018f, 8);
            mb.AddBox(new Vector3(0f, 0.18f, 0.01f), new Vector3(0.04f, 0.09f, 0.05f));
            return Save(mb, "Sidestick");
        }

        static void Overhead(Transform cp, A320Materials m)
        {
            // lưới nút 8×10 ở mặt dưới panel overhead; một số nút có id để tutorial dùng
            var named = new Dictionary<int, string>
            {
                { 0 * 10 + 0, "OVHD_BAT1" }, { 1 * 10 + 0, "OVHD_BAT2" },
                { 0 * 10 + 1, "OVHD_APU_MASTER" }, { 1 * 10 + 1, "OVHD_APU_START" },
                { 4 * 10 + 6, "OVHD_BEACON" }, { 5 * 10 + 6, "OVHD_NAV" },
                { 6 * 10 + 6, "OVHD_STROBE" }, { 7 * 10 + 6, "OVHD_LAND" }, { 3 * 10 + 8, "OVHD_SEATBELT" },
            };
            var btn = BoxMesh("OvhdButton", new Vector3(0.07f, 0.02f, 0.07f));
            var rest = new MeshBuilder();
            rest.Submesh(0);
            for (int c = 0; c < 8; c++)
                for (int r = 0; r < 10; r++)
                {
                    float x = -0.42f + c * 0.12f;
                    float s = 2.0f + r * 0.125f;
                    Vector3 pos = P(x, s, 1.04f);
                    if (named.TryGetValue(c * 10 + r, out string id))
                        Control(cp, id, pos, Quaternion.identity, btn, m.cockpitButton, id, ControlKind.Toggle,
                            new Vector3(0.11f, 0.08f, 0.11f), Vector3.zero);
                    else rest.AddBox(pos, new Vector3(0.07f, 0.02f, 0.07f));
                }
            var t = Node(cp, "Overhead_Buttons", Vector3.zero, Quaternion.identity);
            Attach(t, Save(rest, "Overhead_Buttons"), ShadowCastingMode.Off, m.cockpitButton);
        }

        // ------------------------------------------------------------------ anchor camera
        static void InteriorAnchors(Transform root)
        {
            var a = A320ExteriorGenerator.EnsureAnchors(root);
            Node(a, AircraftPartNames.EyeCpt, P(-0.53f, 2.75f, 0.45f), Quaternion.identity);
            Node(a, AircraftPartNames.EyeFo, P(0.53f, 2.75f, 0.45f), Quaternion.identity);
            Node(a, AircraftPartNames.CamCabin12A, P(-1.43f, RowS(11) - 0.2f, 0.42f), Quaternion.Euler(0f, 270f, 0f));
            Node(a, AircraftPartNames.CamCabin20F, P(1.43f, RowS(19) - 0.2f, 0.42f), Quaternion.Euler(0f, 90f, 0f));
            Node(a, AircraftPartNames.CamCabinAisle, P(0f, 6.9f, 0.50f), Quaternion.Euler(0f, 180f, 0f));
        }
    }
}
