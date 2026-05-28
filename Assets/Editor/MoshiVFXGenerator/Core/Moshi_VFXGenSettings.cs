#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator
{
    public class Moshi_VFXGenSettings : ScriptableObject
    {
        private const string ASSET_PATH = "Assets/Editor/MoshiVFXGenerator/Settings/MoshiVFXGeneratorSettings.asset";

        [Header("路径设置")]
        [Tooltip("默认生成输出目录")]
        public string defaultOutputRoot = "Assets/MoShi/GeneratedVFX";

        [Tooltip("素材库默认扫描目录")]
        public List<string> scanFolders = new List<string> { "Assets/Effect", "Assets/MoShi" };

        [Header("克隆设置")]
        [Tooltip("默认变体数量")]
        [Min(1)] public int defaultVariantCount = 3;

        [Tooltip("默认命名规则")]
        public string defaultNamePattern = "{source}_{index}";

        [Tooltip("默认色相步进")]
        public float defaultHueStep = 0.08f;

        [Header("质检预算")]
        [Tooltip("总最大粒子数警告阈值")]
        public int warningMaxParticles = 3000;

        [Tooltip("总最大粒子数严重阈值")]
        public int criticalMaxParticles = 8000;

        [Tooltip("粒子系统数量警告阈值")]
        public int warningParticleSystems = 12;

        [Tooltip("材质数量警告阈值")]
        public int warningMaterials = 6;

        [Tooltip("Shader数量警告阈值")]
        public int warningShaders = 4;

        [Tooltip("循环粒子数量警告阈值")]
        public int warningLoopingParticles = 5;

        [Header("自动修复")]
        [Tooltip("自动修复时目标总最大粒子数")]
        public int autoFixTargetMaxParticles = 3000;

        [Tooltip("自动修复时单个粒子系统最大粒子数")]
        public int autoFixMaxParticlesPerSystem = 600;

        public static Moshi_VFXGenSettings Instance
        {
            get
            {
                var asset = AssetDatabase.LoadAssetAtPath<Moshi_VFXGenSettings>(ASSET_PATH);
                if (asset != null) return asset;

                EnsureFolder("Assets/Editor/MoshiVFXGenerator/Settings");
                asset = CreateInstance<Moshi_VFXGenSettings>();
                AssetDatabase.CreateAsset(asset, ASSET_PATH);
                AssetDatabase.SaveAssets();
                return asset;
            }
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
