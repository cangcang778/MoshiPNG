using UnityEngine;

/// <summary>
/// 渲染相机标记组件
/// 挂在场景生成器创建的相机上，供逐帧渲染器自动识别
/// </summary>
public class Moshi_RenderCameraTag : MonoBehaviour
{
    public enum RenderPreset
    {
        ModelTurntable,   // 模型展示台
        VFXStage,         // 特效平面台
        ProductShot,      // 产品展示台
        Sprite2D,         // 2D序列帧
        Custom            // 自定义
    }

    public RenderPreset preset;
    public bool useTransparentBackground;
    public bool useOrthographic;
    public int referenceWidth = 1920;
    public int referenceHeight = 1080;
}
