using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using CardCore.Data;
using cfg;

namespace CardCore.UI.UIToolkit
{
    /// <summary>
    /// 效果编辑器面板
    /// 使用 UI Toolkit 实现的三栏布局效果编辑界面
    /// </summary>
    public class EffectEditorPanel : BaseUIToolkitPanel
    {
        [Header("数据引用")]
        [SerializeField] private TempEffectDataSO _effectDataSO;

        // Tab 按钮
        private Button _tabBasic;
        private Button _tabTarget;
        private Button _tabCost;
        private Button _tabEffects;
        private Button _tabConditions;

        // Tab 内容
        private VisualElement _tabBasicContent;
        private VisualElement _tabTargetContent;
        private VisualElement _tabCostContent;
        private VisualElement _tabEffectsContent;
        private VisualElement _tabConditionsContent;

        // 基本信息 Tab
        private TextField _effectNameInput;
        private TextField _effectDescInput;
        private DropdownField _triggerTimingDropdown;
        private DropdownField _activationTypeDropdown;
        private IntegerField _speedInput;

        // 目标 Tab
        private DropdownField _targetTypeDropdown;
        private DropdownField _subTargetDropdown;
        private IntegerField _minTargetsInput;
        private IntegerField _maxTargetsInput;
        private Toggle _targetOptionalToggle;
        private VisualElement _targetConditionsList;

        // 代价 Tab
        private VisualElement _manaCostList;
        private VisualElement _resourceCostList;

        // 原子效果 Tab
        private VisualElement _atomicEffectsList;

        // 条件 Tab
        private VisualElement _conditionsList;

        // 效果列表
        private TextField _searchInput;
        private DropdownField _typeFilter;
        private ScrollView _effectList;
        private Button _btnNewEffect;

        // 按钮
        private Button _btnReset;
        private Button _btnSave;
        private Button _btnSaveAs;

        // 预览
        private Label _previewName;
        private Label _previewDescription;
        private Label _previewTarget;
        private Label _previewCost;
        private Label _previewEffects;
        private Label _previewConditions;
        private TextField _jsonPreview;

        // 当前编辑的效果数据
        private TempEffectDefinition _currentEffect;
        private List<TempEffectDefinition> _effects = new List<TempEffectDefinition>();
        private bool _isDirty;

        protected override void Awake()
        {
            base.Awake();
            _panelName = "EffectEditor";
        }

        protected override void BindUIElements()
        {
            base.BindUIElements();

            // Tab 按钮
            _tabBasic = Q<Button>("tab-basic");
            _tabTarget = Q<Button>("tab-target");
            _tabCost = Q<Button>("tab-cost");
            _tabEffects = Q<Button>("tab-effects");
            _tabConditions = Q<Button>("tab-conditions");

            // Tab 内容
            _tabBasicContent = Q("tab-basic-content");
            _tabTargetContent = Q("tab-target-content");
            _tabCostContent = Q("tab-cost-content");
            _tabEffectsContent = Q("tab-effects-content");
            _tabConditionsContent = Q("tab-conditions-content");

            // 基本信息 Tab
            _effectNameInput = Q<TextField>("effect-name-input");
            _effectDescInput = Q<TextField>("effect-desc-input");
            _triggerTimingDropdown = Q<DropdownField>("trigger-timing-dropdown");
            _activationTypeDropdown = Q<DropdownField>("activation-type-dropdown");
            _speedInput = Q<IntegerField>("speed-input");

            // 目标 Tab
            _targetTypeDropdown = Q<DropdownField>("target-type-dropdown");
            _subTargetDropdown = Q<DropdownField>("sub-target-dropdown");
            _minTargetsInput = Q<IntegerField>("min-targets-input");
            _maxTargetsInput = Q<IntegerField>("max-targets-input");
            _targetOptionalToggle = Q<Toggle>("target-optional-toggle");
            _targetConditionsList = Q("target-conditions-list");

            // 代价 Tab
            _manaCostList = Q("mana-cost-list");
            _resourceCostList = Q("resource-cost-list");

            // 原子效果 Tab
            _atomicEffectsList = Q("atomic-effects-list");

            // 条件 Tab
            _conditionsList = Q("conditions-list");

            // 效果列表
            _searchInput = Q<TextField>("search-input");
            _typeFilter = Q<DropdownField>("type-filter");
            _effectList = Q<ScrollView>("effect-list");
            _btnNewEffect = Q<Button>("btn-new-effect");

            // 按钮
            _btnReset = Q<Button>("btn-reset");
            _btnSave = Q<Button>("btn-save");
            _btnSaveAs = Q<Button>("btn-save-as");

            // 预览
            _previewName = Q<Label>("preview-name");
            _previewDescription = Q<Label>("preview-description");
            _previewTarget = Q<Label>("preview-target");
            _previewCost = Q<Label>("preview-cost");
            _previewEffects = Q<Label>("preview-effects");
            _previewConditions = Q<Label>("preview-conditions");
            _jsonPreview = Q<TextField>("json-preview");
        }

