#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoshiTools
{
    public static class UnityBridgeCommandRunner
    {
        public static UnityBridgeResult Execute(string processingFilePath)
        {
            UnityBridgeCommand command = null;
            try
            {
                string json = File.ReadAllText(processingFilePath);
                command = JsonUtility.FromJson<UnityBridgeCommand>(json);
                if (command == null)
                    return BuildInvalidResult(Path.GetFileNameWithoutExtension(processingFilePath), "命令 JSON 无法解析。", string.Empty);

                if (string.IsNullOrEmpty(command.commandId))
                    command.commandId = Path.GetFileNameWithoutExtension(processingFilePath);

                if (command.payload == null) command.payload = new UnityBridgePayload();
                if (command.options == null) command.options = new UnityBridgeCommandOptions();

                string validationError = ValidateCommand(command);
                if (!string.IsNullOrEmpty(validationError))
                    return UnityBridgeVFXCommands.Failure(command, UnityBridgeErrorCodes.InvalidCommand, validationError, "请检查命令协议字段。", true);

                Stopwatch stopwatch = Stopwatch.StartNew();
                UnityBridgeLogger.Info($"开始执行 {command.commandId} / {command.commandType}");
                UnityBridgeResult result = Dispatch(command);
                stopwatch.Stop();

                int timeoutSeconds = command.options.timeoutSeconds > 0 ? command.options.timeoutSeconds : UnityBridgeConstants.DefaultTimeoutSeconds;
                if (stopwatch.Elapsed.TotalSeconds > timeoutSeconds)
                {
                    result.status = UnityBridgeStatuses.Timeout;
                    result.success = false;
                    result.error = new UnityBridgeError
                    {
                        errorCode = UnityBridgeErrorCodes.ExecuteTimeout,
                        errorMessage = $"任务执行超过 {timeoutSeconds} 秒。",
                        recoverable = true,
                        suggestion = "请降低任务复杂度或拆分执行。"
                    };
                }

                UnityBridgeLogger.Info($"结束执行 {command.commandId} / {result.status}");
                return result;
            }
            catch (Exception ex)
            {
                UnityBridgeLogger.Error(ex.ToString());
                return command != null
                    ? UnityBridgeVFXCommands.Failure(command, UnityBridgeErrorCodes.UnityException, ex.Message, "请查看 Unity Console 和任务日志。", true, ex.ToString())
                    : BuildInvalidResult(Path.GetFileNameWithoutExtension(processingFilePath), ex.Message, ex.ToString());
            }
        }

        public static void WriteResult(UnityBridgeResult result, string targetFolder)
        {
            UnityBridgeSafety.EnsureAssetFolder(targetFolder);
            string commandId = string.IsNullOrEmpty(result.commandId) ? $"result_{DateTime.Now:yyyyMMdd_HHmmssfff}" : result.commandId;
            string resultPath = UnityBridgeSafety.AssetPathToAbsolutePath($"{targetFolder}/{commandId}_result.json");
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            AssetDatabase.ImportAsset(UnityBridgeSafety.NormalizeAssetPath($"{targetFolder}/{commandId}_result.json"), ImportAssetOptions.ForceUpdate);
        }

        private static UnityBridgeResult Dispatch(UnityBridgeCommand command)
        {
            switch (command.commandType)
            {
                case UnityBridgeCommandTypes.PingUnity:
                    return UnityBridgeVFXCommands.Ping(command);
                case UnityBridgeCommandTypes.GetSelectionInfo:
                    return UnityBridgeVFXCommands.GetSelectionInfo(command);
                case UnityBridgeCommandTypes.CreateParticlePrefab:
                    return UnityBridgeVFXCommands.CreateParticlePrefab(command);
                case UnityBridgeCommandTypes.ModifySelectedParticle:
                    return UnityBridgeVFXCommands.ModifySelectedParticle(command);
                case UnityBridgeCommandTypes.PreviewVFX:
                    return UnityBridgeVFXCommands.Preview(command);
                default:
                    return UnityBridgeVFXCommands.Failure(command, UnityBridgeErrorCodes.InvalidCommand, $"未知命令类型：{command.commandType}", "请使用 MVP 支持的命令类型。", true);
            }
        }

        private static string ValidateCommand(UnityBridgeCommand command)
        {
            if (string.IsNullOrEmpty(command.commandType))
                return "缺少 commandType。";

            if (!string.IsNullOrEmpty(command.outputFolder) && !UnityBridgeSafety.ValidateOutputFolder(command.outputFolder, out string outputError))
                return outputError;

            return string.Empty;
        }

        private static UnityBridgeResult BuildInvalidResult(string commandId, string message, string stackTrace)
        {
            return new UnityBridgeResult
            {
                commandId = commandId,
                commandType = string.Empty,
                status = UnityBridgeStatuses.Failed,
                success = false,
                message = message,
                error = new UnityBridgeError
                {
                    errorCode = UnityBridgeErrorCodes.InvalidCommand,
                    errorMessage = message,
                    stackTrace = stackTrace,
                    recoverable = true,
                    suggestion = "请检查 JSON 格式和命令字段。"
                }
            };
        }
    }
}
#endif
