using UnityEngine;
using System.Collections.Generic;

namespace Moshi.PathTool
{
    /// <summary>
    /// 路径阵列循环模式
    /// </summary>
    public enum ArrayCycleMode
    {
        Loop,        // 持续循环，到终点后回到起点
        PingPong,    // 来回往返
        Manual       // 手动控制相位
    }

    /// <summary>
    /// 路径阵列循环器
    /// 将多个对象沿路径均匀排布，并使它们沿路径循环运动
    /// 支持三轴旋转曲线(含扭曲)、透明度渐变、大小渐变
    /// </summary>
    [ExecuteAlways]
    public class PathArrayCycler : MonoBehaviour
    {
        [Header("路径设置")]
        [Tooltip("要跟随的路径")]
        public PathCreator pathCreator;

        [Header("阵列设置")]
        [Tooltip("阵列模板（用于创建实例）")]
        public GameObject prefab;

        [Tooltip("阵列数量")]
        [Range(1, 200)]
        public int count = 8;

        [Tooltip("生成的对象列表（自动填充）")]
        public List<GameObject> instances = new List<GameObject>();

        [Header("运动设置")]
        [Tooltip("循环速度（完整循环/秒）")]
        [Range(0f, 10f)]
        public float speed = 1f;

        [Tooltip("运动模式")]
        public ArrayCycleMode cycleMode = ArrayCycleMode.Loop;

        [Tooltip("Play时自动开始")]
        public bool playOnStart = true;

        [Header("旋转设置")]
        [Tooltip("对象是否朝向路径切线方向")]
        public bool alignToPath = true;

        [Tooltip("向上方向")]
        public Vector3 upDirection = Vector3.up;

        [Tooltip("每个实例的固定偏移旋转(Euler角度)，叠加在曲线之前")]
        public Vector3 perInstanceRotation = Vector3.zero;

        [Tooltip("自旋速度（度/秒），绕自身Z轴持续旋转")]
        [Range(-720f, 720f)]
        public float rotationSpeed = 0f;

        [Header("旋转曲线(沿路径变化的 Euler 角度)")]
        [Tooltip("启用沿路径旋转变化")]
        public bool enableRotationCurve = false;

        [Tooltip("X轴旋转曲线(Pitch): 横轴=路径位置(0→1), 纵轴=角度(度)")]
        public AnimationCurve rotationXOverPath = AnimationCurve.Constant(0f, 1f, 0f);

        [Tooltip("Y轴旋转曲线(Yaw): 横轴=路径位置(0→1), 纵轴=角度(度)")]
        public AnimationCurve rotationYOverPath = AnimationCurve.Constant(0f, 1f, 0f);

        [Tooltip("Z轴旋转曲线(Roll/扭曲): 横轴=路径位置(0→1), 纵轴=角度(度)。绕路径切线方向扭转")]
        public AnimationCurve rotationZOverPath = AnimationCurve.Constant(0f, 1f, 0f);

        [Header("透明渐变")]
        [Tooltip("启用沿路径透明度渐变")]
        public bool enableAlphaGradient = false;

        [Tooltip("透明度曲线: 横轴=路径位置(0起点→1终点), 纵轴=Alpha(0透明→1不透明)")]
        public AnimationCurve alphaOverPath = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("大小渐变")]
        [Tooltip("启用沿路径大小渐变")]
        public bool enableScaleGradient = false;

