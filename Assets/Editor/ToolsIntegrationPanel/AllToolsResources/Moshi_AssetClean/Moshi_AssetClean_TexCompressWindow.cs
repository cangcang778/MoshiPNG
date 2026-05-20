using System;
using UnityEngine;
using UnityEditor;

namespace MoshiTools
{
    /// <summary>
    /// 贴图压缩设置窗口 - 自定义目标分辨率
    /// Texture compress settings window - custom target resolution
    /// </summary>
    public class Moshi_AssetClean_TexCompressWindow : EditorWindow
    {
        // 引用数据
        // Reference data
        private Moshi_AssetClean.TexCheckEntry _info;
        private string _fullPath;
        private Moshi_AssetClean _parentWindow;

        // 目标分辨率
        // Target resolution
        private int _targetWidth;
        private int _targetHeight;
        private bool _syncWidthHeight = true;

        // 常用分辨率预设
        // Common resolution presets
        private readonly int[] _presetSizes = { 64, 128, 256, 512, 1024, 2048 };

        /// <summary>
        /// 显示压缩窗口
        /// Show compress window
        /// </summary>
        internal static void ShowWindow(Moshi_AssetClean.TexCheckEntry info, string fullPath, Moshi_AssetClean parentWindow)
        {
            var window = GetWindow<Moshi_AssetClean_TexCompressWindow>(true, "压缩贴图 - 自定义分辨率", true);
            window._info = info;
            window._fullPath = fullPath;
            window._parentWindow = parentWindow;
            window._targetWidth = info.width;
            window._targetHeight = info.height;
            window.minSize = new Vector2(400, 380);
            window.maxSize = new Vector2(500, 480);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (_info == null)
            {
                Close();
                return;
            }

            EditorGUILayout.Space(10);

            // 文件信息
            // File info
            EditorGUILayout.LabelField("文件信息", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("路径:", _info.path);
            EditorGUILayout.LabelField("当前分辨率:", $"{_info.width} x {_info.height}");

            GUIStyle redStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.red },
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField("当前文件大小:", FormatFileSize(_info.fileSize), redStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 目标分辨率设置
            // Target resolution settings
            EditorGUILayout.LabelField("目标分辨率设置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _syncWidthHeight = EditorGUILayout.Toggle("宽高同步 (正方形)", _syncWidthHeight);

            EditorGUILayout.Space(5);

            // 宽度输入
            // Width input
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标宽度:", GUILayout.Width(80));
            int newWidth = EditorGUILayout.IntField(_targetWidth, GUILayout.Width(80));
            if (newWidth != _targetWidth && newWidth > 0)
            {
                _targetWidth = newWidth;
                if (_syncWidthHeight) _targetHeight = _targetWidth;
            }

            foreach (int size in _presetSizes)
            {
                if (GUILayout.Button(size.ToString(), GUILayout.Width(45)))
                {
                    _targetWidth = size;
                    if (_syncWidthHeight) _targetHeight = size;
                }
            }
            EditorGUILayout.EndHorizontal();

            // 高度输入
            // Height input
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标高度:", GUILayout.Width(80));
            int newHeight = EditorGUILayout.IntField(_targetHeight, GUILayout.Width(80));
            if (newHeight != _targetHeight && newHeight > 0)
            {
                _targetHeight = newHeight;
                if (_syncWidthHeight) _targetWidth = _targetHeight;
            }

            foreach (int size in _presetSizes)
            {
                if (GUILayout.Button(size.ToString(), GUILayout.Width(45)))
                {
                    _targetHeight = size;
                    if (_syncWidthHeight) _targetWidth = size;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 快速设置按钮
            // Quick settings
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("快速设置:", GUILayout.Width(80));
            if (GUILayout.Button("50%"))
            {
                _targetWidth = Mathf.Max(1, _info.width / 2);
                _targetHeight = Mathf.Max(1, _info.height / 2);
            }
            if (GUILayout.Button("25%"))
            {
                _targetWidth = Mathf.Max(1, _info.width / 4);
                _targetHeight = Mathf.Max(1, _info.height / 4);
            }
            if (GUILayout.Button("保持原比例"))
            {
                _syncWidthHeight = false;
            }
            EditorGUILayout.EndHorizontal();

            // 第二行快速设置
            // Second row quick settings
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(80));
            if (GUILayout.Button("1024x1024")) { _targetWidth = 1024; _targetHeight = 1024; }
            if (GUILayout.Button("512x512")) { _targetWidth = 512; _targetHeight = 512; }
            if (GUILayout.Button("256x256")) { _targetWidth = 256; _targetHeight = 256; }
            if (GUILayout.Button("128x128")) { _targetWidth = 128; _targetHeight = 128; }
            EditorGUILayout.EndHorizontal();

            // 对齐到4的倍数
            // Align to multiple of 4
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(80));
            if (GUILayout.Button("对齐到4的倍数"))
            {
                _targetWidth = ((_targetWidth + 3) / 4) * 4;
                _targetHeight = ((_targetHeight + 3) / 4) * 4;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 预估信息
            // Estimated info
            EditorGUILayout.LabelField("预估结果", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            float compressionRatio = (float)(_targetWidth * _targetHeight) / (_info.width * _info.height);
            long estimatedSize = (long)(_info.fileSize * compressionRatio);

            EditorGUILayout.LabelField($"目标分辨率: {_targetWidth} x {_targetHeight}");
            EditorGUILayout.LabelField($"压缩比例: {compressionRatio:P1}");
            EditorGUILayout.LabelField($"预估文件大小: ~{FormatFileSize(estimatedSize)}");

            bool widthDivisible = _targetWidth % 4 == 0;
            bool heightDivisible = _targetHeight % 4 == 0;
            if (!widthDivisible || !heightDivisible)
            {
                EditorGUILayout.HelpBox("⚠️ 目标分辨率不是4的倍数，可能导致压缩问题", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox("注意: 原文件将被备份为 .backup 文件", MessageType.Info);

            EditorGUILayout.Space(10);

            // 操作按钮
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("取消", GUILayout.Width(100), GUILayout.Height(30)))
            {
                Close();
            }

            GUILayout.Space(20);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("确认压缩", GUILayout.Width(120), GUILayout.Height(30)))
            {
                if (_targetWidth <= 0 || _targetHeight <= 0)
                {
                    EditorUtility.DisplayDialog("错误", "目标分辨率必须大于0", "确定");
                    return;
                }

                _parentWindow.TexCheck_ExecuteCompress(_info, _fullPath, _targetWidth, _targetHeight);
                Close();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 格式化文件大小
        /// Format file size
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
