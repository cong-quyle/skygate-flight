using System.Collections.Generic;
using SkyGate.Core;
using SkyGate.Rendering;
using SkyGate.Rendering.Aircraft;
using SkyGate.Rendering.Procedural;
using UnityEngine;

namespace SkyGate.EditorTools
{
    /// <summary>
    /// G2: dựng toàn bộ ngoại thất A320 bằng code, đúng hợp đồng node/pivot (plan mục 5.2).
    /// Mọi mesh được lưu vào Generated/Meshes (giữ GUID khi chạy lại).
    /// </summary>
    public static class A320ExteriorGenerator
    {
        public sealed class Result
        {
            public readonly List<Renderer> lod0 = new List<Renderer>();
            public readonly List<Renderer> lod1 = new List<Renderer>();
            public readonly List<Renderer> lod2 = new List<Renderer>();
            public Renderer hull;
            public readonly List<int> triCountLod = new List<int> { 0, 0, 0 };
        }

        // Dải sải (m) của các bộ phận trên cánh
        static readonly Vector2[] LeFixed = { new Vector2(0.8f, 1.9f), new Vector2(4.6f, 6.6f), new Vector2(16.9f, 17.05f) };
        static readonly Vector2[] Slats = { new Vector2(1.9f, 4.6f), new Vector2(6.6f, 9.2f), new Vector2(9.2f, 11.8f), new Vector2(11.8f, 14.4f), new Vector2(14.4f, 16.9f) };
        static readonly Vector2[] TeFixed = { new Vector2(0.8f, 1.6f), new Vector2(12.4f, 12.6f), new Vector2(16.2f, 17.05f) };
        static readonly Vector2[] Spoilers = { new Vector2(2f, 4f), new Vector2(4f, 6.2f), new Vector2(6.6f, 8.6f), new Vector2(8.6f, 10.5f), new Vector2(10.5f, 12.3f) };

        // ------------------------------------------------------------------ tiện ích node
        static Transform Node(Transform parent, string name, Vector3 pos, Quaternion rot, int layer = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.layer = layer;
            return go.transform;
        }

        static MeshRenderer Attach(Transform t, Mesh mesh, params Material[] mats)
        {
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            return r;
        }

        static Mesh Save(MeshBuilder mb, string name) => AssetIO.SaveMesh(mb.ToMesh(name), "A320_" + name);

        static void Count(Result res, int lod, Mesh m)
        {
            int t = 0;
            for (int i = 0; i < m.subMeshCount; i++) t += (int)(m.GetIndexCount(i) / 3);
            res.triCountLod[lod] += t;
        }

        // ------------------------------------------------------------------ điểm vào
        public static Result Build(Transform root, A320Materials m)
        {
            var res = new Result();
            var wing = A320Geometry.Wing();
            var sharklet = A320Geometry.Sharklet();
            var hstab = A320Geometry.HStab();
            var vstab = A320Geometry.VStab();

            Fuselage(root, m, res);
            foreach (int sideI in new[] { -1, 1 })
            {
                char sc = sideI < 0 ? 'L' : 'R';
                WingGroup(root, m, res, wing, sharklet, sideI, sc);
                TailHorizontal(root, m, res, hstab, sideI, sc);
            }
            TailVertical(root, m, res, vstab);
            Engine(root, 1, m, res);
            Engine(root, 2, m, res);
            MainGear(root, 'L', m, res);
            MainGear(root, 'R', m, res);
            NoseGear(root, m, res);
            Doors(root, m, res);
            LightNodes(root, sharklet);
            GroundAnchors(root);
            BodyLod(root, m, res, 1);
            BodyLod(root, m, res, 2);
            return res;
        }

