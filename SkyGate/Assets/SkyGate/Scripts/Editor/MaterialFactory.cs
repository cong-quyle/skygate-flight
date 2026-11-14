using UnityEditor;
using UnityEngine;

namespace SkyGate.EditorTools
{
    /// <summary>Tạo/cập nhật material URP/Lit (và hào quang additive) mà vẫn giữ GUID.</summary>
    public static class MaterialFactory
    {
        static Shader LitShader()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        static Material GetOrCreate(string name, Shader shader)
        {
            AssetIO.EnsureFolder(AssetIO.Materials);
            string path = AssetIO.Materials + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        public static Material Lit(string name, Color color, float smooth = 0.5f, float metal = 0f,
            Texture map = null, Color? emission = null, bool alphaClip = false, float cutoff = 0.5f, bool twoSided = false)
        {
            var m = GetOrCreate(name, LitShader());
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetTexture("_BaseMap", map);
            m.SetTexture("_MainTex", map);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
            m.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            m.SetFloat("_Cutoff", cutoff);
            if (alphaClip) { m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; m.SetOverrideTag("RenderType", "TransparentCutout"); }
            else { m.DisableKeyword("_ALPHATEST_ON"); m.renderQueue = -1; m.SetOverrideTag("RenderType", "Opaque"); }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Hào quang đèn: URP Particles/Unlit, additive, không ghi depth.</summary>
        public static Material Glow(string name, Texture2D tex)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = GetOrCreate(name, sh);
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", 5f);   // SrcAlpha
            m.SetFloat("_DstBlend", 1f);   // One
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Texture hào quang: gradient tròn, sáng ở giữa, tắt dần mượt.</summary>
        public static Texture2D GlowTexture()
        {
            const string file = "GlowRadial";
            string path = AssetIO.Textures + "/" + file + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    a = Mathf.Pow(a, 1.6f);
                    byte b = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, b);
                }
            t.SetPixels32(px);
            return AssetIO.SavePng(t, file, imp =>
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.sRGBTexture = true;
            });
        }
    }
}
