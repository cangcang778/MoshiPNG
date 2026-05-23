using System.Collections.Generic;
using UnityEngine;

namespace ParticleMeshTools
{
    /// <summary>
    /// 预览数据类型
    /// </summary>
    public enum PreviewDataType
    {
        Mesh,
        Material
    }

    /// <summary>
    /// Mesh预览数据类型
    /// </summary>
    public enum MeshPreviewType
    {
        ParticleSystem,
        ParticleSystemShape,
        MeshFilter,
        SkinnedMeshRenderer
    }

    /// <summary>
    /// Material预览数据类型
    /// </summary>
    public enum MaterialPreviewType
    {
        ParticleSystemRenderer,
        ParticleSystemTrail,
        MeshRenderer,
        SkinnedMeshRenderer
    }

    /// <summary>
    /// Mesh预览项数据
    /// </summary>
    [System.Serializable]
    public class MeshPreviewItem
    {
        public GameObject gameObject;
        public Mesh mesh;
        public MeshPreviewType type;
        public Component component;
        public Material[] materials;
        public Vector3 scale;
        public bool isEnabled;
        
        // 粒子系统特有属性
        public ParticleSystemRenderMode renderMode;
        public float particleScale;
        
        // Shape节点特有属性
        public ParticleSystemShapeType shapeType;
        public bool isShapeEnabled;
        
        public int meshIndex = -1; // 用于多Mesh数组的索引，-1表示单个Mesh
        
        // MeshRenderer特有属性
        public bool castShadows;
        public bool receiveShadows;
        
        public MeshPreviewItem(GameObject go, Mesh m, MeshPreviewType t, Component comp)
        {
            gameObject = go;
            mesh = m;
            type = t;
            component = comp;
            scale = go.transform.localScale;
            isEnabled = go.activeInHierarchy && comp != null;
            
            // 获取材质信息
            if (type == MeshPreviewType.ParticleSystem)
            {
                var ps = comp as ParticleSystem;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                materials = renderer ? renderer.materials : new Material[0];
                renderMode = renderer ? renderer.renderMode : ParticleSystemRenderMode.Billboard;
                particleScale = renderer ? renderer.lengthScale : 1f;
            }
            else if (type == MeshPreviewType.ParticleSystemShape)
            {
                var ps = comp as ParticleSystem;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                materials = renderer ? renderer.materials : new Material[0];
                
                var shape = ps.shape;
                shapeType = shape.shapeType;
                isShapeEnabled = shape.enabled;
            }
            else if (type == MeshPreviewType.MeshFilter)
            {
                var mf = comp as MeshFilter;
                var mr = mf.GetComponent<MeshRenderer>();
                materials = mr ? mr.materials : new Material[0];
                if (mr)
                {
                    castShadows = mr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off;
                    receiveShadows = mr.receiveShadows;
                }
            }
            else if (type == MeshPreviewType.SkinnedMeshRenderer)
            {
                var smr = comp as SkinnedMeshRenderer;
                materials = smr ? smr.materials : new Material[0];
                if (smr)
                {
                    castShadows = smr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off;
                    receiveShadows = smr.receiveShadows;
                }
            }
        }
        
        public string GetTypeDisplayName()
        {
            switch (type)
            {
                case MeshPreviewType.ParticleSystem:
                    if (meshIndex >= 0)
                        return $"[粒子系统-多网格{meshIndex + 1}]";
                    return "[粒子系统-渲染器]";
                case MeshPreviewType.ParticleSystemShape:
                    return "[粒子系统-形状]";
                case MeshPreviewType.MeshFilter:
                    return "[MeshFilter]";
                case MeshPreviewType.SkinnedMeshRenderer:
                    return "[SkinnedMesh]";
                default:
                    return "[未知]";
            }
        }
        
        public Color GetTypeColor()
        {
            switch (type)
            {
                case MeshPreviewType.ParticleSystem:
                    return new Color(0.7f, 1f, 0.7f); // 绿色
                case MeshPreviewType.ParticleSystemShape:
                    return new Color(0.8f, 1f, 0.8f); // 淡绿色
                case MeshPreviewType.MeshFilter:
                    return new Color(0.7f, 0.8f, 1f); // 蓝色
                case MeshPreviewType.SkinnedMeshRenderer:
                    return new Color(1f, 0.8f, 0.7f); // 橙色
                default:
                    return Color.white;
            }
        }
        
        public Color GetTypeTextColor()
        {
            switch (type)
            {
                case MeshPreviewType.ParticleSystem:
                    return new Color(0.2f, 0.6f, 0.2f); // 深绿色
                case MeshPreviewType.ParticleSystemShape:
                    return new Color(0.1f, 0.5f, 0.1f); // 更深的绿色
                case MeshPreviewType.MeshFilter:
                    return new Color(0.2f, 0.3f, 0.8f); // 深蓝色
                case MeshPreviewType.SkinnedMeshRenderer:
                    return new Color(0.8f, 0.4f, 0.1f); // 深橙色
                default:
                    return Color.black;
            }
        }
    }

