using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ParticleMeshTools
{
    /// <summary>
    /// 粒子系统Mesh可视化辅助工具
    /// </summary>
    public static class ParticleMeshVisualizationHelper
    {
        private static Dictionary<ParticleSystem, Color> highlightColors = new Dictionary<ParticleSystem, Color>();
        private static bool isHighlighting = false;
        
        /// <summary>
        /// 高亮显示选中对象中使用指定Mesh的组件
        /// </summary>
        public static void HighlightParticleSystemsWithMesh(Mesh mesh, Color highlightColor)
        {
            ClearHighlights();
            
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
                return;
            
            foreach (var obj in selectedObjects)
            {
                // 高亮粒子系统（绿色）
                var particleSystems = obj.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh == mesh)
                    {
                        highlightColors[ps] = Color.green; // 粒子系统用绿色
                    }
                }
                
                // 高亮MeshFilter（蓝色）
                var meshFilters = obj.GetComponentsInChildren<MeshFilter>();
                foreach (var mf in meshFilters)
                {
                    if (mf.sharedMesh == mesh)
                    {
                        // 为MeshFilter创建一个临时的粒子系统用于高亮显示
                        var tempPS = mf.gameObject.AddComponent<ParticleSystem>();
                        tempPS.Stop();
                        tempPS.gameObject.SetActive(false);
                        highlightColors[tempPS] = Color.cyan; // MeshFilter用青色
                    }
                }
            }
            
            isHighlighting = true;
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        /// <summary>
        /// 清除所有高亮
        /// </summary>
        public static void ClearHighlights()
        {
            highlightColors.Clear();
            isHighlighting = false;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }
        
        /// <summary>
        /// 在场景视图中绘制高亮
        /// </summary>
        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!isHighlighting) return;
            
            foreach (var kvp in highlightColors)
            {
                var ps = kvp.Key;
                var color = kvp.Value;
                
                if (ps == null) continue;
                
                var originalColor = Handles.color;
                Handles.color = color;
                
                // 绘制包围盒
                var bounds = GetParticleSystemBounds(ps);
                Handles.DrawWireCube(bounds.center, bounds.size);
                
                // 绘制标签
                Handles.Label(ps.transform.position, ps.name, EditorStyles.boldLabel);
                
                Handles.color = originalColor;
            }
        }
        
        /// <summary>
        /// 获取粒子系统的包围盒
        /// </summary>
        private static Bounds GetParticleSystemBounds(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }
            
            // 备用方案：使用Transform位置
            return new Bounds(ps.transform.position, Vector3.one);
        }
        
        /// <summary>
        /// 创建引用关系图数据
        /// </summary>
        public static string GenerateReferenceGraph(Dictionary<Mesh, MeshStatistics> statistics)
        {
            var graph = "# 粒子系统Mesh引用关系图\n\n";
            
            foreach (var kvp in statistics)
            {
                var mesh = kvp.Key;
                var stats = kvp.Value;
                
                graph += $"## {mesh.name}\n";
                graph += $"- 引用次数: {stats.referenceCount}\n";
                graph += $"- 文件路径: {stats.meshPath}\n";
                graph += $"- 引用的粒子系统:\n";
                
                foreach (var reference in stats.references)
                {
                    graph += $"  - {reference.particleSystemPath}\n";
                }
                
                graph += "\n";
            }
            
            return graph;
        }
        
        /// <summary>
        /// 导出引用关系到文件
        /// </summary>
        public static void ExportReferenceGraph(Dictionary<Mesh, MeshStatistics> statistics)
        {
            var graph = GenerateReferenceGraph(statistics);
            var path = EditorUtility.SaveFilePanel("导出引用关系图", "", "ParticleMeshReferences", "md");
            
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, graph);
                EditorUtility.DisplayDialog("导出成功", $"引用关系图已导出到: {path}", "确定");
            }
        }
        
        /// <summary>
        /// 在控制台输出详细的引用信息
        /// </summary>
        public static void LogDetailedReferences(Dictionary<Mesh, MeshStatistics> statistics)
        {
            Debug.Log("=== 粒子系统Mesh引用统计 ===");
            
            foreach (var kvp in statistics)
            {
                var mesh = kvp.Key;
                var stats = kvp.Value;
                
                Debug.Log($"Mesh: {mesh.name} (引用次数: {stats.referenceCount})");
                
                foreach (var reference in stats.references)
                {
                    Debug.Log($"  → {reference.particleSystemPath}", reference.particleSystem);
                }
            }
            
            Debug.Log("=== 统计完成 ===");
        }
        
        /// <summary>
        /// 选择所有使用指定Mesh的组件（粒子系统和MeshFilter，仅在选中对象范围内）
        /// </summary>
        public static void SelectParticleSystemsWithMesh(Mesh mesh)
        {
            var particleSystems = new List<GameObject>();
            var selectedObjects = Selection.gameObjects;
            
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先在Hierarchy面板中选择要搜索的GameObject", "确定");
                return;
            }
            
            foreach (var obj in selectedObjects)
            {
                // 选择粒子系统
                var allPS = obj.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in allPS)
                {
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh == mesh)
                    {
                        particleSystems.Add(ps.gameObject);
                    }
                }
                
                // 选择MeshFilter
                var allMF = obj.GetComponentsInChildren<MeshFilter>();
                foreach (var mf in allMF)
                {
                    if (mf.sharedMesh == mesh)
                    {
                        particleSystems.Add(mf.gameObject);
                    }
                }
            }
            
            if (particleSystems.Count > 0)
            {
                Selection.objects = particleSystems.ToArray();
                EditorUtility.DisplayDialog("选择完成", $"已选择 {particleSystems.Count} 个使用该Mesh的组件", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("未找到", "在选中对象中没有找到使用该Mesh的组件", "确定");
            }
        }
    }
}