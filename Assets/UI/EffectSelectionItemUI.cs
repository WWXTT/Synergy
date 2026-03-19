using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;
using cfg;
using CardCore.Data;

namespace CardCore.UI
{
    /// <summary>
    /// 效果选择项UI
    /// 显示单个效果的信息，用于效果列表中
    /// </summary>
    public class EffectSelectionItemUI : MonoBehaviour
    {
        #region 序列化字段

        [Header("基本信息")]
        [SerializeField] private TextMeshProUGUI _effectNameText;
        [SerializeField] private TextMeshProUGUI _effectDescriptionText;

        [Header("来源标签")]
        [SerializeField] private Image _sourceTagBackground;
        [SerializeField] private TextMeshProUGUI _sourceTagText;

        [Header("元素类型图标")]
        [SerializeField] private Image _manaTypeIcon;
        [SerializeField] private TextMeshProUGUI _manaTypeText;

        [Header("功能分类标签")]
        [SerializeField] private Image _categoryTagBackground;
        [SerializeField] private TextMeshProUGUI _categoryTagText;

        [Header("发动速度")]
        [SerializeField] private TextMeshProUGUI _speedText;

        [Header("操作按钮")]
        [SerializeField] private Button _addButton;
        [SerializeField] private Button _previewButton;

        #endregion

        #region 颜色配置

        [Header("来源颜色配置")]
        [SerializeField] private Color _customEffectColor = new Color(0.27f, 0.53f, 1f); // 蓝色
        [SerializeField] private Color _presetEffectColor = new Color(0.27f, 0.67f, 0.27f); // 绿色

        [Header("元素颜色配置")]
        [SerializeField] private Color _grayManaColor = new Color(0.61f, 0.66f, 0.66f);
        [SerializeField] private Color _redManaColor = new Color(1f, 0.27f, 0.27f);
        [SerializeField] private Color _blueManaColor = new Color(0.27f, 0.53f, 1f);
        [SerializeField] private Color _greenManaColor = new Color(0.27f, 0.67f, 0.27f);
        [SerializeField] private Color _whiteManaColor = new Color(1f, 1f, 1f);
        [SerializeField] private Color _blackManaColor = new Color(0.2f, 0.2f, 0.2f);

        #endregion

        #region 私有字段

        /// <summary>效果数据</summary>
        private EffectDefinitionData _effectData;

        /// <summary>预制效果数据（如果是预制效果）</summary>
        private PresetEffectData _presetData;

        /// <summary>是否为预制效果</summary>
        private bool _isPreset;

        /// <summary>添加回调</summary>
        private Action<EffectDefinitionData> _onAddClicked;

        /// <summary>预览回调</summary>
        private Action<EffectDefinitionData> _onPreviewClicked;

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置自定义效果数据
        /// </summary>
        public void SetCustomEffect(EffectDefinitionData effectData, Action<EffectDefinitionData> onAddClicked, Action<EffectDefinitionData> onPreviewClicked = null)
        {
            _effectData = effectData;
            _presetData = null;
            _isPreset = false;
            _onAddClicked = onAddClicked;
            _onPreviewClicked = onPreviewClicked;

            UpdateDisplay();
        }

        /// <summary>
        /// 设置预制效果数据
        /// </summary>
        public void SetPresetEffect(PresetEffectData presetData, Action<EffectDefinitionData> onAddClicked, Action<EffectDefinitionData> onPreviewClicked = null)
        {
            _presetData = presetData;
            _effectData = presetData?.EffectData;
            _isPreset = true;
            _onAddClicked = onAddClicked;
            _onPreviewClicked = onPreviewClicked;

            UpdateDisplay();
        }

