using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

/// <summary>
/// 视频自动播放组件 - 配合 Moshi_TLVideo 工具使用
/// 支持渐入渐出效果，结束时清除RT
/// </summary>
public class VideoAutoPlaySimple : MonoBehaviour
{
    [Header("渐变设置")]
    [Tooltip("启用渐入效果")]
    public bool fadeIn = true;
    [Tooltip("渐入时长（秒）")]
    public float fadeInDuration = 0.5f;
    
    [Tooltip("启用渐出效果")]
    public bool fadeOut = true;
    [Tooltip("渐出时长（秒）")]
    public float fadeOutDuration = 0.5f;
    
    [Header("其他设置")]
    [Tooltip("播放结束后清除RenderTexture")]
    public bool clearOnFinish = true;
    
    [Tooltip("显示视频的RawImage（自动查找）")]
    public RawImage targetRawImage;
    
    private VideoPlayer videoPlayer;
    private RenderTexture targetRT;
    private CanvasGroup canvasGroup;
    
    private bool isFadingIn = false;
    private bool isFadingOut = false;
    private float fadeTimer = 0f;
    private float videoLength = 0f;

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer != null)
        {
            targetRT = videoPlayer.targetTexture;
            videoPlayer.prepareCompleted += OnPrepareCompleted;
            videoPlayer.errorReceived += OnErrorReceived;
            videoPlayer.loopPointReached += OnVideoFinished;
        }
        
        // 尝试查找显示RT的RawImage
        FindTargetRawImage();
    }

    private void FindTargetRawImage()
    {
        if (targetRawImage != null) return;
        
        // 如果有RT，查找使用该RT的RawImage
        if (targetRT != null)
        {
            RawImage[] allRawImages = FindObjectsOfType<RawImage>();
            foreach (var img in allRawImages)
            {
                if (img.texture == targetRT)
                {
                    targetRawImage = img;
                    break;
                }
            }
        }
        
        // 获取或创建CanvasGroup用于渐变
        if (targetRawImage != null)
        {
            canvasGroup = targetRawImage.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = targetRawImage.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnEnable()
    {
        if (videoPlayer != null)
        {
            // 重置状态
            isFadingIn = false;
            isFadingOut = false;
            fadeTimer = 0f;
            
            // 初始透明
            if (fadeIn && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            
            videoPlayer.Prepare();
        }
    }

    private void OnPrepareCompleted(VideoPlayer vp)
    {
        if (gameObject.activeInHierarchy)
        {
            videoLength = (float)vp.length;
            vp.Play();
            
            // 开始渐入
            if (fadeIn)
            {
                FindTargetRawImage(); // 再次尝试查找
                if (canvasGroup != null)
                {
                    isFadingIn = true;
                    fadeTimer = 0f;
                }
            }
            
            Debug.Log($"[VideoAutoPlaySimple] 视频开始播放，时长: {videoLength:F2}秒");
        }
    }

    private void Update()
    {
        if (videoPlayer == null || !videoPlayer.isPlaying) return;
        
        // 渐入
        if (isFadingIn && canvasGroup != null)
        {
            fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTimer / fadeInDuration);
            canvasGroup.alpha = t;
            
            if (t >= 1f)
            {
                isFadingIn = false;
                fadeTimer = 0f;
            }
        }
        
        // 检测是否需要开始渐出
        if (fadeOut && !isFadingOut && !isFadingIn && canvasGroup != null)
        {
            float currentTime = (float)videoPlayer.time;
            float remainingTime = videoLength - currentTime;
            
            if (remainingTime <= fadeOutDuration && remainingTime > 0)
            {
                isFadingOut = true;
                fadeTimer = 0f;
            }
        }
        
        // 渐出
        if (isFadingOut && canvasGroup != null)
        {
            fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTimer / fadeOutDuration);
            canvasGroup.alpha = 1f - t;
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log("[VideoAutoPlaySimple] 视频播放结束");
        
        isFadingIn = false;
        isFadingOut = false;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        
        if (clearOnFinish)
        {
            ClearRenderTexture();
        }
    }

    private void ClearRenderTexture()
    {
        if (targetRT != null)
        {
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = targetRT;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = currentRT;
            Debug.Log("[VideoAutoPlaySimple] 已清除RenderTexture");
        }
    }

    private void OnErrorReceived(VideoPlayer vp, string message)
    {
        Debug.LogError($"[VideoAutoPlaySimple] 视频错误: {message}");
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
        
        isFadingIn = false;
        isFadingOut = false;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        
        if (clearOnFinish)
        {
            ClearRenderTexture();
        }
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepareCompleted;
            videoPlayer.errorReceived -= OnErrorReceived;
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }
}