        protected override void RegisterEvents()
        {
            base.RegisterEvents();

            // Tab 切换
            _tabBasic?.RegisterCallback<ClickEvent>(evt => SwitchTab("basic"));
            _tabTarget?.RegisterCallback<ClickEvent>(evt => SwitchTab("target"));
            _tabCost?.RegisterCallback<ClickEvent>(evt => SwitchTab("cost"));
            _tabEffects?.RegisterCallback<ClickEvent>(evt => SwitchTab("effects"));
            _tabConditions?.RegisterCallback<ClickEvent>(evt => SwitchTab("conditions"));

            // 新建效果
            _btnNewEffect?.RegisterCallback<ClickEvent>(OnNewEffectClick);

            // 基本信息编辑
            _effectNameInput?.RegisterValueChangedCallback(OnEffectNameChanged);
            _effectDescInput?.RegisterValueChangedCallback(OnEffectDescChanged);
            _triggerTimingDropdown?.RegisterValueChangedCallback(OnTriggerTimingChanged);
            _activationTypeDropdown?.RegisterValueChangedCallback(OnActivationTypeChanged);
            _speedInput?.RegisterValueChangedCallback(OnSpeedChanged);

            // 目标编辑
            _targetTypeDropdown?.RegisterValueChangedCallback(OnTargetTypeChanged);
            _subTargetDropdown?.RegisterValueChangedCallback(OnSubTargetChanged);
            _minTargetsInput?.RegisterValueChangedCallback(OnMinTargetsChanged);
            _maxTargetsInput?.RegisterValueChangedCallback(OnMaxTargetsChanged);
            _targetOptionalToggle?.RegisterValueChangedCallback(OnTargetOptionalChanged);

            // 添加按钮
            BindButton("btn-add-target-condition", OnAddTargetCondition);
            BindButton("btn-add-mana-cost", OnAddManaCost);
            BindButton("btn-add-resource-cost", OnAddResourceCost);
            BindButton("btn-add-atomic-effect", OnAddAtomicEffect);
            BindButton("btn-add-condition", OnAddCondition);

            // 保存/重置
            _btnReset?.RegisterCallback<ClickEvent>(OnResetClick);
            _btnSave?.RegisterCallback<ClickEvent>(OnSaveClick);
            _btnSaveAs?.RegisterCallback<ClickEvent>(OnSaveAsClick);

            // 初始化下拉框
            InitializeDropdowns();
        }

