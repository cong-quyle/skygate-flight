using SkyGate.Core;
using SkyGate.Rendering.Procedural;
using UnityEditor;
using UnityEngine;

namespace SkyGate.EditorTools
{
    /// <summary>
    /// G3: bake texture sơn thân (4096×1024), đuôi đứng (2048×2048) và vỏ trong có lỗ cửa sổ (2048×1024)
    /// từ LiveryDefinition. Mọi toạ độ lấy từ FuselageProfile/A320Windows nên sơn ngoài và lỗ trong luôn khớp nhau.
    /// </summary>
    public static class LiveryBaker
    {
        public const int FusW = 4096, FusH = 1024, TailW = 2048, TailH = 2048, ShellW = 2048, ShellH = 1024;
        const string LiveryPath = AssetIO.Data + "/Livery_VJ_Style.asset";

        static float Cov(float sdf, float texel) => Mathf.Clamp01(0.5f + sdf / texel);
        static Color Over(Color dst, Color src, float a) => Color.Lerp(dst, new Color(src.r, src.g, src.b, 1f), a);

        public static LiveryDefinition EnsureLivery()
        {
            var l = AssetDatabase.LoadAssetAtPath<LiveryDefinition>(LiveryPath);
            if (l != null) return l;
            AssetIO.EnsureFolder(AssetIO.Data);
            l = ScriptableObject.CreateInstance<LiveryDefinition>();
            AssetDatabase.CreateAsset(l, LiveryPath);
            AssetDatabase.SaveAssets();
            return l;
        }

        // ------------------------------------------------------------------ thân
        public static Texture2D BakeFuselage(LiveryDefinition liv)
        {
            var px = new Color32[FusW * FusH];
            const float texel = 0.011f;
            for (int y = 0; y < FusH; y++)
            {
                float theta = (y + 0.5f) / FusH * Mathf.PI * 2f;
                for (int x = 0; x < FusW; x++)
                {
                    float s = (x + 0.5f) / FusW * FuselageProfile.L;
                    Vector3 P = FuselageProfile.Surface(s, theta);
                    float cy = FuselageProfile.CenterY(s);
                    float yRel = P.y - A320Data.FuselageCenterY;
                    px[y * FusW + x] = ShadeFuselage(liv, s, yRel, P.x, cy, texel);
                }
            }
            var tex = new Texture2D(FusW, FusH, TextureFormat.RGBA32, true, false);
            tex.SetPixels32(px);
            return AssetIO.SavePng(tex, "Livery_Fuselage", imp => ConfigureColor(imp, 4096, wrapV: TextureWrapMode.Repeat));
        }

        static Color32 ShadeFuselage(LiveryDefinition liv, float s, float yRel, float x, float cy, float texel)
        {
            Color c = liv.fuselageBase;
            if (yRel < liv.bellySplitYRel) c = liv.bellyColor;
            if (s < 1.1f) c = liv.radomeColor;

            // dải trang trí phía đuôi: dải đỏ cong lên về phía đuôi, viền vàng phía trên
            if (liv.rearSwoosh && s > liv.swooshStartS)
            {
                float k = (s - liv.swooshStartS) / (FuselageProfile.L - liv.swooshStartS);
                float yc = -0.55f + (s - liv.swooshStartS) * 0.115f;
                float w = liv.swooshWidth * (0.7f + 0.9f * k);
                float dRed = w - Mathf.Abs(yRel - yc);
                float dYel = 0.055f - Mathf.Abs(yRel - (yc + w + 0.085f));
                c = Over(c, liv.swooshAccent, Cov(dYel, texel));
                c = Over(c, liv.swooshColor, Cov(dRed, texel));
            }

            // viền cửa
            for (int i = 0; i < A320Windows.Doors.Length; i++)
            {
                var d = A320Windows.Doors[i];
                if (!A320Windows.DoorAppliesToSide(d, x)) continue;
                if (Mathf.Abs(s - d.s) > d.w) continue;
                float sdf = A320Windows.DoorSdf(d, s, yRel);
                float line = 0.009f - Mathf.Abs(sdf);
                if (line > -texel) c = Over(c, liv.doorLineColor, Cov(line, texel));
            }

            // chữ hãng, số đăng ký
            float hw = Mathf.Abs(x);
            FuselageProfile.Dims(s, out float halfW, out _, out _);
            if (hw > 0.35f * halfW)
            {
                c = DrawText(c, liv, liv.titleText, liv.fuselageWordmark, liv.titleColor, s, yRel, x,
                    liv.titleCenterS, liv.titleCenterYRel, liv.titleHeight, texel);
                c = DrawText(c, liv, liv.registration, null, liv.registrationColor, s, yRel, x,
                    liv.registrationS, liv.registrationYRel, liv.registrationHeight, texel);
            }

            // cửa sổ khách và kính buồng lái
            float sdfW = Mathf.Max(A320Windows.PassengerWindowSdf(s, yRel), A320Windows.CockpitWindowSdf(s, yRel, x));
            if (sdfW > -texel)
            {
                Color glass = liv.windowColor;
                float refl = Mathf.Clamp01(0.5f + yRel * 0.25f);
                glass = Color.Lerp(glass, new Color(0.18f, 0.24f, 0.32f), 0.35f * refl);
                c = Over(c, glass, Cov(sdfW, texel));
            }
            return c;
        }

