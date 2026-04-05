using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CardCore.Attribute;

namespace CardCore.UI.UIToolkit
{
    /// <summary>
    /// 效果拼装编辑器面板
    /// 支持原子效果的筛选、拖拽组装、条件挂载和实时预览
    /// </summary>
    public class EffectEditorPanel : BaseUIToolkitPanel
    {
        #region 内部类型

        /// <summary>效果组装节点</summary>
        private class EffectAssemblyNode
        {
            public string NodeId;
            public AtomicEffectConfig Config;
            public int Value;
            public List<CardCore.ActivationCondition> Conditions = new List<CardCore.ActivationCondition>();
        }

        /// <summary>效果类型分类</summary>
        private enum EffectCategory
        {
            All, Damage, Healing, CardMove, Buff, Control, Protect, Special
        }

        #endregion

        #region 序列化字段

        [Header("模板引用")]
        [SerializeField] private VisualTreeAsset _effectListItemTemplate;
        [SerializeField] private VisualTreeAsset _conditionListItemTemplate;

        #endregion

        #region 状态

        private List<EffectAssemblyNode> _assemblyNodes = new List<EffectAssemblyNode>();
        private int _nodeIdCounter = 0;

        // 筛选状态
        private string _activeColorFilter = "all";
        private EffectCategory _activeCategory = EffectCategory.All;
        private string _effectSearchText = "";
        private string _conditionSearchText = "";

        // 所有可用效果配置（缓存）
        private List<AtomicEffectConfig> _allEffectConfigs = new List<AtomicEffectConfig>();

        // 拖拽状态
        private bool _isDragging = false;
        private VisualElement _dragGhost;
        private string _dragEffectEnumName;
        private CardCore.ConditionType _dragConditionType;

        #endregion

        #region UI元素引用

        // 左栏
        private ScrollView _effectLibraryList;
        private Label _effectListCount;
        private TextField _effectSearchInput;
        private readonly Dictionary<string, VisualElement> _colorFilterBtns = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, VisualElement> _typeFilterTags = new Dictionary<string, VisualElement>();

        // 中栏
        private ScrollView _assemblyContent;
        private VisualElement _assemblyEmptyZone;

        // 右栏
        private ScrollView _conditionLibraryList;
        private TextField _conditionSearchInput;
        private VisualElement _previewEmpty;
        private VisualElement _previewTree;

        #endregion

        #region 生命周期

        protected override void BindUIElements()
        {
            // 左栏 - 筛选按钮
            foreach (string color in new[] { "all", "red", "blue", "green", "white", "black", "gray" })
            {
                var btn = Q($"filter-color-{color}");
                if (btn != null) _colorFilterBtns[color] = btn;
            }

            foreach (string cat in new[] { "all", "damage", "healing", "cardmove", "buff", "control", "protect", "special" })
            {
                var tag = Q($"filter-type-{cat}");
                if (tag != null) _typeFilterTags[cat] = tag;
            }

            _effectSearchInput = Q<TextField>("effect-search-input");
            _effectLibraryList = Q<ScrollView>("effect-library-list");
            _effectListCount = Q<Label>("effect-list-count");

            // 中栏
            _assemblyContent = Q<ScrollView>("assembly-content");
            _assemblyEmptyZone = Q("assembly-empty-zone");

            // 右栏
            _conditionSearchInput = Q<TextField>("condition-search-input");
            _conditionLibraryList = Q<ScrollView>("condition-library-list");
            _previewEmpty = Q("preview-empty");
            _previewTree = Q("preview-tree");
        }

