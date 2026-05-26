using UnityEngine;
using UnityEngine.Rendering;

namespace MoshiTools
{
    /// <summary>
    /// 透明逐帧渲染环境配置
    /// Transparent frame render environment configuration
    /// </summary>
    [AddComponentMenu("Moshi/透明逐帧渲染配置")]
    public class Moshi_FrameRenderConfig : MonoBehaviour
    {
        public enum EnvironmentMode
        {
            VFX,
            UI,
            Character,
            Custom
        }

        [Header("渲染目标")]
        [Tooltip("需要逐帧渲染的特效根节点")]
        public Transform targetRoot;

        [Tooltip("参考 Game 相机，用于同步构图和后处理设置")]
        public Camera referenceCamera;

        [Header("差分渲染相机")]
        [Tooltip("黑底渲染相机")]
        public Camera blackCamera;

        [Tooltip("白底渲染相机")]
        public Camera whiteCamera;

        [Header("后处理")]
        [Tooltip("用于逐帧渲染的全局后处理 Volume")]
        public Volume postProcessVolume;

        [Header("输出设置")]
        [Tooltip("输出宽度")]
        public int width = 1024;

        [Tooltip("输出高度")]
        public int height = 1024;

        [Tooltip("默认帧率")]
        public int fps = 30;

        [Tooltip("输出目录")]
        public string outputDirectory = "Assets/Moshi_FrameOutput";

        [Header("透明设置")]
        [Tooltip("使用专业遮罩合成：后期颜色单独渲染，Alpha 使用无后期遮罩重建")]
        public bool useProfessionalAlpha = true;

        [Tooltip("使用黑白背景差分重建透明 Alpha")]
        public bool useDifferentialAlpha = true;

        [Tooltip("使用 HDR 渲染缓冲，保留 Bloom 和高亮颜色")]
        public bool useHDRBuffer = true;

        [Range(0f, 5f), Tooltip("根据后期亮度补充辉光区域 Alpha 的强度")]
        public float glowAlphaStrength = 2f;

        [Range(0f, 0.05f), Tooltip("低于该 Alpha 的像素会被清为透明")]
        public float alphaThreshold = 0.001f;

        [Tooltip("渲染时临时把目标根节点隔离到专用 Layer")]
        public bool isolateTargetLayer = true;

        [Tooltip("渲染相机使用的 LayerMask")]
        public LayerMask renderLayerMask = -1;

        [Header("环境模式")]
        [Tooltip("环境预设模式")]
        public EnvironmentMode environmentMode = EnvironmentMode.VFX;
    }
}
