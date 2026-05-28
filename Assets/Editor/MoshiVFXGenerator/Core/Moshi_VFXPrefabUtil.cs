#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator
{
    public static class Moshi_VFXPrefabUtil
    {
        public static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath)) return;

            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        public static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "VFX";
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c.ToString(), "_");
            return value.Trim();
        }

        public static string GetUniqueAssetPath(string path)
        {
            return AssetDatabase.GenerateUniqueAssetPath(path.Replace('\\', '/'));
        }

        public static string GetTransformPath(Transform root, Transform target)
        {
            if (root == null || target == null) return string.Empty;
            if (root == target) return root.name;

            string path = target.name;
            Transform parent = target.parent;
            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return root.name + "/" + path;
        }

        public static void DeleteCreatedAssets(System.Collections.Generic.List<string> paths)
        {
            if (paths == null) return;
            for (int i = paths.Count - 1; i >= 0; i--)
            {
                string path = paths[i];
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.Refresh();
        }
    }
}
#endif
