using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;

namespace CardCore.UI
{
    /// <summary>
    /// 条件编辑弹窗
    /// </summary>
    public class ConditionEditorPopup : BaseUI
    {
        [Header("条件类型选择")]
        [SerializeField] private TMP_Dropdown _conditionTypeDropdown;

        [Header("参数输入")]
        [SerializeField] private GameObject _valueGroup;
        [SerializeField] private TMP_InputField _valueInput;

        [SerializeField] private GameObject _value2Group;
        [SerializeField] private TMP_InputField _value2Input;

        [SerializeField] private GameObject _cardTypeGroup;
        [SerializeField] private TMP_Dropdown _cardTypeDropdown;

        [SerializeField] private GameObject _manaTypeGroup;
        [SerializeField] private TMP_Dropdown _manaTypeDropdown;

        [SerializeField] private GameObject _stringValueGroup;
        [SerializeField] private TMP_InputField _stringValueInput;

        [Header("取反选项")]
        [SerializeField] private Toggle _negateToggle;

        [Header("预览")]
        [SerializeField] private TMP_Text _previewText;

        [Header("按钮")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        /// <summary>当前编辑的数据</summary>
        private ActivationConditionData _currentData;

        /// <summary>确认回调</summary>
        private Action<ActivationConditionData> _onConfirmed;

        protected override void Initialize()
        {
            base.Initialize();

            InitializeDropdowns();

            _confirmButton?.AddClickListener(OnConfirmClicked);
            _cancelButton?.AddClickListener(OnCancelClicked);

            _conditionTypeDropdown?.onValueChanged.AddListener(OnConditionTypeChanged);
            _valueInput?.onValueChanged.AddListener(OnParameterChanged);
            _value2Input?.onValueChanged.AddListener(OnParameterChanged);
            _cardTypeDropdown?.onValueChanged.AddListener(OnParameterChanged);
            _manaTypeDropdown?.onValueChanged.AddListener(OnParameterChanged);
            _stringValueInput?.onValueChanged.AddListener(OnParameterChanged);
            _negateToggle?.onValueChanged.AddListener(OnParameterChanged);
        }

        /// <summary>
        /// 初始化下拉菜单
        /// </summary>
        private void InitializeDropdowns()
        {
            // 条件类型
            if (_conditionTypeDropdown != null)
            {
                _conditionTypeDropdown.ClearOptions();
                var options = new List<string>();
                foreach (ConditionType type in Enum.GetValues(typeof(ConditionType)))
                {
                    options.Add(GetConditionTypeDisplayName(type));
                }
                _conditionTypeDropdown.AddOptions(options);
            }

            // 卡牌类型
            if (_cardTypeDropdown != null)
            {
                _cardTypeDropdown.ClearOptions();
                var options = new List<string>();
                foreach (CardType type in Enum.GetValues(typeof(CardType)))
                {
                    options.Add(type.ToString());
                }
                _cardTypeDropdown.AddOptions(options);
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
        }

        /// <summary>
        /// 显示弹窗
        /// </summary>
        /// <param name="data">编辑数据（null表示新建）</param>
        /// <param name="onConfirmed">确认回调</param>
        public void Show(ActivationConditionData data, Action<ActivationConditionData> onConfirmed)
        {
            _onConfirmed = onConfirmed;

            if (data != null)
            {
                _currentData = new ActivationConditionData
                {
                    Type = data.Type,
                    Value = data.Value,
                    Value2 = data.Value2,
                    CardTypeParam = data.CardTypeParam,
                    ManaTypeParam = data.ManaTypeParam,
                    StringValue = data.StringValue,
                    Negate = data.Negate,
                    CustomConditionId = data.CustomConditionId
                };
            }
            else
            {
                _currentData = new ActivationConditionData
                {
                    Type = 0,
                    Value = 1,
                    Value2 = 0,
                    Negate = false
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

            _conditionTypeDropdown.value = (int)_currentData.Type;
            _valueInput.text = _currentData.Value.ToString();
            _value2Input.text = _currentData.Value2.ToString();

            if (_currentData.CardTypeParam.HasValue)
                _cardTypeDropdown.value = (int)_currentData.CardTypeParam.Value;

            if (_currentData.ManaTypeParam.HasValue)
                _manaTypeDropdown.value = (int)_currentData.ManaTypeParam.Value;

            _stringValueInput.text = _currentData.StringValue ?? string.Empty;
            _negateToggle.isOn = _currentData.Negate;

            UpdateParameterVisibility();
            UpdatePreview();
        }

        /// <summary>
        /// 从UI更新数据
        /// </summary>
        private void UpdateDataFromUI()
        {
            if (_currentData == null) return;

            _currentData.Type = _conditionTypeDropdown.value;

            if (int.TryParse(_valueInput.text, out int value))
                _currentData.Value = value;

            if (int.TryParse(_value2Input.text, out int value2))
                _currentData.Value2 = value2;

            _currentData.CardTypeParam = _cardTypeDropdown.value;
            _currentData.ManaTypeParam = _manaTypeDropdown.value;
            _currentData.StringValue = _stringValueInput.text;
            _currentData.Negate = _negateToggle.isOn;
        }

        /// <summary>
        /// 更新参数显示
        /// </summary>
        private void UpdateParameterVisibility()
        {
            var conditionType = (ConditionType)_conditionTypeDropdown.value;

            bool showValue = NeedsValueParameter(conditionType);
            bool showValue2 = NeedsValue2Parameter(conditionType);
            bool showCardType = NeedsCardTypeParameter(conditionType);
            bool showManaType = NeedsManaTypeParameter(conditionType);
            bool showString = NeedsStringParameter(conditionType);

            if (_valueGroup != null) _valueGroup.SetActive(showValue);
            if (_value2Group != null) _value2Group.SetActive(showValue2);
            if (_cardTypeGroup != null) _cardTypeGroup.SetActive(showCardType);
            if (_manaTypeGroup != null) _manaTypeGroup.SetActive(showManaType);
            if (_stringValueGroup != null) _stringValueGroup.SetActive(showString);
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

        private bool NeedsValueParameter(ConditionType type)
        {
            return type switch
            {
                ConditionType.MinCardsInHand => true,
                ConditionType.MaxCardsInHand => true,
                ConditionType.MinCardsOnField => true,
                ConditionType.MaxCardsOnField => true,
                ConditionType.CardsInGraveyard => true,
                ConditionType.CardsInDeck => true,
                ConditionType.MinManaAvailable => true,
                ConditionType.SpecificManaTypeAvailable => true,
                ConditionType.ControllerHasLife => true,
                ConditionType.OpponentHasLife => true,
                ConditionType.CardHasPower => true,
                ConditionType.CardHasLife => true,
                ConditionType.DamageDealtThisTurn => true,
                ConditionType.DamageTakenThisTurn => true,
                _ => false
            };
        }

        private bool NeedsValue2Parameter(ConditionType type)
        {
            return false; // Currently no conditions use Value2
        }

        private bool NeedsCardTypeParameter(ConditionType type)
        {
            return type switch
            {
                ConditionType.CardHasType => true,
                ConditionType.FieldHasCardType => true,
                ConditionType.OpponentFieldHasCardType => true,
                ConditionType.HandHasCardType => true,
                ConditionType.GraveyardHasCardType => true,
                _ => false
            };
        }

        private bool NeedsManaTypeParameter(ConditionType type)
        {
            return type switch
            {
                ConditionType.CardHasManaType => true,
                ConditionType.SpecificManaTypeAvailable => true,
                _ => false
            };
        }

        private bool NeedsStringParameter(ConditionType type)
        {
            return type switch
            {
                ConditionType.HasKeyword => true,
                ConditionType.HasAbility => true,
                _ => false
            };
        }

        #endregion

        #region 事件处理

        private void OnConditionTypeChanged(int index)
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

        private void OnParameterChanged(bool value)
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

        private string GetConditionTypeDisplayName(ConditionType type)
        {
            return type switch
            {
                // 资源条件
                ConditionType.MinCardsInHand => "手牌数量下限",
                ConditionType.MaxCardsInHand => "手牌数量上限",
                ConditionType.MinCardsOnField => "场上数量下限",
                ConditionType.MaxCardsOnField => "场上数量上限",
                ConditionType.CardsInGraveyard => "墓地数量条件",
                ConditionType.CardsInDeck => "牌库数量条件",
                ConditionType.MinManaAvailable => "可用元素下限",
                ConditionType.SpecificManaTypeAvailable => "特定元素可用",

                // 实体条件
                ConditionType.ControllerHasLife => "控制者生命值",
                ConditionType.OpponentHasLife => "对手生命值",
                ConditionType.CardHasType => "卡牌类型条件",
                ConditionType.CardHasManaType => "法力颜色条件",
                ConditionType.CardIsTapped => "卡牌已横置",
                ConditionType.CardIsUntapped => "卡牌未横置",
                ConditionType.CardHasPower => "攻击力条件",
                ConditionType.CardHasLife => "生命值条件",
                ConditionType.HasKeyword => "拥有关键词",
                ConditionType.HasAbility => "拥有异能",

                // 时点条件
                ConditionType.OncePerTurn => "每回合一次",
                ConditionType.OnlyMainPhase => "仅主要阶段",
                ConditionType.OnlyOwnTurn => "仅自己回合",
                ConditionType.OnlyOpponentTurn => "仅对手回合",
                ConditionType.FirstTimeThisGame => "本局首次",
                ConditionType.FirstTimeThisTurn => "本回合首次",
                ConditionType.DuringCombat => "战斗中",
                ConditionType.NotDuringCombat => "非战斗中",

                // 场地条件
                ConditionType.FieldHasCardType => "场上有特定类型",
                ConditionType.OpponentFieldHasCardType => "对手场上有特定类型",
                ConditionType.HandHasCardType => "手牌中有特定类型",
                ConditionType.GraveyardHasCardType => "墓地中有特定类型",

                // 伤害条件
                ConditionType.DamageDealtThisTurn => "本回合造成伤害",
                ConditionType.DamageTakenThisTurn => "本回合受到伤害",
                ConditionType.CombatDamageDealt => "造成战斗伤害",
                ConditionType.CombatDamageTaken => "受到战斗伤害",

                // 战斗条件
                ConditionType.Attacking => "正在攻击",
                ConditionType.Blocking => "正在阻挡",
                ConditionType.BlockedThisTurn => "本回合被阻挡",
                ConditionType.WasBlocked => "被阻挡过",

                // 连锁条件
                ConditionType.StackHasEffects => "栈上有效果",
                ConditionType.StackEmpty => "栈为空",
                ConditionType.HasPriority => "拥有优先权",

                // 复合条件
                ConditionType.And => "满足所有条件",
                ConditionType.Or => "满足任一条件",
                ConditionType.Not => "条件取反",
                ConditionType.Custom => "自定义条件",

                _ => type.ToString()
            };
        }

        #endregion
    }
}