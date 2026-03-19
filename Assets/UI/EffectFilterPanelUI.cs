using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;
using cfg;
using CardCore.Data;

namespace CardCore.UI
{
    /// <summary>
    /// 效果筛选面板UI
    /// 支持按元素类型、功能分类、发动速度进行多选筛选
    /// </summary>
    public class EffectFilterPanelUI : BaseUI
    {
        #region 序列化字段

        [Header("元素类型筛选")]
        [SerializeField] private Transform _manaTypeContainer;
        [SerializeField] private GameObject _manaTypeTogglePrefab;
        [SerializeField] private Toggle _grayToggle;
        [SerializeField] private Toggle _redToggle;
        [SerializeField] private Toggle _blueToggle;
        [SerializeField] private Toggle _greenToggle;
        [SerializeField] private Toggle _whiteToggle;
        [SerializeField] private Toggle _blackToggle;

        [Header("功能分类筛选")]
        [SerializeField] private Transform _categoryContainer;
        [SerializeField] private TMP_Dropdown _categoryDropdown;

        [Header("发动速度筛选")]
        [SerializeField] private Transform _speedContainer;
        [SerializeField] private TMP_Dropdown _speedDropdown;

        [Header("搜索")]
        [SerializeField] private TMP_InputField _searchInput;

        [Header("来源筛选")]
        [SerializeField] private Toggle _showCustomToggle;
        [SerializeField] private Toggle _showPresetToggle;

        [Header("操作按钮")]
        [SerializeField] private Button _resetFilterButton;
        [SerializeField] private Button _applyFilterButton;

        #endregion

        #region 私有字段

        /// <summary>当前筛选数据</summary>
        private EffectFilterData _currentFilter = new EffectFilterData();

        /// <summary>元素类型Toggle映射</summary>
        private Dictionary<ManaType, Toggle> _manaTypeToggles = new Dictionary<ManaType, Toggle>();

        /// <summary>筛选变更事件</summary>
        public event Action<EffectFilterData> OnFilterChanged;

        #endregion

        #region 生命周期