        private void InitializeDropdowns()
        {
            // 触发时点
            if (_triggerTimingDropdown != null)
            {
                _triggerTimingDropdown.choices = GetEnumChoices<TriggerTiming>();
                _triggerTimingDropdown.value = TriggerTiming.瞬间发动.ToString();
            }

            // 发动类型
            if (_activationTypeDropdown != null)
            {
                _activationTypeDropdown.choices = new List<string> { "主动", "被动", "触发" };
                _activationTypeDropdown.value = "主动";
            }

            // 目标类型
            if (_targetTypeDropdown != null)
            {
                _targetTypeDropdown.choices = GetEnumChoices<TargetType>();
                _targetTypeDropdown.value = TargetType.指定卡牌.ToString();
            }

            // 子目标类型
            if (_subTargetDropdown != null)
            {
                _subTargetDropdown.choices = GetEnumChoices<SubTargetType>();
            }

            // 类型筛选
            if (_typeFilter != null)
            {
                _typeFilter.choices = new List<string> { "全部", "伤害", "治疗", "移动", "控制" };
                _typeFilter.value = "全部";
            }
        }

        private List<string> GetEnumChoices<T>() where T : Enum
        {
            var choices = new List<string>();
            foreach (T value in Enum.GetValues(typeof(T)))
            {
                choices.Add(value.ToString());
            }
            return choices;
        }

        private void SwitchTab(string tabName)
        {
            // 清除所有 Tab 激活状态
            _tabBasic?.RemoveFromClassList("active");
            _tabTarget?.RemoveFromClassList("active");
            _tabCost?.RemoveFromClassList("active");
            _tabEffects?.RemoveFromClassList("active");
            _tabConditions?.RemoveFromClassList("active");

            _tabBasicContent?.RemoveFromClassList("active");
            _tabTargetContent?.RemoveFromClassList("active");
            _tabCostContent?.RemoveFromClassList("active");
            _tabEffectsContent?.RemoveFromClassList("active");
            _tabConditionsContent?.RemoveFromClassList("active");

            // 激活选中的 Tab
            switch (tabName)
            {
                case "basic":
                    _tabBasic?.AddToClassList("active");
                    _tabBasicContent?.AddToClassList("active");
                    break;
                case "target":
                    _tabTarget?.AddToClassList("active");
                    _tabTargetContent?.AddToClassList("active");
                    break;
                case "cost":
                    _tabCost?.AddToClassList("active");
                    _tabCostContent?.AddToClassList("active");
                    break;
                case "effects":
                    _tabEffects?.AddToClassList("active");
                    _tabEffectsContent?.AddToClassList("active");
                    break;
                case "conditions":
                    _tabConditions?.AddToClassList("active");
                    _tabConditionsContent?.AddToClassList("active");
                    break;
            }
        }

        public override void Refresh()
        {
            base.Refresh();
            LoadEffects();
        }

        private void LoadEffects()
        {
            // TODO: 从数据源加载效果列表
            _effects.Clear();
            RefreshEffectList();
        }

        private void RefreshEffectList()
        {
            if (_effectList == null) return;

            _effectList.Clear();

            foreach (var effect in _effects)
            {
                var item = CreateEffectListItem(effect);
                _effectList.Add(item);
            }
        }

        private VisualElement CreateEffectListItem(TempEffectDefinition effect)
        {
            var item = new VisualElement { name = $"effect-item-{effect.id}" };
            item.AddToClassList("effect-list-item");

            var name = new Label(effect.name) { name = "effect-name" };
            name.AddToClassList("effect-list-item-name");
            item.Add(name);

            var type = new Label(effect.triggerTiming.ToString()) { name = "effect-type" };
            type.AddToClassList("effect-list-item-type");
            item.Add(type);

            item.RegisterCallback<ClickEvent>(evt => SelectEffect(effect));

            return item;
        }

        private void SelectEffect(TempEffectDefinition effect)
        {
            _currentEffect = effect;
            UpdateEditPanel();
            UpdatePreview();
            HighlightSelectedEffect();
        }

        private void HighlightSelectedEffect()
        {
            if (_effectList == null) return;

            foreach (var child in _effectList.Children())
            {
                child.RemoveFromClassList("selected");
            }

            if (_currentEffect != null)
            {
                var selectedItem = _effectList.Q($"effect-item-{_currentEffect.id}");
                selectedItem?.AddToClassList("selected");
            }
        }

