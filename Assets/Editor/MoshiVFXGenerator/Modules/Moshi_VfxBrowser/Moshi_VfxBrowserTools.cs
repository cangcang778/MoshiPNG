#if UNITY_EDITOR
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
#if MOSHI_VFX_GRAPH_INSTALLED
using UnityEngine.VFX;
#endif

namespace MoshiVFXGenerator.VfxBrowser
{
    /// <summary>
    /// 特效浏览器工具类（时长计算 / 曲线采样 / 配置资源加载）
    /// VFX Browser utility class (duration calc, curve sampling, setting asset loading)
    /// </summary>
    public static class Moshi_VfxBrowserTools
    {
        private const int CurveSampleCount = 20; // 曲线采样精度 / Curve sample precision

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 加载 ScriptableObject 配置资源
        /// Load ScriptableObject setting asset
        /// </summary>
        public static T LoadSettingAsset<T>(string configPath, [CallerFilePath] string sourceFilePath = "") where T : ScriptableObject
        {
            string finalPath = GetFinalPath(configPath, sourceFilePath);
            return AssetDatabase.LoadAssetAtPath<T>(finalPath);
        }

        /// <summary>
        /// 保存 ScriptableObject 配置资源
        /// Save ScriptableObject setting asset
        /// </summary>
        public static void SaveSettingAsset<T>(T instance, string configPath, [CallerFilePath] string sourceFilePath = "") where T : ScriptableObject
        {
            string finalPath = GetFinalPath(configPath, sourceFilePath);

            string directory = System.IO.Path.GetDirectoryName(finalPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(instance, finalPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 将相对路径转换为 Assets/ 开头的项目路径
        /// Convert relative path to Assets/ prefixed project path
        /// </summary>
        private static string GetFinalPath(string configPath, string sourceFilePath)
        {
            string finalPath = configPath;
            if (!configPath.StartsWith("Assets/"))
            {
                string sourceDir = System.IO.Path.GetDirectoryName(sourceFilePath);
                string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(sourceDir ?? "", configPath));
                fullPath = fullPath.Replace('\\', '/');
                int assetsIndex = fullPath.IndexOf("Assets/");
                if (assetsIndex != -1)
                {
                    finalPath = fullPath.Substring(assetsIndex);
                }
            }
            return finalPath;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetMaxValue(ParticleSystem.MinMaxCurve minMaxCurve)
        {
            switch (minMaxCurve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return minMaxCurve.constant;
                case ParticleSystemCurveMode.Curve:
                    return GetMaxValue(minMaxCurve.curve);
                case ParticleSystemCurveMode.TwoConstants:
                    return minMaxCurve.constantMax;
                case ParticleSystemCurveMode.TwoCurves:
                    var ret1 = GetMaxValue(minMaxCurve.curveMin);
                    var ret2 = GetMaxValue(minMaxCurve.curveMax);
                    return ret1 > ret2 ? ret1 : ret2;
            }
            return -1f;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetMinValue(ParticleSystem.MinMaxCurve minMaxCurve)
        {
            switch (minMaxCurve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return minMaxCurve.constant;
                case ParticleSystemCurveMode.Curve:
                    return GetMinValue(minMaxCurve.curve);
                case ParticleSystemCurveMode.TwoConstants:
                    return minMaxCurve.constantMin;
                case ParticleSystemCurveMode.TwoCurves:
                    var ret1 = GetMinValue(minMaxCurve.curveMin);
                    var ret2 = GetMinValue(minMaxCurve.curveMax);
                    return ret1 < ret2 ? ret1 : ret2;
            }
            return -1f;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetMaxValue(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;

            var ret = float.MinValue;
            var frames = curve.keys;

            for (var i = 0; i < frames.Length; i++)
            {
                var value = frames[i].value;
                if (value > ret) ret = value;
            }

            if (frames.Length >= 2)
            {
                var startTime = frames[0].time;
                var endTime = frames[frames.Length - 1].time;
                var step = (endTime - startTime) / CurveSampleCount;
                for (var i = 0; i <= CurveSampleCount; i++)
                {
                    var value = curve.Evaluate(startTime + step * i);
                    if (value > ret) ret = value;
                }
            }

            return ret;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetMinValue(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;

            var ret = float.MaxValue;
            var frames = curve.keys;

            for (var i = 0; i < frames.Length; i++)
            {
                var value = frames[i].value;
                if (value < ret) ret = value;
            }

            if (frames.Length >= 2)
            {
                var startTime = frames[0].time;
                var endTime = frames[frames.Length - 1].time;
                var step = (endTime - startTime) / CurveSampleCount;
                for (var i = 0; i <= CurveSampleCount; i++)
                {
                    var value = curve.Evaluate(startTime + step * i);
                    if (value < ret) ret = value;
                }
            }

            return ret;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private static float GetLastBurstTime(ParticleSystem.EmissionModule emission)
        {
            var burstCount = emission.burstCount;
            if (burstCount == 0) return 0f;

            var lastBurstTime = 0f;
            for (var i = 0; i < burstCount; i++)
            {
                var burst = emission.GetBurst(i);
                var burstTime = burst.time;
                if (burstTime > lastBurstTime)
                {
                    lastBurstTime = burstTime;
                }
            }
            return lastBurstTime;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private static float GetTrailDuration(ParticleSystem.TrailModule trails, float particleLifetime)
        {
            if (!trails.enabled) return 0f;
            return GetMaxValue(trails.lifetime) * particleLifetime;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetDuration(ParticleSystem particle, bool allowLoop = true)
        {
            var emission = particle.emission;
            if (!emission.enabled) return 0f;

            if (particle.TryGetComponent<ParticleSystemRenderer>(out var renderer))
            {
                if (!renderer.enabled) return 0f;
            }
            else
            {
                return 0f;
            }

            var main = particle.main;
            var startDelay = GetMaxValue(main.startDelay);
            var startLifetime = GetMaxValue(main.startLifetime);
            var trailDuration = GetTrailDuration(particle.trails, startLifetime);

            if (main.loop)
            {
                if (!allowLoop) return -1f;
                return startDelay + main.duration + startLifetime + trailDuration;
            }

            float baseDuration;
            var rateOverTime = GetMinValue(emission.rateOverTime);
            if (rateOverTime <= 0)
            {
                var lastBurstTime = GetLastBurstTime(emission);
                baseDuration = startDelay + lastBurstTime + startLifetime;
            }
            else
            {
                baseDuration = startDelay + Mathf.Max(main.duration, startLifetime);
            }

            return baseDuration + trailDuration;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private static float GetSubEmitterExtraDuration(ParticleSystem particle, bool allowLoop)
        {
            var subEmitters = particle.subEmitters;
            if (!subEmitters.enabled) return 0f;

            var maxExtraDuration = 0f;
            var subEmitterCount = subEmitters.subEmittersCount;

            for (var i = 0; i < subEmitterCount; i++)
            {
                var subPs = subEmitters.GetSubEmitterSystem(i);
                if (subPs == null) continue;

                var subType = subEmitters.GetSubEmitterType(i);
                var subDuration = GetDuration(subPs, allowLoop);

                if (subType == ParticleSystemSubEmitterType.Death)
                {
                    if (subDuration > maxExtraDuration)
                    {
                        maxExtraDuration = subDuration;
                    }
                }
            }

            return maxExtraDuration;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetParticleDuration(GameObject gameObject, bool includeChildren = true, bool includeInactive = false, bool allowLoop = true)
        {
            if (includeChildren)
            {
                var particles = gameObject.GetComponentsInChildren<ParticleSystem>(includeInactive);
                var duration = -1f;
                for (var i = 0; i < particles.Length; i++)
                {
                    var ps = particles[i];
                    var time = GetDuration(ps, allowLoop);
                    var subEmitterExtraTime = GetSubEmitterExtraDuration(ps, allowLoop);
                    var totalTime = time + subEmitterExtraTime;

                    if (totalTime > duration)
                    {
                        duration = totalTime;
                    }
                }

                return duration;
            }
            else
            {
                var ps = gameObject.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var time = GetDuration(ps, allowLoop);
                    var subEmitterExtraTime = GetSubEmitterExtraDuration(ps, allowLoop);
                    return time + subEmitterExtraTime;
                }
                return -1f;
            }
        }

#if MOSHI_VFX_GRAPH_INSTALLED
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetDuration(VisualEffect vfx, bool allowLoop = true, string durationPropertyName = "Duration")
        {
            if (vfx == null || vfx.visualEffectAsset == null) return -1f;

            if (vfx.HasFloat(durationPropertyName))
            {
                return vfx.GetFloat(durationPropertyName);
            }

            string[] commonDurationNames = { "Duration", "duration", "TotalTime", "totalTime", "EffectDuration", "Lifetime", "lifetime" };
            foreach (var name in commonDurationNames)
            {
                if (vfx.HasFloat(name))
                {
                    return vfx.GetFloat(name);
                }
            }

            if (vfx.HasBool("Loop") && vfx.GetBool("Loop"))
            {
                return allowLoop ? 3f : -1f;
            }

            return 3f;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetVfxDuration(GameObject gameObject, bool includeChildren = true, bool includeInactive = false, bool allowLoop = true, string durationPropertyName = "Duration")
        {
            if (includeChildren)
            {
                var vfxList = gameObject.GetComponentsInChildren<VisualEffect>(includeInactive);
                var duration = -1f;
                for (var i = 0; i < vfxList.Length; i++)
                {
                    var vfx = vfxList[i];
                    var time = GetDuration(vfx, allowLoop, durationPropertyName);
                    if (time > duration) duration = time;
                }
                return duration;
            }
            else
            {
                var vfx = gameObject.GetComponent<VisualEffect>();
                if (vfx != null) return GetDuration(vfx, allowLoop, durationPropertyName);
                return -1f;
            }
        }
#else
        public static float GetVfxDuration(GameObject gameObject, bool includeChildren = true, bool includeInactive = false, bool allowLoop = true, string durationPropertyName = "Duration")
        {
            return -1f;
        }
#endif

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 获取特效的播放时间长度（自动检测 ParticleSystem 和 VFX Graph）
        /// Get effect playback duration (auto-detects ParticleSystem and VFX Graph)
        /// </summary>
        public static float GetEffectDuration(GameObject gameObject, bool includeChildren = true, bool includeInactive = false, bool allowLoop = true)
        {
            var particleDuration = GetParticleDuration(gameObject, includeChildren, includeInactive, allowLoop);
            var vfxDuration = GetVfxDuration(gameObject, includeChildren, includeInactive, allowLoop);
            return Mathf.Max(particleDuration, vfxDuration);
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 检查 Prefab 是否包含特效组件（ParticleSystem / VFX Graph / Canvas / Animator + Animation Clip）
        /// Check if prefab contains VFX components
        /// </summary>
        public static bool HasVfxComponent(GameObject prefab)
        {
            if (prefab == null) return false;

            bool hasParticle = prefab.GetComponent<ParticleSystem>() != null ||
                               prefab.GetComponentInChildren<ParticleSystem>(true) != null;
            if (hasParticle) return true;

#if MOSHI_VFX_GRAPH_INSTALLED
            bool hasVFX = prefab.GetComponent<VisualEffect>() != null ||
                          prefab.GetComponentInChildren<VisualEffect>(true) != null;
            if (hasVFX) return true;
#endif

            // UGUI 特效：含 Canvas 且有 Animator 或 Image/RawImage（认为是 UI 动效 Prefab）
            // UGUI VFX: has Canvas + Animator/Image (considered UI animation prefab)
            var canvas = prefab.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                var animator = prefab.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.runtimeAnimatorController != null) return true;
            }

            return false;
        }
    }
}
#endif
