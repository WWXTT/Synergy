// TextureArrayGeneratorEditor.cs
// 使用 UnityTexture2DArrayImportPipeline 创建 Texture2DArray
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Oddworm.EditorFramework;

public class TextureArrayGeneratorEditor : EditorWindow
{
    private List<Texture2D> albedoTextures = new List<Texture2D>();
    private List<Texture2D> normalTextures = new List<Texture2D>();

    private int targetSize = 1024;
    private string outputFolder = "Assets/Performance/HexMap/_Materials/GeneratedTextures";

    private Vector2 scrollPos;

    [MenuItem("Tools/创建贴图数组")]
    public static void ShowWindow()
    {
        GetWindow<TextureArrayGeneratorEditor>("Texture Array Generator");
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Texture2DArray Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. 拖入贴图\n" +
            "2. 设置目标尺寸（所有贴图会统一缩放到此尺寸）\n" +
            "3. 点击 Generate，自动创建 .texture2darray 资产",
            MessageType.Info);

        EditorGUILayout.Space();

        // 设置
        GUILayout.Label("Settings", EditorStyles.boldLabel);
        targetSize = EditorGUILayout.IntSlider("Target Size", targetSize, 64, 4096);
        EditorGUILayout.BeginHorizontal();
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string folder = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
            if (!string.IsNullOrEmpty(folder))
            {
                outputFolder = "Assets" + folder.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Albedo 区域
        GUILayout.Label("Albedo Textures (Diffuse/Color):", EditorStyles.boldLabel);
        Rect albedoDropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(albedoDropArea, "Drag & Drop Albedo Textures Here");
        HandleDragDrop(albedoDropArea, albedoTextures);

        EditorGUILayout.Space(4);

        for (int i = 0; i < albedoTextures.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"[{i}]", GUILayout.Width(25));
            albedoTextures[i] = (Texture2D)EditorGUILayout.ObjectField(albedoTextures[i], typeof(Texture2D), false, GUILayout.Height(32));
            if (GUILayout.Button("X", GUILayout.Width(24), GUILayout.Height(32)))
            {
                albedoTextures.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        // Normal 区域
        GUILayout.Label("Normal Textures:", EditorStyles.boldLabel);
        Rect normalDropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(normalDropArea, "Drag & Drop Normal Textures Here");
        HandleDragDrop(normalDropArea, normalTextures);

        EditorGUILayout.Space(4);

        for (int i = 0; i < normalTextures.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"[{i}]", GUILayout.Width(25));
            normalTextures[i] = (Texture2D)EditorGUILayout.ObjectField(normalTextures[i], typeof(Texture2D), false, GUILayout.Height(32));
            if (GUILayout.Button("X", GUILayout.Width(24), GUILayout.Height(32)))
            {
                normalTextures.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        // 生成按钮
        GUI.enabled = albedoTextures.Count > 0 || normalTextures.Count > 0;
        if (GUILayout.Button("Generate Texture2DArrays", GUILayout.Height(30)))
        {
            Generate();
        }
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    private void HandleDragDrop(Rect dropArea, List<Texture2D> targetList)
    {
        Event evt = Event.current;
        if (evt.type == EventType.DragUpdated && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.Use();
        }
        else if (evt.type == EventType.DragPerform && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.AcceptDrag();
            foreach (Object obj in DragAndDrop.objectReferences)
            {
                if (obj is Texture2D tex && !targetList.Contains(tex))
                {
                    targetList.Add(tex);
                }
            }
            evt.Use();
        }
    }

    private void Generate()
    {
        // 确保输出目录存在
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        string resizedFolder = Path.Combine(outputFolder, "Resized");
        if (!Directory.Exists(resizedFolder))
        {
            Directory.CreateDirectory(resizedFolder);
        }

        AssetDatabase.Refresh();

        int totalCount = Mathf.Max(albedoTextures.Count, normalTextures.Count);
        if (totalCount == 0)
        {
            EditorUtility.DisplayDialog("Error", "No textures added.", "OK");
            return;
        }

        try
        {
            AssetDatabase.StartAssetEditing();

            // 生成 Albedo Texture2DArray
            if (albedoTextures.Count > 0)
            {
                string albedoArrayPath = Path.Combine(outputFolder, "AlbedoMaps.texture2darray");
                albedoArrayPath = albedoArrayPath.Replace("\\", "/");
                GenerateTextureArray(albedoTextures, albedoArrayPath, resizedFolder, "Albedo", false);
            }

            // 生成 Normal Texture2DArray
            if (normalTextures.Count > 0)
            {
                string normalArrayPath = Path.Combine(outputFolder, "NormalMaps.texture2darray");
                normalArrayPath = normalArrayPath.Replace("\\", "/");
                GenerateTextureArray(normalTextures, normalArrayPath, resizedFolder, "Normal", true);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("Success",
            $"Texture2DArrays generated in:\n{outputFolder}\n\nLayers: {totalCount}", "OK");
    }

    private void GenerateTextureArray(List<Texture2D> sources, string arrayAssetPath, string resizedFolder, string prefix, bool isNormalMap)
    {
        // 第一步：将所有贴图统一缩放到 targetSize x targetSize，保存为磁盘资产
        List<Texture2D> resizedAssets = new List<Texture2D>();

        for (int i = 0; i < sources.Count; i++)
        {
            Texture2D source = sources[i];
            if (source == null)
            {
                Debug.LogWarning($"[TextureArrayGenerator] {prefix}[{i}] is null, skipping");
                continue;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            string resizedPath = Path.Combine(resizedFolder, $"{prefix}_{i}_{sourceName}.png").Replace("\\", "/");

            // 确保源贴图可读
            MakeReadable(sourcePath);

            // 如果尺寸已经匹配，直接复制；否则缩放
            if (source.width == targetSize && source.height == targetSize)
            {
                // 尺寸匹配，直接复制源文件
                if (source.format != TextureFormat.RGBA32 && source.format != TextureFormat.RGB24)
                {
                    // 需要转换格式，通过缩放流程处理
                    resizedAssets.Add(ResizeAndSave(source, resizedPath, isNormalMap));
                }
                else
                {
                    // 格式和尺寸都匹配，复制像素数据保存为 PNG
                    resizedAssets.Add(SaveAsPNG(source, resizedPath));
                }
            }
            else
            {
                resizedAssets.Add(ResizeAndSave(source, resizedPath, isNormalMap));
            }

            EditorUtility.DisplayProgressBar("Generating", $"{prefix} [{i}/{sources.Count}]", (float)i / sources.Count);
        }

        EditorUtility.ClearProgressBar();

        if (resizedAssets.Count == 0)
        {
            Debug.LogError($"[TextureArrayGenerator] No valid {prefix} textures to process");
            return;
        }

        // 第二步：确保所有缩放后的贴图导入设置一致
        foreach (var tex in resizedAssets)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            ConfigureImportSettings(path, isNormalMap);
        }

        AssetDatabase.Refresh();

        // 重新加载，确保拿到最新的资产引用
        for (int i = 0; i < resizedAssets.Count; i++)
        {
            string path = AssetDatabase.GetAssetPath(resizedAssets[i]);
            resizedAssets[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // 第三步：创建 .texture2darray 文件并配置 importer
        CreateTexture2DArrayAsset(resizedAssets, arrayAssetPath);

        Debug.Log($"[TextureArrayGenerator] {prefix} array created: {arrayAssetPath}, layers: {resizedAssets.Count}");
    }

    private void MakeReadable(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    private void ConfigureImportSettings(string assetPath, bool isNormalMap)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !isNormalMap;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = true;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private Texture2D ResizeAndSave(Texture2D source, string outputPath, bool isNormalMap)
    {
        // 使用 RenderTexture 缩放
        RenderTexture rt = RenderTexture.GetTemporary(targetSize, targetSize, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;

        Graphics.SetRenderTarget(rt);
        GL.Clear(true, true, isNormalMap ? new Color(0.5f, 0.5f, 1f, 1f) : Color.black);

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, targetSize, 0, targetSize);

        // 保持宽高比居中
        float srcAspect = (float)source.width / source.height;
        float scale;
        Vector2 offset = Vector2.zero;
        if (srcAspect > 1f)
        {
            scale = (float)targetSize / source.width;
            offset.y = (targetSize - source.height * scale) * 0.5f;
        }
        else
        {
            scale = (float)targetSize / source.height;
            offset.x = (targetSize - source.width * scale) * 0.5f;
        }

        Graphics.DrawTexture(new Rect(offset.x, offset.y, source.width * scale, source.height * scale), source);
        GL.PopMatrix();

        RenderTexture.active = rt;
        Texture2D result = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, true);
        result.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0);
        result.Apply();

        // 保存为 PNG
        byte[] pngData = result.EncodeToPNG();
        File.WriteAllBytes(outputPath, pngData);

        RenderTexture.ReleaseTemporary(rt);
        RenderTexture.active = null;
        DestroyImmediate(result);

        AssetDatabase.ImportAsset(outputPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
    }

    private Texture2D SaveAsPNG(Texture2D source, string outputPath)
    {
        byte[] pngData = source.EncodeToPNG();
        File.WriteAllBytes(outputPath, pngData);

        AssetDatabase.ImportAsset(outputPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
    }

    private void CreateTexture2DArrayAsset(List<Texture2D> textures, string assetPath)
    {
        // 创建 .texture2darray 文件
        if (!File.Exists(assetPath))
        {
            string content = "Texture2DArray asset";
            File.WriteAllText(assetPath, content);
            AssetDatabase.ImportAsset(assetPath);
        }

        // 获取 importer 并配置
        var importer = AssetImporter.GetAtPath(assetPath) as Texture2DArrayImporter;
        if (importer == null)
        {
            Debug.LogError($"[TextureArrayGenerator] Failed to get Texture2DArrayImporter at {assetPath}");
            return;
        }

        importer.textures = textures.ToArray();
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 1;
        importer.isReadable = false;

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }
}