    /// <summary>
    /// Material预览项数据
    /// </summary>
    [System.Serializable]
    public class MaterialPreviewItem
    {
        public GameObject gameObject;
        public Material material;
        public MaterialPreviewType type;
        public Component component;
        public Vector3 scale;
        public bool isEnabled;
        
        // 粒子系统特有属性
        public ParticleSystemRenderMode renderMode;
        public bool isTrailMaterial; // 是否为Trail材质
        
        public int materialIndex = -1; // 用于多Material数组的索引，-1表示单个Material
        
        // 渲染器特有属性
        public bool castShadows;
        public bool receiveShadows;
        
        public MaterialPreviewItem(GameObject go, Material mat, MaterialPreviewType t, Component comp)
        {
            gameObject = go;
            material = mat;
            type = t;
            component = comp;
            scale = go.transform.localScale;
            isEnabled = go.activeInHierarchy && comp != null;
            
            // 根据类型设置特有属性
            if (type == MaterialPreviewType.ParticleSystemRenderer || type == MaterialPreviewType.ParticleSystemTrail)
            {
                var ps = comp as ParticleSystem;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderMode = renderer ? renderer.renderMode : ParticleSystemRenderMode.Billboard;
                isTrailMaterial = (type == MaterialPreviewType.ParticleSystemTrail);
            }
            else if (type == MaterialPreviewType.MeshRenderer)
            {
                var mr = comp as MeshRenderer;
                if (mr)
                {
                    castShadows = mr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off;
                    receiveShadows = mr.receiveShadows;
                }
            }
            else if (type == MaterialPreviewType.SkinnedMeshRenderer)
            {
                var smr = comp as SkinnedMeshRenderer;
                if (smr)
                {
                    castShadows = smr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off;
                    receiveShadows = smr.receiveShadows;
                }
            }
        }
        
        public string GetTypeDisplayName()
        {
            switch (type)
            {
                case MaterialPreviewType.ParticleSystemRenderer:
                    if (materialIndex >= 0)
                        return $"[粒子系统-材质{materialIndex + 1}]";
                    return "[粒子系统-渲染器]";
                case MaterialPreviewType.ParticleSystemTrail:
                    return "[粒子系统-拖尾]";
                case MaterialPreviewType.MeshRenderer:
                    if (materialIndex >= 0)
                        return $"[MeshRenderer-材质{materialIndex + 1}]";
                    return "[MeshRenderer]";
                case MaterialPreviewType.SkinnedMeshRenderer:
                    if (materialIndex >= 0)
                        return $"[SkinnedMesh-材质{materialIndex + 1}]";
                    return "[SkinnedMesh]";
                default:
                    return "[未知]";
            }
        }
        
        public Color GetTypeColor()
        {
            switch (type)
            {
                case MaterialPreviewType.ParticleSystemRenderer:
                    return new Color(0.7f, 1f, 0.7f); // 绿色
                case MaterialPreviewType.ParticleSystemTrail:
                    return new Color(0.9f, 1f, 0.7f); // 淡黄绿色
                case MaterialPreviewType.MeshRenderer:
                    return new Color(0.7f, 0.8f, 1f); // 蓝色
                case MaterialPreviewType.SkinnedMeshRenderer:
                    return new Color(1f, 0.8f, 0.7f); // 橙色
                default:
                    return Color.white;
            }
        }
        
        public Color GetTypeTextColor()
        {
            switch (type)
            {
                case MaterialPreviewType.ParticleSystemRenderer:
                    return new Color(0.2f, 0.6f, 0.2f); // 深绿色
                case MaterialPreviewType.ParticleSystemTrail:
                    return new Color(0.4f, 0.6f, 0.1f); // 深黄绿色
                case MaterialPreviewType.MeshRenderer:
                    return new Color(0.2f, 0.3f, 0.8f); // 深蓝色
                case MaterialPreviewType.SkinnedMeshRenderer:
                    return new Color(0.8f, 0.4f, 0.1f); // 深橙色
                default:
                    return Color.black;
            }
        }
    }
    
    /// <summary>
    /// Mesh预览统计数据
    /// </summary>
    [System.Serializable]
    public class MeshPreviewStatistics
    {
        public Dictionary<Mesh, List<MeshPreviewItem>> meshGroups = new Dictionary<Mesh, List<MeshPreviewItem>>();
        public List<MeshPreviewItem> allItems = new List<MeshPreviewItem>();
        
