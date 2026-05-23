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

            duration = EditorGUILayout.FloatField(
                new GUIContent("持续时间(秒)", "烘焙动画的时间长度"),
                duration);
            duration = Mathf.Max(0.1f, duration);

            // 匹配周期按钮
            if (cycler != null && cycler.speed > 0.001f)
            {
                float cyclePeriod = 1f / cycler.speed;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"  完整周期: {cyclePeriod:F3}s（{cycler.speed:F2}圈/秒）",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("匹配周期", EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    duration = cyclePeriod;
                }
                if (GUILayout.Button("×2", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    duration = cyclePeriod * 2f;
                }
                if (GUILayout.Button("×4", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    duration = cyclePeriod * 4f;
                }
                EditorGUILayout.EndHorizontal();
            }

            autoLoopFix = EditorGUILayout.Toggle(
                new GUIContent("自动循环修复", "强制首尾帧一致 + 设置 Clip LoopTime，确保动画无缝循环"),
                autoLoopFix);

            if (autoLoopFix && cycler != null && cycler.speed > 0.001f)
            {
                float rawSpd = cycler.speed;
                float cyclesNeeded = rawSpd * duration;
                int roundedCycles = Mathf.Max(1, Mathf.RoundToInt(cyclesNeeded));
                float adjustedSpeed = roundedCycles / duration;
                if (Mathf.Abs(adjustedSpeed - rawSpd) > 0.001f)
                {
                    EditorGUILayout.LabelField(
                        $"  ⚡ 速度自动适配: {rawSpd:F3} → {adjustedSpeed:F3} 圈/秒（恰好 {roundedCycles} 圈）",
                        EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        $"  ✓ 恰好 {roundedCycles} 圈，无需调整速度",
                        EditorStyles.miniLabel);
                }
            }

            sampleRate = EditorGUILayout.IntSlider(
                new GUIContent("采样率(FPS)", "每秒采样帧数，越高越精确但文件越大"),
                sampleRate, 10, 60);

            speedCurve = EditorGUILayout.CurveField(
                new GUIContent("速度曲线", "横轴=归一化时间(0→1), 纵轴=速度倍率"),
                speedCurve);

            // 预估信息
            int totalFrames = Mathf.RoundToInt(duration * sampleRate);
            int instanceCount = (cycler != null && cycler.instances != null) ? cycler.instances.Count : 0;
            string outputDesc = mergeToSingleClip
                ? $"→ 1 个 Clip，包含 {instanceCount} 个子对象 × {totalFrames} 帧"
                : $"→ {instanceCount} 个 Clip × {totalFrames} 帧 = {instanceCount * totalFrames} 关键帧";
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

            // 自动循环修复：调整速度使持续时间 = 整数圈
            float speed = rawSpeed;
            int actualCycles = 0;
            if (autoLoopFix && duration > 0.001f)
            {
                float cyclesNeeded = rawSpeed * duration;
                actualCycles = Mathf.Max(1, Mathf.RoundToInt(cyclesNeeded));
                speed = actualCycles / duration;
            }

            int totalFrames = Mathf.RoundToInt(duration * sampleRate);

            if (mergeToSingleClip)
                BakeMergeSingleClip(validInstances, path, speed, mode, alignToPath, upDir, fixedRotEuler, rotSpeed, useRotCurve, rxCurve, ryCurve, rzCurve, useScaleCurve, sCurve, count, parent, totalFrames, enableGrowAnimation);
            else
                BakeSeparateClips(validInstances, path, speed, mode, alignToPath, upDir, fixedRotEuler, rotSpeed, useRotCurve, rxCurve, ryCurve, rzCurve, useScaleCurve, sCurve, count, parent, totalFrames, enableGrowAnimation);
        }

        // ---- 合并模式：所有实例写入同一个 Clip ----

        private void BakeMergeSingleClip(
            List<GameObject> instances, PathCreator path, float speed, ArrayCycleMode mode,
            bool alignToPath, Vector3 upDir, Vector3 fixedRotEuler, float rotSpeed,
            bool useRotCurve, AnimationCurve rxCurve, AnimationCurve ryCurve, AnimationCurve rzCurve,
            bool useScaleCurve, AnimationCurve sCurve, int count, Transform parent, int totalFrames,
            bool enableGrow)
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

            // 循环修复：首尾帧匹配 + loopTime
            if (autoLoopFix)
                FixClipLoop(clip, totalFrames);

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
            bool enableGrow)
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
                        FixClipLoop(clip, totalFrames);

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
        /// 修复 Clip 使其可无缝循环：
        /// 1. 将每条曲线的最后一个关键帧的值设为与第一帧相同
        /// 2. 设置 AnimationClipSettings.loopTime = true
        /// </summary>
        private static void FixClipLoop(AnimationClip clip, int totalFrames)
        {
            // 遍历 clip 中所有绑定的曲线
            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.keys.Length < 2) continue;

                Keyframe[] keys = curve.keys;
                float lastTime = keys[keys.Length - 1].time;
                float firstValue = keys[0].value;

                // 将最后一个关键帧的值替换为第一帧的值
                keys[keys.Length - 1] = new Keyframe(lastTime, firstValue);

                // 写回曲线
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
            }

            // 设置 loopTime
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
