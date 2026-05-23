// Spine 版本兼容适配层
// Spine version compatibility adapter layer
// 集中管理 Spine 3.8、4.2 和 4.3+ 的 API 差异
// Centralized management of Spine 3.8, 4.2 and 4.3+ API differences
//
// HAS_SPINE 宏由 SpineDefineManager.cs 自动管理（HAS_SPINE 不存在鸡生蛋问题，
// 因为没有 Spine 时本文件整体跳过，有 Spine 时 SpineDefineManager 第一次运行
// 会添加 HAS_SPINE 并触发重编译，第二次编译时 HAS_SPINE 已就位）
// HAS_SPINE define is auto-managed by SpineDefineManager.cs
//
// 版本适配方案 / Version adaptation approach:
//   不使用条件编译（#if SPINE_4_3 等），因为宏设置和代码编译在同一次发生，
//   会导致鸡生蛋问题。改为运行时通过反射检测 Spine 版本，用委托分发到对应 API。
//   No conditional compilation (#if SPINE_4_3 etc.) because define setting and
//   code compilation happen in the same pass, causing chicken-and-egg problem.
//   Instead, detect Spine version at runtime via reflection and dispatch via delegates.
//
// 支持版本 / Supported versions:
//   Spine 3.8:  bone.WorldX, bone.AX, bone.AppliedRotation, bone.AScaleX, etc.
//   Spine 4.2:  bone.WorldX, bone.X, bone.Rotation, bone.ScaleX, bone.Inherit, etc.
//   Spine 4.3+: bone.AppliedPose.WorldX, bone.AppliedPose.X, bone.AppliedPose.Rotation, etc.

#if HAS_SPINE
using Spine;
using Spine.Unity;
using UnityEngine;
using System;
using System.Reflection;

namespace MoshiTools
{
    /// <summary>
    /// Spine 版本检测枚举
    /// Spine version detection enum
    /// </summary>
    public enum SpineVersion
    {
        Spine38,    // 3.8.x
        Spine42,    // 4.0 ~ 4.2.x
        Spine43     // 4.3+ (PosedActive 架构)
    }

    /// <summary>
    /// Spine 版本兼容适配层 - 运行时反射检测版本，委托分发 API
    /// Spine version compatibility adapter - runtime reflection detection, delegate dispatch
    /// </summary>
    public static class SpineCompat
    {
        // 当前检测到的 Spine 版本
        // Currently detected Spine version
        public static SpineVersion DetectedVersion { get; private set; }

        // 版本检测是否已完成
        // Whether version detection has been completed
        public static bool IsInitialized { get; private set; }

        // ══════════════════════════════════════════
        //  反射缓存
        //  Reflection cache
        // ══════════════════════════════════════════

        // 4.3+: bone.AppliedPose 属性的 PropertyInfo
        private static PropertyInfo _appliedPoseProp;

        // BonePose 类型上的各属性
        // Properties on BonePose type
        private static PropertyInfo _bp_WorldX, _bp_WorldY, _bp_WorldRotationX;
        private static PropertyInfo _bp_WorldScaleX, _bp_WorldScaleY;
        private static PropertyInfo _bp_X, _bp_Y, _bp_Rotation, _bp_ScaleX, _bp_ScaleY;
        private static PropertyInfo _bp_Inherit;

        // 3.8: bone 上的旧版属性
        // 3.8: legacy properties on Bone
        private static PropertyInfo _bone_WorldX, _bone_WorldY, _bone_WorldRotationX;
        private static PropertyInfo _bone_WorldScaleX, _bone_WorldScaleY;
        private static PropertyInfo _bone_AX, _bone_AY, _bone_AppliedRotation;
        private static PropertyInfo _bone_AScaleX, _bone_AScaleY;
        private static PropertyInfo _bone_Skeleton;

        // 4.2: bone 上的属性（与 3.8 世界属性相同，本地属性不同）
        // 4.2: properties on Bone (world same as 3.8, local differ)
        private static PropertyInfo _bone_X, _bone_Y, _bone_Rotation;
        private static PropertyInfo _bone_ScaleX, _bone_ScaleY;
        private static PropertyInfo _bone_Inherit;

