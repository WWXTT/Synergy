using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;

namespace CardCore.UI
{
    /// <summary>
    /// 效果代价项 UI
    /// </summary>
    public class EffectCostItemUI : MonoBehaviour
    {
        [Header("通用显示")]
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Button _editButton;
        [SerializeField] private Button _removeButton;

        [Header("元素代价编辑")]
        [SerializeField] private GameObject _elementCostEditor;
        [SerializeField] private TMP_Dropdown _manaTypeDropdown;
        [SerializeField] private TMP_InputField _amountInput;

        [Header("资源代价编辑")]
        [SerializeField] private GameObject _resourceCostEditor;
        [SerializeField] private TMP_Dropdown _fromZoneDropdown;
        [SerializeField] private TMP_Dropdown _toZoneDropdown;
        [SerializeField] private TMP_InputField _countInput;

        /// <summary>元素代价数据</summary>
        public ElementCostData ElementCostData { get; private set; }

        /// <summary>资源代价数据</summary>
        public ResourceCostData ResourceCostData { get; private set; }

        /// <summary>是否为元素代价</summary>
        private bool _isElementCost;

        /// <summary>改变回调</summary>
        private Action<ElementCostData> _onElementChanged;
        private Action<ResourceCostData> _onResourceChanged;

        /// <summary>移除回调</summary>
        private Action<ElementCostData> _onElementRemoved;
        private Action<ResourceCostData> _onResourceRemoved;

        private void Awake()
        {
            InitializeDropdowns();

            _editButton?.AddClickListener(OnEditClicked);
            _removeButton?.AddClickListener(OnRemoveClicked);

            _manaTypeDropdown?.onValueChanged.AddListener(OnElementDataChanged);
            _amountInput?.onValueChanged.AddListener(OnElementDataChanged);

            _fromZoneDropdown?.onValueChanged.AddListener(OnResourceDataChanged);
            _toZoneDropdown?.onValueChanged.AddListener(OnResourceDataChanged);
            _countInput?.onValueChanged.AddListener(OnResourceDataChanged);
        }

        /// <summary>
        /// 初始化下拉菜单
        /// </summary>
        private void InitializeDropdowns()
        {
            if (_manaTypeDropdown != null)
            {
                _manaTypeDropdown.ClearOptions();
                foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
                {
                    _manaTypeDropdown.options.Add(new TMP_Dropdown.OptionData(type.ToString()));
                }
                _manaTypeDropdown.RefreshShownValue();
            }

            if (_fromZoneDropdown != null)
            {
                _fromZoneDropdown.ClearOptions();
                foreach (ResourceZone zone in Enum.GetValues(typeof(ResourceZone)))
                {
                    _fromZoneDropdown.options.Add(new TMP_Dropdown.OptionData(GetResourceZoneDisplayName(zone)));
                }
                _fromZoneDropdown.RefreshShownValue();
            }

            if (_toZoneDropdown != null)
            {
                _toZoneDropdown.ClearOptions();
                foreach (DestinationZone zone in Enum.GetValues(typeof(DestinationZone)))
                {
                    _toZoneDropdown.options.Add(new TMP_Dropdown.OptionData(GetDestinationZoneDisplayName(zone)));
                }
                _toZoneDropdown.RefreshShownValue();
            }
        }

        /// <summary>
        /// 设置元素代价
        /// </summary>
        public void SetElementCost(ElementCostData data, Action<ElementCostData> onChanged, Action<ElementCostData> onRemoved)
        {
            _isElementCost = true;
            ElementCostData = data;
            _onElementChanged = onChanged;
            _onElementRemoved = onRemoved;

            if (_elementCostEditor != null) _elementCostEditor.SetActive(true);
            if (_resourceCostEditor != null) _resourceCostEditor.SetActive(false);

            UpdateElementUI();
        }

        /// <summary>
        /// 设置资源代价
        /// </summary>
        public void SetResourceCost(ResourceCostData data, Action<ResourceCostData> onChanged, Action<ResourceCostData> onRemoved)
        {
            _isElementCost = false;
            ResourceCostData = data;
            _onResourceChanged = onChanged;
            _onResourceRemoved = onRemoved;

            if (_elementCostEditor != null) _elementCostEditor.SetActive(false);
            if (_resourceCostEditor != null) _resourceCostEditor.SetActive(true);

            UpdateResourceUI();
        }

        /// <summary>
        /// 更新元素代价UI
        /// </summary>
        private void UpdateElementUI()
        {
            if (ElementCostData == null) return;

            if (_manaTypeDropdown != null)
                _manaTypeDropdown.value = ElementCostData.ManaType;

            if (_amountInput != null)
                _amountInput.text = ElementCostData.Amount.ToString();

            UpdateDescription();
        }

        /// <summary>
        /// 更新资源代价UI
        /// </summary>
        private void UpdateResourceUI()
        {
            if (ResourceCostData == null) return;

            if (_fromZoneDropdown != null)
                _fromZoneDropdown.value = ResourceCostData.FromZone;

            if (_toZoneDropdown != null)
                _toZoneDropdown.value = ResourceCostData.ToZone;

            if (_countInput != null)
                _countInput.text = ResourceCostData.Count.ToString();

            UpdateDescription();
        }

        /// <summary>
        /// 更新描述
        /// </summary>
        private void UpdateDescription()
        {
            if (_descriptionText == null) return;

            if (_isElementCost && ElementCostData != null)
            {
                var cost = ElementCostData.ToElementCost();
                _descriptionText.text = cost.GetDescription();
            }
            else if (!_isElementCost && ResourceCostData != null)
            {
                var cost = ResourceCostData.ToResourceCost();
                _descriptionText.text = cost.GetDescription();
            }
        }

        #region 事件处理

        private void OnElementDataChanged(int value) => OnElementDataChanged("");
        private void OnElementDataChanged(string value)
        {
            if (ElementCostData == null) return;

            ElementCostData.ManaType = _manaTypeDropdown.value;

            if (int.TryParse(_amountInput.text, out int amount))
                ElementCostData.Amount = amount;

            UpdateDescription();
            _onElementChanged?.Invoke(ElementCostData);
        }

        private void OnResourceDataChanged(int value) => OnResourceDataChanged("");
        private void OnResourceDataChanged(string value)
        {
            if (ResourceCostData == null) return;

            ResourceCostData.FromZone = _fromZoneDropdown.value;
            ResourceCostData.ToZone = _toZoneDropdown.value;

            if (int.TryParse(_countInput.text, out int count))
                ResourceCostData.Count = count;

            UpdateDescription();
            _onResourceChanged?.Invoke(ResourceCostData);
        }

        private void OnEditClicked()
        {
            // 内联编辑已实现
        }

        private void OnRemoveClicked()
        {
            if (_isElementCost)
                _onElementRemoved?.Invoke(ElementCostData);
            else
                _onResourceRemoved?.Invoke(ResourceCostData);
        }

        #endregion

        #region 显示名称

        private string GetResourceZoneDisplayName(ResourceZone zone)
        {
            return zone switch
            {
                ResourceZone.Hand => "手牌",
                ResourceZone.Battlefield => "场上",
                ResourceZone.ExtraDeck => "额外卡组",
                ResourceZone.Graveyard => "墓地",
                _ => zone.ToString()
            };
        }

        private string GetDestinationZoneDisplayName(DestinationZone zone)
        {
            return zone switch
            {
                DestinationZone.Graveyard => "墓地",
                DestinationZone.Exile => "除外",
                _ => zone.ToString()
            };
        }

        #endregion
    }
}