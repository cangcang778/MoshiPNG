#if UNITY_EDITOR
using UnityEngine;

namespace MoshiVFXGenerator.Cloner
{
    public static class VFXParticleParamScaler
    {
        public static void ApplyMultiplier(GameObject root, float multiplier, System.Collections.Generic.List<string> excludedLayerPaths = null)
        {
            if (root == null || Mathf.Approximately(multiplier, 1f)) return;
            multiplier = Mathf.Clamp(multiplier, 0.1f, 5f);

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ShouldSkip(root.transform, ps.transform, excludedLayerPaths)) continue;
                var main = ps.main;
                main.startLifetime = ScaleCurve(main.startLifetime, multiplier, 0.03f);
                main.startSpeed = ScaleCurve(main.startSpeed, multiplier, 0f);
                main.startSize = ScaleCurve(main.startSize, multiplier, 0.001f);
                main.maxParticles = Mathf.Max(8, Mathf.RoundToInt(main.maxParticles * multiplier));

                var emission = ps.emission;
                if (emission.enabled)
                {
                    emission.rateOverTime = ScaleCurve(emission.rateOverTime, multiplier, 0f);
                    emission.rateOverDistance = ScaleCurve(emission.rateOverDistance, multiplier, 0f);
                }
            }
        }

        private static bool ShouldSkip(Transform root, Transform target, System.Collections.Generic.List<string> excludedLayerPaths)
        {
            if (excludedLayerPaths == null || excludedLayerPaths.Count == 0 || root == null || target == null) return false;
            string path = Moshi_VFXPrefabUtil.GetTransformPath(root, target);
            string relative = path.Contains("/") ? path.Substring(path.IndexOf('/') + 1) : path;
            foreach (string excluded in excludedLayerPaths)
            {
                if (string.IsNullOrEmpty(excluded)) continue;
                string ex = excluded.Contains("/") ? excluded.Substring(excluded.IndexOf('/') + 1) : excluded;
                if (relative == ex || path == excluded || target.name == excluded) return true;
            }
            return false;
        }

        private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float multiplier, float minValue)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    curve.constant = Mathf.Max(minValue, curve.constant * multiplier);
                    return curve;
                case ParticleSystemCurveMode.TwoConstants:
                    curve.constantMin = Mathf.Max(minValue, curve.constantMin * multiplier);
                    curve.constantMax = Mathf.Max(minValue, curve.constantMax * multiplier);
                    return curve;
                case ParticleSystemCurveMode.Curve:
                case ParticleSystemCurveMode.TwoCurves:
                    curve.curveMultiplier = Mathf.Max(minValue, curve.curveMultiplier * multiplier);
                    return curve;
                default:
                    return curve;
            }
        }
    }
}
#endif