        protected override void RegisterEvents()
        {
            // 颜色筛选
            foreach (var kvp in _colorFilterBtns)
            {
                string color = kvp.Key;
                kvp.Value.RegisterCallback<ClickEvent>(evt => OnColorFilterChanged(color));
            }

            // 类型筛选
            foreach (var kvp in _typeFilterTags)
            {
                string cat = kvp.Key;
                kvp.Value.RegisterCallback<ClickEvent>(evt => OnCategoryFilterChanged(cat));
            }

            // 搜索
            BindTextField("effect-search-input", text =>
            {
                _effectSearchText = text ?? "";
                RefreshEffectLibrary();
            });

            BindTextField("condition-search-input", text =>
            {
                _conditionSearchText = text ?? "";
                RefreshConditionLibrary();
            });

            // 按钮
            BindButton("btn-add-node", OnAddEmptyNode);
            BindButton("btn-clear-assembly", OnClearAssembly);
            BindButton("btn-reset", OnReset);
            BindButton("btn-save", OnSave);
        }

        protected override void OnShow()
        {
            LoadAllEffectConfigs();
            RefreshEffectLibrary();
            RefreshConditionLibrary();
            UpdateAssemblyView();
            UpdatePreview();
        }

        #endregion

        #region 数据加载

        private void LoadAllEffectConfigs()
        {
            _allEffectConfigs.Clear();
            foreach (CardCore.AtomicEffectType effectType in Enum.GetValues(typeof(CardCore.AtomicEffectType)))
            {
                var config = AtomicEffectTable.GetByType(effectType);
                if (config != null)
                    _allEffectConfigs.Add(config);
            }
        }

        #endregion

        #region 筛选逻辑

        private void OnColorFilterChanged(string color)
        {
            _activeColorFilter = color;

            // 更新UI状态
            foreach (var kvp in _colorFilterBtns)
            {
                kvp.Value.EnableInClassList("active", kvp.Key == color);
            }

            RefreshEffectLibrary();
        }

        private void OnCategoryFilterChanged(string category)
        {
            _activeCategory = ParseCategory(category);

            foreach (var kvp in _typeFilterTags)
            {
                kvp.Value.EnableInClassList("active", kvp.Key == category);
            }

            RefreshEffectLibrary();
        }

        private void RefreshEffectLibrary()
        {
            if (_effectLibraryList == null) return;

            _effectLibraryList.Clear();

            var filtered = _allEffectConfigs.Where(MatchesFilter).ToList();

            foreach (var config in filtered)
            {
                var item = CreateEffectLibraryItem(config);
                _effectLibraryList.Add(item);
            }

            if (_effectListCount != null)
                _effectListCount.text = $"共 {filtered.Count} 个效果";
        }

        private bool MatchesFilter(AtomicEffectConfig config)
        {
            // 颜色筛选
            if (_activeColorFilter != "all")
            {
                var colorName = _activeColorFilter;
                // 首字母大写匹配 Tags 中的值
                colorName = char.ToUpper(colorName[0]) + colorName.Substring(1);
                var tags = config.GetTagList();
                if (!tags.Contains(colorName, StringComparer.OrdinalIgnoreCase))
                    return false;
            }

            // 类型分类筛选
            if (_activeCategory != EffectCategory.All)
            {
                if (!MatchesCategory(config, _activeCategory))
                    return false;
            }

            // 搜索文本
            if (!string.IsNullOrEmpty(_effectSearchText))
            {
                var search = _effectSearchText.ToLower();
                if (config.DisplayName != null && config.DisplayName.ToLower().Contains(search))
                    return true;
                if (config.EnumName != null && config.EnumName.ToLower().Contains(search))
                    return true;
                if (config.Description != null && config.Description.ToLower().Contains(search))
                    return true;
                return false;
            }

            return true;
        }

