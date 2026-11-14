using SkyGate.Core;
using SkyGate.Rendering.Aircraft;
using SkyGate.Rendering.Cameras;
using SkyGate.Rendering.Cockpit;
using SkyGate.Rendering.Controls;
using SkyGate.Rendering.Demo;
using SkyGate.Rendering.Environment;
using SkyGate.Rendering.Quality;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SkyGate.EditorTools
{
    /// <summary>
    /// G7: dựng scene thử đồ họa "GraphicsSandbox": mặt đất, đường băng 3200×45 dọc +Z, sân đỗ, nhà ga/tháp giả để so kích thước,
    /// mặt trời + bầu trời, post-process, máy bay A320_VJ và toàn bộ hệ thống (camera, điều khiển cảm ứng, quality, HUD).
    /// </summary>
    public static class SandboxSceneBuilder
    {
        const string ScenePath = AssetIO.Scenes + "/GraphicsSandbox.unity";

        [MenuItem("SkyGate/2. Build Graphics Sandbox scene")]
        public static void Build()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(A320AssetBuilder.PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("SkyGate", "Chưa có prefab A320_VJ. Hãy chạy 'SkyGate/1. Build A320 placeholder' trước.", "OK");
                return;
            }
            AssetIO.EnsureFolder(AssetIO.Scenes);
            AssetIO.EnsureFolder(AssetIO.Settings);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- môi trường
            var sunGo = new GameObject("Sun");
            var sunLight = sunGo.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = 0.9f;
            var sunCtl = sunGo.AddComponent<SunController>();
            sunCtl.sun = sunLight;
            sunCtl.hour = 10f;
            sunCtl.skyTemplate = SkyTemplate();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2000f, 1f, 2000f);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            var grassTex = NoiseTexture("Sandbox_Grass", 256, new Color(0.22f, 0.34f, 0.15f), new Color(0.30f, 0.42f, 0.20f), 14f);
            var groundMat = MaterialFactory.Lit("Sandbox_Ground", Color.white, 0.05f, 0f, grassTex);
            groundMat.SetTextureScale("_BaseMap", new Vector2(2500f, 2500f));
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            var runway = Block("Runway", new Vector3(0f, 0.03f, 1600f), new Vector3(45f, 0.06f, 3200f),
                MaterialFactory.Lit("Sandbox_Runway", Color.white, 0.12f, 0f, RunwayTexture()));
            var apronMat = MaterialFactory.Lit("Sandbox_Apron", new Color(0.42f, 0.42f, 0.43f), 0.10f);
            Block("Apron", new Vector3(0f, 0.025f, -135f), new Vector3(140f, 0.05f, 230f), apronMat);
            Block("TaxiLink", new Vector3(0f, 0.027f, -10f), new Vector3(23f, 0.054f, 30f), apronMat);
            var bld = MaterialFactory.Lit("Sandbox_Building", new Color(0.7f, 0.72f, 0.76f), 0.2f);
            Block("Terminal", new Vector3(-160f, 12f, -150f), new Vector3(90f, 24f, 40f), bld);
            var towerShaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            towerShaft.name = "ControlTower";
            towerShaft.transform.position = new Vector3(-260f, 15f, -40f);
            towerShaft.transform.localScale = new Vector3(8f, 15f, 8f);
            towerShaft.GetComponent<Renderer>().sharedMaterial = bld;
            Object.DestroyImmediate(towerShaft.GetComponent<Collider>());
            var towerAnchor = new GameObject("TowerAnchor").transform;
            towerAnchor.position = new Vector3(-260f, 34f, -40f);

            // ---------- post-process
            var volGo = new GameObject("GlobalVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = BuildVolumeProfile();

            // ---------- máy bay
            var ac = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            ac.transform.SetPositionAndRotation(new Vector3(0f, 0f, -120f), Quaternion.identity);
            var bus = ac.GetComponent<AircraftStateBus>();
            var s = bus.state;
            s.headingDeg = 0f; s.trackDeg = 0f; s.selHeadingDeg = 0f;
            bus.state = s;
            var demo = ac.AddComponent<DemoStateDriver>();
            demo.bus = bus; demo.sun = sunCtl;

            // ---------- camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 30000f;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None; // đã có MSAA trong URP asset
            camData.requiresDepthTexture = false;
            camData.requiresColorTexture = false;
            var director = camGo.AddComponent<CameraDirector>();
            director.cam = cam; director.bus = bus; director.tower = towerAnchor;

            // ---------- hệ thống
            var sys = new GameObject("Systems");
            var touch = sys.AddComponent<TouchFlightControls>();
            touch.bus = bus;
            var quality = sys.AddComponent<GraphicsQualityManager>();
            quality.tier = QualityTier.High;
            quality.tierAssets = new[] { ProjectSetup.LoadTierAsset(0), ProjectSetup.LoadTierAsset(1), ProjectSetup.LoadTierAsset(2) };
            var fo = sys.AddComponent<FloatingOrigin>();
            fo.target = ac.transform;
            var hud = sys.AddComponent<DebugHud>();
            hud.director = director; hud.quality = quality; hud.sun = sunCtl; hud.demo = demo;
            var inter = sys.AddComponent<CockpitInteractor>();
            inter.director = director;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[SkyGate] Đã tạo " + ScenePath + ". Mở scene và bấm Play. Phím: L đèn, D cửa, N ngày/đêm, P dừng/chạy, C đổi camera, V camera phụ, "
                      + "G gear, F flap (Z+F lùi), B speedbrake, Shift/Ctrl cần ga, mũi tên hoặc W/S lái.");
        }

        static Material SkyTemplate()
        {
            const string path = AssetIO.Materials + "/Sandbox_Sky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var sh = Shader.Find("Skybox/Procedural");
                if (sh == null) return null;
                AssetIO.EnsureFolder(AssetIO.Materials);
                m = new Material(sh) { name = "Sandbox_Sky" };
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        static GameObject Block(string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void AddToBuildSettings(string path)
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == path) return;
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ post-process
        static VolumeProfile BuildVolumeProfile()
        {
            const string path = AssetIO.Settings + "/SkyGate_Volume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.contrast.Override(8f);
            ca.saturation.Override(6f);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ------------------------------------------------------------------ texture thủ tục
        static Texture2D NoiseTexture(string file, int n, Color a, Color b, float freq)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = Mathf.PerlinNoise(x / (float)n * freq, y / (float)n * freq) * 0.6f
                              + Mathf.PerlinNoise(x / (float)n * freq * 4.1f, y / (float)n * freq * 4.1f) * 0.4f;
                    px[y * n + x] = Color.Lerp(a, b, v);
                }
            t.SetPixels32(px);
            return AssetIO.SavePng(t, file, imp =>
            {
                imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Repeat; imp.anisoLevel = 4; imp.sRGBTexture = true;
            });
        }

        static Texture2D RunwayTexture()
        {
            const int W = 128, H = 2048;
            const float width = 45f, length = 3200f;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H * length;
                for (int x = 0; x < W; x++)
                {
                    float u = ((x + 0.5f) / W - 0.5f) * width;
                    float n = Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                    Color c = Color.Lerp(new Color(0.15f, 0.15f, 0.16f), new Color(0.21f, 0.21f, 0.22f), n);
                    float au = Mathf.Abs(u);
                    bool white = false;
                    if (au > 20.3f && au < 21.2f) white = true;                                           // vạch mép
                    if (au < 0.45f && v > 330f && v < length - 330f && Mathf.Repeat(v, 50f) < 30f) white = true; // vạch tim
                    if (v > 18f && v < 48f && au > 3f && au < 20f && Mathf.Repeat(au, 3.2f) < 1.6f) white = true; // vạch ngưỡng
                    if (v > length - 48f && v < length - 18f && au > 3f && au < 20f && Mathf.Repeat(au, 3.2f) < 1.6f) white = true;
                    foreach (float tz in new[] { 150f, 300f, 450f, 600f })                                // vùng chạm bánh
                        if (v > tz && v < tz + 22f && au > 6f && au < 9f) white = true;
                    if (white) c = Color.Lerp(c, new Color(0.9f, 0.9f, 0.88f), 0.9f);
                    px[y * W + x] = c;
                }
            }
            t.SetPixels32(px);
            return AssetIO.SavePng(t, "Sandbox_Runway", imp =>
            {
                imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Clamp; imp.anisoLevel = 8; imp.sRGBTexture = true; imp.maxTextureSize = 2048;
            });
        }
    }
}
