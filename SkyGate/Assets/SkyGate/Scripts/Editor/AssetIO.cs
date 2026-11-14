using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SkyGate.EditorTools
{
    /// <summary>
    /// Lưu asset do generator tạo ra mà GIỮ NGUYÊN GUID: chạy lại menu sẽ ghi đè nội dung, không tạo "Mesh 1", "Mesh 2".
    /// </summary>
    public static class AssetIO
    {
        public const string Root = "Assets/SkyGate/Generated";
        public const string Meshes = Root + "/Meshes";
        public const string Textures = Root + "/Textures";
        public const string Materials = Root + "/Materials";
        public const string Prefabs = Root + "/Prefabs";
        public const string Data = "Assets/SkyGate/Data";
        public const string Settings = "Assets/SkyGate/Settings";
        public const string Scenes = "Assets/SkyGate/Scenes";

        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            if (i <= 0) return;
            string parent = path.Substring(0, i);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(i + 1));
        }

        public static Mesh SaveMesh(Mesh mesh, string name)
        {
            EnsureFolder(Meshes);
            string path = Meshes + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static T SaveAsset<T>(T obj, string path) where T : UnityEngine.Object
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(obj, existing);
                UnityEngine.Object.DestroyImmediate(obj);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        /// <summary>Lưu texture dạng PNG rồi cấu hình importer. Trả về asset đã import.</summary>
        public static Texture2D SavePng(Texture2D tex, string fileName, Action<TextureImporter> configure)
        {
            EnsureFolder(Textures);
            string path = Textures + "/" + fileName + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null)
            {
                configure?.Invoke(imp);
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
