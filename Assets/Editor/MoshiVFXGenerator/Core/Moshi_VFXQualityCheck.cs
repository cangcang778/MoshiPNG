#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator
{
    public static class Moshi_VFXQualityCheck
    {
        public static VFXQualityReport CheckPrefab(string prefabPath)
        {
            return CheckPrefab(prefabPath, Moshi_VFXGenSettings.Instance);
        }

        public static VFXQualityReport CheckPrefab(string prefabPath, Moshi_VFXGenSettings settings)
        {
            if (settings == null) settings = Moshi_VFXGenSettings.Instance;
            var report = new VFXQualityReport { prefabPath = prefabPath };
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                AddIssue(report, VFXIssueSeverity.Critical, "Prefab无效", "无法加载 Prefab", prefabPath);
                return report;
            }

            ParticleSystem[] particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);

            report.particleSystemCount = particleSystems.Length;
            report.rendererCount = renderers.Length;

            var materials = new HashSet<Material>();
            var shaders = new HashSet<Shader>();

            foreach (ParticleSystem ps in particleSystems)
            {
                var main = ps.main;
                report.totalMaxParticles += main.maxParticles;
                if (main.loop) report.loopingParticleCount++;

                if (main.maxParticles <= 0)
                    AddIssue(report, VFXIssueSeverity.Warning, "粒子数为0", "该粒子系统 maxParticles 为 0", GetPath(prefab.transform, ps.transform));
            }

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                Material[] sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length == 0)
                {
                    AddIssue(report, VFXIssueSeverity.Critical, "缺少材质", "Renderer 没有材质数组", GetPath(prefab.transform, renderer.transform));
                    continue;
                }

                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    Material mat = sharedMaterials[i];
                    if (mat == null)
                    {
                        AddIssue(report, VFXIssueSeverity.Critical, "缺少材质", $"Renderer 第 {i} 个材质为空", GetPath(prefab.transform, renderer.transform));
                        continue;
                    }
                    materials.Add(mat);
                    if (mat.shader != null) shaders.Add(mat.shader);
                }
            }

            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == null)
                    AddIssue(report, VFXIssueSeverity.Critical, "缺少Mesh", "MeshFilter 的 sharedMesh 为空", GetPath(prefab.transform, meshFilter.transform));
            }

            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                Vector3 scale = transform.localScale;
                if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f) || Mathf.Approximately(scale.z, 0f))
                    AddIssue(report, VFXIssueSeverity.Critical, "缩放为0", "对象 localScale 存在 0，会导致不可见或异常", GetPath(prefab.transform, transform));
                if (Mathf.Abs(scale.x) > 100f || Mathf.Abs(scale.y) > 100f || Mathf.Abs(scale.z) > 100f)
                    AddIssue(report, VFXIssueSeverity.Warning, "缩放过大", "对象 localScale 超过 100，建议检查", GetPath(prefab.transform, transform));
            }

            report.materialCount = materials.Count;
            report.shaderCount = shaders.Count;

            if (report.totalMaxParticles > settings.criticalMaxParticles)
                AddIssue(report, VFXIssueSeverity.Critical, "粒子量严重超标", $"总最大粒子数 {report.totalMaxParticles} > {settings.criticalMaxParticles}", prefabPath);
            else if (report.totalMaxParticles > settings.warningMaxParticles)
                AddIssue(report, VFXIssueSeverity.Warning, "粒子量偏高", $"总最大粒子数 {report.totalMaxParticles} > {settings.warningMaxParticles}", prefabPath);

            if (report.particleSystemCount > settings.warningParticleSystems)
                AddIssue(report, VFXIssueSeverity.Warning, "粒子系统较多", $"粒子系统数量 {report.particleSystemCount} > {settings.warningParticleSystems}", prefabPath);
            if (report.materialCount > settings.warningMaterials)
                AddIssue(report, VFXIssueSeverity.Warning, "材质数量较多", $"材质数量 {report.materialCount} > {settings.warningMaterials}", prefabPath);
            if (report.shaderCount > settings.warningShaders)
                AddIssue(report, VFXIssueSeverity.Warning, "Shader数量较多", $"Shader 数量 {report.shaderCount} > {settings.warningShaders}", prefabPath);
            if (report.loopingParticleCount > settings.warningLoopingParticles)
                AddIssue(report, VFXIssueSeverity.Warning, "循环粒子较多", $"循环粒子数量 {report.loopingParticleCount} > {settings.warningLoopingParticles}", prefabPath);

            return report;
        }

        public static VFXQualityReport AutoFixPrefab(string prefabPath, Moshi_VFXGenSettings settings)
        {
            if (settings == null) settings = Moshi_VFXGenSettings.Instance;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                return CheckPrefab(prefabPath, settings);
            try
            {
                ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
                int total = 0;
                foreach (ParticleSystem ps in particleSystems)
                    total += ps.main.maxParticles;

                float ratio = total > settings.autoFixTargetMaxParticles && total > 0
                    ? settings.autoFixTargetMaxParticles / (float)total
                    : 1f;

                foreach (ParticleSystem ps in particleSystems)
                {
                    var main = ps.main;
                    int fixedCount = Mathf.RoundToInt(main.maxParticles * ratio);
                    fixedCount = Mathf.Clamp(fixedCount, 8, Mathf.Max(8, settings.autoFixMaxParticlesPerSystem));
                    main.maxParticles = fixedCount;

                    if (main.startLifetime.constant <= 0f && main.startLifetime.mode == ParticleSystemCurveMode.Constant)
                        main.startLifetime = 0.03f;
                    if (main.simulationSpeed > 5f)
                        main.simulationSpeed = 5f;
                }

                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    Vector3 scale = transform.localScale;
                    if (Mathf.Approximately(scale.x, 0f)) scale.x = 0.001f;
                    if (Mathf.Approximately(scale.y, 0f)) scale.y = 0.001f;
                    if (Mathf.Approximately(scale.z, 0f)) scale.z = 0.001f;
                    transform.localScale = scale;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return CheckPrefab(prefabPath, settings);
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

        private static string GetPath(Transform root, Transform target)
        {
            return Moshi_VFXPrefabUtil.GetTransformPath(root, target);
        }
    }
}
#endif
