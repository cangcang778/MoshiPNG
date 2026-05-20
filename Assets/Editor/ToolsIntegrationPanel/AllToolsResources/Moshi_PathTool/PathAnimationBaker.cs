using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Moshi.PathTool;

namespace Moshi.PathTool.Editor
{
    /// <summary>
    /// 路径动画烘焙器 - 将路径移动导出为AnimationClip
    /// </summary>
    public class PathAnimationBaker : EditorWindow
    {
        private PathCreator pathCreator;
        private GameObject targetObject;
        private float duration = 5f;
        private int sampleRate = 30;
        private bool bakeRotation = true;
        private bool bakeScale = false;
        private Vector3 scaleStart = Vector3.one;
        private Vector3 scaleEnd = Vector3.one;
        private AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);
        private RotationMode rotationMode = RotationMode.Path;
        private Vector3 upDirection = Vector3.up;
        private string clipName = "PathAnimation";
        private string savePath = "Assets/Animations";

        private Vector2 scrollPosition;
        private AnimationClip previewClip;
        
        private const string TOOL_NAME = "路径动画烘焙";

        [MenuItem("工具/Moshi/路径工具/" + TOOL_NAME, false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<PathAnimationBaker>(TOOL_NAME);
            window.minSize = new Vector2(350, 500);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("路径动画烘焙器", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButtonMini("路径工具");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("将GameObject沿路径移动的轨迹导出为AnimationClip动画", MessageType.Info);

            EditorGUILayout.Space(10);

            // 目标设置
            EditorGUILayout.LabelField("目标设置", EditorStyles.boldLabel);
            pathCreator = (PathCreator)EditorGUILayout.ObjectField("路径", pathCreator, typeof(PathCreator), true);
            targetObject = (GameObject)EditorGUILayout.ObjectField("目标物体", targetObject, typeof(GameObject), true);

            if (pathCreator != null)
            {
                EditorGUILayout.LabelField($"路径点数: {pathCreator.pathPoints.Count}");
                EditorGUILayout.LabelField($"路径长度: {pathCreator.GetPathLength():F2} 单位");
            }

            EditorGUILayout.Space(10);

            // 动画设置
            EditorGUILayout.LabelField("动画设置", EditorStyles.boldLabel);
            clipName = EditorGUILayout.TextField("动画名称", clipName);
            duration = EditorGUILayout.FloatField("持续时间 (秒)", duration);
            duration = Mathf.Max(0.1f, duration);
            sampleRate = EditorGUILayout.IntSlider("采样率 (帧/秒)", sampleRate, 10, 60);
            speedCurve = EditorGUILayout.CurveField("速度曲线", speedCurve);

            EditorGUILayout.Space(5);

            // 旋转设置
            EditorGUILayout.LabelField("旋转设置", EditorStyles.boldLabel);
            bakeRotation = EditorGUILayout.Toggle("烘焙旋转", bakeRotation);
            if (bakeRotation)
            {
                rotationMode = (RotationMode)EditorGUILayout.EnumPopup("旋转模式", rotationMode);
                upDirection = EditorGUILayout.Vector3Field("向上方向", upDirection);
            }

            EditorGUILayout.Space(5);

            // 缩放设置
            EditorGUILayout.LabelField("缩放设置", EditorStyles.boldLabel);
            bakeScale = EditorGUILayout.Toggle("烘焙缩放", bakeScale);
            if (bakeScale)
            {
                scaleStart = EditorGUILayout.Vector3Field("起始缩放", scaleStart);
                scaleEnd = EditorGUILayout.Vector3Field("结束缩放", scaleEnd);
            }

            EditorGUILayout.Space(10);

            // 保存设置
            EditorGUILayout.LabelField("保存设置", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            savePath = EditorGUILayout.TextField("保存路径", savePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("选择保存目录", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        savePath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            // 烘焙按钮
            GUI.enabled = pathCreator != null && pathCreator.pathPoints.Count >= 2;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("烘焙动画", GUILayout.Height(40)))
            {
                BakeAnimation();
            }
            GUI.backgroundColor = Color.white;

            // 预览按钮
            if (GUILayout.Button("预览路径", GUILayout.Height(30)))
            {
                PreviewPath();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(10);

            // 快速操作
            EditorGUILayout.LabelField("快速操作", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("从选中获取路径"))
            {
                GetPathFromSelection();
            }
            if (GUILayout.Button("从选中获取目标"))
            {
                GetTargetFromSelection();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        private void BakeAnimation()
        {
            if (pathCreator == null || pathCreator.pathPoints.Count < 2)
            {
                EditorUtility.DisplayDialog("错误", "请先设置有效的路径！", "确定");
                return;
            }

            // 确保保存目录存在
            if (!AssetDatabase.IsValidFolder(savePath))
            {
                string[] folders = savePath.Split('/');
                string currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    string newPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = newPath;
                }
            }

            // 创建AnimationClip
            AnimationClip clip = new AnimationClip();
            clip.name = clipName;
            clip.frameRate = sampleRate;

            int totalFrames = Mathf.RoundToInt(duration * sampleRate);

            // 创建关键帧数组
            Keyframe[] posXKeys = new Keyframe[totalFrames + 1];
            Keyframe[] posYKeys = new Keyframe[totalFrames + 1];
            Keyframe[] posZKeys = new Keyframe[totalFrames + 1];

            Keyframe[] rotXKeys = null;
            Keyframe[] rotYKeys = null;
            Keyframe[] rotZKeys = null;
            Keyframe[] rotWKeys = null;

            Keyframe[] scaleXKeys = null;
            Keyframe[] scaleYKeys = null;
            Keyframe[] scaleZKeys = null;

            if (bakeRotation)
            {
                rotXKeys = new Keyframe[totalFrames + 1];
                rotYKeys = new Keyframe[totalFrames + 1];
                rotZKeys = new Keyframe[totalFrames + 1];
                rotWKeys = new Keyframe[totalFrames + 1];
            }

            if (bakeScale)
            {
                scaleXKeys = new Keyframe[totalFrames + 1];
                scaleYKeys = new Keyframe[totalFrames + 1];
                scaleZKeys = new Keyframe[totalFrames + 1];
            }

            // 采样路径
            for (int i = 0; i <= totalFrames; i++)
            {
                float time = (float)i / sampleRate;
                float normalizedTime = (float)i / totalFrames;

                // 应用速度曲线
                float curveValue = speedCurve.Evaluate(normalizedTime);
                float adjustedProgress = GetAdjustedProgress(normalizedTime, curveValue);

                // 获取位置
                Vector3 position = pathCreator.GetPointAtDistance(adjustedProgress);

                // 如果有目标物体，转换为本地坐标
                if (targetObject != null && targetObject.transform.parent != null)
                {
                    position = targetObject.transform.parent.InverseTransformPoint(position);
                }

                posXKeys[i] = new Keyframe(time, position.x);
                posYKeys[i] = new Keyframe(time, position.y);
                posZKeys[i] = new Keyframe(time, position.z);

                // 获取旋转
                if (bakeRotation)
                {
                    Quaternion rotation = GetRotationAtProgress(adjustedProgress);

                    if (targetObject != null && targetObject.transform.parent != null)
                    {
                        rotation = Quaternion.Inverse(targetObject.transform.parent.rotation) * rotation;
                    }

                    rotXKeys[i] = new Keyframe(time, rotation.x);
                    rotYKeys[i] = new Keyframe(time, rotation.y);
                    rotZKeys[i] = new Keyframe(time, rotation.z);
                    rotWKeys[i] = new Keyframe(time, rotation.w);
                }

                // 获取缩放
                if (bakeScale)
                {
                    Vector3 scale = Vector3.Lerp(scaleStart, scaleEnd, normalizedTime);
                    scaleXKeys[i] = new Keyframe(time, scale.x);
                    scaleYKeys[i] = new Keyframe(time, scale.y);
                    scaleZKeys[i] = new Keyframe(time, scale.z);
                }
            }

            // 创建曲线并添加到clip
            string relativePath = "";
            if (targetObject != null && targetObject.transform.parent != null)
            {
                relativePath = AnimationUtility.CalculateTransformPath(targetObject.transform, targetObject.transform.root);
            }

            clip.SetCurve(relativePath, typeof(Transform), "localPosition.x", new AnimationCurve(posXKeys));
            clip.SetCurve(relativePath, typeof(Transform), "localPosition.y", new AnimationCurve(posYKeys));
            clip.SetCurve(relativePath, typeof(Transform), "localPosition.z", new AnimationCurve(posZKeys));

            if (bakeRotation)
            {
                clip.SetCurve(relativePath, typeof(Transform), "localRotation.x", new AnimationCurve(rotXKeys));
                clip.SetCurve(relativePath, typeof(Transform), "localRotation.y", new AnimationCurve(rotYKeys));
                clip.SetCurve(relativePath, typeof(Transform), "localRotation.z", new AnimationCurve(rotZKeys));
                clip.SetCurve(relativePath, typeof(Transform), "localRotation.w", new AnimationCurve(rotWKeys));
            }

            if (bakeScale)
            {
                clip.SetCurve(relativePath, typeof(Transform), "localScale.x", new AnimationCurve(scaleXKeys));
                clip.SetCurve(relativePath, typeof(Transform), "localScale.y", new AnimationCurve(scaleYKeys));
                clip.SetCurve(relativePath, typeof(Transform), "localScale.z", new AnimationCurve(scaleZKeys));
            }

            // 确保四元数旋转曲线正确
            clip.EnsureQuaternionContinuity();

            // 保存clip
            string fullPath = $"{savePath}/{clipName}.anim";
            AssetDatabase.CreateAsset(clip, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            previewClip = clip;

            EditorUtility.DisplayDialog("成功", $"动画已保存到:\n{fullPath}", "确定");
            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
        }

        private float GetAdjustedProgress(float normalizedTime, float curveValue)
        {
            // 简单的速度曲线积分近似
            // 更精确的实现需要数值积分
            return Mathf.Clamp01(normalizedTime * curveValue);
        }

        private Quaternion GetRotationAtProgress(float progress)
        {
            Vector3 tangent = pathCreator.GetTangentAtDistance(progress);

            switch (rotationMode)
            {
                case RotationMode.Path:
                    if (tangent != Vector3.zero)
                        return Quaternion.LookRotation(tangent);
                    break;

                case RotationMode.PathWithUp:
                    if (tangent != Vector3.zero)
                        return Quaternion.LookRotation(tangent, upDirection);
                    break;

                default:
                    break;
            }

            return Quaternion.identity;
        }

        private void PreviewPath()
        {
            if (pathCreator == null)
                return;

            Selection.activeGameObject = pathCreator.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void GetPathFromSelection()
        {
            if (Selection.activeGameObject != null)
            {
                PathCreator pc = Selection.activeGameObject.GetComponent<PathCreator>();
                if (pc != null)
                {
                    pathCreator = pc;
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "选中的物体没有PathCreator组件", "确定");
                }
            }
        }

        private void GetTargetFromSelection()
        {
            if (Selection.activeGameObject != null)
            {
                targetObject = Selection.activeGameObject;
            }
        }
    }
}
