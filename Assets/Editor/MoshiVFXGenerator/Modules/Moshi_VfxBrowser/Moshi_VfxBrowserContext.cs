#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if MOSHI_URP_INSTALLED
using UnityEngine.Rendering.Universal;
#endif
#if MOSHI_VFX_GRAPH_INSTALLED
using UnityEngine.VFX;
#endif

namespace MoshiVFXGenerator.VfxBrowser
{
    /// <summary>
    /// VFX 预览上下文 — 负责单个特效的渲染和模拟
    /// VFX preview context — handles rendering and simulation of a single effect
    /// 使用 PreviewRenderUtility 在独立的渲染环境中预览特效
    /// Uses PreviewRenderUtility to preview effects in an isolated render environment
    /// 支持 ParticleSystem / VFX Graph / Animator / UGUI Canvas 的同步播放
    /// Supports synchronized playback of ParticleSystem / VFX Graph / Animator / UGUI Canvas
    /// </summary>
    public class Moshi_VfxBrowserContext
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 渲染相关 / Rendering
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private PreviewRenderUtility _Preview;
        private GameObject _BgQuad;
        private GameObject _Instance;
        private Material _BgMat;
        private bool _IsUguiMode; // UGUI 模式（含 Canvas 的 Prefab） / UGUI mode (prefab with Canvas)
        private Vector3 _UguiCenter = Vector3.zero; // UGUI 模式下 Canvas 中心 / Canvas center in UGUI mode
        private float _UguiWorldSize = 2f;          // UGUI 模式下 Canvas 世界尺寸 / Canvas world size in UGUI mode

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 相机参数 / Camera Parameters
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private float _CamDist = 5.0f;
        private float _CamDistMul = 1.0f;
        private float _CamPitch = 30f;
        private float _CamYaw = -135f;
        private Color _BgColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        private Vector3 _Center = Vector3.zero;
        private Vector3 _PanOffset = Vector3.zero;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 播放控制 / Playback Control
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private float _CachedDuration = -1f;
        private float _Time = 0f;
        private ParticleSystem _MainPS;
        private List<Animator> _Animators = new List<Animator>();
        private List<ParticleSystem> _Particles = new List<ParticleSystem>();
        private List<ParticleSystem> _RootParticles = new List<ParticleSystem>();
        private List<TrailRenderer> _VfxTrails = new List<TrailRenderer>();
#if MOSHI_VFX_GRAPH_INSTALLED
        private List<VisualEffect> _VfxGraphs = new List<VisualEffect>();
#endif

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 移动模式 / Move Mode
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private float _MoveSpeed = 5f;
        private float _MoveAccelElapsed = 0f;
        private bool _MovePaused = false;
        private bool _MovingMode = false;
        private Vector3 _InitialCenter;
        private Vector3 _MoveDirection = Vector3.forward;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 静态缓存 / Static Cache
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private static Moshi_VfxBrowserWindow _CachedWindow;

        /// <summary>
        /// 高帧率模式开关
        /// High frame rate mode
        /// </summary>
        public static bool HighFrameRateMode { get; set; } = true;

        /************************************************************************************************************************/