        // ------------------------------------------------------------------ thân
        public static MeshBuilder FuselageBuilder(int q)
        {
            var st = FuselageProfile.Stations(q);
            int seg = FuselageProfile.Segments(q);
            int rows = st.Length, cols = seg + 1;
            var p = new Vector3[rows, cols];
            var n = new Vector3[rows, cols];
            var uv = new Vector2[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    float th = c / (float)seg * Mathf.PI * 2f;
                    p[r, c] = FuselageProfile.Surface(st[r], th);
                    n[r, c] = FuselageProfile.Normal(st[r], th);
                    uv[r, c] = new Vector2(st[r] / FuselageProfile.L, c / (float)seg);
                }
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddGrid(p, n, uv);
            // ống xả APU ở cuối thân
            var ring = new List<Vector3>();
            for (int c = 0; c < seg; c++) ring.Add(p[rows - 1, c]);
            mb.Submesh(1);
            mb.AddPolygon(ring, Vector3.back, Vector2.one);
            return mb;
        }

        static void Fuselage(Transform root, A320Materials m, Result res)
        {
            var mb = FuselageBuilder(0);
            var mesh = Save(mb, "Fuselage_LOD0");
            var t = Node(root, AircraftPartNames.Fuselage, Vector3.zero, Quaternion.identity, SkyLayers.Hull);
            var r = Attach(t, mesh, m.fuselage, m.engineDark);
            res.hull = r;
            res.lod0.Add(r);
            Count(res, 0, mesh);
        }

        // ------------------------------------------------------------------ cánh
        static void WingGroup(Transform root, A320Materials m, Result res, Planform w, Planform sh, int side, char sc)
        {
            float s = side;
            float x0 = A320Geometry.WingRootX, x1 = A320Geometry.WingTipX;

            // phần cố định
            var fixedMb = new MeshBuilder();
            fixedMb.Submesh(0);
            w.BuildPiece(fixedMb, s, x0, x1, 0.13f, 0.74f, 2.2f, 12, false, true);
            foreach (var r in LeFixed) w.BuildPiece(fixedMb, s, r.x, r.y, 0f, 0.13f, 2.2f, 6);
            foreach (var r in TeFixed) w.BuildPiece(fixedMb, s, r.x, r.y, 0.74f, 1f, 2.2f, 6);
            fixedMb.Submesh(1);
            sh.BuildPiece(fixedMb, s, 0f, A320Geometry.SharkletLen, 0f, 1f, 1.25f, 10);
            var fixedMesh = Save(fixedMb, "Wing_" + sc);
            var fixedNode = Node(root, AircraftPartNames.Wing(sc), Vector3.zero, Quaternion.identity);
            var fr = Attach(fixedNode, fixedMesh, m.wing, m.sharklet);
            res.lod0.Add(fr);
            Count(res, 0, fixedMesh);

            // slat
            for (int i = 0; i < Slats.Length; i++)
                MovingPiece(root, AircraftPartNames.Slat(sc, i + 1), w, s, Slats[i].x, Slats[i].y, 0f, 0.13f, 0.125f, -0.025f, 6, m.wing, res);
            // flap, aileron
            MovingPiece(root, AircraftPartNames.FlapIn(sc), w, s, 1.6f, 6.3f, 0.74f, 1f, 0.74f, 0f, 6, m.wing, res);
            MovingPiece(root, AircraftPartNames.FlapOut(sc), w, s, 6.3f, 12.4f, 0.74f, 1f, 0.74f, 0f, 6, m.wing, res);
            MovingPiece(root, AircraftPartNames.Aileron(sc), w, s, 12.6f, 16.2f, 0.74f, 1f, 0.74f, 0f, 6, m.wing, res);
            // spoiler
            for (int i = 0; i < Spoilers.Length; i++)
                SpoilerPlate(root, AircraftPartNames.Spoiler(sc, i + 1), w, s, Spoilers[i].x, Spoilers[i].y, m.wing, res);
        }

        static void MovingPiece(Transform parent, string name, Planform pf, float side, float e0, float e1,
            float a, float b, float hingeChord, float hingeY, int nChord, Material mat, Result res)
        {
            var mb = new MeshBuilder();
            mb.Submesh(0);
            pf.BuildPiece(mb, side, e0, e1, a, b, 3f, nChord);
            pf.HingeFrame(side, e0, e1, hingeChord, hingeY, out Vector3 origin, out Vector3 axis);
            Quaternion rot = MeshBuilder.FrameFromAxes(axis, Vector3.up);
            mb.Localize(origin, rot);
            var mesh = Save(mb, name);
            var t = Node(parent, name, origin, rot);
            var r = Attach(t, mesh, mat);
            res.lod0.Add(r);
            Count(res, 0, mesh);
        }

