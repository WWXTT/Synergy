using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;
using Cysharp.Threading.Tasks;
using cfg;
using CardCore.Data;

namespace CardCore.UI
{
    /// <summary>
    /// 效果列表面板UI
    /// 合并显示自定义效果和预制效果，支持筛选
    /// </summary>
    public class EffectListPanelUI : BaseUI
    {
        #region 序列化字段

        [Header("效果列表")]
        [SerializeField] private Transform _effectsContainer;
        [SerializeField] private GameObject _effectItemPrefab;
        [SerializeField] private ScrollRect _effectsScrollRect;

        [Header("空状态提示")]
        [SerializeField] private GameObject _emptyStatePanel;
        [SerializeField] private TextMeshProUGUI _emptyStateText;

        [Header("加载提示")]
        [SerializeField] private GameObject _loadingPanel;

        [Header("统计信息")]
        [SerializeField] private TextMeshProUGUI _totalCountText;
        [SerializeField] private TextMeshProUGUI _filteredCountText;

        [Header("排序")]
        [SerializeField] private TMP_Dropdown _sortDropdown;

        #endregion

        #region 私有字段

        /// <summary>效果项列表</summary>
        private List<EffectSelectionItemUI> _effectItems = new List<EffectSelectionItemUI>();

        /// <summary>当前筛选条件</summary>
        private EffectFilterData _currentFilter = new EffectFilterData();

        /// <summary>自定义效果列表</summary>
        private List<EffectDefinitionData> _customEffects = new List<EffectDefinitionData>();

        /// <summary>预制效果列表</summary>
        private List<PresetEffectData> _presetEffects = new List<PresetEffectData>();

        /// <summary>效果添加回调</summary>
        private Action<EffectDefinitionData> _onEffectAdded;

        /// <summary>效果预览回调</summary>
        private Action<EffectDefinitionData> _onEffectPreviewed;

        /// <summary>是否已加载</summary>
        private bool _isEffectsLoaded = false;

        #endregion

        #region 生命周期

