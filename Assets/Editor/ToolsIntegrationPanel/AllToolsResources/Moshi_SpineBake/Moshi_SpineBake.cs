// Spine骨骼动画烘焙工具
// Bake Spine bone animations to Unity AnimationClip
//
// 条件编译说明 / Conditional compilation:
//   HAS_SPINE 宏由 SpineDefineManager.cs 自动管理（检测 spine-unity 程序集）
//   HAS_SPINE define is auto-managed by SpineDefineManager.cs (detects spine-unity assembly)
//   无 Spine 插件 → 自动无宏 → 显示提示窗口
//   No Spine plugin → auto no defines → shows placeholder window
//   有 Spine 插件 → 自动添加 HAS_SPINE → 正常功能
//   Has Spine plugin → auto adds HAS_SPINE → full functionality
//
// 版本适配说明 / Version adaptation:
//   Spine 版本（3.8/4.2/4.3+）由 SpineCompat.cs 在运行时通过反射自动检测
//   无需手动添加任何版本宏（SPINE_4_2/SPINE_4_3 等），彻底避免编译期鸡生蛋问题
//   Spine version (3.8/4.2/4.3+) is auto-detected at runtime by SpineCompat.cs via reflection
//   No manual version defines needed, avoids compile-time chicken-and-egg issues
using UnityEngine;
using UnityEditor;
#if HAS_SPINE
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spine;
using Spine.Unity;
#endif

namespace MoshiTools
{
    public class Moshi_SpineBake : EditorWindow
    {
        private const string TOOL_NAME = "骨骼动画烘焙";

        [MenuItem("工具/Moshi/" + TOOL_NAME)]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_SpineBake>(TOOL_NAME);
            window.minSize = new Vector2(420, 600);
        }

#if !HAS_SPINE
        // ══════════════════════════════════════════
        //  无 Spine 插件时的空壳窗口
        //  Placeholder window when Spine plugin is not present
        // ══════════════════════════════════════════
        private void OnGUI()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox(
                "当前项目未检测到 Spine 插件。\n\n" +
                "使用此工具需要导入 Spine-Unity 运行时插件。\n" +
                "导入后 HAS_SPINE 宏定义会自动添加，无需手动配置。\n\n" +
                "如已导入 Spine 但仍显示此提示，请尝试：\n" +
                "1. 点击下方按钮重新检测\n" +
                "2. 确认 Assets/Plugins/Spine/ 目录存在\n" +
                "3. 确认 spine-unity.asmdef 文件存在",
                MessageType.Warning);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("重新检测 Spine 插件", GUILayout.Height(30)))
            {
                // 触发宏定义检测
                // Trigger define symbol check
                SpineDefineManager.ForceCheck();
            }
        }
