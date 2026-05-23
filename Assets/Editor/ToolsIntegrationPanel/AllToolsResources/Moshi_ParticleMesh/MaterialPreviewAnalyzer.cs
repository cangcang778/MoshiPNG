using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ParticleMeshTools
{
    /// <summary>
    /// Material预览分析器
    /// </summary>
    public static class MaterialPreviewAnalyzer
    {
        
        /// <summary>
        /// 分析粒子系统渲染器材质
        /// </summary>
        private static void AnalyzeParticleSystemRendererMaterials(GameObject obj, MaterialPreviewStatistics statistics)
        {
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem == null) return;
            
            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            
            // 检查单个材质
            if (renderer.sharedMaterial != null)
            {
                var item = new MaterialPreviewItem(obj, renderer.sharedMaterial, MaterialPreviewType.ParticleSystemRenderer, particleSystem);
                statistics.AddItem(item);
            }
            
            // 检查材质数组
            if (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 1)
            {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    var material = renderer.sharedMaterials[i];
                    if (material != null)
                    {
                        var item = new MaterialPreviewItem(obj, material, MaterialPreviewType.ParticleSystemRenderer, particleSystem);
                        item.materialIndex = i;
                        statistics.AddItem(item);
                    }
                }
            }
        }
        
        /// <summary>
        /// 分析粒子系统拖尾材质
        /// </summary>
        private static void AnalyzeParticleSystemTrailMaterials(GameObject obj, MaterialPreviewStatistics statistics)
        {
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem == null) return;
            
            var trails = particleSystem.trails;
            if (!trails.enabled) return;
            
            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            
            // 检查拖尾材质
            if (renderer.trailMaterial != null)
            {
                var item = new MaterialPreviewItem(obj, renderer.trailMaterial, MaterialPreviewType.ParticleSystemTrail, particleSystem);
                statistics.AddItem(item);
            }
        }
        
        /// <summary>
        /// 分析MeshRenderer材质
        /// </summary>
        private static void AnalyzeMeshRendererMaterials(GameObject obj, MaterialPreviewStatistics statistics)
        {
            var meshRenderer = obj.GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;
            
            // 检查单个材质
            if (meshRenderer.sharedMaterial != null)
            {
                var item = new MaterialPreviewItem(obj, meshRenderer.sharedMaterial, MaterialPreviewType.MeshRenderer, meshRenderer);
                statistics.AddItem(item);
            }
            
            // 检查材质数组
            if (meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 1)
            {
                for (int i = 0; i < meshRenderer.sharedMaterials.Length; i++)
                {
                    var material = meshRenderer.sharedMaterials[i];
                    if (material != null)
                    {
                        var item = new MaterialPreviewItem(obj, material, MaterialPreviewType.MeshRenderer, meshRenderer);
                        item.materialIndex = i;
                        statistics.AddItem(item);
                    }
                }
            }
        }
        
        /// <summary>
        /// 分析SkinnedMeshRenderer材质
        /// </summary>
        private static void AnalyzeSkinnedMeshRendererMaterials(GameObject obj, MaterialPreviewStatistics statistics)
        {
            var skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer == null) return;
            
            // 检查单个材质
            if (skinnedMeshRenderer.sharedMaterial != null)
            {
                var item = new MaterialPreviewItem(obj, skinnedMeshRenderer.sharedMaterial, MaterialPreviewType.SkinnedMeshRenderer, skinnedMeshRenderer);
                statistics.AddItem(item);
            }
            
            // 检查材质数组
            if (skinnedMeshRenderer.sharedMaterials != null && skinnedMeshRenderer.sharedMaterials.Length > 1)
            {
                for (int i = 0; i < skinnedMeshRenderer.sharedMaterials.Length; i++)
                {
                    var material = skinnedMeshRenderer.sharedMaterials[i];
                    if (material != null)
                    {
                        var item = new MaterialPreviewItem(obj, material, MaterialPreviewType.SkinnedMeshRenderer, skinnedMeshRenderer);
                        item.materialIndex = i;
                        statistics.AddItem(item);
                    }
                }
            }
        }
        
        /// <summary>
        /// 批量替换Material
        /// </summary>
        public static int ReplaceMaterials(Material oldMaterial, Material newMaterial, List<MaterialPreviewItem> targetItems)
        {
            if (oldMaterial == null || targetItems == null) return 0;
            
            int replaceCount = 0;
            
            foreach (var item in targetItems)
            {
                if (item.gameObject == null) continue;
                
                replaceCount += ReplaceParticleSystemRendererMaterials(item.gameObject, oldMaterial, newMaterial);
                replaceCount += ReplaceParticleSystemTrailMaterials(item.gameObject, oldMaterial, newMaterial);
                replaceCount += ReplaceMeshRendererMaterials(item.gameObject, oldMaterial, newMaterial);
                replaceCount += ReplaceSkinnedMeshRendererMaterials(item.gameObject, oldMaterial, newMaterial);
            }
            
            return replaceCount;
        }
        
        /// <summary>
        /// 替换粒子系统渲染器材质
        /// </summary>
        private static int ReplaceParticleSystemRendererMaterials(GameObject obj, Material oldMaterial, Material newMaterial)
        {
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem == null) return 0;
            
            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return 0;
            
            int replaceCount = 0;
            
            // 替换单个材质
            if (renderer.sharedMaterial == oldMaterial)
            {
                Undo.RecordObject(renderer, "Replace Particle System Renderer Material");
                renderer.sharedMaterial = newMaterial;
                replaceCount++;
            }
            
            // 替换材质数组中的材质
            var materials = renderer.sharedMaterials.ToArray();
            bool hasChanges = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == oldMaterial)
                {
                    materials[i] = newMaterial;
                    hasChanges = true;
                    replaceCount++;
                }
            }
            
            if (hasChanges)
            {
                Undo.RecordObject(renderer, "Replace Particle System Renderer Materials Array");
                renderer.sharedMaterials = materials;
            }
            
            return replaceCount;
        }
        
        /// <summary>
        /// 替换粒子系统拖尾材质
        /// </summary>
        private static int ReplaceParticleSystemTrailMaterials(GameObject obj, Material oldMaterial, Material newMaterial)
        {
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem == null) return 0;
            
            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return 0;
            
            int replaceCount = 0;
            
            // 替换拖尾材质
            if (renderer.trailMaterial == oldMaterial)
            {
                Undo.RecordObject(renderer, "Replace Particle System Trail Material");
                renderer.trailMaterial = newMaterial;
                replaceCount++;
            }
            
            return replaceCount;
        }
        
        /// <summary>
        /// 替换MeshRenderer材质
        /// </summary>
        private static int ReplaceMeshRendererMaterials(GameObject obj, Material oldMaterial, Material newMaterial)
        {
            var meshRenderer = obj.GetComponent<MeshRenderer>();
            if (meshRenderer == null) return 0;
            
            int replaceCount = 0;
            
            // 替换单个材质
            if (meshRenderer.sharedMaterial == oldMaterial)
            {
                Undo.RecordObject(meshRenderer, "Replace MeshRenderer Material");
                meshRenderer.sharedMaterial = newMaterial;
                replaceCount++;
            }
            
            // 替换材质数组中的材质
            var materials = meshRenderer.sharedMaterials.ToArray();
            bool hasChanges = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == oldMaterial)
                {
                    materials[i] = newMaterial;
                    hasChanges = true;
                    replaceCount++;
                }
            }
            
            if (hasChanges)
            {
                Undo.RecordObject(meshRenderer, "Replace MeshRenderer Materials Array");
                meshRenderer.sharedMaterials = materials;
            }
            
            return replaceCount;
        }
        
        /// <summary>
        /// 替换SkinnedMeshRenderer材质
        /// </summary>
        private static int ReplaceSkinnedMeshRendererMaterials(GameObject obj, Material oldMaterial, Material newMaterial)
        {
            var skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer == null) return 0;
            
            int replaceCount = 0;
            
            // 替换单个材质
            if (skinnedMeshRenderer.sharedMaterial == oldMaterial)
            {
                Undo.RecordObject(skinnedMeshRenderer, "Replace SkinnedMeshRenderer Material");
                skinnedMeshRenderer.sharedMaterial = newMaterial;
                replaceCount++;
            }
            
            // 替换材质数组中的材质
            var materials = skinnedMeshRenderer.sharedMaterials.ToArray();
            bool hasChanges = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == oldMaterial)
                {
                    materials[i] = newMaterial;
                    hasChanges = true;
                    replaceCount++;
                }
            }
            
            if (hasChanges)
            {
                Undo.RecordObject(skinnedMeshRenderer, "Replace SkinnedMeshRenderer Materials Array");
                skinnedMeshRenderer.sharedMaterials = materials;
            }
            
            return replaceCount;
        }
    }
}