        // BoneData 上的属性（3.8/4.2）
        // Properties on BoneData (3.8/4.2)
        private static PropertyInfo _data_X, _data_Y, _data_Rotation;
        private static PropertyInfo _data_ScaleX, _data_ScaleY;
        private static PropertyInfo _data_TransformMode;

        // BoneData.GetSetupPose() 方法（4.3+）
        // BoneData.GetSetupPose() method (4.3+)
        private static MethodInfo _data_GetSetupPose;

        // Setup Pose 对象上的属性（4.3+）
        // Properties on setup pose object (4.3+)
        private static PropertyInfo _sp_X, _sp_Y, _sp_Rotation, _sp_ScaleX, _sp_ScaleY;

        // Skeleton.SetupPose / SetToSetupPose
        private static MethodInfo _skeleton_SetupPose;
        private static MethodInfo _skeleton_SetToSetupPose;

        // Skeleton.UpdateWorldTransform
        private static MethodInfo _skeleton_UpdateWorldTransform_NoParam;
        private static MethodInfo _skeleton_UpdateWorldTransform_Physics;

        // Spine.Physics.Update 枚举值（4.2+）
        // Spine.Physics.Update enum value (4.2+)
        private static object _physicsUpdateValue;

        // TransformMode 枚举值（3.8）
        // TransformMode enum values (3.8)
        private static object _tm_Normal, _tm_NoScale, _tm_NoScaleOrReflection;

        // Inherit 枚举值（4.2+）
        // Inherit enum values (4.2+)
        private static object _inherit_Normal, _inherit_NoScale, _inherit_NoScaleOrReflection;

        // ══════════════════════════════════════════
        //  静态构造器 - 运行时版本检测
        //  Static constructor - runtime version detection
        // ══════════════════════════════════════════

        static SpineCompat()
        {
            DetectVersion();
            CacheReflection();
            IsInitialized = true;
            Debug.Log($"[Moshi] SpineCompat 初始化完成，检测到 Spine 版本: {DetectedVersion}");
        }

        /// <summary>
        /// 通过反射检测 Spine 版本
        /// Detect Spine version via reflection
        /// 4.3+: Spine.BonePose 类型存在
        /// 4.2:  Spine.Physics 枚举存在 且 BonePose 不存在
        /// 3.8:  以上都不存在
        /// </summary>
        private static void DetectVersion()
        {
            Type bonePoseType = null;
            Type physicsType = null;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name != "spine-csharp" && name != "spine-unity") continue;

                if (bonePoseType == null)
                    bonePoseType = assembly.GetType("Spine.BonePose");
                if (physicsType == null)
                    physicsType = assembly.GetType("Spine.Physics");
            }

