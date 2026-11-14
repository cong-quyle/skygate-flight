using System.Collections.Generic;
using System.Text;
using SkyGate.Rendering.Aircraft;
using UnityEditor;
using UnityEngine;

namespace SkyGate.EditorTools
{
    /// <summary>
    /// G11/G13: quy tắc import cho Assets/SkyGate/Art (chỉ áp dụng lần import đầu, để bạn vẫn sửa tay được)
    /// và kiểm tra "hợp đồng model" (plan mục 5.2) cho model máy bay: đủ node, hướng/kích thước, ngân sách tam giác theo LOD.
    /// </summary>
    public sealed class ArtImportRules : AssetPostprocessor
    {
        const string ArtRoot = "Assets/SkyGate/Art/";

        // ------------------------------------------------------------------ model
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var mi = (ModelImporter)assetImporter;
            if (!mi.importSettingsMissing) return;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Low;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.importAnimation = assetPath.Contains("_anim");
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ArtRoot) || !assetPath.Contains("/Aircraft/")) return;
            var sb = new StringBuilder("[SkyGate] Kiểm tra model máy bay: " + assetPath + "\n");
            bool ok = true;

            var names = new HashSet<string>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) names.Add(t.name);
            foreach (string n in AircraftPartNames.Required)
                if (!names.Contains(n)) { sb.AppendLine("  THIẾU node: " + n); ok = false; }

            if (root.transform.localRotation != Quaternion.identity || root.transform.localScale != Vector3.one)
            { sb.AppendLine("  Root phải có rotation = identity và scale = 1 (xuất FBX với Apply Transform)."); ok = false; }

            Bounds b = new Bounds();
            bool has = false;
            var tri = new int[3];
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var ext = Vector3.Scale(mb.extents, mf.transform.lossyScale);
                var c = mf.transform.TransformPoint(mb.center);
                var wb = new Bounds(c, ext * 2f);
                if (!has) { b = wb; has = true; } else b.Encapsulate(wb);
                int lod = mf.name.Contains("_LOD2") ? 2 : mf.name.Contains("_LOD1") ? 1 : 0;
                int t = 0;
                for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) t += (int)(mf.sharedMesh.GetIndexCount(i) / 3);
                tri[lod] += t;
            }
            if (has)
            {
                if (b.size.z < 30f || b.size.z > 45f)
                { sb.AppendLine("  Chiều dài theo Z = " + b.size.z.ToString("0.0") + " m (kỳ vọng 30–45 m). Kiểm tra đơn vị và hướng mũi +Z."); ok = false; }
                sb.AppendLine("  Kích thước: " + b.size.x.ToString("0.0") + " × " + b.size.y.ToString("0.0") + " × " + b.size.z.ToString("0.0") + " m");
            }
            sb.AppendLine("  Tam giác: LOD0 " + tri[0] + " (ngân sách 60–90k), LOD1 " + tri[1] + " (~25k), LOD2 " + tri[2] + " (~6k)");
            if (tri[0] > 100000) { sb.AppendLine("  LOD0 vượt ngân sách."); ok = false; }
            if (ok) Debug.Log(sb.ToString()); else Debug.LogWarning(sb.ToString());
        }

        // ------------------------------------------------------------------ texture
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var ti = (TextureImporter)assetImporter;
            if (!ti.importSettingsMissing) return;

            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool isNormal = n.EndsWith("_n") || n.EndsWith("_normal");
            bool isMask = n.EndsWith("_m") || n.EndsWith("_ao") || n.EndsWith("_mask") || n.EndsWith("_orm");

            int max = 1024;
            if (assetPath.Contains("/Aircraft/")) max = 2048;
            else if (assetPath.Contains("/Airport/")) max = 2048;

            ti.mipmapEnabled = true;
            ti.maxTextureSize = max;
            if (isNormal) ti.textureType = TextureImporterType.NormalMap;
            else if (isMask) ti.sRGBTexture = false;

            var android = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = max,
                format = (isNormal) ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6,
                compressionQuality = 50,
            };
            ti.SetPlatformTextureSettings(android);
        }
    }
}
