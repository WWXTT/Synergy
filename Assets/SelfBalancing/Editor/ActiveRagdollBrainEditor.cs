#if UNITY_EDITOR
namespace FrostPunchGames
{
    using UnityEngine;
    using UnityEditor;
    using System;
    using System.Reflection;
    using System.Text.RegularExpressions;

    [CustomEditor(typeof(ActiveRagdollBrain))]
    public class ActiveRagdollBrainEditor : Editor
    {
        private ActiveRagdollBrain brain;
        private int currentTab = 0;

        private string[] tabs = { "构建", "步态 IK", "肌肉", "平衡与手臂" };

        private SerializedObject profileObj;
        private SerializedObject ikSolverObj;
        private SerializedObject syncerObj;
        private SerializedObject armBalanceObj;
        private SerializedObject stepManagerObj;
        private SerializedObject urgencyObj;

        private GUIStyle headerStyle;
        private GUIStyle tabStyle;
        private GUIStyle contentBoxStyle;
        private GUIStyle sectionBoxStyle;
        private GUIStyle sectionHeaderStyle;
        private GUIStyle sliderThumbStyle;
        private GUIStyle valueBoxStyle;

        private Texture2D inactiveTabTex, activeTabTex, contentBgTex, sectionBoxTex, customLogo;
        private Texture2D sliderThumbTex, valueBoxTex;

        private readonly Color accentGreen = new Color(0.12f, 0.88f, 0.22f);
        private readonly Color darkBg = new Color(0.14f, 0.14f, 0.14f);
        private readonly Color panelBg = new Color(0.18f, 0.18f, 0.18f);
        private readonly Color trackDark = new Color(0.08f, 0.08f, 0.08f);

        private bool isAdvancedMode;

        private void OnEnable()
        {
            brain = (ActiveRagdollBrain)target;
            isAdvancedMode = EditorPrefs.GetBool("ActiveRagdoll_AdvancedMode", false);

            if (brain.ActiveProfile != null) profileObj = new SerializedObject(brain.ActiveProfile);
            if (brain.hiddenIKSolver != null) ikSolverObj = new SerializedObject(brain.hiddenIKSolver);
            if (brain.hiddenSyncer != null) syncerObj = new SerializedObject(brain.hiddenSyncer);
            if (brain.hiddenArmBalance != null) armBalanceObj = new SerializedObject(brain.hiddenArmBalance);
            if (brain.hiddenStepManager != null) stepManagerObj = new SerializedObject(brain.hiddenStepManager);
            if (brain.hiddenUrgency != null) urgencyObj = new SerializedObject(brain.hiddenUrgency);
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool("ActiveRagdoll_AdvancedMode", isAdvancedMode);
        }

        private string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            string cleaned = input.Replace("-", "").Replace("=", "");
            cleaned = Regex.Replace(cleaned, @"\s*\(.*?\)", "");
            return cleaned.Trim();
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();

            Rect bgRect = new Rect(-20, -20, EditorGUIUtility.currentViewWidth + 40, 5000);
            EditorGUI.DrawRect(bgRect, darkBg);

            GUILayout.Space(-15);

            float viewWidth = EditorGUIUtility.currentViewWidth;
            float targetHeight = 110f;

            if (customLogo != null && customLogo.width > 0)
            {
                float aspect = (float)customLogo.height / customLogo.width;
                targetHeight = viewWidth * aspect;
            }
            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabs.Length; i++)
            {
                if (GUILayout.Toggle(currentTab == i, CleanText(tabs[i]), tabStyle, GUILayout.Height(34)))
                    currentTab = i;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(contentBoxStyle);
            EditorGUILayout.Space(5);

            if (isAdvancedMode) DrawAdvancedModeContent();
            else DrawCuratedDashboardContent();

            EditorGUILayout.Space(5);
            GUILayout.EndVertical();

            EditorGUILayout.Space(15);
            DrawFooterTools();
        }