        private void UpdateEditPanel()
        {
            if (_currentEffect == null) return;

            // 基本信息
            _effectNameInput.SetValueWithoutNotify(_currentEffect.name);
            _effectDescInput.SetValueWithoutNotify(_currentEffect.description);
            _triggerTimingDropdown.SetValueWithoutNotify(_currentEffect.triggerTiming.ToString());
            _speedInput.SetValueWithoutNotify(_currentEffect.speed);

            // 目标信息
            _targetTypeDropdown.SetValueWithoutNotify(_currentEffect.targetType.ToString());
            _minTargetsInput.SetValueWithoutNotify(_currentEffect.minTargets);
            _maxTargetsInput.SetValueWithoutNotify(_currentEffect.maxTargets);
            _targetOptionalToggle.SetValueWithoutNotify(_currentEffect.targetOptional);

            // 刷新列表
            RefreshManaCostList();
            RefreshAtomicEffectsList();
            RefreshConditionsList();

            _isDirty = false;
        }

        private void RefreshManaCostList()
        {
            if (_manaCostList == null || _currentEffect == null) return;

            _manaCostList.Clear();

            foreach (var cost in _currentEffect.manaCosts)
            {
                var item = CreateManaCostItem(cost);
                _manaCostList.Add(item);
            }
        }

        private VisualElement CreateManaCostItem(ManaCostEntry cost)
        {
            var item = new VisualElement { name = "mana-cost-item" };
            item.AddToClassList("cost-item");

            var typeDropdown = new DropdownField { name = "cost-type" };
            typeDropdown.AddToClassList("cost-item-type");
            typeDropdown.choices = GetEnumChoices<ManaType>();
            typeDropdown.value = cost.manaType.ToString();
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                if (Enum.TryParse<ManaType>(evt.newValue, out var type))
                {
                    cost.manaType = type;
                    _isDirty = true;
                }
            });
            item.Add(typeDropdown);

            var valueField = new IntegerField { name = "cost-value", value = cost.amount };
            valueField.AddToClassList("cost-item-value");
            valueField.RegisterValueChangedCallback(evt =>
            {
                cost.amount = evt.newValue;
                _isDirty = true;
            });
            item.Add(valueField);

            var deleteBtn = new Button(() =>
            {
                _currentEffect.manaCosts.Remove(cost);
                RefreshManaCostList();
                _isDirty = true;
            }) { text = "删除" };
            deleteBtn.AddToClassList("btn");
            deleteBtn.AddToClassList("btn-sm");
            deleteBtn.AddToClassList("btn-danger");
            item.Add(deleteBtn);