        static void SpoilerPlate(Transform parent, string name, Planform w, float side, float e0, float e1, Material mat, Result res)
        {
            const float xa = 0.60f, xb = 0.745f;
            Vector3 P(float eta, float x, float off)
            {
                var st = w.Sample(eta);
                return w.Point(side, eta, x, w.UpperY(eta, x) + off / st.chord);
            }
            var c = new Vector3[8];
            c[0] = P(e0, xa, -0.004f); c[1] = P(e1, xa, -0.004f); c[2] = P(e1, xb, -0.004f); c[3] = P(e0, xb, -0.004f);
            c[4] = P(e0, xa, 0.012f); c[5] = P(e1, xa, 0.012f); c[6] = P(e1, xb, 0.012f); c[7] = P(e0, xb, 0.012f);
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddHexahedron(c);
            float em = (e0 + e1) * 0.5f;
            w.HingeFrame(side, e0, e1, xa, w.UpperY(em, xa), out Vector3 origin, out Vector3 axis);
            Quaternion rot = MeshBuilder.FrameFromAxes(axis, Vector3.up);
            mb.Localize(origin, rot);
            var mesh = Save(mb, name);
            var t = Node(parent, name, origin, rot);
            var r = Attach(t, mesh, mat);
            res.lod0.Add(r);
            Count(res, 0, mesh);
        }

        // ------------------------------------------------------------------ đuôi
        static void TailHorizontal(Transform root, A320Materials m, Result res, Planform hs, int side, char sc)
        {
            float s = side;
            var mb = new MeshBuilder();
            mb.Submesh(0);
            hs.BuildPiece(mb, s, A320Geometry.HStabRootX, A320Geometry.HStabTipX, 0f, 0.70f, 3f, 10, false, true);
            var mesh = Save(mb, "HStab_" + sc);
            var t = Node(root, AircraftPartNames.HStab(sc), Vector3.zero, Quaternion.identity);
            var r = Attach(t, mesh, m.stab);
            res.lod0.Add(r);
            Count(res, 0, mesh);
            MovingPiece(root, AircraftPartNames.Elevator(sc), hs, s, A320Geometry.HStabRootX, A320Geometry.HStabTipX,
                0.70f, 1f, 0.70f, 0f, 8, m.stab, res);
        }

        static void TailVertical(Transform root, A320Materials m, Result res, Planform vs)
        {
            float y0 = A320Geometry.VStabRootY, y1 = A320Geometry.VStabTipY;
            var mb = new MeshBuilder();
            mb.Submesh(0);
            vs.BuildPiece(mb, 1f, y0, y1, 0f, 0.68f, 2f, 10, false, true, A320Geometry.TailUv);
            var mesh = Save(mb, "VStab");
            var t = Node(root, AircraftPartNames.VStab, Vector3.zero, Quaternion.identity);
            var r = Attach(t, mesh, m.tail);
            res.lod0.Add(r);
            Count(res, 0, mesh);

            // Rudder: trục quay = đường bản lề thẳng đứng. Local Y = trục bản lề.
            var rb = new MeshBuilder();
            rb.Submesh(0);
            vs.BuildPiece(rb, 1f, y0, y1, 0.68f, 1f, 2f, 8, true, true, A320Geometry.TailUv);
            Vector3 h0 = vs.Point(1f, y0, 0.68f, 0f), h1 = vs.Point(1f, y1, 0.68f, 0f);
            Vector3 origin = (h0 + h1) * 0.5f;
            Vector3 axis = (h1 - h0).normalized;
            Quaternion rot = Quaternion.LookRotation(Vector3.Cross(Vector3.right, axis), axis);
            rb.Localize(origin, rot);
            var rmesh = Save(rb, "Rudder");
            var rt = Node(root, AircraftPartNames.Rudder, origin, rot);
            var rr = Attach(rt, rmesh, m.tail);
            res.lod0.Add(rr);
            Count(res, 0, rmesh);
        }

