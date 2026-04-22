using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 反向引用查找工具：选中一个 GameObject 后，查找场景中所有引用了它的其他 GameObject。
/// 菜单：Tools > Reference Finder
/// </summary>
public class ReferenceFinderWindow : EditorWindow
{
    private GameObject _selected;
    private List<ReferenceEntry> _results = new List<ReferenceEntry>();
    private Vector2 _scrollPos;
    private bool _autoRefresh = true;
    private bool _showComponents = true;

    private struct ReferenceEntry
    {
        public GameObject sourceObject;
        public string componentName;
        public string fieldName;
    }

    [MenuItem("Tools/Reference Finder")]
    private static void Open()
    {
        var window = GetWindow<ReferenceFinderWindow>("Reference Finder");
        window.minSize = new Vector2(320, 200);
    }

    private void OnSelectionChange()
    {
        if (_autoRefresh)
        {
            _selected = Selection.activeGameObject;
            ScanReferences();
            Repaint();
        }
    }

    private void OnGUI()
    {
        // --- 顶部：当前选中 ---
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Selected:", GUILayout.Width(60));
        if (_selected != null)
        {
            GUILayout.Label(_selected.name, EditorStyles.boldLabel);
        }
        else
        {
            GUILayout.Label("None", EditorStyles.label);
        }
        EditorGUILayout.EndHorizontal();

        // --- 选项 ---
        EditorGUILayout.BeginHorizontal();
        _autoRefresh = EditorGUILayout.ToggleLeft("Auto Refresh", _autoRefresh, GUILayout.Width(120));
        _showComponents = EditorGUILayout.ToggleLeft("Show Components", _showComponents, GUILayout.Width(130));
        if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(70)))
        {
            _selected = Selection.activeGameObject;
            ScanReferences();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // --- 结果列表 ---
        if (_selected == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject in the Hierarchy to find all references to it.", MessageType.Info);
            return;
        }

        if (_results.Count == 0)
        {
            EditorGUILayout.HelpBox("No references found in the current scene.", MessageType.Info);
            return;
        }

        GUILayout.Label($"Found {_results.Count} reference(s):", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        GameObject lastObj = null;
        foreach (var entry in _results)
        {
            if (entry.sourceObject != lastObj)
            {
                lastObj = entry.sourceObject;
                EditorGUILayout.Space(2);

                // 物体行：可点击跳转
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(entry.sourceObject.name, EditorStyles.linkLabel, GUILayout.ExpandWidth(true)))
                {
                    Selection.activeGameObject = entry.sourceObject;
                    EditorGUIUtility.PingObject(entry.sourceObject);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (_showComponents)
            {
                // 显示具体的组件和字段
                var rect = EditorGUILayout.GetControlRect(GUILayout.Height(EditorGUIUtility.singleLineHeight));
                rect.x += 16;
                rect.width -= 16;
                EditorGUI.LabelField(rect, $"{entry.componentName}  >  {entry.fieldName}", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // -----------------------------------------------------------------
    //  核心扫描逻辑
    // -----------------------------------------------------------------

    private void ScanReferences()
    {
        _results.Clear();
        if (_selected == null) return;

        var targetInstanceID = _selected.GetInstanceID();
        // 收集所有场景根物体（含 inactive）
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(go => go.hideFlags == HideFlags.None && !EditorUtility.IsPersistent(go))
            .ToList();

        foreach (var go in allObjects)
        {
            if (go == _selected) continue;
            ScanGameObject(go, targetInstanceID);
        }
    }

    private void ScanGameObject(GameObject go, int targetInstanceID)
    {
        var components = go.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null) continue; // missing script
            ScanComponent(go, comp, targetInstanceID);
        }
    }

    private void ScanComponent(GameObject go, Component comp, int targetInstanceID)
    {
        var so = new SerializedObject(comp);
        var prop = so.GetIterator();
        bool enterChildren = true;

        while (prop.Next(enterChildren))
        {
            enterChildren = true;

            // 只检查具体值属性，跳过数组大小等
            if (prop.propertyType == SerializedPropertyType.ObjectReference)
            {
                var objRef = prop.objectReferenceValue;
                if (objRef == null) continue;

                // 检查是否直接引用了目标 GameObject
                if (IsReferenceToTarget(objRef, targetInstanceID))
                {
                    AddResult(go, comp, prop);
                }
            }

            // 避免深入数组/列表的 size 属性
            if (prop.propertyType == SerializedPropertyType.ArraySize)
            {
                enterChildren = false;
            }
        }
    }

    private bool IsReferenceToTarget(Object objRef, int targetInstanceID)
    {
        // 直接是同一个 GameObject
        if (objRef is GameObject goRef && goRef.GetInstanceID() == targetInstanceID)
            return true;

        // 是挂在目标上的 Component → 也算引用了目标
        if (objRef is Component compRef && compRef.gameObject.GetInstanceID() == targetInstanceID)
            return true;

        return false;
    }

    private void AddResult(GameObject go, Component comp, SerializedProperty prop)
    {
        // 构建可读的字段路径
        var fieldPath = BuildReadablePath(prop);

        _results.Add(new ReferenceEntry
        {
            sourceObject = go,
            componentName = comp.GetType().Name,
            fieldName = fieldPath
        });
    }

    private static string BuildReadablePath(SerializedProperty prop)
    {
        var path = prop.propertyPath;
        // 将 Array.data[0] 替换为 [0] 更易读
        path = path.Replace("Array.data[", "[");
        return path;
    }
}
