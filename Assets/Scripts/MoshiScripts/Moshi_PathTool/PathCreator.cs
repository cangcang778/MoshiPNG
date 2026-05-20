using UnityEngine;
using System.Collections.Generic;

namespace Moshi.PathTool
{
    /// <summary>
    /// 路径数据容器
    /// 用于存储从外部导入的路径数据，支持运行时跟随和动画烘焙
    /// </summary>
    [ExecuteInEditMode]
    public class PathCreator : MonoBehaviour
    {
        [Header("路径设置")]
        [Tooltip("路径名称")]
        public string pathName = "Imported Path";

        [Tooltip("路径点列表（本地坐标）")]
        public List<Vector3> pathPoints = new List<Vector3>();

        [Tooltip("是否闭合路径")]
        public bool closePath = false;

        [Header("显示设置")]
        [Tooltip("路径颜色")]
        public Color pathColor = Color.cyan;

        [Tooltip("控制点颜色")]
        public Color pointColor = Color.yellow;

        [Tooltip("控制点大小")]
        [Range(0.1f, 2f)]
        public float pointSize = 0.3f;

        [Tooltip("显示距离标签")]
        public bool showDistances = true;

        [Tooltip("距离标签颜色")]
        public Color distanceColor = Color.white;

        [Tooltip("曲线采样精度")]
        [Range(5, 50)]
        public int curveSamples = 20;

        #region 公共方法 - 读取

        /// <summary>
        /// 获取世界坐标路径点
        /// </summary>
        public List<Vector3> GetWorldPoints()
        {
            List<Vector3> worldPoints = new List<Vector3>();
            foreach (var point in pathPoints)
            {
                worldPoints.Add(transform.TransformPoint(point));
            }
            return worldPoints;
        }

        /// <summary>
        /// 获取路径总长度
        /// </summary>
        public float GetPathLength()
        {
            return BezierCurve.CalculatePathLength(GetWorldPoints(), curveSamples);
        }

        /// <summary>
        /// 获取两点之间的距离
        /// </summary>
        public float GetSegmentLength(int index)
        {
            if (index < 0 || index >= pathPoints.Count - 1)
                return 0f;

            Vector3 p1 = transform.TransformPoint(pathPoints[index]);
            Vector3 p2 = transform.TransformPoint(pathPoints[index + 1]);
            return Vector3.Distance(p1, p2);
        }

        /// <summary>
        /// 获取所有线段距离
        /// </summary>
        public List<float> GetAllSegmentLengths()
        {
            List<float> lengths = new List<float>();
            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                lengths.Add(GetSegmentLength(i));
            }
            if (closePath && pathPoints.Count > 1)
            {
                Vector3 p1 = transform.TransformPoint(pathPoints[pathPoints.Count - 1]);
                Vector3 p2 = transform.TransformPoint(pathPoints[0]);
                lengths.Add(Vector3.Distance(p1, p2));
            }
            return lengths;
        }

        /// <summary>
        /// 根据归一化距离获取路径上的位置
        /// </summary>
        public Vector3 GetPointAtDistance(float normalizedDistance)
        {
            List<Vector3> points = GetWorldPoints();
            if (closePath && points.Count > 0)
            {
                points.Add(points[0]);
            }
            return BezierCurve.GetPointAtDistance(points, normalizedDistance, curveSamples);
        }

        /// <summary>
        /// 根据归一化距离获取路径上的切线方向
        /// </summary>
        public Vector3 GetTangentAtDistance(float normalizedDistance)
        {
            List<Vector3> points = GetWorldPoints();
            if (closePath && points.Count > 0)
            {
                points.Add(points[0]);
            }
            return BezierCurve.GetTangentAtDistance(points, normalizedDistance, curveSamples);
        }

        /// <summary>
        /// 获取采样后的平滑路径点
        /// </summary>
        public List<Vector3> GetSampledPath()
        {
            List<Vector3> points = GetWorldPoints();
            if (closePath && points.Count > 0)
            {
                points.Add(points[0]);
            }
            return BezierCurve.SampleSmoothPath(points, curveSamples);
        }

        #endregion

        #region 公共方法 - 供导入器使用

        /// <summary>
        /// 设置路径点
        /// </summary>
        public void SetPathPoints(List<Vector3> points, bool clearExisting = true)
        {
            if (clearExisting)
            {
                pathPoints.Clear();
            }
            pathPoints.AddRange(points);
        }

        /// <summary>
        /// 清空路径点
        /// </summary>
        public void ClearPathPoints()
        {
            pathPoints.Clear();
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (pathPoints.Count < 2)
                return;

            // 绘制平滑曲线
            List<Vector3> sampledPoints = GetSampledPath();
            Gizmos.color = pathColor;

            for (int i = 0; i < sampledPoints.Count - 1; i++)
            {
                Gizmos.DrawLine(sampledPoints[i], sampledPoints[i + 1]);
            }

            // 绘制控制点
            Gizmos.color = pointColor;
            foreach (var point in pathPoints)
            {
                Vector3 worldPoint = transform.TransformPoint(point);
                Gizmos.DrawSphere(worldPoint, pointSize * 0.5f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (pathPoints.Count < 2 || !showDistances)
                return;

            // 选中时显示距离标签
            DrawDistanceLabels();
        }

        private void DrawDistanceLabels()
        {
            UnityEditor.Handles.color = distanceColor;
            GUIStyle style = new GUIStyle();
            style.normal.textColor = distanceColor;
            style.fontSize = 12;
            style.alignment = TextAnchor.MiddleCenter;

            List<float> lengths = GetAllSegmentLengths();
            List<Vector3> worldPoints = GetWorldPoints();

            for (int i = 0; i < lengths.Count && i < worldPoints.Count - 1; i++)
            {
                Vector3 midPoint = (worldPoints[i] + worldPoints[i + 1]) * 0.5f;
                UnityEditor.Handles.Label(midPoint, $"{lengths[i]:F2}m", style);
            }

            // 闭合路径的最后一段
            if (closePath && worldPoints.Count > 1 && lengths.Count == worldPoints.Count)
            {
                Vector3 midPoint = (worldPoints[worldPoints.Count - 1] + worldPoints[0]) * 0.5f;
                UnityEditor.Handles.Label(midPoint, $"{lengths[lengths.Count - 1]:F2}m", style);
            }

            // 显示总长度
            if (worldPoints.Count > 0)
            {
                Vector3 labelPos = worldPoints[0] + Vector3.up * 0.5f;
                float totalLength = GetPathLength();
                UnityEditor.Handles.Label(labelPos, $"总长: {totalLength:F2}m", style);
            }
        }
#endif
    }
}