        // ------------------------------------------------------------------ động cơ
        /// <summary>Mesh nacelle theo toạ độ cục bộ của động cơ (z = 0 ở mép cửa hút, trục +Z hướng mũi).</summary>
        static MeshBuilder NacelleBuilder(int segs, bool detailed)
        {
            var mb = new MeshBuilder();
            if (detailed)
            {
                mb.Submesh(0);
                mb.AddLathe(new[] { new Vector2(-0.18f, 0.975f), new Vector2(-0.8f, 0.99f), new Vector2(-2.4f, 0.99f) }, segs, Matrix4x4.identity);
                mb.Submesh(1);
                mb.AddLathe(new[]
                {
                    new Vector2(-0.95f, 0.78f), new Vector2(-0.45f, 0.80f), new Vector2(-0.06f, 0.85f),
                    new Vector2(0.0f, 0.89f), new Vector2(-0.06f, 0.945f), new Vector2(-0.18f, 0.975f),
                }, segs, Matrix4x4.identity);
                mb.Submesh(2);
                mb.AddLathe(new[] { new Vector2(-3.3f, 0.66f), new Vector2(-3.9f, 0.60f), new Vector2(-4.5f, 0.45f), new Vector2(-5.25f, 0.03f) }, segs, Matrix4x4.identity);
                mb.AddLathe(new[] { new Vector2(-2.4f, 0.80f), new Vector2(-3.5f, 0.72f) }, segs, Matrix4x4.identity);
                mb.Submesh(3); // pylon
                mb.AddHexahedron(new[]
                {
                    new Vector3(-0.11f, 0.88f, -0.8f), new Vector3(0.11f, 0.88f, -0.8f), new Vector3(0.11f, 0.88f, -5.0f), new Vector3(-0.11f, 0.88f, -5.0f),
                    new Vector3(-0.09f, 1.30f, -0.9f), new Vector3(0.09f, 1.30f, -0.9f), new Vector3(0.09f, 1.30f, -4.6f), new Vector3(-0.09f, 1.30f, -4.6f),
                });
            }
            else
            {
                mb.Submesh(4);
                mb.AddLathe(new[]
                {
                    new Vector2(-0.95f, 0.78f), new Vector2(0f, 0.88f), new Vector2(-0.18f, 0.975f), new Vector2(-2.4f, 0.99f),
                    new Vector2(-3.55f, 0.80f), new Vector2(-4.5f, 0.45f), new Vector2(-5.25f, 0.03f),
                }, segs, Matrix4x4.identity);
            }
            return mb;
        }

        static void Engine(Transform root, int idx, A320Materials m, Result res)
        {
            float side = idx == 1 ? -1f : 1f; // Engine_1 = trái
            var pos = new Vector3(side * A320Data.EngineSpanX, 1.65f, 3.8f);
            var eng = Node(root, AircraftPartNames.Engine(idx), pos, Quaternion.identity);
            var mb = NacelleBuilder(48, true);
            var mesh = Save(mb, "Engine_" + idx);
            var r = Attach(eng, mesh, m.nacelle, m.metal, m.engineDark, m.wing);
            res.lod0.Add(r);
            Count(res, 0, mesh);

            // ống đảo chiều (trượt về sau)
            var rv = new MeshBuilder();
            rv.Submesh(0);
            rv.AddLathe(new[] { new Vector2(-2.4f, 0.99f), new Vector2(-3.0f, 0.93f), new Vector2(-3.55f, 0.80f) }, 48, Matrix4x4.identity);
            var rvMesh = Save(rv, "Reverser_" + idx);
            var rvNode = Node(eng, AircraftPartNames.Reverser(idx), Vector3.zero, Quaternion.identity);
            res.lod0.Add(Attach(rvNode, rvMesh, m.nacelle));
            Count(res, 0, rvMesh);

            // quạt: nón đầu, đĩa và 22 cánh quạt
            var fb = new MeshBuilder();
            fb.Submesh(0);
            fb.AddLathe(new[] { new Vector2(0.30f, 0.0f), new Vector2(0.20f, 0.14f), new Vector2(0.0f, 0.26f) }, 24, Matrix4x4.identity);
            for (int k = 0; k < 22; k++)
            {
                float ang = k * (360f / 22f);
                Quaternion q = Quaternion.Euler(0f, 0f, ang) * Quaternion.Euler(35f, 0f, 0f);
                Vector3 c = Quaternion.Euler(0f, 0f, ang) * new Vector3(0.52f, 0f, 0f);
                fb.AddBox(c, new Vector3(0.50f, 0.012f, 0.14f), q);
            }
            fb.Submesh(1);
            fb.AddDisk(Vector3.zero, Vector3.forward, 0.77f, 32);
            var fanMesh = Save(fb, "Fan_" + idx);
            var fanNode = Node(eng, AircraftPartNames.Fan(idx), new Vector3(0f, 0f, -0.85f), Quaternion.identity);
            res.lod0.Add(Attach(fanNode, fanMesh, m.metal, m.engineDark));
            Count(res, 0, fanMesh);
        }