        [Tooltip("缩放曲线: 横轴=路径位置(0起点→1终点), 纵轴=缩放倍率")]
        public AnimationCurve scaleOverPath = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("生长动画")]
        [Tooltip("启用后实例按顺序逐个出现（scale: 0→正常值），模拟生长效果")]
        public bool enableGrowAnimation = false;

        [Tooltip("生长完成所需时间（秒），0=自动使用1圈的时间")]
        [Range(0f, 30f)]
        public float growthDuration = 0f;

        [Header("状态")]
        [SerializeField]
        [Range(0f, 1f)]
        private float phaseOffset = 0f;

        [SerializeField]
        private bool isPlaying = false;

        private float pingPongDirection = 1f;
        private float accumulatedSpin = 0f;
        private float growthProgress = 0f;

        // 缓存：每个实例的 Renderer 引用，避免每帧 GetComponent
        private List<Renderer[]> instanceRenderers = new List<Renderer[]>();

        // 缓存：每个实例的材质实例（生成时创建一次，避免每帧 r.material）
        private List<Material[]> instanceMaterials = new List<Material[]>();

        public float PhaseOffset
        {
            get => phaseOffset;
            set => phaseOffset = value % 1f;
        }

        public bool IsPlaying => isPlaying;

        private void Start()
        {
            if (Application.isPlaying && playOnStart && instances.Count > 0)
                Play();
        }

        private void Update()
        {
            if (pathCreator == null || instances.Count == 0 || !isPlaying)
                return;

            AdvancePhase();
            UpdateAllPositions();
        }

        private void AdvancePhase()
        {
            float delta = speed * Time.deltaTime;

            switch (cycleMode)
            {
                case ArrayCycleMode.Loop:
                    phaseOffset = (phaseOffset + delta) % 1f;
                    break;

                case ArrayCycleMode.PingPong:
                    phaseOffset += delta * pingPongDirection;
                    if (phaseOffset >= 1f)
                    {
                        phaseOffset = 1f;
                        pingPongDirection = -1f;
                    }
                    else if (phaseOffset <= 0f)
                    {
                        phaseOffset = 0f;
                        pingPongDirection = 1f;
                    }
                    break;
            }

            if (rotationSpeed != 0f)
                accumulatedSpin += rotationSpeed * Time.deltaTime;

            // 生长进度累积
            if (enableGrowAnimation && growthProgress < 1f)
            {
                float growTime = growthDuration > 0.001f ? growthDuration : (1f / Mathf.Max(speed, 0.001f));
                growthProgress = Mathf.Min(1f, growthProgress + Time.deltaTime / growTime);
            }
        }

        /// <summary>
        /// 更新所有实例位置、旋转、缩放、透明度
        /// </summary>
        public void UpdateAllPositions()
        {
            if (pathCreator == null) return;

            int n = instances.Count;
            for (int i = 0; i < n; i++)
            {
                if (instances[i] == null) continue;

                float t = (phaseOffset + (float)i / n) % 1f;
                if (t < 0f) t += 1f;

                Transform trans = instances[i].transform;
                trans.position = pathCreator.GetPointAtDistance(t);

                // === 旋转 ===
                if (alignToPath)
                {
                    Vector3 tangent = pathCreator.GetTangentAtDistance(t);
                    if (tangent != Vector3.zero)
                    {
                        Quaternion pathRotation = Quaternion.LookRotation(tangent, upDirection);

                        // 固定偏移旋转(三轴)
                        Quaternion fixedRot = Quaternion.Euler(perInstanceRotation);

                        // 沿路径的旋转曲线(Euler角度) → 叠加在路径朝向上
                        Quaternion curveRot = enableRotationCurve
                            ? Quaternion.Euler(
                                rotationXOverPath.Evaluate(t),
                                rotationYOverPath.Evaluate(t),
                                rotationZOverPath.Evaluate(t))
                            : Quaternion.identity;

                        // 持续自旋(绕自身Z轴)
                        Quaternion spinRot = Quaternion.Euler(0f, 0f, accumulatedSpin);

                        trans.rotation = pathRotation * fixedRot * curveRot * spinRot;
                    }
                }
                else
                {
                    Quaternion fixedRot = Quaternion.Euler(perInstanceRotation);

                    Quaternion curveRot = enableRotationCurve
                        ? Quaternion.Euler(
                            rotationXOverPath.Evaluate(t),
                            rotationYOverPath.Evaluate(t),
                            rotationZOverPath.Evaluate(t))
                        : Quaternion.identity;

                    Quaternion spinRot = Quaternion.Euler(0f, 0f, accumulatedSpin);
                    trans.rotation = fixedRot * curveRot * spinRot;
                }

                // === 缩放渐变 ===
                if (enableScaleGradient)
                {
                    float scaleMul = scaleOverPath.Evaluate(t);
                    trans.localScale = Vector3.one * scaleMul;
                }
                else if (!enableGrowAnimation)
                {
                    // 未开启任何缩放相关功能时恢复默认缩放
                    trans.localScale = Vector3.one;
                }

                // === 生长动画 ===
                if (enableGrowAnimation && isPlaying && count > 1)
                {
                    float activationThreshold = (float)i / (count - 1);
                    if (growthProgress < activationThreshold)
                        trans.localScale = Vector3.zero;
                    else if (!enableScaleGradient)
                        trans.localScale = Vector3.one;
                }

                // === 透明渐变 ===
                if (enableAlphaGradient)
                {
                    float alpha = alphaOverPath.Evaluate(t);
                    ApplyAlpha(i, alpha);
                }
                else
                {
                    // 关闭渐变时恢复全不透明
                    ResetAlpha(i);
                }
            }
        }

        /// <summary>
        /// 对第 i 个实例应用透明度
        /// </summary>
        private void ApplyAlpha(int index, float alpha)
        {
            if (index < 0 || index >= instanceMaterials.Count) return;

            Material[] materials = instanceMaterials[index];
            if (materials == null) return;

            foreach (var mat in materials)
            {
                if (mat == null) continue;
                SetMaterialAlpha(mat, alpha);
            }

            // 同时处理 ParticleSystem（没有缓存material）
            if (index < instanceRenderers.Count)
            {
                Renderer[] renderers = instanceRenderers[index];
                if (renderers != null)
                {
                    foreach (var r in renderers)
                    {
                        if (r is ParticleSystemRenderer)
                        {
                            var ps = r.GetComponent<ParticleSystem>();
                            if (ps != null)
                            {
                                var main = ps.main;
                                Color sc = main.startColor.color;
                                sc.a = alpha;
                                main.startColor = new ParticleSystem.MinMaxGradient(sc);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 重置第 i 个实例的透明度为 1（不透明）
        /// </summary>
        private void ResetAlpha(int index)
        {
            if (index < 0 || index >= instanceMaterials.Count) return;

            Material[] materials = instanceMaterials[index];
            if (materials == null) return;

            foreach (var mat in materials)
            {
                if (mat == null) continue;
                SetMaterialAlpha(mat, 1f);
            }
        }

        /// <summary>
        /// 设置单个材质的透明度（兼容 Built-in / URP / HDRP）
        /// </summary>
        private static void SetMaterialAlpha(Material mat, float alpha)
        {
            // Built-in RP: _Color
            if (mat.HasProperty("_Color"))
            {
                Color c = mat.GetColor("_Color");
                c.a = alpha;
                mat.SetColor("_Color", c);
            }
            // URP / HDRP: _BaseColor
            else if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            }
        }

        /// <summary>
        /// 生成阵列实例
        /// </summary>
        public void GenerateInstances()
        {
            ClearInstances();

            if (prefab == null || pathCreator == null)
            {
                Debug.LogWarning("PathArrayCycler: 需要设置 prefab 和 pathCreator");
                return;
            }

            if (pathCreator.pathPoints.Count < 2)
            {
                Debug.LogWarning("PathArrayCycler: 路径至少需要2个点");
                return;
            }

            accumulatedSpin = 0f;
            growthProgress = 0f;

            for (int i = 0; i < count; i++)
            {
                GameObject go;
#if UNITY_EDITOR
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
#else
                go = Instantiate(prefab, transform);
#endif
                go.name = $"{prefab.name}_{i:D3}";
                instances.Add(go);

                Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
                instanceRenderers.Add(renderers);

                // 为每个 Renderer 预创建材质实例 + 缓存
                Material[] mats = new Material[renderers.Length];
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] != null && !(renderers[r] is ParticleSystemRenderer))
                        mats[r] = renderers[r].material; // 触发材质实例化，只执行一次
                }
                instanceMaterials.Add(mats);
            }

            phaseOffset = 0f;
            UpdateAllPositions();
        }

        /// <summary>
        /// 清除所有实例
        /// </summary>
        public void ClearInstances()
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                if (instances[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(instances[i]);
                    else
                        DestroyImmediate(instances[i]);
                }
            }
            instances.Clear();
            instanceRenderers.Clear();
            instanceMaterials.Clear();
        }

        /// <summary>
        /// 开始播放
        /// </summary>
        public void Play()
        {
            if (pathCreator == null || pathCreator.pathPoints.Count < 2)
            {
                Debug.LogWarning("PathArrayCycler: 没有有效路径");
                return;
            }
            if (instances.Count == 0)
                GenerateInstances();

            isPlaying = true;
            pingPongDirection = 1f;
            growthProgress = 0f;
        }

        /// <summary>
        /// 暂停
        /// </summary>
        public void Pause() => isPlaying = false;

        /// <summary>
        /// 停止并重置
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
            phaseOffset = 0f;
            accumulatedSpin = 0f;
            growthProgress = 0f;
            pingPongDirection = 1f;
            UpdateAllPositions();
        }

        /// <summary>
        /// 设置相位并刷新
        /// </summary>
        public void SetPhase(float phase)
        {
            phaseOffset = Mathf.Clamp01(phase);
            UpdateAllPositions();
        }
    }
}