        static Color DrawText(Color c, LiveryDefinition liv, string text, Texture2D logo, Color color,
            float s, float yRel, float x, float centerS, float centerY, float height, float texel)
        {
            if (string.IsNullOrEmpty(text) && logo == null) return c;
            float aspect = logo != null ? (float)logo.width / logo.height : BitmapFont5x7.WidthCells(text) / 7f;
            float width = height * aspect;
            if (Mathf.Abs(s - centerS) > width * 0.5f + 0.05f || Mathf.Abs(yRel - centerY) > height * 0.5f + 0.05f) return c;
            bool left = x < 0f;
            float cover = 0f;
            const int n = 2;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float ds = ((i + 0.5f) / n - 0.5f) * 0.0092f;
                    float dy = ((j + 0.5f) / n - 0.5f) * 0.012f;
                    float ss = s + ds, yy = yRel + dy;
                    // bên trái đọc từ mũi về đuôi, bên phải đọc từ đuôi về mũi
                    float u = left ? (ss - (centerS - width * 0.5f)) / width : ((centerS + width * 0.5f) - ss) / width;
                    float v = (yy - (centerY - height * 0.5f)) / height;
                    if (logo != null)
                    {
                        try { cover += u >= 0f && u <= 1f && v >= 0f && v <= 1f ? logo.GetPixelBilinear(u, v).a : 0f; }
                        catch { cover += BitmapFont5x7.Coverage(text, u, v); }
                    }
                    else cover += BitmapFont5x7.Coverage(text, u, v);
                }
            cover /= n * n;
            return cover > 0f ? Over(c, color, cover) : c;
        }

        // ------------------------------------------------------------------ đuôi đứng
        public static Texture2D BakeTail(LiveryDefinition liv)
        {
            A320Geometry.TailUvBounds(out float zMin, out float zMax, out float yMin, out float yMax);
            float Wm = zMax - zMin, Hm = yMax - yMin;
            var px = new Color32[TailW * TailH];
            float texel = Wm / (TailW * 0.5f);
            for (int y = 0; y < TailH; y++)
            {
                float v = (y + 0.5f) / TailH;
                for (int x = 0; x < TailW; x++)
                {
                    float u = (x + 0.5f) / TailW;
                    bool leftFace = u < 0.5f;
                    float pu = leftFace ? u * 2f : (u - 0.5f) * 2f; // tăng từ trái sang phải khi nhìn mặt đó
                    float frac = leftFace ? pu : 1f - pu;            // 0 = phía mũi, 1 = phía đuôi
                    Color c = liv.tailBase;
                    float yc = 0.16f + 0.55f * frac;
                    float w = 0.085f;
                    float dY = (0.035f - Mathf.Abs(v - (yc + w + 0.035f))) * Hm;
                    float dB = (w - Mathf.Abs(v - yc)) * Hm;
                    c = Over(c, liv.tailAccent, Cov(dY, texel));
                    c = Over(c, liv.tailAccent, Cov(dB, texel) * 0.35f);
                    if (liv.tailLogo != null)
                    {
                        float size = liv.tailLogoScale;
                        float lu = (pu - 0.5f) * Wm / (size * Hm) + 0.5f;
                        float lv = (v - 0.55f) / size + 0.5f;
                        if (lu >= 0f && lu <= 1f && lv >= 0f && lv <= 1f)
                        {
                            Color lc = Color.white;
                            try { lc = liv.tailLogo.GetPixelBilinear(lu, lv); } catch { lc.a = 0f; }
                            c = Color.Lerp(c, new Color(lc.r, lc.g, lc.b, 1f), lc.a);
                        }
                    }
                    else
                    {
                        float dx = (pu - 0.5f) * Wm, dy = (v - 0.55f) * Hm;
                        float rr = 0.9f - Mathf.Sqrt(dx * dx + dy * dy);
                        c = Over(c, liv.tailAccent, Cov(rr, texel));
                        float r2 = 0.55f - Mathf.Sqrt(dx * dx + dy * dy);
                        c = Over(c, liv.tailBase, Cov(r2, texel));
                    }
                    px[y * TailW + x] = c;
                }
            }
            var tex = new Texture2D(TailW, TailH, TextureFormat.RGBA32, true, false);
            tex.SetPixels32(px);
            return AssetIO.SavePng(tex, "Livery_Tail", imp => ConfigureColor(imp, 2048, wrapV: TextureWrapMode.Clamp));
        }

