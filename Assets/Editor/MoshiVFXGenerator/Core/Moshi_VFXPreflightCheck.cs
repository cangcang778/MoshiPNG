#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator
{
    public static class Moshi_VFXPreflightCheck
    {
        public static VFXQualityReport CheckCloneConfig(VFXCloneConfig config)
        {
            var report = new VFXQualityReport { prefabPath = config != null && config.sourcePrefab != null ? AssetDatabase.GetAssetPath(config.sourcePrefab) : string.Empty };
            if (config == null)
            {
                AddIssue(report, VFXIssueSeverity.Critical, "配置为空", "克隆配置为空", string.Empty);
                return report;
            }

            if (config.sourcePrefab == null)
                AddIssue(report, VFXIssueSeverity.Critical, "缺少源Prefab", "请选择源 Prefab 后再生成", string.Empty);
            else
            {
                string sourcePath = AssetDatabase.GetAssetPath(config.sourcePrefab);
                if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    AddIssue(report, VFXIssueSeverity.Critical, "源资源无效", "源资源不是有效 Prefab", sourcePath);
                CheckMissingReferences(config.sourcePrefab, report);
            }

            if (string.IsNullOrEmpty(config.outputRoot) || !config.outputRoot.StartsWith("Assets/"))
                AddIssue(report, VFXIssueSeverity.Critical, "输出目录无效", "输出目录必须位于 Assets 下", config.outputRoot);

            if (config.variantCount <= 0)
                AddIssue(report, VFXIssueSeverity.Critical, "变体数量无效", "变体数量必须大于 0", string.Empty);

            if (config.textureMode == VFXCloneTextureMode.Manual && config.replacementTexture == null)
                AddIssue(report, VFXIssueSeverity.Warning, "替换贴图为空", "贴图模式为手动，但没有指定贴图，将跳过贴图替换", string.Empty);

            if (config.textureMode == VFXCloneTextureMode.ByTag && CountTextureByTag(config.textureTag) == 0)
                AddIssue(report, VFXIssueSeverity.Warning, "没有匹配贴图", $"素材库中没有标签为 [{config.textureTag}] 的贴图，将跳过贴图替换", string.Empty);

            return report;
        }

        public static VFXQualityReport CheckOutputConfig(string outputRoot, string effectName)
        {
            var report = new VFXQualityReport { prefabPath = outputRoot };
            if (string.IsNullOrEmpty(outputRoot) || !outputRoot.StartsWith("Assets/"))
                AddIssue(report, VFXIssueSeverity.Critical, "输出目录无效", "输出目录必须位于 Assets 下", outputRoot);
            if (string.IsNullOrEmpty(effectName))
                AddIssue(report, VFXIssueSeverity.Critical, "名称为空", "生成名称不能为空", outputRoot);
            return report;
        }

        private static int CountTextureByTag(string tag)
        {
            int count = 0;
            foreach (VFXAssetInfo info in Moshi_VFXAssetDB.Instance.assets)
            {
                if (info.kind != VFXAssetKind.Texture) continue;
                if (string.IsNullOrEmpty(tag) || info.mainTag == tag || info.tags.Contains(tag))
                    count++;
            }
            return count;
        }

        private static void CheckMissingReferences(GameObject prefab, VFXQualityReport report)
        {
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0)
                    AddIssue(report, VFXIssueSeverity.Warning, "Renderer无材质", "该 Renderer 没有材质数组", GetPath(prefab.transform, renderer.transform));
                else
                {
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null)
                            AddIssue(report, VFXIssueSeverity.Warning, "材质为空", $"Renderer 第 {i} 个材质为空", GetPath(prefab.transform, renderer.transform));
                    }
                }
            }

            foreach (MeshFilter meshFilter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null)
                    AddIssue(report, VFXIssueSeverity.Warning, "Mesh为空", "MeshFilter 的 sharedMesh 为空", GetPath(prefab.transform, meshFilter.transform));
            }
        }

        private static string GetPath(Transform root, Transform target)
        {
            return Moshi_VFXPrefabUtil.GetTransformPath(root, target);
        }

        private static void AddIssue(VFXQualityReport report, VFXIssueSeverity severity, string title, string message, string objectPath)
        {
            report.issues.Add(new VFXQualityIssue
            {
                severity = severity,
                title = title,
                message = message,
                objectPath = objectPath
            });
        }
    }
}
#endif
