#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoshiTools
{
    [InitializeOnLoad]
    public static class Moshi_UnityBridge
    {
        private const string ListeningPrefsKey = "Moshi_CodeBuddyBridge_IsListening";
        private static double lastScanTime;
        private static bool updateRegistered;

        public static bool IsListening => EditorPrefs.GetBool(ListeningPrefsKey, false);
        public static double ScanIntervalSeconds { get; set; } = 0.5d;

        static Moshi_UnityBridge()
        {
            UnityBridgeSafety.EnsureAllFolders();
            RecoverProcessingTasks();
            if (IsListening)
                StartListening(false);
        }

        public static void StartListening(bool savePrefs = true)
        {
            UnityBridgeSafety.EnsureAllFolders();
            if (savePrefs) EditorPrefs.SetBool(ListeningPrefsKey, true);
            if (!updateRegistered)
            {
                EditorApplication.update += EditorUpdate;
                updateRegistered = true;
            }
            UnityBridgeLogger.Info("CodeBuddy Unity Bridge 监听已开启。 ");
        }

        public static void StopListening(bool savePrefs = true)
        {
            if (savePrefs) EditorPrefs.SetBool(ListeningPrefsKey, false);
            if (updateRegistered)
            {
                EditorApplication.update -= EditorUpdate;
                updateRegistered = false;
            }
            UnityBridgeLogger.Info("CodeBuddy Unity Bridge 监听已停止。 ");
        }

        public static void ProcessPendingNow()
        {
            UnityBridgeSafety.EnsureAllFolders();
            ProcessOnePendingCommand();
        }

        public static string EnqueueCommand(UnityBridgeCommand command)
        {
            UnityBridgeSafety.EnsureAllFolders();
            if (command == null) command = new UnityBridgeCommand();
            if (string.IsNullOrEmpty(command.commandId)) command.commandId = GenerateCommandId();
            if (string.IsNullOrEmpty(command.version)) command.version = "1.0";
            if (string.IsNullOrEmpty(command.createdAt)) command.createdAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            if (string.IsNullOrEmpty(command.outputFolder)) command.outputFolder = UnityBridgeConstants.DefaultOutputFolder;
            if (command.payload == null) command.payload = new UnityBridgePayload();
            if (command.options == null) command.options = new UnityBridgeCommandOptions();

            string fileName = UnityBridgeSafety.SanitizeFileName(command.commandId, GenerateCommandId()) + ".json";
            string assetPath = UnityBridgeConstants.PendingFolder + "/" + fileName;
            string absolutePath = UnityBridgeSafety.AssetPathToAbsolutePath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(command, true));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            UnityBridgeLogger.Info($"写入测试命令：{command.commandId} / {command.commandType}");
            return assetPath;
        }

        public static string EnqueuePingCommand()
        {
            return EnqueueCommand(new UnityBridgeCommand
            {
                commandType = UnityBridgeCommandTypes.PingUnity,
                targetMode = "none",
                payload = new UnityBridgePayload { message = "hello_unity" }
            });
        }

        public static string EnqueueBlueBurstCommand()
        {
            return EnqueueCommand(new UnityBridgeCommand
            {
                commandType = UnityBridgeCommandTypes.CreateParticlePrefab,
                targetMode = "none",
                outputFolder = UnityBridgeConstants.DefaultOutputFolder,
                payload = new UnityBridgePayload
                {
                    vfxName = "FX_BlueBurst",
                    displayName = "蓝色爆点",
                    vfxType = "burst",
                    style = "tech",
                    duration = 0.6f,
                    loop = false,
                    colorPalette = new[] { "#4AA8FF", "#7B3DFF" },
                    scale = 1f,
                    sortingOrder = 0,
                    layers = new[]
                    {
                        new UnityBridgeVFXLayer
                        {
                            name = "Flash",
                            role = "flash",
                            particleCount = 24,
                            lifetime = 0.18f,
                            startSpeed = 0.2f,
                            startSize = 1.2f,
                            burstCount = 24,
                            color = "#7FD8FF",
                            materialHint = "additive"
                        },
                        new UnityBridgeVFXLayer
                        {
                            name = "Spark",
                            role = "spark",
                            particleCount = 48,
                            lifetime = 0.45f,
                            startSpeed = 2.5f,
                            startSize = 0.18f,
                            burstCount = 48,
                            color = "#4AA8FF",
                            materialHint = "additive"
                        }
                    }
                }
            });
        }

        public static string EnqueueModifySelectedCommand()
        {
            return EnqueueCommand(new UnityBridgeCommand
            {
                commandType = UnityBridgeCommandTypes.ModifySelectedParticle,
                targetMode = "selected",
                payload = new UnityBridgePayload
                {
                    modifyMode = "relative",
                    target = "selected",
                    adjustments = new UnityBridgeAdjustments
                    {
                        brightnessMultiplier = 1.25f,
                        particleCountMultiplier = 0.85f,
                        durationMultiplier = 0.85f,
                        sizeMultiplier = 1.05f,
                        speedMultiplier = 0.95f
                    }
                }
            });
        }

        public static UnityBridgeQueueStats GetQueueStats()
        {
            UnityBridgeSafety.EnsureAllFolders();
            return new UnityBridgeQueueStats
            {
                pending = CountJson(UnityBridgeConstants.PendingFolder, "*.json"),
                processing = CountJson(UnityBridgeConstants.ProcessingFolder, "*.json"),
                done = CountJson(UnityBridgeConstants.DoneFolder, "*_result.json"),
                failed = CountJson(UnityBridgeConstants.FailedFolder, "*_result.json"),
                cancelled = CountJson(UnityBridgeConstants.CancelledFolder, "*_result.json"),
                timeout = CountJson(UnityBridgeConstants.TimeoutFolder, "*_result.json")
            };
        }

        public static List<UnityBridgeResult> GetRecentResults(int maxCount)
        {
            List<FileInfo> files = new List<FileInfo>();
            CollectResultFiles(UnityBridgeConstants.DoneFolder, files);
            CollectResultFiles(UnityBridgeConstants.FailedFolder, files);
            CollectResultFiles(UnityBridgeConstants.TimeoutFolder, files);
            files.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));

            List<UnityBridgeResult> results = new List<UnityBridgeResult>();
            for (int i = 0; i < files.Count && results.Count < maxCount; i++)
            {
                try
                {
                    UnityBridgeResult result = JsonUtility.FromJson<UnityBridgeResult>(File.ReadAllText(files[i].FullName));
                    if (result != null) results.Add(result);
                }
                catch
                {
                    // 忽略损坏的历史结果
                    // Ignore broken history result
                }
            }
            return results;
        }

        public static void OpenCommandFolder()
        {
            UnityBridgeSafety.EnsureAllFolders();
            EditorUtility.RevealInFinder(UnityBridgeSafety.AssetPathToAbsolutePath(UnityBridgeConstants.CommandRoot));
        }

        public static void OpenOutputFolder()
        {
            UnityBridgeSafety.EnsureAllFolders();
            EditorUtility.RevealInFinder(UnityBridgeSafety.AssetPathToAbsolutePath(UnityBridgeConstants.DefaultOutputFolder));
        }

        public static void ClearCompletedResults()
        {
            DeleteJsonFiles(UnityBridgeConstants.DoneFolder);
            DeleteJsonFiles(UnityBridgeConstants.CancelledFolder);
            AssetDatabase.Refresh();
            UnityBridgeLogger.Info("已清理完成任务记录。 ");
        }

        public static void RecoverProcessingTasks()
        {
            UnityBridgeSafety.EnsureAllFolders();
            string processingPath = UnityBridgeSafety.AssetPathToAbsolutePath(UnityBridgeConstants.ProcessingFolder);
            if (!Directory.Exists(processingPath)) return;

            string[] files = Directory.GetFiles(processingPath, "*.json");
            foreach (string file in files)
            {
                string commandId = Path.GetFileNameWithoutExtension(file);
                UnityBridgeResult result = new UnityBridgeResult
                {
                    commandId = commandId,
                    status = UnityBridgeStatuses.Failed,
                    success = false,
                    message = "检测到 Unity 上次退出时仍有 processing 任务，已标记为疑似崩溃。",
                    error = new UnityBridgeError
                    {
                        errorCode = UnityBridgeErrorCodes.UnityCrashSuspected,
                        errorMessage = "上次任务未正常结束。",
                        recoverable = true,
                        suggestion = "请确认该任务是否需要重新执行。"
                    }
                };
                UnityBridgeCommandRunner.WriteResult(result, UnityBridgeConstants.FailedFolder);
                MoveFileToFolder(file, UnityBridgeConstants.FailedFolder);
                UnityBridgeLogger.Warn($"恢复残留 processing 任务：{commandId}");
            }
            AssetDatabase.Refresh();
        }

        private static string GenerateCommandId()
        {
            return $"cmd_{DateTime.Now:yyyyMMdd_HHmmssfff}";
        }

        private static void EditorUpdate()
        {
            if (!IsListening) return;
            if (EditorApplication.timeSinceStartup - lastScanTime < ScanIntervalSeconds) return;
            lastScanTime = EditorApplication.timeSinceStartup;
            ProcessOnePendingCommand();
        }


        private static void ProcessOnePendingCommand()
        {
            string pendingPath = UnityBridgeSafety.AssetPathToAbsolutePath(UnityBridgeConstants.PendingFolder);
            if (!Directory.Exists(pendingPath)) return;

            string[] files = Directory.GetFiles(pendingPath, "*.json");
            if (files.Length == 0) return;
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            string pendingFile = files[0];
            string processingFile = MoveFileToFolder(pendingFile, UnityBridgeConstants.ProcessingFolder);
            UnityBridgeResult result = UnityBridgeCommandRunner.Execute(processingFile);
            string finalFolder = result.status == UnityBridgeStatuses.Done
                ? UnityBridgeConstants.DoneFolder
                : result.status == UnityBridgeStatuses.Timeout
                    ? UnityBridgeConstants.TimeoutFolder
                    : UnityBridgeConstants.FailedFolder;
            UnityBridgeCommandRunner.WriteResult(result, finalFolder);
            MoveFileToFolder(processingFile, finalFolder);
            AssetDatabase.Refresh();
        }

        private static string MoveFileToFolder(string sourceFile, string targetAssetFolder)
        {
            UnityBridgeSafety.EnsureAssetFolder(targetAssetFolder);
            string targetFolder = UnityBridgeSafety.AssetPathToAbsolutePath(targetAssetFolder);
            Directory.CreateDirectory(targetFolder);
            string targetPath = Path.Combine(targetFolder, Path.GetFileName(sourceFile)).Replace('\\', '/');
            if (File.Exists(targetPath))
            {
                string name = Path.GetFileNameWithoutExtension(sourceFile);
                string ext = Path.GetExtension(sourceFile);
                targetPath = Path.Combine(targetFolder, $"{name}_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}").Replace('\\', '/');
            }
            File.Move(sourceFile, targetPath);
            return targetPath;
        }

        private static int CountJson(string assetFolder, string pattern)
        {
            string path = UnityBridgeSafety.AssetPathToAbsolutePath(assetFolder);
            return Directory.Exists(path) ? Directory.GetFiles(path, pattern).Length : 0;
        }

        private static void CollectResultFiles(string assetFolder, List<FileInfo> files)
        {
            string path = UnityBridgeSafety.AssetPathToAbsolutePath(assetFolder);
            if (!Directory.Exists(path)) return;
            foreach (string file in Directory.GetFiles(path, "*_result.json"))
                files.Add(new FileInfo(file));
        }

        private static void DeleteJsonFiles(string assetFolder)
        {
            string path = UnityBridgeSafety.AssetPathToAbsolutePath(assetFolder);
            if (!Directory.Exists(path)) return;
            foreach (string file in Directory.GetFiles(path, "*.json"))
                File.Delete(file);
        }
    }
}
#endif