        private void DrawCuratedDashboardContent()
        {
            if (currentTab == 0) { DrawBuilderTab(); return; }

            if (profileObj == null) { ShowMissingProfileWarning(); return; }

            // balanceUrgency 统一仲裁器：在除「构建」外的三个模块顶部都显示，作为唯一的
            // 动画↔物理表现控制（手动 / 自动两种模式）。
            DrawBalanceUrgencyBox();

            profileObj.Update();
            EditorGUI.BeginChangeCheck();

            switch (currentTab)
            {
                case 1:
                    BeginSectionBox("步态与迈步");
                    DrawProp(profileObj, "MasterIKBlend", "总体 IK 混合");
                    DrawProp(profileObj, "StepHeight", "抬脚离地高度");
                    DrawProp(profileObj, "StepPrediction", "步幅长度倍率");
                    DrawProp(profileObj, "RunSpeedThreshold", "奔跑步频速度");
                    EndSectionBox();

                    BeginSectionBox("髋部平衡");
                    DrawProp(profileObj, "ZMPWeightShiftWalking", "行走重心偏移");
                    DrawProp(profileObj, "ZMPWeightShiftIdle", "站立重心偏移");
                    DrawProp(profileObj, "ContrappostoStrength", "对立姿态强度");
                    DrawProp(profileObj, "CounterBalanceTilt", "躯干反向平衡");
                    DrawProp(profileObj, "ForwardLeanMultiplier", "速度前倾幅度");
                    DrawProp(profileObj, "StrafeLeanMultiplier", "横移侧倾幅度");
                    DrawProp(profileObj, "HipsSpringStiffness", "骨盆弹性刚度");
                    DrawProp(profileObj, "HipsSpringDamping", "骨盆阻尼");
                    EndSectionBox();

                    BeginSectionBox("LIPM 平衡反馈");
                    DrawProp(profileObj, "UseBalanceFeedback", "启用捕获点");
                    DrawProp(profileObj, "CPFullAuthorityMargin", "完全接管裕度");
                    DrawProp(profileObj, "CPStepTriggerMargin", "纠正迈步裕度");
                    DrawProp(profileObj, "CPFootBiasStrength", "落脚偏置强度");
                    DrawProp(profileObj, "CPHipLeanGain", "踝部前倾增益");
                    DrawProp(profileObj, "CPHipShiftGain", "髋部偏移增益");
                    // CPStepUrgencyGain（迈步紧迫增益）由 balanceUrgency 间接控制，隐藏不暴露（高级模式仍可见）。
                    DrawProp(profileObj, "ShowBalanceDebug", "显示调试叠加层");
                    EndSectionBox();
                    break;

                case 2:
                    BeginSectionBox("总体刚性");
                    DrawProp(profileObj, "TotalWeight", "总质量　重建后生效");
                    DrawProp(profileObj, "MuscleSpring", "关节弹簧张力");
                    DrawProp(profileObj, "MuscleDamper", "关节阻尼");
                    EndSectionBox();

                    BeginSectionBox("肌肉模拟");
                    DrawProp(profileObj, "MuscleAnchorWeight", "全局肌肉张力");
                    // MuscleDriveWeight（关节伺服强度=肌肉总权重）现由 balanceUrgency 间接调制，隐藏不暴露（高级模式仍可见）。
                    DrawProp(profileObj, "PoseTrackingSpeed", "姿态平滑速度");
                    DrawProp(profileObj, "ServoMaxForce", "最大电机扭矩");
                    EndSectionBox();
                    break;

                case 3:
                    BeginSectionBox("平衡紧迫度");
                    DrawProp(profileObj, "MaxExpectedAccel", "饱和加速度");
                    DrawProp(profileObj, "UrgencyLongFilter", "平静时滤波");
                    DrawProp(profileObj, "UrgencyShortFilter", "紧急时滤波");
                    DrawProp(profileObj, "UrgencyDivergenceWeight", "发散权重");
                    EndSectionBox();

                    BeginSectionBox("手臂平衡");
                    DrawProp(profileObj, "CPArmBalanceGain", "手臂平衡增益");
                    // ArmBalanceUrgencyLo/Hi（手臂的 urgency 响应窗口）由 balanceUrgency 间接控制，隐藏不暴露（高级模式仍可见）。
                    DrawProp(profileObj, "ArmBalanceMaxHandOffset", "最大手部位移");
                    DrawProp(profileObj, "ArmBalanceOffsetSmoothTime", "偏移平滑");
                    EndSectionBox();
                    break;
            }

            bool changed = EditorGUI.EndChangeCheck();
            profileObj.ApplyModifiedProperties();
            if (changed && Application.isPlaying && brain.ActiveProfile != null)
                brain.ApplyProfile(brain.ActiveProfile);
        }

