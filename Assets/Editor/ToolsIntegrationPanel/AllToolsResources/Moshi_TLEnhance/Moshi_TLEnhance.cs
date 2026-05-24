using UnityEngine;
using UnityEditor;
using UnityEngine.Timeline;
using UnityEditor.Timeline;
using UnityEditor.ShortcutManagement;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// Timeline增强工具 - 合并快捷键与轨道快速选择功能
/// 
/// Clip移动/裁剪快捷键（在Timeline窗口聚焦时生效）：
/// [ - 将选中Clip的入点移动到播放头位置（保持时长，整体移动）
/// ] - 将选中Clip的出点移动到播放头位置（保持时长，整体移动）
/// Alt+[ - 裁剪选中Clip的入点到播放头位置（改变时长）
/// Alt+] - 裁剪选中Clip的出点到播放头位置（改变时长）
/// 
/// 轨道快速选择（先单击选中轨道/组，再按快捷键）：
/// A - 选中该轨道所有Clip
/// Shift+A - 选中该轨道及子轨道所有Clip  
/// Ctrl+A - 全选Timeline所有Clip
/// </summary>
[InitializeOnLoad]
public static class Moshi_TLEnhance
{
    private static double lastKeyTime = 0;
    private const double KEY_COOLDOWN = 0.15; // 按键冷却时间，防止重复触发
    
    // 缓存
    private static System.Type timelineWindowType;
    private static EditorWindow timelineWindow;
    
    static Moshi_TLEnhance()
    {
        // 注册全局GUI事件处理
        EditorApplication.update += OnEditorUpdate;
        
        // 注册全局事件处理器
        var globalEventHandlerField = typeof(EditorApplication).GetField("globalEventHandler", 
            BindingFlags.Static | BindingFlags.NonPublic);
        if (globalEventHandlerField != null)
        {
            var handler = (EditorApplication.CallbackFunction)globalEventHandlerField.GetValue(null);
            handler += OnGlobalEvent;
            globalEventHandlerField.SetValue(null, handler);
        }
    }
    
