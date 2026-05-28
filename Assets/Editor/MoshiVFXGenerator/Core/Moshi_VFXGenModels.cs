#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoshiVFXGenerator
{
    public enum VFXAssetKind { Unknown, Shader, Material, Texture, Mesh, Prefab }
    public enum VFXIssueSeverity { Info, Warning, Critical }
    public enum VFXCloneColorMode { HueShift, Palette, Random }
    public enum VFXCloneParamMode { None, Multiplier, Random }
    public enum VFXCloneTextureMode { None, Manual, ByTag }

    [Serializable]
    public class VFXAssetInfo
    {
        public string guid;
        public string path;
        public string name;
        public VFXAssetKind kind;
        public string mainTag;
        public List<string> tags = new List<string>();
        public string shaderName;
        public long fileSize;
        public bool favorite;
    }

    [Serializable]
    public class VFXLayerInfo
    {
        public string path;
        public string name;
        public string componentType;
        public string materialPath;
        public string shaderName;
        public int maxParticles;
        public bool looping;
        public bool missingMaterial;
        public bool missingMesh;
    }

    [Serializable]
    public class VFXPrefabInfo
    {
        public string prefabPath;
        public string prefabName;
        public int particleSystemCount;
        public int rendererCount;
        public int materialCount;
        public int totalMaxParticles;
        public List<VFXLayerInfo> layers = new List<VFXLayerInfo>();
    }

    [Serializable]
    public class VFXQualityIssue
    {
        public VFXIssueSeverity severity;
        public string title;
        public string message;
        public string objectPath;
    }

    [Serializable]
    public class VFXQualityReport
    {
        public string prefabPath;
        public int totalMaxParticles;
        public int particleSystemCount;
        public int rendererCount;
        public int materialCount;
        public int shaderCount;
        public int loopingParticleCount;
        public List<VFXQualityIssue> issues = new List<VFXQualityIssue>();

        public bool HasCriticalIssue()
        {
            return issues.Exists(i => i.severity == VFXIssueSeverity.Critical);
        }

        public int GetWarningCount()
        {
            return issues.FindAll(i => i.severity == VFXIssueSeverity.Warning).Count;
        }

        public int GetCriticalCount()
        {
            return issues.FindAll(i => i.severity == VFXIssueSeverity.Critical).Count;
        }
    }

    [Serializable]
    public class VFXCloneConfig
    {
        public GameObject sourcePrefab;
        public string outputRoot = "Assets/MoShi/GeneratedVFX";
        public int variantCount = 3;
        public string namePattern = "{source}_{index}";
        public VFXCloneColorMode colorMode = VFXCloneColorMode.HueShift;
        public VFXCloneParamMode paramMode = VFXCloneParamMode.Multiplier;
        public VFXCloneTextureMode textureMode = VFXCloneTextureMode.None;
        public float hueStep = 0.08f;
        public Color paletteColor = Color.white;
        public float parameterMultiplier = 1f;
        public Texture2D replacementTexture;
        public string textureTag = "噪声";
        public int randomSeed = 12345;
        public bool cloneMaterials = true;
        public bool runQualityCheck = true;
        public List<string> excludedColorLayerPaths = new List<string>();
        public List<string> excludedTextureLayerPaths = new List<string>();
        public List<string> excludedParamLayerPaths = new List<string>();
    }

    [Serializable]
    public class VFXCloneResult
    {
        public bool success;
        public string variantName;
        public string prefabPath;
        public string reportPath;
        public string message;
        public VFXQualityReport qualityReport;
        public List<string> createdAssets = new List<string>();
    }

    [Serializable]
    public class VFXHistoryRecord
    {
        public string time;
        public string operation;
        public string sourcePath;
        public string outputPath;
        public string reportPath;
        public int successCount;
        public int failCount;
        public string configJson;
        public int randomSeed;
        public List<string> createdAssets = new List<string>();
    }
}
#endif
