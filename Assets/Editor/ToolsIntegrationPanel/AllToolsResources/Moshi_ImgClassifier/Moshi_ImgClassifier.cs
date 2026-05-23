#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using UnityEngine;
using UnityEditor;

namespace MoshiTools
{
    /// <summary>
    /// 图片分类归档工具（画面记忆模式）
    /// Image Classification and Archiving Tool (Visual Fingerprint Mode)
    /// </summary>
    public partial class Moshi_ImgClassifier : EditorWindow
    {
        #region 枚举定义

        /// <summary>
        /// 工具标签页
        /// Tool tab
        /// </summary>
        private enum ToolTab
        {
            Classify,       // 分类归档
            DuplicateClean  // 重复查清
        }

        /// <summary>
        /// 匹配模式
        /// Match mode
        /// </summary>
        public enum MatchMode
        {
            Preview,    // 预览模式
            Execute     // 执行模式
        }

        /// <summary>
        /// 匹配策略
        /// Match strategy
        /// </summary>
        public enum MatchStrategy
        {
            Average,    // 平均指纹比较
            KNN         // K近邻投票
        }

        #endregion

        #region 数据类定义

        /// <summary>
        /// 分类结果项
        /// Classification result item
        /// </summary>
        public class ClassifyResult
        {
            public string filePath;              // 文件路径
            public string fileName;              // 文件名
            public string matchedCategory;       // 匹配的分类名称
            public float similarity;             // 相似度
            public string targetPath;            // 目标路径
            public bool isMatched;               // 是否匹配成功
        }

        /// <summary>
        /// 配置数据
        /// Configuration data
        /// </summary>
        [Serializable]
        public class ConfigData
        {
            public string lastScanFolder = "";
            public bool includeSubfolders = true;
            public string unmatchedFolder = "";  // 未匹配文件目标文件夹
            public List<LearnedCategory> learnedCategories = new List<LearnedCategory>();
        }

        // =====================================================
        // 画面指纹系统 - 直接记忆图片的空间结构和颜色分布
        // Visual Fingerprint System - directly memorize spatial structure and color distribution
        // =====================================================

        /// <summary>
        /// 缩略图尺寸（像素指纹的分辨率）
        /// Thumbnail size for pixel fingerprint resolution
        /// </summary>
        private const int FINGERPRINT_SIZE = 32;

        /// <summary>
        /// 图片画面指纹 - 81维特征向量 + 余弦相似度（A+C方案v4.0）
        /// Image visual fingerprint - 81-dim feature vector + cosine similarity (A+C scheme v4.0)
        /// 
        /// 核心思路：将图片的视觉特征压缩为81维向量，
        /// 使用余弦相似度比较，比逐像素比较快75倍、存储小100倍。
        /// v4.0 新增8维语义特征，增强Fire/Light/Star/Shape等VFX贴图的区分能力。
        /// Core idea: compress visual features into 81-dim vector,
        /// use cosine similarity, 75x faster and 100x smaller than per-pixel comparison.
        /// v4.0 adds 8 semantic dimensions for better VFX texture discrimination.
        /// </summary>
        [Serializable]
        public class ImageFingerprint
        {
            // ===== 特征向量（用于余弦相似度比较）=====
            // Feature vector (for cosine similarity comparison)
            // 总计 69 维
            public float[] featureVector;

            // ===== 硬规则预过滤用的原始值（不参与向量比较）=====
            // Raw values for hard-rule pre-filtering (not part of vector comparison)
            public float overallAlpha;      // 整体不透明度（0=全透明，1=全不透明）
            public float avgBrightness;     // 平均亮度
            public float avgSaturation;     // 平均饱和度
            public float compactness;       // 紧凑度（非透明像素占比）

            // 序列帧网格检测特征（区分Particle序列帧 vs Glow单图）
            // Sprite sheet grid detection features (distinguish Particle sprite sheets vs Glow single images)
            public int estimatedGridCount;     // 估计的子图数量（1=单图，4=2×2，9=3×3...）
            public int estimatedRows;          // 估计行数
            public int estimatedCols;          // 估计列数
            public float gridRegularity;       // 网格规律性（0=无规律，1=完美网格）

            // 语义特征（用于硬规则预过滤，不参与向量比较）
            // Semantic features (for hard-rule pre-filtering, not part of vector comparison)
            public float colorTemperature;   // 颜色温度（0=冷色, 0.5=中性, 1=暖色）
            public float warmColorRatio;     // 暖色占比（红/橙/黄色系）
            public float coolColorRatio;     // 冷色占比（蓝/青/绿色系）
            public float dominantHue;        // 主色调（0~1 = 0°~360°色相）
            public float colorPurity;        // 颜色纯度（高饱和像素占比）

            // 特征向量维度常量
            // Feature vector dimension constants
            public const int DIM_GLOBAL = 4;           // 全局统计特征
            public const int DIM_SPATIAL = 16;         // 4×4空间亮度分布
            public const int DIM_ALPHA_SPATIAL = 16;   // 4×4空间Alpha分布
            public const int DIM_COLOR_HIST = 8;       // 颜色直方图(色相8bin)
            public const int DIM_SHAPE = 8;            // Alpha形状描述子(极坐标8方向)
            public const int DIM_GRID = 5;             // 网格结构特征
            public const int DIM_RADIAL = 6;           // 径向特征(梯度/对称性/4环带)
            public const int DIM_EDGE = 4;             // 边缘连续性(水平/垂直/左右Alpha衰减)
            public const int DIM_TEXTURE = 2;          // 纹理频率(局部方差/全局方差)
            public const int DIM_GLOW_DISC = 4;        // Glow区分特征(锐度/角度集中/衰减平滑/边界清晰)
            public const int DIM_SEMANTIC = 8;         // 语义区分特征(温度/暖色/冷色/尖角/方向数/纯度/亮度集中/形状规则)
            public const int TOTAL_DIM = DIM_GLOBAL + DIM_SPATIAL + DIM_ALPHA_SPATIAL
                                        + DIM_COLOR_HIST + DIM_SHAPE + DIM_GRID
                                        + DIM_RADIAL + DIM_EDGE + DIM_TEXTURE
                                        + DIM_GLOW_DISC + DIM_SEMANTIC; // = 81

            public ImageFingerprint()
            {
                featureVector = new float[TOTAL_DIM];
            }

            /// <summary>
            /// 计算与另一个指纹的余弦相似度（0~1）
            /// Cosine similarity between two feature vectors (0~1)
            /// 
            /// 优势：
            /// 1. 每个维度天然平等，不需要手动调权重
            /// 2. 计算只需 81 次乘法，比逐像素比较快 75 倍
            /// 3. 对向量长度不敏感，只看"方向"
            /// </summary>
            public float CompareTo(ImageFingerprint other)
            {
                if (other == null || featureVector == null || other.featureVector == null) return 0f;

                float dotProduct = 0f;
                float normA = 0f;
                float normB = 0f;

                for (int i = 0; i < TOTAL_DIM; i++)
                {
                    dotProduct += featureVector[i] * other.featureVector[i];
                    normA += featureVector[i] * featureVector[i];
                    normB += other.featureVector[i] * other.featureVector[i];
                }

                float denominator = Mathf.Sqrt(normA) * Mathf.Sqrt(normB);
                if (denominator < 1e-8f) return 0f;

                // 余弦相似度范围 [-1, 1]，映射到 [0, 1]
                // Cosine similarity range [-1, 1], map to [0, 1]
                float cosSim = dotProduct / denominator;
                return Mathf.Clamp01((cosSim + 1f) * 0.5f);
            }

            /// <summary>
            /// 计算多个指纹的平均指纹（用于分类的"代表指纹"）
            /// Calculate average fingerprint from multiple fingerprints
            /// </summary>
            public static ImageFingerprint ComputeAverage(List<ImageFingerprint> fingerprints)
            {
                if (fingerprints == null || fingerprints.Count == 0) return null;

                var avg = new ImageFingerprint();
                int count = fingerprints.Count;

                // 向量逐维求平均
                // Average each dimension of feature vector
                for (int i = 0; i < TOTAL_DIM; i++)
                {
                    float sum = 0f;
                    foreach (var fp in fingerprints)
                        sum += fp.featureVector[i];
                    avg.featureVector[i] = sum / count;
                }

                // 原始字段也求平均（用于硬规则和UI显示）
                // Average raw fields (for hard rules and UI display)
                foreach (var fp in fingerprints)
                {
                    avg.overallAlpha += fp.overallAlpha;
                    avg.avgBrightness += fp.avgBrightness;
                    avg.avgSaturation += fp.avgSaturation;
                    avg.compactness += fp.compactness;
                    avg.estimatedGridCount += fp.estimatedGridCount;
                    avg.estimatedRows += fp.estimatedRows;
                    avg.estimatedCols += fp.estimatedCols;
                    avg.gridRegularity += fp.gridRegularity;
                    avg.colorTemperature += fp.colorTemperature;
                    avg.warmColorRatio += fp.warmColorRatio;
                    avg.coolColorRatio += fp.coolColorRatio;
                    avg.dominantHue += fp.dominantHue;
                    avg.colorPurity += fp.colorPurity;
                }

                avg.overallAlpha /= count;
                avg.avgBrightness /= count;
                avg.avgSaturation /= count;
                avg.compactness /= count;
                avg.estimatedGridCount = Mathf.RoundToInt((float)avg.estimatedGridCount / count);
                avg.estimatedRows = Mathf.RoundToInt((float)avg.estimatedRows / count);
                avg.estimatedCols = Mathf.RoundToInt((float)avg.estimatedCols / count);
                avg.gridRegularity /= count;
                avg.colorTemperature /= count;
                avg.warmColorRatio /= count;
                avg.coolColorRatio /= count;
                avg.dominantHue /= count;
                avg.colorPurity /= count;

                return avg;
            }
        }

        /// <summary>
        /// 学习到的分类（画面记忆模式）
        /// Learned category (Visual Fingerprint Mode)
        /// </summary>
        [Serializable]
        public class LearnedCategory
        {
            public string categoryName;                          // 分类名称
            public string targetFolder;                          // 目标文件夹
            public ImageFingerprint avgFingerprint;              // 平均指纹（代表指纹）
            public List<ImageFingerprint> sampleFingerprints;    // 样本指纹列表
            public int sampleCount;                              // 样本数量

            public LearnedCategory()
            {
                sampleFingerprints = new List<ImageFingerprint>();
                avgFingerprint = null;
            }
        }

        #endregion

        #region 窗口变量

        private const string TOOL_NAME = "图片分类归档";
        private ToolTab currentTab = ToolTab.Classify;
        // 配置文件跟随工具主文件存放（延迟初始化，避免静态构造器中调用 AssetDatabase）
        // Config file stored alongside the main tool script (lazy init to avoid AssetDatabase calls in static ctor)
        private static string _configPath;
        private static string CONFIG_PATH
        {
            get
            {
                if (string.IsNullOrEmpty(_configPath))
                    _configPath = GetConfigPath();
                return _configPath;
            }
        }
        
        private static string GetConfigPath()
        {
            // 获取当前脚本所在目录，配置文件放在同目录下
            // Get current script directory, store config in the same folder
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:MonoScript Moshi_ImgClassifier");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("Moshi_ImgClassifier.cs"))
                {
                    return Path.Combine(Path.GetDirectoryName(path), "Moshi_ImgClassifier_Config.json");
                }
            }
            // 兜底路径
            // Fallback path
            return "Assets/Editor/ToolsIntegrationPanel/AllToolsResources/Moshi_ImgClassifier/Moshi_ImgClassifier_Config.json";
        }

        private ConfigData config = new ConfigData();
        private List<ClassifyResult> classifyResults = new List<ClassifyResult>();
        
        private string scanFolder = "";
        private bool includeSubfolders = true;
        private MatchMode matchMode = MatchMode.Preview;
        
        // 智能学习相关
        private string sampleRootFolder = "";
        private List<SubFolderInfo> selectedSubFolders = new List<SubFolderInfo>();
        private Vector2 sampleScrollPosition;
        private Vector2 categoryScrollPosition;
        private int selectedCategoryIndex = -1;
        private bool showLearningResults = true;
        private float similarityThreshold = 0.85f;  // 相似度阈值（余弦相似度模式，同类通常>0.9）
        private bool enableDebugLog = false;          // 调试日志开关（分类时输出详细得分）
        private string unmatchedFolder = "";  // 未匹配文件目标文件夹
        
        private Vector2 resultScrollPosition;
        private bool showResults = true;
        
        // 匹配策略
        private MatchStrategy matchStrategy = MatchStrategy.KNN;
        private int knnK = 3; // KNN的K值
        
        private GUIStyle headerStyle;
        private GUIStyle categoryItemStyle;
        private GUIStyle matchedStyle;
        private GUIStyle unmatchedStyle;
        
        #endregion

        #region 菜单入口

        [MenuItem("工具/Moshi/" + TOOL_NAME)]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_ImgClassifier>(TOOL_NAME);
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        #endregion

        #region 生命周期

        private void OnEnable()
        {
            LoadConfig();
        }

        private void OnDisable()
        {
            SaveConfig();
        }

        private void InitStyles()
        {
            if (headerStyle != null) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(5, 5, 5, 5)
            };

            categoryItemStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 5, 5)
            };

            matchedStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.2f, 0.6f, 0.2f) }
            };

            unmatchedStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.red }
            };
        }

        #endregion

        #region GUI绘制

        private void OnGUI()
        {
            if (headerStyle == null) InitStyles();

            EditorGUILayout.Space(5);

            // 标题
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(TOOL_NAME, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 标签页切换按钮组
            // Tab switching button group
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(currentTab == ToolTab.Classify, "分类归档", EditorStyles.miniButtonLeft))
                currentTab = ToolTab.Classify;
            if (GUILayout.Toggle(currentTab == ToolTab.DuplicateClean, "重复查清", EditorStyles.miniButtonRight))
                currentTab = ToolTab.DuplicateClean;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 根据标签页绘制内容
            // Draw content based on selected tab
            switch (currentTab)
            {
                case ToolTab.Classify:
                    DrawClassifyTab();
                    break;
                case ToolTab.DuplicateClean:
                    DrawDuplicateCleanTab();
                    break;
            }
        }

        /// <summary>
        /// 绘制分类归档标签页（原 OnGUI 内容）
        /// Draw classify tab (original OnGUI content)
        /// </summary>
        private void DrawClassifyTab()
        {
            EditorGUILayout.LabelField("从已分类样本中学习特征向量，硬规则预过滤+余弦相似度智能分类", EditorStyles.miniLabel);

            EditorGUILayout.Space(10);

            // 样本文件夹管理
            DrawSampleFolders();

            EditorGUILayout.Space(5);

            // 学习到的分类
            DrawLearnedCategories();

            EditorGUILayout.Space(5);

            // 扫描设置
            DrawScanSettings();

            EditorGUILayout.Space(5);

            // 操作按钮
            DrawActionButtons();

            EditorGUILayout.Space(10);

            // 结果显示
            DrawResults();
        }


        /// <summary>
        /// 绘制样本文件夹管理
        /// </summary>
        private void DrawSampleFolders()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("样本文件夹", headerStyle);
            EditorGUILayout.LabelField("添加或自动识别子文件夹作为分类样本", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // 主文件夹选择与自动识别
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("主文件夹", GUILayout.Width(60));
            sampleRootFolder = EditorGUILayout.TextField(sampleRootFolder);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择样本主文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    string dataPath = Application.dataPath;
                    if (path.StartsWith(dataPath))
                    {
                        sampleRootFolder = "Assets" + path.Substring(dataPath.Length);
                    }
                    else
                    {
                        sampleRootFolder = path;
                    }
                }
            }
            
            // 自动识别按钮
            GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
            if (GUILayout.Button("自动识别", GUILayout.Width(60)))
            {
                AutoDetectSubFolders();
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 样本文件夹列表（可编辑）
            EditorGUILayout.LabelField($"样本列表 ({selectedSubFolders.Count} 个):", EditorStyles.miniLabel);
            
            sampleScrollPosition = EditorGUILayout.BeginScrollView(sampleScrollPosition, GUILayout.Height(120));

            int removeIndex = -1;
            for (int i = 0; i < selectedSubFolders.Count; i++)
            {
                var folder = selectedSubFolders[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // 文件夹路径（可编辑）
                folder.path = EditorGUILayout.TextField(folder.path);
                
                // 浏览按钮
                if (GUILayout.Button("浏览", GUILayout.Width(40)))
                {
                    string path = EditorUtility.OpenFolderPanel("选择样本文件夹", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        string dataPath = Application.dataPath;
                        if (path.StartsWith(dataPath))
                        {
                            folder.path = "Assets" + path.Substring(dataPath.Length);
                        }
                        else
                        {
                            folder.path = path;
                        }
                        folder.name = Path.GetFileName(folder.path);
                        folder.fileCount = CountImagesInFolder(folder.path);
                    }
                }

                // 显示图片数量
                EditorGUILayout.LabelField($"({folder.fileCount})", EditorStyles.miniLabel, GUILayout.Width(45));

                // 删除按钮
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    removeIndex = i;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                selectedSubFolders.RemoveAt(removeIndex);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndScrollView();

            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("+ 手动添加", GUILayout.Height(25)))
            {
                selectedSubFolders.Add(new SubFolderInfo { path = "", name = "", fileCount = 0 });
                GUIUtility.ExitGUI();
            }
            
            // VFX推荐分类预设按钮
            // VFX recommended category preset button
            GUI.backgroundColor = new Color(1f, 0.95f, 0.7f);
            if (GUILayout.Button("VFX预设", GUILayout.Height(25), GUILayout.Width(70)))
            {
                ShowVFXPresetMenu();
            }
            GUI.backgroundColor = Color.white;
            
            GUI.backgroundColor = new Color(0.7f, 0.9f, 0.7f);
            if (GUILayout.Button("开始学习", GUILayout.Height(25)))
            {
                PerformLearning();
                GUIUtility.ExitGUI();
                return;
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();



            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 自动识别子文件夹
        /// </summary>
        private void AutoDetectSubFolders()
        {
            if (string.IsNullOrEmpty(sampleRootFolder))
            {
                EditorUtility.DisplayDialog("提示", "请先选择主文件夹", "确定");
                return;
            }

            selectedSubFolders.Clear();

            // 判断是绝对路径还是相对路径
            bool isAbsolutePath = Path.IsPathRooted(sampleRootFolder);
            string fullPath;
            
            if (isAbsolutePath)
            {
                fullPath = sampleRootFolder;
            }
            else
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                fullPath = Path.Combine(projectRoot, sampleRootFolder);
            }

            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("提示", "主文件夹不存在", "确定");
                return;
            }

            try
            {
                string[] subDirs = Directory.GetDirectories(fullPath);
                foreach (var subDir in subDirs)
                {
                    string dirName = Path.GetFileName(subDir);
                    // 子文件夹路径保持与主文件夹相同的格式（绝对或相对）
                    string subFolderPath = subDir.Replace("\\", "/");
                    int fileCount = CountImagesInFolder(subFolderPath);

                    if (fileCount > 0)
                    {
                        selectedSubFolders.Add(new SubFolderInfo
                        {
                            path = subFolderPath,
                            name = dirName,
                            fileCount = fileCount
                        });
                    }
                }

                if (selectedSubFolders.Count == 0)
                {
                    EditorUtility.DisplayDialog("提示", "未找到包含图片的子文件夹", "确定");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"自动识别失败: {e.Message}");
            }
        }

        /// <summary>
        /// 统计文件夹中的图片数量
        /// </summary>
        private int CountImagesInFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return 0;

            // 判断是绝对路径还是相对路径
            string fullPath;
            if (Path.IsPathRooted(folderPath))
            {
                fullPath = folderPath;
            }
            else
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                fullPath = Path.Combine(projectRoot, folderPath);
            }
            
            if (!Directory.Exists(fullPath)) return 0;

            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga"
            };

            int count = 0;
            string[] files = Directory.GetFiles(fullPath, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                if (!file.EndsWith(".meta"))
                {
                    string ext = Path.GetExtension(file);
                    if (imageExtensions.Contains(ext))
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 子文件夹信息
        /// </summary>

        private class SubFolderInfo
        {
            public string path;
            public string name;
            public int fileCount;
        }

        /// <summary>
        /// 显示VFX推荐分类预设菜单
        /// Show VFX recommended category preset menu
        /// </summary>
        private void ShowVFXPresetMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("基础方案（6类）/Fire + Light + Star + Shape + Particle + Smoke"),
                false, () => ApplyVFXPreset(new string[] { "Fire", "Light", "Star", "Shape", "Particle", "Smoke" }));

            menu.AddItem(new GUIContent("标准方案（10类）/+ Spark + Trail + Ring + Distortion"),
                false, () => ApplyVFXPreset(new string[] { "Fire", "Light", "Star", "Shape", "Particle", "Smoke", "Spark", "Trail", "Ring", "Distortion" }));

            menu.AddItem(new GUIContent("完整方案（14类）/+ Flare + Lightning + Dissolve + Noise"),
                false, () => ApplyVFXPreset(new string[] { "Fire", "Light", "Star", "Shape", "Particle", "Smoke", "Spark", "Trail", "Ring", "Distortion", "Flare", "Lightning", "Dissolve", "Noise" }));

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("自定义/添加 Fire（火焰）"), false, () => AddPresetCategory("Fire"));
            menu.AddItem(new GUIContent("自定义/添加 Light（光效）"), false, () => AddPresetCategory("Light"));
            menu.AddItem(new GUIContent("自定义/添加 Glow（光晕）"), false, () => AddPresetCategory("Glow"));
            menu.AddItem(new GUIContent("自定义/添加 Star（星形）"), false, () => AddPresetCategory("Star"));
            menu.AddItem(new GUIContent("自定义/添加 Shape（形状）"), false, () => AddPresetCategory("Shape"));
            menu.AddItem(new GUIContent("自定义/添加 Particle（粒子）"), false, () => AddPresetCategory("Particle"));
            menu.AddItem(new GUIContent("自定义/添加 Smoke（烟雾）"), false, () => AddPresetCategory("Smoke"));
            menu.AddItem(new GUIContent("自定义/添加 Spark（火花）"), false, () => AddPresetCategory("Spark"));
            menu.AddItem(new GUIContent("自定义/添加 Trail（拖尾）"), false, () => AddPresetCategory("Trail"));
            menu.AddItem(new GUIContent("自定义/添加 Ring（圆环）"), false, () => AddPresetCategory("Ring"));
            menu.AddItem(new GUIContent("自定义/添加 Distortion（扭曲）"), false, () => AddPresetCategory("Distortion"));
            menu.AddItem(new GUIContent("自定义/添加 Flare（光斑）"), false, () => AddPresetCategory("Flare"));
            menu.AddItem(new GUIContent("自定义/添加 Lightning（闪电）"), false, () => AddPresetCategory("Lightning"));
            menu.AddItem(new GUIContent("自定义/添加 Dissolve（溶解）"), false, () => AddPresetCategory("Dissolve"));
            menu.AddItem(new GUIContent("自定义/添加 Noise（噪声）"), false, () => AddPresetCategory("Noise"));

            menu.ShowAsContext();
        }

        /// <summary>
        /// 应用VFX分类预设方案（在主文件夹下创建子文件夹结构）
        /// Apply VFX category preset (create subfolder structure under main folder)
        /// </summary>
        private void ApplyVFXPreset(string[] categoryNames)
        {
            if (string.IsNullOrEmpty(sampleRootFolder))
            {
                EditorUtility.DisplayDialog("提示", "请先选择主文件夹，预设将在该文件夹下创建分类子文件夹", "确定");
                return;
            }

            // 获取完整路径
            string fullPath;
            if (Path.IsPathRooted(sampleRootFolder))
                fullPath = sampleRootFolder;
            else
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                fullPath = Path.Combine(projectRoot, sampleRootFolder);
            }

            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("提示", "主文件夹不存在", "确定");
                return;
            }

            // 确认操作
            string categoriesStr = string.Join(", ", categoryNames);
            if (!EditorUtility.DisplayDialog("创建VFX分类文件夹",
                $"将在主文件夹下创建以下分类子文件夹：\n{categoriesStr}\n\n" +
                "已有的子文件夹不会被覆盖。\n" +
                "请将对应类型的样本图片放入各子文件夹后点击\"开始学习\"。",
                "确定创建", "取消"))
                return;

            // 创建子文件夹
            int createdCount = 0;
            foreach (var name in categoryNames)
            {
                string subPath = Path.Combine(fullPath, name);
                if (!Directory.Exists(subPath))
                {
                    Directory.CreateDirectory(subPath);
                    createdCount++;
                }
            }

            // 自动刷新子文件夹列表
            AutoDetectSubFolders();

            string msg = createdCount > 0
                ? $"已创建 {createdCount} 个新文件夹（共 {categoryNames.Length} 个分类）。\n请将样本图片放入各子文件夹后点击\"开始学习\"。"
                : $"所有 {categoryNames.Length} 个分类文件夹已存在。";
            EditorUtility.DisplayDialog("VFX预设", msg, "确定");

            Repaint();
        }

        /// <summary>
        /// 添加单个预设分类文件夹
        /// Add a single preset category folder
        /// </summary>
        private void AddPresetCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(sampleRootFolder))
            {
                EditorUtility.DisplayDialog("提示", "请先选择主文件夹", "确定");
                return;
            }

            string fullPath;
            if (Path.IsPathRooted(sampleRootFolder))
                fullPath = sampleRootFolder;
            else
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                fullPath = Path.Combine(projectRoot, sampleRootFolder);
            }

            string subPath = Path.Combine(fullPath, categoryName);
            if (!Directory.Exists(subPath))
            {
                Directory.CreateDirectory(subPath);
                Debug.Log($"[VFX预设] 已创建分类文件夹: {categoryName}");
            }

            // 检查是否已在列表中
            bool exists = selectedSubFolders.Any(f => 
                Path.GetFileName(f.path.TrimEnd('/', '\\')) == categoryName);

            if (!exists)
            {
                string subFolderPath = Path.IsPathRooted(sampleRootFolder) 
                    ? subPath 
                    : Path.Combine(sampleRootFolder, categoryName).Replace("\\", "/");

                selectedSubFolders.Add(new SubFolderInfo
                {
                    path = subFolderPath,
                    name = categoryName,
                    fileCount = CountImagesInFolder(subFolderPath)
                });
            }

            Repaint();
        }

        /// <summary>
        /// 绘制学习到的分类
        /// </summary>
        private void DrawLearnedCategories()
        {
            var prevShowLearningResults = showLearningResults;
            showLearningResults = EditorGUILayout.Foldout(showLearningResults, 
                $"学习到的分类 ({config.learnedCategories.Count})", true);

            // Foldout 切换后需要 ExitGUI，避免 Layout/Repaint 事件之间控件数量不一致
            // ExitGUI after foldout toggle to prevent control count mismatch between Layout/Repaint events
            if (showLearningResults != prevShowLearningResults)
            {
                GUIUtility.ExitGUI();
                return;
            }

            if (!showLearningResults) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (config.learnedCategories.Count == 0)
            {
                EditorGUILayout.HelpBox("尚未学习任何分类，请先添加样本文件夹并点击\"开始学习\"", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            categoryScrollPosition = EditorGUILayout.BeginScrollView(categoryScrollPosition, GUILayout.Height(150));

            // 延迟应用选中状态变化，避免 Layout/Repaint 事件之间控件数量不一致
            // Deferred selection change to prevent control count mismatch between Layout/Repaint events
            bool selectionChanged = false;
            int newSelectedIndex = selectedCategoryIndex;

            for (int i = 0; i < config.learnedCategories.Count; i++)
            {
                var category = config.learnedCategories[i];
                bool isSelected = (i == selectedCategoryIndex);

                EditorGUILayout.BeginHorizontal(isSelected ? categoryItemStyle : EditorStyles.helpBox);

                // 选择框
                bool wasSelected = isSelected;
                bool nowSelected = GUILayout.Toggle(wasSelected, "", GUILayout.Width(20));
                if (nowSelected != wasSelected)
                {
                    newSelectedIndex = nowSelected ? i : -1;
                    selectionChanged = true;
                }

                // 分类名称
                EditorGUILayout.LabelField(category.categoryName, EditorStyles.boldLabel, GUILayout.Width(120));

                // 样本数量
                EditorGUILayout.LabelField($"样本: {category.sampleCount}", GUILayout.Width(80));

                // 目标文件夹
                if (isSelected)
                {
                    category.targetFolder = EditorGUILayout.TextField(category.targetFolder);
                }
                else
                {
                    EditorGUILayout.LabelField(category.targetFolder);
                }

                EditorGUILayout.EndHorizontal();

                // 显示指纹详情（仅选中时）
                // Show fingerprint details (only when selected)
                if (isSelected && category.sampleFingerprints != null && category.sampleFingerprints.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    
                    // 计算样本参数范围（min~max）
                    // Calculate sample parameter ranges (min~max)
                    var samples = category.sampleFingerprints;
                    float minAlpha = float.MaxValue, maxAlpha = float.MinValue;
                    float minBright = float.MaxValue, maxBright = float.MinValue;
                    float minSat = float.MaxValue, maxSat = float.MinValue;
                    float minCompact = float.MaxValue, maxCompact = float.MinValue;
                    float minRegularity = float.MaxValue, maxRegularity = float.MinValue;
                    int minGrid = int.MaxValue, maxGrid = int.MinValue;
                    float minTemp = float.MaxValue, maxTemp = float.MinValue;
                    float minWarm = float.MaxValue, maxWarm = float.MinValue;
                    float minCool = float.MaxValue, maxCool = float.MinValue;
                    
                    foreach (var s in samples)
                    {
                        if (s.overallAlpha < minAlpha) minAlpha = s.overallAlpha;
                        if (s.overallAlpha > maxAlpha) maxAlpha = s.overallAlpha;
                        if (s.avgBrightness < minBright) minBright = s.avgBrightness;
                        if (s.avgBrightness > maxBright) maxBright = s.avgBrightness;
                        if (s.avgSaturation < minSat) minSat = s.avgSaturation;
                        if (s.avgSaturation > maxSat) maxSat = s.avgSaturation;
                        if (s.compactness < minCompact) minCompact = s.compactness;
                        if (s.compactness > maxCompact) maxCompact = s.compactness;
                        if (s.gridRegularity < minRegularity) minRegularity = s.gridRegularity;
                        if (s.gridRegularity > maxRegularity) maxRegularity = s.gridRegularity;
                        if (s.estimatedGridCount < minGrid) minGrid = s.estimatedGridCount;
                        if (s.estimatedGridCount > maxGrid) maxGrid = s.estimatedGridCount;
                        if (s.colorTemperature < minTemp) minTemp = s.colorTemperature;
                        if (s.colorTemperature > maxTemp) maxTemp = s.colorTemperature;
                        if (s.warmColorRatio < minWarm) minWarm = s.warmColorRatio;
                        if (s.warmColorRatio > maxWarm) maxWarm = s.warmColorRatio;
                        if (s.coolColorRatio < minCool) minCool = s.coolColorRatio;
                        if (s.coolColorRatio > maxCool) maxCool = s.coolColorRatio;
                    }
                    
                    EditorGUILayout.LabelField($"样本数: {samples.Count}  " +
                        $"不透明度: {minAlpha:F2}~{maxAlpha:F2}  亮度: {minBright:F2}~{maxBright:F2}", 
                        EditorStyles.miniLabel);
                    
                    EditorGUILayout.LabelField($"饱和度: {minSat:F2}~{maxSat:F2}  " +
                        $"紧凑度: {minCompact:F2}~{maxCompact:F2}  " +
                        $"规律性: {minRegularity:F2}~{maxRegularity:F2}", 
                        EditorStyles.miniLabel);
                    
                    string gridStr = (minGrid == maxGrid) ? $"{minGrid}" : $"{minGrid}~{maxGrid}";
                    EditorGUILayout.LabelField($"子图数: {gridStr}  " +
                        $"色温: {minTemp:F2}~{maxTemp:F2}  " +
                        $"暖色: {minWarm:F2}~{maxWarm:F2}  " +
                        $"冷色: {minCool:F2}~{maxCool:F2}", 
                        EditorStyles.miniLabel);
                    
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();

            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            
            // 导出按钮
            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            if (GUILayout.Button("导出数据", GUILayout.Height(25)))
            {
                ExportLearnedData();
            }
            
            // 导入按钮
            GUI.backgroundColor = new Color(0.9f, 1f, 0.7f);
            if (GUILayout.Button("导入数据", GUILayout.Height(25)))
            {
                ImportLearnedData();
                GUIUtility.ExitGUI();
                return;
            }
            
            // 清除按钮
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("清除数据", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("确认", "确定要清除所有学习到的分类数据吗？", "确定", "取消"))
                {
                    config.learnedCategories.Clear();
                    SaveConfig();
                    GUIUtility.ExitGUI();
                }
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 延迟应用选中状态变化并 ExitGUI，确保所有布局组已正确关闭
            // Apply deferred selection change after all layout groups are properly closed
            if (selectionChanged)
            {
                selectedCategoryIndex = newSelectedIndex;
                GUIUtility.ExitGUI();
            }
        }

        /// <summary>
        /// 导出学习数据
        /// </summary>
        private void ExportLearnedData()
        {
            if (config.learnedCategories.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有可导出的学习数据", "确定");
                return;
            }

            string defaultPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), "UserSettings");
            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }

            string filePath = EditorUtility.SaveFilePanel("导出学习数据", defaultPath, 
                $"ImgClassifier_Data_{DateTime.Now:yyyyMMdd}", "json");

            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                var exportData = new LearnedDataExport
                {
                    version = "4.0",
                    exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    categories = config.learnedCategories
                };

                string json = JsonUtility.ToJson(exportData, true);
                File.WriteAllText(filePath, json);

                EditorUtility.DisplayDialog("导出成功", 
                    $"已导出 {config.learnedCategories.Count} 个分类数据到:\n{filePath}", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("导出失败", $"导出失败: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 导入学习数据
        /// </summary>
        private void ImportLearnedData()
        {
            string filePath = EditorUtility.OpenFilePanel("导入学习数据", "", "json");
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                string json = File.ReadAllText(filePath);
                var importData = JsonUtility.FromJson<LearnedDataExport>(json);

                if (importData == null || importData.categories == null || importData.categories.Count == 0)
                {
                    EditorUtility.DisplayDialog("导入失败", "文件格式无效或没有分类数据", "确定");
                    return;
                }

                // 版本兼容性检查：旧版数据维度不匹配，无法使用
                // Version compatibility check: old data dimension mismatch
                if (!string.IsNullOrEmpty(importData.version) && importData.version != "4.0")
                {
                    EditorUtility.DisplayDialog("版本不兼容", 
                        $"导入的数据为旧版(v{importData.version})，当前版本为v4.0({ImageFingerprint.TOTAL_DIM}维)。\n" +
                        "维度不兼容，请重新学习样本指纹。", "确定");
                    return;
                }

                // 询问导入方式
                int option = EditorUtility.DisplayDialogComplex("导入方式",
                    $"发现 {importData.categories.Count} 个分类数据\n" +
                    $"导出版本: {importData.version}\n" +
                    $"导出时间: {importData.exportTime}\n\n" +
                    "请选择导入方式:",
                    "合并（保留现有）", "覆盖（替换现有）", "取消");

                if (option == 2) return; // 取消

                if (option == 1) // 覆盖
                {
                    config.learnedCategories.Clear();
                }

                // 合并导入（处理重复分类名）
                int addedCount = 0;
                int mergedCount = 0;

                foreach (var category in importData.categories)
                {
                    var existing = config.learnedCategories.FirstOrDefault(c => c.categoryName == category.categoryName);
                    
                    if (existing != null)
                    {
                        // 合并样本指纹
                        if (category.sampleFingerprints != null)
                        {
                            foreach (var fp in category.sampleFingerprints)
                            {
                                existing.sampleFingerprints.Add(fp);
                            }
                            // 重新计算平均指纹
                            existing.avgFingerprint = ImageFingerprint.ComputeAverage(existing.sampleFingerprints);
                            existing.sampleCount = existing.sampleFingerprints.Count;
                            mergedCount++;
                        }
                    }
                    else
                    {
                        // 添加新分类
                        config.learnedCategories.Add(category);
                        addedCount++;
                    }
                }

                SaveConfig();

                EditorUtility.DisplayDialog("导入成功", 
                    $"导入完成!\n" +
                    $"新增分类: {addedCount}\n" +
                    $"合并分类: {mergedCount}\n" +
                    $"当前总计: {config.learnedCategories.Count} 个分类", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("导入失败", $"导入失败: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 学习数据导出格式
        /// </summary>
        [Serializable]
        private class LearnedDataExport
        {
            public string version;
            public string exportTime;
            public List<LearnedCategory> categories;
        }

        /// <summary>
        /// 绘制扫描设置
        /// </summary>
        private void DrawScanSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("分类设置", headerStyle);

            // 目标文件夹
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标文件夹", GUILayout.Width(70));
            scanFolder = EditorGUILayout.TextField(scanFolder);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择目标文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    string dataPath = Application.dataPath;
                    if (path.StartsWith(dataPath))
                    {
                        scanFolder = "Assets" + path.Substring(dataPath.Length);
                    }
                    else
                    {
                        scanFolder = path;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // 包含子文件夹
            includeSubfolders = EditorGUILayout.Toggle("包含子文件夹", includeSubfolders);

            // 未匹配文件目标文件夹
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("未匹配文件夹", GUILayout.Width(70));
            unmatchedFolder = EditorGUILayout.TextField(unmatchedFolder);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择未匹配文件存放文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    string dataPath = Application.dataPath;
                    if (path.StartsWith(dataPath))
                    {
                        unmatchedFolder = "Assets" + path.Substring(dataPath.Length);
                    }
                    else
                    {
                        unmatchedFolder = path;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // 相似度阈值（扩展范围）
            similarityThreshold = EditorGUILayout.Slider("相似度阈值", similarityThreshold, 0.3f, 1.0f);

            // 调试日志开关
            // Debug log toggle
            enableDebugLog = EditorGUILayout.Toggle("调试日志", enableDebugLog);

            // 匹配模式选择（使用miniButton风格）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("匹配模式", GUILayout.Width(60));
            
            if (GUILayout.Toggle(matchMode == MatchMode.Preview, "预览模式", EditorStyles.miniButtonLeft))
            {
                matchMode = MatchMode.Preview;
            }
            if (GUILayout.Toggle(matchMode == MatchMode.Execute, "直接执行", EditorStyles.miniButtonRight))
            {
                matchMode = MatchMode.Execute;
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 匹配策略选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("匹配策略", GUILayout.Width(60));
            
            var prevStrategy = matchStrategy;
            if (GUILayout.Toggle(matchStrategy == MatchStrategy.KNN, "KNN投票", EditorStyles.miniButtonLeft))
            {
                matchStrategy = MatchStrategy.KNN;
            }
            if (GUILayout.Toggle(matchStrategy == MatchStrategy.Average, "平均值", EditorStyles.miniButtonRight))
            {
                matchStrategy = MatchStrategy.Average;
            }
            // 切换策略后需要 ExitGUI，避免 Layout/Repaint 事件之间控件数量不一致
            // ExitGUI after strategy switch to prevent control count mismatch between Layout/Repaint events
            if (matchStrategy != prevStrategy)
            {
                GUIUtility.ExitGUI();
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // KNN参数
            if (matchStrategy == MatchStrategy.KNN)
            {
                EditorGUI.indentLevel++;
                knnK = EditorGUILayout.IntSlider("K值", knnK, 1, 10);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制操作按钮
        /// </summary>
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button("智能分类", GUILayout.Height(30)))
            {
                PerformAIClassification();
                GUIUtility.ExitGUI();
                return;
            }
            GUI.backgroundColor = Color.white;

            GUI.enabled = classifyResults.Count > 0 && matchMode == MatchMode.Preview;

            GUI.backgroundColor = new Color(1f, 0.8f, 0.6f);
            if (GUILayout.Button("执行归档", GUILayout.Height(30)))
            {
                ExecuteArchive();
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;

            GUI.enabled = true;

            if (GUILayout.Button("清空结果", GUILayout.Height(30)))
            {
                classifyResults.Clear();
                GUIUtility.ExitGUI();
                return;
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制结果列表
        /// </summary>
        private void DrawResults()
        {
            if (classifyResults.Count == 0)
            {
                EditorGUILayout.HelpBox("点击\"智能分类\"按钮开始分类", MessageType.Info);
                return;
            }

            var prevShowResults = showResults;
            showResults = EditorGUILayout.Foldout(showResults, $"分类结果 ({classifyResults.Count})", true);
            
            // Foldout 切换后需要 ExitGUI，避免 Layout/Repaint 事件之间控件数量不一致
            // ExitGUI after foldout toggle to prevent control count mismatch between Layout/Repaint events
            if (showResults != prevShowResults)
            {
                GUIUtility.ExitGUI();
                return;
            }

            if (!showResults) return;

            // 统计信息
            int matchedCount = classifyResults.Count(r => r.isMatched);
            int unmatchedCount = classifyResults.Count - matchedCount;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"匹配成功: {matchedCount} | 未匹配: {unmatchedCount}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 结果列表
            resultScrollPosition = EditorGUILayout.BeginScrollView(resultScrollPosition, GUILayout.Height(150));

            foreach (var result in classifyResults)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // 状态图标
                EditorGUILayout.LabelField(result.isMatched ? "✓" : "✗", 
                    result.isMatched ? matchedStyle : unmatchedStyle, 
                    GUILayout.Width(20));

                // 文件名
                EditorGUILayout.LabelField(result.fileName, GUILayout.Width(150));

                // 匹配的分类
                if (result.isMatched)
                {
                    EditorGUILayout.LabelField($"→ [{result.matchedCategory}]", GUILayout.Width(120));
                    EditorGUILayout.LabelField($"相似度: {result.similarity:F2}", EditorStyles.miniLabel, GUILayout.Width(80));
                }
                else
                {
                    EditorGUILayout.LabelField("未匹配", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region 智能学习（画面指纹模式）

        // 标准化采样尺寸（用于指纹提取时的中间处理）
        // Standard sampling size for intermediate processing during fingerprint extraction
        private const int STANDARD_SIZE = 64;

        /// <summary>
        /// 提取图片画面指纹（A+C方案v3.1：73维特征向量）
        /// Extract image visual fingerprint (A+C scheme v3.1: 73-dim feature vector)
        /// 
        /// 将图片缩放到32×32，计算73维特征向量：
        /// [0-3]   全局统计：不透明度/亮度/饱和度/紧凑度
        /// [4-19]  4×4空间亮度分布
        /// [20-35] 4×4空间Alpha分布
        /// [36-43] 颜色直方图（8bin色相）
        /// [44-51] Alpha形状描述子（极坐标8方向）
        /// [52-56] 网格结构特征
        /// [57-62] 径向特征（梯度/对称性/4环带亮度）
        /// [63-66] 边缘连续性（水平/垂直/左右Alpha衰减）
        /// [67-68] 纹理频率（局部方差/全局方差）
        /// [69-72] Glow区分特征（锐度/角度集中度/衰减平滑度/边界清晰度）
        /// </summary>
        private ImageFingerprint ExtractFingerprint(Texture2D texture)
        {
            if (texture == null) return null;

            try
            {
                // 获取可读纹理
                var readableTexture = GetReadableTexture(texture);
                if (readableTexture == null) return null;

                // 缩放到指纹尺寸
                var thumbTexture = ScaleTexture(readableTexture, FINGERPRINT_SIZE, FINGERPRINT_SIZE);
                Color[] pixels = thumbTexture.GetPixels();

                // 清理临时纹理
                if (thumbTexture != readableTexture)
                {
                    UnityEngine.Object.DestroyImmediate(thumbTexture);
                }

                int size = FINGERPRINT_SIZE * FINGERPRINT_SIZE;
                var fp = new ImageFingerprint();

                // ============================================
                // 第一步：逐像素提取基础通道（临时数组，不存储）
                // Step 1: Extract per-pixel channels (temp arrays, not stored)
                // ============================================
                float[] lumArr = new float[size];
                float[] alphaArr = new float[size];
                float[] hueArr = new float[size];
                float[] satArr = new float[size];

                float totalAlpha = 0f, totalBright = 0f, totalSat = 0f;
                int nonTransparent = 0;

                for (int i = 0; i < size; i++)
                {
                    Color px = pixels[i];
                    alphaArr[i] = px.a;
                    totalAlpha += px.a;

                    float brightness = px.r * 0.299f + px.g * 0.587f + px.b * 0.114f;
                    lumArr[i] = brightness * px.a; // Alpha预乘

                    float h, s, v;
                    Color.RGBToHSV(px, out h, out s, out v);
                    hueArr[i] = h;
                    satArr[i] = s;

                    if (px.a >= 0.1f)
                    {
                        totalBright += brightness;
                        totalSat += s;
                        nonTransparent++;
                    }
                }

                // 全局统计（同时存到原始字段 + 特征向量）
                // Global statistics (stored in both raw fields and feature vector)
                fp.overallAlpha = totalAlpha / size;
                fp.compactness = nonTransparent / (float)size;
                fp.avgBrightness = nonTransparent > 0 ? totalBright / nonTransparent : 0f;
                fp.avgSaturation = nonTransparent > 0 ? totalSat / nonTransparent : 0f;

                int idx = 0;

                // [0-3] 全局统计特征
                // [0-3] Global statistical features
                fp.featureVector[idx++] = fp.overallAlpha;
                fp.featureVector[idx++] = fp.avgBrightness;
                fp.featureVector[idx++] = fp.avgSaturation;
                fp.featureVector[idx++] = fp.compactness;

                // ============================================
                // 第二步：4×4 空间亮度分布
                // Step 2: 4×4 spatial brightness distribution
                // ============================================
                // [4-19] 把 32×32 分成 4×4=16 个区域
                int gridN = 4;
                int cellW = FINGERPRINT_SIZE / gridN;
                int cellH = FINGERPRINT_SIZE / gridN;

                for (int cy = 0; cy < gridN; cy++)
                {
                    for (int cx = 0; cx < gridN; cx++)
                    {
                        float sum = 0f;
                        int count = 0;
                        int startX = cx * cellW;
                        int startY = cy * cellH;
                        int endX = (cx == gridN - 1) ? FINGERPRINT_SIZE : startX + cellW;
                        int endY = (cy == gridN - 1) ? FINGERPRINT_SIZE : startY + cellH;

                        for (int y = startY; y < endY; y++)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                sum += lumArr[y * FINGERPRINT_SIZE + x];
                                count++;
                            }
                        }
                        fp.featureVector[idx++] = count > 0 ? sum / count : 0f;
                    }
                }

                // ============================================
                // 第三步：4×4 空间 Alpha 分布
                // Step 3: 4×4 spatial alpha distribution
                // ============================================
                // [20-35]
                for (int cy = 0; cy < gridN; cy++)
                {
                    for (int cx = 0; cx < gridN; cx++)
                    {
                        float sum = 0f;
                        int count = 0;
                        int startX = cx * cellW;
                        int startY = cy * cellH;
                        int endX = (cx == gridN - 1) ? FINGERPRINT_SIZE : startX + cellW;
                        int endY = (cy == gridN - 1) ? FINGERPRINT_SIZE : startY + cellH;

                        for (int y = startY; y < endY; y++)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                sum += alphaArr[y * FINGERPRINT_SIZE + x];
                                count++;
                            }
                        }
                        fp.featureVector[idx++] = count > 0 ? sum / count : 0f;
                    }
                }

                // ============================================
                // 第四步：颜色直方图（8 bin 色相）
                // Step 4: Color histogram (8-bin hue)
                // ============================================
                // [36-43] 仅统计非透明且有一定饱和度的像素
                float[] hueHist = new float[8];
                int huePixelCount = 0;

                for (int i = 0; i < size; i++)
                {
                    if (alphaArr[i] >= 0.1f && satArr[i] >= 0.1f)
                    {
                        int bin = Mathf.Clamp(Mathf.FloorToInt(hueArr[i] * 8f), 0, 7);
                        hueHist[bin] += 1f;
                        huePixelCount++;
                    }
                }
                // 归一化为占比
                // Normalize to proportions
                for (int b = 0; b < 8; b++)
                {
                    fp.featureVector[idx++] = huePixelCount > 0 ? hueHist[b] / huePixelCount : 0f;
                }

                // ============================================
                // 第五步：Alpha 形状描述子（极坐标 8 方向）
                // Step 5: Alpha shape descriptor (polar 8-direction)
                // ============================================
                // [44-51] 以 alpha 质量重心为原点，将 360° 分成 8 个扇区
                // 计算每个扇区的 alpha 质量占比

                // 先算 alpha 质量重心
                // Calculate alpha mass centroid
                float cxSum = 0f, cySum = 0f, massSum = 0f;
                for (int y = 0; y < FINGERPRINT_SIZE; y++)
                {
                    for (int x = 0; x < FINGERPRINT_SIZE; x++)
                    {
                        float a = alphaArr[y * FINGERPRINT_SIZE + x];
                        cxSum += x * a;
                        cySum += y * a;
                        massSum += a;
                    }
                }

                float centerX = massSum > 0f ? cxSum / massSum : FINGERPRINT_SIZE * 0.5f;
                float centerY = massSum > 0f ? cySum / massSum : FINGERPRINT_SIZE * 0.5f;

                float[] dirHist = new float[8];
                float dirTotal = 0f;

                for (int y = 0; y < FINGERPRINT_SIZE; y++)
                {
                    for (int x = 0; x < FINGERPRINT_SIZE; x++)
                    {
                        float a = alphaArr[y * FINGERPRINT_SIZE + x];
                        if (a < 0.05f) continue;

                        float dx = x - centerX;
                        float dy = y - centerY;
                        float angle = Mathf.Atan2(dy, dx); // -π ~ π
                        if (angle < 0) angle += 2f * Mathf.PI;

                        int dir = Mathf.Clamp(Mathf.FloorToInt(angle / (2f * Mathf.PI) * 8f), 0, 7);
                        dirHist[dir] += a;
                        dirTotal += a;
                    }
                }
                for (int d = 0; d < 8; d++)
                {
                    fp.featureVector[idx++] = dirTotal > 0f ? dirHist[d] / dirTotal : 0.125f;
                }

                // ============================================
                // 第六步：网格结构检测（复用现有代码）
                // Step 6: Grid structure detection (reuse existing code)
                // ============================================
                DetectSpriteSheetGrid(readableTexture, fp);

                // [52-56] 网格特征写入向量
                // [52-56] Grid features into vector
                fp.featureVector[idx++] = fp.estimatedGridCount > 0
                    ? Mathf.Log(fp.estimatedGridCount, 2f) / Mathf.Log(64f, 2f) : 0f;  // log归一化
                fp.featureVector[idx++] = fp.estimatedRows / 8f;   // 行数归一化
                fp.featureVector[idx++] = fp.estimatedCols / 8f;   // 列数归一化
                fp.featureVector[idx++] = fp.gridRegularity;        // 规律性 0~1
                fp.featureVector[idx++] = fp.estimatedGridCount > 1 ? 1f : 0f; // 序列帧标志

                // ============================================
                // 第七步：径向特征（6维）
                // Step 7: Radial features (6 dimensions)
                // ============================================
                // [57-62] 径向梯度/对称性/4环带亮度
                float halfSize = FINGERPRINT_SIZE * 0.5f;

                // B0: 径向梯度 = 中心区域亮度 / (中心+外围)
                // B0: Radial gradient = inner brightness / (inner + outer)
                float innerSum = 0f, outerSum = 0f;
                int innerCount = 0, outerCount = 0;

                // B2-B5: 4个环带的亮度累加
                // B2-B5: 4 ring band brightness accumulation
                float[] ringSum = new float[4];
                int[] ringCount = new int[4];

                // B1: 径向对称性 - 4个方向从中心向外的平均亮度
                // B1: Radial symmetry - average brightness along 4 directions from center
                float[] dirBrightness = new float[4]; // 上/下/左/右
                int[] dirCount = new int[4];

                for (int y = 0; y < FINGERPRINT_SIZE; y++)
                {
                    for (int x = 0; x < FINGERPRINT_SIZE; x++)
                    {
                        float dx = x - halfSize;
                        float dy = y - halfSize;
                        float r = Mathf.Sqrt(dx * dx + dy * dy) / halfSize; // 归一化半径 0~1.414
                        float lum = lumArr[y * FINGERPRINT_SIZE + x];

                        // 环带分类（按归一化半径分4段）
                        // Ring band classification (4 bands by normalized radius)
                        int band = Mathf.Clamp(Mathf.FloorToInt(r * 4f / 1.414f), 0, 3);
                        ringSum[band] += lum;
                        ringCount[band]++;

                        // 内外区域（r<0.5 = 内，r>=0.5 = 外）
                        // Inner/outer regions (r<0.5 = inner, r>=0.5 = outer)
                        if (r < 0.5f) { innerSum += lum; innerCount++; }
                        else { outerSum += lum; outerCount++; }
                    }
                }

                // B0: 径向梯度
                // B0: Radial gradient
                float innerAvg = innerCount > 0 ? innerSum / innerCount : 0f;
                float outerAvg = outerCount > 0 ? outerSum / outerCount : 0f;
                float radialGradient = (innerAvg + outerAvg) > 0.001f
                    ? innerAvg / (innerAvg + outerAvg) : 0.5f;
                fp.featureVector[idx++] = radialGradient;

                // B1: 径向对称性（上/下/左/右 4个方向从中心向外的亮度序列相似度）
                // B1: Radial symmetry (similarity of brightness profiles in 4 directions)
                int halfInt = (int)halfSize;
                // 上方向
                // Up direction
                for (int d = 1; d <= halfInt; d++)
                {
                    int px = halfInt; int py = halfInt - d;
                    if (py >= 0 && px < FINGERPRINT_SIZE) { dirBrightness[0] += lumArr[py * FINGERPRINT_SIZE + px]; dirCount[0]++; }
                }
                // 下方向
                // Down direction
                for (int d = 1; d <= halfInt; d++)
                {
                    int px = halfInt; int py = halfInt + d;
                    if (py < FINGERPRINT_SIZE && px < FINGERPRINT_SIZE) { dirBrightness[1] += lumArr[py * FINGERPRINT_SIZE + px]; dirCount[1]++; }
                }
                // 左方向
                // Left direction
                for (int d = 1; d <= halfInt; d++)
                {
                    int px = halfInt - d; int py = halfInt;
                    if (px >= 0 && py < FINGERPRINT_SIZE) { dirBrightness[2] += lumArr[py * FINGERPRINT_SIZE + px]; dirCount[2]++; }
                }
                // 右方向
                // Right direction
                for (int d = 1; d <= halfInt; d++)
                {
                    int px = halfInt + d; int py = halfInt;
                    if (px < FINGERPRINT_SIZE && py < FINGERPRINT_SIZE) { dirBrightness[3] += lumArr[py * FINGERPRINT_SIZE + px]; dirCount[3]++; }
                }

                float[] dirAvgs = new float[4];
                for (int d = 0; d < 4; d++)
                    dirAvgs[d] = dirCount[d] > 0 ? dirBrightness[d] / dirCount[d] : 0f;

                float dirMean = (dirAvgs[0] + dirAvgs[1] + dirAvgs[2] + dirAvgs[3]) / 4f;
                float dirVariance = 0f;
                for (int d = 0; d < 4; d++)
                {
                    float diff = dirAvgs[d] - dirMean;
                    dirVariance += diff * diff;
                }
                dirVariance /= 4f;
                float dirStd = Mathf.Sqrt(dirVariance);
                float radialSymmetry = dirMean > 0.001f ? Mathf.Clamp01(1f - dirStd / dirMean) : 0f;
                fp.featureVector[idx++] = radialSymmetry;

                // B2-B5: 4环带归一化亮度
                // B2-B5: 4 ring band normalized brightness
                for (int b = 0; b < 4; b++)
                {
                    fp.featureVector[idx++] = ringCount[b] > 0 ? ringSum[b] / ringCount[b] : 0f;
                }

                // ============================================
                // 第八步：边缘连续性（4维）
                // Step 8: Edge continuity (4 dimensions)
                // ============================================
                // [63-66] 水平/垂直连续性 + 左右Alpha衰减
                int edgeWidth = 2; // 取边缘2列/行

                // C0: 水平边缘连续性（左右边缘匹配度）
                // C0: Horizontal edge continuity (left-right edge matching)
                float hContinuity = 0f;
                int hCount = 0;
                for (int y = 0; y < FINGERPRINT_SIZE; y++)
                {
                    for (int e = 0; e < edgeWidth; e++)
                    {
                        int leftIdx = y * FINGERPRINT_SIZE + e;
                        int rightIdx = y * FINGERPRINT_SIZE + (FINGERPRINT_SIZE - 1 - e);
                        float lumDiff = Mathf.Abs(lumArr[leftIdx] - lumArr[rightIdx]);
                        float alphaDiff = Mathf.Abs(alphaArr[leftIdx] - alphaArr[rightIdx]);
                        hContinuity += 1f - (lumDiff + alphaDiff) * 0.5f;
                        hCount++;
                    }
                }
                fp.featureVector[idx++] = hCount > 0 ? hContinuity / hCount : 0f;

                // C1: 垂直边缘连续性（上下边缘匹配度）
                // C1: Vertical edge continuity (top-bottom edge matching)
                float vContinuity = 0f;
                int vCount = 0;
                for (int x = 0; x < FINGERPRINT_SIZE; x++)
                {
                    for (int e = 0; e < edgeWidth; e++)
                    {
                        int topIdx = e * FINGERPRINT_SIZE + x;
                        int bottomIdx = (FINGERPRINT_SIZE - 1 - e) * FINGERPRINT_SIZE + x;
                        float lumDiff = Mathf.Abs(lumArr[topIdx] - lumArr[bottomIdx]);
                        float alphaDiff = Mathf.Abs(alphaArr[topIdx] - alphaArr[bottomIdx]);
                        vContinuity += 1f - (lumDiff + alphaDiff) * 0.5f;
                        vCount++;
                    }
                }
                fp.featureVector[idx++] = vCount > 0 ? vContinuity / vCount : 0f;

                // C2: 左边缘Alpha衰减比（左边缘Alpha / 中心Alpha）
                // C2: Left edge alpha fade ratio
                // C3: 右边缘Alpha衰减比（右边缘Alpha / 中心Alpha）
                // C3: Right edge alpha fade ratio
                float leftEdgeAlpha = 0f, rightEdgeAlpha = 0f, centerAlphaSum = 0f;
                int leCount = 0, reCount = 0, ceCount = 0;
                int centerStart = FINGERPRINT_SIZE / 4;
                int centerEnd = FINGERPRINT_SIZE * 3 / 4;

                for (int y = 0; y < FINGERPRINT_SIZE; y++)
                {
                    for (int x = 0; x < edgeWidth; x++)
                    {
                        leftEdgeAlpha += alphaArr[y * FINGERPRINT_SIZE + x];
                        leCount++;
                    }
                    for (int x = FINGERPRINT_SIZE - edgeWidth; x < FINGERPRINT_SIZE; x++)
                    {
                        rightEdgeAlpha += alphaArr[y * FINGERPRINT_SIZE + x];
                        reCount++;
                    }
                    for (int x = centerStart; x < centerEnd; x++)
                    {
                        centerAlphaSum += alphaArr[y * FINGERPRINT_SIZE + x];
                        ceCount++;
                    }
                }

                float leftAvgA = leCount > 0 ? leftEdgeAlpha / leCount : 0f;
                float rightAvgA = reCount > 0 ? rightEdgeAlpha / reCount : 0f;
                float centerAvgA = ceCount > 0 ? centerAlphaSum / ceCount : 0.001f;

                fp.featureVector[idx++] = Mathf.Clamp01(leftAvgA / Mathf.Max(centerAvgA, 0.001f));
                fp.featureVector[idx++] = Mathf.Clamp01(rightAvgA / Mathf.Max(centerAvgA, 0.001f));

                // ============================================
                // 第九步：纹理频率特征（2维）
                // Step 9: Texture frequency features (2 dimensions)
                // ============================================
                // [67-68] 局部方差 + 全局方差

                // D0: 局部亮度方差（8×8小格子内部方差的均值）
                // D0: Local brightness variance (mean of 8×8 cell variances)
                int localGridN = 8;
                int lcW = FINGERPRINT_SIZE / localGridN;
                int lcH = FINGERPRINT_SIZE / localGridN;
                float totalLocalVar = 0f;
                int localCellCount = 0;

                for (int cy = 0; cy < localGridN; cy++)
                {
                    for (int cx = 0; cx < localGridN; cx++)
                    {
                        float cellSum = 0f;
                        float cellSumSq = 0f;
                        int cellCount = 0;
                        int sX = cx * lcW;
                        int sY = cy * lcH;

                        for (int y = sY; y < sY + lcH && y < FINGERPRINT_SIZE; y++)
                        {
                            for (int x = sX; x < sX + lcW && x < FINGERPRINT_SIZE; x++)
                            {
                                float lum = lumArr[y * FINGERPRINT_SIZE + x];
                                cellSum += lum;
                                cellSumSq += lum * lum;
                                cellCount++;
                            }
                        }

                        if (cellCount > 1)
                        {
                            float mean = cellSum / cellCount;
                            float variance = (cellSumSq / cellCount) - (mean * mean);
                            totalLocalVar += Mathf.Max(0f, variance);
                            localCellCount++;
                        }
                    }
                }
                fp.featureVector[idx++] = localCellCount > 0 ? totalLocalVar / localCellCount : 0f;

                // D1: 全局亮度方差
                // D1: Global brightness variance
                float globalSum = 0f, globalSumSq = 0f;
                for (int i = 0; i < size; i++)
                {
                    globalSum += lumArr[i];
                    globalSumSq += lumArr[i] * lumArr[i];
                }
                float globalMean = globalSum / size;
                float globalVariance = (globalSumSq / size) - (globalMean * globalMean);
                fp.featureVector[idx++] = Mathf.Max(0f, globalVariance);

                // ============================================
                // 第十步：Glow区分特征（4维）
                // Step 10: Glow discrimination features (4 dimensions)
                // ============================================
                // [69-72] 边缘锐度/角度集中度/径向衰减平滑度/Alpha边界清晰度

                // E0: 边缘锐度（Sobel梯度幅值均值）
                // E0: Edge sharpness (mean Sobel gradient magnitude)
                // Glow: 极低（柔和渐变），Star/Object: 高（尖锐边缘）
                float sobelSum = 0f;
                int sobelCount = 0;
                for (int y = 1; y < FINGERPRINT_SIZE - 1; y++)
                {
                    for (int x = 1; x < FINGERPRINT_SIZE - 1; x++)
                    {
                        // Sobel X 方向
                        // Sobel X direction
                        float gx = -lumArr[(y - 1) * FINGERPRINT_SIZE + (x - 1)]
                                   + lumArr[(y - 1) * FINGERPRINT_SIZE + (x + 1)]
                                   - 2f * lumArr[y * FINGERPRINT_SIZE + (x - 1)]
                                   + 2f * lumArr[y * FINGERPRINT_SIZE + (x + 1)]
                                   - lumArr[(y + 1) * FINGERPRINT_SIZE + (x - 1)]
                                   + lumArr[(y + 1) * FINGERPRINT_SIZE + (x + 1)];

                        // Sobel Y 方向
                        // Sobel Y direction
                        float gy = -lumArr[(y - 1) * FINGERPRINT_SIZE + (x - 1)]
                                   - 2f * lumArr[(y - 1) * FINGERPRINT_SIZE + x]
                                   - lumArr[(y - 1) * FINGERPRINT_SIZE + (x + 1)]
                                   + lumArr[(y + 1) * FINGERPRINT_SIZE + (x - 1)]
                                   + 2f * lumArr[(y + 1) * FINGERPRINT_SIZE + x]
                                   + lumArr[(y + 1) * FINGERPRINT_SIZE + (x + 1)];

                        float magnitude = Mathf.Sqrt(gx * gx + gy * gy);
                        sobelSum += magnitude;
                        sobelCount++;
                    }
                }
                // 归一化到 0~1 范围（Sobel最大值约 4.0）
                // Normalize to 0~1 range (Sobel max ~4.0)
                fp.featureVector[idx++] = sobelCount > 0 ? Mathf.Clamp01(sobelSum / sobelCount / 2f) : 0f;

                // E1: 角度能量集中度（8方向能量分布的归一化熵）
                // E1: Angular energy concentration (normalized entropy of 8-direction energy)
                // Glow: 均匀分布（高熵→值接近1），Star: 集中在少数方向（低熵→值接近0）
                // 复用之前计算的 dirHist（Alpha形状描述子的8方向分布）
                float entropy = 0f;
                for (int d = 0; d < 8; d++)
                {
                    float p = dirTotal > 0f ? dirHist[d] / dirTotal : 0.125f;
                    if (p > 1e-6f)
                    {
                        entropy -= p * Mathf.Log(p) / Mathf.Log(2f);
                    }
                }
                // 归一化熵：最大熵 = log2(8) = 3，归一化到 0~1
                // Normalized entropy: max = log2(8) = 3, normalize to 0~1
                fp.featureVector[idx++] = entropy / 3f;

                // E2: 径向衰减平滑度（从中心向外的亮度曲线单调递减程度）
                // E2: Radial decay smoothness (monotonic decrease of brightness from center)
                // Glow: 高（中心亮→边缘暗，平滑递减），Ring/Star: 低（不规则）
                int numRings = 8;
                float[] ringAvgLum = new float[numRings];
                int[] ringPixCount = new int[numRings];
                float maxRadius = halfSize * 1.414f; // 对角线半径

                for (int y = 0; y < FINGERPRINT_SIZE; y++)
                {
                    for (int x = 0; x < FINGERPRINT_SIZE; x++)
                    {
                        float rdx = x - halfSize;
                        float rdy = y - halfSize;
                        float dist = Mathf.Sqrt(rdx * rdx + rdy * rdy);
                        int ringIdx = Mathf.Clamp(Mathf.FloorToInt(dist / maxRadius * numRings), 0, numRings - 1);
                        ringAvgLum[ringIdx] += lumArr[y * FINGERPRINT_SIZE + x];
                        ringPixCount[ringIdx]++;
                    }
                }

                // 计算归一化环带亮度
                // Calculate normalized ring brightness
                for (int r = 0; r < numRings; r++)
                {
                    ringAvgLum[r] = ringPixCount[r] > 0 ? ringAvgLum[r] / ringPixCount[r] : 0f;
                }

                // 统计相邻环带中亮度递减的比例
                // Count proportion of adjacent rings with decreasing brightness
                int decreasingPairs = 0;
                int totalPairs = 0;
                float totalSmoothness = 0f;
                for (int r = 0; r < numRings - 1; r++)
                {
                    if (ringAvgLum[r] > 0.01f || ringAvgLum[r + 1] > 0.01f)
                    {
                        totalPairs++;
                        if (ringAvgLum[r] >= ringAvgLum[r + 1] - 0.01f) // 允许微小抖动
                        {
                            decreasingPairs++;
                        }
                        // 相邻环带亮度差的平滑度（差异越小越平滑）
                        // Smoothness of adjacent ring brightness difference
                        float diff = Mathf.Abs(ringAvgLum[r] - ringAvgLum[r + 1]);
                        totalSmoothness += 1f - Mathf.Clamp01(diff * 5f); // 差>0.2视为不平滑
                    }
                }
                float decayScore = totalPairs > 0 ? (float)decreasingPairs / totalPairs : 0f;
                float smoothScore = totalPairs > 0 ? totalSmoothness / totalPairs : 0f;
                fp.featureVector[idx++] = decayScore * 0.5f + smoothScore * 0.5f;

                // E3: Alpha边界清晰度（Alpha从不透明→透明的过渡宽度）
                // E3: Alpha boundary sharpness (transition width from opaque to transparent)
                // Glow: 低（宽渐变带），Object/Star: 高（窄硬边）
                // 沿8个径向方向从中心向外采样，统计Alpha从>0.5降到<0.1的像素距离
                // Sample along 8 radial directions from center, measure pixel distance for alpha 0.5→0.1 transition
                float totalTransitionWidth = 0f;
                int transitionCount = 0;
                float[] radialDirs = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

                foreach (float angleDeg in radialDirs)
                {
                    float rad = angleDeg * Mathf.Deg2Rad;
                    float cosA = Mathf.Cos(rad);
                    float sinA = Mathf.Sin(rad);

                    int startPixel = -1; // Alpha首次降到<0.5的位置
                    int endPixel = -1;   // Alpha首次降到<0.1的位置

                    for (int step = 0; step < halfInt; step++)
                    {
                        int sx = Mathf.RoundToInt(halfSize + cosA * step);
                        int sy = Mathf.RoundToInt(halfSize + sinA * step);

                        if (sx < 0 || sx >= FINGERPRINT_SIZE || sy < 0 || sy >= FINGERPRINT_SIZE)
                            break;

                        float alpha = alphaArr[sy * FINGERPRINT_SIZE + sx];

                        if (startPixel < 0 && alpha < 0.5f)
                            startPixel = step;
                        if (endPixel < 0 && alpha < 0.1f)
                        {
                            endPixel = step;
                            break;
                        }
                    }

                    if (startPixel >= 0 && endPixel >= 0 && endPixel > startPixel)
                    {
                        // 过渡宽度归一化到 0~1（halfInt为最大可能宽度）
                        // Transition width normalized to 0~1
                        float width = (float)(endPixel - startPixel) / halfInt;
                        totalTransitionWidth += width;
                        transitionCount++;
                    }
                }

                // 反转：宽过渡=低值（Glow），窄过渡=高值（Star/Object）
                // Invert: wide transition = low (Glow), narrow transition = high (Star/Object)
                float avgTransition = transitionCount > 0 ? totalTransitionWidth / transitionCount : 0.5f;
                fp.featureVector[idx++] = Mathf.Clamp01(1f - avgTransition);

                // ============================================
                // 第十一步：语义区分特征（8维）- v4.0新增
                // Step 11: Semantic discrimination features (8 dimensions) - v4.0 new
                // ============================================
                // [73-80] 颜色温度/暖色比/冷色比/尖角度/活跃方向数/颜色纯度/亮度集中度/形状规则度
                // 用于精准区分 Fire(暖色高温)/Light(中性光晕)/Star(尖角集中)/Shape(规则几何) 等

                // F0: 颜色温度（暖色=红橙黄 vs 冷色=蓝青绿）
                // F0: Color temperature (warm=red/orange/yellow vs cool=blue/cyan/green)
                // 色相: 0~0.11=红, 0.11~0.19=橙, 0.19~0.42=黄绿, 0.42~0.72=蓝青, 0.72~0.83=紫, 0.83~1=红
                float warmSum = 0f, coolSum = 0f;
                float warmPixCount = 0f, coolPixCount = 0f;
                float[] hueBins = new float[8]; // 用于后续计算主色调
                float totalColorPixels = 0f;

                for (int i = 0; i < size; i++)
                {
                    if (alphaArr[i] < 0.1f) continue;
                    float h = hueArr[i];
                    float s = satArr[i];
                    float weight = alphaArr[i] * s; // 饱和度加权，无色像素权重低

                    if (s < 0.08f) continue; // 跳过几乎无彩色的像素

                    totalColorPixels += weight;
                    int bin = Mathf.Clamp(Mathf.FloorToInt(h * 8f), 0, 7);
                    hueBins[bin] += weight;

                    // 暖色范围：红(0~0.11), 橙(0.11~0.19), 黄(0.19~0.22), 紫红(0.83~1.0)
                    // Warm range: red(0~0.11), orange(0.11~0.19), yellow(0.19~0.22), magenta(0.83~1.0)
                    if (h < 0.22f || h > 0.83f)
                    {
                        warmSum += weight;
                        warmPixCount += weight;
                    }
                    // 冷色范围：青绿(0.22~0.52), 蓝(0.52~0.72)
                    // Cool range: cyan-green(0.22~0.52), blue(0.52~0.72)
                    else if (h >= 0.22f && h <= 0.72f)
                    {
                        coolSum += weight;
                        coolPixCount += weight;
                    }
                }

                float totalWarmCool = warmSum + coolSum;
                float temperature = totalWarmCool > 0.001f ? warmSum / totalWarmCool : 0.5f;
                fp.colorTemperature = temperature;
                fp.featureVector[idx++] = temperature;

                // F1: 暖色占比（Fire/Spark特征: >0.7）
                // F1: Warm color ratio (Fire/Spark: >0.7)
                fp.warmColorRatio = totalColorPixels > 0.001f ? warmPixCount / totalColorPixels : 0f;
                fp.featureVector[idx++] = fp.warmColorRatio;

                // F2: 冷色占比（Ice/Crystal特征: >0.5）
                // F2: Cool color ratio (Ice/Crystal: >0.5)
                fp.coolColorRatio = totalColorPixels > 0.001f ? coolPixCount / totalColorPixels : 0f;
                fp.featureVector[idx++] = fp.coolColorRatio;

                // F3: Alpha形状尖角度（Star特征: 高尖角, Glow特征: 低尖角）
                // F3: Alpha shape spikiness (Star: high, Glow: low)
                // 计算径向Alpha剖面的方差 - 尖角形状的方差大，圆形/光晕方差小
                // Compute variance of radial alpha profile - spiky shapes have high variance, round/glow low
                float[] radialAlphaProfile = new float[16]; // 16个方向的径向Alpha积分
                float radialAlphaTotal = 0f;

                for (int dir = 0; dir < 16; dir++)
                {
                    float angle = dir * Mathf.PI * 2f / 16f;
                    float dirCos = Mathf.Cos(angle);
                    float dirSin = Mathf.Sin(angle);
                    float alphaIntegral = 0f;

                    for (int step = 1; step <= halfInt; step++)
                    {
                        int sx = Mathf.RoundToInt(centerX + dirCos * step);
                        int sy = Mathf.RoundToInt(centerY + dirSin * step);
                        if (sx >= 0 && sx < FINGERPRINT_SIZE && sy >= 0 && sy < FINGERPRINT_SIZE)
                        {
                            alphaIntegral += alphaArr[sy * FINGERPRINT_SIZE + sx];
                        }
                    }
                    radialAlphaProfile[dir] = alphaIntegral;
                    radialAlphaTotal += alphaIntegral;
                }

                // 归一化 + 计算方差
                float radialAlphaMean = radialAlphaTotal / 16f;
                float radialAlphaVar = 0f;
                for (int d = 0; d < 16; d++)
                {
                    float diff = radialAlphaProfile[d] - radialAlphaMean;
                    radialAlphaVar += diff * diff;
                }
                radialAlphaVar /= 16f;
                // 归一化到 0~1 范围（除以均值的平方，得到变异系数的平方）
                float spikiness = radialAlphaMean > 0.001f
                    ? Mathf.Clamp01(radialAlphaVar / (radialAlphaMean * radialAlphaMean))
                    : 0f;
                fp.featureVector[idx++] = spikiness;

                // F4: 活跃方向数（Shape特征: 2~4, Star特征: 4~8+）
                // F4: Active direction count (Shape: 2~4, Star: 4~8+)
                // 统计径向Alpha明显大于均值的方向数
                // Count directions with radial alpha significantly above mean
                int activeDirections = 0;
                float activeThreshold = radialAlphaMean * 1.2f; // 高于均值20%算活跃
                for (int d = 0; d < 16; d++)
                {
                    if (radialAlphaProfile[d] > activeThreshold)
                        activeDirections++;
                }
                fp.featureVector[idx++] = activeDirections / 16f; // 归一化到 0~1

                // F5: 颜色纯度（高饱和度像素占比，区分彩色形状 vs 低饱和光效）
                // F5: Color purity (proportion of high-saturation pixels)
                int highSatPixels = 0;
                for (int i = 0; i < size; i++)
                {
                    if (alphaArr[i] >= 0.1f && satArr[i] >= 0.4f)
                        highSatPixels++;
                }
                fp.colorPurity = nonTransparent > 0 ? (float)highSatPixels / nonTransparent : 0f;
                fp.featureVector[idx++] = fp.colorPurity;

                // F6: 亮度空间集中度（中心亮 vs 均匀分布）
                // F6: Brightness spatial concentration (center-bright vs uniform)
                // Light/Glow特征: 高集中（中心亮边缘暗），Particle/Shape: 低集中（分散分布）
                float innerBrightSum = 0f, totalBrightSum = 0f;
                int innerBrightCount = 0, totalBrightCount = 0;
                float innerRadius = FINGERPRINT_SIZE * 0.3f; // 内30%区域

                for (int y = 0; y < FINGERPRINT_SIZE; y++)
                {
                    for (int x = 0; x < FINGERPRINT_SIZE; x++)
                    {
                        if (alphaArr[y * FINGERPRINT_SIZE + x] < 0.05f) continue;
                        float lum = lumArr[y * FINGERPRINT_SIZE + x];
                        float dx2 = x - halfSize;
                        float dy2 = y - halfSize;
                        float dist = Mathf.Sqrt(dx2 * dx2 + dy2 * dy2);

                        totalBrightSum += lum;
                        totalBrightCount++;

                        if (dist < innerRadius)
                        {
                            innerBrightSum += lum;
                            innerBrightCount++;
                        }
                    }
                }

                float innerBrightAvg = innerBrightCount > 0 ? innerBrightSum / innerBrightCount : 0f;
                float totalBrightAvg = totalBrightCount > 0 ? totalBrightSum / totalBrightCount : 0.001f;
                float brightConcentration = Mathf.Clamp01(innerBrightAvg / Mathf.Max(totalBrightAvg, 0.001f));
                fp.featureVector[idx++] = brightConcentration;

                // F7: 形状规则度（对称性 × 边界锐度，区分几何Shape vs 有机Fire/Smoke）
                // F7: Shape regularity (symmetry × boundary sharpness, distinguish geometric Shape vs organic Fire/Smoke)
                // 计算4折对称度：比较上下/左右镜像的Alpha相似度
                float symmetryScore = 0f;
                int symCount = 0;

                for (int y = 0; y < FINGERPRINT_SIZE / 2; y++)
                {
                    for (int x = 0; x < FINGERPRINT_SIZE; x++)
                    {
                        int mirrorY = FINGERPRINT_SIZE - 1 - y;
                        float a1 = alphaArr[y * FINGERPRINT_SIZE + x];
                        float a2 = alphaArr[mirrorY * FINGERPRINT_SIZE + x];
                        symmetryScore += 1f - Mathf.Abs(a1 - a2);
                        symCount++;
                    }
                }
                for (int y = 0; y < FINGERPRINT_SIZE; y++)
                {
                    for (int x = 0; x < FINGERPRINT_SIZE / 2; x++)
                    {
                        int mirrorX = FINGERPRINT_SIZE - 1 - x;
                        float a1 = alphaArr[y * FINGERPRINT_SIZE + x];
                        float a2 = alphaArr[y * FINGERPRINT_SIZE + mirrorX];
                        symmetryScore += 1f - Mathf.Abs(a1 - a2);
                        symCount++;
                    }
                }

                float avgSymmetry = symCount > 0 ? symmetryScore / symCount : 0.5f;
                // 结合边界锐度（之前计算的 Sobel 锐度在 fp.featureVector[idx - 13]）
                // Combine with boundary sharpness (Sobel sharpness computed earlier)
                float sobelSharpness = fp.featureVector[idx - 13]; // E0: 边缘锐度位置
                float shapeRegularity = avgSymmetry * 0.6f + sobelSharpness * 0.4f;
                fp.featureVector[idx++] = Mathf.Clamp01(shapeRegularity);

                // 记录主色调
                // Record dominant hue
                float maxHueBin = 0f;
                int maxHueBinIdx = 0;
                for (int b = 0; b < 8; b++)
                {
                    if (hueBins[b] > maxHueBin)
                    {
                        maxHueBin = hueBins[b];
                        maxHueBinIdx = b;
                    }
                }
                fp.dominantHue = (maxHueBinIdx + 0.5f) / 8f; // 映射回 0~1 色相范围

                return fp;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"指纹提取失败: {texture.name} - {e.Message}");
                return null;
            }
        }

        // 网格检测用的采样尺寸（比指纹更高分辨率，保留网格间隔信息）
        // Grid detection sampling size (higher resolution than fingerprint to preserve grid gap info)
        private const int GRID_DETECT_SIZE = 64;

        // Alpha阈值：低于此值视为透明
        // Alpha threshold: below this value is considered transparent
        private const float ALPHA_THRESHOLD = 0.05f;

        /// <summary>
        /// 检测图片是否为序列帧网格排列
        /// Detect if image is a sprite sheet with grid layout
        /// 
        /// 使用两种互补的方法：
        /// 方法A：Alpha投影法 - 沿X/Y轴投影Alpha通道，找周期性低谷
        /// 方法B：连通区域计数 - 统计独立的不透明区域数量
        /// </summary>
        private void DetectSpriteSheetGrid(Texture2D texture, ImageFingerprint fp)
        {
            if (texture == null || fp == null) return;

            try
            {
                // 缩放到检测分辨率
                var detectTexture = ScaleTexture(texture, GRID_DETECT_SIZE, GRID_DETECT_SIZE);
                if (detectTexture == null) return;

                Color[] pixels = detectTexture.GetPixels();
                int w = GRID_DETECT_SIZE;
                int h = GRID_DETECT_SIZE;

                // 构建Alpha二值化矩阵
                // Build alpha binary matrix
                bool[,] alphaMap = new bool[w, h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        alphaMap[x, y] = pixels[y * w + x].a > ALPHA_THRESHOLD;
                    }
                }

                // 方法A：Alpha投影法检测网格行列数
                // Method A: Alpha projection to detect grid rows/columns
                int projRows, projCols;
                float projRegularity;
                DetectGridByProjection(pixels, w, h, out projRows, out projCols, out projRegularity);

                // 方法B：连通区域计数
                // Method B: Connected component counting
                int componentCount = CountConnectedComponents(alphaMap, w, h);

                // 综合两种方法的结果
                // Combine results from both methods
                int projGrid = projRows * projCols;

                // 选择更可靠的结果
                if (projGrid > 1 && projRegularity > 0.3f)
                {
                    // 投影法检测到规律网格 → 优先使用投影法结果
                    fp.estimatedRows = projRows;
                    fp.estimatedCols = projCols;
                    fp.estimatedGridCount = projGrid;
                    fp.gridRegularity = projRegularity;
                }
                else if (componentCount > 1)
                {
                    // 投影法不可靠，但连通区域发现多个独立区域
                    fp.estimatedGridCount = componentCount;
                    // 尝试推算行列数（假设接近正方形排列）
                    float sqrtCount = Mathf.Sqrt(componentCount);
                    fp.estimatedCols = Mathf.RoundToInt(sqrtCount);
                    fp.estimatedRows = Mathf.CeilToInt((float)componentCount / fp.estimatedCols);
                    fp.gridRegularity = projRegularity * 0.5f; // 规律性打折
                }
                else
                {
                    // 单图
                    fp.estimatedGridCount = 1;
                    fp.estimatedRows = 1;
                    fp.estimatedCols = 1;
                    fp.gridRegularity = 1f; // 单图默认完美规律
                }

                // 清理检测用纹理
                if (detectTexture != texture)
                {
                    UnityEngine.Object.DestroyImmediate(detectTexture);
                }
            }
            catch (Exception e)
            {
                // 网格检测失败不影响其他指纹
                Debug.LogWarning($"网格检测异常: {e.Message}");
                fp.estimatedGridCount = 1;
                fp.estimatedRows = 1;
                fp.estimatedCols = 1;
                fp.gridRegularity = 0f;
            }
        }

        /// <summary>
        /// Alpha投影法检测网格行列数
        /// Detect grid rows/columns by projecting Alpha channel along X/Y axes
        /// 
        /// 原理：将Alpha通道沿X/Y轴求和，序列帧图片的投影曲线会出现周期性低谷
        /// （透明间隔），通过检测低谷数量来推算行列数
        /// </summary>
        private void DetectGridByProjection(Color[] pixels, int w, int h,
            out int rows, out int cols, out float regularity)
        {
            rows = 1;
            cols = 1;
            regularity = 0f;

            // X轴投影（每列的Alpha总和）→ 检测列数
            // X-axis projection (sum of Alpha per column) → detect column count
            float[] xProjection = new float[w];
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int y = 0; y < h; y++)
                {
                    sum += pixels[y * w + x].a;
                }
                xProjection[x] = sum / h;
            }

            // Y轴投影（每行的Alpha总和）→ 检测行数
            // Y-axis projection (sum of Alpha per row) → detect row count
            float[] yProjection = new float[h];
            for (int y = 0; y < h; y++)
            {
                float sum = 0f;
                for (int x = 0; x < w; x++)
                {
                    sum += pixels[y * w + x].a;
                }
                yProjection[y] = sum / w;
            }

            // 在投影曲线中找低谷（透明间隔）
            // Find valleys (transparent gaps) in projection curves
            float xRegularity, yRegularity;
            cols = DetectPeriodicValleys(xProjection, out xRegularity);
            rows = DetectPeriodicValleys(yProjection, out yRegularity);

            // cols和rows是间隔数，实际块数 = 间隔数 + 1（但如果是1说明没检测到间隔）
            if (cols > 1) cols++;
            if (rows > 1) rows++;

            // 确保至少为1
            cols = Mathf.Max(1, cols);
            rows = Mathf.Max(1, rows);

            regularity = (xRegularity + yRegularity) * 0.5f;
        }

        /// <summary>
        /// 在投影曲线中检测周期性低谷
        /// Detect periodic valleys in projection curve
        /// 
        /// 返回值：检测到的间隔数（低谷数量）
        /// 如果没有检测到周期性低谷，返回1
        /// </summary>
        private int DetectPeriodicValleys(float[] projection, out float regularity)
        {
            regularity = 0f;
            int len = projection.Length;
            if (len < 4) return 1;

            // 计算整体平均Alpha
            float totalAvg = 0f;
            for (int i = 0; i < len; i++) totalAvg += projection[i];
            totalAvg /= len;

            // 如果整体Alpha太低，认为大部分都是透明，无法判断
            if (totalAvg < 0.05f) return 1;

            // 低谷阈值：整体平均值的30%以下视为低谷
            // Valley threshold: below 30% of overall average
            float valleyThreshold = totalAvg * 0.30f;

            // 跳过边缘区域（前后各10%），避免边缘透明区域的干扰
            // Skip edge regions (10% each side) to avoid edge transparency interference
            int margin = Mathf.Max(2, len / 10);

            // 找到所有低谷区域的中心位置
            // Find center positions of all valley regions
            List<int> valleyPositions = new List<int>();
            bool inValley = false;
            int valleyStart = 0;

            for (int i = margin; i < len - margin; i++)
            {
                if (projection[i] < valleyThreshold)
                {
                    if (!inValley)
                    {
                        inValley = true;
                        valleyStart = i;
                    }
                }
                else
                {
                    if (inValley)
                    {
                        inValley = false;
                        int valleyCenter = (valleyStart + i) / 2;
                        valleyPositions.Add(valleyCenter);
                    }
                }
            }

            // 如果结束时仍在低谷中
            if (inValley)
            {
                int valleyCenter = (valleyStart + len - margin) / 2;
                valleyPositions.Add(valleyCenter);
            }

            int valleyCount = valleyPositions.Count;
            if (valleyCount == 0) return 1;

            // 检测低谷间距的规律性（周期性）
            // Check regularity of valley spacing (periodicity)
            if (valleyCount >= 2)
            {
                List<int> gaps = new List<int>();
                for (int i = 1; i < valleyPositions.Count; i++)
                {
                    gaps.Add(valleyPositions[i] - valleyPositions[i - 1]);
                }

                // 计算间距的标准差
                float avgGap = 0f;
                foreach (int g in gaps) avgGap += g;
                avgGap /= gaps.Count;

                float variance = 0f;
                foreach (int g in gaps)
                {
                    float diff = g - avgGap;
                    variance += diff * diff;
                }
                variance /= gaps.Count;
                float stdDev = Mathf.Sqrt(variance);

                // 规律性 = 1 - (标准差 / 平均间距)，规律性越高间距越均匀
                regularity = avgGap > 0f ? Mathf.Clamp01(1f - stdDev / avgGap) : 0f;
            }
            else
            {
                // 只有一个低谷（可能是 2 列/行）
                regularity = 0.5f;
            }

            return valleyCount;
        }

        /// <summary>
        /// 连通区域计数（Flood Fill）
        /// Count connected components using Flood Fill
        /// 
        /// 统计Alpha大于阈值的独立连通区域数量
        /// 序列帧：多个独立区域（4/9/16...）
        /// 单图：通常只有1个连通区域
        /// </summary>
        private int CountConnectedComponents(bool[,] alphaMap, int w, int h)
        {
            bool[,] visited = new bool[w, h];
            int componentCount = 0;

            // 最小区域面积阈值（过滤噪点）
            // Minimum area threshold to filter noise
            int minArea = (w * h) / 200; // 至少占总面积的0.5%
            minArea = Mathf.Max(minArea, 4); // 最少4个像素

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (alphaMap[x, y] && !visited[x, y])
                    {
                        // BFS 洪水填充
                        int area = FloodFill(alphaMap, visited, x, y, w, h);
                        if (area >= minArea)
                        {
                            componentCount++;
                        }
                    }
                }
            }

            return componentCount;
        }

        /// <summary>
        /// BFS洪水填充，返回连通区域面积
        /// BFS Flood Fill, returns connected component area
        /// </summary>
        private int FloodFill(bool[,] alphaMap, bool[,] visited, int startX, int startY, int w, int h)
        {
            int area = 0;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;

            // 4邻域方向
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                area++;

                for (int d = 0; d < 4; d++)
                {
                    int nx = pos.x + dx[d];
                    int ny = pos.y + dy[d];

                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && !visited[nx, ny] && alphaMap[nx, ny])
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }

            return area;
        }

        /// <summary>
        /// 缩放纹理到指定尺寸
        /// Scale texture to target size
        /// </summary>
        private Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            if (source == null) return null;

            // 使用RenderTexture进行高质量缩放
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            
            // 设置双线性过滤以获得更好的缩放质量
            Graphics.Blit(source, rt);
            
            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            return result;
        }

        /// <summary>
        /// 执行学习（画面指纹模式）
        /// Perform learning (Visual Fingerprint Mode)
        /// </summary>
        private void PerformLearning()
        {
            // 过滤掉空路径
            var validFolders = selectedSubFolders.Where(f => !string.IsNullOrEmpty(f.path)).ToList();
            
            if (validFolders.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先添加或自动识别样本文件夹", "确定");
                return;
            }

            config.learnedCategories.Clear();

            EditorUtility.DisplayProgressBar("学习画面指纹", "正在学习...", 0);

            try
            {
                int totalFolders = validFolders.Count;
                int processedFolders = 0;

                foreach (var subFolder in validFolders)
                {
                    processedFolders++;
                    EditorUtility.DisplayProgressBar("学习画面指纹", 
                        $"正在学习: {subFolder.name} ({processedFolders}/{totalFolders})", 
                        (float)processedFolders / totalFolders);

                    // 创建分类
                    var category = new LearnedCategory
                    {
                        categoryName = subFolder.name,
                        targetFolder = subFolder.name,
                        sampleFingerprints = new List<ImageFingerprint>()
                    };

                    // 扫描文件夹中的图片 - 支持绝对路径和相对路径
                    string fullPath;
                    bool isAbsolutePath = Path.IsPathRooted(subFolder.path);
                    string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                    
                    if (isAbsolutePath)
                    {
                        fullPath = subFolder.path;
                    }
                    else
                    {
                        fullPath = Path.Combine(projectRoot, subFolder.path);
                    }
                    
                    if (!Directory.Exists(fullPath))
                    {
                        Debug.LogWarning($"文件夹不存在: {fullPath}");
                        continue;
                    }

                    var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga"
                    };

                    string[] files = Directory.GetFiles(fullPath, "*.*", SearchOption.TopDirectoryOnly);

                    foreach (string file in files)
                    {
                        if (file.EndsWith(".meta")) continue;

                        string extension = Path.GetExtension(file);
                        if (!imageExtensions.Contains(extension)) continue;

                        // 尝试从工程内加载，如果失败则从文件系统直接读取
                        Texture2D texture = null;
                        bool isExternalFile = false;

                        // 检查是否在工程内
                        if (file.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            string relativePath = file.Substring(projectRoot.Length).TrimStart('/', '\\').Replace("\\", "/");
                            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
                        }

                        // 如果工程内加载失败，尝试从文件系统直接读取
                        if (texture == null && File.Exists(file))
                        {
                            try
                            {
                                byte[] fileData = File.ReadAllBytes(file);
                                texture = new Texture2D(2, 2);
                                if (texture.LoadImage(fileData))
                                {
                                    isExternalFile = true;
                                }
                                else
                                {
                                    if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                                    texture = null;
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning($"读取图片失败: {file} - {e.Message}");
                            }
                        }

                        if (texture != null)
                        {
                            var fingerprint = ExtractFingerprint(texture);
                            if (fingerprint != null)
                            {
                                category.sampleFingerprints.Add(fingerprint);
                            }

                            // 如果是外部文件，用完后销毁
                            if (isExternalFile)
                            {
                                UnityEngine.Object.DestroyImmediate(texture);
                            }
                        }

                        // 限制每个分类最多学习2000个样本
                        // Limit max 2000 samples per category
                        if (category.sampleFingerprints.Count >= 2000) break;
                    }

                    category.sampleCount = category.sampleFingerprints.Count;

                    // 计算平均指纹
                    if (category.sampleFingerprints.Count > 0)
                    {
                        category.avgFingerprint = ImageFingerprint.ComputeAverage(category.sampleFingerprints);
                        config.learnedCategories.Add(category);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            SaveConfig();

            if (config.learnedCategories.Count > 0)
            {
                Debug.Log($"[画面指纹学习] 学习完成，共学习到 {config.learnedCategories.Count} 个分类");
                foreach (var cat in config.learnedCategories)
                {
                    Debug.Log($"  [{cat.categoryName}] {cat.sampleCount} 个样本  " +
                        $"不透明度={cat.avgFingerprint.overallAlpha:F2}  " +
                        $"亮度={cat.avgFingerprint.avgBrightness:F2}  " +
                        $"紧凑度={cat.avgFingerprint.compactness:F2}  " +
                        $"网格={cat.avgFingerprint.estimatedRows}×{cat.avgFingerprint.estimatedCols}" +
                        $"({cat.avgFingerprint.estimatedGridCount}子图)  " +
                        $"规律性={cat.avgFingerprint.gridRegularity:F2}  " +
                        $"色温={cat.avgFingerprint.colorTemperature:F2}  " +
                        $"暖色={cat.avgFingerprint.warmColorRatio:F2}  " +
                        $"冷色={cat.avgFingerprint.coolColorRatio:F2}  " +
                        $"纯度={cat.avgFingerprint.colorPurity:F2}");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("学习失败", "未能从样本中学习到有效指纹", "确定");
            }
        }

        /// <summary>
        /// 硬规则预过滤：根据确定性特征排除不可能的分类（方案A）
        /// Hard-rule pre-filter: exclude impossible categories based on deterministic features (Scheme A)
        /// 
        /// 规则设计原则：
        /// 1. 只排除"绝对不可能"的情况，宁可漏排不可错排
        /// 2. 每条规则都有大的容错空间
        /// 3. 安全兜底：过滤后候选集太小则回退到全集
        /// </summary>
        private List<LearnedCategory> ApplyHardRuleFilter(
            ImageFingerprint input, List<LearnedCategory> allCategories)
        {
            if (allCategories == null || allCategories.Count <= 2)
                return allCategories; // 分类太少不过滤

            var candidates = new List<LearnedCategory>();

            foreach (var category in allCategories)
            {
                if (ShouldKeepCandidate(input, category))
                {
                    candidates.Add(category);
                }
            }

            // 安全兜底：如果过滤后候选集太小，回退到全集
            // Safety fallback: if too few candidates remain, use all categories
            if (candidates.Count < 2)
            {
                if (enableDebugLog)
                    Debug.Log($"[硬规则] 过滤后仅剩{candidates.Count}个候选，回退到全集({allCategories.Count}个)");
                return allCategories;
            }

            if (enableDebugLog && candidates.Count < allCategories.Count)
            {
                var excluded = allCategories.Where(c => !candidates.Contains(c))
                    .Select(c => c.categoryName);
                Debug.Log($"[硬规则] 排除分类: {string.Join(", ", excluded)} " +
                    $"(剩余{candidates.Count}/{allCategories.Count})");
            }

            return candidates;
        }

        /// <summary>
        /// 判断某个分类是否应保留为候选
        /// Determine if a category should remain as candidate
        /// 
        /// 返回 true = 保留，false = 排除
        /// Return true = keep, false = exclude
        /// </summary>
        private bool ShouldKeepCandidate(ImageFingerprint input, LearnedCategory category)
        {
            if (category.sampleFingerprints == null || category.sampleFingerprints.Count == 0)
                return false;

            // 计算该分类样本的统计范围（min/max）
            // Calculate sample statistical ranges (min/max)
            float minGrid = float.MaxValue, maxGrid = float.MinValue;
            float minCompact = float.MaxValue, maxCompact = float.MinValue;
            float minAlpha = float.MaxValue, maxAlpha = float.MinValue;
            float minTemp = float.MaxValue, maxTemp = float.MinValue;
            float minWarm = float.MaxValue, maxWarm = float.MinValue;
            float minCool = float.MaxValue, maxCool = float.MinValue;

            foreach (var s in category.sampleFingerprints)
            {
                if (s.estimatedGridCount < minGrid) minGrid = s.estimatedGridCount;
                if (s.estimatedGridCount > maxGrid) maxGrid = s.estimatedGridCount;
                if (s.compactness < minCompact) minCompact = s.compactness;
                if (s.compactness > maxCompact) maxCompact = s.compactness;
                if (s.overallAlpha < minAlpha) minAlpha = s.overallAlpha;
                if (s.overallAlpha > maxAlpha) maxAlpha = s.overallAlpha;
                if (s.colorTemperature < minTemp) minTemp = s.colorTemperature;
                if (s.colorTemperature > maxTemp) maxTemp = s.colorTemperature;
                if (s.warmColorRatio < minWarm) minWarm = s.warmColorRatio;
                if (s.warmColorRatio > maxWarm) maxWarm = s.warmColorRatio;
                if (s.coolColorRatio < minCool) minCool = s.coolColorRatio;
                if (s.coolColorRatio > maxCool) maxCool = s.coolColorRatio;
            }

            // ─────────────────────────────────────────────
            // 规则1：序列帧 vs 单图的强区分
            // Rule 1: Sprite sheet vs single image strong distinction
            // ─────────────────────────────────────────────
            // 如果输入是明确的多子图（6+），但该分类所有样本都是单图 → 排除
            // 如果输入是明确的单图，但该分类所有样本都是多子图（6+）→ 排除
            bool inputIsMultiGrid = input.estimatedGridCount >= 6;
            bool inputIsSingle = input.estimatedGridCount <= 1;
            bool catAllSingle = maxGrid <= 1;
            bool catAllMulti = minGrid >= 6;

            if (inputIsMultiGrid && catAllSingle) return false;
            if (inputIsSingle && catAllMulti) return false;

            // ─────────────────────────────────────────────
            // 规则2：紧凑度极端差异
            // Rule 2: Extreme compactness mismatch
            // ─────────────────────────────────────────────
            // 容差 0.35（很宽松），只排除极端情况
            // Tolerance 0.35 (very loose), only exclude extreme cases
            float compactMargin = 0.35f;
            if (input.compactness > maxCompact + compactMargin ||
                input.compactness < minCompact - compactMargin)
                return false;

            // ─────────────────────────────────────────────
            // 规则3：整体不透明度极端差异
            // Rule 3: Extreme overall alpha mismatch
            // ─────────────────────────────────────────────
            float alphaMargin = 0.40f;
            if (input.overallAlpha > maxAlpha + alphaMargin ||
                input.overallAlpha < minAlpha - alphaMargin)
                return false;

            // ─────────────────────────────────────────────
            // 规则4：颜色温度极端差异（区分Fire暖色 vs Light/Star冷色）
            // Rule 4: Extreme color temperature mismatch (Fire warm vs Light/Star cool)
            // ─────────────────────────────────────────────
            // 容差 0.45（宽松），只排除极端情况：
            // 例如纯暖色Fire(温度>0.85)不可能匹配到全冷色分类(温度<0.3)
            float tempMargin = 0.45f;
            if (input.colorTemperature > maxTemp + tempMargin ||
                input.colorTemperature < minTemp - tempMargin)
                return false;

            // ─────────────────────────────────────────────
            // 规则5：暖冷色极端差异（区分Fire/Spark vs Ice/Crystal）
            // Rule 5: Extreme warm/cool ratio mismatch
            // ─────────────────────────────────────────────
            // 如果输入几乎全暖色(>0.8)，但分类几乎全冷色(max暖色<0.15) → 排除
            // 如果输入几乎全冷色(>0.7)，但分类几乎全暖色(max冷色<0.1) → 排除
            if (input.warmColorRatio > 0.8f && maxWarm < 0.15f) return false;
            if (input.coolColorRatio > 0.7f && maxCool < 0.10f) return false;

            // 通过所有规则 → 保留
            // Passed all rules → keep
            return true;
        }

        /// <summary>
        /// KNN加权投票匹配（A+C方案：硬规则预过滤 + 余弦相似度）
        /// KNN weighted voting (A+C scheme: hard-rule pre-filter + cosine similarity)
        /// 
        /// 改进：使用相似度作为投票权重，而非简单计数
        /// 相似度越高的样本投票权重越大，减少低质量匹配的干扰
        /// </summary>
        private LearnedCategory FindBestMatchKNN(ImageFingerprint fingerprint, List<LearnedCategory> categories, int k, out float bestSimilarity, string fileName = "")
        {
            bestSimilarity = 0f;
            if (categories == null || categories.Count == 0 || fingerprint == null) return null;

            // ===== 方案 A：硬规则预过滤 =====
            // ===== Scheme A: Hard-rule pre-filter =====
            var candidateCategories = ApplyHardRuleFilter(fingerprint, categories);

            // ===== 方案 C：余弦相似度 KNN =====
            // ===== Scheme C: Cosine similarity KNN =====
            // 收集所有候选样本的相似度
            var allMatches = new List<KeyValuePair<LearnedCategory, float>>();

            foreach (var category in candidateCategories)
            {
                if (category.sampleFingerprints == null || category.sampleFingerprints.Count == 0) continue;

                foreach (var sampleFp in category.sampleFingerprints)
                {
                    float similarity = fingerprint.CompareTo(sampleFp);
                    allMatches.Add(new KeyValuePair<LearnedCategory, float>(category, similarity));
                }
            }

            if (allMatches.Count == 0) return null;

            // 按相似度降序排序
            allMatches.Sort((a, b) => b.Value.CompareTo(a.Value));

            // === 改进的 KNN 投票：每个分类只取最高分样本参与投票 ===
            // === Improved KNN voting: each category contributes only its top sample ===
            // 
            // 原问题：直接取全局前K个，样本量大的分类会"淹没"真正匹配的分类
            // 例如 Trail 有3个0.72的近邻，Particle只有2个0.75的近邻 → Trail胜出（错误）
            // 
            // 改进方案：先按分类分组，每个分类取其最高分的样本作为代表
            // 然后按代表分数排序，取前K个分类投票
            // Original problem: taking global top-K, categories with more samples can "drown out" correct match
            // Fix: group by category, use each category's best sample as representative, then vote

            // 第一步：每个分类取其最高分样本
            // Step 1: find best sample per category
            var bestPerCategory = new Dictionary<string, float>();      // 每个分类的最高相似度
            var bestCategoryMap = new Dictionary<string, LearnedCategory>(); // 分类名→分类对象

            foreach (var match in allMatches)
            {
                string catName = match.Key.categoryName;
                if (!bestPerCategory.ContainsKey(catName) || match.Value > bestPerCategory[catName])
                {
                    bestPerCategory[catName] = match.Value;
                    bestCategoryMap[catName] = match.Key;
                }
            }

            // 第二步：按最高分排序
            // Step 2: sort categories by their best sample similarity
            var sortedCategories = new List<KeyValuePair<string, float>>(bestPerCategory);
            sortedCategories.Sort((a, b) => b.Value.CompareTo(a.Value));

            // 如果最高分远超第二高（差距>0.08），直接使用最高分的分类（高置信度直通）
            // If top match is significantly better than 2nd (gap>0.08), use it directly
            if (sortedCategories.Count >= 2 && 
                (sortedCategories[0].Value - sortedCategories[1].Value) > 0.08f)
            {
                if (enableDebugLog && !string.IsNullOrEmpty(fileName))
                {
                    Debug.Log($"[分类调试] {fileName} (本图: {fingerprint.estimatedRows}×{fingerprint.estimatedCols}={fingerprint.estimatedGridCount}子图) " +
                        $"→ 高置信度直通: {sortedCategories[0].Key} " +
                        $"(得分={sortedCategories[0].Value:F3}, 第二名={sortedCategories[1].Key}:{sortedCategories[1].Value:F3}, 差距={sortedCategories[0].Value - sortedCategories[1].Value:F3})");
                }
                bestSimilarity = sortedCategories[0].Value;
                return bestCategoryMap[sortedCategories[0].Key];
            }

            // 第三步：对前K个分类做加权投票（考虑每个分类的 top-N 样本）
            // Step 3: weighted voting for top-K categories (consider each category's top-N samples)
            int actualK = Mathf.Min(k, sortedCategories.Count);
            float topSim = sortedCategories[0].Value;
            float voteThreshold = topSim * 0.80f; // 至少达到最高分的80%才能投票

            string bestCategoryName = null;
            float bestAvgSim = 0f;

            for (int i = 0; i < actualK; i++)
            {
                string catName = sortedCategories[i].Key;
                float catBestSim = sortedCategories[i].Value;

                // 跳过远低于最高分的分类
                // Skip categories far below the best
                if (catBestSim < voteThreshold) continue;

                // 收集该分类在全局排名中的所有样本相似度（取前3个）
                // Collect this category's top 3 sample similarities
                var catSamples = new List<float>();
                foreach (var match in allMatches)
                {
                    if (match.Key.categoryName == catName)
                    {
                        catSamples.Add(match.Value);
                        if (catSamples.Count >= 3) break; // allMatches已排序，前3个就是最高的
                    }
                }

                // 评分 = 最高分 × 0.6 + 前3平均 × 0.4（最高分占主导，避免一个巧合翻盘）
                // Score = best × 0.6 + top3_avg × 0.4 (best dominates, avoids single-sample fluke)
                float avgTopN = 0f;
                foreach (float s in catSamples) avgTopN += s;
                avgTopN /= catSamples.Count;

                float score = catBestSim * 0.6f + avgTopN * 0.4f;

                if (score > bestAvgSim)
                {
                    bestAvgSim = score;
                    bestCategoryName = catName;
                }
            }

            // 调试日志：输出各分类得分对比 + 网格信息
            // Debug log: output score comparison + grid info for each category
            if (enableDebugLog && !string.IsNullOrEmpty(fileName))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[分类调试] {fileName} (本图: {fingerprint.estimatedRows}×{fingerprint.estimatedCols}={fingerprint.estimatedGridCount}子图, 规律性={fingerprint.gridRegularity:F2})");
                int showCount = Mathf.Min(5, sortedCategories.Count);
                for (int i = 0; i < showCount; i++)
                {
                    string catName = sortedCategories[i].Key;
                    float catBest = sortedCategories[i].Value;
                    string marker = (catName == bestCategoryName) ? " ← 选中" : "";
                    
                    // 查找该分类的平均网格信息
                    var cat = bestCategoryMap[catName];
                    string gridInfo = "";
                    if (cat.avgFingerprint != null)
                    {
                        gridInfo = $" [样本平均: {cat.avgFingerprint.estimatedRows}×{cat.avgFingerprint.estimatedCols}={cat.avgFingerprint.estimatedGridCount}子图]";
                    }
                    
                    sb.AppendLine($"  [{i + 1}] {catName}: 最高分={catBest:F3}{gridInfo}{marker}");
                }
                if (bestCategoryName != null)
                    sb.AppendLine($"  最终: {bestCategoryName} (相似度={bestPerCategory[bestCategoryName]:F3})");
                else
                    sb.AppendLine($"  最终: 无匹配");
                Debug.Log(sb.ToString());
            }

            // 返回最佳匹配的分类
            if (bestCategoryName != null)
            {
                bestSimilarity = bestPerCategory[bestCategoryName];
                return bestCategoryMap[bestCategoryName];
            }

            return null;
        }

        /// <summary>
        /// 执行智能分类（画面指纹模式）
        /// Perform AI classification (Visual Fingerprint Mode)
        /// </summary>
        private void PerformAIClassification()
        {
            if (config.learnedCategories.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先学习样本画面指纹", "确定");
                return;
            }

            if (string.IsNullOrEmpty(scanFolder))
            {
                EditorUtility.DisplayDialog("提示", "请先选择目标文件夹", "确定");
                return;
            }

            classifyResults.Clear();

            // 判断是绝对路径还是相对路径
            bool isAbsolutePath = Path.IsPathRooted(scanFolder);
            string fullPath;
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            if (isAbsolutePath)
            {
                fullPath = scanFolder;
            }
            else
            {
                fullPath = Path.Combine(projectRoot, scanFolder);
            }

            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("错误", $"文件夹不存在: {scanFolder}", "确定");
                return;
            }

            SearchOption searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] files = Directory.GetFiles(fullPath, "*.*", searchOption);

            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".psd", ".tiff", ".tif"
            };

            EditorUtility.DisplayProgressBar("智能分类", "正在分类...", 0);

            try
            {
                int totalFiles = files.Length;
                int processedFiles = 0;

                foreach (string file in files)
                {
                    processedFiles++;
                    EditorUtility.DisplayProgressBar("智能分类",
                        $"正在处理: {Path.GetFileName(file)}",
                        (float)processedFiles / totalFiles);

                    if (file.EndsWith(".meta")) continue;

                    string extension = Path.GetExtension(file);
                    if (!imageExtensions.Contains(extension)) continue;

                    // 尝试加载纹理（支持工程内外）
                    Texture2D texture = null;
                    bool isExternalFile = false;

                    // 检查是否在工程内
                    if (file.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        string relativePath = file.Substring(projectRoot.Length).TrimStart('/', '\\').Replace("\\", "/");
                        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
                    }

                    // 如果工程内加载失败，尝试从文件系统直接读取
                    if (texture == null && File.Exists(file))
                    {
                        try
                        {
                            byte[] fileData = File.ReadAllBytes(file);
                            texture = new Texture2D(2, 2);
                            if (texture.LoadImage(fileData))
                            {
                                isExternalFile = true;
                            }
                            else
                            {
                                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                                texture = null;
                            }
                        }
                        catch { }
                    }

                    if (texture == null) continue;

                    // 提取画面指纹
                    var fingerprint = ExtractFingerprint(texture);

                    // 如果是外部文件，用完后销毁
                    if (isExternalFile)
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }

                    if (fingerprint == null) continue;

                    // 找到最相似的分类
                    LearnedCategory bestMatch = null;
                    float bestSimilarity = 0f;

                    if (matchStrategy == MatchStrategy.KNN)
                    {
                        // KNN投票模式：与所有样本指纹比较，取前K个投票
                        bestMatch = FindBestMatchKNN(fingerprint, config.learnedCategories, knnK, out bestSimilarity, Path.GetFileName(file));
                    }
                    else
                    {
                        // 平均指纹模式：与每个分类的平均指纹比较
                        foreach (var category in config.learnedCategories)
                        {
                            if (category.avgFingerprint == null) continue;
                            float similarity = fingerprint.CompareTo(category.avgFingerprint);
                            if (similarity > bestSimilarity)
                            {
                                bestSimilarity = similarity;
                                bestMatch = category;
                            }
                        }
                    }

                    // 相似度阈值判断
                    bool isMatched = bestSimilarity >= similarityThreshold;

                    var result = new ClassifyResult
                    {
                        filePath = file.Replace("\\", "/"),
                        fileName = Path.GetFileName(file),
                        isMatched = isMatched,
                        similarity = bestSimilarity
                    };

                    if (isMatched && bestMatch != null)
                    {
                        result.matchedCategory = bestMatch.categoryName;
                        // 目标路径保持与源文件夹相同的格式（绝对或相对）
                        if (isAbsolutePath)
                        {
                            result.targetPath = Path.Combine(fullPath, bestMatch.targetFolder, Path.GetFileName(file));
                        }
                        else
                        {
                            result.targetPath = Path.Combine(scanFolder, bestMatch.targetFolder, Path.GetFileName(file));
                        }
                    }
                    else if (!string.IsNullOrEmpty(unmatchedFolder))
                    {
                        // 未匹配但设置了未匹配文件夹，设置目标路径
                        result.matchedCategory = "未分类";
                        result.targetPath = Path.Combine(unmatchedFolder, Path.GetFileName(file)).Replace("\\", "/");
                    }

                    classifyResults.Add(result);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (matchMode == MatchMode.Execute)
            {
                ExecuteArchive();
            }
            else
            {
                int matchedCount = classifyResults.Count(r => r.isMatched);
                Debug.Log($"[画面指纹分类] 分类完成，共 {classifyResults.Count} 个图片文件，匹配成功 {matchedCount} 个");
            }
        }

        /// <summary>
        /// 获取可读纹理
        /// Get readable texture copy
        /// </summary>
        private Texture2D GetReadableTexture(Texture2D source)
        {
            if (source == null) return null;

            // 检查纹理是否可读
            try
            {
                source.GetPixel(0, 0);
                return source;
            }
            catch
            {
                // 纹理不可读，需要创建副本
                RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                readable.Apply();

                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);

                return readable;
            }
        }

        #endregion

        #region 文件归档

        /// <summary>
        /// 执行归档操作
        /// </summary>
        private void ExecuteArchive()
        {
            var archiveResults = classifyResults.Where(r => !string.IsNullOrEmpty(r.targetPath)).ToList();

            if (archiveResults.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有需要归档的文件", "确定");
                return;
            }

            int matchedCount = archiveResults.Count(r => r.isMatched);
            int unmatchedCount = archiveResults.Count - matchedCount;

            // 检查是否有工程外的文件
            bool hasExternalFiles = archiveResults.Any(r => Path.IsPathRooted(r.filePath));
            string confirmMsg = $"确定要归档以下文件吗？\n\n匹配成功: {matchedCount} 个";
            if (unmatchedCount > 0)
            {
                confirmMsg += $"\n未匹配移至: {unmatchedCount} 个";
            }
            if (hasExternalFiles)
            {
                confirmMsg += "\n\n包含工程外的文件，将使用文件系统移动";
            }

            if (!EditorUtility.DisplayDialog("确认归档", confirmMsg, "确定", "取消"))
            {
                return;
            }

            int successCount = 0;
            int failCount = 0;

            // 分离工程内和工程外文件
            // Separate internal and external files
            var internalFiles = archiveResults.Where(r => !Path.IsPathRooted(r.filePath)).ToList();
            var externalFiles = archiveResults.Where(r => Path.IsPathRooted(r.filePath)).ToList();

            // 先处理工程外文件（纯文件系统操作，不需要 AssetDatabase）
            // Process external files first (pure file system, no AssetDatabase needed)
            foreach (var result in externalFiles)
            {
                try
                {
                    string targetDir = Path.GetDirectoryName(result.targetPath);
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    if (File.Exists(result.filePath))
                    {
                        File.Move(result.filePath, result.targetPath);

                        // 同时移动 .meta 文件（如果存在）
                        string metaFile = result.filePath + ".meta";
                        if (File.Exists(metaFile))
                        {
                            File.Move(metaFile, result.targetPath + ".meta");
                        }

                        successCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"文件不存在: {result.fileName}");
                        failCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"归档失败: {result.fileName} - {e.Message}");
                    failCount++;
                }
            }

            // 再处理工程内文件（需要 AssetDatabase）
            // Then process internal files (requires AssetDatabase)
            if (internalFiles.Count > 0)
            {
                try
                {
                    AssetDatabase.StartAssetEditing();

                    foreach (var result in internalFiles)
                    {
                        try
                        {
                            string targetDir = Path.GetDirectoryName(result.targetPath);
                            if (!AssetDatabase.IsValidFolder(targetDir))
                            {
                                CreateFolderRecursive(targetDir);
                            }

                            string error = AssetDatabase.MoveAsset(result.filePath, result.targetPath);
                            if (string.IsNullOrEmpty(error))
                            {
                                successCount++;
                            }
                            else
                            {
                                Debug.LogWarning($"移动失败: {result.fileName} - {error}");
                                failCount++;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"归档失败: {result.fileName} - {e.Message}");
                            failCount++;
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            EditorUtility.DisplayDialog("归档完成",
                $"成功归档 {successCount} 个文件\n失败 {failCount} 个文件",
                "确定");

            // 清空结果列表
            classifyResults.Clear();
        }

        /// <summary>
        /// 递归创建文件夹（工程内）
        /// </summary>
        private void CreateFolderRecursive(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parentPath = Path.GetDirectoryName(folderPath);
            string folderName = Path.GetFileName(folderPath);

            // 确保父文件夹存在
            if (!AssetDatabase.IsValidFolder(parentPath))
            {
                CreateFolderRecursive(parentPath);
            }

            // 创建当前文件夹
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        #endregion

        #region 配置管理

        private void SaveConfig()
        {
            try
            {
                config.lastScanFolder = scanFolder;
                config.includeSubfolders = includeSubfolders;
                config.unmatchedFolder = unmatchedFolder;

                string directory = Path.GetDirectoryName(CONFIG_PATH);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(config, true);
                File.WriteAllText(CONFIG_PATH, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"保存配置失败: {e.Message}");
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(CONFIG_PATH))
                {
                    string json = File.ReadAllText(CONFIG_PATH);
                    config = JsonUtility.FromJson<ConfigData>(json);
                    
                    scanFolder = config.lastScanFolder;
                    includeSubfolders = config.includeSubfolders;
                    unmatchedFolder = config.unmatchedFolder ?? "";

                    // 验证已加载的学习数据是否为新格式（73维特征向量）
                    // Validate loaded data is in new format (73-dim feature vector)
                    if (config.learnedCategories != null && config.learnedCategories.Count > 0)
                    {
                        var firstCat = config.learnedCategories[0];
                        if (firstCat.sampleFingerprints != null && firstCat.sampleFingerprints.Count > 0)
                        {
                            var firstFp = firstCat.sampleFingerprints[0];
                            if (firstFp.featureVector == null || firstFp.featureVector.Length != ImageFingerprint.TOTAL_DIM)
                            {
                                Debug.LogWarning("[图片分类] 检测到旧版本学习数据（维度不匹配），已清空。请重新学习样本。");
                                config.learnedCategories.Clear();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"加载配置失败（可能是旧版格式），已重置: {e.Message}");
                config = new ConfigData();
            }
        }

        #endregion
    }
}
#endif