        // ------------------------------------------------------------------ càng
        static void AddWheel(MeshBuilder mb, Vector3 c, float halfW, float rOut, float rHub)
        {
            var prof = new[]
            {
                new Vector2(halfW, rHub), new Vector2(halfW, rOut * 0.86f), new Vector2(halfW * 0.66f, rOut),
                new Vector2(-halfW * 0.66f, rOut), new Vector2(-halfW, rOut * 0.86f), new Vector2(-halfW, rHub),
            };
            var xf = Matrix4x4.TRS(c, Quaternion.Euler(0f, 90f, 0f), Vector3.one);
            mb.Submesh(0);
            mb.AddLathe(prof, 32, xf);
            mb.Submesh(1);
            mb.AddDisk(c + Vector3.right * (halfW + 0.002f), Vector3.right, rHub, 20);
            mb.AddDisk(c - Vector3.right * (halfW + 0.002f), Vector3.left, rHub, 20);
        }

        static void MainGear(Transform root, char sc, A320Materials m, Result res)
        {
            float sx = sc == 'L' ? -1f : 1f;
            const float axleDrop = 2.165f;
            var attach = new Vector3(sx * A320Data.MainGearTrack * 0.5f, 2.75f, A320Data.MainGearZ);
            var g = Node(root, AircraftPartNames.MainGear(sc), attach, Quaternion.identity);
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddTube(Vector3.zero, new Vector3(0f, -axleDrop, 0f), 0.075f, 0.095f, 14);
            mb.AddTube(Vector3.zero, new Vector3(0f, -0.9f, 0f), 0.11f, 0.11f, 14);
            mb.AddTube(new Vector3(0f, -1.2f, 0f), new Vector3(-sx * 0.55f, -0.1f, 0.12f), 0.03f, 0.03f, 8);
            var mesh = Save(mb, "MainGear_" + sc);
            var r = Attach(g, mesh, m.gear);
            res.lod0.Add(r); res.lod1.Add(r);
            Count(res, 0, mesh);

            var wb = new MeshBuilder();
            AddWheel(wb, new Vector3(-0.46f, 0f, 0f), 0.18f, 0.585f, 0.30f);
            AddWheel(wb, new Vector3(0.46f, 0f, 0f), 0.18f, 0.585f, 0.30f);
            wb.Submesh(1);
            wb.AddTube(new Vector3(-0.46f, 0f, 0f), new Vector3(0.46f, 0f, 0f), 0.05f, 0.05f, 10, false);
            var wmesh = Save(wb, "MainGear_" + sc + "_Wheels");
            var wn = Node(g, AircraftPartNames.MainGearWheels(sc), new Vector3(0f, -axleDrop, 0f), Quaternion.identity);
            var wr = Attach(wn, wmesh, m.tire, m.metal);
            res.lod0.Add(wr); res.lod1.Add(wr);
            Count(res, 0, wmesh);
        }