    /// <summary>
    /// 全局事件处理器
    /// </summary>
    private static void OnGlobalEvent()
    {
        Event e = Event.current;
        if (e == null) return;
        
        // 只在Timeline窗口中处理
        if (!IsTimelineWindowFocused()) return;
        
        // 检测按键事件
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.A)
        {
            // 获取当前选中的轨道
            TrackAsset selectedTrack = GetSelectedTrackFromSelection();
            
            if (e.control && !e.shift && !e.alt)
            {
                // Ctrl+A：全选Timeline所有Clip
                SelectAllClipsInTimeline();
                e.Use();
            }
            else if (e.shift && !e.control && !e.alt && selectedTrack != null)
            {
                // Shift+A：选中轨道及子轨道所有Clip
                SelectAllClipsInTrackAndChildren(selectedTrack);
                Debug.Log($"[TLEnhance] Shift+A：选中轨道 [{selectedTrack.name}] 及子轨道所有Clip");
                e.Use();
            }
            else if (!e.control && !e.shift && !e.alt && selectedTrack != null)
            {
                // A：选中该轨道所有Clip
                SelectAllClipsInTrack(selectedTrack);
                Debug.Log($"[TLEnhance] A：选中轨道 [{selectedTrack.name}] 所有Clip");
                e.Use();
            }
        }
    }
    
    /// <summary>
    /// 从Selection获取当前选中的轨道
    /// </summary>
    private static TrackAsset GetSelectedTrackFromSelection()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj is TrackAsset track)
            {
                return track;
            }
        }
        return null;
    }
    
    private static void OnEditorUpdate()
    {
        // 检查Timeline窗口是否有焦点
        if (EditorWindow.focusedWindow != null && 
            EditorWindow.focusedWindow.GetType().Name == "TimelineWindow")
        {
            // 缓存Timeline窗口引用
            if (timelineWindow == null || timelineWindow != EditorWindow.focusedWindow)
            {
                timelineWindow = EditorWindow.focusedWindow;
                timelineWindowType = timelineWindow.GetType();
            }
        }
    }
    

    
    #region Clip移动裁剪快捷键
    
    /// <summary>
    /// [ 键：将选中Clip的入点移动到播放头位置（保持时长，整体移动）
    /// </summary>
    [Shortcut("Timeline Clip/入点移到播放头", KeyCode.LeftBracket)]
    public static void MoveClipStartToPlayheadShortcut()
    {
        if (!IsTimelineWindowFocused()) return;
        if (!CheckCooldown()) return;
        MoveClipStartToPlayhead();
    }
    
    /// <summary>
    /// ] 键：将选中Clip的出点移动到播放头位置（保持时长，整体移动）
    /// </summary>
    [Shortcut("Timeline Clip/出点移到播放头", KeyCode.RightBracket)]
    public static void MoveClipEndToPlayheadShortcut()
    {
        if (!IsTimelineWindowFocused()) return;
        if (!CheckCooldown()) return;
        MoveClipEndToPlayhead();
    }
    
    /// <summary>
    /// Alt+[ 键：裁剪选中Clip的入点到播放头位置
    /// </summary>
    [Shortcut("Timeline Clip/裁剪入点到播放头", KeyCode.LeftBracket, ShortcutModifiers.Alt)]
    public static void TrimClipInPointShortcut()
    {
        if (!IsTimelineWindowFocused()) return;
        if (!CheckCooldown()) return;
        TrimClipInPoint();
    }
    
    /// <summary>
    /// Alt+] 键：裁剪选中Clip的出点到播放头位置
    /// </summary>
    [Shortcut("Timeline Clip/裁剪出点到播放头", KeyCode.RightBracket, ShortcutModifiers.Alt)]
    public static void TrimClipOutPointShortcut()
    {
        if (!IsTimelineWindowFocused()) return;
        if (!CheckCooldown()) return;
        TrimClipOutPoint();
    }
    
    /// <summary>
    /// 检查冷却时间
    /// </summary>
    private static bool CheckCooldown()
    {
        if (EditorApplication.timeSinceStartup - lastKeyTime < KEY_COOLDOWN) 
            return false;
        lastKeyTime = EditorApplication.timeSinceStartup;
        return true;
    }
    
    /// <summary>
    /// 检查Timeline窗口是否聚焦
    /// </summary>
    private static bool IsTimelineWindowFocused()
    {
        return EditorWindow.focusedWindow != null && 
               EditorWindow.focusedWindow.GetType().Name == "TimelineWindow";
    }
    
    #endregion
    
    #region Clip移动功能
    
    /// <summary>
    /// 将选中Clip的入点移动到播放头位置（保持时长）
    /// </summary>
    private static bool MoveClipStartToPlayhead()
    {
        var clips = GetSelectedTimelineClips();
        if (clips.Count == 0) return false;
        
        double playheadTime = GetPlayheadTime();
        if (playheadTime < 0) return false;
        
        Undo.RecordObjects(GetTracksToRecord(clips), "移动Clip入点到播放头");
        
        foreach (var clip in clips)
        {
            // 保持时长，整体移动
            clip.start = playheadTime;
        }
        
        RefreshTimeline();
        return true;
    }
    
    /// <summary>
    /// 将选中Clip的出点移动到播放头位置（保持时长）
    /// </summary>
    private static bool MoveClipEndToPlayhead()
    {
        var clips = GetSelectedTimelineClips();
        if (clips.Count == 0) return false;
        
        double playheadTime = GetPlayheadTime();
        if (playheadTime < 0) return false;
        
        Undo.RecordObjects(GetTracksToRecord(clips), "移动Clip出点到播放头");
        
        foreach (var clip in clips)
        {
            // 保持时长，整体移动（出点对齐播放头）
            double duration = clip.duration;
            double newStart = playheadTime - duration;
            if (newStart < 0) newStart = 0;
            clip.start = newStart;
        }
        
        RefreshTimeline();
        return true;
    }
    
    #endregion
    
    #region Clip裁剪功能
    
    /// <summary>
    /// 裁剪选中Clip的入点到播放头位置（改变时长）
    /// </summary>
    private static bool TrimClipInPoint()
    {
        var clips = GetSelectedTimelineClips();
        if (clips.Count == 0) return false;
        
        double playheadTime = GetPlayheadTime();
        if (playheadTime < 0) return false;
        
        Undo.RecordObjects(GetTracksToRecord(clips), "裁剪Clip入点");
        
        int trimmedCount = 0;
        foreach (var clip in clips)
        {
            double clipEnd = clip.start + clip.duration;
            
            // 播放头必须在Clip范围内或之前
            if (playheadTime < clipEnd)
            {
                double originalStart = clip.start;
                double trimAmount = playheadTime - originalStart;
                
                if (trimAmount > 0)
                {
                    // 裁剪入点：增加clipIn，减少duration
                    clip.clipIn += trimAmount * clip.timeScale;
                    clip.start = playheadTime;
                    clip.duration -= trimAmount;
                    trimmedCount++;
                }
                else if (trimAmount < 0)
                {
                    // 向前扩展入点
                    double extendAmount = -trimAmount;
                    if (clip.clipIn >= extendAmount * clip.timeScale)
                    {
                        clip.clipIn -= extendAmount * clip.timeScale;
                        clip.start = playheadTime;
                        clip.duration += extendAmount;
                        trimmedCount++;
                    }
                    else
                    {
                        // clipIn不足，只能扩展到clipIn为0
                        double maxExtend = clip.clipIn / clip.timeScale;
                        clip.start -= maxExtend;
                        clip.duration += maxExtend;
                        clip.clipIn = 0;
                        trimmedCount++;
                    }
                }
            }
        }
        
        RefreshTimeline();
        return trimmedCount > 0;
    }
    
    /// <summary>
    /// 裁剪选中Clip的出点到播放头位置（改变时长）
    /// </summary>
    private static bool TrimClipOutPoint()
    {
        var clips = GetSelectedTimelineClips();
        if (clips.Count == 0) return false;
        
        double playheadTime = GetPlayheadTime();
        if (playheadTime < 0) return false;
        
        Undo.RecordObjects(GetTracksToRecord(clips), "裁剪Clip出点");
        
        int trimmedCount = 0;
        foreach (var clip in clips)
        {
            // 播放头必须在Clip起点之后
            if (playheadTime > clip.start)
            {
                double newDuration = playheadTime - clip.start;
                
                // 限制最小时长
                if (newDuration > 0.001)
                {
                    clip.duration = newDuration;
                    trimmedCount++;
                }
            }
        }
        
        RefreshTimeline();
        return trimmedCount > 0;
    }
    
    #endregion
    
    #region 轨道快速选择功能
    
    /// <summary>
    /// 选中指定轨道的所有Clip
    /// </summary>
    public static void SelectAllClipsInTrack(TrackAsset track)
    {
        if (track == null) return;
        
        var clips = track.GetClips().ToList();
        if (clips.Count == 0) return;
        
        SetSelectedClips(clips);
        RefreshTimeline();
    }
    
    /// <summary>
    /// 追加选择轨道的所有Clip
    /// </summary>
    public static void AppendSelectClipsInTrack(TrackAsset track)
    {
        if (track == null) return;
        
        var currentSelection = GetSelectedTimelineClips();
        var trackClips = track.GetClips().ToList();
        
        // 合并选择
        foreach (var clip in trackClips)
        {
            if (!currentSelection.Contains(clip))
            {
                currentSelection.Add(clip);
            }
        }
        
        SetSelectedClips(currentSelection);
        RefreshTimeline();
    }
    
    /// <summary>
    /// 选中轨道及其子轨道的所有Clip
    /// </summary>
    public static void SelectAllClipsInTrackAndChildren(TrackAsset track)
    {
        if (track == null) return;
        
        var allClips = new List<TimelineClip>();
        CollectClipsRecursive(track, allClips);
        
        if (allClips.Count > 0)
        {
            SetSelectedClips(allClips);
            RefreshTimeline();
        }
    }
    
    /// <summary>
    /// 递归收集轨道及子轨道的所有Clip
    /// </summary>
    private static void CollectClipsRecursive(TrackAsset track, List<TimelineClip> clips)
    {
        clips.AddRange(track.GetClips());
        
        foreach (var childTrack in track.GetChildTracks())
        {
            CollectClipsRecursive(childTrack, clips);
        }
    }
    
    /// <summary>
    /// 选中Timeline中所有轨道的所有Clip
    /// </summary>
    public static void SelectAllClipsInTimeline()
    {
        var timelineAsset = TimelineEditor.inspectedAsset;
        if (timelineAsset == null) return;
        
        var allClips = new List<TimelineClip>();
        
        // 遍历所有根轨道
        foreach (var track in timelineAsset.GetRootTracks())
        {
            CollectClipsRecursive(track, allClips);
        }
        
        if (allClips.Count > 0)
        {
            SetSelectedClips(allClips);
            RefreshTimeline();
            Debug.Log($"[TLEnhance] 已选中所有轨道共 {allClips.Count} 个Clip");
        }
    }
    
    #endregion
    
    #region 辅助方法
    
    /// <summary>
    /// 获取当前选中的Timeline Clips
    /// </summary>
    public static List<TimelineClip> GetSelectedTimelineClips()
    {
        var clips = new List<TimelineClip>();
        
        try
        {
            // 通过反射获取TimelineEditor的选中项
            var timelineEditorType = typeof(TimelineEditor);
            
            // 尝试获取selectedClips属性
            var selectedClipsProperty = timelineEditorType.GetProperty("selectedClips", 
                BindingFlags.Public | BindingFlags.Static);
            
            if (selectedClipsProperty != null)
            {
                var selectedClips = selectedClipsProperty.GetValue(null) as IEnumerable<TimelineClip>;
                if (selectedClips != null)
                {
                    clips.AddRange(selectedClips);
                }
            }
            
            // 如果上面的方法失败，尝试其他方式
            if (clips.Count == 0)
            {
                // 尝试通过Selection获取
                var windowType = System.Type.GetType("UnityEditor.Timeline.TimelineWindow, Unity.Timeline.Editor");
                if (windowType != null)
                {
                    var windowInstance = EditorWindow.GetWindow(windowType, false, null, false);
                    if (windowInstance != null)
                    {
                        var stateProperty = windowType.GetProperty("state", 
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        if (stateProperty != null)
                        {
                            var state = stateProperty.GetValue(windowInstance);
                            if (state != null)
                            {
                                var selectedClipsField = state.GetType().GetProperty("selectedClips",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                
                                if (selectedClipsField != null)
                                {
                                    var selectedClips = selectedClipsField.GetValue(state) as IEnumerable<TimelineClip>;
                                    if (selectedClips != null)
                                    {
                                        clips.AddRange(selectedClips);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"获取选中Clip失败: {ex.Message}");
        }
        
        return clips;
    }
    
    /// <summary>
    /// 获取需要记录 Undo 的轨道对象（兼容 Timeline 1.4.x）
    /// </summary>
    private static Object[] GetTracksToRecord(List<TimelineClip> clips)
    {
        return clips
            .Select(GetParentTrack)
            .Where(track => track != null)
            .Distinct()
            .Cast<Object>()
            .ToArray();
    }

    /// <summary>
    /// 获取 Clip 所属轨道（兼容 Timeline 1.4.x）
    /// </summary>
    private static TrackAsset GetParentTrack(TimelineClip clip)
    {
        if (clip == null) return null;

        var timelineAsset = TimelineEditor.inspectedAsset;
        if (timelineAsset == null) return null;

        foreach (var track in timelineAsset.GetRootTracks())
        {
            var parentTrack = FindParentTrackRecursive(track, clip);
            if (parentTrack != null) return parentTrack;
        }

        return null;
    }

    /// <summary>
    /// 递归查找 Clip 所属轨道
    /// </summary>
    private static TrackAsset FindParentTrackRecursive(TrackAsset track, TimelineClip clip)
    {
        if (track == null) return null;

        foreach (var trackClip in track.GetClips())
        {
            if (ReferenceEquals(trackClip, clip)) return track;
        }

        foreach (var childTrack in track.GetChildTracks())
        {
            var parentTrack = FindParentTrackRecursive(childTrack, clip);
            if (parentTrack != null) return parentTrack;
        }

        return null;
    }
    
    /// <summary>
    /// 设置选中的Timeline Clips
    /// </summary>
    public static void SetSelectedClips(List<TimelineClip> clips)
    {
        try
        {
            // 方法1：通过反射设置TimelineEditor.selectedClips
            var timelineEditorType = typeof(TimelineEditor);
            var selectedClipsProperty = timelineEditorType.GetProperty("selectedClips",
                BindingFlags.Public | BindingFlags.Static);
            
            if (selectedClipsProperty != null && selectedClipsProperty.CanWrite)
            {
                selectedClipsProperty.SetValue(null, clips.ToArray());
                return;
            }
            
            // 方法2：通过TimelineWindow.state设置
            var windowType = System.Type.GetType("UnityEditor.Timeline.TimelineWindow, Unity.Timeline.Editor");
            if (windowType != null)
            {
                var windowInstance = EditorWindow.GetWindow(windowType, false, null, false);
                if (windowInstance != null)
                {
                    var stateProperty = windowType.GetProperty("state",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (stateProperty != null)
                    {
                        var state = stateProperty.GetValue(windowInstance);
                        if (state != null)
                        {
                            // 尝试找到选择管理器
                            var selectionManagerField = state.GetType().GetField("m_SelectionManager",
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            
                            if (selectionManagerField != null)
                            {
                                var selectionManager = selectionManagerField.GetValue(state);
                                if (selectionManager != null)
                                {
                                    // 清除当前选择
                                    var clearMethod = selectionManager.GetType().GetMethod("Clear",
                                        BindingFlags.Public | BindingFlags.Instance);
                                    clearMethod?.Invoke(selectionManager, null);
                                    
                                    // 添加新选择
                                    var addMethod = selectionManager.GetType().GetMethod("Add",
                                        BindingFlags.Public | BindingFlags.Instance);
                                    if (addMethod != null)
                                    {
                                        foreach (var clip in clips)
                                        {
                                            addMethod.Invoke(selectionManager, new object[] { clip });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"设置选中Clip失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取播放头时间
    /// </summary>
    public static double GetPlayheadTime()
    {
        try
        {
            // 方法1：通过TimelineEditor获取
            var timelineEditorType = typeof(TimelineEditor);
            var inspectedDirectorProperty = timelineEditorType.GetProperty("inspectedDirector",
                BindingFlags.Public | BindingFlags.Static);
            
            if (inspectedDirectorProperty != null)
            {
                var director = inspectedDirectorProperty.GetValue(null) as UnityEngine.Playables.PlayableDirector;
                if (director != null)
                {
                    return director.time;
                }
            }
            
            // 方法2：通过TimelineWindow获取
            var windowType = System.Type.GetType("UnityEditor.Timeline.TimelineWindow, Unity.Timeline.Editor");
            if (windowType != null)
            {
                var windowInstance = EditorWindow.GetWindow(windowType, false, null, false);
                if (windowInstance != null)
                {
                    var stateProperty = windowType.GetProperty("state",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (stateProperty != null)
                    {
                        var state = stateProperty.GetValue(windowInstance);
                        if (state != null)
                        {
                            var editSequenceProperty = state.GetType().GetProperty("editSequence",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            
                            if (editSequenceProperty != null)
                            {
                                var editSequence = editSequenceProperty.GetValue(state);
                                if (editSequence != null)
                                {
                                    var timeProperty = editSequence.GetType().GetProperty("time",
                                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    
                                    if (timeProperty != null)
                                    {
                                        return (double)timeProperty.GetValue(editSequence);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // 方法3：查找场景中的PlayableDirector
            var directors = Object.FindObjectsOfType<UnityEngine.Playables.PlayableDirector>();
            if (directors.Length > 0)
            {
                return directors[0].time;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"获取播放头时间失败: {ex.Message}");
        }
        
        return -1;
    }
    
    /// <summary>
    /// 刷新Timeline窗口
    /// </summary>
    public static void RefreshTimeline()
    {
        try
        {
            // 标记Timeline资源为脏
            var timelineAsset = TimelineEditor.inspectedAsset;
            if (timelineAsset != null)
            {
                EditorUtility.SetDirty(timelineAsset);
            }
            
            // 刷新Timeline窗口
            var windowType = System.Type.GetType("UnityEditor.Timeline.TimelineWindow, Unity.Timeline.Editor");
            if (windowType != null)
            {
                var windowInstance = EditorWindow.GetWindow(windowType, false, null, false);
                if (windowInstance != null)
                {
                    windowInstance.Repaint();
                }
            }
            
            // 强制重绘
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"刷新Timeline失败: {ex.Message}");
        }
    }
    
    #endregion
}
