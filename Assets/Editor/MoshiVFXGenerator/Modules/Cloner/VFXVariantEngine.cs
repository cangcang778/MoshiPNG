#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.Cloner
{
    public static class VFXVariantEngine
    {
        public static List<VFXCloneResult> GenerateVariants(VFXCloneConfig config)
        {
            var results = new List<VFXCloneResult>();
            if (config == null || config.sourcePrefab == null)
            {
                results.Add(Fail("源Prefab为空"));
                return results;
            }

            string sourcePath = AssetDatabase.GetAssetPath(config.sourcePrefab);
            if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(Fail("请选择有效的Prefab资源"));
                return results;
            }

            VFXQualityReport preflight = Moshi_VFXPreflightCheck.CheckCloneConfig(config);
            if (preflight.HasCriticalIssue())
            {
                results.Add(new VFXCloneResult
                {
                    success = false,
                    message = "生成前预检失败，请查看质检报告",
                    qualityReport = preflight
                });
                return results;
            }

            int count = Mathf.Max(1, config.variantCount);
            UnityEngine.Random.InitState(config.randomSeed);
            Moshi_VFXPrefabUtil.EnsureFolder(config.outputRoot);

            for (int i = 0; i < count; i++)
            {
                results.Add(GenerateSingle(config, sourcePath, i));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AddHistory(config, sourcePath, results);
            return results;
        }

        private static VFXCloneResult GenerateSingle(VFXCloneConfig config, string sourcePath, int index)
        {
            var result = new VFXCloneResult();
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            string variantName = BuildVariantName(config.namePattern, sourceName, index);
            result.variantName = variantName;

            string groupRoot = config.outputRoot.TrimEnd('/') + "/" + sourceName + "_Variants/" + variantName;
            string prefabFolder = groupRoot + "/Prefab";
            string materialFolder = groupRoot + "/Material";
            string reportFolder = groupRoot + "/Report";

            try
            {
                Moshi_VFXPrefabUtil.EnsureFolder(prefabFolder);
                Moshi_VFXPrefabUtil.EnsureFolder(materialFolder);
                Moshi_VFXPrefabUtil.EnsureFolder(reportFolder);

                string prefabPath = Moshi_VFXPrefabUtil.GetUniqueAssetPath(prefabFolder + "/" + variantName + ".prefab");
                if (!AssetDatabase.CopyAsset(sourcePath, prefabPath))
                    throw new Exception("复制Prefab失败");

                result.createdAssets.Add(prefabPath);
                result.prefabPath = prefabPath;

                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    root.name = variantName;

                    if (config.cloneMaterials)
                        VFXMaterialSwapper.CloneAndAssignMaterials(root, materialFolder, result.createdAssets);

                    ApplyColor(root, config, index);
                    ApplyTexture(root, config);

                    if (config.paramMode == VFXCloneParamMode.Multiplier)
                        VFXParticleParamScaler.ApplyMultiplier(root, config.parameterMultiplier);
                    else if (config.paramMode == VFXCloneParamMode.Random)
                        VFXParticleParamScaler.ApplyMultiplier(root, UnityEngine.Random.Range(0.75f, 1.25f));

                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                if (config.runQualityCheck)
                    result.qualityReport = Moshi_VFXQualityCheck.CheckPrefab(prefabPath);

                result.success = true;
                result.message = "生成成功";
                WriteCloneReport(reportFolder, config, result);
            }
            catch (Exception ex)
            {
                result.success = false;
                result.message = ex.Message;
                Moshi_VFXPrefabUtil.DeleteCreatedAssets(result.createdAssets);
            }

            return result;
        }

        private static string BuildVariantName(string pattern, string sourceName, int index)
        {
            if (string.IsNullOrEmpty(pattern)) pattern = "{source}_{index}";
            string value = pattern
                .Replace("{source}", sourceName)
                .Replace("{index}", (index + 1).ToString("00"));
            return Moshi_VFXPrefabUtil.SanitizeFileName(value);
        }

        private static void ApplyColor(GameObject root, VFXCloneConfig config, int index)
        {
            switch (config.colorMode)
            {
                case VFXCloneColorMode.Palette:
                    VFXColorRemapper.ApplyPalette(root, config.paletteColor, config.excludedColorLayerPaths);
                    break;
                case VFXCloneColorMode.Random:
                    VFXColorRemapper.ApplyHueShift(root, UnityEngine.Random.Range(0f, 1f), config.excludedColorLayerPaths);
                    break;
                case VFXCloneColorMode.HueShift:
                default:
                    VFXColorRemapper.ApplyHueShift(root, config.hueStep * (index + 1), config.excludedColorLayerPaths);
                    break;
            }
        }

        private static void ApplyTexture(GameObject root, VFXCloneConfig config)
        {
            Texture texture = null;
            if (config.textureMode == VFXCloneTextureMode.Manual)
                texture = config.replacementTexture;
            else if (config.textureMode == VFXCloneTextureMode.ByTag)
                texture = PickTextureByTag(config.textureTag);

            if (texture != null)
                VFXMaterialSwapper.ReplaceTextures(root, texture);
        }

        private static Texture PickTextureByTag(string tag)
        {
            var candidates = new List<VFXAssetInfo>();
            foreach (VFXAssetInfo info in Moshi_VFXAssetDB.Instance.assets)
            {
                if (info.kind != VFXAssetKind.Texture) continue;
                if (string.IsNullOrEmpty(tag) || info.mainTag == tag || info.tags.Contains(tag))
                    candidates.Add(info);
            }

            if (candidates.Count == 0) return null;
            VFXAssetInfo selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return AssetDatabase.LoadAssetAtPath<Texture>(selected.path);
        }

        private static VFXCloneResult Fail(string message)
        {
            return new VFXCloneResult { success = false, message = message };
        }

        private static void WriteCloneReport(string reportFolder, VFXCloneConfig config, VFXCloneResult result)
        {
            string reportPath = Moshi_VFXPrefabUtil.GetUniqueAssetPath(reportFolder + "/" + result.variantName + "_生成报告.json");
            string json = JsonUtility.ToJson(new VFXCloneReportData
            {
                variantName = result.variantName,
                prefabPath = result.prefabPath,
                success = result.success,
                message = result.message,
                qualityReport = result.qualityReport,
                createdAssets = result.createdAssets,
                colorMode = config.colorMode.ToString(),
                textureMode = config.textureMode.ToString(),
                paramMode = config.paramMode.ToString(),
                randomSeed = config.randomSeed
            }, true);
            File.WriteAllText(reportPath, json);
            result.reportPath = reportPath;
            result.createdAssets.Add(reportPath);
        }

        [Serializable]
        private class VFXCloneReportData
        {
            public string variantName;
            public string prefabPath;
            public bool success;
            public string message;
            public VFXQualityReport qualityReport;
            public List<string> createdAssets;
            public string colorMode;
            public string textureMode;
            public string paramMode;
            public int randomSeed;
        }

        private static void AddHistory(VFXCloneConfig config, string sourcePath, List<VFXCloneResult> results)
        {
            int success = 0;
            int fail = 0;
            string firstOutput = string.Empty;
            string firstReport = string.Empty;
            var createdAssets = new List<string>();
            foreach (VFXCloneResult result in results)
            {
                if (result.success)
                {
                    success++;
                    if (string.IsNullOrEmpty(firstOutput)) firstOutput = result.prefabPath;
                    if (string.IsNullOrEmpty(firstReport)) firstReport = result.reportPath;
                    if (result.createdAssets != null) createdAssets.AddRange(result.createdAssets);
                }
                else
                {
                    fail++;
                }
            }

            Moshi_VFXHistory.Instance.AddRecord(new VFXHistoryRecord
            {
                operation = "克隆变体",
                sourcePath = sourcePath,
                outputPath = firstOutput,
                reportPath = firstReport,
                successCount = success,
                failCount = fail,
                configJson = JsonUtility.ToJson(config),
                randomSeed = config.randomSeed,
                createdAssets = createdAssets
            });
        }
    }
}
#endif
