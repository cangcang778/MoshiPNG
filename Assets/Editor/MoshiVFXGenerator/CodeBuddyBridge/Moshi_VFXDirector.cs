#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MoshiTools
{
    public class Moshi_VFXDirector : EditorWindow
    {
        private const string TOOL_NAME = "CodeBuddy特效导演";
        private Vector2 scrollPosition;
        private Vector2 logScrollPosition;
        private GUIStyle statusStyle;
        private GUIStyle boxTitleStyle;
        private bool stylesInitialized;

        [MenuItem("工具/Moshi特效生成器/CodeBuddy特效导演")]
        public static void ShowWindow()
        {
            Moshi_VFXDirector window = GetWindow<Moshi_VFXDirector>(TOOL_NAME);
            window.minSize = new Vector2(720, 520);
        }

        private void OnEnable()
        {
            UnityBridgeSafety.EnsureAllFolders();
            if (Moshi_UnityBridge.IsListening)
                Moshi_UnityBridge.StartListening(false);
        }

        private void OnDisable()
        {
            // 监听状态由用户显式控制，不随窗口关闭自动停止。
            // Listening state is controlled explicitly by user, not by window lifetime.
        }

        private void OnGUI()
        {
            InitializeStyles();
            DrawHeader();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawStatusPanel();
            DrawQueuePanel();
            DrawActionsPanel();
            DrawRecentTasksPanel();
            DrawLogPanel();
            EditorGUILayout.EndScrollView();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;
            statusStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            boxTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.85f, 0.95f, 1f) }
            };
            stylesInitialized = true;
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(TOOL_NAME, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("CodeBuddy 自然语言制作 Unity 特效的 Unity 端执行器", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("打开生成器", GUILayout.Width(90), GUILayout.Height(24)))
                MoshiVFXGenerator.Moshi_VFXGen.ShowWindow();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6f);
        }

        private void DrawStatusPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("监听状态", boxTitleStyle);
            EditorGUILayout.BeginHorizontal();
            bool listening = Moshi_UnityBridge.IsListening;
            Color oldColor = GUI.color;
            GUI.color = listening ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.45f, 0.35f);
            EditorGUILayout.LabelField(listening ? "● 已开启" : "● 已停止", statusStyle, GUILayout.Width(100));
            GUI.color = oldColor;

            if (GUILayout.Button("开启监听", EditorStyles.miniButtonLeft, GUILayout.Width(90)))
            {
                Moshi_UnityBridge.StartListening();
                Repaint();
            }
            if (GUILayout.Button("停止监听", EditorStyles.miniButtonRight, GUILayout.Width(90)))
            {
                Moshi_UnityBridge.StopListening();
                Repaint();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Bridge v{UnityBridgeConstants.BridgeVersion}", EditorStyles.miniLabel, GUILayout.Width(110));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawQueuePanel()
        {
            UnityBridgeQueueStats stats = Moshi_UnityBridge.GetQueueStats();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("队列统计", boxTitleStyle);
            EditorGUILayout.BeginHorizontal();
            DrawStat("等待", stats.pending);
            DrawStat("执行", stats.processing);
            DrawStat("完成", stats.done);
            DrawStat("失败", stats.failed);
            DrawStat("取消", stats.cancelled);
            DrawStat("超时", stats.timeout);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawStat(string label, int value)
        {
            EditorGUILayout.LabelField($"{label}: {value}", GUILayout.Width(78));
        }

        private void DrawActionsPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("操作", boxTitleStyle);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("立即扫描", GUILayout.Height(26)))
                Moshi_UnityBridge.ProcessPendingNow();
            if (GUILayout.Button("打开队列", GUILayout.Height(26)))
                Moshi_UnityBridge.OpenCommandFolder();
            if (GUILayout.Button("打开输出", GUILayout.Height(26)))
                Moshi_UnityBridge.OpenOutputFolder();
            if (GUILayout.Button("恢复任务", GUILayout.Height(26)))
                Moshi_UnityBridge.RecoverProcessingTasks();
            if (GUILayout.Button("清理完成", GUILayout.Height(26)))
                Moshi_UnityBridge.ClearCompletedResults();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("写入Ping", GUILayout.Height(24)))
                Moshi_UnityBridge.EnqueuePingCommand();
            if (GUILayout.Button("写入蓝爆", GUILayout.Height(24)))
                Moshi_UnityBridge.EnqueueBlueBurstCommand();
            if (GUILayout.Button("写入修改", GUILayout.Height(24)))
                Moshi_UnityBridge.EnqueueModifySelectedCommand();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("MVP 阶段只处理文件队列命令，不自动播放带 JS/Puerts 回调的业务 UI 动画，也不直接覆盖正式 Prefab。", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void DrawRecentTasksPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("最近任务", boxTitleStyle);
            List<UnityBridgeResult> results = Moshi_UnityBridge.GetRecentResults(8);
            if (results.Count == 0)
            {
                EditorGUILayout.LabelField("暂无任务结果", EditorStyles.miniLabel);
            }
            else
            {
                foreach (UnityBridgeResult result in results)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(result.commandId, GUILayout.Width(170));
                    EditorGUILayout.LabelField(result.commandType, GUILayout.Width(170));
                    Color oldColor = GUI.color;
                    GUI.color = result.success ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.45f, 0.35f);
                    EditorGUILayout.LabelField(result.status, GUILayout.Width(70));
                    GUI.color = oldColor;
                    EditorGUILayout.LabelField(result.message, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawLogPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("日志", boxTitleStyle);
            logScrollPosition = EditorGUILayout.BeginScrollView(logScrollPosition, GUILayout.Height(140));
            IReadOnlyList<string> lines = UnityBridgeLogger.RecentLines;
            if (lines.Count == 0)
            {
                EditorGUILayout.LabelField("暂无日志", EditorStyles.miniLabel);
            }
            else
            {
                foreach (string line in lines)
                    EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }
}
#endif