            if (bonePoseType != null)
            {
                DetectedVersion = SpineVersion.Spine43;
            }
            else if (physicsType != null)
            {
                DetectedVersion = SpineVersion.Spine42;
            }
            else
            {
                DetectedVersion = SpineVersion.Spine38;
            }
        }

        /// <summary>
        /// 根据检测到的版本缓存对应的反射信息
        /// Cache reflection info based on detected version
        /// </summary>
        private static void CacheReflection()
        {
            Type boneType = typeof(Bone);
            Type boneDataType = typeof(BoneData);
            Type skeletonType = typeof(Skeleton);

            // ── Skeleton 方法 ──
            _skeleton_SetupPose = skeletonType.GetMethod("SetupPose", Type.EmptyTypes);
            _skeleton_SetToSetupPose = skeletonType.GetMethod("SetToSetupPose", Type.EmptyTypes);

            // UpdateWorldTransform - 无参版(3.8) 和 带Physics参数版(4.2+)
            _skeleton_UpdateWorldTransform_NoParam = skeletonType.GetMethod("UpdateWorldTransform", Type.EmptyTypes);

            // 查找 Spine.Physics 枚举
            Type physicsEnumType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name == "spine-csharp" || name == "spine-unity")
                {
                    physicsEnumType = assembly.GetType("Spine.Physics");
                    if (physicsEnumType != null) break;
                }
            }

            if (physicsEnumType != null)
            {
                _skeleton_UpdateWorldTransform_Physics = skeletonType.GetMethod("UpdateWorldTransform", new Type[] { physicsEnumType });
                // Physics.Update 枚举值
                _physicsUpdateValue = Enum.Parse(physicsEnumType, "Update");
            }

            switch (DetectedVersion)
            {
                case SpineVersion.Spine43:
                    CacheSpine43(boneType, boneDataType);
                    break;
                case SpineVersion.Spine42:
                    CacheSpine42(boneType, boneDataType);
                    break;
                case SpineVersion.Spine38:
                    CacheSpine38(boneType, boneDataType);
                    break;
            }
        }

        /// <summary>
        /// 缓存 4.3+ 反射信息（PosedActive 架构）
        /// Cache 4.3+ reflection info (PosedActive architecture)
        /// </summary>
        private static void CacheSpine43(Type boneType, Type boneDataType)
        {
            // bone.AppliedPose（返回 BonePose 对象）
            // 需要从基类链中查找，因为 AppliedPose 定义在 Posed<D,P,A> 泛型基类上
            _appliedPoseProp = FindProperty(boneType, "AppliedPose");

            if (_appliedPoseProp != null)
            {
                Type bonePoseType = _appliedPoseProp.PropertyType;

                // BonePose 世界属性
                _bp_WorldX = bonePoseType.GetProperty("WorldX");
                _bp_WorldY = bonePoseType.GetProperty("WorldY");
                _bp_WorldRotationX = bonePoseType.GetProperty("WorldRotationX");
                _bp_WorldScaleX = bonePoseType.GetProperty("WorldScaleX");
                _bp_WorldScaleY = bonePoseType.GetProperty("WorldScaleY");

                // BonePose 本地属性（继承自 BoneLocal）
                _bp_X = bonePoseType.GetProperty("X");
                _bp_Y = bonePoseType.GetProperty("Y");
                _bp_Rotation = bonePoseType.GetProperty("Rotation");
                _bp_ScaleX = bonePoseType.GetProperty("ScaleX");
                _bp_ScaleY = bonePoseType.GetProperty("ScaleY");
                _bp_Inherit = bonePoseType.GetProperty("Inherit");
            }

            // BoneData.GetSetupPose() → 返回 BoneLocal 对象
            _data_GetSetupPose = boneDataType.GetMethod("GetSetupPose", Type.EmptyTypes);
            if (_data_GetSetupPose == null)
            {
                // 从基类查找
                _data_GetSetupPose = FindMethod(boneDataType, "GetSetupPose", Type.EmptyTypes);
            }

            if (_data_GetSetupPose != null)
            {
                Type setupPoseType = _data_GetSetupPose.ReturnType;
                _sp_X = setupPoseType.GetProperty("X");
                _sp_Y = setupPoseType.GetProperty("Y");
                _sp_Rotation = setupPoseType.GetProperty("Rotation");
                _sp_ScaleX = setupPoseType.GetProperty("ScaleX");
                _sp_ScaleY = setupPoseType.GetProperty("ScaleY");
            }

            // Inherit 枚举值
            CacheInheritEnum();
        }

        /// <summary>
        /// 缓存 4.2 反射信息
        /// Cache 4.2 reflection info
        /// </summary>
        private static void CacheSpine42(Type boneType, Type boneDataType)
        {
            // 世界属性（直接在 Bone 上）
            _bone_WorldX = boneType.GetProperty("WorldX");
            _bone_WorldY = boneType.GetProperty("WorldY");
            _bone_WorldRotationX = boneType.GetProperty("WorldRotationX");
            _bone_WorldScaleX = boneType.GetProperty("WorldScaleX");
            _bone_WorldScaleY = boneType.GetProperty("WorldScaleY");

            // 本地属性（4.2 用 X/Y/Rotation/ScaleX/ScaleY）
            _bone_X = boneType.GetProperty("X");
            _bone_Y = boneType.GetProperty("Y");
            _bone_Rotation = boneType.GetProperty("Rotation");
            _bone_ScaleX = boneType.GetProperty("ScaleX");
            _bone_ScaleY = boneType.GetProperty("ScaleY");
            _bone_Inherit = boneType.GetProperty("Inherit");

            // BoneData 属性（4.2 直接 data.X/Y 等）
            _data_X = boneDataType.GetProperty("X");
            _data_Y = boneDataType.GetProperty("Y");
            _data_Rotation = boneDataType.GetProperty("Rotation");
            _data_ScaleX = boneDataType.GetProperty("ScaleX");
            _data_ScaleY = boneDataType.GetProperty("ScaleY");

            // Inherit 枚举值
            CacheInheritEnum();

            // Bone.Skeleton
            _bone_Skeleton = boneType.GetProperty("Skeleton");
        }

        /// <summary>
        /// 缓存 3.8 反射信息
        /// Cache 3.8 reflection info
        /// </summary>
        private static void CacheSpine38(Type boneType, Type boneDataType)
        {
            // 世界属性
            _bone_WorldX = boneType.GetProperty("WorldX");
            _bone_WorldY = boneType.GetProperty("WorldY");
            _bone_WorldRotationX = boneType.GetProperty("WorldRotationX");
            _bone_WorldScaleX = boneType.GetProperty("WorldScaleX");
            _bone_WorldScaleY = boneType.GetProperty("WorldScaleY");

            // 本地属性（3.8 用 AX/AY/AppliedRotation/AScaleX/AScaleY）
            _bone_AX = boneType.GetProperty("AX");
            _bone_AY = boneType.GetProperty("AY");
            _bone_AppliedRotation = boneType.GetProperty("AppliedRotation");
            _bone_AScaleX = boneType.GetProperty("AScaleX");
            _bone_AScaleY = boneType.GetProperty("AScaleY");

            // BoneData 属性
            _data_X = boneDataType.GetProperty("X");
            _data_Y = boneDataType.GetProperty("Y");
            _data_Rotation = boneDataType.GetProperty("Rotation");
            _data_ScaleX = boneDataType.GetProperty("ScaleX");
            _data_ScaleY = boneDataType.GetProperty("ScaleY");

            // TransformMode 枚举（3.8 用 bone.Data.TransformMode）
            _data_TransformMode = boneDataType.GetProperty("TransformMode");
            CacheTransformModeEnum();

            // Bone.Skeleton
            _bone_Skeleton = boneType.GetProperty("Skeleton");
        }

        /// <summary>
        /// 缓存 Inherit 枚举值（4.2+）
        /// Cache Inherit enum values (4.2+)
        /// </summary>
        private static void CacheInheritEnum()
        {
            Type inheritType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name != "spine-csharp" && name != "spine-unity") continue;

                inheritType = assembly.GetType("Spine.Inherit");
                if (inheritType != null) break;
            }

            if (inheritType == null) return;

            _inherit_Normal = Enum.Parse(inheritType, "Normal");
            _inherit_NoScale = Enum.Parse(inheritType, "NoScale");
            _inherit_NoScaleOrReflection = Enum.Parse(inheritType, "NoScaleOrReflection");
        }

        /// <summary>
        /// 缓存 TransformMode 枚举值（3.8）
        /// Cache TransformMode enum values (3.8)
        /// </summary>
        private static void CacheTransformModeEnum()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name != "spine-csharp" && name != "spine-unity") continue;

                Type tmType = assembly.GetType("Spine.TransformMode");
                if (tmType != null)
                {
                    _tm_Normal = Enum.Parse(tmType, "Normal");
                    _tm_NoScale = Enum.Parse(tmType, "NoScale");
                    _tm_NoScaleOrReflection = Enum.Parse(tmType, "NoScaleOrReflection");
                    break;
                }
            }
        }

        // ══════════════════════════════════════════
        //  辅助反射方法
        //  Helper reflection methods
        // ══════════════════════════════════════════

        /// <summary>
        /// 在类型及其基类链中查找属性
        /// Find property in type and its base class chain
        /// </summary>
        private static PropertyInfo FindProperty(Type type, string propertyName)
        {
            while (type != null && type != typeof(object))
            {
                var prop = type.GetProperty(propertyName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (prop != null) return prop;
                type = type.BaseType;
            }
            return null;
        }

        /// <summary>
        /// 在类型及其基类链中查找方法
        /// Find method in type and its base class chain
        /// </summary>
        private static MethodInfo FindMethod(Type type, string methodName, Type[] paramTypes)
        {
            while (type != null && type != typeof(object))
            {
                var method = type.GetMethod(methodName, paramTypes);
                if (method != null) return method;
                type = type.BaseType;
            }
            return null;
        }

        /// <summary>
        /// 安全获取属性值
        /// Safely get property value
        /// </summary>
        private static float GetFloat(PropertyInfo prop, object target, float fallback = 0f)
        {
            if (prop == null || target == null) return fallback;
            try
            {
                return (float)prop.GetValue(target);
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        /// 获取 4.3+ bone.AppliedPose 对象
        /// Get 4.3+ bone.AppliedPose object
        /// </summary>
        private static object GetAppliedPose(Bone bone)
        {
            if (_appliedPoseProp == null) return null;
            return _appliedPoseProp.GetValue(bone);
        }

        // ══════════════════════════════════════════
        //  公共 API - Skeleton 方法适配
        //  Public API - Skeleton method adapters
        // ══════════════════════════════════════════

        /// <summary>
        /// 重置到绑定姿态
        /// Reset to setup pose
        /// 3.8: skeleton.SetToSetupPose()
        /// 4.2+: skeleton.SetupPose()
        /// </summary>
        public static void SetToSetupPose(Skeleton skeleton)
        {
            if (DetectedVersion == SpineVersion.Spine38)
            {
                // 3.8: SetToSetupPose()
                if (_skeleton_SetToSetupPose != null)
                    _skeleton_SetToSetupPose.Invoke(skeleton, null);
                else if (_skeleton_SetupPose != null)
                    _skeleton_SetupPose.Invoke(skeleton, null);
            }
            else
            {
                // 4.2+ / 4.3+: SetupPose()
                if (_skeleton_SetupPose != null)
                    _skeleton_SetupPose.Invoke(skeleton, null);
            }
        }

        /// <summary>
        /// 更新世界变换
        /// Update world transform
        /// 3.8: skeleton.UpdateWorldTransform()  无参数
        /// 4.2+: skeleton.UpdateWorldTransform(Spine.Physics.Update)
        /// </summary>
        public static void UpdateWorldTransform(Skeleton skeleton)
        {
            if (DetectedVersion == SpineVersion.Spine38)
            {
                // 3.8: 无参数版
                if (_skeleton_UpdateWorldTransform_NoParam != null)
                    _skeleton_UpdateWorldTransform_NoParam.Invoke(skeleton, null);
            }
            else
            {
                // 4.2+ / 4.3+: 带 Physics 参数版
                if (_skeleton_UpdateWorldTransform_Physics != null && _physicsUpdateValue != null)
                    _skeleton_UpdateWorldTransform_Physics.Invoke(skeleton, new object[] { _physicsUpdateValue });
            }
        }

        // ══════════════════════════════════════════
        //  公共 API - Bone 世界空间属性
        //  Public API - Bone world-space properties
        // ══════════════════════════════════════════

        /// <summary>
        /// 获取骨骼世界X坐标
        /// Get bone world X position
        /// </summary>
        public static float GetWorldX(Bone bone)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return GetFloat(_bp_WorldX, GetAppliedPose(bone));
            return GetFloat(_bone_WorldX, bone);
        }

        /// <summary>
        /// 获取骨骼世界Y坐标
        /// Get bone world Y position
        /// </summary>
        public static float GetWorldY(Bone bone)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return GetFloat(_bp_WorldY, GetAppliedPose(bone));
            return GetFloat(_bone_WorldY, bone);
        }

        /// <summary>
        /// 获取骨骼世界旋转角度
        /// Get bone world rotation angle
        /// </summary>
        public static float GetWorldRotationX(Bone bone)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return GetFloat(_bp_WorldRotationX, GetAppliedPose(bone));
            return GetFloat(_bone_WorldRotationX, bone);
        }

        /// <summary>
        /// 获取骨骼世界X缩放
        /// Get bone world X scale
        /// </summary>
        public static float GetWorldScaleX(Bone bone)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return GetFloat(_bp_WorldScaleX, GetAppliedPose(bone));
            return GetFloat(_bone_WorldScaleX, bone);
        }

        /// <summary>
        /// 获取骨骼世界Y缩放
        /// Get bone world Y scale
        /// </summary>
        public static float GetWorldScaleY(Bone bone)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return GetFloat(_bp_WorldScaleY, GetAppliedPose(bone));
            return GetFloat(_bone_WorldScaleY, bone);
        }

        // ══════════════════════════════════════════
        //  公共 API - Bone 本地/Applied 属性
        //  Public API - Bone local/applied properties
        // ══════════════════════════════════════════

        /// <summary>
        /// 获取骨骼应用后的本地X坐标
        /// Get bone applied local X position
        /// </summary>
        public static float GetAppliedX(Bone bone)
        {
            switch (DetectedVersion)
            {
                case SpineVersion.Spine43:
                    return GetFloat(_bp_X, GetAppliedPose(bone));
                case SpineVersion.Spine42:
                    return GetFloat(_bone_X, bone);
                default: // 3.8
                    return GetFloat(_bone_AX, bone);
            }
        }

        /// <summary>
        /// 获取骨骼应用后的本地Y坐标
        /// Get bone applied local Y position
        /// </summary>
        public static float GetAppliedY(Bone bone)
        {
            switch (DetectedVersion)
            {
                case SpineVersion.Spine43:
                    return GetFloat(_bp_Y, GetAppliedPose(bone));
                case SpineVersion.Spine42:
                    return GetFloat(_bone_Y, bone);
                default: // 3.8
                    return GetFloat(_bone_AY, bone);
            }
        }

        /// <summary>
        /// 获取骨骼应用后的旋转角度
        /// Get bone applied rotation
        /// </summary>
        public static float GetAppliedRotation(Bone bone)
        {
            switch (DetectedVersion)
            {
                case SpineVersion.Spine43:
                    return GetFloat(_bp_Rotation, GetAppliedPose(bone));
                case SpineVersion.Spine42:
                    return GetFloat(_bone_Rotation, bone);
                default: // 3.8
                    return GetFloat(_bone_AppliedRotation, bone);
            }
        }

        /// <summary>
        /// 获取骨骼应用后的X缩放
        /// Get bone applied X scale
        /// </summary>
        public static float GetAppliedScaleX(Bone bone)
        {
            switch (DetectedVersion)
            {
                case SpineVersion.Spine43:
                    return GetFloat(_bp_ScaleX, GetAppliedPose(bone));
                case SpineVersion.Spine42:
                    return GetFloat(_bone_ScaleX, bone);
                default: // 3.8
                    return GetFloat(_bone_AScaleX, bone);
            }
        }

        /// <summary>
        /// 获取骨骼应用后的Y缩放
        /// Get bone applied Y scale
        /// </summary>
        public static float GetAppliedScaleY(Bone bone)
        {
            switch (DetectedVersion)
            {
                case SpineVersion.Spine43:
                    return GetFloat(_bp_ScaleY, GetAppliedPose(bone));
                case SpineVersion.Spine42:
                    return GetFloat(_bone_ScaleY, bone);
                default: // 3.8
                    return GetFloat(_bone_AScaleY, bone);
            }
        }

        // ══════════════════════════════════════════
        //  公共 API - 继承模式判断
        //  Public API - Inherit mode check
        // ══════════════════════════════════════════

        /// <summary>
        /// 判断骨骼是否继承旋转
        /// Check if bone inherits rotation
        /// </summary>
        public static bool InheritsRotation(Bone bone)
        {
            if (DetectedVersion == SpineVersion.Spine38)
            {
                // 3.8: bone.Data.TransformMode
                if (_data_TransformMode == null) return true;
                object tm = _data_TransformMode.GetValue(bone.Data);
                return Equals(tm, _tm_Normal) || Equals(tm, _tm_NoScale) || Equals(tm, _tm_NoScaleOrReflection);
            }
            else if (DetectedVersion == SpineVersion.Spine42)
            {
                // 4.2: bone.Inherit
                if (_bone_Inherit == null) return true;
                object inherit = _bone_Inherit.GetValue(bone);
                return Equals(inherit, _inherit_Normal) || Equals(inherit, _inherit_NoScale) || Equals(inherit, _inherit_NoScaleOrReflection);
            }
            else
            {
                // 4.3+: bone.AppliedPose.Inherit
                object appliedPose = GetAppliedPose(bone);
                if (appliedPose == null || _bp_Inherit == null) return true;
                object inherit = _bp_Inherit.GetValue(appliedPose);
                return Equals(inherit, _inherit_Normal) || Equals(inherit, _inherit_NoScale) || Equals(inherit, _inherit_NoScaleOrReflection);
            }
        }

        // ══════════════════════════════════════════
        //  公共 API - BoneData Setup Pose 属性
        //  Public API - BoneData setup pose properties
        // ══════════════════════════════════════════

        /// <summary>
        /// 获取 4.3+ 的 Setup Pose 对象
        /// Get 4.3+ setup pose object
        /// </summary>
        private static object GetSetupPose(BoneData data)
        {
            if (_data_GetSetupPose == null) return null;
            return _data_GetSetupPose.Invoke(data, null);
        }

        /// <summary>
        /// 获取BoneData的Setup Pose X坐标
        /// Get BoneData setup pose X position
        /// </summary>
        public static float GetSetupPoseX(BoneData data)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return GetFloat(_sp_X, GetSetupPose(data));
            return GetFloat(_data_X, data);
        }

        /// <summary>
        /// 获取BoneData的Setup Pose Y坐标
        /// Get BoneData setup pose Y position
        /// </summary>
        public static float GetSetupPoseY(BoneData data)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return GetFloat(_sp_Y, GetSetupPose(data));
            return GetFloat(_data_Y, data);
        }

        /// <summary>
        /// 获取BoneData的Setup Pose 旋转
        /// Get BoneData setup pose rotation
        /// </summary>
        public static float GetSetupPoseRotation(BoneData data)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return GetFloat(_sp_Rotation, GetSetupPose(data));
            return GetFloat(_data_Rotation, data);
        }

        /// <summary>
        /// 获取BoneData的Setup Pose X缩放
        /// Get BoneData setup pose X scale
        /// </summary>
        public static float GetSetupPoseScaleX(BoneData data)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return GetFloat(_sp_ScaleX, GetSetupPose(data));
            return GetFloat(_data_ScaleX, data);
        }

        /// <summary>
        /// 获取BoneData的Setup Pose Y缩放
        /// Get BoneData setup pose Y scale
        /// </summary>
        public static float GetSetupPoseScaleY(BoneData data)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return GetFloat(_sp_ScaleY, GetSetupPose(data));
            return GetFloat(_data_ScaleY, data);
        }

        // ══════════════════════════════════════════
        //  公共 API - Bone.Skeleton 属性
        //  Public API - Bone.Skeleton property
        // ══════════════════════════════════════════

        /// <summary>
        /// 获取骨骼所属的Skeleton
        /// Get the Skeleton that owns this bone
        /// 4.3+: 无此属性，返回 null（调用方需自行持有 Skeleton 引用）
        /// 4.3+: No such property, returns null (callers should hold their own Skeleton ref)
        /// </summary>
        public static Skeleton GetSkeleton(Bone bone)
        {
            if (DetectedVersion == SpineVersion.Spine43)
                return null;

            if (_bone_Skeleton == null) return null;
            return _bone_Skeleton.GetValue(bone) as Skeleton;
        }
    }
}
#endif
