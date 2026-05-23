using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 极坐标置换模块
    /// 置换图在极坐标空间采样：横轴=角度(0°~360°)，纵轴=半径(中心→边缘)
    /// Texture Tool - Polar displacement module
    /// Displacement map sampled in polar space: X=angle(0°~360°), Y=radius(center→edge)
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region 极坐标置换 数据

        // 变形模式枚举
        // Deformation mode enum
        private enum PolarDeformMode
        {
            Parameter,      // 参数控制 / Parameter control
            DisplaceMap     // 置换图 / Displacement map
        }

        // 曲线类型枚举
        // Curve type enum
        private enum PolarCurveType
        {
            Linear,         // 线性 / Linear
            Quadratic,      // 平方 / Quadratic
            Exponential     // 指数 / Exponential
        }

        // 边缘处理模式枚举
        // Edge handling mode enum
        private enum PolarEdgeMode
        {
            Clamp,          // 裁剪 / Clamp
            Wrap,           // 环绕 / Wrap
            Mirror          // 镜像 / Mirror
        }

        // 插值模式枚举
        // Interpolation mode enum
        private enum PolarInterpMode
        {
            Bilinear,       // 双线性 / Bilinear
            Bicubic         // 双三次 / Bicubic
        }

        // 置换图通道模式枚举（对应 Shader 采样通道 .rr / .rg 等）
        // Displacement channel mode enum (maps to Shader sample swizzle .rr / .rg etc.)
        private enum DisplaceChannelMode
        {
            RG,             // R=径向 G=角度（双通道独立） / R=radial G=angular (dual channel)
            RR,             // R=径向 R=角度（单通道同步） / R=radial R=angular (single channel)
            R_Only,         // 仅R→径向（角度不变） / R→radial only (no angular)
            G_Only          // 仅G→角度（径向不变） / G→angular only (no radial)
        }

        // 极坐标置换标签页数据
        // Polar Displace Tab Data
        private class PolarDisplaceTabData
        {
            // 源贴图 / Source texture
            public Texture2D inputTexture;

            // 变形模式 / Deformation mode
            public PolarDeformMode deformMode = PolarDeformMode.Parameter;

            // ── 参数控制模式 ──
            // ── Parameter control mode ──
            public float centerX = 0.5f;                        // 中心点X (0~1) / Center X
            public float centerY = 0.5f;                        // 中心点Y (0~1) / Center Y
            public float radialStretch = 0f;                    // 径向拉伸 (-1~1) / Radial stretch
            public PolarCurveType radialCurve = PolarCurveType.Linear;  // 径向曲线 / Radial curve
            public float angleTwist = 0f;                       // 角度扭曲（度） / Angle twist (degrees)
            public PolarCurveType angleCurve = PolarCurveType.Linear;   // 角度曲线 / Angle curve
            public float radialWave = 0f;                       // 径向波动幅度 (0~1) / Radial wave amplitude
            public float waveFrequency = 6f;                    // 波动频率 / Wave frequency
            public float wavePhase = 0f;                        // 波动相位（度） / Wave phase (degrees)

            // ── 置换图模式（Shader风格参数） ──
            // ── Displacement map mode (Shader-style parameters) ──
            public Texture2D displaceMap;                       // 置换贴图 / Displacement map

            // Tiling & Offset（对应 Shader 的 _ST: xy=tiling, zw=offset）
            // Tiling & Offset (maps to Shader _ST: xy=tiling, zw=offset)
            public float tilingU = 1f;                          // 平铺 U / Tiling U
            public float tilingV = 1f;                          // 平铺 V / Tiling V
            public float offsetU = 0f;                          // 偏移 U / Offset U
            public float offsetV = 0f;                          // 偏移 V / Offset V

            // UV流动速度（对应 Shader 的 _DistortTexU/_DistortTexV）
            // UV scroll speed (maps to Shader _DistortTexU/_DistortTexV)
            public float uvSpeedU = 0f;                         // U方向速度 / U speed
            public float uvSpeedV = 0f;                         // V方向速度 / V speed

            // 模拟时间（编辑器中模拟 Shader 的 _Time.y）
            // Simulated time (simulates Shader _Time.y in editor)
            public float simulateTime = 1f;                     // 模拟时间 / Simulated time

            // UV流动动画播放状态
            // UV scroll animation playback state
            public bool uvAnimPlaying = false;                  // 是否正在播放UV流动动画 / UV anim playing
            public double uvAnimLastTime = 0;                   // 上次动画更新的时间戳 / Last anim update timestamp

            // UV旋转（对应 Shader 的 _MainTexRotation）
            // UV rotation (maps to Shader _MainTexRotation)
            public float uvRotation = 0f;                       // UV旋转角度(度) / UV rotation angle (degrees)

            // 通道选择（Shader中用 .rr / .rg / .r 等）
            // Channel select (in Shader: .rr / .rg / .r etc.)
            public DisplaceChannelMode channelMode = DisplaceChannelMode.RG;  // 通道模式 / Channel mode

            // 扭曲强度（对应 Shader 的 _MainTexDistort 分轴版）
            // Distortion intensity (maps to Shader _MainTexDistort, per-axis)
            public float radialIntensity = 1f;                  // 径向强度 / Radial intensity
            public float angleIntensity = 1f;                   // 角度强度 / Angle intensity

            // ── 通用设置 ──
            // ── Common settings ──
            public PolarEdgeMode edgeMode = PolarEdgeMode.Clamp;
            public PolarInterpMode interpMode = PolarInterpMode.Bilinear;

            // 预览 / Preview
            public List<Texture2D> originalPreviews = new List<Texture2D>();
            public List<Texture2D> outputPreviews = new List<Texture2D>();
            public Vector2 scroll;

            // 置换图可视化预览 / Displacement map visualization preview
            public Texture2D displaceRadialPreview;      // 径向偏移热力图 / Radial offset heatmap
            public Texture2D displaceAngularPreview;     // 角度偏移热力图 / Angular offset heatmap
            public Texture2D displaceCombinedPreview;    // 叠加到源图的合成预览 / Combined overlay preview
            public bool showDisplacePreview = true;      // 是否显示置换预览 / Show displacement preview

            // ComputeShader 引用
            // ComputeShader reference
            public ComputeShader computeShader;
        }

        private PolarDisplaceTabData polarDisplaceData = new PolarDisplaceTabData();

        #endregion

        #region 极坐标置换 初始化/清理

        partial void InitPolarDisplaceModule()
        {
            // 尝试加载 ComputeShader
            // Try to load ComputeShader
            if (polarDisplaceData.computeShader == null)
            {
                string[] guids = AssetDatabase.FindAssets("PolarDisplace t:ComputeShader");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("PolarDisplace.compute"))
                    {
                        polarDisplaceData.computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                        break;
                    }
                }
            }
        }

        partial void ClearPolarDisplacePreviews()
        {
            ClearPreviews(polarDisplaceData.originalPreviews, polarDisplaceData.outputPreviews);
            ClearDisplaceMapPreviews();
            StopUVAnim();
        }

        /// <summary>
        /// 停止UV流动动画播放
        /// Stop UV scroll animation playback
        /// </summary>
        private void StopUVAnim()
        {
            if (polarDisplaceData.uvAnimPlaying)
            {
                polarDisplaceData.uvAnimPlaying = false;
                EditorApplication.update -= UVAnimUpdate;
            }
        }

        /// <summary>
        /// UV流动动画更新回调（每帧推进模拟时间并刷新预览）
        /// UV scroll animation update callback (advance simulate time and refresh previews each frame)
        /// </summary>
        private void UVAnimUpdate()
        {
            if (!polarDisplaceData.uvAnimPlaying) return;

            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - polarDisplaceData.uvAnimLastTime);

            // 限制刷新频率：至少间隔 0.15 秒（约6fps），避免CPU计算卡顿
            // Limit refresh rate: at least 0.15s interval (~6fps), avoid CPU lag
            if (dt < 0.15f) return;

            polarDisplaceData.uvAnimLastTime = now;

            // 推进模拟时间（循环 0~10）
            // Advance simulate time (loop 0~10)
            polarDisplaceData.simulateTime += dt;
            if (polarDisplaceData.simulateTime > 10f)
            {
                polarDisplaceData.simulateTime -= 10f;
            }

            // 刷新置换图预览和输出预览（动画模式用低分辨率避免卡顿）
            // Refresh displacement preview and output preview (low-res for animation to avoid lag)
            GenerateDisplaceMapVisualization();
            GeneratePolarDisplacePreviewsLowRes();
            Repaint();
        }

        /// <summary>
        /// 清理置换图可视化预览纹理
        /// Clear displacement map visualization preview textures
        /// </summary>
        private void ClearDisplaceMapPreviews()
        {
            if (polarDisplaceData.displaceRadialPreview != null)
            {
                DestroyImmediate(polarDisplaceData.displaceRadialPreview);
                polarDisplaceData.displaceRadialPreview = null;
            }
            if (polarDisplaceData.displaceAngularPreview != null)
            {
                DestroyImmediate(polarDisplaceData.displaceAngularPreview);
                polarDisplaceData.displaceAngularPreview = null;
            }
            if (polarDisplaceData.displaceCombinedPreview != null)
            {
                DestroyImmediate(polarDisplaceData.displaceCombinedPreview);
                polarDisplaceData.displaceCombinedPreview = null;
            }
        }

        #endregion

        #region 极坐标置换 拖放

        private void HandlePolarDisplaceDragDrop(UnityEngine.Object[] draggedObjects)
        {
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject is Texture2D texture)
                {
                    polarDisplaceData.inputTexture = texture;
                    GeneratePolarDisplacePreviews();
                    break;
                }
            }
        }

        #endregion

        #region 极坐标置换 UI

        private void DrawPolarDisplaceTab()
        {
            // ── 源贴图 ──
            // ── Source texture ──
            EditorGUILayout.LabelField("极坐标置换工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("源贴图", GUILayout.Width(60));
            EditorGUI.BeginChangeCheck();
            polarDisplaceData.inputTexture = (Texture2D)EditorGUILayout.ObjectField(
                polarDisplaceData.inputTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                GeneratePolarDisplacePreviews();
            }
            if (polarDisplaceData.inputTexture != null)
            {
                EditorGUILayout.LabelField(
                    $"{polarDisplaceData.inputTexture.width}×{polarDisplaceData.inputTexture.height} {polarDisplaceData.inputTexture.format}",
                    EditorStyles.miniLabel, GUILayout.Width(120));
            }
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                polarDisplaceData.inputTexture = null;
                ClearPreviews(polarDisplaceData.originalPreviews, polarDisplaceData.outputPreviews);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // ── 变形模式 ──
            // ── Deformation mode ──
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("变形模式", GUILayout.Width(60));
            if (GUILayout.Toggle(polarDisplaceData.deformMode == PolarDeformMode.Parameter, "参数控制", EditorStyles.miniButtonLeft))
            {
                if (polarDisplaceData.deformMode != PolarDeformMode.Parameter)
                {
                    polarDisplaceData.deformMode = PolarDeformMode.Parameter;
                    GeneratePolarDisplacePreviews();
                }
            }
            if (GUILayout.Toggle(polarDisplaceData.deformMode == PolarDeformMode.DisplaceMap, "置换图", EditorStyles.miniButtonRight))
            {
                if (polarDisplaceData.deformMode != PolarDeformMode.DisplaceMap)
                {
                    polarDisplaceData.deformMode = PolarDeformMode.DisplaceMap;
                    GeneratePolarDisplacePreviews();
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();

            if (polarDisplaceData.deformMode == PolarDeformMode.Parameter)
            {
                DrawPolarParameterMode();
            }
            else
            {
                DrawPolarDisplaceMapMode();
            }

            EditorGUILayout.Space(10);

            // ── 通用设置 ──
            // ── Common settings ──
            EditorGUILayout.LabelField("通用设置", EditorStyles.boldLabel);

            // 边缘处理
            // Edge handling
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("边缘处理", GUILayout.Width(60));
            if (GUILayout.Toggle(polarDisplaceData.edgeMode == PolarEdgeMode.Clamp, "裁剪", EditorStyles.miniButtonLeft))
            {
                polarDisplaceData.edgeMode = PolarEdgeMode.Clamp;
            }
            if (GUILayout.Toggle(polarDisplaceData.edgeMode == PolarEdgeMode.Wrap, "环绕", EditorStyles.miniButtonMid))
            {
                polarDisplaceData.edgeMode = PolarEdgeMode.Wrap;
            }
            if (GUILayout.Toggle(polarDisplaceData.edgeMode == PolarEdgeMode.Mirror, "镜像", EditorStyles.miniButtonRight))
            {
                polarDisplaceData.edgeMode = PolarEdgeMode.Mirror;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 插值模式
            // Interpolation mode
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("插值模式", GUILayout.Width(60));
            if (GUILayout.Toggle(polarDisplaceData.interpMode == PolarInterpMode.Bilinear, "双线性", EditorStyles.miniButtonLeft))
            {
                polarDisplaceData.interpMode = PolarInterpMode.Bilinear;
            }
            if (GUILayout.Toggle(polarDisplaceData.interpMode == PolarInterpMode.Bicubic, "双三次", EditorStyles.miniButtonRight))
            {
                polarDisplaceData.interpMode = PolarInterpMode.Bicubic;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                if (polarDisplaceData.deformMode == PolarDeformMode.DisplaceMap)
                {
                    GenerateDisplaceMapVisualization();
                }
                GeneratePolarDisplacePreviews();
            }

            // 预览区
            // Preview section
            DrawPreviewSection(polarDisplaceData.originalPreviews, polarDisplaceData.outputPreviews, ref polarDisplaceData.scroll);

            EditorGUILayout.Space(10);

            // ── 底部按钮 ──
            // ── Bottom buttons ──
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重置参数", GUILayout.Height(28)))
            {
                ResetPolarParameters();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("导出贴图", GUILayout.Height(28)))
            {
                ExportPolarDisplace(false);
            }
            if (GUILayout.Button("覆盖原图", GUILayout.Height(28)))
            {
                ExportPolarDisplace(true);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制参数控制模式UI
        /// Draw parameter control mode UI
        /// </summary>
        private void DrawPolarParameterMode()
        {
            EditorGUILayout.LabelField("参数控制模式", EditorStyles.boldLabel);

            polarDisplaceData.centerX = EditorGUILayout.Slider("中心点 X", polarDisplaceData.centerX, 0f, 1f);
            polarDisplaceData.centerY = EditorGUILayout.Slider("中心点 Y", polarDisplaceData.centerY, 0f, 1f);

            EditorGUILayout.Space(3);

            // 径向拉伸 + 曲线
            // Radial stretch + curve
            EditorGUILayout.BeginHorizontal();
            polarDisplaceData.radialStretch = EditorGUILayout.Slider("径向拉伸", polarDisplaceData.radialStretch, -1f, 1f);
            DrawPolarCurveButtons("radial", ref polarDisplaceData.radialCurve);
            EditorGUILayout.EndHorizontal();

            // 角度扭曲 + 曲线
            // Angle twist + curve
            EditorGUILayout.BeginHorizontal();
            polarDisplaceData.angleTwist = EditorGUILayout.Slider("角度扭曲", polarDisplaceData.angleTwist, -360f, 360f);
            DrawPolarCurveButtons("angle", ref polarDisplaceData.angleCurve);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 径向波动参数
            // Radial wave parameters
            polarDisplaceData.radialWave = EditorGUILayout.Slider("径向波动", polarDisplaceData.radialWave, 0f, 1f);
            polarDisplaceData.waveFrequency = EditorGUILayout.Slider("波动频率", polarDisplaceData.waveFrequency, 1f, 30f);
            polarDisplaceData.wavePhase = EditorGUILayout.Slider("波动相位", polarDisplaceData.wavePhase, 0f, 360f);
        }

        /// <summary>
        /// 绘制曲线类型按钮组
        /// Draw curve type button group
        /// </summary>
        private void DrawPolarCurveButtons(string id, ref PolarCurveType curveType)
        {
            GUILayout.Label("曲线", GUILayout.Width(30));
            if (GUILayout.Toggle(curveType == PolarCurveType.Linear, "线性", EditorStyles.miniButtonLeft, GUILayout.Width(35)))
            {
                curveType = PolarCurveType.Linear;
            }
            if (GUILayout.Toggle(curveType == PolarCurveType.Quadratic, "平方", EditorStyles.miniButtonMid, GUILayout.Width(35)))
            {
                curveType = PolarCurveType.Quadratic;
            }
            if (GUILayout.Toggle(curveType == PolarCurveType.Exponential, "指数", EditorStyles.miniButtonRight, GUILayout.Width(35)))
            {
                curveType = PolarCurveType.Exponential;
            }
        }

        /// <summary>
        /// 绘制置换图模式UI
        /// Draw displacement map mode UI
        /// </summary>
        private void DrawPolarDisplaceMapMode()
        {
            EditorGUILayout.LabelField("置换图模式", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "置换图在极坐标空间采样：\n" +
                "• 横轴对应角度 (0°~360°)，纵轴对应半径 (中心→边缘)\n" +
                "• 参数体系与 Shader 一致：Tiling/Offset/UV速度/旋转/通道选择\n" +
                "• UV变换顺序：旋转 → Tiling*UV + Offset → + 速度偏移", MessageType.Info);

            // ── 置换贴图 ──
            // ── Displacement texture ──
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("置换贴图", GUILayout.Width(60));
            EditorGUI.BeginChangeCheck();
            polarDisplaceData.displaceMap = (Texture2D)EditorGUILayout.ObjectField(
                polarDisplaceData.displaceMap, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                GenerateDisplaceMapVisualization();
                GeneratePolarDisplacePreviews();
            }
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                polarDisplaceData.displaceMap = null;
                ClearDisplaceMapPreviews();
                GeneratePolarDisplacePreviews();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // ── Tiling & Offset（对应 _ST） ──
            // ── Tiling & Offset (maps to _ST) ──
            EditorGUILayout.LabelField("Tiling & Offset", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tiling", GUILayout.Width(60));
            polarDisplaceData.tilingU = EditorGUILayout.FloatField(polarDisplaceData.tilingU, GUILayout.Width(60));
            polarDisplaceData.tilingV = EditorGUILayout.FloatField(polarDisplaceData.tilingV, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Offset", GUILayout.Width(60));
            polarDisplaceData.offsetU = EditorGUILayout.FloatField(polarDisplaceData.offsetU, GUILayout.Width(60));
            polarDisplaceData.offsetV = EditorGUILayout.FloatField(polarDisplaceData.offsetV, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // ── UV速度（对应 _DistortTexU/_DistortTexV） ──
            // ── UV Speed (maps to _DistortTexU/_DistortTexV) ──
            EditorGUILayout.LabelField("UV 流动速度", EditorStyles.miniBoldLabel);

            polarDisplaceData.uvSpeedU = EditorGUILayout.Slider("速度 U", polarDisplaceData.uvSpeedU, -2f, 2f);
            polarDisplaceData.uvSpeedV = EditorGUILayout.Slider("速度 V", polarDisplaceData.uvSpeedV, -2f, 2f);

            // 模拟时间 + 播放控制（Shader中 speed * _Time.y，编辑器中用滑条+动画代替时间轴）
            // Simulated time + playback control (in Shader: speed * _Time.y, in editor: slider + anim)
            EditorGUILayout.BeginHorizontal();
            polarDisplaceData.simulateTime = EditorGUILayout.Slider("模拟时间", polarDisplaceData.simulateTime, 0f, 10f);

            // 播放/停止按钮
            // Play/Stop button
            bool hasSpeed = Mathf.Abs(polarDisplaceData.uvSpeedU) > 0.001f || Mathf.Abs(polarDisplaceData.uvSpeedV) > 0.001f;
            EditorGUI.BeginDisabledGroup(!hasSpeed);
            if (GUILayout.Button(polarDisplaceData.uvAnimPlaying ? "■ 停止" : "▶ 播放", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                polarDisplaceData.uvAnimPlaying = !polarDisplaceData.uvAnimPlaying;
                if (polarDisplaceData.uvAnimPlaying)
                {
                    polarDisplaceData.uvAnimLastTime = EditorApplication.timeSinceStartup;
                    EditorApplication.update += UVAnimUpdate;
                }
                else
                {
                    EditorApplication.update -= UVAnimUpdate;
                    // 停止后刷新全分辨率预览
                    // Refresh full-res preview after stopping
                    GenerateDisplaceMapVisualization();
                    GeneratePolarDisplacePreviews();
                }
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("归零", EditorStyles.miniButton, GUILayout.Width(32)))
            {
                polarDisplaceData.simulateTime = 0f;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"实际UV偏移: U={polarDisplaceData.uvSpeedU * polarDisplaceData.simulateTime:F3}  V={polarDisplaceData.uvSpeedV * polarDisplaceData.simulateTime:F3}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(3);

            // ── UV旋转（对应 _MainTexRotation） ──
            // ── UV Rotation (maps to _MainTexRotation) ──
            polarDisplaceData.uvRotation = EditorGUILayout.Slider("UV旋转", polarDisplaceData.uvRotation, 0f, 360f);

            EditorGUILayout.Space(5);

            // ── 通道选择（对应 .rr / .rg 等） ──
            // ── Channel select (maps to .rr / .rg etc.) ──
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("通道模式", GUILayout.Width(60));
            if (GUILayout.Toggle(polarDisplaceData.channelMode == DisplaceChannelMode.RG, "R+G", EditorStyles.miniButtonLeft))
            {
                polarDisplaceData.channelMode = DisplaceChannelMode.RG;
            }
            if (GUILayout.Toggle(polarDisplaceData.channelMode == DisplaceChannelMode.RR, "R+R", EditorStyles.miniButtonMid))
            {
                polarDisplaceData.channelMode = DisplaceChannelMode.RR;
            }
            if (GUILayout.Toggle(polarDisplaceData.channelMode == DisplaceChannelMode.R_Only, "仅R", EditorStyles.miniButtonMid))
            {
                polarDisplaceData.channelMode = DisplaceChannelMode.R_Only;
            }
            if (GUILayout.Toggle(polarDisplaceData.channelMode == DisplaceChannelMode.G_Only, "仅G", EditorStyles.miniButtonRight))
            {
                polarDisplaceData.channelMode = DisplaceChannelMode.G_Only;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 通道说明
            // Channel description
            string channelDesc = polarDisplaceData.channelMode switch
            {
                DisplaceChannelMode.RG => "R通道→径向偏移，G通道→角度偏移",
                DisplaceChannelMode.RR => "R通道同时控制径向和角度偏移",
                DisplaceChannelMode.R_Only => "R通道→仅径向偏移，角度不变",
                DisplaceChannelMode.G_Only => "G通道→仅角度偏移，径向不变",
                _ => ""
            };
            EditorGUILayout.LabelField(channelDesc, EditorStyles.miniLabel);

            EditorGUILayout.Space(3);

            // ── 扭曲强度（对应 _MainTexDistort 分轴版） ──
            // ── Distortion intensity (maps to _MainTexDistort, per-axis) ──
            EditorGUILayout.LabelField("扭曲强度", EditorStyles.miniBoldLabel);
            polarDisplaceData.radialIntensity = EditorGUILayout.Slider("径向强度", polarDisplaceData.radialIntensity, 0f, 5f);
            polarDisplaceData.angleIntensity = EditorGUILayout.Slider("角度强度", polarDisplaceData.angleIntensity, 0f, 5f);

            // 中心点（置换图模式也需要）
            // Center point (also needed for displace map mode)
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("极坐标中心", EditorStyles.miniBoldLabel);
            polarDisplaceData.centerX = EditorGUILayout.Slider("中心点 X", polarDisplaceData.centerX, 0f, 1f);
            polarDisplaceData.centerY = EditorGUILayout.Slider("中心点 Y", polarDisplaceData.centerY, 0f, 1f);

            // ── 置换图可视化预览 ──
            // ── Displacement map visualization preview ──
            DrawDisplaceMapVisualization();
        }

        /// <summary>
        /// 绘制置换图可视化预览区域
        /// Draw displacement map visualization preview area
        /// </summary>
        private void DrawDisplaceMapVisualization()
        {
            if (polarDisplaceData.displaceMap == null) return;

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            polarDisplaceData.showDisplacePreview = EditorGUILayout.Foldout(
                polarDisplaceData.showDisplacePreview, "置换图预览", true);

            if (GUILayout.Button("刷新预览", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                GenerateDisplaceMapVisualization();
                GeneratePolarDisplacePreviews();
            }
            EditorGUILayout.EndHorizontal();

            if (!polarDisplaceData.showDisplacePreview) return;

            // 确保预览已生成
            // Ensure previews are generated
            if (polarDisplaceData.displaceRadialPreview == null ||
                polarDisplaceData.displaceAngularPreview == null)
            {
                GenerateDisplaceMapVisualization();
            }

            if (polarDisplaceData.displaceRadialPreview == null) return;

            // 确定预览尺寸（最大128px，保持比例）
            // Determine preview size (max 128px, keep aspect ratio)
            float previewSize = 128f;

            // UV偏移状态信息（置换图采样坐标偏移量）
            // UV offset status info (displacement map sampling coordinate offset)
            float actualOffsetU = polarDisplaceData.uvSpeedU * polarDisplaceData.simulateTime;
            float actualOffsetV = polarDisplaceData.uvSpeedV * polarDisplaceData.simulateTime;
            if (Mathf.Abs(actualOffsetU) > 0.001f || Mathf.Abs(actualOffsetV) > 0.001f)
            {
                EditorGUILayout.HelpBox(
                    $"UV流动生效中: 置换图采样偏移 U={actualOffsetU:F3} V={actualOffsetV:F3}\n" +
                    $"（Speed × Time = {polarDisplaceData.uvSpeedU:F2}×{polarDisplaceData.simulateTime:F2}, " +
                    $"{polarDisplaceData.uvSpeedV:F2}×{polarDisplaceData.simulateTime:F2}）", MessageType.None);
            }

            EditorGUILayout.BeginHorizontal();

            // 径向偏移热力图
            // Radial offset heatmap
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("径向偏移(R)", EditorStyles.miniLabel, GUILayout.Width(previewSize));
            if (polarDisplaceData.displaceRadialPreview != null)
            {
                Rect r = EditorGUILayout.GetControlRect(GUILayout.Width(previewSize), GUILayout.Height(previewSize));
                GUI.DrawTexture(r, polarDisplaceData.displaceRadialPreview, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(4);

            // 角度偏移热力图
            // Angular offset heatmap
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("角度偏移(G)", EditorStyles.miniLabel, GUILayout.Width(previewSize));
            if (polarDisplaceData.displaceAngularPreview != null)
            {
                Rect r = EditorGUILayout.GetControlRect(GUILayout.Width(previewSize), GUILayout.Height(previewSize));
                GUI.DrawTexture(r, polarDisplaceData.displaceAngularPreview, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(4);

            // 叠加合成预览（如果有源贴图）
            // Combined overlay preview (if source texture available)
            if (polarDisplaceData.displaceCombinedPreview != null)
            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("向量场叠加", EditorStyles.miniLabel, GUILayout.Width(previewSize));
                Rect r = EditorGUILayout.GetControlRect(GUILayout.Width(previewSize), GUILayout.Height(previewSize));
                GUI.DrawTexture(r, polarDisplaceData.displaceCombinedPreview, ScaleMode.ScaleToFit);
                EditorGUILayout.EndVertical();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 图例说明
            // Legend
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);
            EditorGUILayout.LabelField("■", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.2f, 0.5f, 1f) } }, GUILayout.Width(10));
            EditorGUILayout.LabelField("负偏移(-)", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("■", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } }, GUILayout.Width(10));
            EditorGUILayout.LabelField("无偏移(0)", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("■", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.3f, 0.2f) } }, GUILayout.Width(10));
            EditorGUILayout.LabelField("正偏移(+)", EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 重置所有参数
        /// Reset all parameters
        /// </summary>
        private void ResetPolarParameters()
        {
            polarDisplaceData.centerX = 0.5f;
            polarDisplaceData.centerY = 0.5f;
            polarDisplaceData.radialStretch = 0f;
            polarDisplaceData.radialCurve = PolarCurveType.Linear;
            polarDisplaceData.angleTwist = 0f;
            polarDisplaceData.angleCurve = PolarCurveType.Linear;
            polarDisplaceData.radialWave = 0f;
            polarDisplaceData.waveFrequency = 6f;
            polarDisplaceData.wavePhase = 0f;

            // 置换图Shader风格参数重置
            // Displacement map Shader-style parameter reset
            polarDisplaceData.tilingU = 1f;
            polarDisplaceData.tilingV = 1f;
            polarDisplaceData.offsetU = 0f;
            polarDisplaceData.offsetV = 0f;
            polarDisplaceData.uvSpeedU = 0f;
            polarDisplaceData.uvSpeedV = 0f;
            polarDisplaceData.simulateTime = 1f;
            StopUVAnim();
            polarDisplaceData.uvRotation = 0f;
            polarDisplaceData.channelMode = DisplaceChannelMode.RG;
            polarDisplaceData.radialIntensity = 1f;
            polarDisplaceData.angleIntensity = 1f;

            polarDisplaceData.edgeMode = PolarEdgeMode.Clamp;
            polarDisplaceData.interpMode = PolarInterpMode.Bilinear;
            GeneratePolarDisplacePreviews();
        }

        #endregion

        #region 极坐标置换 逻辑

        /// <summary>
        /// 置换图UV变换（对应 Shader: 旋转 → TRANSFORM_TEX → + 速度偏移）
        /// Displacement map UV transform (maps to Shader: rotate → TRANSFORM_TEX → + speed offset)
        /// 公式: uv = rotate(uv, rotation) * tiling + offset + speed * time
        /// 编辑器中无时间轴，speed 直接叠加为静态偏移预览
        /// In editor there's no timeline, speed adds as static offset for preview
        /// </summary>
        private Vector2 TransformDisplaceUV(float rawU, float rawV)
        {
            float u = rawU;
            float v = rawV;

            // ①UV旋转（绕0.5,0.5中心旋转，对应 Shader _MainTexRotation）
            // UV rotation (rotate around 0.5,0.5 center, maps to Shader _MainTexRotation)
            float rot = polarDisplaceData.uvRotation * Mathf.Deg2Rad;
            if (Mathf.Abs(rot) > 0.001f)
            {
                float cu = u - 0.5f;
                float cv = v - 0.5f;
                float cosR = Mathf.Cos(rot);
                float sinR = Mathf.Sin(rot);
                u = cu * cosR - cv * sinR + 0.5f;
                v = cu * sinR + cv * cosR + 0.5f;
            }

            // ②TRANSFORM_TEX: uv * tiling + offset（对应 Shader _ST）
            // TRANSFORM_TEX: uv * tiling + offset (maps to Shader _ST)
            u = u * polarDisplaceData.tilingU + polarDisplaceData.offsetU;
            v = v * polarDisplaceData.tilingV + polarDisplaceData.offsetV;

            // ③UV速度偏移（对应 Shader frac(time * half2(U,V))）
            // UV speed offset (maps to Shader frac(time * half2(U,V)))
            // speed * simulateTime 模拟运行时时间轴
            // speed * simulateTime simulates runtime timeline
            u += polarDisplaceData.uvSpeedU * polarDisplaceData.simulateTime;
            v += polarDisplaceData.uvSpeedV * polarDisplaceData.simulateTime;

            // Wrap到 [0,1]（对应 Shader 的 frac）
            // Wrap to [0,1] (maps to Shader frac)
            u = u - Mathf.Floor(u);
            v = v - Mathf.Floor(v);

            return new Vector2(u, v);
        }

        /// <summary>
        /// 根据通道模式从置换图采样获取径向和角度偏移量
        /// Get radial and angular offset from displacement sample based on channel mode
        /// 对应 Shader 中的 .rr / .rg / .r 等通道选择
        /// Maps to Shader channel swizzle .rr / .rg / .r etc.
        /// </summary>
        private void SampleDisplaceChannels(Color sample, float radialInt, float angleInt,
            out float deltaR, out float deltaAngle)
        {
            switch (polarDisplaceData.channelMode)
            {
                case DisplaceChannelMode.RG:
                    // R→径向，G→角度（双通道独立）
                    // R→radial, G→angular (dual channel independent)
                    deltaR = (sample.r - 0.5f) * 2f * radialInt;
                    deltaAngle = (sample.g - 0.5f) * 2f * angleInt;
                    break;
                case DisplaceChannelMode.RR:
                    // R同时控制两者（单通道同步，类似 Shader .rr）
                    // R controls both (single channel sync, like Shader .rr)
                    deltaR = (sample.r - 0.5f) * 2f * radialInt;
                    deltaAngle = (sample.r - 0.5f) * 2f * angleInt;
                    break;
                case DisplaceChannelMode.R_Only:
                    // 仅R→径向，角度不变
                    // R→radial only, no angular
                    deltaR = (sample.r - 0.5f) * 2f * radialInt;
                    deltaAngle = 0f;
                    break;
                case DisplaceChannelMode.G_Only:
                    // 仅G→角度，径向不变
                    // G→angular only, no radial
                    deltaR = 0f;
                    deltaAngle = (sample.g - 0.5f) * 2f * angleInt;
                    break;
                default:
                    deltaR = 0f;
                    deltaAngle = 0f;
                    break;
            }
        }

        /// <summary>
        /// 从极坐标空间采样置换图（完整Shader风格UV变换）
        /// Sample displacement map from polar space (full Shader-style UV transform)
        /// 流程：极坐标(angle,radius) → TransformDisplaceUV → 采样像素
        /// Pipeline: polar(angle,radius) → TransformDisplaceUV → sample pixel
        /// </summary>
        private Color SampleDisplaceMapPolar(Color[] displacePixels, int dw, int dh,
            float normalizedAngle, float normalizedR)
        {
            Vector2 uv = TransformDisplaceUV(normalizedAngle, normalizedR);
            int dpx = Mathf.Clamp(Mathf.RoundToInt(uv.x * (dw - 1)), 0, dw - 1);
            int dpy = Mathf.Clamp(Mathf.RoundToInt(uv.y * (dh - 1)), 0, dh - 1);
            return displacePixels[dpy * dw + dpx];
        }

        /// <summary>
        /// 生成置换图可视化预览（径向热力图、角度热力图、向量场叠加图）
        /// Generate displacement map visualization (radial heatmap, angular heatmap, vector field overlay)
        /// </summary>
        private void GenerateDisplaceMapVisualization()
        {
            ClearDisplaceMapPreviews();

            if (polarDisplaceData.displaceMap == null) return;

            Color[] displacePixels = GetTexturePixels(polarDisplaceData.displaceMap);
            if (displacePixels == null) return;

            int dw = polarDisplaceData.displaceMap.width;
            int dh = polarDisplaceData.displaceMap.height;

            // 可视化尺寸（128x128）
            // Visualization size
            int vizSize = 128;
            float radialInt = polarDisplaceData.radialIntensity;
            float angleInt = polarDisplaceData.angleIntensity;

            // ── 径向偏移热力图：在直角坐标空间展示，极坐标采样置换图 ──
            // ── Radial offset heatmap: display in cartesian space, sample displacement in polar ──
            Texture2D radialTex = new Texture2D(vizSize, vizSize, TextureFormat.ARGB32, false);
            Texture2D angularTex = new Texture2D(vizSize, vizSize, TextureFormat.ARGB32, false);
            Color[] radialPixels = new Color[vizSize * vizSize];
            Color[] angularPixels = new Color[vizSize * vizSize];

            float cx = polarDisplaceData.centerX;
            float cy = polarDisplaceData.centerY;
            float maxR = Mathf.Max(
                Mathf.Sqrt(cx * cx + cy * cy),
                Mathf.Sqrt((1f - cx) * (1f - cx) + cy * cy),
                Mathf.Sqrt(cx * cx + (1f - cy) * (1f - cy)),
                Mathf.Sqrt((1f - cx) * (1f - cx) + (1f - cy) * (1f - cy))
            );
            if (maxR < 0.001f) maxR = 0.001f;

            for (int y = 0; y < vizSize; y++)
            {
                for (int x = 0; x < vizSize; x++)
                {
                    float u = (float)x / (vizSize - 1);
                    float v = (float)y / (vizSize - 1);

                    float dx = u - cx;
                    float dy = v - cy;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float normalizedR = radius / maxR;
                    float angle = Mathf.Atan2(dy, dx);
                    float normalizedAngle = (angle / (2f * Mathf.PI) + 0.5f);
                    normalizedAngle = normalizedAngle - Mathf.Floor(normalizedAngle);

                    // 通过统一UV变换采样置换图（Shader风格）
                    // Sample displacement map via unified UV transform (Shader-style)
                    Color displaceSample = SampleDisplaceMapPolar(displacePixels, dw, dh, normalizedAngle, normalizedR);

                    // 通过通道模式获取偏移量
                    // Get offset via channel mode
                    SampleDisplaceChannels(displaceSample, radialInt, angleInt,
                        out float radialOffset, out float angularOffset);

                    // 热力图着色：蓝(-) → 灰(0) → 红(+)
                    // Heatmap coloring: blue(-) → gray(0) → red(+)
                    radialPixels[y * vizSize + x] = OffsetToHeatColor(radialOffset);
                    angularPixels[y * vizSize + x] = OffsetToHeatColor(angularOffset);
                }
            }

            radialTex.SetPixels(radialPixels);
            radialTex.Apply();
            polarDisplaceData.displaceRadialPreview = radialTex;

            angularTex.SetPixels(angularPixels);
            angularTex.Apply();
            polarDisplaceData.displaceAngularPreview = angularTex;

            // ── 叠加合成预览：源图 + 向量场箭头 ──
            // ── Combined overlay: source + vector field arrows ──
            if (polarDisplaceData.inputTexture != null)
            {
                GenerateVectorFieldOverlay(vizSize, displacePixels, dw, dh, maxR);
            }
        }

        /// <summary>
        /// 偏移量转热力颜色：蓝色(-) → 深灰(0) → 红色(+)
        /// Offset to heatmap color: blue(-) → dark gray(0) → red(+)
        /// </summary>
        private Color OffsetToHeatColor(float offset)
        {
            // 将偏移量映射到 -1~1 范围后裁剪
            // Map offset to -1~1 range then clamp
            float t = Mathf.Clamp(offset, -1f, 1f);

            if (t < 0f)
            {
                // 负偏移：深灰 → 蓝色
                // Negative: dark gray → blue
                float a = -t;
                return Color.Lerp(new Color(0.25f, 0.25f, 0.25f, 1f), new Color(0.15f, 0.4f, 1f, 1f), a);
            }
            else
            {
                // 正偏移：深灰 → 红色
                // Positive: dark gray → red
                return Color.Lerp(new Color(0.25f, 0.25f, 0.25f, 1f), new Color(1f, 0.25f, 0.15f, 1f), t);
            }
        }

        /// <summary>
        /// 生成向量场叠加预览（源图上绘制偏移方向箭头）
        /// Generate vector field overlay (draw offset direction arrows on source)
        /// </summary>
        private void GenerateVectorFieldOverlay(int vizSize, Color[] displacePixels, int dw, int dh, float maxR)
        {
            Color[] sourcePixels = GetTexturePixels(polarDisplaceData.inputTexture);
            if (sourcePixels == null) return;

            int srcW = polarDisplaceData.inputTexture.width;
            int srcH = polarDisplaceData.inputTexture.height;

            Texture2D combinedTex = new Texture2D(vizSize, vizSize, TextureFormat.ARGB32, false);
            Color[] combinedPixels = new Color[vizSize * vizSize];

            float cx = polarDisplaceData.centerX;
            float cy = polarDisplaceData.centerY;
            float radialInt = polarDisplaceData.radialIntensity;
            float angleInt = polarDisplaceData.angleIntensity;

            // 先绘制缩小版源图作为底图
            // Draw scaled-down source as background
            for (int y = 0; y < vizSize; y++)
            {
                for (int x = 0; x < vizSize; x++)
                {
                    float u = (float)x / (vizSize - 1);
                    float v = (float)y / (vizSize - 1);
                    int sx = Mathf.Clamp(Mathf.RoundToInt(u * (srcW - 1)), 0, srcW - 1);
                    int sy = Mathf.Clamp(Mathf.RoundToInt(v * (srcH - 1)), 0, srcH - 1);
                    Color c = sourcePixels[sy * srcW + sx];
                    // 降低亮度便于看清箭头
                    // Dim brightness for arrow visibility
                    combinedPixels[y * vizSize + x] = c * 0.4f + new Color(0.1f, 0.1f, 0.1f, 0f);
                    combinedPixels[y * vizSize + x].a = 1f;
                }
            }

            // 在网格点上绘制偏移向量线段
            // Draw offset vector lines at grid points
            int gridStep = 8;  // 每8像素一个采样点 / One sample per 8 pixels
            for (int gy = gridStep / 2; gy < vizSize; gy += gridStep)
            {
                for (int gx = gridStep / 2; gx < vizSize; gx += gridStep)
                {
                    float u = (float)gx / (vizSize - 1);
                    float v = (float)gy / (vizSize - 1);

                    float ddx = u - cx;
                    float ddy = v - cy;
                    float radius = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                    float normalizedR = radius / maxR;
                    float angle = Mathf.Atan2(ddy, ddx);
                    float normalizedAngle = (angle / (2f * Mathf.PI) + 0.5f);
                    normalizedAngle = normalizedAngle - Mathf.Floor(normalizedAngle);

                    // 通过统一UV变换采样置换图（Shader风格）
                    // Sample displacement map via unified UV transform (Shader-style)
                    Color displaceSample = SampleDisplaceMapPolar(displacePixels, dw, dh, normalizedAngle, normalizedR);

                    // 通过通道模式获取偏移量
                    // Get offset via channel mode
                    SampleDisplaceChannels(displaceSample, radialInt, angleInt,
                        out float rOffset, out float aOffset);

                    // 径向方向（指向中心/远离中心）
                    // Radial direction (toward/away from center)
                    float radDir = (radius > 0.001f) ? 1f : 0f;
                    float radDirX = (radius > 0.001f) ? ddx / radius : 0f;
                    float radDirY = (radius > 0.001f) ? ddy / radius : 0f;

                    // 切向方向（垂直于径向）
                    // Tangential direction (perpendicular to radial)
                    float tanDirX = -radDirY;
                    float tanDirY = radDirX;

                    // 合成偏移向量（像素空间）
                    // Combined offset vector (pixel space)
                    float arrowScale = gridStep * 0.8f;
                    float vx = (radDirX * rOffset + tanDirX * aOffset) * arrowScale;
                    float vy = (radDirY * rOffset + tanDirY * aOffset) * arrowScale;

                    float mag = Mathf.Sqrt(vx * vx + vy * vy);
                    if (mag < 0.5f) continue;

                    // 颜色根据偏移强度
                    // Color based on offset magnitude
                    float colorT = Mathf.Clamp01(mag / (gridStep * 1.5f));
                    Color lineColor = Color.Lerp(new Color(0.3f, 1f, 0.3f, 1f), new Color(1f, 1f, 0f, 1f), colorT);

                    // 绘制线段
                    // Draw line segment
                    DrawLineOnPixels(combinedPixels, vizSize, vizSize, gx, gy,
                        gx + Mathf.RoundToInt(vx), gy + Mathf.RoundToInt(vy), lineColor);
                }
            }

            combinedTex.SetPixels(combinedPixels);
            combinedTex.Apply();
            polarDisplaceData.displaceCombinedPreview = combinedTex;
        }

        /// <summary>
        /// 在像素数组上绘制线段（Bresenham算法）
        /// Draw line on pixel array (Bresenham's algorithm)
        /// </summary>
        private void DrawLineOnPixels(Color[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            int maxSteps = dx + dy + 1;
            for (int step = 0; step < maxSteps; step++)
            {
                if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
                {
                    pixels[y0 * width + x0] = color;
                }

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        private void GeneratePolarDisplacePreviews()
        {
            ClearPreviews(polarDisplaceData.originalPreviews, polarDisplaceData.outputPreviews);

            if (polarDisplaceData.inputTexture == null) return;

            GenerateOriginalPreview(polarDisplaceData.inputTexture, polarDisplaceData.originalPreviews);

            int width = polarDisplaceData.inputTexture.width;
            int height = polarDisplaceData.inputTexture.height;

            Texture2D result = ApplyPolarDisplaceCPU(polarDisplaceData.inputTexture, width, height);

            if (result != null)
            {
                polarDisplaceData.outputPreviews.Add(result);
            }
        }

        /// <summary>
        /// 低分辨率预览（动画播放时使用，最大128px，避免CPU卡顿）
        /// Low-resolution preview (used during animation playback, max 128px, avoid CPU lag)
        /// </summary>
        private void GeneratePolarDisplacePreviewsLowRes()
        {
            ClearPreviews(polarDisplaceData.originalPreviews, polarDisplaceData.outputPreviews);

            if (polarDisplaceData.inputTexture == null) return;

            GenerateOriginalPreview(polarDisplaceData.inputTexture, polarDisplaceData.originalPreviews);

            // 动画时限制最大分辨率为128，大幅降低CPU开销
            // During animation, cap resolution at 128 to greatly reduce CPU cost
            int srcW = polarDisplaceData.inputTexture.width;
            int srcH = polarDisplaceData.inputTexture.height;
            int maxDim = Mathf.Max(srcW, srcH);
            int cap = 128;
            int width, height;
            if (maxDim > cap)
            {
                float scale = (float)cap / maxDim;
                width = Mathf.Max(1, Mathf.RoundToInt(srcW * scale));
                height = Mathf.Max(1, Mathf.RoundToInt(srcH * scale));
            }
            else
            {
                width = srcW;
                height = srcH;
            }

            Texture2D result = ApplyPolarDisplaceCPU(polarDisplaceData.inputTexture, width, height);

            if (result != null)
            {
                polarDisplaceData.outputPreviews.Add(result);
            }
        }

        /// <summary>
        /// 应用曲线函数到 t 值 (0~1)
        /// Apply curve function to t value (0~1)
        /// </summary>
        private float ApplyPolarCurve(float t, PolarCurveType curve)
        {
            float sign = Mathf.Sign(t);
            float abs = Mathf.Abs(t);

            switch (curve)
            {
                case PolarCurveType.Quadratic:
                    return sign * abs * abs;
                case PolarCurveType.Exponential:
                    return sign * (Mathf.Exp(abs) - 1f) / (Mathf.Exp(1f) - 1f);
                case PolarCurveType.Linear:
                default:
                    return t;
            }
        }

        /// <summary>
        /// 边缘处理：将uv坐标限制/环绕/镜像到 [0,1] 范围
        /// Edge handling: clamp/wrap/mirror UV coordinates to [0,1] range
        /// </summary>
        private float ApplyPolarEdge(float coord, PolarEdgeMode mode)
        {
            switch (mode)
            {
                case PolarEdgeMode.Wrap:
                    coord = coord - Mathf.Floor(coord);
                    return coord;
                case PolarEdgeMode.Mirror:
                    coord = Mathf.Abs(coord);
                    int n = Mathf.FloorToInt(coord);
                    coord = coord - n;
                    if (n % 2 == 1) coord = 1f - coord;
                    return coord;
                case PolarEdgeMode.Clamp:
                default:
                    return Mathf.Clamp01(coord);
            }
        }

        /// <summary>
        /// 双三次插值权重函数 (Mitchell-Netravali)
        /// Bicubic interpolation weight function (Mitchell-Netravali)
        /// </summary>
        private float BicubicWeight(float x)
        {
            float ax = Mathf.Abs(x);
            if (ax <= 1f)
                return (1.5f * ax * ax * ax - 2.5f * ax * ax + 1f);
            else if (ax < 2f)
                return (-0.5f * ax * ax * ax + 2.5f * ax * ax - 4f * ax + 2f);
            return 0f;
        }

        /// <summary>
        /// 双三次插值采样
        /// Bicubic interpolation sampling
        /// </summary>
        private Color SampleBicubic(Color[] pixels, int texW, int texH, float px, float py, PolarEdgeMode edgeMode)
        {
            int ix = Mathf.FloorToInt(px);
            int iy = Mathf.FloorToInt(py);
            float fx = px - ix;
            float fy = py - iy;

            Color result = Color.clear;

            for (int m = -1; m <= 2; m++)
            {
                float wy = BicubicWeight(fy - m);
                for (int n = -1; n <= 2; n++)
                {
                    float wx = BicubicWeight(fx - n);
                    float su = (float)(ix + n) / (texW - 1);
                    float sv = (float)(iy + m) / (texH - 1);
                    su = ApplyPolarEdge(su, edgeMode);
                    sv = ApplyPolarEdge(sv, edgeMode);
                    int sx = Mathf.Clamp(Mathf.RoundToInt(su * (texW - 1)), 0, texW - 1);
                    int sy = Mathf.Clamp(Mathf.RoundToInt(sv * (texH - 1)), 0, texH - 1);
                    Color c = pixels[sy * texW + sx];
                    result += c * wx * wy;
                }
            }

            result.r = Mathf.Clamp01(result.r);
            result.g = Mathf.Clamp01(result.g);
            result.b = Mathf.Clamp01(result.b);
            result.a = Mathf.Clamp01(result.a);
            return result;
        }

        /// <summary>
        /// 双线性插值采样
        /// Bilinear interpolation sampling
        /// </summary>
        private Color SampleBilinearPolar(Color[] pixels, int texW, int texH, float px, float py, PolarEdgeMode edgeMode)
        {
            int x0 = Mathf.FloorToInt(px);
            int y0 = Mathf.FloorToInt(py);
            float fx = px - x0;
            float fy = py - y0;

            // 通过边缘模式计算4个采样点
            // Calculate 4 sample points via edge mode
            float u0 = (float)x0 / Mathf.Max(texW - 1, 1);
            float v0 = (float)y0 / Mathf.Max(texH - 1, 1);
            float u1 = (float)(x0 + 1) / Mathf.Max(texW - 1, 1);
            float v1 = (float)(y0 + 1) / Mathf.Max(texH - 1, 1);

            u0 = ApplyPolarEdge(u0, edgeMode);
            v0 = ApplyPolarEdge(v0, edgeMode);
            u1 = ApplyPolarEdge(u1, edgeMode);
            v1 = ApplyPolarEdge(v1, edgeMode);

            int sx0 = Mathf.Clamp(Mathf.RoundToInt(u0 * (texW - 1)), 0, texW - 1);
            int sy0 = Mathf.Clamp(Mathf.RoundToInt(v0 * (texH - 1)), 0, texH - 1);
            int sx1 = Mathf.Clamp(Mathf.RoundToInt(u1 * (texW - 1)), 0, texW - 1);
            int sy1 = Mathf.Clamp(Mathf.RoundToInt(v1 * (texH - 1)), 0, texH - 1);

            Color c00 = pixels[sy0 * texW + sx0];
            Color c10 = pixels[sy0 * texW + sx1];
            Color c01 = pixels[sy1 * texW + sx0];
            Color c11 = pixels[sy1 * texW + sx1];

            Color c0 = Color.Lerp(c00, c10, fx);
            Color c1 = Color.Lerp(c01, c11, fx);
            return Color.Lerp(c0, c1, fy);
        }

        /// <summary>
        /// CPU 极坐标置换核心算法
        /// 工作原理：遍历输出图每个像素(x,y)→转为极坐标(angle,radius)→应用变形→在极坐标空间采样源图
        /// CPU polar displacement core algorithm
        /// Workflow: for each output pixel(x,y)→convert to polar(angle,radius)→apply deformation→sample source in polar space
        /// </summary>
        private Texture2D ApplyPolarDisplaceCPU(Texture2D source, int width, int height)
        {
            Color[] sourcePixels = GetTexturePixels(source);
            if (sourcePixels == null) return null;

            // 置换图像素（如果有的话）
            // Displacement map pixels (if available)
            Color[] displacePixels = null;
            int displaceW = 0, displaceH = 0;
            if (polarDisplaceData.deformMode == PolarDeformMode.DisplaceMap && polarDisplaceData.displaceMap != null)
            {
                displacePixels = GetTexturePixels(polarDisplaceData.displaceMap);
                displaceW = polarDisplaceData.displaceMap.width;
                displaceH = polarDisplaceData.displaceMap.height;
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.ARGB32, false);
            Color[] resultPixels = new Color[width * height];

            float cx = polarDisplaceData.centerX;
            float cy = polarDisplaceData.centerY;
            PolarEdgeMode edgeMode = polarDisplaceData.edgeMode;
            PolarInterpMode interpMode = polarDisplaceData.interpMode;

            // 最大半径（从中心到最远角落）
            // Max radius (from center to farthest corner)
            float maxR = Mathf.Max(
                Mathf.Sqrt(cx * cx + cy * cy),
                Mathf.Sqrt((1f - cx) * (1f - cx) + cy * cy),
                Mathf.Sqrt(cx * cx + (1f - cy) * (1f - cy)),
                Mathf.Sqrt((1f - cx) * (1f - cx) + (1f - cy) * (1f - cy))
            );
            if (maxR < 0.001f) maxR = 0.001f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / Mathf.Max(width - 1, 1);
                    float v = (float)y / Mathf.Max(height - 1, 1);

                    // 转为相对中心点的坐标
                    // Convert to center-relative coordinates
                    float dx = u - cx;
                    float dy = v - cy;

                    // 极坐标：半径 + 角度
                    // Polar coordinates: radius + angle
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float normalizedR = radius / maxR;  // 0~1 归一化半径
                    float angle = Mathf.Atan2(dy, dx);  // -PI ~ PI

                    // 归一化角度到 0~1
                    // Normalize angle to 0~1
                    float normalizedAngle = (angle / (2f * Mathf.PI) + 0.5f);
                    normalizedAngle = normalizedAngle - Mathf.Floor(normalizedAngle);

                    // 应用变形
                    // Apply deformation
                    float deltaR = 0f;
                    float deltaAngle = 0f;

                    if (polarDisplaceData.deformMode == PolarDeformMode.Parameter)
                    {
                        // 径向拉伸（带曲线）
                        // Radial stretch (with curve)
                        float stretchAmount = ApplyPolarCurve(normalizedR, polarDisplaceData.radialCurve);
                        deltaR = polarDisplaceData.radialStretch * stretchAmount;

                        // 角度扭曲（带曲线，随半径递增）
                        // Angle twist (with curve, increases with radius)
                        float twistAmount = ApplyPolarCurve(normalizedR, polarDisplaceData.angleCurve);
                        deltaAngle = (polarDisplaceData.angleTwist / 360f) * twistAmount;

                        // 径向波动
                        // Radial wave
                        if (polarDisplaceData.radialWave > 0.001f)
                        {
                            float wavePhaseRad = polarDisplaceData.wavePhase * Mathf.Deg2Rad;
                            float wave = Mathf.Sin(normalizedAngle * 2f * Mathf.PI * polarDisplaceData.waveFrequency + wavePhaseRad);
                            deltaR += polarDisplaceData.radialWave * wave * normalizedR;
                        }
                    }
                    else if (displacePixels != null)
                    {
                        // 置换图模式：Shader风格UV变换 → 极坐标采样 → 通道选择
                        // Displace map mode: Shader-style UV transform → polar sample → channel select
                        Color displaceSample = SampleDisplaceMapPolar(displacePixels, displaceW, displaceH,
                            normalizedAngle, normalizedR);

                        // 根据通道模式获取径向/角度偏移
                        // Get radial/angular offset based on channel mode
                        SampleDisplaceChannels(displaceSample, polarDisplaceData.radialIntensity,
                            polarDisplaceData.angleIntensity, out deltaR, out deltaAngle);
                    }

                    // 应用偏移后的极坐标
                    // Polar coordinates after offset
                    float newR = Mathf.Max(0f, normalizedR + deltaR);
                    float newAngle = normalizedAngle + deltaAngle;
                    newAngle = newAngle - Mathf.Floor(newAngle);  // wrap to [0,1]

                    // 极坐标转回直角坐标
                    // Convert polar back to cartesian
                    float realAngle = (newAngle - 0.5f) * 2f * Mathf.PI;
                    float realRadius = newR * maxR;
                    float srcU = cx + Mathf.Cos(realAngle) * realRadius;
                    float srcV = cy + Mathf.Sin(realAngle) * realRadius;

                    // 边缘处理
                    // Edge handling
                    srcU = ApplyPolarEdge(srcU, edgeMode);
                    srcV = ApplyPolarEdge(srcV, edgeMode);

                    // 采样
                    // Sample
                    float px = srcU * (source.width - 1);
                    float py = srcV * (source.height - 1);

                    if (interpMode == PolarInterpMode.Bicubic)
                    {
                        resultPixels[y * width + x] = SampleBicubic(sourcePixels, source.width, source.height, px, py, edgeMode);
                    }
                    else
                    {
                        resultPixels[y * width + x] = SampleBilinearPolar(sourcePixels, source.width, source.height, px, py, edgeMode);
                    }
                }
            }

            result.SetPixels(resultPixels);
            result.Apply();
            return result;
        }

        /// <summary>
        /// 导出极坐标置换结果
        /// Export polar displacement result
        /// </summary>
        private void ExportPolarDisplace(bool overwrite)
        {
            if (polarDisplaceData.inputTexture == null)
            {
                EditorUtility.DisplayDialog("错误", "请先拖入源贴图", "确定");
                return;
            }

            if (polarDisplaceData.outputPreviews.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "没有可导出的结果，请先调整参数", "确定");
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(polarDisplaceData.inputTexture);
            string directory = Path.GetDirectoryName(sourcePath);

            int width = polarDisplaceData.inputTexture.width;
            int height = polarDisplaceData.inputTexture.height;

            Texture2D result = ApplyPolarDisplaceCPU(polarDisplaceData.inputTexture, width, height);

            if (result == null)
            {
                EditorUtility.DisplayDialog("错误", "极坐标置换处理失败", "确定");
                return;
            }

            byte[] bytes = result.EncodeToPNG();
            string outputPath;

            if (overwrite)
            {
                outputPath = sourcePath;
            }
            else
            {
                string baseName = Path.GetFileNameWithoutExtension(sourcePath);
                outputPath = Path.Combine(directory, $"{baseName}_polar.png");

                // 避免重名
                // Avoid name collision
                int counter = 1;
                while (File.Exists(outputPath))
                {
                    outputPath = Path.Combine(directory, $"{baseName}_polar_{counter}.png");
                    counter++;
                }
            }

            File.WriteAllBytes(outputPath, bytes);
            DestroyImmediate(result);

            AssetDatabase.Refresh();

            // 设置 TextureImporter
            // Set TextureImporter
            TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            EditorUtility.DisplayDialog("成功",
                overwrite ? "已覆盖原图" : $"已导出到:\n{outputPath}",
                "确定");
        }

        #endregion
    }
}