        private bool MatchesCategory(AtomicEffectConfig config, EffectCategory category)
        {
            var tags = config.GetTagList();
            var tagName = config.EnumName;

            return category switch
            {
                EffectCategory.Damage => tagName.Contains("Damage") || tags.Contains("Damage")
                    || tagName.Contains("Destroy") || tagName.Contains("LifeLoss") || tagName.Contains("Drain")
                    || tagName.Contains("Poisonous") || tagName.Contains("Fight"),
                EffectCategory.Healing => tagName.Contains("Heal") || tagName.Contains("Restore")
                    || tagName.Contains("RemoveDebuffs"),
                EffectCategory.CardMove => tagName.Contains("Draw") || tagName.Contains("Discard")
                    || tagName.Contains("Mill") || tagName.Contains("Return") || tagName.Contains("Put")
                    || tagName.Contains("Shuffle") || tagName.Contains("Search") || tagName.Contains("Move")
                    || tagName.Contains("Bounce") || tagName.Contains("Reveal") || tagName.Contains("Exile")
                    || tagName.Contains("Look"),
                EffectCategory.Buff => tagName.Contains("Modify") || tagName.Contains("Set")
                    || tagName.Contains("AddCounter") || tagName.Contains("DoubleCounter")
                    || tagName.Contains("AddKeyword") || tagName.Contains("AddCardType")
                    || tagName.Contains("Swap") || tagName.Contains("Grant") || tagName.Contains("Ramp")
                    || tagName.Contains("AddMana"),
                EffectCategory.Control => tagName.Contains("GainControl") || tagName.Contains("Steal")
                    || tagName.Contains("Counter") || tagName.Contains("Negate") || tagName.Contains("Prevent")
                    || tagName.Contains("Redirect") || tagName.Contains("Nullify") || tagName.Contains("Copy")
                    || tagName.Contains("Tap") || tagName.Contains("Untap") || tagName.Contains("Freeze")
                    || tagName.Contains("ModifyCost"),
                EffectCategory.Protect => tagName.Contains("Shield") || tagName.Contains("Immunity")
                    || tagName.Contains("Ward") || tagName.Contains("Armor") || tagName.Contains("Taunt")
                    || tagName.Contains("Stealth") || tagName.Contains("CannotBeTargeted")
                    || tagName.Contains("SpellShield") || tagName.Contains("Unaffected"),
                EffectCategory.Special => tagName.Contains("Token") || tagName.Contains("CopyCard")
                    || tagName.Contains("Transform") || tagName.Contains("Evolve") || tagName.Contains("Equip")
                    || tagName.Contains("Repeat") || tagName.Contains("Delayed") || tagName.Contains("Random")
                    || tagName.Contains("ModifyGameRule") || tagName.Contains("ExtraTurn")
                    || tagName.Contains("ChooseOne"),
                _ => true
            };
        }

        private EffectCategory ParseCategory(string s) => s switch
        {
            "damage" => EffectCategory.Damage,
            "healing" => EffectCategory.Healing,
            "cardmove" => EffectCategory.CardMove,
            "buff" => EffectCategory.Buff,
            "control" => EffectCategory.Control,
            "protect" => EffectCategory.Protect,
            "special" => EffectCategory.Special,
            _ => EffectCategory.All
        };

        #endregion

        #region 条件列表

        private void RefreshConditionLibrary()
        {
            if (_conditionLibraryList == null) return;

            _conditionLibraryList.Clear();

            foreach (CardCore.ConditionType condType in Enum.GetValues(typeof(CardCore.ConditionType)))
            {
                // 跳过逻辑组合类型
                if (condType == CardCore.ConditionType.And || condType == CardCore.ConditionType.Or
                    || condType == CardCore.ConditionType.Not || condType == CardCore.ConditionType.Custom)
                    continue;

                var name = condType.ToString();

                // 搜索过滤
                if (!string.IsNullOrEmpty(_conditionSearchText))
                {
                    if (!name.ToLower().Contains(_conditionSearchText.ToLower()))
                        continue;
                }

                var item = CreateConditionLibraryItem(condType, name);
                _conditionLibraryList.Add(item);
            }
        }

        #endregion

        #region 列表项创建

