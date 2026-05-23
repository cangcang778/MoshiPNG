using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ParticleMeshTools
{
    /// <summary>
    /// 粒子系统Mesh分析器
    /// </summary>
    public static class ParticleMeshAnalyzer
    {
        /// <summary>
        /// 分析选中GameObject中的Mesh引用（包括粒子系统和MeshFilter）
        /// </summary>
        public static Dictionary<Mesh, MeshStatistics> AnalyzeSelectedParticleSystems()
        {
            return AnalyzeSelectedParticleSystems(true, true);
        }
        
        /// <summary>
        /// 分析选中GameObject中的Mesh引用（支持过滤参数）
        /// </summary>
        /// <param name="includeMeshFilters">是否包含MeshFilter组件</param>
        /// <param name="includeParticleSystems">是否包含粒子系统组件</param>
        public static Dictionary<Mesh, MeshStatistics> AnalyzeSelectedParticleSystems(bool includeMeshFilters, bool includeParticleSystems)
        {
            var statistics = new Dictionary<Mesh, MeshStatistics>();
            var selectedObjects = Selection.gameObjects;
            
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                return statistics;
            }
            
            foreach (var obj in selectedObjects)
            {
                // 分析选中对象及其子对象中的粒子系统
                if (includeParticleSystems)
                {
                    var particleSystems = obj.GetComponentsInChildren<ParticleSystem>();
                    foreach (var ps in particleSystems)
                    {
                        AnalyzeParticleSystem(ps, statistics);
                    }
                }
                
                // 分析选中对象及其子对象中的MeshFilter
                if (includeMeshFilters)
                {
                    var meshFilters = obj.GetComponentsInChildren<MeshFilter>();
                    foreach (var mf in meshFilters)
                    {
                        AnalyzeMeshFilter(mf, statistics);
                    }
                }
            }
            
            return statistics;
        }
        
        /// <summary>
        /// 分析场景中所有粒子系统的Mesh引用（保留原功能作为备用）
        /// </summary>
        public static Dictionary<Mesh, MeshStatistics> AnalyzeSceneParticleSystems()
        {
            var statistics = new Dictionary<Mesh, MeshStatistics>();
            var particleSystems = Object.FindObjectsOfType<ParticleSystem>();
            
            foreach (var ps in particleSystems)
            {
                AnalyzeParticleSystem(ps, statistics);
            }
            
            return statistics;
        }
        
        /// <summary>
        /// 分析指定Prefab中的粒子系统Mesh引用
        /// </summary>
        public static Dictionary<Mesh, MeshStatistics> AnalyzePrefabParticleSystems(GameObject prefab)
        {
            var statistics = new Dictionary<Mesh, MeshStatistics>();
            var particleSystems = prefab.GetComponentsInChildren<ParticleSystem>();
            
            foreach (var ps in particleSystems)
            {
                AnalyzeParticleSystem(ps, statistics);
            }
            
            return statistics;
        }
        
        /// <summary>
        /// 分析单个粒子系统
        /// </summary>
        private static void AnalyzeParticleSystem(ParticleSystem ps, Dictionary<Mesh, MeshStatistics> statistics)
        {
            if (ps == null) return;
            
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            
            // 检查是否使用Mesh渲染模式
            if (renderer.renderMode != ParticleSystemRenderMode.Mesh) return;
            
            var mesh = renderer.mesh;
            if (mesh == null) return;
            
            // 创建引用记录
            var reference = new ParticleMeshReference(ps, mesh);
            
            // 添加到统计信息
            if (!statistics.ContainsKey(mesh))
            {
                statistics[mesh] = new MeshStatistics(mesh);
            }
            
            statistics[mesh].AddReference(reference);
        }
        
        /// <summary>
        /// 分析单个MeshFilter
        /// </summary>
        private static void AnalyzeMeshFilter(MeshFilter mf, Dictionary<Mesh, MeshStatistics> statistics)
        {
            if (mf == null) return;
            
            var mesh = mf.sharedMesh;
            if (mesh == null) return;
            
            // 创建引用记录
            var reference = new ParticleMeshReference(mf, mesh);
            
            // 添加到统计信息
            if (!statistics.ContainsKey(mesh))
            {
                statistics[mesh] = new MeshStatistics(mesh);
            }
            
            statistics[mesh].AddReference(reference);
        }
        
        // 缓存的Mesh GUID列表（避免重复调用FindAssets）
        private static string[] _cachedMeshGuids = null;
        private static double _lastMeshCacheTime = 0;
        private const double MESH_CACHE_EXPIRE_TIME = 30.0; // 缓存30秒过期
        
        /// <summary>
        /// 获取所有Mesh的GUID（带缓存）
        /// </summary>
        private static string[] GetCachedMeshGuids()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            if (_cachedMeshGuids == null || currentTime - _lastMeshCacheTime > MESH_CACHE_EXPIRE_TIME)
            {
                _cachedMeshGuids = AssetDatabase.FindAssets("t:Mesh");
                _lastMeshCacheTime = currentTime;
            }
            return _cachedMeshGuids;
        }
        
        /// <summary>
        /// 刷新Mesh缓存
        /// </summary>
        public static void RefreshMeshCache()
        {
            _cachedMeshGuids = null;
        }
        
        /// <summary>
        /// 获取Mesh资源总数
        /// </summary>
        public static int GetMeshAssetCount()
        {
            return GetCachedMeshGuids().Length;
        }
        
        /// <summary>
        /// 分页获取Mesh资源
        /// </summary>
        /// <param name="pageIndex">页码（从0开始）</param>
        /// <param name="pageSize">每页数量</param>
        /// <param name="searchTerm">搜索关键词（可选）</param>
        /// <returns>分页结果</returns>
        public static MeshPageResult GetMeshAssetsPage(int pageIndex, int pageSize, string searchTerm = null)
        {
            var guids = GetCachedMeshGuids();
            var result = new MeshPageResult();
            
            // 如果有搜索词，需要过滤
            if (!string.IsNullOrEmpty(searchTerm))
            {
                string lowerSearch = searchTerm.ToLower();
                var filteredMeshes = new List<Mesh>();
                
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    // 先检查路径名是否包含搜索词（避免加载资源）
                    if (path.ToLower().Contains(lowerSearch))
                    {
                        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (mesh != null)
                        {
                            filteredMeshes.Add(mesh);
                        }
                    }
                }
                
                result.totalCount = filteredMeshes.Count;
                result.meshes = filteredMeshes
                    .OrderBy(m => m.name)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            else
            {
                // 无搜索词，直接分页加载
                result.totalCount = guids.Length;
                int startIndex = pageIndex * pageSize;
                int endIndex = Mathf.Min(startIndex + pageSize, guids.Length);
                
                result.meshes = new List<Mesh>();
                for (int i = startIndex; i < endIndex; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh != null)
                    {
                        result.meshes.Add(mesh);
                    }
                }
                
                // 按名称排序当前页
                result.meshes = result.meshes.OrderBy(m => m.name).ToList();
            }
            
            result.pageIndex = pageIndex;
            result.pageSize = pageSize;
            result.totalPages = Mathf.CeilToInt((float)result.totalCount / pageSize);
            
            return result;
        }
        
        /// <summary>
        /// 获取所有可用的Mesh资源（保留兼容性，但不推荐使用）
        /// </summary>
        [System.Obsolete("Use GetMeshAssetsPage for better performance")]
        public static List<Mesh> GetAllMeshAssets()
        {
            var meshGuids = GetCachedMeshGuids();
            var meshes = new List<Mesh>();
            
            foreach (var guid in meshGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh != null)
                {
                    meshes.Add(mesh);
                }
            }
            
            return meshes.OrderBy(m => m.name).ToList();
        }
        
        /// <summary>
        /// 搜索包含指定名称的Mesh（保留兼容性，推荐使用GetMeshAssetsPage）
        /// </summary>
        public static List<Mesh> SearchMeshByName(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                // 返回第一页数据而不是全部
                return GetMeshAssetsPage(0, 100, null).meshes;
            }
            
            return GetMeshAssetsPage(0, 100, searchTerm).meshes;
        }
        
        /// <summary>
        /// 获取Mesh的详细信息
        /// </summary>
        public static string GetMeshInfo(Mesh mesh)
        {
            if (mesh == null) return "无效Mesh";
            
            // 性能优化：使用GetIndexCount代替triangles.Length，避免创建数组副本
            int triangleCount = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                triangleCount += (int)mesh.GetIndexCount(i);
            }
            triangleCount /= 3;
            
            return $"{mesh.name} (顶点: {mesh.vertexCount}, 三角形: {triangleCount})";
        }
    }
    
    /// <summary>
    /// Mesh分页查询结果
    /// </summary>
    public class MeshPageResult
    {
        public List<Mesh> meshes = new List<Mesh>();
        public int totalCount;
        public int pageIndex;
        public int pageSize;
        public int totalPages;
        
        public bool HasPreviousPage => pageIndex > 0;
        public bool HasNextPage => pageIndex < totalPages - 1;
    }
}