        private void DrawBalanceUrgencyBox()
        {
            BeginSectionBox("balanceUrgency");

            if (urgencyObj == null)
            {
                EditorGUILayout.HelpBox("请先生成骨架，再控制 balanceUrgency。", MessageType.Info);
                EndSectionBox();
                return;
            }

            urgencyObj.Update();

            // 两种模式：手动设置（钉死下面的值）/ 自动（由质心加速度偏差计算）。
            DrawProp(urgencyObj, "overrideUrgency", "手动设置");

            SerializedProperty ov = urgencyObj.FindProperty("overrideUrgency");
            bool isOverriding = ov != null && ov.boolValue;

            EditorGUI.BeginDisabledGroup(!isOverriding);
            DrawProp(urgencyObj, "overrideValue", "手动数值");
            EditorGUI.EndDisabledGroup();

            urgencyObj.ApplyModifiedProperties();

            // Live readout of the actual urgency the arbiter is emitting (auto or overridden).
            if (Application.isPlaying && brain.hiddenUrgency != null)
            {
                float live = brain.hiddenUrgency.Urgency;
                Rect r = EditorGUILayout.GetControlRect(false, 18);
                Rect labelRect = new Rect(r.x, r.y, EditorGUIUtility.labelWidth, r.height);
                EditorGUI.LabelField(labelRect, isOverriding ? "实时值　手动" : "实时值　自动");
                Rect barRect = new Rect(r.x + EditorGUIUtility.labelWidth, r.y + 7, r.width - EditorGUIUtility.labelWidth, 4);
                EditorGUI.DrawRect(barRect, trackDark);
                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(live), 4), accentGreen);
                Rect valRect = new Rect(barRect.x + barRect.width - 45, r.y, 45, r.height);
                EditorGUI.LabelField(valRect, live.ToString("F2"), valueBoxStyle);
                Repaint();
            }
            else
            {
                EditorGUILayout.HelpBox(isOverriding
                    ? "手动模式：三大模块——步态IK（步频/落脚＋髋策略）、肌肉（关节驱动总权重）、手臂（平衡偏移/武器摆动/手部支撑/质心混合/MagicBlend）——都将按设定值表现。进入运行模式可观察动作。"
                    : "自动模式：balanceUrgency 由质心加速度偏差自动计算。进入运行模式可查看实时数值。",
                    MessageType.None);
            }

            EndSectionBox();
        }

        private void DrawCustomSlider(SerializedProperty prop, RangeAttribute range, string label)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, prop.tooltip), GUILayout.Width(EditorGUIUtility.labelWidth));

            Rect layoutRect = GUILayoutUtility.GetRect(50, 20, GUILayout.ExpandWidth(true));
            float thumbH = sliderThumbStyle.fixedHeight;
            Rect sliderRect = new Rect(layoutRect.x, layoutRect.y + (layoutRect.height - thumbH) * 0.5f, layoutRect.width, thumbH);

            float val = prop.floatValue;
            float min = range.min;
            float max = range.max;
            float fillPct = Mathf.Clamp01((val - min) / (max - min));

            float trackY = sliderRect.y + (sliderRect.height - 4) * 0.5f;
            Rect trackRect = new Rect(sliderRect.x, trackY, sliderRect.width, 4);
            EditorGUI.DrawRect(trackRect, trackDark);

            Rect fillRect = new Rect(sliderRect.x, trackY, sliderRect.width * fillPct, 4);
            EditorGUI.DrawRect(fillRect, accentGreen);

            prop.floatValue = GUI.HorizontalSlider(sliderRect, val, min, max, GUIStyle.none, sliderThumbStyle);

            GUILayout.Space(8);

            string valString = prop.floatValue.ToString("F2");
            GUI.Box(GUILayoutUtility.GetRect(45, 20), valString, valueBoxStyle);

            GUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private FieldInfo GetFieldInfo(SerializedProperty property)
        {
            Type currentType = property.serializedObject.targetObject.GetType();
            FieldInfo field = null;
            string[] path = property.propertyPath.Split('.');

            for (int i = 0; i < path.Length; i++)
            {
                string part = path[i];

                if (part == "Array" && i + 1 < path.Length && path[i + 1].StartsWith("data["))
                {
                    if (currentType.IsArray)
                        currentType = currentType.GetElementType();
                    else if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
                        currentType = currentType.GetGenericArguments()[0];

                    i++;
                    continue;
                }

                field = GetFieldFromHierarchy(currentType, part);
                if (field == null) return null;
                currentType = field.FieldType;
            }
            return field;
        }

        private FieldInfo GetFieldFromHierarchy(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f;
                type = type.BaseType;
            }
            return null;
        }

        private RangeAttribute GetRangeAttribute(SerializedProperty prop)
        {
            FieldInfo field = GetFieldInfo(prop);
            if (field != null)
            {
                object[] attrs = field.GetCustomAttributes(typeof(RangeAttribute), true);
                if (attrs.Length > 0) return (RangeAttribute)attrs[0];
            }
            return null;
        }

        private void DrawPropertyWithCustomSliders(SerializedProperty prop)
        {
            RangeAttribute range = GetRangeAttribute(prop);

            if (range != null && prop.propertyType == SerializedPropertyType.Float)
            {
                DrawCustomSlider(prop, range, CleanText(prop.displayName));
                return;
            }

            EditorGUILayout.PropertyField(prop, new GUIContent(CleanText(prop.displayName), prop.tooltip), true);
        }

        private void DrawProp(SerializedObject obj, string path, string customLabel = null)
        {
            SerializedProperty prop = obj.FindProperty(path);
            if (prop != null)
            {
                string finalLabel = CleanText(customLabel ?? prop.displayName);
                RangeAttribute range = GetRangeAttribute(prop);

                if (range != null && prop.propertyType == SerializedPropertyType.Float)
                    DrawCustomSlider(prop, range, finalLabel);
                else
                    EditorGUILayout.PropertyField(prop, new GUIContent(finalLabel, prop.tooltip), true);
            }
        }

        private void BeginSectionBox(string title)
        {
            GUILayout.BeginVertical(sectionBoxStyle);
            EditorGUILayout.LabelField(CleanText(title), sectionHeaderStyle);
            DrawTightDivider();
        }

        private void EndSectionBox()
        {
            GUILayout.EndVertical();
            EditorGUILayout.Space(12);
        }

        private void ShowMissingWarning() => EditorGUILayout.HelpBox("请先生成骨架，再查看调节参数。", MessageType.Info);

        private void ShowMissingProfileWarning() => EditorGUILayout.HelpBox("请在「构建」页签中指定一个调节配置，才能编辑这些参数。", MessageType.Info);

        private void DrawAdvancedModeContent()
        {
            switch (currentTab)
            {
                case 0: DrawBuilderTab(); break;
                case 1:
                    DrawRawScriptProperties(ikSolverObj, "IK 引擎原始参数");
                    DrawRawScriptProperties(stepManagerObj, "迈步管理器原始参数");
                    break;
                case 2: DrawRawScriptProperties(syncerObj, "物理同步原始参数"); break;
                case 3:
                    DrawRawScriptProperties(urgencyObj, "平衡紧迫度原始参数");
                    DrawRawScriptProperties(armBalanceObj, "手臂平衡原始参数");
                    break;
            }
        }

        private void DrawRawScriptProperties(SerializedObject serializedObj, string defaultHeader)
        {
            if (serializedObj == null) { ShowMissingWarning(); return; }

            serializedObj.Update();
            SerializedProperty prop = serializedObj.GetIterator();
            bool enterChildren = true, isInsideBox = false;

            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script") continue;

                FieldInfo field = GetFieldInfo(prop);
                if (field != null)
                {
                    object[] headers = field.GetCustomAttributes(typeof(HeaderAttribute), true);
                    if (headers.Length > 0)
                    {
                        if (isInsideBox) { GUILayout.EndVertical(); EditorGUILayout.Space(12); }
                        isInsideBox = true;
                        GUILayout.BeginVertical(sectionBoxStyle);
                        EditorGUILayout.LabelField(CleanText(((HeaderAttribute)headers[0]).header), sectionHeaderStyle);
                        DrawTightDivider();
                    }
                }
                if (!isInsideBox) { isInsideBox = true; GUILayout.BeginVertical(sectionBoxStyle); EditorGUILayout.LabelField(CleanText(defaultHeader), sectionHeaderStyle); DrawTightDivider(); }

                DrawPropertyWithCustomSliders(prop);
            }
            if (isInsideBox) GUILayout.EndVertical();
            serializedObj.ApplyModifiedProperties();
        }

        private void DrawBuilderTab()
        {
            BeginSectionBox("构建配置");

            SerializedProperty profileProp = serializedObject.FindProperty("ActiveProfile");
            EditorGUILayout.PropertyField(profileProp, new GUIContent(CleanText("调节配置"), profileProp.tooltip));

            SerializedProperty groundProp = serializedObject.FindProperty("GroundLayer");
            EditorGUILayout.PropertyField(groundProp, new GUIContent(CleanText("地面检测层"), groundProp.tooltip));

            SerializedProperty impactProp = serializedObject.FindProperty("ImpactLayers");
            EditorGUILayout.PropertyField(impactProp, new GUIContent(CleanText("撞击层"), impactProp.tooltip));

            SerializedProperty layerProp = serializedObject.FindProperty("RagdollLayerIndex");
            layerProp.intValue = EditorGUILayout.LayerField(new GUIContent("布偶物理层", layerProp.tooltip), layerProp.intValue);

            serializedObject.ApplyModifiedProperties();
            EndSectionBox();

            EditorGUILayout.Space(10);

            bool isGenerated = brain.PhysicalRig != null && brain.GhostRig != null;

            EditorGUI.BeginDisabledGroup(isGenerated);
            GUI.backgroundColor = isGenerated ? Color.gray : accentGreen;
            string btnText = isGenerated ? "主动布偶已生成" : "生成完整主动布偶";

            if (GUILayout.Button(btnText, GUILayout.Height(45)))
            {
                Undo.RecordObject(brain, "Generate Ragdoll");
                brain.SetupEverything();
                OnEnable();
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(5);

            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);

            GUI.backgroundColor = originalColor;

            if (profileObj != null)
            {
                EditorGUILayout.Space(10);
                profileObj.Update();
                EditorGUI.BeginChangeCheck();

                BeginSectionBox("物理 LOD");
                DrawProp(profileObj, "LODEnable", "启用距离 LOD");
                DrawProp(profileObj, "LOD1Distance", "LOD 1 距离");
                DrawProp(profileObj, "LOD2Distance", "LOD 2 距离");
                DrawProp(profileObj, "LOD3Distance", "LOD 3 距离");
                EndSectionBox();

                bool changed = EditorGUI.EndChangeCheck();
                profileObj.ApplyModifiedProperties();
                if (changed && Application.isPlaying && brain.ActiveProfile != null)
                    brain.ApplyProfile(brain.ActiveProfile);
            }
        }

        private void DrawFooterTools()
        {
            GUILayout.BeginVertical(sectionBoxStyle);
            EditorGUI.BeginChangeCheck();
            isAdvancedMode = EditorGUILayout.ToggleLeft(" 显示高级引擎参数", isAdvancedMode, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool("ActiveRagdoll_AdvancedMode", isAdvancedMode);
            DrawTightDivider();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("显示脚本", EditorStyles.miniButtonLeft)) ToggleScriptsVisibility(HideFlags.None);
            if (GUILayout.Button("隐藏脚本", EditorStyles.miniButtonRight)) ToggleScriptsVisibility(HideFlags.HideInInspector);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawTightDivider()
        {
            EditorGUILayout.Space(4);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.1f, 0.1f, 0.11f, 1f));
            EditorGUILayout.Space(8);
        }

        private void ToggleScriptsVisibility(HideFlags flag)
        {
            ApplyHideFlags(brain.gameObject, flag);            // masterRoot — 隐藏组件大多挂在这里（LOD/Perimeter/Urgency/Debug 等）
            if (brain.GhostRig != null) ApplyHideFlags(brain.GhostRig, flag);
            if (brain.PhysicalRig != null) ApplyHideFlags(brain.PhysicalRig, flag);
            if (brain.ShadowMimic != null) ApplyHideFlags(brain.ShadowMimic, flag);

            // 改完 hideFlags 后必须重画 Inspector，否则要手动重选对象才能看到效果。
            EditorApplication.RepaintHierarchyWindow();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            Repaint();
        }

        private void ApplyHideFlags(GameObject go, HideFlags flag)
        {
            foreach (var c in go.GetComponents<MonoBehaviour>())
            {
                if (c == null || c is ActiveRagdollBrain) continue; // 不要把 Brain 自己藏掉
                c.hideFlags = flag;
                EditorUtility.SetDirty(c);
            }
        }

        private void InitializeStyles()
        {
            Color sectionBorder = new Color(Mathf.Max(0, panelBg.r - 0.04f), Mathf.Max(0, panelBg.g - 0.04f), Mathf.Max(0, panelBg.b - 0.04f));
            Color valueBoxBg = new Color(darkBg.r + 0.05f, darkBg.g + 0.05f, darkBg.b + 0.05f);

            inactiveTabTex = MakeBorderTex(16, 16, darkBg, trackDark, 1);
            activeTabTex = MakeFeatheredTabTex(64, 64, darkBg, accentGreen);
            contentBgTex = MakeBorderTex(16, 16, darkBg, darkBg, 0);
            sectionBoxTex = MakeBorderTex(16, 16, panelBg, sectionBorder, 1);
            sliderThumbTex = MakeTex(10, 18, Color.white);
            valueBoxTex = MakeTex(16, 16, valueBoxBg);

            headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
            sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, normal = { textColor = accentGreen } };
            contentBoxStyle = new GUIStyle(GUI.skin.box) { normal = { background = contentBgTex }, padding = new RectOffset(12, 12, 12, 12), margin = new RectOffset(0, 0, 0, 0) };
            sectionBoxStyle = new GUIStyle(GUI.skin.box) { normal = { background = sectionBoxTex }, padding = new RectOffset(15, 15, 15, 15), margin = new RectOffset(0, 0, 0, 0) };

            tabStyle = new GUIStyle(GUIStyle.none)
            {
                normal = { background = inactiveTabTex, textColor = new Color(0.55f, 0.55f, 0.55f) },
                hover = { background = inactiveTabTex, textColor = new Color(0.55f, 0.55f, 0.55f) },
                active = { background = inactiveTabTex, textColor = new Color(0.55f, 0.55f, 0.55f) },
                focused = { background = inactiveTabTex, textColor = new Color(0.55f, 0.55f, 0.55f) },

                onNormal = { background = activeTabTex, textColor = accentGreen },
                onHover = { background = activeTabTex, textColor = accentGreen },
                onActive = { background = activeTabTex, textColor = accentGreen },
                onFocused = { background = activeTabTex, textColor = accentGreen },

                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(2, 2, 2, 2)
            };

            sliderThumbStyle = new GUIStyle(GUIStyle.none) { normal = { background = sliderThumbTex }, active = { background = sliderThumbTex }, hover = { background = sliderThumbTex }, fixedWidth = 10, fixedHeight = 18 };
            valueBoxStyle = new GUIStyle(GUI.skin.box) { normal = { background = valueBoxTex, textColor = Color.white }, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 11, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
        }

        private Texture2D MakeFeatheredTabTex(int w, int h, Color centerColor, Color edgeColor)
        {
            Color[] p = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = Mathf.Abs((x / (float)(w - 1)) * 2f - 1f);
                    float ny = Mathf.Abs((y / (float)(h - 1)) * 2f - 1f);

                    float dist = Mathf.Max(nx, ny);

                    float curvePower = 10f;
                    float t = Mathf.Pow(dist, curvePower);

                    p[y * w + x] = Color.Lerp(centerColor, edgeColor, t);
                }
            }
            Texture2D r = new Texture2D(w, h);
            r.SetPixels(p);
            r.Apply();
            return r;
        }

        private Texture2D MakeBorderTex(int w, int h, Color bg, Color border, int bWidth)
        {
            Color[] p = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) p[y * w + x] = (x < bWidth || x >= w - bWidth || y < bWidth || y >= h - bWidth) ? border : bg;
            Texture2D r = new Texture2D(w, h); r.SetPixels(p); r.Apply(); return r;
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] p = new Color[w * h];
            for (int i = 0; i < p.Length; i++) p[i] = col;
            Texture2D r = new Texture2D(w, h); r.SetPixels(p); r.Apply(); return r;
        }
    }
}
#endif