        protected override void Initialize()
        {
            base.Initialize();

            // 初始化元素类型Toggle
            InitializeManaTypeToggles();

            // 初始化下拉菜单
            InitializeDropdowns();

            // 绑定事件
            BindEvents();

            // 初始化解锁状态
            RefreshUnlockState();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取当前筛选条件
        /// </summary>
        public EffectFilterData GetFilter()
        {
            return _currentFilter.Clone();
        }

        /// <summary>
        /// 设置筛选条件
        /// </summary>
        public void SetFilter(EffectFilterData filter)
        {
            if (filter == null) return;

            _currentFilter = filter.Clone();
            UpdateUIFromFilter();
            OnFilterChanged?.Invoke(_currentFilter);
        }

        /// <summary>
        /// 重置筛选条件
        /// </summary>
        public void ResetFilter()
        {
            _currentFilter.Reset();
            UpdateUIFromFilter();
            OnFilterChanged?.Invoke(_currentFilter);
        }

        /// <summary>
        /// 刷新元素解锁状态
        /// </summary>
        public void RefreshUnlockState()
        {
            // 从ElementUnlockManager获取解锁状态
            var unlockedTypes = ElementUnlockManager.Instance.GetAvailableManaTypes();
            _currentFilter.UnlockedManaTypes = new List<ManaType>(unlockedTypes);

            // 更新UI显示
            foreach (var kvp in _manaTypeToggles)
            {
                bool isUnlocked = unlockedTypes.Contains(kvp.Key);
                kvp.Value.gameObject.SetActive(isUnlocked);

                // 可选：显示锁定状态（如果有解锁需求）
                // var lockIcon = kvp.Value.transform.Find("LockIcon");
                // if (lockIcon != null) lockIcon.gameObject.SetActive(!isUnlocked);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化元素类型Toggle
        /// </summary>
        private void InitializeManaTypeToggles()
        {
            _manaTypeToggles.Clear();

            // 映射预设的Toggle
            if (_grayToggle != null) _manaTypeToggles[ManaType.灰色] = _grayToggle;
            if (_redToggle != null) _manaTypeToggles[ManaType.红色] = _redToggle;
            if (_blueToggle != null) _manaTypeToggles[ManaType.蓝色] = _blueToggle;
            if (_greenToggle != null) _manaTypeToggles[ManaType.绿色] = _greenToggle;
            if (_whiteToggle != null) _manaTypeToggles[ManaType.白色] = _whiteToggle;
            if (_blackToggle != null) _manaTypeToggles[ManaType.黑色] = _blackToggle;

            // 绑定事件
            foreach (var kvp in _manaTypeToggles)
            {
                var manaType = kvp.Key;
                kvp.Value.onValueChanged.AddListener((isOn) => OnManaTypeToggleChanged(manaType, isOn));
            }
        }

        /// <summary>
        /// 初始化下拉菜单
        /// </summary>
        private void InitializeDropdowns()
        {
            // 功能分类下拉菜单
            if (_categoryDropdown != null)
            {
                _categoryDropdown.ClearOptions();
                var options = new List<string> { "全部分类" };
                foreach (EffectCategory category in Enum.GetValues(typeof(EffectCategory)))
                {
                    options.Add(GetCategoryDisplayName(category));
                }
                _categoryDropdown.AddOptions(options);
                _categoryDropdown.value = 0;
            }

            // 发动速度下拉菜单
            if (_speedDropdown != null)
            {
                _speedDropdown.ClearOptions();
                var options = new List<string> { "全部速度", "强制诱发", "可选诱发", "自由时点" };
                _speedDropdown.AddOptions(options);
                _speedDropdown.value = 0;
            }
        }

        /// <summary>
        /// 绑定事件
        /// </summary>
        private void BindEvents()
        {
            // 下拉菜单事件
            _categoryDropdown?.onValueChanged.AddListener(OnCategoryDropdownChanged);
            _speedDropdown?.onValueChanged.AddListener(OnSpeedDropdownChanged);

            // 搜索输入事件
            _searchInput?.onValueChanged.AddListener(OnSearchInputChanged);

            // 来源筛选事件
            _showCustomToggle?.onValueChanged.AddListener(OnShowCustomToggleChanged);
            _showPresetToggle?.onValueChanged.AddListener(OnShowPresetToggleChanged);

            // 按钮事件
            _resetFilterButton?.AddClickListener(OnResetFilterClicked);
            _applyFilterButton?.AddClickListener(OnApplyFilterClicked);
        }

        /// <summary>
        /// 从筛选数据更新UI
        /// </summary>
        private void UpdateUIFromFilter()
        {
            // 更新元素类型Toggle
            foreach (var kvp in _manaTypeToggles)
            {
                kvp.Value.isOn = _currentFilter.SelectedManaTypes.Contains(kvp.Key);
            }

            // 更新下拉菜单
            if (_categoryDropdown != null)
            {
                _categoryDropdown.value = 0; // 重置为"全部"
            }

            if (_speedDropdown != null)
            {
                _speedDropdown.value = 0; // 重置为"全部"
            }

            // 更新搜索框
            if (_searchInput != null)
            {
                _searchInput.text = _currentFilter.SearchKeyword;
            }

            // 更新来源Toggle
            if (_showCustomToggle != null)
            {
                _showCustomToggle.isOn = _currentFilter.ShowCustomEffects;
            }

            if (_showPresetToggle != null)
            {
                _showPresetToggle.isOn = _currentFilter.ShowPresetEffects;
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 元素类型Toggle变更
        /// </summary>
        private void OnManaTypeToggleChanged(ManaType manaType, bool isOn)
        {
            if (isOn)
            {
                if (!_currentFilter.SelectedManaTypes.Contains(manaType))
                {
                    _currentFilter.SelectedManaTypes.Add(manaType);
                }
            }
            else
            {
                _currentFilter.SelectedManaTypes.Remove(manaType);
            }

            // 实时触发筛选
            OnFilterChanged?.Invoke(_currentFilter);
        }

        /// <summary>
        /// 功能分类下拉菜单变更
        /// </summary>
        private void OnCategoryDropdownChanged(int index)
        {
            _currentFilter.SelectedCategories.Clear();

            if (index > 0)
            {
                // index - 1 对应枚举值
                var category = (EffectCategory)(index - 1);
                _currentFilter.SelectedCategories.Add(category);
            }

            OnFilterChanged?.Invoke(_currentFilter);
        }

        /// <summary>
        /// 发动速度下拉菜单变更
        /// </summary>
        private void OnSpeedDropdownChanged(int index)
        {
            _currentFilter.SelectedSpeeds.Clear();

            if (index > 0)
            {
                // index - 1 对应枚举值
                var speed = (EffectSpeed)(index - 1);
                _currentFilter.SelectedSpeeds.Add(speed);
            }

            OnFilterChanged?.Invoke(_currentFilter);
        }

        /// <summary>
        /// 搜索输入变更
        /// </summary>
        private void OnSearchInputChanged(string value)
        {
            _currentFilter.SearchKeyword = value?.Trim() ?? string.Empty;
            OnFilterChanged?.Invoke(_currentFilter);
        }

        /// <summary>
        /// 显示自定义效果Toggle变更
        /// </summary>
        private void OnShowCustomToggleChanged(bool isOn)
        {
            _currentFilter.ShowCustomEffects = isOn;
            OnFilterChanged?.Invoke(_currentFilter);
        }

        /// <summary>
        /// 显示预制效果Toggle变更
        /// </summary>
        private void OnShowPresetToggleChanged(bool isOn)
        {
            _currentFilter.ShowPresetEffects = isOn;
            OnFilterChanged?.Invoke(_currentFilter);
        }

        /// <summary>
        /// 重置筛选按钮点击
        /// </summary>
        private void OnResetFilterClicked()
        {
            ResetFilter();
        }

        /// <summary>
        /// 应用筛选按钮点击
        /// </summary>
        private void OnApplyFilterClicked()
        {
            OnFilterChanged?.Invoke(_currentFilter);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取功能分类显示名称
        /// </summary>
        private string GetCategoryDisplayName(EffectCategory category)
        {
            return category switch
            {
                EffectCategory.Damage => "伤害",
                EffectCategory.Heal => "治疗",
                EffectCategory.Draw => "抽卡",
                EffectCategory.Control => "控制",
                EffectCategory.Destroy => "破坏",
                EffectCategory.Buff => "增益",
                EffectCategory.Debuff => "减益",
                EffectCategory.Move => "移动",
                EffectCategory.CostReduction => "降费",
                EffectCategory.Special => "特殊",
                _ => category.ToString()
            };
        }

        #endregion

        #region 显示隐藏

        protected override void OnShow()
        {
            base.OnShow();
            RefreshUnlockState();
        }

        #endregion
    }
}
