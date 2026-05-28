#if UNITY_EDITOR
using MoshiVFXGenerator.Factory;
using UnityEngine;

namespace MoshiVFXGenerator
{
    public class Moshi_VFXGenPreset : ScriptableObject
    {
        [Header("克隆器配置")]
        public VFXCloneConfig cloneConfig = new VFXCloneConfig();

        [Header("配方生成配置")]
        public VFXFactoryConfig factoryConfig = new VFXFactoryConfig();
    }
}
#endif