#else

        // ── 坐标空间枚举 ──
        // Coordinate space enum
        private enum CoordSpace { World, Local }

        // ── 输出模式枚举 ──
        // Output mode enum
        private enum OutputMode { NewClip, MergeClip }

        // ── 初始姿态模式枚举 ──
        // Initial pose mode enum
        private enum InitPoseMode { FirstFrame, SetupPose }

        // ── 录制通道枚举 ──
        // Recording channel flags
        [System.Flags]
        private enum RecordChannel
        {
            Position = 1,
            Rotation = 2,
            Scale = 4
        }

        // ── 字段 ──
        // Fields
        private GameObject spineObject;
        private int selectedAnimIndex = 0;
        private int sampleRate = 30;
        private CoordSpace coordSpace = CoordSpace.World;
        private RecordChannel recordChannel = RecordChannel.Position | RecordChannel.Rotation | RecordChannel.Scale;
        private OutputMode outputMode = OutputMode.NewClip;
        private InitPoseMode initPoseMode = InitPoseMode.FirstFrame;
        private bool autoCreateFollowers = true;
        private string savePath = "Assets/";

        // 合并模式：目标已有clip
        // Merge mode: target existing clip
        private AnimationClip targetClip;

        // 合并模式：曲线基础路径（对应Hierarchy中的父级路径）
        // Merge mode: curve base path (corresponds to parent path in Hierarchy)
        // 例如 "BG/Long/qinglongpoyun_in"，最终曲线路径 = basePath + "/Baked_骨骼名"
        // e.g. "BG/Long/qinglongpoyun_in", final curve path = basePath + "/Baked_BoneName"
        private string mergeBasePath = "";
        private bool autoDetectBasePath = true;

        // 手动输入的路径（独立于自动检测，避免被覆盖）
        // Manual input path (independent from auto-detect to avoid overwrite)
        private string manualBasePath = "";

        // 自动检测路径缓存（避免每帧扫描）
        // Auto-detect path cache (avoid scanning every frame)
        private GameObject cachedDetectSpine;
        private string cachedDetectPath = "";

        // Hierarchy推算起点（手动指定，留空则默认去掉第一层根节点）
        // Hierarchy inference root (manual override, null = default remove first root)
        private Transform hierarchyRoot;
        private Transform cachedHierarchyRoot; // 缓存，起点变化时触发重新检测

        // 骨骼列表相关
        // Bone list related
        private List<string> allBoneNames = new List<string>();
        private List<bool> boneSelected = new List<bool>();
        private string boneSearchFilter = "";
        private Vector2 boneListScroll;

        // 动画列表
        // Animation list
        private string[] animationNames = new string[0];

        // 进度
        // Progress
        private bool isBaking = false;
        private float bakeProgress = 0f;
        private string bakeStatus = "";

        // 缓存
        // Cache
        private GameObject lastSpineObject;

        // ── GUI样式 ──
        // GUI styles
        private GUIStyle headerStyle;
        private bool stylesInitialized = false;

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.Space(4);

            // 标题 + 帮助按钮
            // Title + Help button
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(TOOL_NAME, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Spine对象选择 ──
            // Spine object selection
            EditorGUILayout.LabelField("Spine对象", headerStyle);
            EditorGUI.BeginChangeCheck();
            spineObject = (GameObject)EditorGUILayout.ObjectField("目标对象", spineObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() || (spineObject != lastSpineObject))
            {
                lastSpineObject = spineObject;
                RefreshSpineData();
            }

            // 自动获取选中对象
            // Auto-detect selected object
            if (spineObject == null && Selection.activeGameObject != null)
            {
                var go = Selection.activeGameObject;
                if (go.GetComponent<SkeletonAnimation>() != null || go.GetComponent<SkeletonGraphic>() != null)
                {
                    spineObject = go;
                    lastSpineObject = go;
                    RefreshSpineData();
                }
            }

            if (spineObject != null)
            {
                var sa = spineObject.GetComponent<SkeletonAnimation>();
                var sg = spineObject.GetComponent<SkeletonGraphic>();
                string compType = sa != null ? "SkeletonAnimation" : (sg != null ? "SkeletonGraphic" : "未检测到Spine组件");
                EditorGUILayout.LabelField("组件类型", compType);
            }

            if (allBoneNames.Count == 0 && spineObject != null)
            {
                EditorGUILayout.HelpBox("未检测到有效的Spine组件或骨骼数据。请确认对象上挂载了SkeletonAnimation或SkeletonGraphic组件。", MessageType.Warning);
                return;
            }

            if (allBoneNames.Count == 0)
            {
                EditorGUILayout.HelpBox("请选择一个包含SkeletonAnimation或SkeletonGraphic组件的游戏对象。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);

            // ── 动画设置 ──
            // Animation settings
            EditorGUILayout.LabelField("动画设置", headerStyle);

            if (animationNames.Length > 0)
            {
                selectedAnimIndex = EditorGUILayout.Popup("动画名称", selectedAnimIndex, animationNames);
            }
            else
            {
                EditorGUILayout.LabelField("动画名称", "无可用动画");
            }

            sampleRate = EditorGUILayout.IntField("采样帧率", sampleRate);
            sampleRate = Mathf.Clamp(sampleRate, 1, 120);

            // 坐标空间 - 按钮组
            // Coordinate space - button group
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("坐标空间", GUILayout.Width(60));
            if (GUILayout.Toggle(coordSpace == CoordSpace.World, "世界", EditorStyles.miniButtonLeft))
                coordSpace = CoordSpace.World;
            if (GUILayout.Toggle(coordSpace == CoordSpace.Local, "本地", EditorStyles.miniButtonRight))
                coordSpace = CoordSpace.Local;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 录制通道 - 复选框
            // Recording channels - checkboxes
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("录制通道", GUILayout.Width(60));
            bool recPos = (recordChannel & RecordChannel.Position) != 0;
            bool recRot = (recordChannel & RecordChannel.Rotation) != 0;
            bool recScl = (recordChannel & RecordChannel.Scale) != 0;

            recPos = GUILayout.Toggle(recPos, "位置", GUILayout.Width(50));
            recRot = GUILayout.Toggle(recRot, "旋转", GUILayout.Width(50));
            recScl = GUILayout.Toggle(recScl, "缩放", GUILayout.Width(50));

            recordChannel = 0;
            if (recPos) recordChannel |= RecordChannel.Position;
            if (recRot) recordChannel |= RecordChannel.Rotation;
            if (recScl) recordChannel |= RecordChannel.Scale;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // ── 骨骼选择 ──
            // Bone selection
            EditorGUILayout.LabelField("骨骼选择", headerStyle);

            // 搜索框
            // Search box
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🔍", GUILayout.Width(20));
            boneSearchFilter = EditorGUILayout.TextField(boneSearchFilter);
            EditorGUILayout.EndHorizontal();

            // 全选/反选/清空 按钮
            // Select all/invert/clear buttons
            EditorGUILayout.BeginHorizontal();
            int selectedCount = boneSelected.Count(b => b);
            EditorGUILayout.LabelField($"已选: {selectedCount}/{allBoneNames.Count}", GUILayout.Width(100));

            if (GUILayout.Button("全选", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
            {
                for (int i = 0; i < boneSelected.Count; i++) boneSelected[i] = true;
            }
            if (GUILayout.Button("反选", EditorStyles.miniButtonMid, GUILayout.Width(50)))
            {
                for (int i = 0; i < boneSelected.Count; i++) boneSelected[i] = !boneSelected[i];
            }
            if (GUILayout.Button("清空", EditorStyles.miniButtonRight, GUILayout.Width(50)))
            {
                for (int i = 0; i < boneSelected.Count; i++) boneSelected[i] = false;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 骨骼列表（滚动区域）
            // Bone list (scrollable)
            boneListScroll = EditorGUILayout.BeginScrollView(boneListScroll, GUILayout.MaxHeight(250));
            string filter = boneSearchFilter.ToLower();
            for (int i = 0; i < allBoneNames.Count; i++)
            {
                if (!string.IsNullOrEmpty(filter) && !allBoneNames[i].ToLower().Contains(filter))
                    continue;
                boneSelected[i] = EditorGUILayout.ToggleLeft(allBoneNames[i], boneSelected[i]);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);

            // ── 输出设置 ──
            // Output settings
            EditorGUILayout.LabelField("输出设置", headerStyle);

            // 输出模式 - 按钮组
            // Output mode - button group
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("输出模式", GUILayout.Width(60));
            if (GUILayout.Toggle(outputMode == OutputMode.NewClip, "新建Clip", EditorStyles.miniButtonLeft))
                outputMode = OutputMode.NewClip;
            if (GUILayout.Toggle(outputMode == OutputMode.MergeClip, "合并Clip", EditorStyles.miniButtonRight))
                outputMode = OutputMode.MergeClip;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (outputMode == OutputMode.NewClip)
            {
                // ── 新建Clip模式 ──
                // New clip mode
                EditorGUILayout.BeginHorizontal();
                savePath = EditorGUILayout.TextField("保存路径", savePath);
                if (GUILayout.Button("选择", GUILayout.Width(50)))
                {
                    string path = EditorUtility.OpenFolderPanel("选择保存路径", savePath, "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        // 转换为相对路径
                        // Convert to relative path
                        if (path.StartsWith(UnityEngine.Application.dataPath))
                            savePath = "Assets" + path.Substring(UnityEngine.Application.dataPath.Length);
                        else
                            savePath = path;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // ── 合并Clip模式 ──
                // Merge clip mode
                targetClip = (AnimationClip)EditorGUILayout.ObjectField("目标Clip", targetClip, typeof(AnimationClip), false);

                if (targetClip == null)
                {
                    EditorGUILayout.HelpBox("请拖入一个已有的AnimationClip文件，烘焙的骨骼曲线将合并写入该Clip中。", MessageType.Info);
                }
                else
                {
                    string clipPath = AssetDatabase.GetAssetPath(targetClip);
                    EditorGUILayout.LabelField("Clip路径", clipPath);
                    EditorGUILayout.LabelField("帧率", targetClip.frameRate.ToString());
                    EditorGUILayout.LabelField("时长", $"{targetClip.length:F3}s");
                    EditorGUILayout.LabelField("Legacy", targetClip.legacy ? "是" : "否");

                    // 获取已有曲线数量
                    // Get existing curve count
                    int existingCurves = AnimationUtility.GetCurveBindings(targetClip).Length;
                    EditorGUILayout.LabelField("已有曲线", existingCurves.ToString());

                    EditorGUILayout.HelpBox("烘焙的骨骼曲线将以子路径形式合并到此Clip中。同名曲线将被覆盖。", MessageType.Warning);

                    // 检查是否为独立.anim文件
                    // Check if it's a standalone .anim file
                    if (!clipPath.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
                    {
                        EditorGUILayout.HelpBox("⚠ 该Clip不是独立的.anim文件（可能是FBX/模型内嵌动画），无法写入！请使用独立的.anim文件。", MessageType.Error);
                    }

                    EditorGUILayout.Space(4);

                    // ── 曲线基础路径设置 ──
                    // Curve base path settings
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("路径模式", GUILayout.Width(60));
                    if (GUILayout.Toggle(autoDetectBasePath, "自动检测", EditorStyles.miniButtonLeft))
                        autoDetectBasePath = true;
                    if (GUILayout.Toggle(!autoDetectBasePath, "手动输入", EditorStyles.miniButtonRight))
                        autoDetectBasePath = false;
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    if (autoDetectBasePath)
                    {
                        // Hierarchy推算起点设置（可选）
                        // Hierarchy inference root setting (optional)
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("推算起点", GUILayout.Width(60));
                        Transform newHierarchyRoot = (Transform)EditorGUILayout.ObjectField(
                            hierarchyRoot, typeof(Transform), true);
                        if (newHierarchyRoot != hierarchyRoot)
                        {
                            hierarchyRoot = newHierarchyRoot;
                            // 起点变化时清除缓存，触发重新检测
                            // Clear cache on root change to trigger re-detect
                            cachedDetectSpine = null;
                        }
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.HelpBox(
                            "推算起点：Hierarchy推算路径时的截止节点（不包含该节点本身）。\n" +
                            "留空则默认去掉最顶层根节点。\n" +
                            "例如：拖入动画组件所在的物体，路径将从该物体的下一级子物体开始。",
                            MessageType.None);

                        // Hierarchy路径推算（带缓存）
                        // Hierarchy path inference (with cache)
                        // spine对象或推算起点变化时重新推算
                        // Re-infer when spine object or hierarchy root changes
                        if (cachedDetectSpine != spineObject || cachedHierarchyRoot != hierarchyRoot)
                        {
                            cachedDetectSpine = spineObject;
                            cachedHierarchyRoot = hierarchyRoot;

                            if (spineObject != null)
                            {
                                cachedDetectPath = InferBasePathFromHierarchy(spineObject, hierarchyRoot);
                            }
                            else
                            {
                                cachedDetectPath = "";
                            }
                        }

                        // 推算结果直接写入 mergeBasePath
                        // Inference result writes directly to mergeBasePath
                        mergeBasePath = cachedDetectPath ?? "";

                        EditorGUILayout.BeginHorizontal();
                        if (!string.IsNullOrEmpty(mergeBasePath))
                        {
                            EditorGUILayout.LabelField("推算路径", mergeBasePath);
                        }
                        else
                        {
                            EditorGUILayout.LabelField("推算路径", "(未检测到)");
                        }
                        // 手动刷新按钮
                        // Manual refresh button
                        if (GUILayout.Button("刷新", GUILayout.Width(50)))
                        {
                            cachedDetectSpine = null; // 清除缓存，下帧重新推算
                            cachedHierarchyRoot = null;
                        }
                        EditorGUILayout.EndHorizontal();

                        if (!string.IsNullOrEmpty(mergeBasePath))
                        {
                            EditorGUILayout.HelpBox(
                                $"已从Spine对象Hierarchy推算路径: \"{mergeBasePath}\"" +
                                (hierarchyRoot != null ? $"\n推算起点: \"{hierarchyRoot.name}\"" : "\n起点: 默认（去掉最顶层根节点）"),
                                MessageType.Info);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("未能推算路径（请确保已指定Spine对象），将使用根路径 \"Baked_骨骼名\"。\n如需指定前缀路径请切换为「手动输入」。", MessageType.Info);
                        }
                    }
                    else
                    {
                        // 手动输入基础路径（使用独立变量，不会被自动检测覆盖）
                        // Manual base path input (uses independent variable, won't be overwritten by auto-detect)
                        manualBasePath = EditorGUILayout.TextField("基础路径", manualBasePath);

                        // 手动输入结果写入 mergeBasePath
                        // Manual input result writes to mergeBasePath
                        mergeBasePath = manualBasePath;

                        EditorGUILayout.HelpBox(
                            $"最终曲线路径 = \"{(string.IsNullOrEmpty(mergeBasePath) ? "" : mergeBasePath + "/")}" +
                            "Baked_骨骼名\"\n" +
                            "请填写动画组件所在物体到Baked物体父级的Hierarchy相对路径。\n" +
                            "例如: BG/Long/qinglongpoyun_in",
                            MessageType.Info);
                    }
                }
            }

            EditorGUILayout.Space(4);

            // ── 通用设置：自动创建跟随空物体 + 初始姿态（两种输出模式通用） ──
            // Common settings: auto-create followers + initial pose (shared by both output modes)
            autoCreateFollowers = EditorGUILayout.Toggle("自动创建跟随空物体", autoCreateFollowers);

            // 初始姿态模式 - 仅在自动创建开启时显示
            // Initial pose mode - only shown when auto-create is enabled
            if (autoCreateFollowers)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("初始姿态", GUILayout.Width(60));
                if (GUILayout.Toggle(initPoseMode == InitPoseMode.FirstFrame, "动画首帧", EditorStyles.miniButtonLeft))
                    initPoseMode = InitPoseMode.FirstFrame;
                if (GUILayout.Toggle(initPoseMode == InitPoseMode.SetupPose, "骨骼绑定", EditorStyles.miniButtonRight))
                    initPoseMode = InitPoseMode.SetupPose;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (initPoseMode == InitPoseMode.FirstFrame)
                {
                    EditorGUILayout.HelpBox("生成物体的初始Transform = 动画第1帧的位置/旋转/缩放", MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox("生成物体的初始Transform = Spine骨骼的原始绑定姿态（Setup Pose）", MessageType.None);
                }
            }

            EditorGUILayout.Space(12);

            // ── 烘焙按钮 ──
            // Bake button
            bool canBake = !isBaking && selectedCount > 0 && animationNames.Length > 0;
            if (outputMode == OutputMode.MergeClip && targetClip == null) canBake = false;
            EditorGUI.BeginDisabledGroup(!canBake);
            string bakeLabel = outputMode == OutputMode.NewClip ? "开始烘焙" : "开始合并烘焙";
            if (GUILayout.Button(bakeLabel, GUILayout.Height(32)))
            {
                DoBake();
            }
            EditorGUI.EndDisabledGroup();

            // 进度显示
            // Progress display
            if (isBaking)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(20)), bakeProgress, bakeStatus);
            }
        }

        /// <summary>
        /// 刷新Spine数据：骨骼列表和动画列表
        /// Refresh Spine data: bone list and animation list
        /// </summary>
        private void RefreshSpineData()
        {
            allBoneNames.Clear();
            boneSelected.Clear();
            animationNames = new string[0];

            if (spineObject == null) return;

            Skeleton skeleton = GetSkeleton();
            if (skeleton == null) return;

            SkeletonData skeletonData = skeleton.Data;

            // 获取骨骼名称列表
            // Get bone name list
            var bones = skeleton.Bones;
            for (int i = 0; i < bones.Count; i++)
            {
                allBoneNames.Add(bones.Items[i].Data.Name);
                boneSelected.Add(false);
            }

            // 获取动画名称列表
            // Get animation name list
            var animations = skeletonData.Animations;
            animationNames = new string[animations.Count];
            for (int i = 0; i < animations.Count; i++)
            {
                animationNames[i] = animations.Items[i].Name;
            }

            selectedAnimIndex = 0;
        }

        /// <summary>
        /// 获取Skeleton对象
        /// Get Skeleton object
        /// </summary>
        private Skeleton GetSkeleton()
        {
            if (spineObject == null) return null;

            var sa = spineObject.GetComponent<SkeletonAnimation>();
            if (sa != null) return sa.Skeleton;

            var sg = spineObject.GetComponent<SkeletonGraphic>();
            if (sg != null) return sg.Skeleton;

            return null;
        }

        /// <summary>
        /// 获取AnimationState对象
        /// Get AnimationState object
        /// </summary>
        private Spine.AnimationState GetAnimationState()
        {
            if (spineObject == null) return null;

            var sa = spineObject.GetComponent<SkeletonAnimation>();
            if (sa != null) return sa.state;

            var sg = spineObject.GetComponent<SkeletonGraphic>();
            if (sg != null) return sg.AnimationState;

            return null;
        }

        /// <summary>
        /// 获取positionScale（SkeletonGraphic使用canvas像素比例，SkeletonAnimation使用1.0）
        /// Get positionScale (SkeletonGraphic uses canvas pixel scale, SkeletonAnimation uses 1.0)
        /// Spine 3.8: 通过 Canvas.referencePixelsPerUnit 获取缩放
        /// </summary>
        private float GetPositionScale()
        {
            var sg = spineObject.GetComponent<SkeletonGraphic>();
            if (sg != null)
            {
                var canvas = sg.GetComponentInParent<Canvas>();
                if (canvas != null) return canvas.referencePixelsPerUnit;
            }
            return 1f;
        }

        /// <summary>
        /// 执行烘焙主流程
        /// Execute main baking process
        /// </summary>
        private void DoBake()
        {
            if (animationNames.Length == 0 || selectedAnimIndex >= animationNames.Length) return;

            Skeleton skeleton = GetSkeleton();
            Spine.AnimationState animState = GetAnimationState();
            if (skeleton == null || animState == null)
            {
                EditorUtility.DisplayDialog("错误", "无法获取Spine组件数据。请确认对象上的Spine组件已正确初始化。", "确定");
                return;
            }

            string animName = animationNames[selectedAnimIndex];
            SkeletonData skeletonData = skeleton.Data;
            Spine.Animation animation = skeletonData.FindAnimation(animName);
            if (animation == null)
            {
                EditorUtility.DisplayDialog("错误", $"找不到动画: {animName}", "确定");
                return;
            }

            // 收集选中的骨骼名称
            // Collect selected bone names
            List<string> selectedBoneNames = new List<string>();
            for (int i = 0; i < allBoneNames.Count; i++)
            {
                if (boneSelected[i]) selectedBoneNames.Add(allBoneNames[i]);
            }

            if (selectedBoneNames.Count == 0) return;

            // 确保保存路径存在
            // Ensure save path exists
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            isBaking = true;
            bakeProgress = 0f;

            try
            {
                float positionScale = GetPositionScale();
                float duration = animation.Duration;
                float dt = 1f / sampleRate;
                int totalFrames = Mathf.CeilToInt(duration * sampleRate) + 1;
                float skeletonFlipRotation = Mathf.Sign(skeleton.ScaleX * skeleton.ScaleY);

                // 查找所有目标骨骼
                // Find all target bones
                List<Bone> targetBones = new List<Bone>();
                foreach (string boneName in selectedBoneNames)
                {
                    Bone bone = skeleton.FindBone(boneName);
                    if (bone != null)
                        targetBones.Add(bone);
                    else
                        Debug.LogWarning($"[Moshi_SpineBake] 未找到骨骼: {boneName}");
                }

                if (targetBones.Count == 0)
                {
                    EditorUtility.DisplayDialog("错误", "没有找到有效的骨骼。", "确定");
                    isBaking = false;
                    return;
                }

                // 准备曲线容器：每个骨骼一组曲线
                // Prepare curve containers: one set of curves per bone
                var curveSets = new Dictionary<Bone, BoneCurveSet>();
                foreach (var bone in targetBones)
                {
                    curveSets[bone] = new BoneCurveSet();
                }

                // ── 保存当前状态 ──
                // Save current state (记录当前track信息以便恢复)
                // We'll restore by calling SetupPose after baking

                // ── 设置动画并逐帧采样 ──
                // Set animation and sample frame by frame
                SpineCompat.SetToSetupPose(skeleton);
                animState.ClearTracks();
                var track = animState.SetAnimation(0, animation, false);

                // 首帧采样（trackTime = 0）
                // First frame sampling (trackTime = 0)
                animState.Update(0);
                animState.Apply(skeleton);
                SpineCompat.UpdateWorldTransform(skeleton);

                RecordFrame(skeleton, targetBones, curveSets, 0, 0f, positionScale, skeletonFlipRotation);

                // 逐帧推进
                // Advance frame by frame
                for (int frame = 1; frame < totalFrames; frame++)
                {
                    float time = frame * dt;

                    // 确保不超过动画时长
                    // Ensure we don't exceed animation duration
                    float actualDt = dt;
                    if (time > duration)
                    {
                        actualDt = duration - (frame - 1) * dt;
                        if (actualDt <= 0) break;
                    }

                    animState.Update(actualDt);
                    animState.Apply(skeleton);
                    SpineCompat.UpdateWorldTransform(skeleton);

                    float keyTime = Mathf.Min(time, duration);
                    RecordFrame(skeleton, targetBones, curveSets, frame, keyTime, positionScale, skeletonFlipRotation);

                    bakeProgress = (float)frame / totalFrames;
                    bakeStatus = $"采样中... {frame}/{totalFrames}帧";
                }

                // ── 生成AnimationClip并保存 ──
                // Generate AnimationClip and save
                int clipIndex = 0;
                List<string> createdClipPaths = new List<string>();

                if (outputMode == OutputMode.NewClip)
                {
                    // ── 新建Clip模式 ──
                    // New clip mode: each bone gets its own clip file
                    foreach (var bone in targetBones)
                    {
                        var curves = curveSets[bone];
                        string boneName = bone.Data.Name;

                        AnimationClip clip = new AnimationClip();
                        clip.frameRate = sampleRate;
                        clip.legacy = true; // 使用Legacy模式方便Animation组件播放

                        // 根据坐标空间确定路径
                        // Determine path based on coordinate space
                        string relativePath = "";

                        bool recPos = (recordChannel & RecordChannel.Position) != 0;
                        bool recRot = (recordChannel & RecordChannel.Rotation) != 0;
                        bool recScl = (recordChannel & RecordChannel.Scale) != 0;

                        if (recPos)
                        {
                            clip.SetCurve(relativePath, typeof(Transform), "localPosition.x", curves.posX);
                            clip.SetCurve(relativePath, typeof(Transform), "localPosition.y", curves.posY);
                            clip.SetCurve(relativePath, typeof(Transform), "localPosition.z", curves.posZ);
                        }

                        if (recRot)
                        {
                            clip.SetCurve(relativePath, typeof(Transform), "localEulerAnglesRaw.x", curves.rotX);
                            clip.SetCurve(relativePath, typeof(Transform), "localEulerAnglesRaw.y", curves.rotY);
                            clip.SetCurve(relativePath, typeof(Transform), "localEulerAnglesRaw.z", curves.rotZ);
                        }

                        if (recScl)
                        {
                            clip.SetCurve(relativePath, typeof(Transform), "localScale.x", curves.sclX);
                            clip.SetCurve(relativePath, typeof(Transform), "localScale.y", curves.sclY);
                            clip.SetCurve(relativePath, typeof(Transform), "localScale.z", curves.sclZ);
                        }

                        // 设置WrapMode
                        // Set WrapMode
                        clip.wrapMode = WrapMode.Once;

                        // 确保曲线切线正确
                        // Ensure curve tangents are correct
                        clip.EnsureQuaternionContinuity();

                        // 保存.anim文件
                        // Save .anim file
                        string safeAnimName = animName.Replace("/", "_").Replace("\\", "_");
                        string safeBoneName = boneName.Replace("/", "_").Replace("\\", "_");
                        string clipFileName = $"Baked_{safeAnimName}_{safeBoneName}.anim";
                        string clipPath = Path.Combine(savePath, clipFileName).Replace("\\", "/");

                        AssetDatabase.CreateAsset(clip, clipPath);
                        createdClipPaths.Add(clipPath);

                        clipIndex++;
                        bakeProgress = 0.8f + 0.2f * clipIndex / targetBones.Count;
                        bakeStatus = $"保存动画... {clipIndex}/{targetBones.Count}";
                    }
                }
                else
                {
                    // ── 合并Clip模式 ──
                    // Merge clip mode: write all bone curves into existing targetClip
                    if (targetClip == null)
                    {
                        EditorUtility.DisplayDialog("错误", "请先指定要合并的目标AnimationClip文件。", "确定");
                        isBaking = false;
                        return;
                    }

                    // 检查clip是否可写（排除FBX内嵌动画等只读clip）
                    // Check if clip is writable (exclude read-only clips like FBX embedded animations)
                    string targetClipPath = AssetDatabase.GetAssetPath(targetClip);
                    if (!targetClipPath.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
                    {
                        EditorUtility.DisplayDialog("错误",
                            $"目标Clip不是独立的.anim文件，无法写入。\n路径: {targetClipPath}\n\n请使用独立的.anim文件（非FBX/模型内嵌动画）。",
                            "确定");
                        isBaking = false;
                        return;
                    }

                    MergeCurvesToExistingClip(targetBones, curveSets);
                    string mergedPath = AssetDatabase.GetAssetPath(targetClip);
                    createdClipPaths.Add(mergedPath);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // ── 自动创建跟随空物体（两种模式通用） ──
                // Auto-create follower empty objects (shared by both output modes)
                if (autoCreateFollowers)
                {
                    CreateFollowerObjects(skeleton, targetBones, createdClipPaths, animName);
                }

                // ── 恢复Skeleton状态 ──
                // Restore Skeleton state
                SpineCompat.SetToSetupPose(skeleton);
                animState.ClearTracks();

                bakeProgress = 1f;
                bakeStatus = "烘焙完成!";

                if (outputMode == OutputMode.NewClip)
                {
                    int totalClips = createdClipPaths.Count;
                    EditorUtility.DisplayDialog("烘焙完成",
                        $"成功烘焙 {totalClips} 个骨骼动画。\n保存路径: {savePath}",
                        "确定");
                }
                else
                {
                    string mergedPath = AssetDatabase.GetAssetPath(targetClip);
                    int curveCount = AnimationUtility.GetCurveBindings(targetClip).Length;
                    EditorUtility.DisplayDialog("合并完成",
                        $"已将 {targetBones.Count} 个骨骼的动画曲线合并到:\n{mergedPath}\n当前曲线总数: {curveCount}",
                        "确定");
                }

                // 选中保存的clip
                // Select saved clip
                if (outputMode == OutputMode.MergeClip && targetClip != null)
                {
                    EditorGUIUtility.PingObject(targetClip);
                }
                else if (createdClipPaths.Count > 0)
                {
                    var lastClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(createdClipPaths[0]);
                    if (lastClip != null) EditorGUIUtility.PingObject(lastClip);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Moshi_SpineBake] 烘焙失败: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("烘焙失败", ex.Message, "确定");
            }
            finally
            {
                isBaking = false;
                Repaint();
            }
        }

        /// <summary>
        /// 记录一帧的骨骼数据
        /// Record one frame of bone data
        /// 通过 SpineCompat 适配层兼容 Spine 3.8 / 4.2
        /// Uses SpineCompat adapter for Spine 3.8 / 4.2 compatibility
        /// </summary>
        private void RecordFrame(Skeleton skeleton, List<Bone> targetBones,
            Dictionary<Bone, BoneCurveSet> curveSets, int frame, float time,
            float positionScale, float skeletonFlipRotation)
        {
            foreach (var bone in targetBones)
            {
                var curves = curveSets[bone];

                if (coordSpace == CoordSpace.World)
                {
                    // ── 世界空间采样 ──
                    // World space sampling
                    float px = SpineCompat.GetWorldX(bone) * positionScale;
                    float py = SpineCompat.GetWorldY(bone) * positionScale;

                    // 旋转：使用骨骼的世界旋转
                    // Rotation: use bone's world rotation
                    float rotZ = SpineCompat.GetWorldRotationX(bone) * skeletonFlipRotation;

                    // 缩放：使用世界缩放
                    // Scale: use world scale
                    float sx = SpineCompat.GetWorldScaleX(bone);
                    float sy = SpineCompat.GetWorldScaleY(bone);

                    curves.posX.AddKey(new Keyframe(time, px));
                    curves.posY.AddKey(new Keyframe(time, py));
                    curves.posZ.AddKey(new Keyframe(time, 0f));
                    curves.rotX.AddKey(new Keyframe(time, 0f));
                    curves.rotY.AddKey(new Keyframe(time, 0f));
                    curves.rotZ.AddKey(new Keyframe(time, rotZ));
                    curves.sclX.AddKey(new Keyframe(time, sx));
                    curves.sclY.AddKey(new Keyframe(time, sy));
                    curves.sclZ.AddKey(new Keyframe(time, 1f));
                }
                else
                {
                    // ── 本地空间采样 ──
                    // Local space sampling

                    // 位置
                    // Position
                    float px = SpineCompat.GetAppliedX(bone) * positionScale;
                    float py = SpineCompat.GetAppliedY(bone) * positionScale;

                    // 旋转：根据继承模式判断
                    // Rotation: based on inherit mode
                    float rotZ;
                    bool inheritsRotation = SpineCompat.InheritsRotation(bone);
                    if (inheritsRotation)
                    {
                        rotZ = SpineCompat.GetAppliedRotation(bone);
                    }
                    else
                    {
                        rotZ = SpineCompat.GetWorldRotationX(bone) * skeletonFlipRotation;
                    }

                    // 缩放
                    // Scale
                    float sx = SpineCompat.GetAppliedScaleX(bone);
                    float sy = SpineCompat.GetAppliedScaleY(bone);

                    curves.posX.AddKey(new Keyframe(time, px));
                    curves.posY.AddKey(new Keyframe(time, py));
                    curves.posZ.AddKey(new Keyframe(time, 0f));
                    curves.rotX.AddKey(new Keyframe(time, 0f));
                    curves.rotY.AddKey(new Keyframe(time, 0f));
                    curves.rotZ.AddKey(new Keyframe(time, rotZ));
                    curves.sclX.AddKey(new Keyframe(time, sx));
                    curves.sclY.AddKey(new Keyframe(time, sy));
                    curves.sclZ.AddKey(new Keyframe(time, 1f));
                }
            }
        }

        /// <summary>
        /// 创建跟随空物体并挂载Animation组件
        /// Create follower empty objects and attach Animation components
        /// 自动将动画第1帧的位置、旋转、缩放设为物体默认参数
        /// Auto-set first frame's position, rotation, scale as object default transform
        /// </summary>
        private void CreateFollowerObjects(Skeleton skeleton, List<Bone> targetBones, List<string> clipPaths, string animName)
        {
            if (spineObject == null) return;

            Undo.SetCurrentGroupName("Spine骨骼烘焙 - 创建跟随物体");
            int undoGroup = Undo.GetCurrentGroup();

            int createdCount = 0;
            int skippedCount = 0;

            // 确定跟随物体的父级容器
            // Determine the parent container for follower objects
            // 合并模式下使用 mergeBasePath 对应的 Hierarchy 路径；新建模式下直接挂在 spineObject 下
            // In merge mode, use mergeBasePath as Hierarchy path; in new clip mode, parent is spineObject
            Transform parentTransform = spineObject.transform;
            if (outputMode == OutputMode.MergeClip && !string.IsNullOrEmpty(mergeBasePath))
            {
                // 尝试找到 basePath 对应的父级物体
                // Try to find the parent object matching basePath
                Transform baseParent = spineObject.transform.Find(mergeBasePath);
                if (baseParent != null)
                {
                    parentTransform = baseParent;
                }
                else
                {
                    Debug.LogWarning($"[Moshi_SpineBake] 未找到路径 \"{mergeBasePath}\" 对应的物体，跟随物体将创建在 {spineObject.name} 下");
                }
            }

            for (int i = 0; i < targetBones.Count; i++)
            {
                Bone bone = targetBones[i];
                string boneName = bone.Data.Name;
                string safeBoneName = boneName.Replace("/", "_").Replace("\\", "_");

                string goName = $"Baked_{safeBoneName}";

                // ── 检测是否已存在同名子物体 ──
                // Check if a child with the same name already exists
                Transform existingChild = parentTransform.Find(goName);

                if (existingChild != null)
                {
                    // 已存在同名物体：跳过创建，不改变初始姿态
                    // Same-name object exists: skip creation, keep its Transform unchanged
                    skippedCount++;

                    // 合并模式不挂Animation（曲线已写入统一Clip）；新建模式才更新Animation引用
                    // Merge mode skips Animation (curves written to unified clip); new clip mode updates Animation ref
                    if (outputMode == OutputMode.NewClip && i < clipPaths.Count)
                    {
                        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPaths[i]);
                        if (clip != null)
                        {
                            UnityEngine.Animation anim = existingChild.GetComponent<UnityEngine.Animation>();
                            if (anim == null)
                            {
                                anim = Undo.AddComponent<UnityEngine.Animation>(existingChild.gameObject);
                            }
                            else
                            {
                                Undo.RecordObject(anim, "更新动画引用");
                            }

                            // 移除旧的同名clip再重新添加
                            // Remove old clip with same name then re-add
                            if (anim.GetClip(clip.name) != null)
                                anim.RemoveClip(clip.name);
                            anim.AddClip(clip, clip.name);
                            anim.clip = clip;
                        }
                    }

                    continue;
                }

                // ── 不存在同名物体：新建 + 设置初始姿态 ──
                // No same-name object: create new + set initial pose
                GameObject follower = new GameObject(goName);
                Undo.RegisterCreatedObjectUndo(follower, "创建跟随物体");

                follower.transform.SetParent(parentTransform, false);

                // 合并模式不挂Animation（曲线已写入统一Clip）；新建模式才挂载
                // Merge mode skips Animation (curves in unified clip); new clip mode attaches Animation
                AnimationClip newClip = (outputMode == OutputMode.NewClip && i < clipPaths.Count)
                    ? AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPaths[i])
                    : null;
                if (outputMode == OutputMode.NewClip && newClip != null)
                {
                    UnityEngine.Animation animComp = follower.AddComponent<UnityEngine.Animation>();
                    animComp.AddClip(newClip, newClip.name);
                    animComp.clip = newClip;
                    animComp.playAutomatically = true;
                }

                // ── 设置初始Transform（仅新建物体才设置） ──
                // Set initial Transform (only for newly created objects)
                if (initPoseMode == InitPoseMode.FirstFrame)
                {
                    // 合并模式：从targetClip中按骨骼名过滤提取首帧数据
                    // 新建模式：clip内只有当前骨骼曲线，无需过滤
                    // Merge mode: filter by bone name from targetClip for first frame
                    // New clip mode: clip only contains current bone curves, no filter needed
                    if (outputMode == OutputMode.MergeClip)
                    {
                        ApplyFirstFrameTransform(follower.transform, targetClip, goName);
                    }
                    else
                    {
                        ApplyFirstFrameTransform(follower.transform, newClip);
                    }
                }
                else
                {
                    // 骨骼绑定模式：从BoneData读取Setup Pose原始数据
                    // Setup pose mode: read original bind data from BoneData
                    ApplySetupPoseTransform(follower.transform, bone, skeleton);
                }

                createdCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            // 输出日志提示（方便调试）
            // Log summary for debugging
            if (skippedCount > 0)
            {
                Debug.Log($"[Moshi_SpineBake] 跟随物体: 新建 {createdCount} 个, 跳过 {skippedCount} 个(已存在同名物体，保留原Transform)");
            }
        }

        /// <summary>
        /// 将AnimationClip第1帧的位置、旋转、缩放应用到Transform的默认值
        /// Apply first frame's position, rotation, scale from AnimationClip to Transform defaults
        /// </summary>
        /// <summary>
        /// 将AnimationClip第1帧的位置、旋转、缩放应用到Transform的默认值
        /// Apply first frame's position, rotation, scale from AnimationClip to Transform defaults
        /// </summary>
        /// <param name="target">目标Transform</param>
        /// <param name="clip">动画Clip</param>
        /// <param name="filterPath">仅匹配此路径后缀的曲线（用于合并Clip中区分不同骨骼），null则匹配全部</param>
        private void ApplyFirstFrameTransform(Transform target, AnimationClip clip, string filterPath = null)
        {
            if (clip == null) return;

            // 默认值
            // Default values
            Vector3 pos = Vector3.zero;
            Vector3 rot = Vector3.zero;
            Vector3 scl = Vector3.one;

            // 遍历所有曲线，提取 time=0 时的值
            // Iterate all curves, extract value at time=0
            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                // 若指定了filterPath，只匹配路径以filterPath结尾的曲线
                // If filterPath is specified, only match curves whose path ends with filterPath
                if (filterPath != null)
                {
                    // binding.path 可能是 "Long/huakaijianlong/Baked_L2" 或 "Baked_L2"
                    // 检查路径是否以 filterPath 结尾（即最后一段路径匹配）
                    // Check if path ends with filterPath (last segment matches)
                    if (!binding.path.EndsWith(filterPath) &&
                        !binding.path.Equals(filterPath))
                        continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0) continue;

                // 取第1帧（time=0）的值
                // Get first frame (time=0) value
                float val = curve.Evaluate(0f);

                switch (binding.propertyName)
                {
                    case "localPosition.x": pos.x = val; break;
                    case "localPosition.y": pos.y = val; break;
                    case "localPosition.z": pos.z = val; break;
                    case "localEulerAnglesRaw.x": rot.x = val; break;
                    case "localEulerAnglesRaw.y": rot.y = val; break;
                    case "localEulerAnglesRaw.z": rot.z = val; break;
                    case "localScale.x": scl.x = val; break;
                    case "localScale.y": scl.y = val; break;
                    case "localScale.z": scl.z = val; break;
                }
            }

            // 应用到物体默认Transform
            // Apply to object default Transform
            bool recPos = (recordChannel & RecordChannel.Position) != 0;
            bool recRot = (recordChannel & RecordChannel.Rotation) != 0;
            bool recScl = (recordChannel & RecordChannel.Scale) != 0;

            if (recPos) target.localPosition = pos;
            if (recRot) target.localRotation = Quaternion.Euler(rot);
            if (recScl) target.localScale = scl;
        }

        /// <summary>
        /// 将Spine骨骼的Setup Pose（原始绑定姿态）应用到Transform的默认值
        /// Apply bone's Setup Pose (original bind pose) from BoneData to Transform defaults
        /// 通过 SpineCompat 适配层兼容 Spine 3.8 / 4.2
        /// Uses SpineCompat adapter for Spine 3.8 / 4.2 compatibility
        /// </summary>
        private void ApplySetupPoseTransform(Transform target, Bone bone, Skeleton skeleton)
        {
            if (bone == null) return;

            BoneData data = bone.Data;
            float positionScale = GetPositionScale();

            bool recPos = (recordChannel & RecordChannel.Position) != 0;
            bool recRot = (recordChannel & RecordChannel.Rotation) != 0;
            bool recScl = (recordChannel & RecordChannel.Scale) != 0;

            if (coordSpace == CoordSpace.World)
            {
                // 世界空间：需要使用骨骼在SetupPose下的世界坐标
                // World space: need bone's world coordinates under SetupPose
                SpineCompat.SetToSetupPose(skeleton);
                SpineCompat.UpdateWorldTransform(skeleton);

                float skeletonFlipRotation = Mathf.Sign(skeleton.ScaleX * skeleton.ScaleY);

                if (recPos) target.localPosition = new Vector3(SpineCompat.GetWorldX(bone) * positionScale, SpineCompat.GetWorldY(bone) * positionScale, 0f);
                if (recRot) target.localRotation = Quaternion.Euler(0f, 0f, SpineCompat.GetWorldRotationX(bone) * skeletonFlipRotation);
                if (recScl) target.localScale = new Vector3(SpineCompat.GetWorldScaleX(bone), SpineCompat.GetWorldScaleY(bone), 1f);
            }
            else
            {
                // 本地空间：直接从BoneData读取原始绑定数据
                // Local space: read directly from BoneData's original bind values
                if (recPos) target.localPosition = new Vector3(SpineCompat.GetSetupPoseX(data) * positionScale, SpineCompat.GetSetupPoseY(data) * positionScale, 0f);
                if (recRot) target.localRotation = Quaternion.Euler(0f, 0f, SpineCompat.GetSetupPoseRotation(data));
                if (recScl) target.localScale = new Vector3(SpineCompat.GetSetupPoseScaleX(data), SpineCompat.GetSetupPoseScaleY(data), 1f);
            }
        }

        /// <summary>
        /// 将烘焙的骨骼曲线合并到已有的AnimationClip中
        /// Merge baked bone curves into an existing AnimationClip
        /// 每个骨骼使用 "basePath/Baked_骨骼名" 作为曲线路径，同名曲线将被覆盖
        /// Each bone uses "basePath/Baked_BoneName" as curve path, same-name curves will be overwritten
        /// </summary>
        private void MergeCurvesToExistingClip(List<Bone> targetBones, Dictionary<Bone, BoneCurveSet> curveSets)
        {
            if (targetClip == null) return;

            bool recPos = (recordChannel & RecordChannel.Position) != 0;
            bool recRot = (recordChannel & RecordChannel.Rotation) != 0;
            bool recScl = (recordChannel & RecordChannel.Scale) != 0;

            // 构建基础路径前缀
            // Build base path prefix
            string pathPrefix = string.IsNullOrEmpty(mergeBasePath) ? "" : mergeBasePath.TrimEnd('/') + "/";

            // 先清除即将写入的曲线（避免残留旧数据）
            // First remove curves that we're about to write (avoid stale data)
            var existingBindings = AnimationUtility.GetCurveBindings(targetClip);

            // 收集需要清除的子路径集合
            // Collect sub-paths to clear
            HashSet<string> pathsToClear = new HashSet<string>();
            foreach (var bone in targetBones)
            {
                string safeBoneName = bone.Data.Name.Replace("/", "_").Replace("\\", "_");
                pathsToClear.Add($"{pathPrefix}Baked_{safeBoneName}");
            }

            // 移除已有的同路径曲线（保留其他曲线不动）
            // Remove existing curves at same paths (keep other curves untouched)
            foreach (var binding in existingBindings)
            {
                if (pathsToClear.Contains(binding.path))
                {
                    AnimationUtility.SetEditorCurve(targetClip, binding, null);
                }
            }

            // 写入新曲线
            // Write new curves
            int boneIndex = 0;
            foreach (var bone in targetBones)
            {
                var curves = curveSets[bone];
                string safeBoneName = bone.Data.Name.Replace("/", "_").Replace("\\", "_");
                string relativePath = $"{pathPrefix}Baked_{safeBoneName}";

                if (recPos)
                {
                    targetClip.SetCurve(relativePath, typeof(Transform), "localPosition.x", curves.posX);
                    targetClip.SetCurve(relativePath, typeof(Transform), "localPosition.y", curves.posY);
                    targetClip.SetCurve(relativePath, typeof(Transform), "localPosition.z", curves.posZ);
                }

                if (recRot)
                {
                    targetClip.SetCurve(relativePath, typeof(Transform), "localEulerAnglesRaw.x", curves.rotX);
                    targetClip.SetCurve(relativePath, typeof(Transform), "localEulerAnglesRaw.y", curves.rotY);
                    targetClip.SetCurve(relativePath, typeof(Transform), "localEulerAnglesRaw.z", curves.rotZ);
                }

                if (recScl)
                {
                    targetClip.SetCurve(relativePath, typeof(Transform), "localScale.x", curves.sclX);
                    targetClip.SetCurve(relativePath, typeof(Transform), "localScale.y", curves.sclY);
                    targetClip.SetCurve(relativePath, typeof(Transform), "localScale.z", curves.sclZ);
                }

                boneIndex++;
                bakeProgress = 0.8f + 0.2f * boneIndex / targetBones.Count;
                bakeStatus = $"合并曲线... {boneIndex}/{targetBones.Count}";
            }

            // 确保曲线切线正确
            // Ensure curve tangents are correct
            targetClip.EnsureQuaternionContinuity();

            // 保存修改
            // Save changes
            EditorUtility.SetDirty(targetClip);
            AssetDatabase.SaveAssets();

            // 清除自动检测缓存，下次重绘会重新推算
            // Clear auto-detect cache so next repaint will re-infer
            cachedDetectSpine = null;
        }

        /// <summary>
        /// 从Spine对象在Hierarchy中的路径推算basePath（用于Clip中无Baked_曲线的情况）
        /// Infer basePath from Spine object's Hierarchy path (for clips without Baked_ curves)
        /// 从Spine对象向上遍历到起点节点，构建Hierarchy相对路径
        /// Traverse from Spine object up to stop node, build Hierarchy relative path
        /// </summary>
        /// <param name="spineGO">Spine对象</param>
        /// <param name="stopAt">推算起点节点（不包含该节点本身），null则默认去掉最顶层根节点</param>
        private string InferBasePathFromHierarchy(GameObject spineGO, Transform stopAt = null)
        {
            if (spineGO == null) return "";

            // 从Spine对象自身开始，向上收集路径
            // Start from Spine object, collect path upwards
            List<string> pathParts = new List<string>();
            Transform current = spineGO.transform;

            if (stopAt != null)
            {
                // 有手动起点：从Spine向上收集直到遇到stopAt（不包含stopAt本身）
                // With manual root: collect from Spine upwards until stopAt (excluding stopAt itself)
                while (current != null && current != stopAt)
                {
                    pathParts.Insert(0, current.name);
                    current = current.parent;
                }

                // 验证stopAt确实是Spine对象的祖先
                // Verify stopAt is actually an ancestor of Spine object
                if (current != stopAt)
                {
                    // stopAt不是祖先，回退到默认逻辑
                    // stopAt is not an ancestor, fallback to default logic
                    Debug.LogWarning($"[Moshi_SpineBake] 推算起点 \"{stopAt.name}\" 不是 \"{spineGO.name}\" 的祖先节点，已回退到默认模式。");
                    return InferBasePathFromHierarchy(spineGO, null);
                }

                return pathParts.Count > 0 ? string.Join("/", pathParts) : "";
            }
            else
            {
                // 默认模式：收集所有层级，去掉第一层根节点
                // Default mode: collect all levels, remove first root
                while (current != null)
                {
                    pathParts.Insert(0, current.name);
                    current = current.parent;
                }

                // pathParts 现在是 [根节点, 子1, 子2, ..., SpineObject]
                // 去掉第一层根节点（通常是Prefab根或场景根）
                // Remove first level root (usually Prefab root or scene root)
                if (pathParts.Count > 1)
                {
                    pathParts.RemoveAt(0);
                    return string.Join("/", pathParts);
                }

                // Spine对象自身就是根节点，无前缀
                // Spine object is the root itself, no prefix
                return "";
            }
        }

        /// <summary>
        /// 骨骼曲线数据容器
        /// Bone curve data container
        /// </summary>
        private class BoneCurveSet
        {
            public AnimationCurve posX = new AnimationCurve();
            public AnimationCurve posY = new AnimationCurve();
            public AnimationCurve posZ = new AnimationCurve();
            public AnimationCurve rotX = new AnimationCurve();
            public AnimationCurve rotY = new AnimationCurve();
            public AnimationCurve rotZ = new AnimationCurve();
            public AnimationCurve sclX = new AnimationCurve();
            public AnimationCurve sclY = new AnimationCurve();
            public AnimationCurve sclZ = new AnimationCurve();
        }
#endif
    }
}