        /// <summary>
        /// 获取效果数据
        /// </summary>
        public EffectDefinitionData GetEffectData()
        {
            return _effectData;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 更新显示
        /// </summary>
        private void UpdateDisplay()
        {
            if (_effectData == null) return;

            // 更新名称
            if (_effectNameText != null)
            {
                _effectNameText.text = _effectData.DisplayName ?? "未命名效果";
            }

            // 更新描述
            if (_effectDescriptionText != null)
            {
                _effectDescriptionText.text = _effectData.Description ?? "";
            }

            // 更新来源标签
            UpdateSourceTag();

            // 更新元素类型
            UpdateManaType();

            // 更新功能分类
            UpdateCategory();

            // 更新发动速度
            UpdateSpeed();

            // 绑定按钮事件
            BindButtonEvents();
        }

        /// <summary>
        /// 更新来源标签
        /// </summary>
        private void UpdateSourceTag()
        {
            if (_sourceTagBackground == null || _sourceTagText == null) return;

            if (_isPreset)
            {
                _sourceTagBackground.color = _presetEffectColor;
                _sourceTagText.text = "预制";
            }
            else
            {
                _sourceTagBackground.color = _customEffectColor;
                _sourceTagText.text = "自定义";
            }
        }

        /// <summary>
        /// 更新元素类型显示
        /// </summary>
        private void UpdateManaType()
        {
            if (_effectData?.Cost?.ElementCosts == null || _effectData.Cost.ElementCosts.Count == 0)
            {
                if (_manaTypeIcon != null) _manaTypeIcon.color = _grayManaColor;
                if (_manaTypeText != null) _manaTypeText.text = "灰";
                return;
            }

            var elementCost = _effectData.Cost.ElementCosts[0];
            var manaType = (ManaType)elementCost.ManaType;

            if (_manaTypeIcon != null)
            {
                _manaTypeIcon.color = GetManaTypeColor(manaType);
            }

            if (_manaTypeText != null)
            {
                _manaTypeText.text = GetManaTypeShortName(manaType);
            }
        }

        /// <summary>
        /// 更新功能分类
        /// </summary>
        private void UpdateCategory()
        {
            if (_categoryTagText == null) return;

            string categoryName = "特殊";

            if (_presetData != null)
            {
                categoryName = _presetData.GetCategoryDisplayName();
            }

            _categoryTagText.text = categoryName;
        }

        /// <summary>
        /// 更新发动速度
        /// </summary>
        private void UpdateSpeed()
        {
            if (_speedText == null) return;

            var speed = (EffectSpeed)_effectData.ActivationType;
            _speedText.text = speed switch
            {
                EffectSpeed.强制诱发 => "强制",
                EffectSpeed.可选诱发 => "可选",
                EffectSpeed.自由时点 => "自由",
                _ => speed.ToString()
            };
        }

        /// <summary>
        /// 绑定按钮事件
        /// </summary>
        private void BindButtonEvents()
        {
            if (_addButton != null)
            {
                _addButton.onClick.RemoveAllListeners();
                _addButton.onClick.AddListener(() => _onAddClicked?.Invoke(_effectData));
            }

            if (_previewButton != null)
            {
                _previewButton.onClick.RemoveAllListeners();
                _previewButton.onClick.AddListener(() => _onPreviewClicked?.Invoke(_effectData));
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取元素类型颜色
        /// </summary>
        private Color GetManaTypeColor(ManaType manaType)
        {
            return manaType switch
            {
                ManaType.灰色 => _grayManaColor,
                ManaType.红色 => _redManaColor,
                ManaType.蓝色 => _blueManaColor,
                ManaType.绿色 => _greenManaColor,
                ManaType.白色 => _whiteManaColor,
                ManaType.黑色 => _blackManaColor,
                _ => Color.white
            };
        }

        /// <summary>
        /// 获取元素类型简称
        /// </summary>
        private string GetManaTypeShortName(ManaType manaType)
        {
            return manaType switch
            {
                ManaType.灰色 => "灰",
                ManaType.红色 => "红",
                ManaType.蓝色 => "蓝",
                ManaType.绿色 => "绿",
                ManaType.白色 => "白",
                ManaType.黑色 => "黑",
                _ => "?"
            };
        }

        #endregion
    }
}
