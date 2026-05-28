#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace MoshiVFXGenerator.Blueprint
{
    public static class VFXBlueprintSerializer
    {
        public static void Save(string path, VFXBlueprint blueprint)
        {
            if (blueprint == null || string.IsNullOrEmpty(path)) return;
            string json = JsonUtility.ToJson(blueprint, true);
            File.WriteAllText(path, json);
        }

        public static VFXBlueprint Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<VFXBlueprint>(json);
        }
    }
}
#endif
