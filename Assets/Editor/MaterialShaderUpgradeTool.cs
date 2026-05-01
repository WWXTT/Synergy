#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MaterialShaderUpgradeTool : EditorWindow
{
    // 源 Shader 名称（通常为 Standard）
    private string sourceShaderName = "Standard";
    // 目标 Shader 名称，可在窗口中修改
    private string targetShaderName = "Universal Render Pipeline/Lit";

    // 你指定的属性映射关系
    // Key: 源材质上的属性名, Value: 目标 Shader 上的属性名
    private Dictionary<string, string> texturePropertyMapping = new Dictionary<string, string>
    {
        { "_MainTex", "_BaseMap" },           // 主纹理
    };

    [MenuItem("Tools/Upgrade Selected Materials")]
    static void Init()
    {
        MaterialShaderUpgradeTool window = (MaterialShaderUpgradeTool)EditorWindow.GetWindow(typeof(MaterialShaderUpgradeTool));
        window.titleContent = new GUIContent("材质升级");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("材质 Shader 批量升级工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        targetShaderName = EditorGUILayout.TextField("目标 Shader 名称", targetShaderName);

        EditorGUILayout.HelpBox(
            "在 Project 窗口中选中多个材质球，然后点击下方按钮。\n" +
            "只会处理 Shader 为 \"Standard\" 的材质。",
            MessageType.Info
        );

        EditorGUILayout.Space();
        GUI.enabled = !string.IsNullOrEmpty(targetShaderName);
        if (GUILayout.Button("升级选中的材质"))
        {
            UpgradeSelectedMaterials();
        }
        GUI.enabled = true;
    }

    void UpgradeSelectedMaterials()
    {
        // 获取当前在 Project 窗口中选择的所有对象
        Object[] selectedObjects = Selection.objects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("请先在 Project 窗口中选择至少一个材质球。");
            return;
        }

        Shader targetShader = Shader.Find(targetShaderName);
        if (targetShader == null)
        {
            Debug.LogError($"目标 Shader \"{targetShaderName}\" 未找到！请检查名称是否正确。");
            return;
        }

        int upgradedCount = 0;
        foreach (Object obj in selectedObjects)
        {
            Material material = obj as Material;
            if (material == null)
                continue;

            if (material.shader.name != sourceShaderName)
            {
                Debug.Log($"跳过 \"{material.name}\"：当前 Shader 不是 {sourceShaderName}。", material);
                continue;
            }

            // 记录 Undo，方便回退
            Undo.RecordObject(material, "Upgrade Material Shader");

            // --- 步骤1：在更换 Shader 前，保存符合条件的纹理引用 ---
            Dictionary<string, Texture> savedTextures = new Dictionary<string, Texture>();
            foreach (var mapping in texturePropertyMapping)
            {
                string sourceProperty = mapping.Key;
                // 检查源材质是否有这个属性，并且它是一个纹理属性
                if (material.HasProperty(sourceProperty))
                {
                    // 注意：HasProperty 不区分类型，这里特意用 GetTexture 来尝试获取纹理
                    // 如果原属性不是纹理类型（比如是颜色），GetTexture 会返回 null
                    Texture tex = material.GetTexture(sourceProperty);
                    if (tex != null)
                    {
                        savedTextures[sourceProperty] = tex;
                    }
                }
            }

            // --- 步骤2：执行 Shader 替换 ---
            material.shader = targetShader;

            // --- 步骤3：根据映射，将保存的纹理写到新 Shader 的属性上 ---
            foreach (var mapping in texturePropertyMapping)
            {
                string sourceProperty = mapping.Key;
                string targetProperty = mapping.Value;

                if (savedTextures.TryGetValue(sourceProperty, out Texture tex))
                {
                    // 确保目标 Shader 有对应的属性（因 Shader 已替换，这里可根据 HasProperty 判断）
                    if (material.HasProperty(targetProperty))
                    {
                        material.SetTexture(targetProperty, tex);
                    }
                }
            }

            EditorUtility.SetDirty(material);
            upgradedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"升级完成！共处理了 {upgradedCount} 个材质球。");
    }
}
#endif