        // ------------------------------------------------------------------ vỏ trong
        public static Texture2D BakeShell(LiveryDefinition liv)
        {
            var px = new Color32[ShellW * ShellH];
            float texel = FuselageProfile.L / ShellW;
            for (int y = 0; y < ShellH; y++)
            {
                float theta = (y + 0.5f) / ShellH * Mathf.PI * 2f;
                for (int x = 0; x < ShellW; x++)
                {
                    float s = (x + 0.5f) / ShellW * FuselageProfile.L;
                    Vector3 P = FuselageProfile.Surface(s, theta);
                    float yRel = P.y - A320Data.FuselageCenterY;
                    float hole = A320Windows.HoleCoverage(s, yRel, P.x, texel);
                    Color c = Color.white;
                    // vùng cửa: tối hơn một chút
                    for (int i = 0; i < A320Windows.Doors.Length; i++)
                    {
                        var d = A320Windows.Doors[i];
                        if (!A320Windows.DoorAppliesToSide(d, P.x) || Mathf.Abs(s - d.s) > d.w) continue;
                        if (A320Windows.DoorSdf(d, s, yRel) > 0f) c = new Color(0.82f, 0.83f, 0.85f);
                    }
                    c.a = 1f - hole;
                    px[y * ShellW + x] = c;
                }
            }
            var tex = new Texture2D(ShellW, ShellH, TextureFormat.RGBA32, true, false);
            tex.SetPixels32(px);
            return AssetIO.SavePng(tex, "Livery_Shell", imp =>
            {
                ConfigureColor(imp, 2048, TextureWrapMode.Repeat);
                imp.alphaIsTransparency = true;
                imp.mipMapsPreserveCoverage = true;
                imp.alphaTestReferenceValue = 0.5f;
            });
        }

        static void ConfigureColor(TextureImporter imp, int maxSize, TextureWrapMode wrapV)
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.wrapModeU = TextureWrapMode.Clamp;
            imp.wrapModeV = wrapV;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.maxTextureSize = maxSize;
            imp.isReadable = false;
            imp.alphaIsTransparency = false;
            imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = maxSize,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 50,
            });
        }

        public static void BakeAll(LiveryDefinition liv, out Texture2D fuselage, out Texture2D tail, out Texture2D shell)
        {
            EditorUtility.DisplayProgressBar("SkyGate", "Bake sơn thân 4096×1024…", 0.1f);
            fuselage = BakeFuselage(liv);
            EditorUtility.DisplayProgressBar("SkyGate", "Bake sơn đuôi…", 0.5f);
            tail = BakeTail(liv);
            EditorUtility.DisplayProgressBar("SkyGate", "Bake vỏ trong…", 0.8f);
            shell = BakeShell(liv);
            EditorUtility.ClearProgressBar();
        }

        [MenuItem("SkyGate/Bake livery")]
        public static void MenuBake()
        {
            var liv = EnsureLivery();
            BakeAll(liv, out _, out _, out _);
            AssetDatabase.SaveAssets();
            Debug.Log("[SkyGate] Đã bake livery. Chạy 'SkyGate/1. Build A320 placeholder' để áp dụng vào prefab.");
        }
    }
}
