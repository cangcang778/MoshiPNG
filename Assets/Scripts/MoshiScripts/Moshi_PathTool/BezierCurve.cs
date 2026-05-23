using UnityEngine;
using System.Collections.Generic;

namespace Moshi.PathTool
{
    /// <summary>
    /// 贝塞尔曲线工具类
    /// </summary>
    public static class BezierCurve
    {
        /// <summary>
        /// 计算二次贝塞尔曲线上的点
        /// </summary>
        public static Vector3 QuadraticPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float u = 1 - t;
            return u * u * p0 + 2 * u * t * p1 + t * t * p2;
        }

        /// <summary>
        /// 计算三次贝塞尔曲线上的点
        /// </summary>
        public static Vector3 CubicPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
        }

        /// <summary>
        /// 计算三次贝塞尔曲线的切线
        /// </summary>
        public static Vector3 CubicTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;

            Vector3 tangent = 3 * uu * (p1 - p0) + 6 * u * t * (p2 - p1) + 3 * tt * (p3 - p2);
            return tangent.normalized;
        }

        /// <summary>
        /// 获取Catmull-Rom样条的控制点（用于平滑路径）
        /// </summary>
        public static void GetCatmullRomControlPoints(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, 
            out Vector3 c1, out Vector3 c2)
        {
            c1 = p1 + (p2 - p0) / 6f;
            c2 = p2 - (p3 - p1) / 6f;
        }

        /// <summary>
        /// 在路径点之间采样平滑曲线
        /// </summary>
        public static List<Vector3> SampleSmoothPath(List<Vector3> points, int samplesPerSegment = 10)
        {
            if (points == null || points.Count < 2)
                return new List<Vector3>(points ?? new List<Vector3>());

            List<Vector3> result = new List<Vector3>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = points[Mathf.Max(0, i - 1)];
                Vector3 p1 = points[i];
                Vector3 p2 = points[Mathf.Min(points.Count - 1, i + 1)];
                Vector3 p3 = points[Mathf.Min(points.Count - 1, i + 2)];

                GetCatmullRomControlPoints(p0, p1, p2, p3, out Vector3 c1, out Vector3 c2);

                for (int j = 0; j < samplesPerSegment; j++)
                {
                    float t = j / (float)samplesPerSegment;
                    result.Add(CubicPoint(p1, c1, c2, p2, t));
                }
            }

            // 添加最后一个点
            result.Add(points[points.Count - 1]);

            return result;
        }

        /// <summary>
        /// 计算路径总长度
        /// </summary>
        public static float CalculatePathLength(List<Vector3> points, int samplesPerSegment = 10)
        {
            if (points == null || points.Count < 2)
                return 0f;

            List<Vector3> sampledPoints = SampleSmoothPath(points, samplesPerSegment);
            float length = 0f;

            for (int i = 0; i < sampledPoints.Count - 1; i++)
            {
                length += Vector3.Distance(sampledPoints[i], sampledPoints[i + 1]);
            }

            return length;
        }

        /// <summary>
        /// 计算两点之间的线段长度（直线距离）
        /// </summary>
        public static float CalculateSegmentLength(Vector3 p1, Vector3 p2)
        {
            return Vector3.Distance(p1, p2);
        }

        /// <summary>
        /// 根据距离百分比获取路径上的位置
        /// </summary>
        public static Vector3 GetPointAtDistance(List<Vector3> points, float normalizedDistance, int samplesPerSegment = 10)
        {
            if (points == null || points.Count == 0)
                return Vector3.zero;

            if (points.Count == 1)
                return points[0];

            List<Vector3> sampledPoints = SampleSmoothPath(points, samplesPerSegment);
            
            // 计算累积距离
            List<float> distances = new List<float> { 0f };
            float totalLength = 0f;

            for (int i = 0; i < sampledPoints.Count - 1; i++)
            {
                totalLength += Vector3.Distance(sampledPoints[i], sampledPoints[i + 1]);
                distances.Add(totalLength);
            }

            if (totalLength <= 0f)
                return points[0];

            float targetDistance = normalizedDistance * totalLength;

            // 找到目标距离所在的线段
            for (int i = 0; i < distances.Count - 1; i++)
            {
                if (targetDistance >= distances[i] && targetDistance <= distances[i + 1])
                {
                    float segmentLength = distances[i + 1] - distances[i];
                    if (segmentLength <= 0f)
                        return sampledPoints[i];

                    float t = (targetDistance - distances[i]) / segmentLength;
                    return Vector3.Lerp(sampledPoints[i], sampledPoints[i + 1], t);
                }
            }

            return sampledPoints[sampledPoints.Count - 1];
        }

        /// <summary>
        /// 根据距离百分比获取路径上的切线方向
        /// </summary>
        public static Vector3 GetTangentAtDistance(List<Vector3> points, float normalizedDistance, int samplesPerSegment = 10)
        {
            if (points == null || points.Count < 2)
                return Vector3.forward;

            List<Vector3> sampledPoints = SampleSmoothPath(points, samplesPerSegment);

            // 计算累积距离
            List<float> distances = new List<float> { 0f };
            float totalLength = 0f;

            for (int i = 0; i < sampledPoints.Count - 1; i++)
            {
                totalLength += Vector3.Distance(sampledPoints[i], sampledPoints[i + 1]);
                distances.Add(totalLength);
            }

            if (totalLength <= 0f)
                return Vector3.forward;

            float targetDistance = normalizedDistance * totalLength;

            // 找到目标距离所在的线段
            for (int i = 0; i < distances.Count - 1; i++)
            {
                if (targetDistance >= distances[i] && targetDistance <= distances[i + 1])
                {
                    Vector3 direction = (sampledPoints[i + 1] - sampledPoints[i]).normalized;
                    return direction == Vector3.zero ? Vector3.forward : direction;
                }
            }

            // 返回最后一段的方向
            if (sampledPoints.Count >= 2)
            {
                Vector3 direction = (sampledPoints[sampledPoints.Count - 1] - sampledPoints[sampledPoints.Count - 2]).normalized;
                return direction == Vector3.zero ? Vector3.forward : direction;
            }

            return Vector3.forward;
        }
    }
}
