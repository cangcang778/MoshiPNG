#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace MoshiTools
{
    public static class UnityBridgeLogger
    {
        private const int MaxMemoryLines = 120;
        private static readonly List<string> recentLines = new List<string>();

        public static IReadOnlyList<string> RecentLines => recentLines;

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Write(string level, string message)
        {
            UnityBridgeSafety.EnsureAssetFolder(UnityBridgeConstants.LogsFolder);
            string line = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";
            recentLines.Add(line);
            while (recentLines.Count > MaxMemoryLines)
                recentLines.RemoveAt(0);

            string logPath = UnityBridgeSafety.AssetPathToAbsolutePath(UnityBridgeConstants.LogsFolder + $"/{DateTime.Now:yyyyMMdd}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.AppendAllText(logPath, line + Environment.NewLine);
            AssetDatabase.ImportAsset(UnityBridgeConstants.LogsFolder, ImportAssetOptions.ForceUpdate);
        }
    }
}
#endif