        static void NoseGear(Transform root, A320Materials m, Result res)
        {
            const float drop = 2.12f;
            var g = Node(root, AircraftPartNames.NoseGear, new Vector3(0f, 2.5f, A320Data.NoseZ - 5.86f), Quaternion.identity);
            var steer = Node(g, AircraftPartNames.NoseGearSteer, Vector3.zero, Quaternion.identity);
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddTube(Vector3.zero, new Vector3(0f, -drop, 0f), 0.06f, 0.075f, 12);
            mb.AddTube(Vector3.zero, new Vector3(0f, -0.7f, 0f), 0.09f, 0.09f, 12);
            var mesh = Save(mb, "NoseGear_Steer");
            var r = Attach(steer, mesh, m.gear);
            res.lod0.Add(r); res.lod1.Add(r);
            Count(res, 0, mesh);

            var wb = new MeshBuilder();
            AddWheel(wb, new Vector3(-0.2f, 0f, 0f), 0.12f, 0.38f, 0.20f);
            AddWheel(wb, new Vector3(0.2f, 0f, 0f), 0.12f, 0.38f, 0.20f);
            wb.Submesh(1);
            wb.AddTube(new Vector3(-0.2f, 0f, 0f), new Vector3(0.2f, 0f, 0f), 0.04f, 0.04f, 8, false);
            var wmesh = Save(wb, "NoseGear_Wheels");
            var wn = Node(steer, AircraftPartNames.NoseGearWheels, new Vector3(0f, -drop, 0f), Quaternion.identity);
            var wr = Attach(wn, wmesh, m.tire, m.metal);
            res.lod0.Add(wr); res.lod1.Add(wr);
            Count(res, 0, wmesh);

            // đèn gắn trên chân càng mũi
            Node(steer, "Light_Taxi", new Vector3(0f, -0.9f, 0.15f), Quaternion.identity);
            Node(steer, "Light_TakeOff", new Vector3(0f, -0.95f, 0.15f), Quaternion.identity);
            Node(steer, "Light_TurnoffL", new Vector3(-0.25f, -1.0f, 0.1f), Quaternion.Euler(0f, -30f, 0f));
            Node(steer, "Light_TurnoffR", new Vector3(0.25f, -1.0f, 0.1f), Quaternion.Euler(0f, 30f, 0f));
        }

        // ------------------------------------------------------------------ cửa
        static void Doors(Transform root, A320Materials m, Result res)
        {
            string[] names =
            {
                AircraftPartNames.DoorL1, AircraftPartNames.DoorR1, AircraftPartNames.DoorL4, AircraftPartNames.DoorR4,
                AircraftPartNames.DoorCargoFwd, AircraftPartNames.DoorCargoAft,
            };
            string[] defs = { "L1", "R1", "L4", "R4", "CargoFwd", "CargoAft" };
            for (int i = 0; i < names.Length; i++)
            {
                A320Windows.DoorDef d = default;
                foreach (var dd in A320Windows.Doors) if (dd.name == defs[i]) d = dd;
                int side = d.side == 0 ? 1 : d.side;
                DoorNode(root, names[i], d, side, m, res);
            }
        }

        static void DoorNode(Transform root, string name, A320Windows.DoorDef d, int side, A320Materials m, Result res)
        {
            float s0 = d.s - d.w * 0.5f, s1 = d.s + d.w * 0.5f;
            float y0 = A320Data.FuselageCenterY + d.yBot, y1 = y0 + d.h;
            const int nS = 4, nY = 6;
            var p = new Vector3[nY + 1, nS + 1];
            var n = new Vector3[nY + 1, nS + 1];
            var uv = new Vector2[nY + 1, nS + 1];
            for (int r = 0; r <= nY; r++)
                for (int c = 0; c <= nS; c++)
                {
                    float s = Mathf.Lerp(s0, s1, c / (float)nS);
                    float y = Mathf.Lerp(y0, y1, r / (float)nY);
                    float th = FuselageProfile.ThetaForY(s, y, side);
                    Vector3 nn = FuselageProfile.Normal(s, th);
                    p[r, c] = FuselageProfile.Surface(s, th) + nn * 0.012f;
                    n[r, c] = nn;
                    uv[r, c] = new Vector2(c / (float)nS, r / (float)nY);
                }
            float ym = (y0 + y1) * 0.5f;
            Vector3 origin = FuselageProfile.Surface(s0, FuselageProfile.ThetaForY(s0, ym, side));
            for (int r = 0; r <= nY; r++)
                for (int c = 0; c <= nS; c++) p[r, c] -= origin;
            var mb = new MeshBuilder();
            mb.Submesh(0);
            mb.AddGrid(p, n, uv);
            var mesh = Save(mb, name);
            var t = Node(root, name, origin, Quaternion.identity);
            var rend = Attach(t, mesh, m.doorPlate);
            rend.enabled = false; // cửa đóng: dùng ô sơn trên thân. Rig bật khi mở
            res.lod0.Add(rend);
            Count(res, 0, mesh);
        }

