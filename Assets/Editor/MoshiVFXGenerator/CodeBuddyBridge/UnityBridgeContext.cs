#if UNITY_EDITOR
using UnityEditor;

namespace MoshiTools
{
    public static class UnityBridgeContext
    {
        private const string LastCommandKey = "Moshi_CodeBuddyBridge_LastCommandId";
        private const string LastPrefabKey = "Moshi_CodeBuddyBridge_LastPrefab";
        private const string LastOutputKey = "Moshi_CodeBuddyBridge_LastOutput";

        public static string LastCommandId
        {
            get => EditorPrefs.GetString(LastCommandKey, string.Empty);
            set => EditorPrefs.SetString(LastCommandKey, value ?? string.Empty);
        }

        public static string LastGeneratedPrefab
        {
            get => EditorPrefs.GetString(LastPrefabKey, string.Empty);
            set => EditorPrefs.SetString(LastPrefabKey, value ?? string.Empty);
        }

        public static string LastOutputFolder
        {
            get => EditorPrefs.GetString(LastOutputKey, UnityBridgeConstants.DefaultOutputFolder);
            set => EditorPrefs.SetString(LastOutputKey, string.IsNullOrEmpty(value) ? UnityBridgeConstants.DefaultOutputFolder : value);
        }
    }
}
#endif
