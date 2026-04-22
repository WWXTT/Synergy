using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System;

public class MissingReferenceChecker : EditorWindow
{
    private List<MissingRefInfo> missingRefs = new List<MissingRefInfo>();
    private Vector2 scrollPos;
    private readonly string[] ignoreAssemblies = { "Unity", "System", "Mono", "netstandard", "mscorlib" };

    [MenuItem("Tools/检查空引用工具")]
    public static void ShowWindow()
    {
        GetWindow<MissingReferenceChecker>("空引用检查器");
    }

    private void OnGUI()
    {
        GUILayout.Label("必填引用检查 (白名单模式)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("只检查标记了 [Required] 的字段或类", MessageType.Info);
        GUILayout.Space(10);

        if (GUILayout.Button("扫描当前场景缺失引用", GUILayout.Height(40)))
        {
            ScanScene();
        }

        GUILayout.Space(10);

        if (missingRefs.Count > 0)
        {
            GUILayout.Label($"发现 {missingRefs.Count} 个缺失引用:", EditorStyles.helpBox);
            GUILayout.Space(5);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var info in missingRefs)
            {
                DrawResultItem(info);
            }
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("所有必填引用均已赋值。", MessageType.Info);
        }
    }

    private void DrawResultItem(MissingRefInfo info)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                Selection.activeGameObject = info.gameObject;
                EditorGUIUtility.PingObject(info.gameObject);
            }
            GUILayout.Label($"{info.gameObject.name}", EditorStyles.boldLabel, GUILayout.Width(150));
            GUILayout.Label($"脚本: <{info.scriptName}>", GUILayout.Width(150));
            GUILayout.Label($"字段: {info.fieldName}", EditorStyles.linkLabel);
        }
    }

    private void ScanScene()
    {
        missingRefs.Clear();
        var allMonoBehaviours = FindObjectsOfType<MonoBehaviour>(true);

        foreach (var mb in allMonoBehaviours)
        {
            if (mb == null) continue;

            var type = mb.GetType();

            // 过滤Unity内置组件
            if (IsSystemType(type)) continue;

            // 检查 MonoBehaviour 自身的字段
            CheckObjectFields(mb, mb.gameObject, type.Name, "");
        }

        if (missingRefs.Count > 0)
            Debug.LogWarning($"[必填检查] 扫描完成，发现 {missingRefs.Count} 处缺失引用。");
        else
            Debug.Log("[必填检查] 扫描完成，必填项均已赋值。");
    }

    private void CheckObjectFields(object obj, GameObject rootGo, string scriptName, string pathPrefix)
    {
        if (obj == null) return;

        var type = obj.GetType();

        // 检查类上是否有 [Required] 标记
        bool isClassRequired = type.GetCustomAttribute<RequiredAttribute>() != null;

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in fields)
        {
            // 1. 必须是面板可见的字段
            bool isPublic = field.IsPublic;
            bool hasSerializeField = field.GetCustomAttribute<SerializeField>() != null;
            if (!isPublic && !hasSerializeField) continue;

            var fieldValue = field.GetValue(obj);
            var fieldType = field.FieldType;
            string fieldPath = string.IsNullOrEmpty(pathPrefix) ? field.Name : $"{pathPrefix}.{field.Name}";

            // 2. 检查是否是 Unity Object 类型
            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                bool isFieldRequired = field.GetCustomAttribute<RequiredAttribute>() != null;

                if (!isFieldRequired && !isClassRequired)
                    continue;

                if (fieldValue == null || (fieldValue as UnityEngine.Object) == null)
                {
                    missingRefs.Add(new MissingRefInfo
                    {
                        gameObject = rootGo,
                        scriptName = scriptName,
                        fieldName = fieldPath
                    });
                }
            }
            // 3. 处理 List<T> 类型（必须在普通类检查之前）
            else if (fieldValue is IList list)
            {
                int index = 0;
                foreach (var item in list)
                {
                    if (item != null)
                    {
                        var itemType = item.GetType();
                        // 如果列表元素是可序列化类，递归检查
                        bool isSerializableClass = itemType.IsSerializable ||
                            Attribute.IsDefined(itemType, typeof(System.SerializableAttribute));
                        if (itemType.IsClass && isSerializableClass)
                        {
                            CheckObjectFields(item, rootGo, scriptName, $"{fieldPath}[{index}]");
                        }
                    }
                    index++;
                }
            }
            // 4. 递归检查可序列化的嵌套类（非 Unity Object、非 List 类型）
            else if (fieldType.IsClass && !fieldType.IsArray && fieldType != typeof(string))
            {
                // 检查是否是标记了 [Serializable] 的类
                // 使用 IsSerializable 或检查 SerializableAttribute
                bool isSerializableClass = fieldType.IsSerializable ||
                    Attribute.IsDefined(fieldType, typeof(System.SerializableAttribute));
                if (isSerializableClass)
                {
                    CheckObjectFields(fieldValue, rootGo, scriptName, fieldPath);
                }
            }
        }
    }

    private bool IsSystemType(System.Type type)
    {
        string name = type.Assembly.FullName;
        foreach (var ignore in ignoreAssemblies)
        {
            if (name.StartsWith(ignore)) return true;
        }
        return false;
    }

    private class MissingRefInfo
    {
        public GameObject gameObject;
        public string scriptName;
        public string fieldName;
    }
}