            return item;
        }

        private void RefreshAtomicEffectsList()
        {
            if (_atomicEffectsList == null || _currentEffect == null) return;

            _atomicEffectsList.Clear();

            foreach (var effect in _currentEffect.atomicEffects)
            {
                var item = CreateAtomicEffectItem(effect);
                _atomicEffectsList.Add(item);
            }
        }

        private VisualElement CreateAtomicEffectItem(TempAtomicEffect effect)
        {
            var item = new VisualElement { name = "atomic-effect-item" };
            item.AddToClassList("atomic-effect-item");

            // 头部：类型和删除按钮
            var header = new VisualElement { name = "header" };
            header.AddToClassList("atomic-effect-header");

            var typeDropdown = new DropdownField { name = "effect-type" };
            typeDropdown.AddToClassList("atomic-effect-type");
            typeDropdown.choices = GetEnumChoices<AtomicEffectType>();
            typeDropdown.value = effect.type.ToString();
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                if (Enum.TryParse<AtomicEffectType>(evt.newValue, out var type))
                {
                    effect.type = type;
                    _isDirty = true;
                    UpdatePreview();
                }
            });
            header.Add(typeDropdown);

            var deleteBtn = new Button(() =>
            {
                _currentEffect.atomicEffects.Remove(effect);
                RefreshAtomicEffectsList();
                _isDirty = true;
                UpdatePreview();
            }) { text = "删除" };
            deleteBtn.AddToClassList("btn");
            deleteBtn.AddToClassList("btn-sm");
            deleteBtn.AddToClassList("btn-danger");
            header.Add(deleteBtn);

            item.Add(header);

            // 参数：数值和持续时间
            var paramsContainer = new VisualElement { name = "params" };
            paramsContainer.AddToClassList("atomic-effect-params");

            // 数值参数
            var valueParam = new VisualElement { name = "value-param" };
            valueParam.AddToClassList("atomic-effect-param");
            var valueLabel = new Label("数值") { name = "value-label" };
            valueLabel.AddToClassList("form-label");
            valueParam.Add(valueLabel);
            var valueField = new IntegerField { value = effect.value };
            valueField.RegisterValueChangedCallback(evt =>
            {
                effect.value = evt.newValue;
                _isDirty = true;
                UpdatePreview();
            });
            valueParam.Add(valueField);
            paramsContainer.Add(valueParam);

            // 持续时间
            var durationParam = new VisualElement { name = "duration-param" };
            durationParam.AddToClassList("atomic-effect-param");
            var durationLabel = new Label("持续时间") { name = "duration-label" };
            durationLabel.AddToClassList("form-label");
            durationParam.Add(durationLabel);
            var durationDropdown = new DropdownField();
            durationDropdown.choices = GetEnumChoices<DurationType>();
            durationDropdown.value = effect.duration.ToString();
            durationDropdown.RegisterValueChangedCallback(evt =>
            {
                if (Enum.TryParse<DurationType>(evt.newValue, out var type))
                {
                    effect.duration = type;
                    _isDirty = true;
                }
            });
            durationParam.Add(durationDropdown);
            paramsContainer.Add(durationParam);

            item.Add(paramsContainer);

            return item;
        }

        private void RefreshConditionsList()
        {
            if (_conditionsList == null || _currentEffect == null) return;

            _conditionsList.Clear();

            foreach (var condition in _currentEffect.conditions)
            {
                var item = CreateConditionItem(condition);
                _conditionsList.Add(item);
            }
        }

        private VisualElement CreateConditionItem(TempCondition condition)
        {
            var item = new VisualElement { name = "condition-item" };
            item.AddToClassList("condition-list-item");

            var typeDropdown = new DropdownField { name = "condition-type" };
            typeDropdown.choices = GetEnumChoices<ConditionType>();
            typeDropdown.value = condition.type.ToString();
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                if (Enum.TryParse<ConditionType>(evt.newValue, out var type))
                {
                    condition.type = type;
                    _isDirty = true;
                    UpdatePreview();
                }
            });
            item.Add(typeDropdown);

            var valueField = new IntegerField { value = condition.value };
            valueField.RegisterValueChangedCallback(evt =>
            {
                condition.value = evt.newValue;
                _isDirty = true;
            });
            item.Add(valueField);

            var negateToggle = new Toggle { value = condition.negated };
            negateToggle.RegisterValueChangedCallback(evt =>
            {
                condition.negated = evt.newValue;
                _isDirty = true;
            });
            item.Add(negateToggle);

            var deleteBtn = new Button(() =>
            {
                _currentEffect.conditions.Remove(condition);
                RefreshConditionsList();
                _isDirty = true;
                UpdatePreview();
            }) { text = "删除" };
            deleteBtn.AddToClassList("btn");
            deleteBtn.AddToClassList("btn-sm");
            deleteBtn.AddToClassList("btn-danger");
            item.Add(deleteBtn);

            return item;
        }

        private void UpdatePreview()
        {
            if (_currentEffect == null) return;

            _previewName.text = _currentEffect.name;
            _previewDescription.text = _currentEffect.description;

            // 目标信息
            _previewTarget.text = $"{_currentEffect.targetType}";
            if (_currentEffect.minTargets > 0 || _currentEffect.maxTargets > 0)
            {
                _previewTarget.text += $" ({_currentEffect.minTargets}-{_currentEffect.maxTargets})";
            }

            // 代价信息
            var costText = "";
            foreach (var cost in _currentEffect.manaCosts)
            {
                costText += $"{cost.manaType}:{cost.amount} ";
            }
            _previewCost.text = string.IsNullOrEmpty(costText) ? "无" : costText;

            // 原子效果
            var effectsText = "";
            foreach (var effect in _currentEffect.atomicEffects)
            {
                effectsText += $"• {effect.type}";
                if (effect.value > 0) effectsText += $" ({effect.value})";
                effectsText += "\n";
            }
            _previewEffects.text = string.IsNullOrEmpty(effectsText) ? "无" : effectsText;

            // 条件
            var conditionsText = "";
            foreach (var condition in _currentEffect.conditions)
            {
                conditionsText += $"• {(condition.negated ? "非" : "")}{condition.type}";
                if (condition.value > 0) conditionsText += $" ({condition.value})";
                conditionsText += "\n";
            }
            _previewConditions.text = string.IsNullOrEmpty(conditionsText) ? "无" : conditionsText;

            // JSON 预览
            UpdateJsonPreview();
        }

        private void UpdateJsonPreview()
        {
            if (_currentEffect == null || _jsonPreview == null) return;

            var json = JsonUtility.ToJson(_currentEffect, true);
            _jsonPreview.SetValueWithoutNotify(json);
        }

        #region 事件处理

        private void OnNewEffectClick(ClickEvent evt)
        {
            var newEffect = new TempEffectDefinition
            {
                id = $"EFFECT_{DateTime.Now.Ticks}",
                name = "新效果",
                description = "",
                triggerTiming = TriggerTiming.瞬间发动,
                speed = 1
            };

            _effects.Add(newEffect);
            RefreshEffectList();
            SelectEffect(newEffect);
            _isDirty = true;
        }

        private void OnEffectNameChanged(ChangeEvent<string> evt)
        {
            if (_currentEffect == null) return;
            _currentEffect.name = evt.newValue;
            _isDirty = true;
            UpdatePreview();
        }

        private void OnEffectDescChanged(ChangeEvent<string> evt)
        {
            if (_currentEffect == null) return;
            _currentEffect.description = evt.newValue;
            _isDirty = true;
            UpdatePreview();
        }

        private void OnTriggerTimingChanged(ChangeEvent<string> evt)
        {
            if (_currentEffect == null) return;
            if (Enum.TryParse<TriggerTiming>(evt.newValue, out var timing))
            {
                _currentEffect.triggerTiming = timing;
                _isDirty = true;
            }
        }

        private void OnActivationTypeChanged(ChangeEvent<string> evt)
        {
            // TODO: 处理发动类型变化
            _isDirty = true;
        }

        private void OnSpeedChanged(ChangeEvent<int> evt)
        {
            if (_currentEffect == null) return;
            _currentEffect.speed = evt.newValue;
            _isDirty = true;
        }

        private void OnTargetTypeChanged(ChangeEvent<string> evt)
        {
            if (_currentEffect == null) return;
            if (Enum.TryParse<TargetType>(evt.newValue, out var type))
            {
                _currentEffect.targetType = type;
                _isDirty = true;
                UpdatePreview();
            }
        }

        private void OnSubTargetChanged(ChangeEvent<string> evt)
        {
            if (_currentEffect == null) return;
            if (Enum.TryParse<SubTargetType>(evt.newValue, out var type))
            {
                _currentEffect.subTargetType = type;
                _isDirty = true;
            }
        }

        private void OnMinTargetsChanged(ChangeEvent<int> evt)
        {
            if (_currentEffect == null) return;
            _currentEffect.minTargets = evt.newValue;
            _isDirty = true;
            UpdatePreview();
        }

        private void OnMaxTargetsChanged(ChangeEvent<int> evt)
        {
            if (_currentEffect == null) return;
            _currentEffect.maxTargets = evt.newValue;
            _isDirty = true;
            UpdatePreview();
        }

        private void OnTargetOptionalChanged(ChangeEvent<bool> evt)
        {
            if (_currentEffect == null) return;
            _currentEffect.targetOptional = evt.newValue;
            _isDirty = true;
        }

        private void OnAddTargetCondition()
        {
            // TODO: 打开条件编辑弹窗
        }

        private void OnAddManaCost()
        {
            if (_currentEffect == null) return;
            _currentEffect.manaCosts.Add(new ManaCostEntry { manaType = ManaType.灰色, amount = 1 });
            RefreshManaCostList();
            _isDirty = true;
            UpdatePreview();
        }

        private void OnAddResourceCost()
        {
            // TODO: 添加资源代价
        }

        private void OnAddAtomicEffect()
        {
            if (_currentEffect == null) return;
            _currentEffect.atomicEffects.Add(new TempAtomicEffect
            {
                type = AtomicEffectType.造成战斗伤害,
                value = 1,
                duration = DurationType.一次性
            });
            RefreshAtomicEffectsList();
            _isDirty = true;
            UpdatePreview();
        }

        private void OnAddCondition()
        {
            if (_currentEffect == null) return;
            _currentEffect.conditions.Add(new TempCondition
            {
                type = ConditionType.每回合一次,
                value = 0,
                negated = false
            });
            RefreshConditionsList();
            _isDirty = true;
            UpdatePreview();
        }

        private void OnResetClick(ClickEvent evt)
        {
            if (_currentEffect != null)
            {
                UpdateEditPanel();
                _isDirty = false;
            }
        }

        private void OnSaveClick(ClickEvent evt)
        {
            SaveEffect();
        }

        private void OnSaveAsClick(ClickEvent evt)
        {
            SaveEffectAs();
        }

        #endregion

        private void SaveEffect()
        {
            if (_currentEffect == null) return;

            Debug.Log($"保存效果: {_currentEffect.name}");
            _isDirty = false;
            RefreshEffectList();
        }

        private void SaveEffectAs()
        {
            if (_currentEffect == null) return;

            var newEffect = new TempEffectDefinition
            {
                id = $"EFFECT_{DateTime.Now.Ticks}",
                name = _currentEffect.name + " (副本)",
                description = _currentEffect.description,
                triggerTiming = _currentEffect.triggerTiming,
                speed = _currentEffect.speed,
                targetType = _currentEffect.targetType,
                minTargets = _currentEffect.minTargets,
                maxTargets = _currentEffect.maxTargets,
                targetOptional = _currentEffect.targetOptional
            };

            foreach (var cost in _currentEffect.manaCosts)
            {
                newEffect.manaCosts.Add(new ManaCostEntry { manaType = cost.manaType, amount = cost.amount });
            }

            foreach (var effect in _currentEffect.atomicEffects)
            {
                newEffect.atomicEffects.Add(new TempAtomicEffect
                {
                    type = effect.type,
                    value = effect.value,
                    duration = effect.duration
                });
            }

            foreach (var condition in _currentEffect.conditions)
            {
                newEffect.conditions.Add(new TempCondition
                {
                    type = condition.type,
                    value = condition.value,
                    negated = condition.negated
                });
            }

            _effects.Add(newEffect);
            RefreshEffectList();
            SelectEffect(newEffect);
        }
    }

    #region 临时数据类

    [Serializable]
    public class TempEffectDefinition
    {
        public string id;
        public string name;
        public string description;
        public TriggerTiming triggerTiming;
        public int speed;
        public TargetType targetType;
        public SubTargetType subTargetType;
        public int minTargets;
        public int maxTargets;
        public bool targetOptional;
        public List<ManaCostEntry> manaCosts = new List<ManaCostEntry>();
        public List<TempAtomicEffect> atomicEffects = new List<TempAtomicEffect>();
        public List<TempCondition> conditions = new List<TempCondition>();
    }

    [Serializable]
    public class TempAtomicEffect
    {
        public AtomicEffectType type;
        public int value;
        public DurationType duration;
    }

    [Serializable]
    public class TempCondition
    {
        public ConditionType type;
        public int value;
        public bool negated;
    }

    #endregion
}