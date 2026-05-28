#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace MoshiTools
{
    [Serializable]
    public class UnityBridgeCommand
    {
        public string version = "1.0";
        public string commandId = string.Empty;
        public string commandType = string.Empty;
        public string targetMode = "none";
        public string targetPath = string.Empty;
        public string outputFolder = UnityBridgeConstants.DefaultOutputFolder;
        public string createdAt = string.Empty;
        public UnityBridgePayload payload = new UnityBridgePayload();
        public UnityBridgeCommandOptions options = new UnityBridgeCommandOptions();
    }

    [Serializable]
    public class UnityBridgeCommandOptions
    {
        public bool previewAfterExecute = true;
        public bool savePrefab = true;
        public bool backupBeforeModify = true;
        public int timeoutSeconds = UnityBridgeConstants.DefaultTimeoutSeconds;
    }

    [Serializable]
    public class UnityBridgePayload
    {
        public string message = string.Empty;

        public bool includeComponents = true;
        public bool includeAssetPath = true;
        public bool includeHierarchyPath = true;

        public string vfxName = "FX_CodeBuddyVFX";
        public string displayName = "CodeBuddy特效";
        public string vfxType = "burst";
        public string style = "normal";
        public float duration = 0.6f;
        public bool loop = false;
        public string[] colorPalette = new string[] { "#4AA8FF", "#7B3DFF" };
        public float scale = 1f;
        public int sortingOrder = 0;
        public UnityBridgeVFXLayer[] layers = new UnityBridgeVFXLayer[0];

        public string modifyMode = "relative";
        public string target = "selected";
        public UnityBridgeAdjustments adjustments = new UnityBridgeAdjustments();

        public bool play = true;
        public bool restart = true;
        public bool capturePreview = false;
    }

    [Serializable]
    public class UnityBridgeVFXLayer
    {
        public string name = "Particle";
        public string role = "flash";
        public string rendererType = "billboard";
        public int particleCount = 24;
        public float lifetime = 0.35f;
        public float startSpeed = 1f;
        public float startSize = 0.5f;
        public float emissionRate = 0f;
        public int burstCount = 24;
        public string color = "#4AA8FF";
        public string materialHint = "additive";
    }

    [Serializable]
    public class UnityBridgeAdjustments
    {
        public float brightnessMultiplier = 1f;
        public float particleCountMultiplier = 1f;
        public float durationMultiplier = 1f;
        public float sizeMultiplier = 1f;
        public float speedMultiplier = 1f;
        public UnityBridgeColorShift colorShift = new UnityBridgeColorShift();
    }

    [Serializable]
    public class UnityBridgeColorShift
    {
        public float hueOffset = 0f;
        public float saturationMultiplier = 1f;
        public float valueMultiplier = 1f;
    }

    [Serializable]
    public class UnityBridgeResult
    {
        public string version = "1.0";
        public string commandId = string.Empty;
        public string commandType = string.Empty;
        public string status = "done";
        public bool success = true;
        public string message = string.Empty;
        public UnityBridgeResultData result = new UnityBridgeResultData();
        public UnityBridgeError error = null;
        public string[] suggestions = new string[0];
    }

    [Serializable]
    public class UnityBridgeResultData
    {
        public string unityVersion = string.Empty;
        public string projectPath = string.Empty;
        public string bridgeVersion = UnityBridgeConstants.BridgeVersion;
        public bool listening = false;

        public string prefabPath = string.Empty;
        public string materialPath = string.Empty;
        public string previewImagePath = string.Empty;
        public int layerCount = 0;
        public int materialCount = 0;
        public int maxParticles = 0;
        public string performanceLevel = "Normal";

        public UnityBridgeSelectionInfo selection = null;
        public string[] modifiedObjects = new string[0];
    }

    [Serializable]
    public class UnityBridgeSelectionInfo
    {
        public string name = string.Empty;
        public int instanceId = 0;
        public string hierarchyPath = string.Empty;
        public string assetPath = string.Empty;
        public string[] components = new string[0];
    }

    [Serializable]
    public class UnityBridgeError
    {
        public string errorCode = string.Empty;
        public string errorMessage = string.Empty;
        public string stackTrace = string.Empty;
        public string unityLogPath = string.Empty;
        public bool recoverable = true;
        public string suggestion = string.Empty;
    }

    public class UnityBridgeQueueStats
    {
        public int pending;
        public int processing;
        public int done;
        public int failed;
        public int cancelled;
        public int timeout;
    }

    public static class UnityBridgeConstants
    {
        public const string BridgeVersion = "0.1.0";
        public const int DefaultTimeoutSeconds = 30;
        public const string CommandRoot = "Assets/Editor/ToolsIntegrationPanel/AgentCommands";
        public const string PendingFolder = CommandRoot + "/pending";
        public const string ProcessingFolder = CommandRoot + "/processing";
        public const string DoneFolder = CommandRoot + "/done";
        public const string FailedFolder = CommandRoot + "/failed";
        public const string CancelledFolder = CommandRoot + "/cancelled";
        public const string TimeoutFolder = CommandRoot + "/timeout";
        public const string LogsFolder = CommandRoot + "/logs";
        public const string DefaultOutputFolder = "Assets/MoShi/GeneratedVFX";
        public const string BridgeSourceFolder = "Assets/Editor/MoshiVFXGenerator/CodeBuddyBridge";
    }

    public static class UnityBridgeCommandTypes
    {
        public const string PingUnity = "ping_unity";
        public const string GetSelectionInfo = "get_selection_info";
        public const string CreateParticlePrefab = "create_particle_prefab";
        public const string ModifySelectedParticle = "modify_selected_particle";
        public const string PreviewVFX = "preview_vfx";
    }

    public static class UnityBridgeStatuses
    {
        public const string Done = "done";
        public const string Failed = "failed";
        public const string Timeout = "timeout";
    }

    public static class UnityBridgeErrorCodes
    {
        public const string InvalidCommand = "INVALID_COMMAND";
        public const string TargetNotFound = "TARGET_NOT_FOUND";
        public const string AssetNotFound = "ASSET_NOT_FOUND";
        public const string PermissionDenied = "PERMISSION_DENIED";
        public const string ExecuteTimeout = "EXECUTE_TIMEOUT";
        public const string UnityException = "UNITY_EXCEPTION";
        public const string UnityCrashSuspected = "UNITY_CRASH_SUSPECTED";
    }
}
#endif
