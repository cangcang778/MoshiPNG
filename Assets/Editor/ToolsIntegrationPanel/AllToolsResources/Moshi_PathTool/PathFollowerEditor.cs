using UnityEngine;
using UnityEditor;
using Moshi.PathTool;

namespace Moshi.PathTool.Editor
{
    /// <summary>
    /// PathFollower 自定义编辑器
    /// 提供编辑器中的进度预览功能
    /// </summary>
    [CustomEditor(typeof(PathFollower))]
    public class PathFollowerEditor : UnityEditor.Editor
    {
        private PathFollower follower;
        private float previewProgress = 0f;
        private bool isPreviewMode = false;

        private void OnEnable()
        {
            follower = (PathFollower)target;
            previewProgress = follower.Progress;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("编辑器预览", EditorStyles.boldLabel);

            if (follower.pathCreator == null)
            {
                EditorGUILayout.HelpBox("请先设置要跟随的路径(PathCreator)", MessageType.Warning);
                
                if (GUILayout.Button("从场景中选择路径"))
                {
                    ShowPathSelectionMenu();
                }
                return;
            }

            // 路径信息
            EditorGUILayout.LabelField($"路径: {follower.pathCreator.pathName}");
            EditorGUILayout.LabelField($"路径点数: {follower.pathCreator.pathPoints.Count}");
            EditorGUILayout.LabelField($"路径长度: {follower.pathCreator.GetPathLength():F2} 单位");

            EditorGUILayout.Space(5);

            // 预览模式开关
            EditorGUI.BeginChangeCheck();
            isPreviewMode = EditorGUILayout.Toggle("预览模式", isPreviewMode);
            if (EditorGUI.EndChangeCheck() && !isPreviewMode)
            {
                // 退出预览模式时恢复原始进度
                previewProgress = follower.Progress;
            }

            if (isPreviewMode)
            {
                EditorGUILayout.HelpBox("拖动滑块预览物体在路径上的位置", MessageType.Info);

                EditorGUI.BeginChangeCheck();
                previewProgress = EditorGUILayout.Slider("预览进度", previewProgress, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    // 在编辑器中预览位置
                    PreviewPosition(previewProgress);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("起点"))
                {
                    previewProgress = 0f;
                    PreviewPosition(0f);
                }
                if (GUILayout.Button("25%"))
                {
                    previewProgress = 0.25f;
                    PreviewPosition(0.25f);
                }
                if (GUILayout.Button("50%"))
                {
                    previewProgress = 0.5f;
                    PreviewPosition(0.5f);
                }
                if (GUILayout.Button("75%"))
                {
                    previewProgress = 0.75f;
                    PreviewPosition(0.75f);
                }
                if (GUILayout.Button("终点"))
                {
                    previewProgress = 1f;
                    PreviewPosition(1f);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("应用当前预览位置", GUILayout.Height(25)))
                {
                    Undo.RecordObject(follower, "Apply Preview Position");
                    follower.SetProgress(previewProgress);
                    isPreviewMode = false;
                    EditorUtility.SetDirty(follower);
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(10);

            // 快捷操作
            EditorGUILayout.LabelField("快捷操作", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("定位到起点"))
            {
                Undo.RecordObject(follower.transform, "Move to Start");
                follower.SetProgress(0f);
            }
            if (GUILayout.Button("定位到终点"))
            {
                Undo.RecordObject(follower.transform, "Move to End");
                follower.SetProgress(1f);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("聚焦到路径"))
            {
                Selection.activeGameObject = follower.pathCreator.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        private void PreviewPosition(float progress)
        {
            if (follower.pathCreator == null)
                return;

            Undo.RecordObject(follower.transform, "Preview Position");
            
            Vector3 position = follower.pathCreator.GetPointAtDistance(progress);
            follower.transform.position = position;

            // 如果启用了旋转，也预览旋转
            if (follower.rotationMode != RotationMode.None)
            {
                Vector3 tangent = follower.pathCreator.GetTangentAtDistance(progress);
                if (tangent != Vector3.zero)
                {
                    Quaternion rotation = Quaternion.LookRotation(tangent, follower.upDirection);
                    follower.transform.rotation = rotation;
                }
            }

            SceneView.RepaintAll();
        }

        private void ShowPathSelectionMenu()
        {
            PathCreator[] paths = Object.FindObjectsOfType<PathCreator>();
            if (paths.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "场景中没有找到PathCreator\n请先导入路径", "确定");
                return;
            }

            GenericMenu menu = new GenericMenu();
            foreach (var path in paths)
            {
                PathCreator p = path;
                menu.AddItem(new GUIContent($"{p.pathName} ({p.pathPoints.Count}点)"),
                    false,
                    () =>
                    {
                        Undo.RecordObject(follower, "Set Path");
                        follower.pathCreator = p;
                        EditorUtility.SetDirty(follower);
                    });
            }
            menu.ShowAsContext();
        }

        private void OnSceneGUI()
        {
            if (follower.pathCreator == null || !isPreviewMode)
                return;

            // 在Scene视图中显示预览进度
            Vector3 position = follower.pathCreator.GetPointAtDistance(previewProgress);
            
            Handles.color = Color.green;
            Handles.SphereHandleCap(0, position, Quaternion.identity, 
                HandleUtility.GetHandleSize(position) * 0.2f, EventType.Repaint);

            Handles.BeginGUI();
            Vector2 screenPos = HandleUtility.WorldToGUIPoint(position);
            GUI.Label(new Rect(screenPos.x + 10, screenPos.y - 10, 100, 20), 
                $"进度: {previewProgress:P0}");
            Handles.EndGUI();
        }
    }
}