        /// <summary>
        /// 创建预览上下文
        /// Create preview context
        /// </summary>
        public Moshi_VfxBrowserContext(GameObject prefab)
        {
            try
            {
                _Preview = new PreviewRenderUtility();
                _Preview.camera.fieldOfView = 60;
                _Preview.camera.nearClipPlane = 0.1f;
                _Preview.camera.farClipPlane = 200f;
                _Preview.camera.clearFlags = CameraClearFlags.SolidColor;
                _Preview.camera.backgroundColor = _BgColor;
                _Preview.camera.allowHDR = true;
                _Preview.camera.allowMSAA = false;

                // 配置 URP 相机（MOSHI_URP_INSTALLED 宏保护）
                // Configure URP camera (protected by MOSHI_URP_INSTALLED define)
                SetupUrpCamera();

                // 创建特效实例
                // Create effect instance
                _Instance = (GameObject)Object.Instantiate(prefab);
                _Instance.transform.position = Vector3.zero;
                _Instance.transform.rotation = Quaternion.identity;
                _Instance.hideFlags = HideFlags.HideAndDontSave;
                _Preview.AddSingleGO(_Instance);

                // 检测是否为 UGUI 特效（含 Canvas）
                // Detect UGUI VFX (with Canvas)
                var canvas = _Instance.GetComponentInChildren<Canvas>(true);
                _IsUguiMode = canvas != null;

                // 背景由相机 clearFlags=SolidColor + backgroundColor 绘制
                // 不创建几何 Quad，避免遮挡特效（如半透明/大范围效果）
                // Background is drawn by camera's SolidColor clear, no quad geometry
                // Avoids occluding VFX (especially transparent / wide effects)

                // 获取组件
                // Get components
                _MainPS = _Instance.GetComponent<ParticleSystem>();
                _Instance.GetComponentsInChildren(true, _Particles);
                _Instance.GetComponentsInChildren(true, _Animators);
#if MOSHI_VFX_GRAPH_INSTALLED
                _Instance.GetComponentsInChildren(true, _VfxGraphs);
#endif
                _Instance.GetComponentsInChildren(true, _VfxTrails);

                // 收集根粒子系统
                // Collect root particle systems
                foreach (var ps in _Particles)
                {
                    if (ps == null) continue;
                    var parent = ps.transform.parent;
                    if (parent == null || parent.GetComponent<ParticleSystem>() == null)
                    {
                        _RootParticles.Add(ps);
                    }
                }

                // UGUI 模式：切换 Canvas 到 WorldSpace
                // UGUI mode: switch Canvas to WorldSpace
                if (_IsUguiMode)
                {
                    SetupUguiCanvas();
                }

                CalculateBounds();
                InitializeAnimators();
                CacheDuration();
                PlayEffect();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Moshi_VfxBrowserContext initialization failed: {(prefab != null ? prefab.name : "null")}\n{e}");
                Cleanup();
            }
        }

        /************************************************************************************************************************/
        #region URP 兼容 / URP Compatibility
        /************************************************************************************************************************/

