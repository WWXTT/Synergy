using CardCore;
using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using cfg;
using CardCore.Data;

namespace CardCore.UI
{
    /// <summary>
    /// 原子效果编辑弹窗
    /// </summary>
    public class AtomicEffectEditorPopup : BaseUI
    {
        [Header("效果类型选择")]
        [SerializeField] private TMP_Dropdown _effectTypeDropdown;

        [Header("参数输入")]
        [SerializeField] private GameObject _valueInputGroup;
        [SerializeField] private TMP_InputField _valueInput;

        [SerializeField] private GameObject _value2InputGroup;
        [SerializeField] private TMP_InputField _value2Input;

        [SerializeField] private GameObject _stringValueGroup;
        [SerializeField] private TMP_InputField _stringValueInput;

        [SerializeField] private GameObject _manaTypeGroup;
        [SerializeField] private TMP_Dropdown _manaTypeDropdown;

        [SerializeField] private GameObject _zoneGroup;
        [SerializeField] private TMP_Dropdown _zoneDropdown;

        [SerializeField] private GameObject _durationGroup;
        [SerializeField] private TMP_Dropdown _durationDropdown;

        [Header("预览")]
        [SerializeField] private TMP_Text _previewText;

        [Header("按钮")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        /// <summary>当前编辑的数据</summary>
        private AtomicEffectData _currentData;

        /// <summary>确认回调</summary>
        private Action<AtomicEffectData> _onConfirmed;

        /// <summary>原始数据（用于编辑）</summary>
        private AtomicEffectData _originalData;

        protected override void Initialize()
        {
            base.Initialize();

            InitializeDropdowns();

            _confirmButton?.AddClickListener(OnConfirmClicked);
            _cancelButton?.AddClickListener(OnCancelClicked);

            _effectTypeDropdown?.onValueChanged.AddListener(OnEffectTypeChanged);
            _valueInput?.onValueChanged.AddListener(OnParameterChanged);
            _value2Input?.onValueChanged.AddListener(OnParameterChanged);
            _stringValueInput?.onValueChanged.AddListener(OnParameterChanged);
            _manaTypeDropdown?.onValueChanged.AddListener(OnParameterChanged);
            _zoneDropdown?.onValueChanged.AddListener(OnParameterChanged);
            _durationDropdown?.onValueChanged.AddListener(OnParameterChanged);
        }

        /// <summary>
        /// 初始化下拉菜单
        /// </summary>
        private void InitializeDropdowns()
        {
            // 效果类型
            if (_effectTypeDropdown != null)
            {
                _effectTypeDropdown.ClearOptions();
                var options = new List<string>();
                foreach (AtomicEffectType type in Enum.GetValues(typeof(AtomicEffectType)))
                {
                    options.Add(GetEffectTypeDisplayName(type));
                }
                _effectTypeDropdown.AddOptions(options);
            }

            // 法力类型
            if (_manaTypeDropdown != null)
            {
                _manaTypeDropdown.ClearOptions();
                var options = new List<string>();
                foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
                {
                    options.Add(type.ToString());
                }
                _manaTypeDropdown.AddOptions(options);
            }

            // 区域
            if (_zoneDropdown != null)
            {
                _zoneDropdown.ClearOptions();
                var options = new List<string>();
                foreach (Zone zone in Enum.GetValues(typeof(Zone)))
                {
                    options.Add(zone.ToString());
                }
                _zoneDropdown.AddOptions(options);
            }

            // 持续时间
            if (_durationDropdown != null)
            {
                _durationDropdown.ClearOptions();
                var options = new List<string>();
                foreach (DurationType duration in Enum.GetValues(typeof(DurationType)))
                {
                    options.Add(GetDurationDisplayName(duration));
                }
                _durationDropdown.AddOptions(options);
            }
        }

        /// <summary>
        /// 显示弹窗
        /// </summary>
        /// <param name="data">编辑数据（null表示新建）</param>
        /// <param name="onConfirmed">确认回调</param>
        public void Show(AtomicEffectData data, Action<AtomicEffectData> onConfirmed)
        {
            _originalData = data;
            _onConfirmed = onConfirmed;

            if (data != null)
            {
                // 复制数据进行编辑
                _currentData = new AtomicEffectData
                {
                    Type = data.Type,
                    Value = data.Value,
                    Value2 = data.Value2,
                    StringValue = data.StringValue,
                    ManaTypeParam = data.ManaTypeParam,
                    ZoneParam = data.ZoneParam,
                    Duration = data.Duration
                };
            }
            else
            {
                _currentData = new AtomicEffectData
                {
                    Type = 0,
                    Value = 1,
                    Value2 = 0,
                    StringValue = string.Empty,
                    ManaTypeParam = 0,
                    ZoneParam = 0,
                    Duration = 0
                };
            }

            UpdateUIFromData();
            Show();
        }

        /// <summary>
        /// 从数据更新UI
        /// </summary>
        private void UpdateUIFromData()
        {
            if (_currentData == null) return;

            _effectTypeDropdown.value = _currentData.Type;
            _valueInput.text = _currentData.Value.ToString();
            _value2Input.text = _currentData.Value2.ToString();
            _stringValueInput.text = _currentData.StringValue ?? string.Empty;
            _manaTypeDropdown.value = _currentData.ManaTypeParam;
            _zoneDropdown.value = _currentData.ZoneParam;
            _durationDropdown.value = _currentData.Duration;

            UpdateParameterVisibility();
            UpdatePreview();
        }

        /// <summary>
        /// 从UI更新数据
        /// </summary>
        private void UpdateDataFromUI()
        {
            if (_currentData == null) return;

            _currentData.Type = _effectTypeDropdown.value;

            if (int.TryParse(_valueInput.text, out int value))
                _currentData.Value = value;

            if (int.TryParse(_value2Input.text, out int value2))
                _currentData.Value2 = value2;

            _currentData.StringValue = _stringValueInput.text;
            _currentData.ManaTypeParam = _manaTypeDropdown.value;
            _currentData.ZoneParam = _zoneDropdown.value;
            _currentData.Duration = _durationDropdown.value;
        }

        /// <summary>
        /// 更新参数显示
        /// </summary>
        private void UpdateParameterVisibility()
        {
            var effectType = (AtomicEffectType)_effectTypeDropdown.value;

            // 根据效果类型显示/隐藏参数组
            bool showValue = NeedsValueParameter(effectType);
            bool showValue2 = NeedsValue2Parameter(effectType);
            bool showString = NeedsStringParameter(effectType);
            bool showMana = NeedsManaTypeParameter(effectType);
            bool showZone = NeedsZoneParameter(effectType);
            bool showDuration = NeedsDurationParameter(effectType);

            if (_valueInputGroup != null) _valueInputGroup.SetActive(showValue);
            if (_value2InputGroup != null) _value2InputGroup.SetActive(showValue2);
            if (_stringValueGroup != null) _stringValueGroup.SetActive(showString);
            if (_manaTypeGroup != null) _manaTypeGroup.SetActive(showMana);
            if (_zoneGroup != null) _zoneGroup.SetActive(showZone);
            if (_durationGroup != null) _durationGroup.SetActive(showDuration);
        }

        /// <summary>
        /// 更新预览
        /// </summary>
        private void UpdatePreview()
        {
            UpdateDataFromUI();

            if (_previewText != null)
            {
                _previewText.text = _currentData?.GetDescription() ?? "未配置";
            }
        }

        #region 参数可见性判断

        private bool NeedsValueParameter(AtomicEffectType type)
        {
            return type switch
            {
                // 伤害与治疗
                AtomicEffectType.DealDamage => true,
                AtomicEffectType.DealCombatDamage => true,
                AtomicEffectType.Heal => true,
                AtomicEffectType.LifeLoss => true,
                AtomicEffectType.DrawCard => true,
                AtomicEffectType.DiscardCard => true,
                AtomicEffectType.MillCard => true,
                AtomicEffectType.ModifyPower => true,
                AtomicEffectType.ModifyLife => true,
                AtomicEffectType.SetPower => true,
                AtomicEffectType.SetLife => true,
                AtomicEffectType.AddMana => true,
                AtomicEffectType.ConsumeMana => true,
                AtomicEffectType.PreventDamage => true,
                AtomicEffectType.AddCounters => true,
                AtomicEffectType.CreateToken => true,
                AtomicEffectType.SearchDeck => true,
                // 红色效果
                AtomicEffectType.AoEDamage => true,
                AtomicEffectType.SplitDamage => true,
                AtomicEffectType.TrampleDamage => true,
                AtomicEffectType.DamageCannotBePrevented => true,
                AtomicEffectType.GrantHaste => true,
                AtomicEffectType.GrantRush => true,
                AtomicEffectType.GrantDoubleStrike => true,
                AtomicEffectType.GrantMultiAttack => true,
                AtomicEffectType.DestroyRandom => true,
                // 蓝色效果
                AtomicEffectType.DrawThenDiscard => true,
                AtomicEffectType.ScryCards => true,
                AtomicEffectType.FreezePermanent => true,
                // 绿色效果
                AtomicEffectType.RampMana => true,
                AtomicEffectType.SearchLand => true,
                AtomicEffectType.UntapAll => true,
                AtomicEffectType.DoubleCounters => true,
                AtomicEffectType.FightTarget => true,
                AtomicEffectType.GrantTrample => true,
                AtomicEffectType.GrantReach => true,
                // 灰色效果
                AtomicEffectType.SearchAndReveal => true,
                // 反规则效果
                AtomicEffectType.GrantSpellShield => true,
                _ => false
            };
        }

        private bool NeedsValue2Parameter(AtomicEffectType type)
        {
            return type switch
            {
                AtomicEffectType.DiscardCard => true, // random flag
                AtomicEffectType.SearchDeck => true,  // destination
                AtomicEffectType.ShuffleIntoDeck => true, // position
                AtomicEffectType.CreateToken => true, // tapped flag
                AtomicEffectType.Nullify => true,     // nullify type
                // 红色效果
                AtomicEffectType.GrantMultiAttack => true, // attack count
                // 蓝色效果
                AtomicEffectType.DrawThenDiscard => true, // draw then discard count
                _ => false
            };
        }

        private bool NeedsStringParameter(AtomicEffectType type)
        {
            return type switch
            {
                AtomicEffectType.AddKeyword => true,
                AtomicEffectType.RemoveKeyword => true,
                AtomicEffectType.CreateToken => true,
                AtomicEffectType.TransformCard => true,
                AtomicEffectType.TransformInto => true,
                // 绿色效果
                AtomicEffectType.RemoveDebuffs => true, // debuff types
                _ => false
            };
        }

        private bool NeedsManaTypeParameter(AtomicEffectType type)
        {
            return type switch
            {
                AtomicEffectType.AddMana => true,
                AtomicEffectType.ConsumeMana => true,
                // 绿色效果
                AtomicEffectType.RampMana => true,
                AtomicEffectType.SearchLand => true,
                _ => false
            };
        }

        private bool NeedsZoneParameter(AtomicEffectType type)
        {
            return type switch
            {
                AtomicEffectType.Exile => true,
                AtomicEffectType.PutToBattlefield => true,
                AtomicEffectType.CopyCard => true,
                AtomicEffectType.MoveCard => true,
                // 灰色效果
                AtomicEffectType.MoveToAnyZone => true,
                AtomicEffectType.ExchangePosition => true,
                _ => false
            };
        }

        private bool NeedsDurationParameter(AtomicEffectType type)
        {
            return type switch
            {
                AtomicEffectType.ModifyPower => true,
                AtomicEffectType.ModifyLife => true,
                AtomicEffectType.AddCardType => true,
                AtomicEffectType.RemoveCardType => true,
                AtomicEffectType.AddKeyword => true,
                AtomicEffectType.RemoveKeyword => true,
                AtomicEffectType.GainControl => true,
                AtomicEffectType.Nullify => true,
                AtomicEffectType.GrantImmunity => true,
                AtomicEffectType.GrantUnaffected => true,
                AtomicEffectType.SwapStats => true,
                // 反规则效果
                AtomicEffectType.GrantCannotBeTargeted => true,
                AtomicEffectType.GrantSpellShield => true,
                // 蓝色效果
                AtomicEffectType.FreezePermanent => true,
                // 绿色效果
                AtomicEffectType.EvolveCreature => true,
                _ => false
            };
        }

        #endregion

        #region 事件处理

        private void OnEffectTypeChanged(int index)
        {
            UpdateParameterVisibility();
            UpdatePreview();
        }

        private void OnParameterChanged(string value)
        {
            UpdatePreview();
        }

        private void OnParameterChanged(int value)
        {
            UpdatePreview();
        }

        private void OnConfirmClicked()
        {
            UpdateDataFromUI();
            _onConfirmed?.Invoke(_currentData);
            Hide();
        }

        private void OnCancelClicked()
        {
            _onConfirmed?.Invoke(null);
            Hide();
        }

        #endregion

        #region 显示名称

        private string GetEffectTypeDisplayName(AtomicEffectType type)
        {
            return EffectClassificationConfig.GetClassification(type).Description;
        }

        private string GetDurationDisplayName(DurationType duration)
        {
            return duration switch
            {
                DurationType.Once => "一次性",
                DurationType.UntilEndOfTurn => "直到回合结束",
                DurationType.UntilLeaveBattlefield => "直到离场",
                DurationType.WhileCondition => "条件满足时",
                DurationType.Permanent => "永久",
                _ => duration.ToString()
            };
        }

        #endregion
    }
}