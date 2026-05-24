using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Moshi.PathTool;

namespace Moshi.PathTool.Editor
{
    /// <summary>
    /// 路径阵列动画烘焙器
    /// 将 PathArrayCycler 中所有实例的循环运动烘焙为 AnimationClip 文件
    /// 支持合并为单个 Clip 或每个实例独立输出
    /// </summary>
    public class PathArrayBaker : EditorWindow
    {
        private PathArrayCycler cycler;
        private float duration = 5f;
        private int sampleRate = 30;
        private AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);
        private string clipPrefix = "ArrayAnim";
        private string savePath = "Assets/Animations";
        private bool mergeToSingleClip = true;
        private bool autoLoopFix = true;
        private bool createAnimationPlayer = true;
        private bool enableGrowAnimation = false;

        private Vector2 scrollPos;

        private const string TOOL_NAME = "路径阵列动画烘焙";

        private struct LoopFixInfo
        {
            public float adjustedSpeed;
            public int intervalSteps;
            public float intervalTime;
            public bool forceFirstLast;
            public bool usesIntervalFormula;
        }

        [MenuItem("工具/Moshi/路径工具/" + TOOL_NAME, false, 101)]
        public static void ShowWindow()
        {
            var window = GetWindow<PathArrayBaker>(TOOL_NAME);
            window.minSize = new Vector2(380, 480);
            window.Show();
        }

        private void OnEnable()
        {
            AutoDetectCycler();
        }

