using UnityEngine;

namespace Moshi.PathTool
{
    /// <summary>
    /// 跟随模式
    /// </summary>
    public enum FollowMode
    {
        Once,       // 单次播放
        Loop,       // 循环播放
        PingPong    // 往返播放
    }

    /// <summary>
    /// 旋转模式
    /// </summary>
    public enum RotationMode
    {
        None,           // 不旋转
        Path,           // 沿路径方向
        PathWithUp,     // 沿路径方向（保持向上）
        LookAt          // 看向目标
    }

    /// <summary>
    /// 路径跟随器组件
    /// </summary>
    public class PathFollower : MonoBehaviour
    {
        [Header("路径设置")]
        [Tooltip("要跟随的路径")]
        public PathCreator pathCreator;

        [Header("移动设置")]
        [Tooltip("移动速度")]
        public float speed = 5f;

        [Tooltip("速度曲线")]
        public AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);

        [Tooltip("跟随模式")]
        public FollowMode followMode = FollowMode.Once;

        [Tooltip("自动开始")]
        public bool autoStart = false;

        [Header("旋转设置")]
        [Tooltip("旋转模式")]
        public RotationMode rotationMode = RotationMode.Path;

        [Tooltip("旋转速度")]
        public float rotationSpeed = 10f;

        [Tooltip("看向目标（用于LookAt模式）")]
        public Transform lookAtTarget;

        [Tooltip("向上方向")]
        public Vector3 upDirection = Vector3.up;

        [Header("状态")]
        [SerializeField]
        private float progress = 0f;

        [SerializeField]
        private bool isPlaying = false;

        [SerializeField]
        private bool isReversing = false;

        private float pathLength = 0f;

        /// <summary>
        /// 当前进度（0-1）
        /// </summary>
        public float Progress
        {
            get => progress;
            set => progress = Mathf.Clamp01(value);
        }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying => isPlaying;

        private void Start()
        {
            if (autoStart)
            {
                Play();
            }
        }

        private void Update()
        {
            if (!isPlaying || pathCreator == null || pathCreator.pathPoints.Count < 2)
                return;

            UpdateMovement();
        }

        private void UpdateMovement()
        {
            // 计算路径长度
            if (pathLength <= 0f)
            {
                pathLength = pathCreator.GetPathLength();
                if (pathLength <= 0f)
                    return;
            }

            // 计算速度
            float currentSpeed = speed * speedCurve.Evaluate(progress);
            float deltaProgress = (currentSpeed / pathLength) * Time.deltaTime;

            // 更新进度
            if (isReversing)
            {
                progress -= deltaProgress;
            }
            else
            {
                progress += deltaProgress;
            }

            // 处理边界
            HandleBoundary();

            // 更新位置
            UpdatePosition();

            // 更新旋转
            UpdateRotation();
        }

        private void HandleBoundary()
        {
            switch (followMode)
            {
                case FollowMode.Once:
                    if (progress >= 1f || progress <= 0f)
                    {
                        progress = Mathf.Clamp01(progress);
                        Stop();
                    }
                    break;

                case FollowMode.Loop:
                    if (progress >= 1f)
                    {
                        progress = 0f;
                    }
                    else if (progress <= 0f)
                    {
                        progress = 1f;
                    }
                    break;

                case FollowMode.PingPong:
                    if (progress >= 1f)
                    {
                        progress = 1f;
                        isReversing = true;
                    }
                    else if (progress <= 0f)
                    {
                        progress = 0f;
                        isReversing = false;
                    }
                    break;
            }
        }

        private void UpdatePosition()
        {
            Vector3 targetPosition = pathCreator.GetPointAtDistance(progress);
            transform.position = targetPosition;
        }

        private void UpdateRotation()
        {
            switch (rotationMode)
            {
                case RotationMode.None:
                    break;

                case RotationMode.Path:
                    Vector3 tangent = pathCreator.GetTangentAtDistance(progress);
                    if (isReversing)
                        tangent = -tangent;
                    if (tangent != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(tangent);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    }
                    break;

                case RotationMode.PathWithUp:
                    Vector3 tangentUp = pathCreator.GetTangentAtDistance(progress);
                    if (isReversing)
                        tangentUp = -tangentUp;
                    if (tangentUp != Vector3.zero)
                    {
                        Quaternion targetRotationUp = Quaternion.LookRotation(tangentUp, upDirection);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotationUp, rotationSpeed * Time.deltaTime);
                    }
                    break;

                case RotationMode.LookAt:
                    if (lookAtTarget != null)
                    {
                        Vector3 lookDirection = (lookAtTarget.position - transform.position).normalized;
                        if (lookDirection != Vector3.zero)
                        {
                            Quaternion targetRotationLook = Quaternion.LookRotation(lookDirection, upDirection);
                            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotationLook, rotationSpeed * Time.deltaTime);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 开始播放
        /// </summary>
        public void Play()
        {
            if (pathCreator == null || pathCreator.pathPoints.Count < 2)
            {
                Debug.LogWarning("PathFollower: 没有有效的路径！");
                return;
            }

            isPlaying = true;
            pathLength = pathCreator.GetPathLength();
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            isPlaying = false;
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
            isReversing = false;
        }

        /// <summary>
        /// 重置到起点
        /// </summary>
        public void Reset()
        {
            Stop();
            progress = 0f;
            if (pathCreator != null && pathCreator.pathPoints.Count > 0)
            {
                UpdatePosition();
            }
        }

        /// <summary>
        /// 重置到终点
        /// </summary>
        public void ResetToEnd()
        {
            Stop();
            progress = 1f;
            if (pathCreator != null && pathCreator.pathPoints.Count > 0)
            {
                UpdatePosition();
            }
        }

        /// <summary>
        /// 设置进度并更新位置
        /// </summary>
        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            if (pathCreator != null && pathCreator.pathPoints.Count > 0)
            {
                UpdatePosition();
                UpdateRotation();
            }
        }

        /// <summary>
        /// 反向播放
        /// </summary>
        public void Reverse()
        {
            isReversing = !isReversing;
        }

        /// <summary>
        /// 设置新路径
        /// </summary>
        public void SetPath(PathCreator newPath)
        {
            pathCreator = newPath;
            pathLength = 0f;
            Reset();
        }
    }
}
