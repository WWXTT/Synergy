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
    /// 效果预览项 UI
    /// 用于在卡牌编辑器中显示效果列表项
    /// </summary>
    public class EffectPreviewItemUI : MonoBehaviour
    {
        [Header("基本信息显示")]
        [SerializeField] private TMP_Text _effectNameText;
        [SerializeField] private TMP_Text _triggerTimingText;
        [SerializeField] private TMP_Text _descriptionText;

        [Header("图标")]
        [SerializeField] private Image _speedIcon;
        [SerializeField] private Image _typeIcon;

        [Header("按钮")]
        [SerializeField] private Button _editButton;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private Button _expandButton;

        [Header("详细信息面板")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private TMP_Text _detailText;

        /// <summary>效果数据</summary>
        private EffectDefinitionData _effectData;

        /// <summary>编辑回调</summary>
        private Action<EffectDefinitionData> _onEdit;

        /// <summary>删除回调</summary>
        private Action<EffectDefinitionData> _onDelete;

        /// <summary>是否展开</summary>
        private bool _isExpanded = false;

        /// <summary>
        /// 设置效果数据
        /// </summary>
        /// <param name="data">效果数据</param>
        /// <param name="onEdit">编辑回调</param>
        /// <param name="onDelete">删除回调</param>
        public void SetEffect(EffectDefinitionData data, Action<EffectDefinitionData> onEdit, Action<EffectDefinitionData> onDelete)
        {
            _effectData = data;
            _onEdit = onEdit;
            _onDelete = onDelete;

            UpdateUI();
        }

        /// <summary>
        /// 获取效果数据
        /// </summary>
        public EffectDefinitionData GetEffect()
        {
            return _effectData;
        }

        private void Awake()
        {
            _editButton?.AddClickListener(OnEditClicked);
            _deleteButton?.AddClickListener(OnDeleteClicked);
            _expandButton?.AddClickListener(OnExpandClicked);

            if (_detailPanel != null)
            {
                _detailPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 更新UI
        /// </summary>
        public void UpdateUI()
        {
            if (_effectData == null) return;

            // 更新名称
            if (_effectNameText != null)
            {
                _effectNameText.text = string.IsNullOrEmpty(_effectData.DisplayName)
                    ? "未命名效果"
                    : _effectData.DisplayName;
            }

            // 更新触发时点
            if (_triggerTimingText != null)
            {
                _triggerTimingText.text = GetTriggerTimingShortName((TriggerTiming)_effectData.TriggerTiming);
            }

            // 更新描述
            if (_descriptionText != null)
            {
                _descriptionText.text = string.IsNullOrEmpty(_effectData.Description)
                    ? "无描述"
                    : _effectData.Description;
            }

            // 更新速度图标颜色
            if (_speedIcon != null)
            {
                _speedIcon.color = GetSpeedColor(_effectData.BaseSpeed);
            }

            // 更新类型图标颜色
            if (_typeIcon != null)
            {
                _typeIcon.color = GetActivationTypeColor((EffectActivationType)_effectData.ActivationType);
            }

            // 更新详细信息
            if (_detailText != null)
            {
                _detailText.text = GetDetailedDescription();
            }
        }

        /// <summary>
        /// 获取详细描述
        /// </summary>
        private string GetDetailedDescription()
        {
            if (_effectData == null) return string.Empty;

            var sb = new System.Text.StringBuilder();

            // 基本信息
            sb.AppendLine($"ID: {_effectData.Id}");
            sb.AppendLine($"速度: {_effectData.BaseSpeed}");
            sb.AppendLine($"发动类型: {(EffectActivationType)_effectData.ActivationType}");
            sb.AppendLine();

            // 目标
            if (_effectData.TargetSelector != null)
            {
                sb.AppendLine($"目标: {_effectData.TargetSelector.GetDescription()}");
            }

            // 代价
            if (_effectData.Cost != null && !_effectData.Cost.IsEmpty)
            {
                sb.AppendLine($"代价: {_effectData.Cost.GetDescription()}");
            }

            // 原子效果
            if (_effectData.Effects != null && _effectData.Effects.Count > 0)
            {
                sb.AppendLine("效果:");
                foreach (var effect in _effectData.Effects)
                {
                    sb.AppendLine($"  - {effect.GetDescription()}");
                }
            }

            // 发动条件
            if (_effectData.ActivationConditions != null && _effectData.ActivationConditions.Count > 0)
            {
                sb.AppendLine("条件:");
                foreach (var condition in _effectData.ActivationConditions)
                {
                    sb.AppendLine($"  - {condition.GetDescription()}");
                }
            }

            return sb.ToString();
        }

        #region 事件处理

        private void OnEditClicked()
        {
            _onEdit?.Invoke(_effectData);
        }

        private void OnDeleteClicked()
        {
            _onDelete?.Invoke(_effectData);
        }

        private void OnExpandClicked()
        {
            _isExpanded = !_isExpanded;

            if (_detailPanel != null)
            {
                _detailPanel.SetActive(_isExpanded);
            }
        }

        #endregion

        #region 显示名称和颜色

        private string GetTriggerTimingShortName(TriggerTiming timing)
        {
            return timing switch
            {
                TriggerTiming.Activate_Active => "[主动]",
                TriggerTiming.Activate_Instant => "[瞬间]",
                TriggerTiming.Activate_Response => "[响应]",
                TriggerTiming.On_EnterBattlefield => "[入场]",
                TriggerTiming.On_LeaveBattlefield => "[离场]",
                TriggerTiming.On_Death => "[死亡]",
                TriggerTiming.On_TurnStart => "[回始]",
                TriggerTiming.On_TurnEnd => "[回末]",
                TriggerTiming.On_AttackDeclare => "[攻宣]",
                TriggerTiming.On_DamageDealt => "[伤敌]",
                TriggerTiming.On_DamageTaken => "[受伤]",
                TriggerTiming.On_CardDraw => "[抽卡]",
                TriggerTiming.On_CardPlay => "[使用]",
                _ => "[其他]"
            };
        }

        private Color GetSpeedColor(int speed)
        {
            return speed switch
            {
                0 => new Color(0.6f, 0.6f, 0.6f), // 灰色 - 基础速度
                1 => new Color(0.3f, 0.7f, 0.4f), // 绿色 - 普通速度
                2 => new Color(0.9f, 0.7f, 0.2f), // 黄色 - 快速
                3 => new Color(0.9f, 0.3f, 0.3f), // 红色 - 极速
                _ => new Color(0.8f, 0.8f, 0.8f)
            };
        }

        private Color GetActivationTypeColor(EffectActivationType type)
        {
            return type switch
            {
                EffectActivationType.Voluntary => new Color(0.3f, 0.6f, 0.9f), // 蓝色 - 自由发动
                EffectActivationType.Automatic => new Color(0.9f, 0.7f, 0.2f), // 黄色 - 自动发动
                EffectActivationType.Mandatory => new Color(0.9f, 0.3f, 0.3f), // 红色 - 强制发动
                _ => new Color(0.6f, 0.6f, 0.6f)
            };
        }

        #endregion
    }
}