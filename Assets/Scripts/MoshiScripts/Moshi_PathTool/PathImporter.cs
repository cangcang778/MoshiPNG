using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Moshi.PathTool
{
    /// <summary>
    /// 路径导入器 - 支持从3ds Max导出的各种格式导入路径
    /// </summary>
    public static class PathImporter
    {
        #region 从Mesh导入（FBX方式）

        /// <summary>
        /// 从Mesh导入路径点
        /// 适用于3ds Max中将样条线转换为网格后导出的FBX
        /// </summary>
        /// <param name="mesh">导入的网格</param>
        /// <param name="sortByDistance">是否按距离排序顶点（推荐开启）</param>
        /// <param name="removeDuplicates">是否移除重复顶点</param>
        /// <param name="duplicateThreshold">重复顶点判定阈值</param>
        /// <returns>路径点列表</returns>
        public static List<Vector3> ImportFromMesh(Mesh mesh, bool sortByDistance = true, 
            bool removeDuplicates = true, float duplicateThreshold = 0.001f)
        {
            if (mesh == null || mesh.vertexCount == 0)
            {
                Debug.LogWarning("[PathImporter] Mesh为空或没有顶点");
                return new List<Vector3>();
            }

            Vector3[] vertices = mesh.vertices;
            List<Vector3> points = new List<Vector3>(vertices);

            // 移除重复顶点
            if (removeDuplicates)
            {
                points = RemoveDuplicatePoints(points, duplicateThreshold);
            }

            // 按距离排序（将顶点按路径顺序排列）
            if (sortByDistance && points.Count > 1)
            {
                points = SortPointsByDistance(points);
            }

            Debug.Log($"[PathImporter] 从Mesh导入了 {points.Count} 个路径点");
            return points;
        }

        /// <summary>
        /// 从MeshFilter导入路径点
        /// </summary>
        public static List<Vector3> ImportFromMeshFilter(MeshFilter meshFilter, bool sortByDistance = true,
            bool removeDuplicates = true, float duplicateThreshold = 0.001f)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning("[PathImporter] MeshFilter为空");
                return new List<Vector3>();
            }

            return ImportFromMesh(meshFilter.sharedMesh, sortByDistance, removeDuplicates, duplicateThreshold);
        }

        /// <summary>
        /// 从GameObject导入路径点（自动查找Mesh）
        /// </summary>
        public static List<Vector3> ImportFromGameObject(GameObject go, bool sortByDistance = true,
            bool removeDuplicates = true, float duplicateThreshold = 0.001f)
        {
            if (go == null)
            {
                Debug.LogWarning("[PathImporter] GameObject为空");
                return new List<Vector3>();
            }

            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                return ImportFromMesh(meshFilter.sharedMesh, sortByDistance, removeDuplicates, duplicateThreshold);
            }

            SkinnedMeshRenderer skinnedMesh = go.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMesh != null && skinnedMesh.sharedMesh != null)
            {
                return ImportFromMesh(skinnedMesh.sharedMesh, sortByDistance, removeDuplicates, duplicateThreshold);
            }

            Debug.LogWarning("[PathImporter] GameObject上没有找到Mesh组件");
            return new List<Vector3>();
        }

        #endregion

        #region 从CSV导入

        /// <summary>
        /// 从CSV文件导入路径点
        /// 支持格式：
        /// - x,y,z（每行一个点）
        /// - index,x,y,z（带索引）
        /// - x,y,z,其他数据（忽略额外列）
        /// </summary>
        /// <param name="filePath">CSV文件路径</param>
        /// <param name="hasHeader">是否有表头行</param>
        /// <param name="swapYZ">是否交换Y和Z轴（3ds Max使用Z-up，Unity使用Y-up）</param>
        /// <param name="scale">缩放系数</param>
        /// <returns>路径点列表</returns>
        public static List<Vector3> ImportFromCSV(string filePath, bool hasHeader = true, 
            bool swapYZ = true, float scale = 1f)
        {
            List<Vector3> points = new List<Vector3>();

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[PathImporter] CSV文件不存在: {filePath}");
                return points;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                int startLine = hasHeader ? 1 : 0;

                for (int i = startLine; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    Vector3? point = ParseCSVLine(line, swapYZ, scale);
                    if (point.HasValue)
                    {
                        points.Add(point.Value);
                    }
                }

                Debug.Log($"[PathImporter] 从CSV导入了 {points.Count} 个路径点");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PathImporter] 读取CSV文件失败: {e.Message}");
            }

            return points;
        }

        /// <summary>
        /// 从CSV字符串内容导入路径点
        /// </summary>
        public static List<Vector3> ImportFromCSVContent(string csvContent, bool hasHeader = true,
            bool swapYZ = true, float scale = 1f)
        {
            List<Vector3> points = new List<Vector3>();

            if (string.IsNullOrEmpty(csvContent))
            {
                Debug.LogWarning("[PathImporter] CSV内容为空");
                return points;
            }

            string[] lines = csvContent.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            int startLine = hasHeader ? 1 : 0;

            for (int i = startLine; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                Vector3? point = ParseCSVLine(line, swapYZ, scale);
                if (point.HasValue)
                {
                    points.Add(point.Value);
                }
            }

            return points;
        }

        private static Vector3? ParseCSVLine(string line, bool swapYZ, float scale)
        {
            // 支持逗号、分号、制表符分隔
            string[] parts = line.Split(new[] { ',', ';', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

            // 尝试找到3个连续的数值
            List<float> values = new List<float>();
            foreach (string part in parts)
            {
                if (float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    values.Add(value);
                    if (values.Count >= 3) break;
                }
            }

            if (values.Count >= 3)
            {
                float x = values[0] * scale;
                float y = values[1] * scale;
                float z = values[2] * scale;

                if (swapYZ)
                {
                    // 3ds Max Z-up -> Unity Y-up
                    return new Vector3(x, z, y);
                }
                else
                {
                    return new Vector3(x, y, z);
                }
            }

            return null;
        }

        #endregion

        #region 从JSON导入

        /// <summary>
        /// 从JSON文件导入路径点
        /// 支持格式：
        /// - 数组格式: [{"x":0,"y":0,"z":0}, ...]
        /// - 对象格式: {"points":[{"x":0,"y":0,"z":0}, ...]}
        /// </summary>
        public static List<Vector3> ImportFromJSON(string filePath, bool swapYZ = true, float scale = 1f)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[PathImporter] JSON文件不存在: {filePath}");
                return new List<Vector3>();
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath);
                return ImportFromJSONContent(jsonContent, swapYZ, scale);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PathImporter] 读取JSON文件失败: {e.Message}");
                return new List<Vector3>();
            }
        }

        /// <summary>
        /// 从JSON字符串内容导入路径点
        /// </summary>
        public static List<Vector3> ImportFromJSONContent(string jsonContent, bool swapYZ = true, float scale = 1f)
        {
            List<Vector3> points = new List<Vector3>();

            if (string.IsNullOrEmpty(jsonContent))
            {
                Debug.LogWarning("[PathImporter] JSON内容为空");
                return points;
            }

            try
            {
                // 使用正则表达式解析坐标（避免依赖外部JSON库）
                // 匹配格式: "x":数值 或 x:数值
                string pattern = @"\{[^}]*?[""']?x[""']?\s*:\s*([-\d.]+)[^}]*?[""']?y[""']?\s*:\s*([-\d.]+)[^}]*?[""']?z[""']?\s*:\s*([-\d.]+)[^}]*?\}";
                
                MatchCollection matches = Regex.Matches(jsonContent, pattern, RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    if (float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        x *= scale;
                        y *= scale;
                        z *= scale;

                        if (swapYZ)
                        {
                            points.Add(new Vector3(x, z, y));
                        }
                        else
                        {
                            points.Add(new Vector3(x, y, z));
                        }
                    }
                }

                Debug.Log($"[PathImporter] 从JSON导入了 {points.Count} 个路径点");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PathImporter] 解析JSON失败: {e.Message}");
            }

            return points;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 移除重复的点
        /// </summary>
        public static List<Vector3> RemoveDuplicatePoints(List<Vector3> points, float threshold = 0.001f)
        {
            if (points == null || points.Count <= 1)
                return points;

            List<Vector3> result = new List<Vector3> { points[0] };
            float thresholdSqr = threshold * threshold;

            for (int i = 1; i < points.Count; i++)
            {
                bool isDuplicate = false;
                foreach (var existingPoint in result)
                {
                    if ((points[i] - existingPoint).sqrMagnitude < thresholdSqr)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    result.Add(points[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// 按最近邻距离排序点（贪婪算法）
        /// 将散乱的顶点按路径顺序排列
        /// </summary>
        public static List<Vector3> SortPointsByDistance(List<Vector3> points)
        {
            if (points == null || points.Count <= 2)
                return points;

            List<Vector3> remaining = new List<Vector3>(points);
            List<Vector3> sorted = new List<Vector3>();

            // 从第一个点开始
            sorted.Add(remaining[0]);
            remaining.RemoveAt(0);

            while (remaining.Count > 0)
            {
                Vector3 lastPoint = sorted[sorted.Count - 1];
                int nearestIndex = 0;
                float nearestDistSqr = float.MaxValue;

                for (int i = 0; i < remaining.Count; i++)
                {
                    float distSqr = (remaining[i] - lastPoint).sqrMagnitude;
                    if (distSqr < nearestDistSqr)
                    {
                        nearestDistSqr = distSqr;
                        nearestIndex = i;
                    }
                }

                sorted.Add(remaining[nearestIndex]);
                remaining.RemoveAt(nearestIndex);
            }

            return sorted;
        }

        /// <summary>
        /// 简化路径（减少点数）
        /// 使用Douglas-Peucker算法
        /// </summary>
        public static List<Vector3> SimplifyPath(List<Vector3> points, float tolerance = 0.1f)
        {
            if (points == null || points.Count <= 2)
                return points;

            return DouglasPeucker(points, 0, points.Count - 1, tolerance);
        }

        private static List<Vector3> DouglasPeucker(List<Vector3> points, int startIndex, int endIndex, float tolerance)
        {
            float maxDist = 0;
            int maxIndex = startIndex;

            Vector3 start = points[startIndex];
            Vector3 end = points[endIndex];
            Vector3 line = end - start;
            float lineLength = line.magnitude;

            if (lineLength > 0)
            {
                line /= lineLength;

                for (int i = startIndex + 1; i < endIndex; i++)
                {
                    Vector3 toPoint = points[i] - start;
                    float projection = Vector3.Dot(toPoint, line);
                    projection = Mathf.Clamp(projection, 0, lineLength);
                    Vector3 closestPoint = start + line * projection;
                    float dist = Vector3.Distance(points[i], closestPoint);

                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        maxIndex = i;
                    }
                }
            }

            List<Vector3> result = new List<Vector3>();

            if (maxDist > tolerance)
            {
                List<Vector3> left = DouglasPeucker(points, startIndex, maxIndex, tolerance);
                List<Vector3> right = DouglasPeucker(points, maxIndex, endIndex, tolerance);

                result.AddRange(left);
                result.RemoveAt(result.Count - 1); // 移除重复的中间点
                result.AddRange(right);
            }
            else
            {
                result.Add(points[startIndex]);
                result.Add(points[endIndex]);
            }

            return result;
        }

        /// <summary>
        /// 居中路径（将路径中心移到原点）
        /// </summary>
        public static List<Vector3> CenterPath(List<Vector3> points)
        {
            if (points == null || points.Count == 0)
                return points;

            Vector3 center = Vector3.zero;
            foreach (var point in points)
            {
                center += point;
            }
            center /= points.Count;

            List<Vector3> centered = new List<Vector3>();
            foreach (var point in points)
            {
                centered.Add(point - center);
            }

            return centered;
        }

        /// <summary>
        /// 缩放路径
        /// </summary>
        public static List<Vector3> ScalePath(List<Vector3> points, float scale)
        {
            if (points == null || points.Count == 0)
                return points;

            List<Vector3> scaled = new List<Vector3>();
            foreach (var point in points)
            {
                scaled.Add(point * scale);
            }

            return scaled;
        }

        #endregion
    }
}