        private void AutoDetectCycler()
        {
            if (cycler == null)
                cycler = Object.FindObjectOfType<PathArrayCycler>();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("路径阵列动画烘焙", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButtonMini("路径阵列动画烘焙");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                mergeToSingleClip
                    ? "将所有实例的动画合并到一个 AnimationClip 中，直接拖入 Animator 即可播放整个阵列"
                    : "每个实例生成独立 .anim 文件，适合需要单独控制的场景",
                MessageType.Info);

            GUILayout.Space(10);

            // ========== 阵列源 ==========
            EditorGUILayout.LabelField("═══ 阵列源 ═══", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.BeginChangeCheck();
            cycler = (PathArrayCycler)EditorGUILayout.ObjectField(
                new GUIContent("阵列循环器", "场景中的 PathArrayCycler 组件"),
                cycler, typeof(PathArrayCycler), true);
            if (EditorGUI.EndChangeCheck()) { }

            if (cycler != null)
            {
                EditorGUI.indentLevel++;
                int validCount = 0;
                if (cycler.instances != null)
                {
                    foreach (var inst in cycler.instances)
                        if (inst != null) validCount++;
                }
                string pathName = cycler.pathCreator != null ? cycler.pathCreator.pathName : "(无)";
                EditorGUILayout.LabelField($"路径: {pathName}  |  实例: {validCount}/{cycler.count}  |  速度: {cycler.speed}/s  |  模式: {cycler.cycleMode}", EditorStyles.miniLabel);

                // 额外信息
                if (cycler.enableRotationCurve)
                    EditorGUILayout.LabelField("旋转曲线: ✓  |  ", EditorStyles.miniLabel);
                if (cycler.enableAlphaGradient)
                    EditorGUILayout.LabelField("透明渐变: ✓ (仅视觉，不烘焙到Clip)  |  ", EditorStyles.miniLabel);
                if (cycler.enableScaleGradient)
                    EditorGUILayout.LabelField("缩放渐变: ✓", EditorStyles.miniLabel);

                if (validCount == 0)
                    EditorGUILayout.HelpBox("阵列尚未生成实例，请先在「路径阵列循环」工具中生成阵列", MessageType.Warning);
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.HelpBox("请选择场景中的 PathArrayCycler", MessageType.Info);
                if (GUILayout.Button("从场景中选取"))
                {
                    cycler = Object.FindObjectOfType<PathArrayCycler>();
                }
            }

            GUILayout.Space(6);

            // ========== 烘焙参数 ==========
            EditorGUILayout.LabelField("═══ 烘焙参数 ═══", EditorStyles.centeredGreyMiniLabel);

            clipPrefix = EditorGUILayout.TextField(
                new GUIContent("动画名", mergeToSingleClip ? "单个 Clip 的文件名" : "Clip 文件名前缀 (生成: 前缀_序号)"),
                clipPrefix);

            mergeToSingleClip = EditorGUILayout.Toggle(
                new GUIContent("合并到单个 Clip", "开启：所有实例的动画写入同一个 AnimationClip\n关闭：每个实例生成独立的 .anim 文件"),
                mergeToSingleClip);

            sampleRate = EditorGUILayout.IntSlider(
                new GUIContent("采样率(FPS)", "每秒采样帧数。持续时间会自动对齐到整数帧，避免 30FPS 下 1秒/N秒无法落到帧点。"),
                sampleRate, 10, 60);

            duration = EditorGUILayout.FloatField(
                new GUIContent("持续时间(秒)", "烘焙动画的时间长度，会按当前 FPS 对齐到整数帧"),
                duration);
            duration = SnapDurationToFrames(Mathf.Max(0.1f, duration), sampleRate);

            // 匹配速度按钮：持续时间是整个 Clip 时长，不再被按钮改写
            if (cycler != null && cycler.speed > 0.001f)
            {
                int validCount = GetValidInstanceCount();
                int alignedFrames = DurationToFrameCount(duration, sampleRate);
                float alignedDuration = alignedFrames / (float)sampleRate;
                float currentCyclePeriod = 1f / cycler.speed;
                float currentIntervalPeriod = validCount > 0 ? currentCyclePeriod / validCount : currentCyclePeriod;
                float oneStepSpeed = CalculateIntervalSpeed(1, alignedDuration, validCount);
                EditorGUILayout.LabelField(
                    $"  当前Clip: {alignedDuration:F3}s = {alignedFrames}帧 @ {sampleRate}FPS  |  当前单格: {currentIntervalPeriod:F3}s  |  1格匹配速度: {oneStepSpeed:F3}圈/秒",
                    EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("匹配间隔", EditorStyles.miniButton, GUILayout.Width(70)))
                    ApplyIntervalMatchedSpeed(1, alignedDuration, validCount);
                if (GUILayout.Button("匹配周期", EditorStyles.miniButton, GUILayout.Width(70)))
                    ApplyCycleMatchedSpeed(alignedDuration);
                if (GUILayout.Button("×2", EditorStyles.miniButton, GUILayout.Width(30)))
                    ApplyIntervalMatchedSpeed(2, alignedDuration, validCount);
                if (GUILayout.Button("×4", EditorStyles.miniButton, GUILayout.Width(30)))
                    ApplyIntervalMatchedSpeed(4, alignedDuration, validCount);
                EditorGUILayout.EndHorizontal();
            }

            autoLoopFix = EditorGUILayout.Toggle(
                new GUIContent("自动循环修复", "持续时间表示整个 Clip 时长。开启后按当前 FPS 对齐后的 Clip 时长反算间隔步数和速度：速度 = 间隔步数 / (数量 × Clip时长)。"),
                autoLoopFix);

            if (autoLoopFix && cycler != null && cycler.speed > 0.001f)
            {
                int validCount = GetValidInstanceCount();
                int alignedFrames = DurationToFrameCount(duration, sampleRate);
                float alignedDuration = alignedFrames / (float)sampleRate;
                LoopFixInfo loopInfo = CalculateLoopFixInfo(cycler.speed, alignedDuration, validCount, cycler.cycleMode);
                if (loopInfo.usesIntervalFormula)
                {
                    EditorGUILayout.LabelField(
                        $"  ⚡ 间隔循环: {cycler.speed:F3} → {loopInfo.adjustedSpeed:F3} 圈/秒（{loopInfo.intervalSteps}格/{validCount}，每格 {loopInfo.intervalTime:F3}s）",
                        EditorStyles.miniLabel);
                    if (!loopInfo.forceFirstLast)
                    {
                        EditorGUILayout.LabelField(
                            "  ✓ 使用阵列等距替位循环：不强制单个物体首尾相同，避免末帧回拉。",
                            EditorStyles.miniLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(
                        $"  ✓ 完整周期循环: {cycler.speed:F3} → {loopInfo.adjustedSpeed:F3} 圈/秒（恰好 {loopInfo.intervalSteps} 圈）",
                        EditorStyles.miniLabel);
                }
            }

            speedCurve = EditorGUILayout.CurveField(
                new GUIContent("速度曲线", "横轴=归一化时间(0→1), 纵轴=速度倍率"),
                speedCurve);

            // 预估信息
            int totalFrames = DurationToFrameCount(duration, sampleRate);
            float actualDuration = totalFrames / (float)sampleRate;
            int instanceCount = (cycler != null && cycler.instances != null) ? cycler.instances.Count : 0;
            string outputDesc = mergeToSingleClip
                ? $"→ 1 个 Clip，{actualDuration:F3}s = {totalFrames} 帧 @ {sampleRate}FPS，包含 {instanceCount} 个子对象"
                : $"→ {instanceCount} 个 Clip × {totalFrames} 帧 @ {sampleRate}FPS = {instanceCount * totalFrames} 关键帧";
            EditorGUILayout.LabelField(outputDesc, EditorStyles.miniLabel);

            GUILayout.Space(6);

            // ========== Animation 组件 ==========
            createAnimationPlayer = EditorGUILayout.Toggle(
                new GUIContent("创建 Animation 组件", "烘焙后在阵列管理器的 GameObject 上自动添加 Animation 组件并挂载 Clip，\n可在场景中直接播放动画（无需手动配置 Animator）"),
                createAnimationPlayer);

            enableGrowAnimation = EditorGUILayout.Toggle(
                new GUIContent("生长动画", "实例按路径位置顺序逐个出现（scale=0→正常值），模拟生长效果"),
                enableGrowAnimation);
            if (enableGrowAnimation)
                EditorGUILayout.LabelField("  首尾实例逐步激活，末尾实例在动画快结束时才出现", EditorStyles.miniLabel);

            GUILayout.Space(6);

            // ========== 保存路径 ==========
            EditorGUILayout.LabelField("═══ 保存路径 ═══", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.BeginHorizontal();
            savePath = EditorGUILayout.TextField("输出目录", savePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("选择保存目录", "Assets", "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    savePath = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(16);

            // ========== 烘焙按钮 ==========
            bool canBake = cycler != null && cycler.pathCreator != null
                && cycler.pathCreator.pathPoints.Count >= 2
                && cycler.instances != null && cycler.instances.Count > 0;

            GUI.enabled = canBake;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("🏗️ 烘焙动画", GUILayout.Height(44)))
            {
                BakeArrayAnimation();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            if (!canBake && cycler != null)
            {
                if (cycler.pathCreator == null)
                    EditorGUILayout.HelpBox("PathArrayCycler 未设置路径", MessageType.Error);
                else if (cycler.instances == null || cycler.instances.Count == 0)
                    EditorGUILayout.HelpBox("PathArrayCycler 没有实例，请先生成阵列", MessageType.Error);
            }

            GUILayout.Space(6);

            // 快速定位
            if (cycler != null && GUILayout.Button("定位到阵列管理器"))
            {
                Selection.activeGameObject = cycler.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            EditorGUILayout.EndScrollView();
        }

        // =====================================================================
        // 烘焙核心逻辑
        // =====================================================================

        private void BakeArrayAnimation()
        {
            if (cycler == null || cycler.pathCreator == null || cycler.instances == null)
            {
                EditorUtility.DisplayDialog("错误", "阵列数据不完整", "确定");
                return;
            }

            // 筛掉 null 实例
            List<GameObject> validInstances = new List<GameObject>();
            foreach (var inst in cycler.instances)
                if (inst != null) validInstances.Add(inst);

            if (validInstances.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "没有有效实例", "确定");
                return;
            }

            // 确保目录存在
            EnsureFolderExists(savePath);

            // 读取所有参数（避免烘焙过程中被修改）
            PathCreator path = cycler.pathCreator;
            float rawSpeed = cycler.speed;
            ArrayCycleMode mode = cycler.cycleMode;
            bool alignToPath = cycler.alignToPath;
            Vector3 upDir = cycler.upDirection;
            Vector3 fixedRotEuler = cycler.perInstanceRotation;
            float rotSpeed = cycler.rotationSpeed;
            bool useRotCurve = cycler.enableRotationCurve;
            AnimationCurve rxCurve = cycler.rotationXOverPath;
            AnimationCurve ryCurve = cycler.rotationYOverPath;
            AnimationCurve rzCurve = cycler.rotationZOverPath;
            bool useScaleCurve = cycler.enableScaleGradient;
            AnimationCurve sCurve = cycler.scaleOverPath;
            int count = validInstances.Count;
            Transform parent = cycler.transform;

            int totalFrames = DurationToFrameCount(duration, sampleRate);
            float actualDuration = totalFrames / (float)sampleRate;

            // 自动循环修复：按数量与实际帧对齐时长计算“单格间隔”循环
            float speed = rawSpeed;
            bool forceFirstLast = true;
            if (autoLoopFix && actualDuration > 0.001f)
            {
                LoopFixInfo loopInfo = CalculateLoopFixInfo(rawSpeed, actualDuration, count, mode);
                speed = loopInfo.adjustedSpeed;
                forceFirstLast = loopInfo.forceFirstLast;
            }

            if (mergeToSingleClip)
                BakeMergeSingleClip(validInstances, path, speed, mode, alignToPath, upDir, fixedRotEuler, rotSpeed, useRotCurve, rxCurve, ryCurve, rzCurve, useScaleCurve, sCurve, count, parent, totalFrames, enableGrowAnimation, forceFirstLast);
            else
                BakeSeparateClips(validInstances, path, speed, mode, alignToPath, upDir, fixedRotEuler, rotSpeed, useRotCurve, rxCurve, ryCurve, rzCurve, useScaleCurve, sCurve, count, parent, totalFrames, enableGrowAnimation, forceFirstLast);
        }

        // ---- 合并模式：所有实例写入同一个 Clip ----

        private void BakeMergeSingleClip(
            List<GameObject> instances, PathCreator path, float speed, ArrayCycleMode mode,
            bool alignToPath, Vector3 upDir, Vector3 fixedRotEuler, float rotSpeed,
            bool useRotCurve, AnimationCurve rxCurve, AnimationCurve ryCurve, AnimationCurve rzCurve,
            bool useScaleCurve, AnimationCurve sCurve, int count, Transform parent, int totalFrames,
            bool enableGrow, bool forceFirstLast)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = clipPrefix;
            clip.frameRate = sampleRate;

            for (int i = 0; i < count; i++)
            {
                GameObject inst = instances[i];
                if (inst == null) continue;

                // 计算实例在父级下的相对路径
                string relPath = AnimationUtility.CalculateTransformPath(inst.transform, parent);

                Keyframe[] px = new Keyframe[totalFrames + 1];
                Keyframe[] py = new Keyframe[totalFrames + 1];
                Keyframe[] pz = new Keyframe[totalFrames + 1];
                Keyframe[] rx = new Keyframe[totalFrames + 1];
                Keyframe[] ry = new Keyframe[totalFrames + 1];
                Keyframe[] rz = new Keyframe[totalFrames + 1];
                Keyframe[] rw = new Keyframe[totalFrames + 1];
                Keyframe[] sx = null, sy = null, sz = null;
                if (useScaleCurve)
                {
                    sx = new Keyframe[totalFrames + 1];
                    sy = new Keyframe[totalFrames + 1];
                    sz = new Keyframe[totalFrames + 1];
                }

                for (int f = 0; f <= totalFrames; f++)
                {
                    SampleFrame(f, totalFrames, i, count, speed, mode, path, alignToPath, upDir,
                        fixedRotEuler, rotSpeed, useRotCurve, rxCurve, ryCurve, rzCurve,
                        useScaleCurve, sCurve, parent, enableGrow,
                        out float time, out Vector3 localPos, out Quaternion localRot, out Vector3 scale);

                    px[f] = new Keyframe(time, localPos.x);
                    py[f] = new Keyframe(time, localPos.y);
                    pz[f] = new Keyframe(time, localPos.z);
                    rx[f] = new Keyframe(time, localRot.x);
                    ry[f] = new Keyframe(time, localRot.y);
                    rz[f] = new Keyframe(time, localRot.z);
                    rw[f] = new Keyframe(time, localRot.w);

                    if (useScaleCurve)
                    {
                        sx[f] = new Keyframe(time, scale.x);
                        sy[f] = new Keyframe(time, scale.y);
                        sz[f] = new Keyframe(time, scale.z);
                    }
                }

                // 设置该子对象的曲线到同一个 Clip 中
                clip.SetCurve(relPath, typeof(Transform), "localPosition.x", new AnimationCurve(px));
                clip.SetCurve(relPath, typeof(Transform), "localPosition.y", new AnimationCurve(py));
                clip.SetCurve(relPath, typeof(Transform), "localPosition.z", new AnimationCurve(pz));
                clip.SetCurve(relPath, typeof(Transform), "localRotation.x", new AnimationCurve(rx));
                clip.SetCurve(relPath, typeof(Transform), "localRotation.y", new AnimationCurve(ry));
                clip.SetCurve(relPath, typeof(Transform), "localRotation.z", new AnimationCurve(rz));
                clip.SetCurve(relPath, typeof(Transform), "localRotation.w", new AnimationCurve(rw));

                if (useScaleCurve)
                {
                    clip.SetCurve(relPath, typeof(Transform), "localScale.x", new AnimationCurve(sx));
                    clip.SetCurve(relPath, typeof(Transform), "localScale.y", new AnimationCurve(sy));
                    clip.SetCurve(relPath, typeof(Transform), "localScale.z", new AnimationCurve(sz));
                }
            }

            clip.EnsureQuaternionContinuity();

            // 循环修复：完整圈时首尾帧匹配；单格间隔循环只设置 loopTime
            if (autoLoopFix)
                FixClipLoop(clip, forceFirstLast);

            // Legacy 标记：Animation 组件需要
            if (createAnimationPlayer)
                clip.legacy = true;

            string assetPath = $"{savePath}/{clip.name}.anim";
            AssetDatabase.CreateAsset(clip, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 创建 Animation 组件
            string animInfo = "";
            if (createAnimationPlayer)
            {
                SetupAnimationPlayer(parent.gameObject, clip);
                animInfo = " + Animation组件";
            }

            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);

            string loopInfo = autoLoopFix ? "（已启用循环）" : "";
            EditorUtility.DisplayDialog("烘焙完成",
                $"已生成合并 AnimationClip{loopInfo}\n文件: {assetPath}\n包含 {count} 个子对象的 Transform 动画{animInfo}",
                "确定");

            string speedInfo = autoLoopFix ? $", speed={speed:F3}(自动适配)" : $", speed={speed:F3}";
            Debug.Log($"[PathArrayBaker] 已创建合并 Clip: {clip.name} ({clip.length:F2}s, {clip.frameRate} FPS, {count} 个子对象, loop={autoLoopFix}{speedInfo})");
        }

        // ---- 分离模式：每个实例独立 Clip ----

        private void BakeSeparateClips(
            List<GameObject> instances, PathCreator path, float speed, ArrayCycleMode mode,
            bool alignToPath, Vector3 upDir, Vector3 fixedRotEuler, float rotSpeed,
            bool useRotCurve, AnimationCurve rxCurve, AnimationCurve ryCurve, AnimationCurve rzCurve,
            bool useScaleCurve, AnimationCurve sCurve, int count, Transform parent, int totalFrames,
            bool enableGrow, bool forceFirstLast)
        {
            List<AnimationClip> createdClips = new List<AnimationClip>();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    GameObject inst = instances[i];
                    if (inst == null) continue;

                    Keyframe[] px = new Keyframe[totalFrames + 1];
                    Keyframe[] py = new Keyframe[totalFrames + 1];
                    Keyframe[] pz = new Keyframe[totalFrames + 1];
                    Keyframe[] rx = new Keyframe[totalFrames + 1];
                    Keyframe[] ry = new Keyframe[totalFrames + 1];
                    Keyframe[] rz = new Keyframe[totalFrames + 1];
                    Keyframe[] rw = new Keyframe[totalFrames + 1];
                    Keyframe[] sx = null, sy = null, sz = null;
                    if (useScaleCurve)
                    {
                        sx = new Keyframe[totalFrames + 1];
                        sy = new Keyframe[totalFrames + 1];
                        sz = new Keyframe[totalFrames + 1];
                    }

                    for (int f = 0; f <= totalFrames; f++)
                    {
                        SampleFrame(f, totalFrames, i, count, speed, mode, path, alignToPath, upDir,
                            fixedRotEuler, rotSpeed, useRotCurve, rxCurve, ryCurve, rzCurve,
                            useScaleCurve, sCurve, parent, enableGrow,
                            out float time, out Vector3 localPos, out Quaternion localRot, out Vector3 scale);

                        px[f] = new Keyframe(time, localPos.x);
                        py[f] = new Keyframe(time, localPos.y);
                        pz[f] = new Keyframe(time, localPos.z);
                        rx[f] = new Keyframe(time, localRot.x);
                        ry[f] = new Keyframe(time, localRot.y);
                        rz[f] = new Keyframe(time, localRot.z);
                        rw[f] = new Keyframe(time, localRot.w);

                        if (useScaleCurve)
                        {
                            sx[f] = new Keyframe(time, scale.x);
                            sy[f] = new Keyframe(time, scale.y);
                            sz[f] = new Keyframe(time, scale.z);
                        }
                    }

                    AnimationClip clip = new AnimationClip();
                    clip.name = $"{clipPrefix}_{i:D3}";
                    clip.frameRate = sampleRate;

                    string relPath = AnimationUtility.CalculateTransformPath(inst.transform, parent);

                    clip.SetCurve(relPath, typeof(Transform), "localPosition.x", new AnimationCurve(px));
                    clip.SetCurve(relPath, typeof(Transform), "localPosition.y", new AnimationCurve(py));
                    clip.SetCurve(relPath, typeof(Transform), "localPosition.z", new AnimationCurve(pz));
                    clip.SetCurve(relPath, typeof(Transform), "localRotation.x", new AnimationCurve(rx));
                    clip.SetCurve(relPath, typeof(Transform), "localRotation.y", new AnimationCurve(ry));
                    clip.SetCurve(relPath, typeof(Transform), "localRotation.z", new AnimationCurve(rz));
                    clip.SetCurve(relPath, typeof(Transform), "localRotation.w", new AnimationCurve(rw));
                    clip.EnsureQuaternionContinuity();

                    if (autoLoopFix)
                        FixClipLoop(clip, forceFirstLast);

                    if (createAnimationPlayer)
                        clip.legacy = true;

                    if (useScaleCurve)
                    {
                        clip.SetCurve(relPath, typeof(Transform), "localScale.x", new AnimationCurve(sx));
                        clip.SetCurve(relPath, typeof(Transform), "localScale.y", new AnimationCurve(sy));
                        clip.SetCurve(relPath, typeof(Transform), "localScale.z", new AnimationCurve(sz));
                    }

                    string assetPath = $"{savePath}/{clip.name}.anim";
                    AssetDatabase.CreateAsset(clip, assetPath);
                    createdClips.Add(clip);
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (createdClips.Count > 0)
            {
                Selection.objects = createdClips.ToArray();
                EditorGUIUtility.PingObject(createdClips[0]);
            }

            // 创建 Animation 组件
            string animInfo = "";
            if (createAnimationPlayer && createdClips.Count > 0)
            {
                SetupAnimationPlayer(parent.gameObject, createdClips.ToArray());
                animInfo = " + Animation组件";
            }

            string loopInfo = autoLoopFix ? "（已启用循环）" : "";
            EditorUtility.DisplayDialog("烘焙完成",
                $"已生成 {createdClips.Count} 个 AnimationClip{loopInfo}\n保存至: {savePath}{animInfo}",
                "确定");

            foreach (var c in createdClips)
                Debug.Log($"[PathArrayBaker] 已创建: {c.name} ({c.length:F2}s, {c.frameRate} FPS)");
        }

        // ---- 单帧采样（通用，合并/分离共用） ----

        private void SampleFrame(
            int frame, int totalFrames, int instanceIndex, int totalInstances,
            float speed, ArrayCycleMode mode, PathCreator path,
            bool alignToPath, Vector3 upDir,
            Vector3 fixedRotEuler, float rotSpeed,
            bool useRotCurve, AnimationCurve rxCurve, AnimationCurve ryCurve, AnimationCurve rzCurve,
            bool useScaleCurve, AnimationCurve sCurve, Transform parent,
            bool enableGrow,
            out float time, out Vector3 localPos, out Quaternion localRot, out Vector3 scale)
        {
            time = (float)frame / sampleRate;
            float normalizedTime = totalFrames > 0 ? (float)frame / totalFrames : 0f;
            float speedMul = speedCurve.Evaluate(normalizedTime);

            float rawPhase = speed * time * speedMul;
            float phaseOffset = ComputePhase(rawPhase, mode);

            float t = (phaseOffset + (float)instanceIndex / totalInstances) % 1f;
            if (t < 0f) t += 1f;

            // 世界位置
            Vector3 worldPos = path.GetPointAtDistance(t);

            // 世界旋转
            Quaternion worldRot;
            if (alignToPath)
            {
                Vector3 tangent = path.GetTangentAtDistance(t);
                if (tangent != Vector3.zero)
                {
                    Quaternion pathRot = Quaternion.LookRotation(tangent, upDir);
                    Quaternion fixedR = Quaternion.Euler(fixedRotEuler);
                    Quaternion curveR = Quaternion.identity;
                    if (useRotCurve)
                        curveR = Quaternion.Euler(rxCurve.Evaluate(t), ryCurve.Evaluate(t), rzCurve.Evaluate(t));
                    Quaternion spinR = Quaternion.Euler(0f, 0f, rotSpeed * time);
                    worldRot = pathRot * fixedR * curveR * spinR;
                }
                else
                {
                    worldRot = Quaternion.identity;
                }
            }
            else
            {
                Quaternion fixedR = Quaternion.Euler(fixedRotEuler);
                Quaternion curveR = Quaternion.identity;
                if (useRotCurve)
                    curveR = Quaternion.Euler(rxCurve.Evaluate(t), ryCurve.Evaluate(t), rzCurve.Evaluate(t));
                Quaternion spinR = Quaternion.Euler(0f, 0f, rotSpeed * time);
                worldRot = fixedR * curveR * spinR;
            }

            // 缩放
            scale = Vector3.one;
            if (useScaleCurve)
                scale = Vector3.one * sCurve.Evaluate(t);

            // 生长动画：实例按索引顺序逐个出现
            if (enableGrow && totalFrames > 0 && totalInstances > 1)
            {
                float growProgress = (float)frame / totalFrames;
                float activationThreshold = (float)instanceIndex / (totalInstances - 1);
                if (growProgress < activationThreshold)
                    scale = Vector3.zero;
            }

            // 转换为本地坐标（相对于父级 PathArrayCycler）
            localPos = parent.InverseTransformPoint(worldPos);
            localRot = Quaternion.Inverse(parent.rotation) * worldRot;
        }

        private int GetValidInstanceCount()
        {
            if (cycler == null || cycler.instances == null) return 0;
            int validCount = 0;
            foreach (GameObject instance in cycler.instances)
            {
                if (instance != null) validCount++;
            }
            return validCount;
        }

        private static int DurationToFrameCount(float seconds, int fps)
        {
            fps = Mathf.Max(1, fps);
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.001f, seconds) * fps));
        }

        private static float SnapDurationToFrames(float seconds, int fps)
        {
            fps = Mathf.Max(1, fps);
            return DurationToFrameCount(seconds, fps) / (float)fps;
        }

        private static float CalculateIntervalSpeed(int intervalSteps, float clipDuration, int count)
        {
            intervalSteps = Mathf.Max(1, intervalSteps);
            clipDuration = Mathf.Max(0.001f, clipDuration);
            count = Mathf.Max(1, count);
            return intervalSteps / (count * clipDuration);
        }

        private void ApplyIntervalMatchedSpeed(int intervalSteps, float clipDuration, int count)
        {
            if (cycler == null) return;
            Undo.RecordObject(cycler, "匹配路径阵列间隔速度");
            cycler.speed = CalculateIntervalSpeed(intervalSteps, clipDuration, count);
            EditorUtility.SetDirty(cycler);
        }

        private void ApplyCycleMatchedSpeed(float clipDuration)
        {
            if (cycler == null) return;
            Undo.RecordObject(cycler, "匹配路径阵列周期速度");
            cycler.speed = 1f / Mathf.Max(0.001f, clipDuration);
            EditorUtility.SetDirty(cycler);
        }

        /// <summary>
        /// 自动循环修复公式：
        /// Loop 模式下按阵列数量计算单格间隔，让动画结尾刚好移动 N 个间隔。
        /// 公式：间隔相位 = 1 / count，间隔步数 = round(speed × duration × count)，修正速度 = 间隔步数 / (count × duration)。
        /// 当间隔步数是 count 的整数倍时才是完整圈，可强制首尾一致；否则保留“移动到下一格”的末帧状态。
        /// </summary>
        private static LoopFixInfo CalculateLoopFixInfo(float rawSpeed, float bakeDuration, int count, ArrayCycleMode mode)
        {
            rawSpeed = Mathf.Max(0.001f, rawSpeed);
            bakeDuration = Mathf.Max(0.001f, bakeDuration);
            count = Mathf.Max(1, count);

            if (mode == ArrayCycleMode.Loop && count > 1)
            {
                float idealSteps = rawSpeed * bakeDuration * count;
                int intervalSteps = Mathf.Max(1, Mathf.RoundToInt(idealSteps));
                float adjustedSpeed = intervalSteps / (count * bakeDuration);
                return new LoopFixInfo
                {
                    adjustedSpeed = adjustedSpeed,
                    intervalSteps = intervalSteps,
                    intervalTime = bakeDuration / intervalSteps,
                    forceFirstLast = intervalSteps % count == 0,
                    usesIntervalFormula = true
                };
            }

            int cycles = Mathf.Max(1, Mathf.RoundToInt(rawSpeed * bakeDuration));
            return new LoopFixInfo
            {
                adjustedSpeed = cycles / bakeDuration,
                intervalSteps = cycles,
                intervalTime = bakeDuration / cycles,
                forceFirstLast = true,
                usesIntervalFormula = false
            };
        }

        /// <summary>
        /// 根据模式计算当前相位（与 PathArrayCycler 的 AdvancePhase 一致）
        /// </summary>
        private static float ComputePhase(float rawPhase, ArrayCycleMode mode)
        {
            switch (mode)
            {
                case ArrayCycleMode.Loop:
                    return rawPhase % 1f;

                case ArrayCycleMode.PingPong:
                    // 模拟来回
                    float t = rawPhase % 2f;
                    if (t > 1f) t = 2f - t;
                    return Mathf.Clamp01(t);

                case ArrayCycleMode.Manual:
                    return 0f; // 烘焙时 Manual 模式固定为 0

                default:
                    return rawPhase % 1f;
            }
        }

        /// <summary>
        /// 修复 Clip 循环：
        /// 1. 完整圈循环时可强制每条曲线首尾值一致
        /// 2. 单格间隔循环时只设置 LoopTime，保留末帧的“移动到下一格”状态
        /// </summary>
        private static void FixClipLoop(AnimationClip clip, bool forceFirstLast)
        {
            if (forceFirstLast)
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var binding in bindings)
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null || curve.keys.Length < 2) continue;

                    Keyframe[] keys = curve.keys;
                    float lastTime = keys[keys.Length - 1].time;
                    float firstValue = keys[0].value;
                    keys[keys.Length - 1] = new Keyframe(lastTime, firstValue);
                    AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
                }
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        /// <summary>
        /// 在目标 GameObject 上创建 Animation 组件并挂载 Clip(s)
        /// </summary>
        private void SetupAnimationPlayer(GameObject target, params AnimationClip[] clips)
        {
            if (target == null || clips == null || clips.Length == 0) return;

            // 移除旧的 Animation 组件
            var existing = target.GetComponent<Animation>();
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            // 添加新的 Animation 组件
            Animation anim = Undo.AddComponent<Animation>(target);
            anim.playAutomatically = true;
            if (autoLoopFix)
                anim.wrapMode = WrapMode.Loop;

            foreach (var clip in clips)
            {
                if (clip == null) continue;
                anim.AddClip(clip, clip.name);
            }

            // 默认播放第一个 clip（合并模式下唯一一个）
            if (clips.Length > 0 && clips[0] != null)
                anim.clip = clips[0];

            EditorUtility.SetDirty(target);
            Debug.Log($"[PathArrayBaker] 已为 {target.name} 添加 Animation 组件，含 {clips.Length} 个 Clip");
        }

        /// <summary>
        /// 递归创建目录
        /// </summary>
        private static void EnsureFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
