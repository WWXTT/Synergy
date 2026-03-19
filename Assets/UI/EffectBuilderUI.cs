using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using cfg;
using CardCore.Data;

namespace CardCore.UI
{
    /// <summary>
    /// 效果组装界面 UI
    /// 用于创建和编辑完整的效果定义
    /// </summary>
    public class EffectBuilderUI : BaseUI
    {
        #region 序列化字段

        [Header("面板引用")]
        [SerializeField] private GameObject _basicInfoPanel;
        [SerializeField] private GameObject _targetPanel;
        [SerializeField] private GameObject _costPanel;
        [SerializeField] private GameObject _effectsPanel;
        [SerializeField] private GameObject _conditionsPanel;

        [Header("时点输入")]
        [SerializeField] private TMP_Dropdown _activationTypeDropdown;
        [SerializeField] private TMP_Dropdown _triggerTimingDropdown;
        [SerializeField] private TMP_InputField _speedInput;

        [Header("目标选择")]
        [SerializeField] private TMP_Dropdown _targetCategoryDropdown;
        [SerializeField] private TMP_Dropdown _subTargetDropdown;
        [SerializeField] private TMP_Text _targetCoefficientText;
        [SerializeField] private GameObject _maxTargetsInputContainer;
        [SerializeField] private TMP_InputField _minTargetsInput;
        [SerializeField] private TMP_InputField _maxTargetsInput;
        [SerializeField] private Toggle _optionalTargetToggle;
        [SerializeField] private Button _editTargetFilterButton;

        [Header("代价设置")]
        [SerializeField] private Transform _elementCostsContainer;
        [SerializeField] private GameObject _elementCostItemPrefab;
        [SerializeField] private Button _addElementCostButton;

        [SerializeField] private Transform _resourceCostsContainer;
        [SerializeField] private GameObject _resourceCostItemPrefab;
        [SerializeField] private Button _addResourceCostButton;

        [Header("原子效果")]
        [SerializeField] private TMP_Dropdown _effectColorDropdown;
        [SerializeField] private TMP_Dropdown _effectTypeDropdown;
        [SerializeField] private TMP_Text _effectDescriptionText;
        [SerializeField] private GameObject _effectValueInputContainer;
        [SerializeField] private TMP_InputField _effectValueInput;
        [SerializeField] private TMP_Text _finalEffectText;
        [SerializeField] private Button _addEffectButton;
        [SerializeField] private Transform _atomicEffectsContainer;
        [SerializeField] private GameObject _atomicEffectItemPrefab;

        [Header("发动条件")]
        [SerializeField] private Transform _conditionsContainer;
        [SerializeField] private GameObject _conditionItemPrefab;
        [SerializeField] private Button _addConditionButton;

        [Header("预览")]
        [SerializeField] private TMP_Text _previewText;
        [SerializeField] private ScrollRect _previewScrollRect;

        [Header("操作按钮")]
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _clearButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _confirmButton;

        [Header("弹窗引用")]
        [SerializeField] private AtomicEffectEditorPopup _atomicEffectPopup;
        [SerializeField] private ConditionEditorPopup _conditionPopup;

        #endregion

        #region 私有字段

        /// <summary>当前编辑的效果数据</summary>
        private EffectDefinitionData _currentEffect;

        /// <summary>元素代价项列表</summary>
        private List<EffectCostItemUI> _elementCostItems = new List<EffectCostItemUI>();

        /// <summary>资源代价项列表</summary>
        private List<EffectCostItemUI> _resourceCostItems = new List<EffectCostItemUI>();

        /// <summary>条件项列表</summary>
        private List<ConditionItemUI> _conditionItems = new List<ConditionItemUI>();

        /// <summary>效果改变回调</summary>
        private Action<EffectDefinitionData> _onEffectConfirmed;

        /// <summary>是否为编辑模式</summary>
        private bool _isEditMode = false;

        /// <summary>当前选择的目标分类</summary>
        private TargetCategory _currentTargetCategory;

        /// <summary>当前选择的子目标类型</summary>
        private SubTargetType _currentSubTargetType;

        /// <summary>当前目标系数</summary>
        private float _currentTargetCoefficient = 1.0f;

        /// <summary>当前选择的效果颜色</summary>
        private EffectColor _currentEffectColor;

        /// <summary>当前选择的原子效果类型</summary>
        private AtomicEffectType _currentEffectType;

        #endregion

        #region 生命周期

