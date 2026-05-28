#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoshiTools
{
    public static class UnityBridgeSafety
    {
        public static void EnsureAllFolders()
        {
            EnsureAssetFolder(UnityBridgeConstants.PendingFolder);
            EnsureAssetFolder(UnityBridgeConstants.ProcessingFolder);
            EnsureAssetFolder(UnityBridgeConstants.DoneFolder);
            EnsureAssetFolder(UnityBridgeConstants.FailedFolder);
            EnsureAssetFolder(UnityBridgeConstants.CancelledFolder);
            EnsureAssetFolder(UnityBridgeConstants.TimeoutFolder);
            EnsureAssetFolder(UnityBridgeConstants.LogsFolder);
            EnsureAssetFolder(UnityBridgeConstants.DefaultOutputFolder);
            EnsureAssetFolder(UnityBridgeConstants.DefaultOutputFolder + "/Prefabs");
            EnsureAssetFolder(UnityBridgeConstants.DefaultOutputFolder + "/Materials");
            EnsureAssetFolder(UnityBridgeConstants.DefaultOutputFolder + "/Preview");
        }

        public static void EnsureAssetFolder(string folderPath)
        {
            folderPath = NormalizeAssetPath(folderPath).TrimEnd('/');
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") return;

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').Trim();
        }

        public static bool IsAllowedAssetPath(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            return assetPath.StartsWith(UnityBridgeConstants.CommandRoot, System.StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith(UnityBridgeConstants.DefaultOutputFolder, System.StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith(UnityBridgeConstants.BridgeSourceFolder, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool ValidateOutputFolder(string folderPath, out string error)
        {
            folderPath = NormalizeAssetPath(folderPath);
            if (string.IsNullOrEmpty(folderPath)) folderPath = UnityBridgeConstants.DefaultOutputFolder;
            if (!folderPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) && folderPath != "Assets")
            {
                error = "输出目录必须位于 Assets 下。";
                return false;
            }

            if (!folderPath.StartsWith(UnityBridgeConstants.DefaultOutputFolder, System.StringComparison.OrdinalIgnoreCase))
            {
                error = "MVP 阶段只允许输出到 Assets/MoShi/GeneratedVFX/。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static string AssetPathToAbsolutePath(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }

        public static string GetUniqueAssetPath(string assetPath)
        {
            return AssetDatabase.GenerateUniqueAssetPath(NormalizeAssetPath(assetPath));
        }

        public static string SanitizeFileName(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value)) value = fallback;
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c.ToString(), "_");
            value = value.Replace(" ", "_").Trim();
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        public static string GetHierarchyPath(Transform transform)
        {
            if (transform == null) return string.Empty;
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        public static bool IsHighRiskPreviewTarget(GameObject go)
        {
            if (go == null) return false;
            Component[] components = go.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component == null) continue;
                string typeName = component.GetType().Name;
                string fullName = component.GetType().FullName ?? string.Empty;
                if (typeName.Contains("JSBehaviour") || fullName.Contains("Puerts") || fullName.Contains("Puer"))
                    return true;
            }
            return false;
        }
    }
}
#endif
