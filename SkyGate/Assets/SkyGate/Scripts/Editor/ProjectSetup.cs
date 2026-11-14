using System;
using System.Text;
using SkyGate.Rendering;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SkyGate.EditorTools
{
    /// <summary>
    /// G0: menu "SkyGate/0. Thiết lập dự án". Tạo layer, 3 URP asset (Low/Medium/High), QualitySettings,
    /// Player Settings Android (Vulkan, IL2CPP, ARM64, ASTC) tối ưu cho Redmi Turbo 4 Pro. Chạy lại an toàn nhiều lần.
    /// Tên thuộc tính của URP asset có thể đổi giữa các bản: thuộc tính nào không tìm thấy sẽ được ghi log, không làm hỏng gì.
    /// </summary>
    public static class ProjectSetup
    {
        static readonly string[] TierNames = { "Low", "Medium", "High" };
        static StringBuilder _log;

        [MenuItem("SkyGate/0. Thiết lập dự án")]
        public static void Run()
        {
            _log = new StringBuilder();
            Step("Layer", SetupLayers);
            Step("URP asset", SetupUrpAssets);
            Step("Quality Settings", SetupQualitySettings);
            Step("Player Settings Android", SetupPlayerSettings);
            Step("Input handler", SetupInput);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SkyGate] Thiết lập dự án xong:\n" + _log);
            EditorUtility.DisplayDialog("SkyGate", "Thiết lập xong. Xem Console để biết chi tiết.\nNếu Unity hỏi khởi động lại vì đổi Input handler, hãy chọn Yes.", "OK");
        }

        static void Step(string name, Action a)
        {
            try { a(); _log.AppendLine("  [OK]  " + name); }
            catch (Exception e) { _log.AppendLine("  [LỖI] " + name + ": " + e.Message); Debug.LogException(e); }
        }

        // ------------------------------------------------------------------ layer
        static void SetupLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            Set(layers, SkyLayers.AvionicsDefault, SkyLayers.AvionicsName);
            Set(layers, SkyLayers.InteriorDefault, SkyLayers.InteriorName);
            Set(layers, SkyLayers.HullDefault, SkyLayers.HullName);
            tm.ApplyModifiedProperties();
        }

        static void Set(SerializedProperty layers, int index, string name)
        {
            var el = layers.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(el.stringValue) && el.stringValue != name)
                _log.AppendLine("  [CẢNH BÁO] Layer " + index + " đã có tên '" + el.stringValue + "', ghi đè thành '" + name + "'.");
            el.stringValue = name;
        }

        // ------------------------------------------------------------------ URP
        static UniversalRenderPipelineAsset[] Assets = new UniversalRenderPipelineAsset[3];

        static void SetupUrpAssets()
        {
            AssetIO.EnsureFolder(AssetIO.Settings);
            ScriptableRendererData rendererData = null;
            var existing = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (existing != null)
            {
                var so = new SerializedObject(existing);
                var list = so.FindProperty("m_RendererDataList");
                if (list != null && list.arraySize > 0) rendererData = list.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
            }
            if (rendererData == null)
            {
                string rdPath = AssetIO.Settings + "/SkyGate_UniversalRenderer.asset";
                rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath);
                if (rendererData == null)
                {
                    var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rd, rdPath);
                    rendererData = rd;
                }
            }

            for (int t = 0; t < 3; t++)
            {
                string path = AssetIO.Settings + "/URP_" + TierNames[t] + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null)
                {
                    asset = UniversalRenderPipelineAsset.Create(rendererData);
                    AssetDatabase.CreateAsset(asset, path);
                }
                Configure(asset, t);
                EditorUtility.SetDirty(asset);
                Assets[t] = asset;
            }
            // High là pipeline mặc định (máy thử là Turbo 4 Pro)
            GraphicsSettings.defaultRenderPipeline = Assets[2];
        }

        public static UniversalRenderPipelineAsset LoadTierAsset(int tier) =>
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetIO.Settings + "/URP_" + TierNames[tier] + ".asset");

        static void Configure(UniversalRenderPipelineAsset a, int tier)
        {
            // tier: 0 Low, 1 Medium, 2 High (bảng plan mục 11.1)
            var so = new SerializedObject(a);
            float[] scale = { 0.65f, 0.75f, 0.80f };
            int[] msaa = { 1, 2, 4 };
            bool[] hdr = { false, false, true };
            int[] shadowRes = { 1024, 2048, 2048 };
            float[] shadowDist = { 60f, 120f, 200f };
            int[] cascades = { 1, 2, 2 };
            bool[] soft = { false, false, true };
            int[] addMode = { 1, 2, 2 };          // 1 = per vertex, 2 = per pixel
            int[] addLimit = { 1, 2, 4 };
            int[] lut = { 16, 32, 32 };

            F(so, "m_RenderScale", scale[tier]);
            I(so, "m_MSAA", msaa[tier]);
            B(so, "m_SupportsHDR", hdr[tier]);
            I(so, "m_HDRColorBufferPrecision", 0);   // 32 bit (R11G11B10)
            I(so, "m_MainLightShadowmapResolution", shadowRes[tier]);
            F(so, "m_ShadowDistance", shadowDist[tier]);
            I(so, "m_ShadowCascadeCount", cascades[tier]);
            F(so, "m_Cascade2Split", 0.2f);
            B(so, "m_SoftShadowsSupported", soft[tier]);
            I(so, "m_SoftShadowQuality", 1);
            I(so, "m_AdditionalLightsRenderingMode", addMode[tier]);
            I(so, "m_AdditionalLightsPerObjectLimit", addLimit[tier]);
            B(so, "m_AdditionalLightShadowsSupported", false);
            B(so, "m_RequireDepthTexture", false);   // tắt để Adreno không phải resolve tile
            B(so, "m_RequireOpaqueTexture", false);
            B(so, "m_UseSRPBatcher", true);
            B(so, "m_SupportsDynamicBatching", false);
            I(so, "m_StoreActionsOptimization", 0);  // Auto
            I(so, "m_ColorGradingLutSize", lut[tier]);
            so.ApplyModifiedProperties();
        }

        static void F(SerializedObject so, string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; else Miss(n); }
        static void I(SerializedObject so, string n, int v) { var p = so.FindProperty(n); if (p != null) p.intValue = v; else Miss(n); }
        static void B(SerializedObject so, string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; else Miss(n); }
        static void Miss(string n) => _log.AppendLine("  [CẢNH BÁO] Không tìm thấy thuộc tính URP '" + n + "' (đổi tên giữa các bản?).");

        // ------------------------------------------------------------------ Quality
        static void SetupQualitySettings()
        {
            var obj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (obj == null || obj.Length == 0) throw new Exception("Không đọc được QualitySettings.asset");
            var so = new SerializedObject(obj[0]);
            var levels = so.FindProperty("m_QualitySettings");
            if (levels == null) throw new Exception("Không thấy m_QualitySettings");
            if (levels.arraySize < 3) levels.arraySize = 3;
            else while (levels.arraySize > 3) levels.DeleteArrayElementAtIndex(levels.arraySize - 1);

            float[] lod = { 0.7f, 1.0f, 1.5f };
            int[] tex = { 1, 0, 0 };
            int[] aniso = { 0, 1, 1 };
            for (int i = 0; i < 3; i++)
            {
                var lv = levels.GetArrayElementAtIndex(i);
                SetStr(lv, "name", TierNames[i]);
                SetObj(lv, "customRenderPipeline", Assets[i] != null ? Assets[i] : LoadTierAsset(i));
                SetFloat(lv, "lodBias", lod[i]);
                SetInt(lv, "vSyncCount", 0);
                SetInt(lv, "antiAliasing", 0);
                SetInt(lv, "textureQuality", tex[i]);
                SetInt(lv, "anisotropicTextures", aniso[i]);
                SetInt(lv, "skinWeights", 2);
            }
            var def = so.FindProperty("m_PerPlatformDefaultQuality");
            if (def != null)
            {
                bool done = false;
                for (int i = 0; i < def.arraySize; i++)
                {
                    var e = def.GetArrayElementAtIndex(i);
                    var first = e.FindPropertyRelative("first");
                    var second = e.FindPropertyRelative("second");
                    if (first != null && second != null && first.stringValue == "Android") { second.intValue = 2; done = true; }
                }
                if (!done)
                {
                    def.arraySize++;
                    var e = def.GetArrayElementAtIndex(def.arraySize - 1);
                    var first = e.FindPropertyRelative("first"); var second = e.FindPropertyRelative("second");
                    if (first != null && second != null) { first.stringValue = "Android"; second.intValue = 2; }
                }
            }
            so.ApplyModifiedProperties();
        }

        static void SetStr(SerializedProperty p, string n, string v) { var q = p.FindPropertyRelative(n); if (q != null) q.stringValue = v; }
        static void SetInt(SerializedProperty p, string n, int v) { var q = p.FindPropertyRelative(n); if (q != null) q.intValue = v; }
        static void SetFloat(SerializedProperty p, string n, float v) { var q = p.FindPropertyRelative(n); if (q != null) q.floatValue = v; }
        static void SetObj(SerializedProperty p, string n, UnityEngine.Object v) { var q = p.FindPropertyRelative(n); if (q != null) q.objectReferenceValue = v; }

        // ------------------------------------------------------------------ Player Settings
        static void SetupPlayerSettings()
        {
            PlayerSettings.productName = "SkyGate";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.skygate.flight");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.graphicsJobs = true;
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
#pragma warning disable CS0618
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
#pragma warning restore CS0618
        }

        static void SetupInput()
        {
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (ps == null || ps.Length == 0) return;
            var so = new SerializedObject(ps[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p == null) { _log.AppendLine("  [CẢNH BÁO] Không tìm thấy activeInputHandler: đặt Active Input Handling = Both trong Player Settings."); return; }
            if (p.intValue != 2)
            {
                p.intValue = 2; // Both: Input System cho cảm ứng/phím, Input cũ cho cảm biến nghiêng
                so.ApplyModifiedProperties();
                _log.AppendLine("  Đã đặt Active Input Handling = Both (cần khởi động lại Editor).");
            }
        }
    }
}