        protected override void Initialize()
        {
            base.Initialize();

            // 初始化下拉菜单
            InitializeDropdowns();

            // 绑定按钮事件
            BindButtonEvents();

            // 绑定输入事件
            BindInputEvents();

            // 创建新效果
            CreateNewEffect();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置编辑效果
        /// </summary>
        /// <param name="effectData">效果数据</param>
        /// <param name="onConfirmed">确认回调</param>
        public void SetEffect(EffectDefinitionData effectData, Action<EffectDefinitionData> onConfirmed = null)
        {
            _onEffectConfirmed = onConfirmed;
            _isEditMode = effectData != null;

            if (effectData != null)
            {
                // 深拷贝效果数据
                string json = JsonUtility.ToJson(effectData);
                _currentEffect = JsonUtility.FromJson<EffectDefinitionData>(json);
            }
            else
            {
                CreateNewEffect();
            }

            UpdateUIFromEffect();
        }

        /// <summary>
        /// 获取当前效果数据
        /// </summary>
        /// <returns>效果数据</returns>
        public EffectDefinitionData GetEffect()
        {
            UpdateEffectFromUI();
            return _currentEffect;
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化下拉菜单
        /// </summary>
        private void InitializeDropdowns()
        {
            // 触发时点下拉菜单
            if (_triggerTimingDropdown != null)
            {
                _triggerTimingDropdown.ClearOptions();
                var timingOptions = new List<string>();
                foreach (TriggerTiming timing in Enum.GetValues(typeof(TriggerTiming)))
                {
                    timingOptions.Add(GetTriggerTimingDisplayName(timing));
                }
                _triggerTimingDropdown.AddOptions(timingOptions);
            }

            // 发动类型下拉菜单
            if (_activationTypeDropdown != null)
            {
                _activationTypeDropdown.ClearOptions();
                var activationOptions = new List<string>
                {
                    "基于速度自由发动",
                    "基于条件自由发动",
                    "基于条件强制发动"
                };
                _activationTypeDropdown.AddOptions(activationOptions);
            }

            // 目标分类下拉菜单（一级）
            if (_targetCategoryDropdown != null)
            {
                _targetCategoryDropdown.ClearOptions();
                var categoryOptions = new List<string>();
                foreach (TargetCategory category in Enum.GetValues(typeof(TargetCategory)))
                {
                    categoryOptions.Add(TargetCoefficientConfig.GetCategoryDisplayName(category));
                }
                _targetCategoryDropdown.AddOptions(categoryOptions);
                _currentTargetCategory = TargetCategory.卡牌;
            }

            // 子目标下拉菜单（���级）
            if (_subTargetDropdown != null)
            {
                UpdateSubTargetDropdown(TargetCategory.卡牌);
            }

            // 初始化目标数输入框显示状态
            UpdateTargetCountInputVisibility();

            // 效果颜色分类下拉菜单
            if (_effectColorDropdown != null)
            {
                _effectColorDropdown.ClearOptions();
                var colorOptions = new List<string>();
                foreach (EffectColor color in Enum.GetValues(typeof(EffectColor)))
                {
                    colorOptions.Add(GetEffectColorDisplayName(color));
                }
                _effectColorDropdown.AddOptions(colorOptions);
                _currentEffectColor = EffectColor.Red;
            }

            // 原子效果类型下拉菜单（根据颜色分类）
            if (_effectTypeDropdown != null)
            {
                UpdateEffectTypeDropdown(EffectColor.Red);
            }

            // 初始化效果参数输入框显示状态
            UpdateEffectValueInputVisibility();
        }

        /// <summary>
        /// 更新原子效果类型下拉菜单
        /// </summary>
        private void UpdateEffectTypeDropdown(EffectColor color)
        {
            if (_effectTypeDropdown == null) return;

            _effectTypeDropdown.ClearOptions();

            var effectTypes = EffectClassificationConfig.GetEffectsByColor(color);

            var effectOptions = new List<string>();
            foreach (var effectType in effectTypes)
            {
                effectOptions.Add(GetEffectTypeDisplayName(effectType));
            }

            _effectTypeDropdown.AddOptions(effectOptions);

            // 设置默认选择
            if (effectTypes.Count > 0)
            {
                _currentEffectType = effectTypes[0];
                UpdateEffectDescription();
                UpdateEffectValueInputVisibility();
            }
        }

        /// <summary>
        /// 更新效果参数输入框显示状态
        /// </summary>
        private void UpdateEffectValueInputVisibility()
        {
            if (_effectValueInputContainer != null)
            {
                bool needsValue = EffectNeedsParameter(_currentEffectType);
                _effectValueInputContainer.SetActive(needsValue);
            }
        }

        /// <summary>
        /// 判断效果是否需要参数
        /// </summary>
        private bool EffectNeedsParameter(AtomicEffectType effectType)
        {
            return effectType switch
            {
                // 需要参数的效果
                AtomicEffectType.DealDamage or
                AtomicEffectType.DealCombatDamage or
                AtomicEffectType.AoEDamage or
                AtomicEffectType.SplitDamage or
                AtomicEffectType.Heal or
                AtomicEffectType.LifeLoss or
                AtomicEffectType.DrawCard or
                AtomicEffectType.DiscardCard or
                AtomicEffectType.MillCard or
                AtomicEffectType.SearchDeck or
                AtomicEffectType.ModifyPower or
                AtomicEffectType.ModifyLife or
                AtomicEffectType.SetPower or
                AtomicEffectType.SetLife or
                AtomicEffectType.AddMana or
                AtomicEffectType.ConsumeMana or
                AtomicEffectType.PreventDamage or
                AtomicEffectType.DrawThenDiscard or
                AtomicEffectType.ScryCards or
                AtomicEffectType.AddCounters or
                AtomicEffectType.DoubleCounters => true,

                // 不需要参数的效果
                _ => false
            };
        }

        /// <summary>
        /// 更新子目标下拉菜单
        /// </summary>
        private void UpdateSubTargetDropdown(TargetCategory category)
        {
            if (_subTargetDropdown == null) return;

            _subTargetDropdown.ClearOptions();

            var subTargets = TargetCoefficientConfig.CategorySubTargets.TryGetValue(category, out var targets)
                ? targets
                : Array.Empty<SubTargetType>();

            var subOptions = new List<string>();
            foreach (var subTarget in subTargets)
            {
                subOptions.Add(TargetCoefficientConfig.GetDisplayNameWithCoefficient(subTarget));
            }

            _subTargetDropdown.AddOptions(subOptions);

            // 设置默认选择
            if (subTargets.Length > 0)
            {
                _currentSubTargetType = subTargets[0];
                UpdateCoefficientDisplay();
            }
        }

        /// <summary>
        /// 更新目标数输入框显示状态
        /// </summary>
        private void UpdateTargetCountInputVisibility()
        {
            if (_maxTargetsInputContainer != null)
            {
                _maxTargetsInputContainer.SetActive(TargetCoefficientConfig.NeedsTargetCountInput(_currentTargetCategory));
            }
        }

        /// <summary>
        /// 更新系数显示
        /// </summary>
        private void UpdateCoefficientDisplay()
        {
            _currentTargetCoefficient = TargetCoefficientConfig.GetCoefficient(_currentSubTargetType);

            // 如果需要目标数输入，计算最终系数
            if (TargetCoefficientConfig.NeedsTargetCountInput(_currentTargetCategory))
            {
                if (int.TryParse(_maxTargetsInput?.text, out int targetCount) && targetCount > 0)
                {
                    _currentTargetCoefficient = TargetCoefficientConfig.CalculateFinalCoefficient(_currentSubTargetType, targetCount);
                }
            }

            if (_targetCoefficientText != null)
            {
                _targetCoefficientText.text = $"费用系数: ×{_currentTargetCoefficient:F2}";
            }
        }

        /// <summary>
        /// 绑定按钮事件
        /// </summary>
        private void BindButtonEvents()
        {
            _saveButton?.AddClickListener(OnSaveClicked);
            _loadButton?.AddClickListener(OnLoadClicked);
            _clearButton?.AddClickListener(OnClearClicked);
            _backButton?.AddClickListener(OnBackClicked);
            _confirmButton?.AddClickListener(OnConfirmClicked);

            _addElementCostButton?.AddClickListener(OnAddElementCostClicked);
            _addResourceCostButton?.AddClickListener(OnAddResourceCostClicked);
            _addEffectButton?.AddClickListener(OnAddEffectClicked);
            _addConditionButton?.AddClickListener(OnAddConditionClicked);
            _editTargetFilterButton?.AddClickListener(OnEditTargetFilterClicked);
        }

        /// <summary>
        /// 绑定输入事件
        /// </summary>
        private void BindInputEvents()
        {
            _triggerTimingDropdown?.onValueChanged.AddListener(OnBasicInfoChanged);
            _activationTypeDropdown?.onValueChanged.AddListener(OnBasicInfoChanged);
            _speedInput?.onValueChanged.AddListener(OnBasicInfoChanged);

            _targetCategoryDropdown?.onValueChanged.AddListener(OnTargetCategoryChanged);
            _subTargetDropdown?.onValueChanged.AddListener(OnSubTargetChanged);
            _minTargetsInput?.onValueChanged.AddListener(OnTargetChanged);
            _maxTargetsInput?.onValueChanged.AddListener(OnTargetCountChanged);
            _optionalTargetToggle?.onValueChanged.AddListener(OnTargetChanged);

            _effectColorDropdown?.onValueChanged.AddListener(OnEffectColorChanged);
            _effectTypeDropdown?.onValueChanged.AddListener(OnEffectTypeChanged);
            _effectValueInput?.onValueChanged.AddListener(OnEffectValueChanged);
        }

        #endregion

        #region UI更新方法

        /// <summary>
        /// 从效果数据更新UI
        /// </summary>
        private void UpdateUIFromEffect()
        {
            if (_currentEffect == null) return;

            // 基本信息
            _triggerTimingDropdown.value = _currentEffect.TriggerTiming;
            _activationTypeDropdown.value = _currentEffect.ActivationType;
            _speedInput.text = _currentEffect.BaseSpeed.ToString();

            // 目标选择
            if (_currentEffect.TargetSelector != null)
            {
                // 从存储的值恢复目标分类和子目标
                _currentTargetCategory = _currentEffect.TargetSelector.TargetCategory;
                _currentSubTargetType = _currentEffect.TargetSelector.SubTargetType;

                _targetCategoryDropdown.value = (int)_currentTargetCategory;
                UpdateSubTargetDropdown(_currentTargetCategory);

                // 设置子目标下拉菜单的选中值
                if (TargetCoefficientConfig.CategorySubTargets.TryGetValue(_currentTargetCategory, out var subTargets))
                {
                    int subIndex = System.Array.IndexOf(subTargets, _currentSubTargetType);
                    if (subIndex >= 0)
                    {
                        _subTargetDropdown.value = subIndex;
                    }
                }

                _minTargetsInput.text = _currentEffect.TargetSelector.MinTargets.ToString();
                _maxTargetsInput.text = _currentEffect.TargetSelector.MaxTargets.ToString();
                _optionalTargetToggle.isOn = _currentEffect.TargetSelector.Optional;

                UpdateTargetCountInputVisibility();
                UpdateCoefficientDisplay();
            }

            // 刷新列表
            RefreshCostLists();
            RefreshConditionsList();

            // 更新预览
            UpdatePreview();
        }

        /// <summary>
        /// 从UI更新效果数据
        /// </summary>
        private void UpdateEffectFromUI()
        {
            if (_currentEffect == null) return;

            // 基本信息
            _currentEffect.TriggerTiming = _triggerTimingDropdown?.value ?? 0;
            _currentEffect.ActivationType = _activationTypeDropdown?.value ?? 0;

            if (int.TryParse(_speedInput?.text, out int speed))
            {
                _currentEffect.BaseSpeed = speed;
            }

            // 目标选择
            if (_currentEffect.TargetSelector == null)
            {
                _currentEffect.TargetSelector = new TargetSelectorData();
            }

            _currentEffect.TargetSelector.TargetCategory = _currentTargetCategory;
            _currentEffect.TargetSelector.SubTargetType = _currentSubTargetType;
            _currentEffect.TargetSelector.TargetCoefficient = _currentTargetCoefficient;

            if (int.TryParse(_minTargetsInput?.text, out int minTargets))
            {
                _currentEffect.TargetSelector.MinTargets = minTargets;
            }

            if (int.TryParse(_maxTargetsInput?.text, out int maxTargets))
            {
                _currentEffect.TargetSelector.MaxTargets = maxTargets;
            }

            _currentEffect.TargetSelector.Optional = _optionalTargetToggle?.isOn ?? false;

            // 更新代价
            UpdateCostFromUI();

            // 更新条件
            UpdateConditionsFromUI();
        }

        /// <summary>
        /// 更新预览
        /// </summary>
        private void UpdatePreview()
        {
            if (_previewText == null) return;

            UpdateEffectFromUI();

            var definition = _currentEffect?.ToDefinition();
            if (definition != null)
            {
                _previewText.text = definition.GetFullDescription();
            }
            else
            {
                _previewText.text = "未配置效果";
            }
        }

        /// <summary>
        /// 刷新代价列表
        /// </summary>
        private void RefreshCostLists()
        {
            // 清除元素代价项
            foreach (var item in _elementCostItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _elementCostItems.Clear();

            // 清除资源代价项
            foreach (var item in _resourceCostItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _resourceCostItems.Clear();

            if (_currentEffect?.Cost == null) return;

            // 创建元素代价项
            foreach (var elementCost in _currentEffect.Cost.ElementCosts)
            {
                CreateElementCostItem(elementCost);
            }

            // 创建资源代价项
            foreach (var resourceCost in _currentEffect.Cost.ResourceCosts)
            {
                CreateResourceCostItem(resourceCost);
            }
        }

        /// <summary>
        /// 刷新条件列表
        /// </summary>
        private void RefreshConditionsList()
        {
            // 清除现有项
            foreach (var item in _conditionItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _conditionItems.Clear();

            if (_currentEffect?.ActivationConditions == null) return;

            // 创建条件项
            foreach (var condition in _currentEffect.ActivationConditions)
            {
                CreateConditionItem(condition);
            }
        }

        #endregion

        #region 创建项方法

        /// <summary>
        /// 创建元素代价项
        /// </summary>
        private void CreateElementCostItem(ElementCostData data)
        {
            if (_elementCostItemPrefab == null || _elementCostsContainer == null) return;

            var itemObj = Instantiate(_elementCostItemPrefab, _elementCostsContainer);
            var itemUI = itemObj.GetComponent<EffectCostItemUI>();

            if (itemUI != null)
            {
                itemUI.SetElementCost(data, OnElementCostChanged, OnElementCostRemoved);
                _elementCostItems.Add(itemUI);
            }
        }

        /// <summary>
        /// 创建资源代价项
        /// </summary>
        private void CreateResourceCostItem(ResourceCostData data)
        {
            if (_resourceCostItemPrefab == null || _resourceCostsContainer == null) return;

            var itemObj = Instantiate(_resourceCostItemPrefab, _resourceCostsContainer);
            var itemUI = itemObj.GetComponent<EffectCostItemUI>();

            if (itemUI != null)
            {
                itemUI.SetResourceCost(data, OnResourceCostChanged, OnResourceCostRemoved);
                _resourceCostItems.Add(itemUI);
            }
        }

        /// <summary>
        /// 创建原子效果项
        /// </summary>
        private void CreateAtomicEffectItem(AtomicEffectData data)
        {
            var go = new GameObject($"Effect_{data.Type}");
            go.transform.SetParent(_atomicEffectsContainer);

            var text = go.AddComponent<TMP_Text>();
            text.text = $"{GetEffectTypeDisplayName((AtomicEffectType)data.Type)}: {GetEffectTypeDescription((AtomicEffectType)data.Type, data.Value, data.Value2)}";
            text.fontSize = 14;

            // 添加删除按钮
            var buttonGo = new GameObject("RemoveButton");
            buttonGo.transform.SetParent(go.transform);
            var button = buttonGo.AddComponent<Button>();
            var buttonText = buttonGo.AddComponent<TMP_Text>();
            buttonText.text = "×";
            buttonText.fontSize = 16;
            buttonText.color = Color.red;

            button.onClick.AddListener(() =>
            {
                _currentEffect?.Effects.Remove(data);
                Destroy(go);
                UpdatePreview();
            });
        }

        /// <summary>
        /// 创建条件项
        /// </summary>
        private void CreateConditionItem(ActivationConditionData data)
        {
            if (_conditionItemPrefab == null || _conditionsContainer == null) return;

            var itemObj = Instantiate(_conditionItemPrefab, _conditionsContainer);
            var itemUI = itemObj.GetComponent<ConditionItemUI>();

            if (itemUI != null)
            {
                itemUI.Initialize(data, OnConditionChanged, OnConditionRemoved);
                _conditionItems.Add(itemUI);
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 基本信息 changed
        /// </summary>
        private void OnBasicInfoChanged(string value) => UpdatePreview();
        private void OnBasicInfoChanged(int value) => UpdatePreview();
        private void OnBasicInfoChanged(bool value) => UpdatePreview();

        /// <summary>
        /// 目标 changed
        /// </summary>
        private void OnTargetChanged(string value) => UpdatePreview();
        private void OnTargetChanged(int value) => UpdatePreview();
        private void OnTargetChanged(bool value) => UpdatePreview();

        /// <summary>
        /// 目标分类改变
        /// </summary>
        private void OnTargetCategoryChanged(int index)
        {
            _currentTargetCategory = (TargetCategory)index;
            UpdateSubTargetDropdown(_currentTargetCategory);
            UpdateTargetCountInputVisibility();
            UpdateCoefficientDisplay();
            UpdatePreview();
        }

        /// <summary>
        /// 子目标类型改变
        /// </summary>
        private void OnSubTargetChanged(int index)
        {
            if (TargetCoefficientConfig.CategorySubTargets.TryGetValue(_currentTargetCategory, out var subTargets))
            {
                if (index >= 0 && index < subTargets.Length)
                {
                    _currentSubTargetType = subTargets[index];
                    UpdateCoefficientDisplay();
                    UpdatePreview();
                }
            }
        }

        /// <summary>
        /// 目标数改变
        /// </summary>
        private void OnTargetCountChanged(string value)
        {
            UpdateCoefficientDisplay();
            UpdatePreview();
        }

        /// <summary>
        /// 原子效果类型改变
        /// </summary>
        private void OnEffectTypeChanged(int index)
        {
            // 根据当前颜色和索引获取效果类型
            var effectTypes = EffectClassificationConfig.GetEffectsByColor(_currentEffectColor);
            if (effectTypes != null && effectTypes.Count > 0)
            {
                if (index >= 0 && index < effectTypes.Count)
                {
                    _currentEffectType = effectTypes[index];
                    UpdateEffectDescription();
                    UpdateEffectValueInputVisibility();
                }
            }
        }

        /// <summary>
        /// 效果颜色改变
        /// </summary>
        private void OnEffectColorChanged(int index)
        {
            _currentEffectColor = (EffectColor)index;
            UpdateEffectTypeDropdown(_currentEffectColor);
        }

        /// <summary>
        /// 效果参数值改变
        /// </summary>
        private void OnEffectValueChanged(string value)
        {
            UpdateEffectDescription();
        }

        /// <summary>
        /// 更新效果描述
        /// </summary>
        private void UpdateEffectDescription()
        {
            if (_effectDescriptionText == null) return;

            // 获取数值参数
            int value1 = 0;
            if (int.TryParse(_effectValueInput?.text, out int v1))
                value1 = v1;

            _effectDescriptionText.text = GetEffectTypeDescription(_currentEffectType, value1, 0);
        }

        /// <summary>
        /// 添加原子效果
        /// </summary>
        private void OnAddEffectClicked()
        {
            if (_currentEffect == null) return;

            var newEffect = new AtomicEffectData
            {
                Type = (int)_currentEffectType,
                Value = 0,
                Value2 = 0
            };

            if (int.TryParse(_effectValueInput?.text, out int v1))
                newEffect.Value = v1;

            _currentEffect.Effects.Add(newEffect);
            CreateAtomicEffectItem(newEffect);
            UpdateFinalEffectText();
            UpdatePreview();
        }

        /// <summary>
        /// 更新最终效果文本
        /// </summary>
        private void UpdateFinalEffectText()
        {
            if (_finalEffectText == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 已添加效果 ===");

            if (_currentEffect?.Effects != null)
            {
                for (int i = 0; i < _currentEffect.Effects.Count; i++)
                {
                    var effect = _currentEffect.Effects[i];
                    sb.AppendLine($"{i + 1}. {GetEffectTypeDisplayName((AtomicEffectType)effect.Type)}: {GetEffectTypeDescription((AtomicEffectType)effect.Type, effect.Value, effect.Value2)}");
                }
            }
            else
            {
                sb.AppendLine("无效果");
            }

            _finalEffectText.text = sb.ToString();
        }

        /// <summary>
        /// 添加元素代价
        /// </summary>
        private void OnAddElementCostClicked()
        {
            if (_currentEffect?.Cost == null) return;

            var newCost = new ElementCostData
            {
                ManaType = 0,
                Amount = 1
            };

            _currentEffect.Cost.ElementCosts.Add(newCost);
            CreateElementCostItem(newCost);
            UpdatePreview();
        }

        /// <summary>
        /// 添加资源代价
        /// </summary>
        private void OnAddResourceCostClicked()
        {
            if (_currentEffect?.Cost == null) return;

            var newCost = new ResourceCostData
            {
                FromZone = 0,
                ToZone = 0,
                Count = 1
            };

            _currentEffect.Cost.ResourceCosts.Add(newCost);
            CreateResourceCostItem(newCost);
            UpdatePreview();
        }

        /// <summary>
        /// 添加原子效果
        /// </summary>
        private void OnAddAtomicEffectClicked()
        {
            if (_atomicEffectPopup != null)
            {
                _atomicEffectPopup.Show(null, (data) =>
                {
                    if (data != null)
                    {
                        if (_currentEffect == null) CreateNewEffect();

                        _currentEffect.Effects.Add(data);
                        CreateAtomicEffectItem(data);
                        UpdatePreview();
                    }
                });
            }
            else
            {
                // 简单创建默认效果
                var newEffect = new AtomicEffectData
                {
                    Type = 0,
                    Value = 1
                };

                if (_currentEffect == null) CreateNewEffect();

                _currentEffect.Effects.Add(newEffect);
                CreateAtomicEffectItem(newEffect);
                UpdatePreview();
            }
        }

        /// <summary>
        /// 添加条件
        /// </summary>
        private void OnAddConditionClicked()
        {
            if (_conditionPopup != null)
            {
                _conditionPopup.Show(null, (data) =>
                {
                    if (data != null)
                    {
                        if (_currentEffect == null) CreateNewEffect();

                        _currentEffect.ActivationConditions.Add(data);
                        CreateConditionItem(data);
                        UpdatePreview();
                    }
                });
            }
            else
            {
                // 简单创建默认条件
                var newCondition = new ActivationConditionData
                {
                    Type = 0,
                    Value = 1
                };

                if (_currentEffect == null) CreateNewEffect();

                _currentEffect.ActivationConditions.Add(newCondition);
                CreateConditionItem(newCondition);
                UpdatePreview();
            }
        }

        /// <summary>
        /// 编辑目标筛选条件
        /// </summary>
        private void OnEditTargetFilterClicked()
        {
            // TODO: 打开目标筛选条件编辑器
            UIManager.Instance.ShowNotification("目标筛选条件编辑器待实现");
        }

        /// <summary>
        /// 元素代价 changed
        /// </summary>
        private void OnElementCostChanged(ElementCostData data)
        {
            UpdatePreview();
        }

        /// <summary>
        /// 元素代价 removed
        /// </summary>
        private void OnElementCostRemoved(ElementCostData data)
        {
            _currentEffect?.Cost?.ElementCosts.Remove(data);
            RefreshCostLists();
            UpdatePreview();
        }

        /// <summary>
        /// 资源代价 changed
        /// </summary>
        private void OnResourceCostChanged(ResourceCostData data)
        {
            UpdatePreview();
        }

        /// <summary>
        /// 资源代价 removed
        /// </summary>
        private void OnResourceCostRemoved(ResourceCostData data)
        {
            _currentEffect?.Cost?.ResourceCosts.Remove(data);
            RefreshCostLists();
            UpdatePreview();
        }



        /// <summary>
        /// 条件 changed
        /// </summary>
        private void OnConditionChanged(ActivationConditionData data)
        {
            UpdatePreview();
        }

        /// <summary>
        /// 条件 removed
        /// </summary>
        private void OnConditionRemoved(ActivationConditionData data)
        {
            _currentEffect?.ActivationConditions.Remove(data);
            RefreshConditionsList();
            UpdatePreview();
        }

        /// <summary>
        /// 原子效果 changed
        /// </summary>
        private void OnAtomicEffectChanged(AtomicEffectData data)
        {
            UpdatePreview();
        }

        /// <summary>
        /// 原子效果 removed
        /// </summary>
        private void OnAtomicEffectRemoved(AtomicEffectData data)
        {
            _currentEffect?.Effects.Remove(data);
            RefreshAtomicEffectsList();
            UpdatePreview();
        }

        /// <summary>
        /// 刷新原子效果列表
        /// </summary>
        private void RefreshAtomicEffectsList()
        {
            if (_currentEffect?.Effects == null) return;

            // 创建原子效果项
            foreach (var effect in _currentEffect.Effects)
            {
                CreateAtomicEffectItem(effect);
            }
        }

        /// <summary>
        /// 保存按钮点击
        /// </summary>
        private void OnSaveClicked()
        {
            SaveEffectAsync().Forget();
        }

        /// <summary>
        /// 异步保存效果
        /// </summary>
        private async UniTaskVoid SaveEffectAsync()
        {
            UpdateEffectFromUI();

            if (string.IsNullOrEmpty(_currentEffect?.DisplayName))
            {
                UIManager.Instance.ShowNotification("请输入效果名称");
                return;
            }

            // 生成ID（如果不存在）
            if (string.IsNullOrEmpty(_currentEffect.Id))
            {
                _currentEffect.Id = EffectDefinitionStorage.Instance.GenerateEffectId();
            }

            bool success = await EffectDefinitionStorage.Instance.SaveEffectAsync(_currentEffect);

            if (success)
            {
                UIManager.Instance.ShowNotification($"效果 '{_currentEffect.DisplayName}' 保存成功");
            }
            else
            {
                UIManager.Instance.ShowNotification("保存失败");
            }
        }

        /// <summary>
        /// 加载按钮点击
        /// </summary>
        private void OnLoadClicked()
        {
            LoadEffectsAsync().Forget();
        }

        /// <summary>
        /// 异步加载效果
        /// </summary>
        private async UniTaskVoid LoadEffectsAsync()
        {
            UIManager.Instance.ShowLoading(true);

            var effects = await EffectDefinitionStorage.Instance.LoadAllEffectsAsync();

            UIManager.Instance.ShowLoading(false);

            if (effects.Count > 0)
            {
                // 选择第一个效果加载
                SetEffect(effects[0]);
                UIManager.Instance.ShowNotification($"加载了 {effects.Count} 个效果");
            }
            else
            {
                UIManager.Instance.ShowNotification("没有已保存的效果");
            }
        }

        /// <summary>
        /// 清空按钮点击
        /// </summary>
        private void OnClearClicked()
        {
            CreateNewEffect();
            UpdateUIFromEffect();
            UIManager.Instance.ShowNotification("已清空效果");
        }

        /// <summary>
        /// 返回按钮点击
        /// </summary>
        private void OnBackClicked()
        {
            Hide();
        }

        /// <summary>
        /// 确认按钮点击
        /// </summary>
        private void OnConfirmClicked()
        {
            UpdateEffectFromUI();

            if (string.IsNullOrEmpty(_currentEffect?.DisplayName))
            {
                UIManager.Instance.ShowNotification("请输入效果名称");
                return;
            }

            // 生成ID（如果不存在）
            if (string.IsNullOrEmpty(_currentEffect.Id))
            {
                _currentEffect.Id = EffectDefinitionStorage.Instance.GenerateEffectId();
            }

            _onEffectConfirmed?.Invoke(_currentEffect);
            Hide();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 创建新效果
        /// </summary>
        private void CreateNewEffect()
        {
            _currentEffect = new EffectDefinitionData
            {
                Id = string.Empty,
                DisplayName = "新效果",
                Description = string.Empty,
                BaseSpeed = 0,
                ActivationType = 0,
                TriggerTiming = 0,
                IsOptional = false,
                Duration = 0,
                Cost = new ActivationCostData(),
                TargetSelector = new TargetSelectorData(),
                Effects = new List<AtomicEffectData>(),
                ActivationConditions = new List<ActivationConditionData>(),
                TriggerConditions = new List<ActivationConditionData>(),
                Tags = new List<string>()
            };
        }

        /// <summary>
        /// 从UI更新代价数据
        /// </summary>
        private void UpdateCostFromUI()
        {
            if (_currentEffect?.Cost == null) return;

            // 清空并重建代价列表
            _currentEffect.Cost.ElementCosts.Clear();
            foreach (var item in _elementCostItems)
            {
                if (item != null && item.ElementCostData != null)
                {
                    _currentEffect.Cost.ElementCosts.Add(item.ElementCostData);
                }
            }

            _currentEffect.Cost.ResourceCosts.Clear();
            foreach (var item in _resourceCostItems)
            {
                if (item != null && item.ResourceCostData != null)
                {
                    _currentEffect.Cost.ResourceCosts.Add(item.ResourceCostData);
                }
            }
        }



        /// <summary>
        /// 从UI更新条件数据
        /// </summary>
        private void UpdateConditionsFromUI()
        {
            if (_currentEffect?.ActivationConditions == null) return;

            _currentEffect.ActivationConditions.Clear();
            foreach (var item in _conditionItems)
            {
                if (item != null && item.ConditionData != null)
                {
                    _currentEffect.ActivationConditions.Add(item.ConditionData);
                }
            }
        }

        /// <summary>
        /// 获取触发时点显示名称
        /// </summary>
        private string GetTriggerTimingDisplayName(TriggerTiming timing)
        {
            return timing switch
            {
                TriggerTiming.On_EnterBattlefield => "入场时",
                TriggerTiming.On_LeaveBattlefield => "离场时",
                TriggerTiming.On_Death => "死亡时",
                TriggerTiming.On_TurnStart => "回合开始",
                TriggerTiming.On_TurnEnd => "回合结束",
                TriggerTiming.On_PhaseStart => "阶段开始",
                TriggerTiming.On_PhaseEnd => "阶段结束",
                TriggerTiming.On_StandbyPhase => "准备阶段",
                TriggerTiming.On_DrawPhase => "抽卡阶段",
                TriggerTiming.On_EndPhase => "结束阶段",
                TriggerTiming.On_AttackDeclare => "攻击宣言时",
                TriggerTiming.On_BlockDeclare => "阻拦时",
                TriggerTiming.On_DamageDealt => "造成伤害时",
                TriggerTiming.On_DamageTaken => "受到伤害时",
                TriggerTiming.On_CombatDamage => "战斗伤害时",
                TriggerTiming.On_CardDraw => "抽卡时",
                TriggerTiming.On_CardPlay => "使用卡牌时",
                TriggerTiming.On_CardMove => "卡牌移动时",
                TriggerTiming.On_CardToGraveyard => "卡牌入墓时",
                TriggerTiming.On_CardExile => "卡牌除外时",
                TriggerTiming.On_GameStart => "游戏开始时",
                TriggerTiming.On_GameEnd => "游戏结束时",
                _ => timing.ToString()
            };
        }

        /// <summary>
        /// 获取原子效果类型描述（无参数版本）
        /// </summary>
        private string GetEffectTypeDescription(AtomicEffectType effectType)
        {
            return GetEffectTypeDescription(effectType, 0, 0);
        }

        /// <summary>
        /// 获取原子效果类型显示名称
        /// </summary>
        private string GetEffectTypeDisplayName(AtomicEffectType effectType)
        {
            return effectType switch
            {
                // 伤害与治疗
                AtomicEffectType.DealDamage => "造成伤害",
                AtomicEffectType.DealCombatDamage => "造成战斗伤害",
                AtomicEffectType.Heal => "回复生命",
                AtomicEffectType.LifeLoss => "生命流失",

                // 卡牌移动
                AtomicEffectType.DrawCard => "抽卡",
                AtomicEffectType.DiscardCard => "弃牌",
                AtomicEffectType.MillCard => "堆墓",
                AtomicEffectType.ReturnToHand => "返回手牌",
                AtomicEffectType.PutToBattlefield => "发动",
                AtomicEffectType.Destroy => "销毁",
                AtomicEffectType.Exile => "除外",
                AtomicEffectType.ShuffleIntoDeck => "洗入牌库",
                AtomicEffectType.SearchDeck => "从牌库检索",

                // 状态变更
                AtomicEffectType.Tap => "横置",
                AtomicEffectType.Untap => "重置",
                AtomicEffectType.ModifyPower => "修改攻击力",
                AtomicEffectType.ModifyLife => "修改生命值",
                AtomicEffectType.SetPower => "设置攻击力",
                AtomicEffectType.SetLife => "设置生命值",
                AtomicEffectType.AddCardType => "添加卡牌类型",
                AtomicEffectType.RemoveCardType => "移除卡牌类型",
                AtomicEffectType.AddKeyword => "添加关键词",
                AtomicEffectType.RemoveKeyword => "移除关键词",

                // 法力相关
                AtomicEffectType.AddMana => "添加法力",
                AtomicEffectType.ConsumeMana => "消耗法力",

                // 控制相关
                AtomicEffectType.GainControl => "获得控制权",
                AtomicEffectType.PreventDamage => "防止伤害",
                AtomicEffectType.NegateEffect => "无效效果",
                AtomicEffectType.CounterSpell => "反制法术",

                // 特殊
                AtomicEffectType.CreateToken => "创建衍生物",
                AtomicEffectType.CopyCard => "复制卡牌",
                AtomicEffectType.TransformCard => "转化卡牌",
                AtomicEffectType.SwapStats => "交换属性",
                AtomicEffectType.Nullify => "无效化卡牌",

                // 红色效果
                AtomicEffectType.AoEDamage => "范围伤害",
                AtomicEffectType.SplitDamage => "分配伤害",
                AtomicEffectType.TrampleDamage => "溢出伤害",
                AtomicEffectType.DamageCannotBePrevented => "无法防止的伤害",
                AtomicEffectType.GrantHaste => "赋予敏捷",
                AtomicEffectType.GrantRush => "赋予突袭",
                AtomicEffectType.GrantDoubleStrike => "赋予双击",
                AtomicEffectType.GrantMultiAttack => "赋予多次攻击",
                AtomicEffectType.DestroyArtifact => "破坏神器",
                AtomicEffectType.DestroyRandom => "随机破坏",

                // 蓝色效果
                AtomicEffectType.CounterTargetSpell => "反制目标法术",
                AtomicEffectType.NegateActivation => "无效化发动",
                AtomicEffectType.RedirectTarget => "重定向目标",
                AtomicEffectType.DrawThenDiscard => "抽后弃",
                AtomicEffectType.ScryCards => "预见",
                AtomicEffectType.FreezePermanent => "冻结",
                AtomicEffectType.StealControl => "偷取控制权",
                AtomicEffectType.SwapController => "交换控制者",
                AtomicEffectType.BounceToTop => "弹回牌库顶",
                AtomicEffectType.BounceToBottom => "弹回牌库底",
                AtomicEffectType.CopyExact => "精确复制",

                // 绿色效果
                AtomicEffectType.RampMana => "法力加速",
                AtomicEffectType.SearchLand => "搜索地牌",
                AtomicEffectType.UntapAll => "全部重置",
                AtomicEffectType.AddCounters => "添加指示物",
                AtomicEffectType.DoubleCounters => "翻倍指示物",
                AtomicEffectType.EvolveCreature => "进化生物",
                AtomicEffectType.FightTarget => "与目标战斗",
                AtomicEffectType.GrantTrample => "赋予践踏",
                AtomicEffectType.GrantReach => "赋予阻断飞行",
                AtomicEffectType.RestoreToFullLife => "恢复满生命",
                AtomicEffectType.RemoveDebuffs => "移除减益",

                // 灰色效果
                AtomicEffectType.MoveToAnyZone => "移动到任意区域",
                AtomicEffectType.ExchangePosition => "交换位置",
                AtomicEffectType.SearchAndReveal => "检索并展示",
                AtomicEffectType.SearchAndPlay => "检索并使用",
                AtomicEffectType.TransformInto => "转化为指定卡牌",
                AtomicEffectType.MoveCard => "移动卡牌",

                // 反规则效果
                AtomicEffectType.GrantCannotBeTargeted => "赋予不可被指定",
                AtomicEffectType.GrantSpellShield => "赋予法术护盾",
                AtomicEffectType.GrantImmunity => "赋予免疫",
                AtomicEffectType.GrantUnaffected => "赋予不受影响",
                AtomicEffectType.ModifyGameRule => "修改游戏规则",
                AtomicEffectType.OverrideRestriction => "覆盖限制",

                _ => effectType.ToString()
            };
        }

        /// <summary>
        /// 获取原子效果类型描述（带参数）
        /// </summary>
        private string GetEffectTypeDescription(AtomicEffectType effectType, int value, int value2)
        {
            string desc = effectType switch
            {
                // 伤害与治疗
                AtomicEffectType.DealDamage => "对目标造成 {value} 点伤害",
                AtomicEffectType.DealCombatDamage => "对目标造成 {value} 点战斗伤害",
                AtomicEffectType.Heal => "为目标回复 {value} 点生命",
                AtomicEffectType.LifeLoss => "目标失去 {value} 点生命",

                // 卡牌移动
                AtomicEffectType.DrawCard => "抽 {value} 张卡",
                AtomicEffectType.DiscardCard => "弃掉 {value} 张牌",
                AtomicEffectType.MillCard => "从牌库顶将 {value} 张牌放入墓地",
                AtomicEffectType.ReturnToHand => "将目标返回手牌",
                AtomicEffectType.PutToBattlefield => "将目标放入战场",
                AtomicEffectType.Destroy => "销毁目标",
                AtomicEffectType.Exile => "将目标除外",
                AtomicEffectType.ShuffleIntoDeck => "将目标洗入牌库",
                AtomicEffectType.SearchDeck => "从牌库搜索 {value} 张牌",

                // 状态变更
                AtomicEffectType.Tap => "横置目标",
                AtomicEffectType.Untap => "重置目标",
                AtomicEffectType.ModifyPower => "目标攻击力 +{value}",
                AtomicEffectType.ModifyLife => "目标生命值 +{value}",
                AtomicEffectType.SetPower => "将目标攻击力设为 {value}",
                AtomicEffectType.SetLife => "将目标生命值设为 {value}",

                // 法力相关
                AtomicEffectType.AddMana => "添加 {value} 点法力",
                AtomicEffectType.ConsumeMana => "消耗 {value} 点法力",

                // 控制相关
                AtomicEffectType.GainControl => "获得目标控制权",
                AtomicEffectType.PreventDamage => "防止 {value} 点伤害",
                AtomicEffectType.CounterSpell => "反制目标法术",

                // 特殊
                AtomicEffectType.CreateToken => "创建一个衍生物",
                AtomicEffectType.CopyCard => "复制目标卡牌",

                // 额外效果
                AtomicEffectType.AoEDamage => "对所有目标造成 {value} 点伤害",
                AtomicEffectType.GrantHaste => "目标获得敏捷（可立即攻击）",
                AtomicEffectType.GrantRush => "目标获得突袭（可立即攻击随从）",
                AtomicEffectType.DrawThenDiscard => "抽 {value} 张牌，然后弃掉 {value2} 张牌",
                AtomicEffectType.ScryCards => "查看牌库顶 {value} 张牌，调整顺序",
                AtomicEffectType.FreezePermanent => "冻结目标（下回合无法横置/重置）",
                AtomicEffectType.RampMana => "从牌库搜索一张地牌放入战场",
                AtomicEffectType.AddCounters => "在目标上放置 {value} 个指示物",

                _ => GetEffectTypeDisplayName(effectType)
            };

            // 替换占位符
            desc = desc.Replace("{value}", value > 0 ? value.ToString() : "X");
            desc = desc.Replace("{value2}", value2 > 0 ? value2.ToString() : "X");

            return desc;
        }

        #endregion

        #region 显示隐藏

        protected override void OnShow()
        {
            base.OnShow();

            // 确保存储已加载
            if (!EffectDefinitionStorage.Instance.IsLoaded)
            {
                EffectDefinitionStorage.Instance.LoadAllEffectsAsync().Forget();
            }
        }

        protected override void OnHide()
        {
            base.OnHide();

            // 清理回调
            _onEffectConfirmed = null;
        }

        /// <summary>
        /// 获取效果颜色显示名称
        /// </summary>
        private string GetEffectColorDisplayName(EffectColor color)
        {
            return color switch
            {
                EffectColor.Red => "红色",
                EffectColor.Blue => "蓝色",
                EffectColor.Green => "绿色",
                EffectColor.Gray => "灰色",
                _ => color.ToString()
            };
        }

        #endregion
    }
}