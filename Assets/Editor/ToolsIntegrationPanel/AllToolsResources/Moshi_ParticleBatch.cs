using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class Moshi_ParticleBatch : EditorWindow
{
    private const string TOOL_NAME = "粒子批量修改";
    
    // 可调节参数
    private bool setDurationToLifetime = true;
    private bool disablePrewarm = true;
    private int maxParticles = 1;
    private ParticleSystemRingBufferMode ringBufferMode = ParticleSystemRingBufferMode.PauseUntilReplaced;
    private bool zeroEmissionRates = true;
    private bool modifyBursts = true;
    private bool modifyStartLifetime = false;
    private bool enableColorOverLifetime = false; // 新增：Color over Lifetime 开关

    // 参数值
    private float startLifetime = 5.0f;
    private short burstCount = 1;
    private Gradient colorGradient = new Gradient(); // 新增：颜色渐变

    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        GetWindow<Moshi_ParticleBatch>(TOOL_NAME);
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("粒子系统批量修改工具", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        // 基础参数
        setDurationToLifetime = EditorGUILayout.Toggle("Duration = Start Lifetime", setDurationToLifetime);
        disablePrewarm = EditorGUILayout.Toggle("关闭Prewarm", disablePrewarm);
        maxParticles = EditorGUILayout.IntField("最大粒子数", maxParticles);
        ringBufferMode = (ParticleSystemRingBufferMode)EditorGUILayout.EnumPopup("Ring Buffer模式", ringBufferMode);
        zeroEmissionRates = EditorGUILayout.Toggle("清空发射率", zeroEmissionRates);

        // Start Lifetime 参数
        modifyStartLifetime = EditorGUILayout.Toggle("修改Start Lifetime", modifyStartLifetime);
        if (modifyStartLifetime)
        {
            EditorGUI.indentLevel++;
            startLifetime = EditorGUILayout.FloatField("Start Lifetime值", startLifetime);
            EditorGUI.indentLevel--;
        }

        // 新增：Color over Lifetime 控制
        enableColorOverLifetime = EditorGUILayout.Toggle("启用Color over Lifetime", enableColorOverLifetime);
        if (enableColorOverLifetime)
        {
            EditorGUI.indentLevel++;
            colorGradient = EditorGUILayout.GradientField("颜色渐变", colorGradient);
            EditorGUI.indentLevel--;
        }

        // Burst参数
        modifyBursts = EditorGUILayout.Toggle("修改Bursts", modifyBursts);
        if (modifyBursts)
        {
            EditorGUI.indentLevel++;
            burstCount = (short)EditorGUILayout.IntSlider("Burst Count", burstCount, 1, 100);
            EditorGUI.indentLevel--;
        }

        GUILayout.Space(20);
        EditorGUILayout.HelpBox("在Hierarchy中选择包含粒子系统的GameObject，然后点击下方按钮应用修改。", MessageType.Info);

        if (GUILayout.Button("应用修改到选中对象", GUILayout.Height(40)))
        {
            ApplyModifications();
        }
    }

    private void ApplyModifications()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先在Hierarchy中选择至少一个GameObject", "确定");
            return;
        }

        int modifiedCount = 0;
        int totalSystems = 0;

        Undo.RecordObjects(selectedObjects, "修改粒子系统");

        foreach (GameObject obj in selectedObjects)
        {
            ParticleSystem[] particleSystems = obj.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in particleSystems)
            {
                totalSystems++;
                if (ModifyParticleSystem(ps))
                {
                    modifiedCount++;
                }
            }
        }

        EditorUtility.DisplayDialog("完成", $"成功修改了 {modifiedCount}/{totalSystems} 个粒子系统", "确定");
    }

    private bool ModifyParticleSystem(ParticleSystem ps)
    {
        bool modified = false;
        var main = ps.main;
        var emission = ps.emission;

        // 1. 修改Start Lifetime
        if (modifyStartLifetime && main.startLifetime.constant != startLifetime)
        {
            main.startLifetime = startLifetime;
            modified = true;
        }

        // 2. Duration = Start Lifetime
        if (setDurationToLifetime && main.duration != main.startLifetime.constant)
        {
            main.duration = main.startLifetime.constant;
            modified = true;
        }

        // 3. 关闭Prewarm
        if (disablePrewarm && main.prewarm != false)
        {
            main.prewarm = false;
            modified = true;
        }

        // 4. 最大粒子数
        if (main.maxParticles != maxParticles)
        {
            main.maxParticles = maxParticles;
            modified = true;
        }

        // 5. Ring Buffer模式
        if (main.ringBufferMode != ringBufferMode)
        {
            main.ringBufferMode = ringBufferMode;
            modified = true;
        }

        // 6. 发射率归零
        if (zeroEmissionRates)
        {
            if (emission.rateOverTime.constant != 0f)
            {
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
                modified = true;
            }

            if (emission.rateOverDistance.constant != 0f)
            {
                emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);
                modified = true;
            }
        }

        // 7. 新增：Color over Lifetime 控制
        var colorOverLifetime = ps.colorOverLifetime;
        if (enableColorOverLifetime)
        {
            if (!colorOverLifetime.enabled)
            {
                colorOverLifetime.enabled = true;
                modified = true;
            }

            // 创建颜色渐变
            if (colorGradient != null && colorGradient.colorKeys.Length > 0)
            {
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);
                modified = true;
            }
        }
        else
        {
            if (colorOverLifetime.enabled)
            {
                colorOverLifetime.enabled = false;
                modified = true;
            }
        }

        // 8. 修改Bursts
        if (modifyBursts)
        {
            ParticleSystem.Burst[] currentBursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(currentBursts);

            if (currentBursts.Length == 0)
            {
                // 没有Burst事件，添加一个新事件
                ParticleSystem.Burst newBurst = new ParticleSystem.Burst(0f, burstCount);
                emission.SetBursts(new ParticleSystem.Burst[] { newBurst });
                modified = true;
            }
            else
            {
                // 检查是否需要修改Count
                bool needsUpdate = false;
                for (int i = 0; i < currentBursts.Length; i++)
                {
                    if (currentBursts[i].count.constant != burstCount)
                    {
                        currentBursts[i].count = new ParticleSystem.MinMaxCurve(burstCount);
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                {
                    emission.SetBursts(currentBursts);
                    modified = true;
                }
            }
        }

        return modified;
    }
}