        /// <summary>
        /// 配置预览相机以兼容 URP 管线（激活 CameraOpaqueTexture / CameraDepthTexture）
        /// Configure preview camera for URP (enable CameraOpaqueTexture / CameraDepthTexture)
        /// </summary>
        private void SetupUrpCamera()
        {
#if MOSHI_URP_INSTALLED
            if (_Preview == null || _Preview.camera == null) return;
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return;

            var cam = _Preview.camera;
            var urpData = cam.gameObject.GetComponent<UniversalAdditionalCameraData>();
            if (urpData == null)
            {
                urpData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            var setting = Moshi_VfxBrowserSetting.Instance;
            urpData.requiresColorTexture = setting.EnableUrpColorTexture;
            urpData.requiresDepthTexture = setting.EnableUrpDepthTexture;
            urpData.renderShadows = false;
            urpData.antialiasing = AntialiasingMode.None;
            urpData.renderPostProcessing = false;
#endif
        }

        /// <summary>
        /// UGUI 模式初始化：将 Canvas 切换到 WorldSpace 模式
        /// UGUI mode init: switch all Canvas to WorldSpace mode
        /// PreviewRenderUtility 不支持 ScreenSpace-Overlay，必须切换到 WorldSpace
        /// PreviewRenderUtility does not support ScreenSpace-Overlay, must use WorldSpace
        /// </summary>
        private void SetupUguiCanvas()
        {
            if (_Instance == null) return;

            var canvases = _Instance.GetComponentsInChildren<Canvas>(true);

            // 找根 Canvas（通常 _Instance 本身或第一个）
            // Find root canvas (usually _Instance itself or the first one)
            Canvas rootCanvas = null;
            foreach (var canvas in canvases)
            {
                if (canvas == null) continue;
                if (rootCanvas == null) rootCanvas = canvas;

                // 强制 WorldSpace，绑定预览相机
                // Force WorldSpace, bind preview camera
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = _Preview.camera;

                // 将 Canvas 缩放到合理大小（1 px = 0.01 世界单位）
                // Scale Canvas to reasonable size (1 px = 0.01 world units)
                var rect = canvas.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localScale = Vector3.one * 0.01f;
                }
            }

            // 强制刷新 Canvas，让 CanvasRenderer 立刻更新几何体
            // Force canvas update so CanvasRenderer refreshes its mesh immediately
            Canvas.ForceUpdateCanvases();

            // 根据根 Canvas 的 RectTransform 直接算出特效的世界中心与包围尺寸
            // （不依赖不稳定的 UI Renderer.bounds）
            // Compute world center and size from root Canvas RectTransform directly
            // (UI Renderer.bounds is unreliable right after switching render mode)
            if (rootCanvas != null)
            {
                var rt = rootCanvas.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // Canvas 本地矩形大小 * 缩放 = 世界尺寸
                    // Canvas local rect size * scale = world size
                    Vector2 localSize = rt.rect.size;
                    Vector3 lossyScale = rt.lossyScale;
                    float worldW = Mathf.Abs(localSize.x * lossyScale.x);
                    float worldH = Mathf.Abs(localSize.y * lossyScale.y);
                    float worldSize = Mathf.Max(worldW, worldH);

                    if (worldSize <= 0.001f) worldSize = 2f; // 兜底 / fallback

                    _UguiWorldSize = worldSize;
                    _UguiCenter = rt.TransformPoint(rt.rect.center);
                }
            }
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/

        /// <summary>
        /// 创建背景平面
        /// Create background quad
        /// </summary>
        private void CreateBackgroundQuad()
        {
            _BgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _BgQuad.name = "_PreviewBackground";
            _BgQuad.hideFlags = HideFlags.HideAndDontSave;

            var collider = _BgQuad.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            // 根据当前渲染管线选择正确的 Shader（多级回退）
            // Select correct shader based on render pipeline (multi-level fallback)
            Shader unlitShader = FindBackgroundShader();

            _BgMat = new Material(unlitShader);
            _BgMat.hideFlags = HideFlags.HideAndDontSave;
            _BgMat.renderQueue = 1000;
            ApplyBgColor(_BgMat, _BgColor);

            var renderer = _BgQuad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _BgMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _Preview.AddSingleGO(_BgQuad);
        }

