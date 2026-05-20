using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParticleMeshTools
{
    /// <summary>
    /// Mesh引用类型
    /// </summary>
    public enum MeshReferenceType
    {
        ParticleSystem,
        MeshFilter
    }
    
    /// <summary>
    /// Mesh引用数据（支持粒子系统和MeshFilter）
    /// </summary>
    [Serializable]
    public class ParticleMeshReference
    {
        public ParticleSystem particleSystem;
        public MeshFilter meshFilter;
        public MeshReferenceType referenceType;
        public Mesh mesh;
        public Material material;
        public string objectPath;
        public string meshPath;
        public Vector3 scale = Vector3.one;
        public Color color = Color.white;
        
        // 粒子系统构造函数
        public ParticleMeshReference(ParticleSystem ps, Mesh m)
        {
            particleSystem = ps;
            mesh = m;
            referenceType = MeshReferenceType.ParticleSystem;
            
            if (ps != null)
            {
                objectPath = GetGameObjectPath(ps.gameObject);
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    material = renderer.sharedMaterial;
                }
                
                var shape = ps.shape;
                scale = shape.scale;
                
                var main = ps.main;
                color = main.startColor.color;
            }
            
            if (m != null)
            {
                meshPath = UnityEditor.AssetDatabase.GetAssetPath(m);
            }
        }
        
        // MeshFilter构造函数
        public ParticleMeshReference(MeshFilter mf, Mesh m)
        {
            meshFilter = mf;
            mesh = m;
            referenceType = MeshReferenceType.MeshFilter;
            
            if (mf != null)
            {
                objectPath = GetGameObjectPath(mf.gameObject);
                var renderer = mf.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    material = renderer.sharedMaterial;
                }
                
                scale = mf.transform.localScale;
            }
            
            if (m != null)
            {
                meshPath = UnityEditor.AssetDatabase.GetAssetPath(m);
            }
        }
        
        /// <summary>
        /// 获取引用对象的GameObject
        /// </summary>
        public GameObject GetGameObject()
        {
            return referenceType == MeshReferenceType.ParticleSystem 
                ? (particleSystem != null ? particleSystem.gameObject : null)
                : (meshFilter != null ? meshFilter.gameObject : null);
        }
        
        /// <summary>
        /// 获取引用类型的显示名称
        /// </summary>
        public string GetReferenceTypeName()
        {
            return referenceType == MeshReferenceType.ParticleSystem ? "粒子系统" : "MeshFilter";
        }
        
        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
        
        /// <summary>
        /// 获取显示名称（兼容旧属性名）
        /// </summary>
        public string particleSystemPath 
        { 
            get { return objectPath; } 
        }
    }
    
    /// <summary>
    /// Mesh统计信息
    /// </summary>
    [Serializable]
    public class MeshStatistics
    {
        public Mesh mesh;
        public string meshPath;
        public int referenceCount;
        public List<ParticleMeshReference> references = new List<ParticleMeshReference>();
        
        public MeshStatistics(Mesh m)
        {
            mesh = m;
            if (m != null)
            {
                meshPath = UnityEditor.AssetDatabase.GetAssetPath(m);
            }
        }
        
        public void AddReference(ParticleMeshReference reference)
        {
            references.Add(reference);
            referenceCount = references.Count;
        }
    }
    
    /// <summary>
    /// 替换操作记录
    /// </summary>
    [Serializable]
    public class ReplaceOperation
    {
        public DateTime timestamp;
        public Mesh originalMesh;
        public Mesh newMesh;
        public List<ParticleMeshReference> affectedReferences = new List<ParticleMeshReference>();
        
        public ReplaceOperation(Mesh original, Mesh newMesh)
        {
            timestamp = DateTime.Now;
            originalMesh = original;
            this.newMesh = newMesh;
        }
    }
}