        public int TotalMeshCount => meshGroups.Count;
        public int TotalItemCount => allItems.Count;
        public int ParticleSystemCount => allItems.FindAll(item => item.type == MeshPreviewType.ParticleSystem).Count;
        public int ParticleSystemShapeCount => allItems.FindAll(item => item.type == MeshPreviewType.ParticleSystemShape).Count;
        public int MeshFilterCount => allItems.FindAll(item => item.type == MeshPreviewType.MeshFilter).Count;
        public int SkinnedMeshCount => allItems.FindAll(item => item.type == MeshPreviewType.SkinnedMeshRenderer).Count;
        
        public void Clear()
        {
            meshGroups.Clear();
            allItems.Clear();
        }
        
        public void AddItem(MeshPreviewItem item)
        {
            if (item.mesh == null) return;
            
            allItems.Add(item);
            
            if (!meshGroups.ContainsKey(item.mesh))
            {
                meshGroups[item.mesh] = new List<MeshPreviewItem>();
            }
            meshGroups[item.mesh].Add(item);
        }
        
        /// <summary>
        /// 获取统计摘要信息
        /// </summary>
        public string GetStatisticsSummary()
        {
            var summary = "总计: " + TotalItemCount + " 个Mesh引用\n";
            summary += "- 粒子系统渲染器: " + ParticleSystemCount + "\n";
            summary += "- 粒子系统形状: " + ParticleSystemShapeCount + "\n";
            summary += "- MeshFilter: " + MeshFilterCount + "\n";
            summary += "- SkinnedMeshRenderer: " + SkinnedMeshCount;
            
            return summary;
        }
    }

    /// <summary>
    /// Material预览统计数据
    /// </summary>
    [System.Serializable]
    public class MaterialPreviewStatistics
    {
        public Dictionary<Material, List<MaterialPreviewItem>> materialGroups = new Dictionary<Material, List<MaterialPreviewItem>>();
        public List<MaterialPreviewItem> allItems = new List<MaterialPreviewItem>();
        
        public int TotalMaterialCount => materialGroups.Count;
        public int TotalItemCount => allItems.Count;
        public int ParticleSystemRendererCount => allItems.FindAll(item => item.type == MaterialPreviewType.ParticleSystemRenderer).Count;
        public int ParticleSystemTrailCount => allItems.FindAll(item => item.type == MaterialPreviewType.ParticleSystemTrail).Count;
        public int MeshRendererCount => allItems.FindAll(item => item.type == MaterialPreviewType.MeshRenderer).Count;
        public int SkinnedMeshRendererCount => allItems.FindAll(item => item.type == MaterialPreviewType.SkinnedMeshRenderer).Count;
        
        public void Clear()
        {
            materialGroups.Clear();
            allItems.Clear();
        }
        
        public void AddItem(MaterialPreviewItem item)
        {
            if (item.material == null) return;
            
            allItems.Add(item);
            
            if (!materialGroups.ContainsKey(item.material))
            {
                materialGroups[item.material] = new List<MaterialPreviewItem>();
            }
            materialGroups[item.material].Add(item);
        }
        
        /// <summary>
        /// 获取统计摘要信息
        /// </summary>
        public string GetStatisticsSummary()
        {
            var summary = "总计: " + TotalItemCount + " 个Material引用\n";
            summary += "- 粒子系统渲染器: " + ParticleSystemRendererCount + "\n";
            summary += "- 粒子系统拖尾: " + ParticleSystemTrailCount + "\n";
            summary += "- MeshRenderer: " + MeshRendererCount + "\n";
            summary += "- SkinnedMeshRenderer: " + SkinnedMeshRendererCount;
            
            return summary;
        }
    }

    /// <summary>
    /// 贴图预览项
    /// </summary>
    public class TexturePreviewItem
    {
        public GameObject gameObject;
        public Material material;
        public Texture texture;
        public string propertyName;
        public TextureType textureType;
        public string hierarchyPath;
        public string componentType;
    }
    
    /// <summary>
    /// 贴图类型
    /// </summary>
    public enum TextureType
    {
        MainTexture,    // 主纹理
        NormalMap,      // 法线贴图
        EmissionMap,    // 自发光贴图
        Other,          // 其他贴图
        Missing         // 缺失贴图
    }
    
    /// <summary>
    /// 贴图排序模式
    /// </summary>
    public enum TextureSortMode
    {
        ByName,         // 按名称排序
        ByUsageCount,   // 按使用次数排序
        ByMemorySize    // 按内存占用排序
    }

    /// <summary>
    /// 贴图预览统计信息
    /// </summary>
    public class TexturePreviewStatistics
    {
        public int totalTextureReferences;
        public int uniqueTextures;
        public int missingTextures;
        public long estimatedMemoryUsage;
        public Dictionary<Texture, int> textureUsageCount = new Dictionary<Texture, int>();
        public Dictionary<TextureType, int> typeUsageCount = new Dictionary<TextureType, int>();
    }
}