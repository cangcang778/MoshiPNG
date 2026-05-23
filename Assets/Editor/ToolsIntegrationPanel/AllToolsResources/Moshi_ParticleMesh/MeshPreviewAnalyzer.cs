﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ParticleMeshTools
{
    /// <summary>
    /// Mesh预览分析器
    /// </summary>
    public static class MeshPreviewAnalyzer
    {
        /// <summary>
        /// 分析选中GameObject及其子节点的所有Mesh应用
        /// </summary>
        public static MeshPreviewStatistics AnalyzeSelectedObjects()
        {
            var statistics = new MeshPreviewStatistics();
            
            var selectedObjects = UnityEditor.Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                return statistics;
            }
            
            foreach (var obj in selectedObjects)
            {
                AnalyzeGameObject(obj, statistics);
            }
            
            return statistics;
        }
        
        /// <summary>
        /// 分析选中GameObject及其子节点的所有Mesh应用（包括Shape节点）
        /// </summary>
        public static MeshPreviewStatistics AnalyzeSelectedObjectsWithShapeNodes()
        {
            var statistics = new MeshPreviewStatistics();
            
            var selectedObjects = UnityEditor.Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                return statistics;
            }
            
            foreach (var obj in selectedObjects)
            {
                AnalyzeGameObjectWithShapeNodes(obj, statistics);
            }
            
            return statistics;
        }
        
        /// <summary>
        /// 分析单个GameObject及其子节点
        /// </summary>
        private static void AnalyzeGameObject(GameObject obj, MeshPreviewStatistics statistics)
        {
            // 分析粒子系统
            var particleSystems = obj.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                AnalyzeParticleSystem(ps, statistics);
            }
            
            // 分析MeshFilter
            var meshFilters = obj.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                AnalyzeMeshFilter(mf, statistics);
            }
            
            // 分析SkinnedMeshRenderer
            var skinnedMeshes = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in skinnedMeshes)
            {
                AnalyzeSkinnedMeshRenderer(smr, statistics);
            }
        }
        
        /// <summary>
        /// 分析单个GameObject及其子节点（包括Shape节点）
        /// </summary>
        private static void AnalyzeGameObjectWithShapeNodes(GameObject obj, MeshPreviewStatistics statistics)
        {
            // 分析粒子系统（包含Shape节点）
            var particleSystems = obj.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                AnalyzeParticleSystemWithShape(ps, statistics);
            }
            
            // 分析MeshFilter
            var meshFilters = obj.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                AnalyzeMeshFilter(mf, statistics);
            }
            
            // 分析SkinnedMeshRenderer
            var skinnedMeshes = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in skinnedMeshes)
            {
                AnalyzeSkinnedMeshRenderer(smr, statistics);
            }
        }
        
        private static void AnalyzeParticleSystem(ParticleSystem ps, MeshPreviewStatistics statistics)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            
            if (renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh != null)
            {
                var item = new MeshPreviewItem(ps.gameObject, renderer.mesh, MeshPreviewType.ParticleSystem, ps);
                statistics.AddItem(item);
            }
        }
        
        /// <summary>
        /// 分析粒子系统（包括Shape节点和残留检测）
        /// </summary>
        private static void AnalyzeParticleSystemWithShape(ParticleSystem ps, MeshPreviewStatistics statistics)
        {
            if (ps == null) return;
            
            // 分析Renderer节点的Mesh
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                AnalyzeRendererMeshes(ps, renderer, statistics);
            }
            
            // 分析Shape节点的Mesh
            var shape = ps.shape;
            AnalyzeShapeMesh(ps, shape, statistics);
        }
        
        /// <summary>
        /// 分析Renderer节点的Mesh设置
        /// </summary>
        private static void AnalyzeRendererMeshes(ParticleSystem ps, ParticleSystemRenderer renderer, MeshPreviewStatistics statistics)
        {
            // 检查单个Mesh引用
            if (renderer.mesh != null)
            {
                var item = new MeshPreviewItem(ps.gameObject, renderer.mesh, MeshPreviewType.ParticleSystem, ps);
                item.renderMode = renderer.renderMode;
                
                statistics.AddItem(item);
            }
            
            // 检查多Mesh数组
            var meshes = new Mesh[4]; // Unity最多支持4个Mesh
            var meshCount = renderer.GetMeshes(meshes);
            
            for (int i = 0; i < meshCount; i++)
            {
                if (meshes[i] != null)
                {
                    var item = new MeshPreviewItem(ps.gameObject, meshes[i], MeshPreviewType.ParticleSystem, ps);
                    item.renderMode = renderer.renderMode;
                    item.meshIndex = i;
                    
                    statistics.AddItem(item);
                }
            }
        }
        
        /// <summary>
        /// 分析Shape节点的Mesh设置
        /// </summary>
        private static void AnalyzeShapeMesh(ParticleSystem ps, ParticleSystem.ShapeModule shape, MeshPreviewStatistics statistics)
        {
            if (shape.mesh != null)
            {
                var item = new MeshPreviewItem(ps.gameObject, shape.mesh, MeshPreviewType.ParticleSystemShape, ps);
                item.shapeType = shape.shapeType;
                item.isShapeEnabled = shape.enabled;
                
                statistics.AddItem(item);
            }
        }
        
        private static void AnalyzeMeshFilter(MeshFilter mf, MeshPreviewStatistics statistics)
        {
            if (mf.sharedMesh == null) return;
            
            var item = new MeshPreviewItem(mf.gameObject, mf.sharedMesh, MeshPreviewType.MeshFilter, mf);
            statistics.AddItem(item);
        }
        
        private static void AnalyzeSkinnedMeshRenderer(SkinnedMeshRenderer smr, MeshPreviewStatistics statistics)
        {
            if (smr.sharedMesh == null) return;
            
            var item = new MeshPreviewItem(smr.gameObject, smr.sharedMesh, MeshPreviewType.SkinnedMeshRenderer, smr);
            statistics.AddItem(item);
        }
        
        /// <summary>
        /// 替换指定项的Mesh
        /// </summary>
        public static bool ReplaceMesh(MeshPreviewItem item, Mesh newMesh, bool preserveProperties = true)
        {
            if (item == null || newMesh == null) return false;
            
            try
            {
                UnityEditor.Undo.RecordObject(item.component, $"Replace {item.type} Mesh");
                
                switch (item.type)
                {
                    case MeshPreviewType.ParticleSystem:
                        var ps = item.component as ParticleSystem;
                        var renderer = ps.GetComponent<ParticleSystemRenderer>();
                        if (renderer != null)
                        {
                            // 处理多Mesh数组
                            if (item.meshIndex >= 0)
                            {
                                var meshes = new Mesh[4];
                                var meshCount = renderer.GetMeshes(meshes);
                                
                                if (item.meshIndex < meshCount)
                                {
                                    meshes[item.meshIndex] = newMesh;
                                    renderer.SetMeshes(meshes.Take(meshCount).ToArray());
                                    return true;
                                }
                            }
                            else
                            {
                                // 处理单个Mesh
                                renderer.mesh = newMesh;
                                return true;
                            }
                        }
                        break;
                        
                    case MeshPreviewType.ParticleSystemShape:
                        var psShape = item.component as ParticleSystem;
                        if (psShape != null)
                        {
                            var shape = psShape.shape;
                            shape.mesh = newMesh;
                            return true;
                        }
                        break;
                        
                    case MeshPreviewType.MeshFilter:
                        var mf = item.component as MeshFilter;
                        mf.sharedMesh = newMesh;
                        return true;
                        
                    case MeshPreviewType.SkinnedMeshRenderer:
                        var smr = item.component as SkinnedMeshRenderer;
                        smr.sharedMesh = newMesh;
                        return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"替换Mesh失败: {e.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// 批量替换Mesh
        /// </summary>
        public static int BatchReplaceMesh(List<MeshPreviewItem> items, Mesh originalMesh, Mesh newMesh, bool preserveProperties = true)
        {
            if (items == null || originalMesh == null || newMesh == null) return 0;
            
            int successCount = 0;
            var targetItems = items.FindAll(item => item.mesh == originalMesh);
            
            foreach (var item in targetItems)
            {
                if (ReplaceMesh(item, newMesh, preserveProperties))
                {
                    successCount++;
                }
            }
            
            return successCount;
        }
    }
}