        // ------------------------------------------------------------------ đèn và anchor
        static void LightNodes(Transform root, Planform sh)
        {
            var lights = Node(root, "Lights", Vector3.zero, Quaternion.identity);
            float L = A320Geometry.SharkletLen * 0.95f;
            Node(lights, "Light_NavL", sh.Point(-1f, L, 0.15f, 0f), Quaternion.identity);
            Node(lights, "Light_NavR", sh.Point(1f, L, 0.15f, 0f), Quaternion.identity);
            Node(lights, "Light_StrobeL", sh.Point(-1f, L, 1.0f, 0f), Quaternion.identity);
            Node(lights, "Light_StrobeR", sh.Point(1f, L, 1.0f, 0f), Quaternion.identity);
            float tailZ = A320Data.NoseZ - A320Data.Length - 0.15f;
            Node(lights, "Light_NavTail", new Vector3(0f, 5.0f, tailZ), Quaternion.identity);
            Node(lights, "Light_StrobeTail", new Vector3(0f, 5.05f, tailZ), Quaternion.identity);
            Vector3 top = FuselageProfile.Surface(17f, Mathf.PI);
            Vector3 bot = FuselageProfile.Surface(17f, 0f);
            Node(lights, "Light_BeaconTop", top + Vector3.up * 0.03f, Quaternion.identity);
            Node(lights, "Light_BeaconBottom", bot - Vector3.up * 0.03f, Quaternion.identity);
            Node(lights, "Light_LandL", new Vector3(-3.0f, 2.55f, 3.0f), Quaternion.Euler(3f, 0f, 0f));
            Node(lights, "Light_LandR", new Vector3(3.0f, 2.55f, 3.0f), Quaternion.Euler(3f, 0f, 0f));
            Node(lights, "Light_LogoL", new Vector3(-3.0f, 4.95f, A320Data.NoseZ - 33.0f), Quaternion.Euler(-35f, 0f, 0f));
            Node(lights, "Light_LogoR", new Vector3(3.0f, 4.95f, A320Data.NoseZ - 33.0f), Quaternion.Euler(-35f, 0f, 0f));
            Node(lights, "Light_WingL", new Vector3(-1.95f, 4.3f, A320Data.NoseZ - 11f), Quaternion.Euler(0f, -60f, 0f));
            Node(lights, "Light_WingR", new Vector3(1.95f, 4.3f, A320Data.NoseZ - 11f), Quaternion.Euler(0f, 60f, 0f));
        }

        public static Transform EnsureAnchors(Transform root)
        {
            var a = root.Find(AircraftPartNames.Anchors);
            if (a == null) a = Node(root, AircraftPartNames.Anchors, Vector3.zero, Quaternion.identity);
            return a;
        }