        protected override void Initialize()
        {
            base.Initialize();

            // 初始化排序下拉菜单
            InitializeSortDropdown();

            // 绑定事件
            BindEvents();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置效果添加回调
        /// </summary>
        public void SetCallbacks(Action<EffectDefinitionData> onEffectAdded, Action<EffectDefinitionData> onEffectPreviewed = null)
        {
            _onEffectAdded = onEffectAdded;
            _onEffectPreviewed = onEffectPreviewed;
        }

        /// <summary>
        /// 应用筛选条件
        /// </summary>
        public void ApplyFilter(EffectFilterData filter)
        {
            if (filter == null) return;

            _currentFilter = filter.Clone();
            RefreshEffectList();
        }

        /// <summary>
        /// 刷新效果列表
        /// </summary>
        public async void RefreshEffectList()
        {
            ShowLoading(true);

            // 确保效果已加载
            if (!_isEffectsLoaded)
            {
                await LoadEffectsAsync();
            }

            // 清除现有项
            ClearEffectItems();

            // 获取筛选后的效果
            var filteredEffects = GetFilteredEffects();

            // 创建效果项
            foreach (var effect in filteredEffects)
            {
                CreateEffectItem(effect);
            }

            // 更新统计信息
            UpdateStatistics(filteredEffects.Count);

            // 更新空状态
            UpdateEmptyState(filteredEffects.Count == 0);

            ShowLoading(false);

            // 滚动到顶部
            if (_effectsScrollRect != null)
            {
                _effectsScrollRect.normalizedPosition = Vector2.up;
            }
        }

        /// <summary>
        /// 重新加载效果
        /// </summary>
        public async UniTask ReloadEffectsAsync()
        {
            _isEffectsLoaded = false;
            await LoadEffectsAsync();
            RefreshEffectList();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化排序下拉菜单
        /// </summary>
        private void InitializeSortDropdown()
        {
            if (_sortDropdown == null) return;

            _sortDropdown.ClearOptions();
            var options = new List<string>
            {
                "按名称排序",
                "按分类排序",
                "按速度排序",
                "按来源排序"
            };
            _sortDropdown.AddOptions(options);
            _sortDropdown.value = 0;
        }

        /// <summary>
        /// 绑定事件
        /// </summary>
        private void BindEvents()
        {
            _sortDropdown?.onValueChanged.AddListener(OnSortChanged);
        }

        /// <summary>
        /// 加载效果
        /// </summary>
        private async UniTask LoadEffectsAsync()
        {
            // 加载自定义效果
            _customEffects = await EffectDefinitionStorage.Instance.LoadAllEffectsAsync();

            // 加载预制效果
            _presetEffects = await PresetEffectLibrary.Instance.LoadFromJSON();

            _isEffectsLoaded = true;

            Debug.Log($"EffectListPanelUI: 加载了 {_customEffects.Count} 个自定义效果, {_presetEffects.Count} 个预制效果");
        }

        /// <summary>
        /// 获取筛选后的效果列表
        /// </summary>
        private List<EffectDisplayInfo> GetFilteredEffects()
        {
            var result = new List<EffectDisplayInfo>();

            // 添加自定义效果
            if (_currentFilter.ShowCustomEffects)
            {
                foreach (var effect in _customEffects)
                {
                    if (MatchesFilter(effect, false))
                    {
                        result.Add(new EffectDisplayInfo
                        {
                            EffectData = effect,
                            IsPreset = false,
                            PresetData = null
                        });
                    }
                }
            }

            // 添加预制效果
            if (_currentFilter.ShowPresetEffects)
            {
                foreach (var preset in _presetEffects)
                {
                    if (MatchesFilter(preset.EffectData, true, preset))
                    {
                        result.Add(new EffectDisplayInfo
                        {
                            EffectData = preset.EffectData,
                            IsPreset = true,
                            PresetData = preset
                        });
                    }
                }
            }

            // 排序
            ApplySorting(result);

            return result;
        }

        /// <summary>
        /// 检查效果是否匹配筛选条件
        /// </summary>
        private bool MatchesFilter(EffectDefinitionData effect, bool isPreset, PresetEffectData presetData = null)
        {
            if (effect == null) return false;

            // 按关键词搜索
            if (!string.IsNullOrEmpty(_currentFilter.SearchKeyword))
            {
                string keyword = _currentFilter.SearchKeyword.ToLower();
                if ((effect.DisplayName?.ToLower().Contains(keyword) != true) &&
                    (effect.Description?.ToLower().Contains(keyword) != true))
                {
                    return false;
                }
            }

            // 按元素类型筛选
            if (_currentFilter.SelectedManaTypes != null && _currentFilter.SelectedManaTypes.Count > 0)
            {
                var effectManaTypes = GetEffectManaTypes(effect);
                if (!effectManaTypes.Any(mt => _currentFilter.SelectedManaTypes.Contains(mt)))
                {
                    return false;
                }
            }

            // 按功能分类筛选
            if (_currentFilter.SelectedCategories != null && _currentFilter.SelectedCategories.Count > 0)
            {
                if (isPreset && presetData != null)
                {
                    if (!_currentFilter.SelectedCategories.Contains(presetData.GetCategory()))
                    {
                        return false;
                    }
                }
                else
                {
                    // 自定义效果通过标签或效果类型推断分类
                    var inferredCategory = InferCategoryFromEffect(effect);
                    if (!_currentFilter.SelectedCategories.Contains(inferredCategory))
                    {
                        return false;
                    }
                }
            }

            // 按发动速度筛选
            if (_currentFilter.SelectedSpeeds != null && _currentFilter.SelectedSpeeds.Count > 0)
            {
                var speed = (EffectSpeed)effect.ActivationType;
                if (!_currentFilter.SelectedSpeeds.Contains(speed))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取效果的元素类型列表
        /// </summary>
        private List<ManaType> GetEffectManaTypes(EffectDefinitionData effect)
        {
            var result = new List<ManaType>();

            if (effect.Cost?.ElementCosts != null)
            {
                foreach (var elementCost in effect.Cost.ElementCosts)
                {
                    result.Add((ManaType)elementCost.ManaType);
                }
            }

            if (result.Count == 0)
            {
                result.Add(ManaType.灰色);
            }

            return result;
        }

        /// <summary>
        /// 从效果推断功能分类
        /// </summary>
        private EffectCategory InferCategoryFromEffect(EffectDefinitionData effect)
        {
            if (effect.Effects == null || effect.Effects.Count == 0)
            {
                return EffectCategory.Special;
            }

            // 根据第一个原子效果类型推断分类
            var atomicType = (AtomicEffectType)effect.Effects[0].Type;
            return atomicType switch
            {
                AtomicEffectType.DealDamage => EffectCategory.Damage,
                AtomicEffectType.DealCombatDamage => EffectCategory.Damage,
                AtomicEffectType.Heal => EffectCategory.Heal,
                AtomicEffectType.DrawCard => EffectCategory.Draw,
                AtomicEffectType.Destroy => EffectCategory.Destroy,
                AtomicEffectType.Exile => EffectCategory.Destroy,
                AtomicEffectType.ModifyPower => EffectCategory.Buff,
                AtomicEffectType.ModifyLife => EffectCategory.Buff,
                AtomicEffectType.Tap => EffectCategory.Control,
                AtomicEffectType.Untap => EffectCategory.Control,
                AtomicEffectType.DiscardCard => EffectCategory.Move,
                AtomicEffectType.MillCard => EffectCategory.Move,
                AtomicEffectType.ReturnToHand => EffectCategory.Move,
                _ => EffectCategory.Special
            };
        }

        /// <summary>
        /// 应用排序
        /// </summary>
        private void ApplySorting(List<EffectDisplayInfo> effects)
        {
            if (_sortDropdown == null) return;

            int sortIndex = _sortDropdown.value;

            switch (sortIndex)
            {
                case 0: // 按名称
                    effects.Sort((a, b) => string.Compare(a.EffectData.DisplayName, b.EffectData.DisplayName, StringComparison.Ordinal));
                    break;
                case 1: // 按分类
                    effects.Sort((a, b) =>
                    {
                        var catA = a.IsPreset ? a.PresetData.GetCategory() : InferCategoryFromEffect(a.EffectData);
                        var catB = b.IsPreset ? b.PresetData.GetCategory() : InferCategoryFromEffect(b.EffectData);
                        return catA.CompareTo(catB);
                    });
                    break;
                case 2: // 按速度
                    effects.Sort((a, b) => a.EffectData.ActivationType.CompareTo(b.EffectData.ActivationType));
                    break;
                case 3: // 按来源
                    effects.Sort((a, b) => a.IsPreset.CompareTo(b.IsPreset));
                    break;
            }
        }

        /// <summary>
        /// 创建效果项
        /// </summary>
        private void CreateEffectItem(EffectDisplayInfo effectInfo)
        {
            if (_effectItemPrefab == null || _effectsContainer == null) return;

            var itemObj = Instantiate(_effectItemPrefab, _effectsContainer);
            var itemUI = itemObj.GetComponent<EffectSelectionItemUI>();

            if (itemUI != null)
            {
                if (effectInfo.IsPreset)
                {
                    itemUI.SetPresetEffect(effectInfo.PresetData, OnEffectAddClicked, OnEffectPreviewClicked);
                }
                else
                {
                    itemUI.SetCustomEffect(effectInfo.EffectData, OnEffectAddClicked, OnEffectPreviewClicked);
                }

                _effectItems.Add(itemUI);
            }
        }

        /// <summary>
        /// 清除效果项
        /// </summary>
        private void ClearEffectItems()
        {
            foreach (var item in _effectItems)
            {
                if (item != null && item.gameObject != null)
                {
                    Destroy(item.gameObject);
                }
            }
            _effectItems.Clear();
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics(int filteredCount)
        {
            if (_totalCountText != null)
            {
                int total = _customEffects.Count + _presetEffects.Count;
                _totalCountText.text = $"共 {total} 个效果";
            }

            if (_filteredCountText != null)
            {
                _filteredCountText.text = $"筛选: {filteredCount} 个";
            }
        }

        /// <summary>
        /// 更新空状态显示
        /// </summary>
        private void UpdateEmptyState(bool isEmpty)
        {
            if (_emptyStatePanel != null)
            {
                _emptyStatePanel.SetActive(isEmpty);
            }

            if (_emptyStateText != null && isEmpty)
            {
                if (_currentFilter.IsEmpty())
                {
                    _emptyStateText.text = "暂无效果";
                }
                else
                {
                    _emptyStateText.text = "没有符合条件的效果";
                }
            }
        }

        /// <summary>
        /// 显示/隐藏加载提示
        /// </summary>
        private void ShowLoading(bool show)
        {
            if (_loadingPanel != null)
            {
                _loadingPanel.SetActive(show);
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 效果添加点击
        /// </summary>
        private void OnEffectAddClicked(EffectDefinitionData effectData)
        {
            _onEffectAdded?.Invoke(effectData);
        }

        /// <summary>
        /// 效果预览点击
        /// </summary>
        private void OnEffectPreviewClicked(EffectDefinitionData effectData)
        {
            _onEffectPreviewed?.Invoke(effectData);
        }

        /// <summary>
        /// 排序变更
        /// </summary>
        private void OnSortChanged(int index)
        {
            RefreshEffectList();
        }

        #endregion

        #region 显示隐藏

        protected override void OnShow()
        {
            base.OnShow();
            RefreshEffectList();
        }

        #endregion

        #region 内部类

        /// <summary>
        /// 效果显示信息
        /// </summary>
        private class EffectDisplayInfo
        {
            public EffectDefinitionData EffectData;
            public bool IsPreset;
            public PresetEffectData PresetData;
        }

        #endregion
    }
}
