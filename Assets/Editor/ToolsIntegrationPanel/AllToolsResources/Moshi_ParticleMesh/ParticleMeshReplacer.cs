using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ParticleMeshTools
{
    /// <summary>
    /// 粒子系统Mesh替换器
    /// </summary>
    public static class ParticleMeshReplacer
    {
        private static List<ReplaceOperation> operationHistory = new List<ReplaceOperation>();
        
        /// <summary>
        /// 批量替换Mesh
        /// </summary>
        public static ReplaceOperation ReplaceMesh(Mesh originalMesh, Mesh newMesh, List<ParticleMeshReference> references, bool preserveProperties = true)
        {
            if (originalMesh == null || newMesh == null || references == null)
                return null;
            
            var operation = new ReplaceOperation(originalMesh, newMesh);
            
            foreach (var reference in references)
            {
                if (reference.referenceType == MeshReferenceType.ParticleSystem)
                {
                    if (reference.particleSystem == null) continue;
                    
                    var renderer = reference.particleSystem.GetComponent<ParticleSystemRenderer>();
                    if (renderer == null) continue;
                    
                    // 记录原始属性
                    var originalScale = Vector3.one;
                    var originalColor = Color.white;
                    Material originalMaterial = null;
                    
                    if (preserveProperties)
                    {
                        originalScale = reference.scale;
                        originalColor = reference.color;
                        originalMaterial = reference.material;
                    }
                    
                    // 记录撤销操作
                    Undo.RecordObject(renderer, "Replace Particle System Mesh");
                    if (preserveProperties && reference.particleSystem != null)
                    {
                        Undo.RecordObject(reference.particleSystem, "Preserve Particle Properties");
                    }
                    
                    // 替换Mesh
                    renderer.mesh = newMesh;
                    
                    // 保持原有属性
                    if (preserveProperties)
                    {
                        // 保持材质
                        if (originalMaterial != null)
                        {
                            renderer.sharedMaterial = originalMaterial;
                        }
                        
                        // 保持缩放
                        var shape = reference.particleSystem.shape;
                        shape.scale = originalScale;
                        
                        // 保持颜色
                        var main = reference.particleSystem.main;
                        var startColor = main.startColor;
                        startColor.color = originalColor;
                        main.startColor = startColor;
                    }
                }
                else if (reference.referenceType == MeshReferenceType.MeshFilter)
                {
                    if (reference.meshFilter == null) continue;
                    
                    // 记录原始属性
                    var originalScale = Vector3.one;
                    Material originalMaterial = null;
                    
                    if (preserveProperties)
                    {
                        originalScale = reference.scale;
                        originalMaterial = reference.material;
                    }
                    
                    // 记录撤销操作
                    Undo.RecordObject(reference.meshFilter, "Replace MeshFilter Mesh");
                    if (preserveProperties && reference.meshFilter != null)
                    {
                        Undo.RecordObject(reference.meshFilter.transform, "Preserve MeshFilter Scale");
                        var meshRenderer = reference.meshFilter.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            Undo.RecordObject(meshRenderer, "Preserve MeshFilter Material");
                        }
                    }
                    
                    // 替换Mesh
                    reference.meshFilter.sharedMesh = newMesh;
                    
                    // 保持原有属性
                    if (preserveProperties)
                    {
                        // 保持缩放
                        reference.meshFilter.transform.localScale = originalScale;
                        
                        // 保持材质
                        if (originalMaterial != null)
                        {
                            var meshRenderer = reference.meshFilter.GetComponent<MeshRenderer>();
                            if (meshRenderer != null)
                            {
                                meshRenderer.sharedMaterial = originalMaterial;
                            }
                        }
                    }
                }
                
                operation.affectedReferences.Add(reference);
            }
            
            // 添加到操作历史
            operationHistory.Add(operation);
            
            // 标记场景为已修改
            EditorUtility.SetDirty(null);
            
            return operation;
        }
        
        /// <summary>
        /// 撤销最后一次替换操作
        /// </summary>
        public static bool UndoLastOperation()
        {
            if (operationHistory.Count == 0)
                return false;
            
            Undo.PerformUndo();
            operationHistory.RemoveAt(operationHistory.Count - 1);
            return true;
        }
        
        /// <summary>
        /// 获取操作历史
        /// </summary>
        public static List<ReplaceOperation> GetOperationHistory()
        {
            return new List<ReplaceOperation>(operationHistory);
        }
        
        /// <summary>
        /// 清空操作历史
        /// </summary>
        public static void ClearOperationHistory()
        {
            operationHistory.Clear();
        }
        
        /// <summary>
        /// 验证替换操作的有效性
        /// </summary>
        public static bool ValidateReplacement(Mesh originalMesh, Mesh newMesh, List<ParticleMeshReference> references)
        {
            if (originalMesh == null || newMesh == null || references == null || references.Count == 0)
                return false;
            
            // 检查所有引用是否仍然有效
            foreach (var reference in references)
            {
                if (reference.referenceType == MeshReferenceType.ParticleSystem)
                {
                    if (reference.particleSystem == null)
                        continue;
                    
                    var renderer = reference.particleSystem.GetComponent<ParticleSystemRenderer>();
                    if (renderer == null || renderer.mesh != originalMesh)
                        continue;
                }
                else if (reference.referenceType == MeshReferenceType.MeshFilter)
                {
                    if (reference.meshFilter == null)
                        continue;
                    
                    if (reference.meshFilter.sharedMesh != originalMesh)
                        continue;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 预览替换效果（不实际执行）
        /// </summary>
        public static string PreviewReplacement(Mesh originalMesh, Mesh newMesh, List<ParticleMeshReference> references)
        {
            if (!ValidateReplacement(originalMesh, newMesh, references))
                return "替换操作无效";
            
            var validParticleSystems = 0;
            var validMeshFilters = 0;
            
            foreach (var reference in references)
            {
                if (reference.referenceType == MeshReferenceType.ParticleSystem && reference.particleSystem != null)
                {
                    var renderer = reference.particleSystem.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null && renderer.mesh == originalMesh)
                    {
                        validParticleSystems++;
                    }
                }
                else if (reference.referenceType == MeshReferenceType.MeshFilter && reference.meshFilter != null)
                {
                    if (reference.meshFilter.sharedMesh == originalMesh)
                    {
                        validMeshFilters++;
                    }
                }
            }
            
            var message = $"将替换 '{originalMesh.name}' 为 '{newMesh.name}':" + System.Environment.NewLine;
            if (validParticleSystems > 0)
                message += $"- {validParticleSystems} 个粒子系统" + System.Environment.NewLine;
            if (validMeshFilters > 0)
                message += $"- {validMeshFilters} 个MeshFilter";
            
            return message;
        }
    }
}