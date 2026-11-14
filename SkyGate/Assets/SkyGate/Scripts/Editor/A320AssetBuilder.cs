using System.Text;
using SkyGate.Core;
using SkyGate.Rendering.Aircraft;
using SkyGate.Rendering.Avionics;
using UnityEditor;
using UnityEngine;

namespace SkyGate.EditorTools
{
    /// <summary>G5: menu dựng prefab A320_VJ từ generator. Chạy lại bao nhiêu lần cũng được (giữ GUID asset).</summary>
    public static class A320AssetBuilder
    {
        public const string PrefabPath = AssetIO.Prefabs + "/A320_VJ.prefab";

        [MenuItem("SkyGate/1. Build A320 placeholder")]
        public static void Build()
        {
            AssetIO.EnsureFolder(AssetIO.Meshes);
            AssetIO.EnsureFolder(AssetIO.Materials);
            AssetIO.EnsureFolder(AssetIO.Prefabs);
            AssetIO.EnsureFolder(AssetIO.Textures);

            var liv = LiveryBaker.EnsureLivery();
            LiveryBaker.BakeAll(liv, out var fus, out var tail, out var shell);
            var mats = A320Materials.Create(liv, fus, tail, shell);

            var root = new GameObject("A320_VJ");
            try
            {
                EditorUtility.DisplayProgressBar("SkyGate", "Dựng ngoại thất…", 0.3f);
                var ext = A320ExteriorGenerator.Build(root.transform, mats);
                EditorUtility.DisplayProgressBar("SkyGate", "Dựng nội thất…", 0.7f);
                A320InteriorGenerator.Build(root.transform, mats);

                var bus = root.AddComponent<AircraftStateBus>();
                bus.livery = liv;
                var rig = root.AddComponent<AircraftVisualRig>();
                rig.bus = bus;
                A320RigDefaults.Populate(rig);
                var lights = root.AddComponent<AircraftLights>();
                lights.bus = bus; lights.glowMaterial = mats.glow;
                var amb = root.AddComponent<InteriorAmbient>();
                amb.bus = bus;
                root.AddComponent<AvionicsSystem>().bus = bus;

                var lod = root.AddComponent<LODGroup>();
                lod.fadeMode = LODFadeMode.None;
                lod.SetLODs(new[]
                {
                    new LOD(0.25f, ext.lod0.ToArray()),
                    new LOD(0.08f, ext.lod1.ToArray()),
                    new LOD(0.004f, ext.lod2.ToArray()),
                });
                lod.RecalculateBounds();

                Validate(root.transform);
                var report = Report(root, ext);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[SkyGate] Đã build " + PrefabPath + "\n" + report);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Object.DestroyImmediate(root);
            }
        }

        static void Validate(Transform root)
        {
            foreach (string n in AircraftPartNames.Required)
                if (A320RigDefaults.FindDeep(root, n) == null)
                    Debug.LogWarning("[SkyGate] Thiếu node bắt buộc: " + n);
        }

        static string Report(GameObject root, A320ExteriorGenerator.Result ext)
        {
            long interior = 0;
            var interiorNode = A320RigDefaults.FindDeep(root.transform, AircraftPartNames.Interior);
            if (interiorNode != null)
                foreach (var mf in interiorNode.GetComponentsInChildren<MeshFilter>(true))
                    for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) interior += mf.sharedMesh.GetIndexCount(i) / 3;
            var sb = new StringBuilder();
            sb.AppendLine("Tam giác ngoại thất: LOD0 " + ext.triCountLod[0] + ", LOD1 " + ext.triCountLod[1] + ", LOD2 " + ext.triCountLod[2]);
            sb.AppendLine("Tam giác nội thất (buồng lái + khoang): " + interior);
            sb.Append("Dài " + A320Data.Length + " m, sải cánh " + A320Data.Wingspan + " m (số liệu thiết kế).");
            return sb.ToString();
        }
    }
}