        private VisualElement CreateEffectLibraryItem(AtomicEffectConfig config)
        {
            VisualElement item;

            if (_effectListItemTemplate != null)
            {
                var tree = _effectListItemTemplate.CloneTree();
                item = tree.ElementAt(0);
            }
            else
            {
                // fallback: 简单结构
                item = new VisualElement();
                item.AddToClassList("effect-list-item");

                var icon = new VisualElement { name = "effect-icon" };
                icon.AddToClassList("effect-icon");
                item.Add(icon);

                var content = new VisualElement { name = "effect-content" };
                content.AddToClassList("effect-content");

                var nameLabel = new Label { name = "effect-name", text = config.DisplayName ?? config.EnumName };
                nameLabel.AddToClassList("effect-name");
                content.Add(nameLabel);

                var descLabel = new Label { name = "effect-brief", text = config.Description ?? "" };
                descLabel.AddToClassList("effect-brief");
                content.Add(descLabel);

                item.Add(content);

                var handle = new VisualElement { name = "drag-handle" };
                handle.AddToClassList("drag-handle");
                handle.Add(new Label { text = "⋮⋮" });
                item.Add(handle);
            }

            // 设置数据
            var nameEl = item.Q<Label>("effect-name");
            if (nameEl != null) nameEl.text = config.DisplayName ?? config.EnumName;

            var briefEl = item.Q<Label>("effect-brief");
            if (briefEl != null) briefEl.text = config.Description ?? "";

            // 设置图标颜色
            var iconEl = item.Q("effect-icon");
            if (iconEl != null)
            {
                var tags = config.GetTagList();
                var color = GetColorFromTags(tags);
                iconEl.style.backgroundColor = color;
            }

            // 隐藏编辑/删除按钮（库模式不需要）
            var btnEdit = item.Q<Button>("btn-edit");
            var btnDelete = item.Q<Button>("btn-delete");
            if (btnEdit != null) btnEdit.style.display = DisplayStyle.None;
            if (btnDelete != null) btnDelete.style.display = DisplayStyle.None;

            // 拖拽注册
            item.RegisterCallback<PointerDownEvent>(evt => OnEffectItemPointerDown(evt, config));
            item.RegisterCallback<PointerMoveEvent>(evt => OnEffectItemPointerMove(evt, config));
            item.RegisterCallback<PointerUpEvent>(OnPointerUp);

            // 双击添加
            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 2)
                    AddEffectToAssembly(config);
            });

            // userData 存储配置
            item.userData = config;

            return item;
        }

        private VisualElement CreateConditionLibraryItem(CardCore.ConditionType condType, string displayName)
        {
            VisualElement item;

            if (_conditionListItemTemplate != null)
            {
                var tree = _conditionListItemTemplate.CloneTree();
                item = tree.ElementAt(0);
            }
            else
            {
                item = new VisualElement();
                item.AddToClassList("condition-list-item");

                var icon = new VisualElement { name = "condition-icon" };
                icon.AddToClassList("condition-icon");
                item.Add(icon);

                var content = new VisualElement();
                content.AddToClassList("condition-content");

                var nameLabel = new Label { name = "condition-name", text = displayName };
                nameLabel.AddToClassList("condition-name");
                content.Add(nameLabel);

                item.Add(content);
            }

            var nameEl = item.Q<Label>("condition-name");
            if (nameEl != null) nameEl.text = displayName;

            // 隐藏库模式不需要的控件
            var negate = item.Q<Toggle>("negate-toggle");
            var btnEdit = item.Q<Button>("btn-edit");
            var btnDelete = item.Q<Button>("btn-delete");
            if (negate != null) negate.style.display = DisplayStyle.None;
            if (btnEdit != null) btnEdit.style.display = DisplayStyle.None;
            if (btnDelete != null) btnDelete.style.display = DisplayStyle.None;

            // 拖拽注册
            item.RegisterCallback<PointerDownEvent>(evt => OnConditionItemPointerDown(evt, condType));
            item.RegisterCallback<PointerMoveEvent>(evt => OnConditionItemPointerMove(evt, condType));
            item.RegisterCallback<PointerUpEvent>(OnPointerUp);

            item.userData = condType;

            return item;
        }

        private Color GetColorFromTags(List<string> tags)
        {
            if (tags.Contains("Red")) return new Color(0.91f, 0.30f, 0.24f);
            if (tags.Contains("Blue")) return new Color(0.20f, 0.60f, 0.86f);
            if (tags.Contains("Green")) return new Color(0.18f, 0.80f, 0.44f);
            if (tags.Contains("White")) return new Color(0.95f, 0.95f, 0.95f);
            if (tags.Contains("Black")) return new Color(0.17f, 0.17f, 0.17f);
            return new Color(0.55f, 0.55f, 0.55f); // Gray
        }

        #endregion

        #region 拖拽逻辑

        private Vector3 _pointerStartPos;

        private void OnEffectItemPointerDown(PointerDownEvent evt, AtomicEffectConfig config)
        {
            _pointerStartPos = evt.position;
            _dragEffectEnumName = config.EnumName;
            _dragConditionType = default;
        }

        private void OnEffectItemPointerMove(PointerMoveEvent evt, AtomicEffectConfig config)
        {
            if (_dragEffectEnumName != config.EnumName) return;

            var delta = evt.position - _pointerStartPos;
            if (!_isDragging && delta.magnitude > 10)
            {
                _isDragging = true;
                StartDragEffect(config);
            }

            if (_isDragging && _dragGhost != null)
            {
                _dragGhost.style.left = evt.position.x - 50;
                _dragGhost.style.top = evt.position.y - 15;
            }
        }

        private void OnConditionItemPointerDown(PointerDownEvent evt, CardCore.ConditionType condType)
        {
            _pointerStartPos = evt.position;
            _dragConditionType = condType;
            _dragEffectEnumName = null;
        }

        private void OnConditionItemPointerMove(PointerMoveEvent evt, CardCore.ConditionType condType)
        {
            if (_dragConditionType != condType) return;

            var delta = evt.position - _pointerStartPos;
            if (!_isDragging && delta.magnitude > 10)
            {
                _isDragging = true;
                StartDragCondition(condType);
            }

            if (_isDragging && _dragGhost != null)
            {
                _dragGhost.style.left = evt.position.x - 50;
                _dragGhost.style.top = evt.position.y - 15;
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging) return;

            EndDrag();
        }

        private void StartDragEffect(AtomicEffectConfig config)
        {
            CreateDragGhost(config.DisplayName ?? config.EnumName);
            // 高亮所有可放置区域
            HighlightDropZones(true);
        }

        private void StartDragCondition(CardCore.ConditionType condType)
        {
            CreateDragGhost(condType.ToString());
            HighlightDropZones(true);
        }

        private void CreateDragGhost(string text)
        {
            _dragGhost = new VisualElement();
            _dragGhost.AddToClassList("drag-ghost");
            _dragGhost.style.position = Position.Absolute;
            _dragGhost.style.width = 120;
            _dragGhost.style.height = 30;
            _dragGhost.style.backgroundColor = new Color(0.91f, 0.30f, 0.24f, 0.9f);
            _dragGhost.style.borderTopLeftRadius = 4;
            _dragGhost.style.borderTopRightRadius = 4;
            _dragGhost.style.borderBottomLeftRadius = 4;
            _dragGhost.style.borderBottomRightRadius = 4;
            _dragGhost.style.paddingLeft = 8;
            _dragGhost.style.paddingRight = 8;
            _dragGhost.style.alignContent = Align.Center;

            var label = new Label { text = text };
            label.style.color = Color.white;
            label.style.fontSize = 12;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _dragGhost.Add(label);

            _rootElement.Add(_dragGhost);
        }

        private void HighlightDropZones(bool highlight)
        {
            // 高亮组装区空拖放区
            if (_assemblyEmptyZone != null)
                _assemblyEmptyZone.EnableInClassList("drag-over", highlight);

            // 高亮所有节点的条件拖放区
            var dropZones = _assemblyContent?.Query<VisualElement>(className: "drop-zone").ToList();
            if (dropZones != null)
            {
                foreach (var zone in dropZones)
                    zone.EnableInClassList("drag-over", highlight);
            }
        }

        private void EndDrag()
        {
            _isDragging = false;

            if (_dragGhost != null)
            {
                _dragGhost.RemoveFromHierarchy();
                _dragGhost = null;
            }

            HighlightDropZones(false);

            // 检测放置目标
            if (!string.IsNullOrEmpty(_dragEffectEnumName))
            {
                var config = _allEffectConfigs.FirstOrDefault(c => c.EnumName == _dragEffectEnumName);
                if (config != null)
                {
                    AddEffectToAssembly(config);
                }
                _dragEffectEnumName = null;
            }
            else if (_dragConditionType != default)
            {
                // 添加条件到最后一个选中的节点
                if (_assemblyNodes.Count > 0)
                {
                    var lastNode = _assemblyNodes[_assemblyNodes.Count - 1];
                    var condition = new CardCore.ActivationCondition { Type = _dragConditionType };
                    lastNode.Conditions.Add(condition);
                    UpdateAssemblyView();
                    UpdatePreview();
                }
                _dragConditionType = default;
            }
        }

        #endregion

        #region 组装逻辑

        private void AddEffectToAssembly(AtomicEffectConfig config)
        {
            var node = new EffectAssemblyNode
            {
                NodeId = $"node-{_nodeIdCounter++}",
                Config = config,
                Value = (int)config.BaseCost
            };

            _assemblyNodes.Add(node);
            UpdateAssemblyView();
            UpdatePreview();
        }

        private void RemoveAssemblyNode(string nodeId)
        {
            _assemblyNodes.RemoveAll(n => n.NodeId == nodeId);
            UpdateAssemblyView();
            UpdatePreview();
        }

        private void AddEmptyNode()
        {
            var node = new EffectAssemblyNode
            {
                NodeId = $"node-{_nodeIdCounter++}",
                Config = null,
                Value = 0
            };

            _assemblyNodes.Add(node);
            UpdateAssemblyView();
        }

        private void OnAddEmptyNode()
        {
            AddEmptyNode();
        }

        private void OnClearAssembly()
        {
            _assemblyNodes.Clear();
            _nodeIdCounter = 0;
            UpdateAssemblyView();
            UpdatePreview();
        }

        private void OnReset()
        {
            OnClearAssembly();
            _activeColorFilter = "all";
            _activeCategory = EffectCategory.All;
            _effectSearchText = "";
            _conditionSearchText = "";

            if (_effectSearchInput != null) _effectSearchInput.value = "";
            if (_conditionSearchInput != null) _conditionSearchInput.value = "";

            foreach (var kvp in _colorFilterBtns)
                kvp.Value.EnableInClassList("active", kvp.Key == "all");

            foreach (var kvp in _typeFilterTags)
                kvp.Value.EnableInClassList("active", kvp.Key == "all");

            RefreshEffectLibrary();
            RefreshConditionLibrary();
        }

        private void OnSave()
        {
            if (_assemblyNodes.Count == 0)
            {
                Debug.LogWarning("[EffectEditorPanel] 没有效果可保存");
                return;
            }

            // TODO: 构建完整的 EffectDefinition 并保存
            Debug.Log($"[EffectEditorPanel] 保存效果，共 {_assemblyNodes.Count} 个节点");
        }

        #endregion

        #region 组装视图

        private void UpdateAssemblyView()
        {
            if (_assemblyContent == null) return;

            // 清除现有内容（保留滚动结构）
            _assemblyContent.Clear();

            if (_assemblyNodes.Count == 0)
            {
                // 显示空拖拽区
                var emptyZone = new VisualElement { name = "assembly-empty-zone" };
                emptyZone.AddToClassList("assembly-empty-zone");
                var emptyLabel = new Label("从左侧拖拽效果到此处开始组装");
                emptyLabel.AddToClassList("drop-zone-hint");
                emptyZone.Add(emptyLabel);
                _assemblyContent.Add(emptyZone);
                return;
            }

            for (int i = 0; i < _assemblyNodes.Count; i++)
            {
                var node = _assemblyNodes[i];

                // 节点间连接线
                if (i > 0)
                {
                    var connector = CreateChainConnector();
                    _assemblyContent.Add(connector);
                }

                // 效果节点
                var nodeEl = CreateAssemblyNodeElement(node, i);
                _assemblyContent.Add(nodeEl);
            }

            // 底部追加拖拽区
            var appendZone = new VisualElement { name = "append-drop-zone" };
            appendZone.AddToClassList("drop-zone");
            var hintLabel = new Label("拖拽效果追加到此处");
            hintLabel.AddToClassList("drop-zone-hint-sm");
            appendZone.Add(hintLabel);
            _assemblyContent.Add(appendZone);
        }

        private VisualElement CreateChainConnector()
        {
            var connector = new VisualElement();
            connector.AddToClassList("chain-connector");

            var line = new VisualElement();
            line.AddToClassList("chain-connector-line");
            connector.Add(line);

            var label = new Label("▼");
            label.AddToClassList("chain-connector-label");
            connector.Add(label);

            return connector;
        }

        private VisualElement CreateAssemblyNodeElement(EffectAssemblyNode node, int index)
        {
            var el = new VisualElement { name = node.NodeId };
            el.AddToClassList("effect-node");

            // --- Header ---
            var header = new VisualElement();
            header.AddToClassList("effect-node-header");

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var title = new Label
            {
                text = node.Config?.DisplayName ?? "(空节点)",
                name = "effect-node-title"
            };
            title.AddToClassList("effect-node-title");
            titleRow.Add(title);

            var idx = new Label { text = $"#{index + 1}", name = "effect-node-index" };
            idx.AddToClassList("effect-node-index");
            titleRow.Add(idx);

            header.Add(titleRow);

            var actions = new VisualElement { name = "effect-node-actions" };
            actions.AddToClassList("effect-node-actions");

            var btnRemove = new Button { text = "移除", name = "btn-remove-node" };
            btnRemove.AddToClassList("btn");
            btnRemove.AddToClassList("btn-sm");
            btnRemove.AddToClassList("btn-danger");
            btnRemove.clicked += () => RemoveAssemblyNode(node.NodeId);
            actions.Add(btnRemove);

            var btnUp = new Button { text = "▲", name = "btn-move-up" };
            btnUp.AddToClassList("btn");
            btnUp.AddToClassList("btn-sm");
            btnUp.AddToClassList("btn-secondary");
            btnUp.clicked += () => MoveNode(node.NodeId, -1);
            actions.Add(btnUp);

            var btnDown = new Button { text = "▼", name = "btn-move-down" };
            btnDown.AddToClassList("btn");
            btnDown.AddToClassList("btn-sm");
            btnDown.AddToClassList("btn-secondary");
            btnDown.clicked += () => MoveNode(node.NodeId, 1);
            actions.Add(btnDown);

            header.Add(actions);
            el.Add(header);

            // --- Body ---
            var body = new VisualElement();
            body.AddToClassList("effect-node-body");

            if (node.Config != null)
            {
                var desc = new Label { text = node.Config.Description ?? node.Config.EnumName };
                desc.style.color = new Color(0.55f, 0.55f, 0.69f);
                desc.style.fontSize = 12;
                body.Add(desc);
            }

            // --- Conditions Section ---
            if (node.Conditions.Count > 0 || true) // 始终显示条件区
            {
                var condSection = new VisualElement();
                condSection.AddToClassList("node-conditions-section");

                var condLabel = new Label { text = "发动条件" };
                condLabel.AddToClassList("node-conditions-label");
                condSection.Add(condLabel);

                // 已有条件
                for (int ci = 0; ci < node.Conditions.Count; ci++)
                {
                    var cond = node.Conditions[ci];
                    var condItem = CreateNodeConditionItem(node, ci, cond);
                    condSection.Add(condItem);
                }

                // 条件拖拽区
                var condDropZone = new VisualElement();
                condDropZone.AddToClassList("drop-zone");
                var condLabel2 = new Label("拖拽条件到此处");
                condLabel2.AddToClassList("drop-zone-hint-sm");
                condDropZone.Add(condLabel2);
                condSection.Add(condDropZone);

                body.Add(condSection);
            }

            el.Add(body);
            return el;
        }

        private VisualElement CreateNodeConditionItem(EffectAssemblyNode node, int index, CardCore.ActivationCondition cond)
        {
            var item = new VisualElement();
            item.AddToClassList("node-condition-item");

            var dot = new VisualElement();
            dot.style.width = 8;
            dot.style.height = 8;
            dot.style.borderTopLeftRadius = 4;
            dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = 4;
            dot.style.borderBottomRightRadius = 4;
            dot.style.backgroundColor = new Color(0.20f, 0.60f, 0.86f);
            dot.style.marginRight = 6;
            item.Add(dot);

            var name = new Label { text = cond.Type.ToString() };
            name.AddToClassList("node-condition-name");
            item.Add(name);

            var btnRemove = new Button { text = "✕" };
            btnRemove.AddToClassList("node-condition-remove");
            btnRemove.clicked += () =>
            {
                node.Conditions.RemoveAt(index);
                UpdateAssemblyView();
                UpdatePreview();
            };
            item.Add(btnRemove);

            return item;
        }

        private void MoveNode(string nodeId, int direction)
        {
            var idx = _assemblyNodes.FindIndex(n => n.NodeId == nodeId);
            if (idx < 0) return;

            var newIdx = idx + direction;
            if (newIdx < 0 || newIdx >= _assemblyNodes.Count) return;

            (_assemblyNodes[idx], _assemblyNodes[newIdx]) = (_assemblyNodes[newIdx], _assemblyNodes[idx]);
            UpdateAssemblyView();
            UpdatePreview();
        }

        #endregion

        #region 预览

        private void UpdatePreview()
        {
            if (_previewEmpty == null || _previewTree == null) return;

            if (_assemblyNodes.Count == 0)
            {
                _previewEmpty.style.display = DisplayStyle.Flex;
                _previewTree.style.display = DisplayStyle.None;
                return;
            }

            _previewEmpty.style.display = DisplayStyle.None;
            _previewTree.style.display = DisplayStyle.Flex;
            _previewTree.Clear();

            float totalCost = 0;
            int effectCount = 0;
            int conditionCount = 0;

            foreach (var node in _assemblyNodes)
            {
                if (node.Config == null) continue;

                var block = new VisualElement();
                block.AddToClassList("preview-effect-block");

                var name = new Label { text = node.Config.DisplayName ?? node.Config.EnumName };
                name.AddToClassList("preview-effect-name");
                block.Add(name);

                if (!string.IsNullOrEmpty(node.Config.Description))
                {
                    var desc = new Label { text = node.Config.Description };
                    desc.AddToClassList("preview-effect-desc");
                    block.Add(desc);
                }

                // 条件标签
                foreach (var cond in node.Conditions)
                {
                    var tag = new VisualElement();
                    tag.AddToClassList("preview-condition-tag");

                    var dot = new VisualElement();
                    dot.AddToClassList("preview-condition-dot");
                    tag.Add(dot);

                    var condText = new Label { text = cond.Type.ToString() };
                    condText.AddToClassList("preview-condition-text");
                    tag.Add(condText);

                    block.Add(tag);
                    conditionCount++;
                }

                _previewTree.Add(block);
                totalCost += node.Config.BaseCost * node.Config.CostMultiplier;
                effectCount++;
            }

            // 统计信息
            var stats = new VisualElement();
            stats.AddToClassList("preview-stats");

            AddStatsRow(stats, "效果数量", effectCount.ToString());
            AddStatsRow(stats, "条件数量", conditionCount.ToString());
            AddStatsRow(stats, "预估费用", totalCost.ToString("F1"));

            _previewTree.Add(stats);
        }

        private void AddStatsRow(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("preview-stats-row");

            var lbl = new Label { text = label };
            lbl.AddToClassList("preview-stats-label");
            row.Add(lbl);

            var val = new Label { text = value };
            val.AddToClassList("preview-stats-value");
            row.Add(val);

            parent.Add(row);
        }

        #endregion
    }
}