        static void GroundAnchors(Transform root)
        {
            var a = EnsureAnchors(root);
            float zL1 = A320Data.NoseZ - 6.1f, zL4 = A320Data.NoseZ - 31.0f;
            float doorMidY = A320Data.FuselageCenterY - 0.70f + 0.925f;
            float sillY = A320Data.FuselageCenterY - 0.70f;
            Node(a, "Attach_Towbar", new Vector3(0f, 0.4f, A320Data.NoseZ - 5.86f + 0.55f), Quaternion.identity);
            Node(a, "Attach_JetBridge_L1", new Vector3(-2.0f, doorMidY, zL1), Quaternion.Euler(0f, -90f, 0f));
            Node(a, "Attach_Stairs_L1", new Vector3(-2.0f, sillY, zL1), Quaternion.Euler(0f, -90f, 0f));
            Node(a, "Attach_Stairs_L4", new Vector3(-2.0f, sillY, zL4), Quaternion.Euler(0f, -90f, 0f));
            Node(a, "Attach_GPU", new Vector3(1.4f, 1.2f, A320Data.NoseZ - 3.5f), Quaternion.Euler(0f, 90f, 0f));
            Node(a, "Attach_Fuel_R", new Vector3(8.0f, 2.6f, -1.0f), Quaternion.Euler(0f, 90f, 0f));
            Node(a, "Attach_CargoFwd", new Vector3(2.0f, 2.82f, A320Data.NoseZ - 8.6f), Quaternion.Euler(0f, 90f, 0f));
            Node(a, "Attach_CargoAft", new Vector3(2.0f, 2.82f, A320Data.NoseZ - 24.5f), Quaternion.Euler(0f, 90f, 0f));
            Node(a, "Attach_Catering_R1", new Vector3(2.0f, doorMidY, zL1), Quaternion.Euler(0f, 90f, 0f));
            Node(a, "Attach_Catering_R4", new Vector3(2.0f, doorMidY, zL4), Quaternion.Euler(0f, 90f, 0f));
        }

        // ------------------------------------------------------------------ LOD 1 và 2 (thân gộp một mesh)
        static void BodyLod(Transform root, A320Materials m, Result res, int q)
        {
            var body = new MeshBuilder();
            body.Append(FuselageBuilder(q), Matrix4x4.identity, 0);              // sub 0 thân, sub 1 ống xả
            var w = A320Geometry.Wing();
            var sh = A320Geometry.Sharklet();
            var hs = A320Geometry.HStab();
            var vs = A320Geometry.VStab();
            int nc = q == 1 ? 8 : 4;
            float step = q == 1 ? 4f : 8f;
            foreach (float s in new[] { -1f, 1f })
            {
                var wm = new MeshBuilder(); wm.Submesh(0);
                w.BuildPiece(wm, s, A320Geometry.WingRootX, A320Geometry.WingTipX, 0f, 1f, step, nc, false, true);
                body.Append(wm, Matrix4x4.identity, 2);
                var sm = new MeshBuilder(); sm.Submesh(0);
                sh.BuildPiece(sm, s, 0f, A320Geometry.SharkletLen, 0f, 1f, 2.5f, nc);
                body.Append(sm, Matrix4x4.identity, 6);
                var hm = new MeshBuilder(); hm.Submesh(0);
                hs.BuildPiece(hm, s, A320Geometry.HStabRootX, A320Geometry.HStabTipX, 0f, 1f, 6f, nc, false, true);
                body.Append(hm, Matrix4x4.identity, 5);
                var nm = NacelleBuilder(q == 1 ? 20 : 10, false);
                body.Append(nm, Matrix4x4.Translate(new Vector3(s * A320Data.EngineSpanX, 1.65f, 3.8f)), 0);
            }
            var vm = new MeshBuilder(); vm.Submesh(0);
            vs.BuildPiece(vm, 1f, A320Geometry.VStabRootY, A320Geometry.VStabTipY, 0f, 1f, 6f, nc, false, true, A320Geometry.TailUv);
            body.Append(vm, Matrix4x4.identity, 3);

            string name = "Body_LOD" + q;
            var mesh = Save(body, name);
            var t = Node(root, name, Vector3.zero, Quaternion.identity);
            var r = Attach(t, mesh, m.fuselage, m.engineDark, m.wing, m.tail, m.nacelle, m.stab, m.sharklet);
            (q == 1 ? res.lod1 : res.lod2).Add(r);
            Count(res, q, mesh);
        }
    }
}