        /// <summary>
        /// 同时设置材质 _Color 和 _BaseColor（兼容 Built-in / URP Shader）
        /// Set both _Color and _BaseColor on material (compat: Built-in / URP Shader)
        /// </summary>
        private static void ApplyBgColor(Material mat, Color color)
        {
            if (mat == null) return;

            // Built-in Shader 使用 _Color / _MainTex
            // Built-in shaders use _Color / _MainTex
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }
            // URP Shader 使用 _BaseColor / _BaseMap
            // URP shaders use _BaseColor / _BaseMap
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            // 兜底：某些 Shader 的默认颜色属性
            // Fallback: some shaders use .color directly
            mat.color = color;
        }

        /// <summary>
        /// 查找背景 Shader（URP / Built-in 双路径 + 多级回退）
        /// Find background Shader (URP / Built-in with multi-level fallback)
        /// </summary>
        private static Shader FindBackgroundShader()
        {
            Shader sh = null;

#if MOSHI_URP_INSTALLED
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
            {
                sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            }
#endif

            // Built-in 回退
            // Built-in fallback
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Unlit/Texture");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Hidden/InternalErrorShader");

            return sh;
        }

        /// <summary>
        /// 更新背景平面的位置和方向
        /// Update background quad position and orientation
        /// </summary>
        private void UpdateBackgroundQuad(Quaternion cameraRotation)
        {
            if (_BgQuad == null) return;

            Vector3 backDir = cameraRotation * Vector3.forward;
            _BgQuad.transform.position = _Center + backDir * (_CamDist * 1.5f);
            _BgQuad.transform.rotation = cameraRotation;
            _BgQuad.transform.localScale = Vector3.one * (_CamDist * 3f);

            if (_BgMat != null)
            {
                ApplyBgColor(_BgMat, _BgColor);
            }
        }

        /// <summary>
        /// 计算特效包围盒，自动适配相机距离
        /// Calculate effect bounds, auto-fit camera distance
        /// </summary>
        private void CalculateBounds()
        {
            if (_Instance == null) return;

            // UGUI 模式：直接使用 SetupUguiCanvas 中算好的值
            // UGUI mode: use values computed in SetupUguiCanvas
            if (_IsUguiMode)
            {
                _Center = _UguiCenter;
                _InitialCenter = _Center;
                // 相机正交尺寸 ≈ 世界尺寸的一半，留一点边距
                // Orthographic size ≈ half world size, with small margin
                _CamDist = Mathf.Max(1f, _UguiWorldSize * 0.6f);
                return;
            }

            // 3D 特效自动取景：先跑几帧让粒子发射出来，再测 bounds
            // 3D VFX auto-fit: simulate a few frames so particles emit, then measure bounds
            PrewarmParticles();

            Bounds bounds = MeasureAggregateBounds(out bool hasValidBound);

            if (!hasValidBound)
            {
                // 空 Prefab 或纯空物体 — 使用默认距离
                // Empty prefab / pure empty GO — use default distance
                _Center = _Instance.transform.position;
                _InitialCenter = _Center;
                _CamDist = 3f;
                return;
            }

            _Center = bounds.center;
            _InitialCenter = _Center;

            // 格子占特效尺寸 80% 左右：相机距离 ≈ 半径 / tan(半 FOV) × 1.25 边距
            // Fit VFX to ~80% of cell: cam dist ≈ radius / tan(halfFOV) × 1.25 margin
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            radius = Mathf.Max(radius, 0.2f); // 小特效兜底 / fallback for tiny VFX

            // FOV=60° → halfFOV=30° → tan(30°)≈0.577
            // 距离公式：d = r / tan(halfFOV) × margin
            float halfFovRad = _Preview.camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanHalfFov = Mathf.Tan(halfFovRad);
            float margin = 1.25f;

            _CamDist = Mathf.Clamp(radius / tanHalfFov * margin, 1f, 500f);
        }

        /// <summary>
        /// 预热粒子系统：让粒子发射出来，得到稳定的 bounds
        /// Prewarm particle systems: emit particles for accurate bounds
        /// </summary>
        private void PrewarmParticles()
        {
            if (_Particles == null || _Particles.Count == 0) return;

            const float PREWARM_TIME = 0.5f;

            foreach (var ps in _Particles)
            {
                if (ps == null) continue;
                try
                {
                    ps.Simulate(PREWARM_TIME, true, true);
                }
                catch { /* 某些粒子系统预热失败静默跳过 / silently skip failed prewarm */ }
            }
        }

        /// <summary>
        /// 聚合所有 Renderer（Mesh + Particle + Trail）的包围盒
        /// Aggregate bounds of all Renderers (Mesh + Particle + Trail)
        /// </summary>
        private Bounds MeasureAggregateBounds(out bool hasValidBound)
        {
            hasValidBound = false;
            Bounds bounds = new Bounds(_Instance.transform.position, Vector3.one);

            var renderers = _Instance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;

                // 跳过 bounds 无效的 Renderer（粒子没发射 / 物体未激活）
                // Skip renderers with invalid bounds (particle not emitting / inactive)
                Bounds rb = r.bounds;
                if (rb.size.sqrMagnitude < 0.0001f) continue;

                // 跳过异常大的 bounds（某些 Mesh 导入异常会返回超大 bounds）
                // Skip unreasonably huge bounds (some mesh imports have corrupt bounds)
                if (rb.size.magnitude > 10000f) continue;

                if (!hasValidBound)
                {
                    bounds = rb;
                    hasValidBound = true;
                }
                else
                {
                    bounds.Encapsulate(rb);
                }
            }

            // 如果粒子 bounds 没有收到，从 ParticleSystem.main 估算
            // If no particle bounds, estimate from ParticleSystem.main parameters
            if (!hasValidBound && _Particles != null && _Particles.Count > 0)
            {
                float estimatedRadius = EstimateParticleRadius();
                if (estimatedRadius > 0f)
                {
                    bounds = new Bounds(_Instance.transform.position, Vector3.one * estimatedRadius * 2f);
                    hasValidBound = true;
                }
            }

            return bounds;
        }

        /// <summary>
        /// 根据粒子参数估算粒子最大覆盖半径（startSpeed × lifetime + startSize）
        /// Estimate particle max radius from main params
        /// </summary>
        private float EstimateParticleRadius()
        {
            float maxRadius = 0f;
            foreach (var ps in _Particles)
            {
                if (ps == null) continue;
                var main = ps.main;

                float speed = GetMinMaxMax(main.startSpeed);
                float life = GetMinMaxMax(main.startLifetime);
                float size = GetMinMaxMax(main.startSize);

                float range = speed * life + size;
                if (range > maxRadius) maxRadius = range;
            }
            return maxRadius;
        }

        /// <summary>
        /// 取 MinMaxCurve 的最大值（兼容常量/曲线/随机区间）
        /// Get max value of MinMaxCurve (const / curve / two constants / two curves)
        /// </summary>
        private static float GetMinMaxMax(ParticleSystem.MinMaxCurve mmc)
        {
            switch (mmc.mode)
            {
                case ParticleSystemCurveMode.Constant: return mmc.constant;
                case ParticleSystemCurveMode.TwoConstants: return Mathf.Max(mmc.constantMin, mmc.constantMax);
                case ParticleSystemCurveMode.Curve: return mmc.curveMultiplier;
                case ParticleSystemCurveMode.TwoCurves: return mmc.curveMultiplier;
                default: return 1f;
            }
        }

        /// <summary>
        /// 初始化所有 Animator
        /// Initialize all Animators
        /// </summary>
        private void InitializeAnimators()
        {
            foreach (var animator in _Animators)
            {
                if (animator == null) continue;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
            }
        }

        /// <summary>
        /// 播放特效（粒子 + 动画 + VFX Graph）
        /// Play effect (particles + animations + VFX Graph)
        /// </summary>
        private void PlayEffect()
        {
            _Time = 0f;
            _MoveAccelElapsed = 0f;

            if (_MovingMode && _Instance != null)
            {
                foreach (var ps in _Particles)
                {
                    if (ps == null) continue;
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(false);
                }

                foreach (var trail in _VfxTrails)
                {
                    if (trail == null) continue;
                    trail.emitting = false;
                }

#if MOSHI_VFX_GRAPH_INSTALLED
                foreach (var vfx in _VfxGraphs)
                {
                    if (vfx == null || vfx.visualEffectAsset == null) continue;
                    vfx.Reinit();
                }
#endif

                _Instance.transform.position = Vector3.zero;
                _Center = _InitialCenter + _PanOffset;
            }

            if (_MainPS != null)
            {
                _MainPS.Simulate(0f, true, true);
                _MainPS.Play(true);
            }
            else
            {
                foreach (var ps in _Particles)
                {
                    if (ps == null) continue;
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Play(false);
                }
            }

            if (_MovingMode && _Instance != null)
            {
                foreach (var trail in _VfxTrails)
                {
                    if (trail == null) continue;
                    trail.Clear();
                    trail.emitting = true;
                }
            }

            PlayAnimators();
            PlayVFXGraphs();
        }

        /// <summary>
        /// 播放所有 Animator 动画
        /// Play all Animator animations
        /// </summary>
        private void PlayAnimators()
        {
            foreach (var animator in _Animators)
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;

                animator.Rebind();
                animator.Update(0f);

                var clips = animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                {
                    animator.Play(clips[0].name, 0, 0f);
                }
            }
        }

        /// <summary>
        /// 播放所有 VFX Graph
        /// Play all VFX Graph
        /// </summary>
        private void PlayVFXGraphs()
        {
#if MOSHI_VFX_GRAPH_INSTALLED
            foreach (var vfx in _VfxGraphs)
            {
                if (vfx == null || vfx.visualEffectAsset == null) continue;
                vfx.Reinit();
                vfx.Play();
                vfx.pause = false;
            }
#endif
        }

        /// <summary>
        /// 缓存特效时长
        /// Cache effect duration
        /// </summary>
        private void CacheDuration()
        {
            if (_Instance == null)
            {
                _CachedDuration = 3f;
                return;
            }

            _CachedDuration = Moshi_VfxBrowserTools.GetEffectDuration(_Instance, true, true, true);

            if (_CachedDuration <= 0f)
            {
                _CachedDuration = 3f;
            }

            _CachedDuration += 0.5f;
        }

        private float GetLoopDuration()
        {
            return _CachedDuration + Moshi_VfxBrowserSetting.Instance.DefaulInterval;
        }

        /// <summary>
        /// 刷新预览窗口
        /// Repaint preview window
        /// </summary>
        private static void RepaintPreviewWindow()
        {
            if (_CachedWindow == null)
            {
                var windows = Resources.FindObjectsOfTypeAll<Moshi_VfxBrowserWindow>();
                if (windows.Length > 0)
                {
                    _CachedWindow = windows[0];
                }
            }

            if (_CachedWindow != null)
            {
                _CachedWindow.Repaint();
            }
        }

        /// <summary>
        /// 清除缓存的窗口引用
        /// Clear cached window reference
        /// </summary>
        public static void ClearCachedWindow()
        {
            _CachedWindow = null;
        }

        /************************************************************************************************************************/
        #region 公开接口 / Public Interface
        /************************************************************************************************************************/

        public void Restart() => PlayEffect();

        public void AdjustZoom(float delta)
        {
            _CamDist = Mathf.Max(0.5f, _CamDist + delta);
        }

        public float CamPitch => _CamPitch;
        public float CamYaw => _CamYaw;
        public bool IsUguiMode => _IsUguiMode;

        /// <summary>
        /// 获取特效中心在渲染画面中的像素偏移
        /// Get effect center's pixel offset in rendered image
        /// </summary>
        public Vector2 GetEffectScreenOffset(float cellHeight)
        {
            Vector3 offset = -_PanOffset;
            Quaternion rot = Quaternion.Euler(_CamPitch, _CamYaw, 0);
            float xProj = Vector3.Dot(offset, rot * Vector3.right);
            float yProj = Vector3.Dot(offset, rot * Vector3.up);

            float actualDist = _CamDist / _CamDistMul;
            float halfWorldH = actualDist * Mathf.Tan(30f * Mathf.Deg2Rad);
            float pixelPerWorld = cellHeight * 0.5f / halfWorldH;

            return new Vector2(xProj * pixelPerWorld, -yProj * pixelPerWorld);
        }

        public void AdjustRotation(float yawDelta, float pitchDelta)
        {
            _CamYaw += yawDelta;
            _CamPitch = Mathf.Clamp(_CamPitch + pitchDelta, -89f, 89f);
        }

        public void AdjustPan(float x, float y)
        {
            var cam = _Preview?.camera;
            if (cam == null) return;
            Vector3 offset = cam.transform.right * x * _CamDist + cam.transform.up * y * _CamDist;
            _Center += offset;
            _PanOffset += offset;
        }

        public void ResetCamera()
        {
            _CamPitch = 30f;
            _CamYaw = -135f;
        }

        public void SetBackgroundColor(Color color)
        {
            _BgColor = color;
            if (_Preview != null)
            {
                _Preview.camera.backgroundColor = color;
            }
        }

        public void SetDistanceMultiplier(float multiplier)
        {
            _CamDistMul = Mathf.Clamp(multiplier, 0.1f, 10f);
        }

        /// <summary>
        /// 单格缩放增量调整（相对乘法，滚轮每一步 ~10%）
        /// Per-cell zoom incremental adjustment (relative multiply, ~10% per wheel step)
        /// </summary>
        public void AdjustDistanceMultiplier(float delta)
        {
            // 用乘法方式让近处/远处的手感一致
            // Multiplicative adjustment keeps feel consistent near/far
            float factor = Mathf.Exp(delta * 0.1f);
            _CamDistMul = Mathf.Clamp(_CamDistMul * factor, 0.1f, 10f);
        }

        /// <summary>
        /// 当前距离乘数（单格缩放值）
        /// Current distance multiplier (per-cell zoom value)
        /// </summary>
        public float DistanceMultiplier => _CamDistMul;

        public void SetMovingMode(bool enabled)
        {
            _MovingMode = enabled;
            if (!enabled && _Instance != null)
            {
                _Instance.transform.position = Vector3.zero;
                _Center = _InitialCenter + _PanOffset;
            }
        }

        public bool IsMovingMode => _MovingMode;

        public void SetMoveSpeed(float speed)
        {
            float newSpeed = Mathf.Max(0f, speed);
            bool willMove = newSpeed > 0f;

            _MoveSpeed = newSpeed;

            if (willMove && !_MovingMode) SetMovingMode(true);
            else if (!willMove && _MovingMode) SetMovingMode(false);
        }

        public float MoveSpeed => _MoveSpeed;

        public void SetMovePaused(bool paused)
        {
            _MovePaused = paused;
        }

        public bool IsMovePaused => _MovePaused;

        public void SetMoveDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                _MoveDirection = direction.normalized;
            }
        }

        /// <summary>
        /// 每帧模拟更新
        /// Per-frame simulation update
        /// </summary>
        public void StepSimulation(float deltaTime, float rawDeltaTime, float loopIntervalRate)
        {
            if (_Instance == null) return;

            _Time += deltaTime;

            if (_Time >= GetLoopDuration() * loopIntervalRate)
            {
                PlayEffect();
            }

            if (_MovingMode && !_MovePaused)
            {
                _MoveAccelElapsed += deltaTime;
                float accelDuration = Moshi_VfxBrowserSetting.Instance.TailAccelDuration;
                float speedRatio = accelDuration > 0f ? Mathf.Clamp01(_MoveAccelElapsed / accelDuration) : 1f;
                float currentSpeed = _MoveSpeed * speedRatio;

                Vector3 movement = _MoveDirection * currentSpeed * deltaTime;
                _Instance.transform.position += movement;
                _Center += movement;
            }

            foreach (var ps in _RootParticles)
            {
                if (ps == null) continue;
                ps.Simulate(deltaTime, true, false, true);
            }

            foreach (var animator in _Animators)
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                animator.Update(deltaTime);
            }

            if (HighFrameRateMode)
            {
                RepaintPreviewWindow();
            }
        }

        /// <summary>
        /// 渲染预览画面
        /// Render preview image
        /// URP 下必须使用 Render(true) 让 SRP 回调生效
        /// Under URP, must use Render(true) to enable SRP callbacks
        /// </summary>
        public Texture Render(int width, int height)
        {
            if (_Preview == null || _Instance == null) return null;

            _Preview.camera.backgroundColor = _BgColor;
            _Preview.BeginPreview(new Rect(0, 0, width, height), GUIStyle.none);

            // UGUI 模式：使用正交相机（E3），看向 +Z 方向以正对 Canvas
            // UGUI mode: use orthographic camera (E3), looking +Z to face Canvas
            if (_IsUguiMode)
            {
                var cam = _Preview.camera;
                cam.orthographic = true;
                // 正交尺寸 = Canvas 世界半尺寸 × 1.1（留 10% 边距），再除以距离倍率
                // Ortho size = half world size × 1.1 (10% margin), divided by distance multiplier
                float halfSize = Mathf.Max(0.5f, _UguiWorldSize * 0.55f);
                cam.orthographicSize = halfSize / Mathf.Max(0.01f, _CamDistMul);
                // 面向 Canvas：从 Canvas 前方看过去
                // Face the Canvas from the front
                cam.transform.position = _Center - Vector3.forward * Mathf.Max(1f, _UguiWorldSize);
                cam.transform.rotation = Quaternion.identity;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 1000f;

                UpdateBackgroundQuad(Quaternion.identity);
            }
            else
            {
                var cam = _Preview.camera;
                cam.orthographic = false;
                float actualDist = _CamDist / _CamDistMul;
                Quaternion cameraRotation = Quaternion.Euler(_CamPitch, _CamYaw, 0);
                Vector3 camPos = _Center - cameraRotation * Vector3.forward * actualDist;

                cam.transform.position = camPos;
                cam.transform.rotation = cameraRotation;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 100f;

                UpdateBackgroundQuad(cameraRotation);
            }

            _Preview.lights[0].intensity = 1.2f;
            _Preview.lights[0].transform.rotation = Quaternion.Euler(60f, -120f, 0f);

            // URP 下必须 allowSRP = true 让 CopyColor / CopyDepth Pass 生效
            // Under URP, allowSRP = true is required for CopyColor / CopyDepth passes
            _Preview.Render(true, false);
            return _Preview.EndPreview();
        }

        /// <summary>
        /// 清理所有资源
        /// Cleanup all resources
        /// </summary>
        public void Cleanup()
        {
            _Particles.Clear();
            _RootParticles.Clear();
            _Animators.Clear();
#if MOSHI_VFX_GRAPH_INSTALLED
            _VfxGraphs.Clear();
#endif
            _VfxTrails.Clear();
            _MainPS = null;

            if (_BgQuad != null)
            {
                Object.DestroyImmediate(_BgQuad);
                _BgQuad = null;
            }

            if (_BgMat != null)
            {
                Object.DestroyImmediate(_BgMat);
                _BgMat = null;
            }

            if (_Instance != null)
            {
                Object.DestroyImmediate(_Instance);
                _Instance = null;
            }

            if (_Preview != null)
            {
                _Preview.Cleanup();
                _Preview = null;
            }
        }

        /// <summary>
        /// 析构函数（兜底清理）
        /// Destructor (fallback cleanup)
        /// </summary>
        ~Moshi_VfxBrowserContext()
        {
            if (_Preview != null || _Instance != null)
            {
                Debug.LogWarning("Moshi_VfxBrowserContext was not properly cleaned up, attempting to release resources...");
                try
                {
                    _Particles?.Clear();
                    _RootParticles?.Clear();
                    _Animators?.Clear();
#if MOSHI_VFX_GRAPH_INSTALLED
                    _VfxGraphs?.Clear();
#endif
                    _VfxTrails?.Clear();
                    _MainPS = null;

                    _Preview = null;
                    _Instance = null;
                    _BgQuad = null;
                    _BgMat = null;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Moshi_VfxBrowserContext cleanup failed during finalization: {e}");
                }
            }
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
    }
}
#endif
