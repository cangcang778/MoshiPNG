using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 子物体透明度控制器 - 挂在根物体上，K帧Alpha即可控制所有子物体透明度
/// 支持：SpriteRenderer、UI Image/Text/RawImage、MeshRenderer、ParticleSystem
/// 编辑器和运行时都有效
/// </summary>
[ExecuteAlways]
public class Moshi_ChildrenAlphaController : MonoBehaviour
{
    [Range(0f, 1f)]
    public float alpha = 1f;
    
    private float lastAlpha = -1f;
    
    // 缓存渲染器引用
    private SpriteRenderer[] cachedSprites;
    private MeshRenderer[] cachedMeshRenderers;
    private ParticleSystemRenderer[] cachedParticleRenderers;
    private Graphic[] cachedUIGraphics; // UI Image, Text, RawImage 等
    private CanvasRenderer[] cachedCanvasRenderers;
    
    // 材质管理
    private List<Material> instancedMaterials = new List<Material>();
    private Dictionary<MeshRenderer, Material[]> originalMeshMaterials = new Dictionary<MeshRenderer, Material[]>();
    private Dictionary<ParticleSystemRenderer, Material> originalParticleMaterials = new Dictionary<ParticleSystemRenderer, Material>();
    
    private bool initialized = false;
    
    void OnEnable()
    {
        Initialize();
    }
    
    void Initialize()
    {
        // 缓存所有渲染器
        cachedSprites = GetComponentsInChildren<SpriteRenderer>(true);
        cachedMeshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        cachedParticleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        cachedUIGraphics = GetComponentsInChildren<Graphic>(true); // UI组件
        cachedCanvasRenderers = GetComponentsInChildren<CanvasRenderer>(true);
        
        // 清理旧的实例化材质
        CleanupMaterials();
        
        // 手动实例化MeshRenderer材质（避免使用.materials属性导致泄漏警告）
        foreach (var mr in cachedMeshRenderers)
        {
            if (mr == null) continue;
            
            var sharedMats = mr.sharedMaterials;
            if (sharedMats == null || sharedMats.Length == 0) continue;
            
            // 保存原始材质引用
            originalMeshMaterials[mr] = sharedMats;
            
            // 手动创建实例化材质
            var newMats = new Material[sharedMats.Length];
            for (int i = 0; i < sharedMats.Length; i++)
            {
                if (sharedMats[i] != null)
                {
                    newMats[i] = new Material(sharedMats[i]);
                    instancedMaterials.Add(newMats[i]);
                }
            }
            // 使用sharedMaterials赋值，避免再次实例化
            mr.sharedMaterials = newMats;
        }
        
        // 手动实例化ParticleSystemRenderer材质
        foreach (var psr in cachedParticleRenderers)
        {
            if (psr == null) continue;
            
            var sharedMat = psr.sharedMaterial;
            if (sharedMat == null) continue;
            
            // 保存原始材质引用
            originalParticleMaterials[psr] = sharedMat;
            
            // 手动创建实例化材质
            var newMat = new Material(sharedMat);
            instancedMaterials.Add(newMat);
            // 使用sharedMaterial赋值，避免再次实例化
            psr.sharedMaterial = newMat;
        }
        
        initialized = true;
        lastAlpha = -1f; // 强制首次应用
    }
    
    // 使用 LateUpdate 避免与 Timeline 冲突
    void LateUpdate()
    {
        if (!initialized) Initialize();
        
        // 只在值变化时更新
        if (Mathf.Approximately(alpha, lastAlpha)) return;
        lastAlpha = alpha;
        
        ApplyAlpha();
    }
    
    void ApplyAlpha()
    {
        // UI Graphic (Image, Text, RawImage 等)
        if (cachedUIGraphics != null)
        {
            foreach (var graphic in cachedUIGraphics)
            {
                if (graphic == null) continue;
                var c = graphic.color;
                if (Mathf.Approximately(c.a, alpha)) continue;
                c.a = alpha;
                graphic.color = c;
            }
        }
        
        // CanvasRenderer - 直接设置 alpha
        if (cachedCanvasRenderers != null)
        {
            foreach (var cr in cachedCanvasRenderers)
            {
                if (cr == null) continue;
                if (Mathf.Approximately(cr.GetAlpha(), alpha)) continue;
                cr.SetAlpha(alpha);
            }
        }
        
        // SpriteRenderer - 使用缓存
        if (cachedSprites != null)
        {
            foreach (var sr in cachedSprites)
            {
                if (sr == null) continue;
                var c = sr.color;
                if (Mathf.Approximately(c.a, alpha)) continue;
                c.a = alpha;
                sr.color = c;
            }
        }
        
        // 实例化材质
        foreach (var mat in instancedMaterials)
        {
            if (mat == null) continue;
            
            if (mat.HasProperty("_MainColor"))
            {
                var c = mat.GetColor("_MainColor");
                c.a = alpha;
                mat.SetColor("_MainColor", c);
            }
            if (mat.HasProperty("_Color"))
            {
                var c = mat.GetColor("_Color");
                c.a = alpha;
                mat.SetColor("_Color", c);
            }
            if (mat.HasProperty("_TintColor"))
            {
                var c = mat.GetColor("_TintColor");
                c.a = alpha;
                mat.SetColor("_TintColor", c);
            }
        }
    }
    
    void RestoreOriginalMaterials()
    {
        // 还原MeshRenderer原始材质
        foreach (var kvp in originalMeshMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.sharedMaterials = kvp.Value;
            }
        }
        originalMeshMaterials.Clear();
        
        // 还原ParticleSystemRenderer原始材质
        foreach (var kvp in originalParticleMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.sharedMaterial = kvp.Value;
            }
        }
        originalParticleMaterials.Clear();
    }
    
    void CleanupMaterials()
    {
        // 先还原原始材质
        RestoreOriginalMaterials();
        
        // 再销毁实例化材质
        foreach (var mat in instancedMaterials)
        {
            if (mat != null)
            {
                if (Application.isPlaying)
                    Destroy(mat);
                else
                    DestroyImmediate(mat);
            }
        }
        instancedMaterials.Clear();
    }
    
    void OnDisable()
    {
        CleanupMaterials();
        initialized = false;
    }
    
    // 编辑器中层级变化时重新初始化
    void OnTransformChildrenChanged()
    {
        initialized = false;
    }
}
