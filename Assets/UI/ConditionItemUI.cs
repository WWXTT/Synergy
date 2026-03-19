using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;

namespace CardCore.UI
{
    /// <summary>
    /// 条件项 UI
    /// </summary>
    public class ConditionItemUI : MonoBehaviour
    {
        [Header("显示")]
        [SerializeField] private TMP_Text _typeText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Image _iconImage;

        [Header("取反标记")]
        [SerializeField] private GameObject _negateIndicator;

        [Header("按钮")]
        [SerializeField] private Button _editButton;
        [SerializeField] private Button _removeButton;

        /// <summary>条件数据</summary>
        public ActivationConditionData ConditionData { get; private set; }

        /// <summary>改变回调</summary>
        private Action<ActivationConditionData> _onChanged;

        /// <summary>移除回调</summary>
        private Action<ActivationConditionData> _onRemoved;

        /// <summary>弹窗引用</summary>
        private ConditionEditorPopup _popup;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="data">条件数据</param>
        /// <param name="onChanged">改变回调</param>
        /// <param name="onRemoved">移除回调</param>
        public void Initialize(ActivationConditionData data, Action<ActivationConditionData> onChanged, Action<ActivationConditionData> onRemoved)
        {
            ConditionData = data;
            _onChanged = onChanged;
            _onRemoved = onRemoved;

            _editButton?.AddClickListener(OnEditClicked);
            _removeButton?.AddClickListener(OnRemoveClicked);

            UpdateUI();
        }

        /// <summary>
        /// 设置弹窗引用
        /// </summary>
        public void SetPopup(ConditionEditorPopup popup)
        {
            _popup = popup;
        }

        /// <summary>
        /// 更新UI
        /// </summary>
        public void UpdateUI()
        {
            if (ConditionData == null) return;

            // 更新类型显示
            if (_typeText != null)
            {
                _typeText.text = GetConditionTypeShortName((ConditionType)ConditionData.Type);
            }

            // 更新描述显示
            if (_descriptionText != null)
            {
                _descriptionText.text = ConditionData.GetDescription();
            }

            // 更新取反标记
            if (_negateIndicator != null)
            {
                _negateIndicator.SetActive(ConditionData.Negate);
            }

            // 更新图标颜��
            if (_iconImage != null)
            {
                _iconImage.color = GetConditionTypeColor((ConditionType)ConditionData.Type);
            }
        }

        #region 事件处理

        private void OnEditClicked()
        {
            if (_popup != null)
            {
                _popup.Show(ConditionData, (data) =>
                {
                    if (data != null)
                    {
                        ConditionData = data;
                        UpdateUI();
                        _onChanged?.Invoke(ConditionData);
                    }
                });
            }
            else
            {
                _onChanged?.Invoke(ConditionData);
            }
        }

        private void OnRemoveClicked()
        {
            _onRemoved?.Invoke(ConditionData);
        }

        #endregion

        #region 显示名称和颜色

        private string GetConditionTypeShortName(ConditionType type)
        {
            return type switch
            {
                // 资源条件
                ConditionType.MinCardsInHand => "手牌≥",
                ConditionType.MaxCardsInHand => "手牌≤",
                ConditionType.MinCardsOnField => "场上≥",
                ConditionType.MaxCardsOnField => "场上≤",
                ConditionType.CardsInGraveyard => "墓地≥",
                ConditionType.CardsInDeck => "牌库≥",
                ConditionType.MinManaAvailable => "元素≥",
                ConditionType.SpecificManaTypeAvailable => "元素",

                // 实体条件
                ConditionType.ControllerHasLife => "己血",
                ConditionType.OpponentHasLife => "敌血",
                ConditionType.CardHasType => "类型",
                ConditionType.CardHasManaType => "颜色",
                ConditionType.CardIsTapped => "已横",
                ConditionType.CardIsUntapped => "未横",
                ConditionType.CardHasPower => "攻≥",
                ConditionType.CardHasLife => "防≥",
                ConditionType.HasKeyword => "关键词",
                ConditionType.HasAbility => "异能",

                // 时点条件
                ConditionType.OncePerTurn => "1/回合",
                ConditionType.OnlyMainPhase => "主阶",
                ConditionType.OnlyOwnTurn => "己回合",
                ConditionType.OnlyOpponentTurn => "敌回合",
                ConditionType.FirstTimeThisGame => "首次",
                ConditionType.FirstTimeThisTurn => "本回首次",
                ConditionType.DuringCombat => "战斗中",
                ConditionType.NotDuringCombat => "非战斗",

                // 场地条件
                ConditionType.FieldHasCardType => "场有",
                ConditionType.OpponentFieldHasCardType => "敌场有",
                ConditionType.HandHasCardType => "手有",
                ConditionType.GraveyardHasCardType => "墓有",

                // 伤害条件
                ConditionType.DamageDealtThisTurn => "伤≥",
                ConditionType.DamageTakenThisTurn => "受伤≥",
                ConditionType.CombatDamageDealt => "战伤",
                ConditionType.CombatDamageTaken => "受战伤",

                // 战斗条件
                ConditionType.Attacking => "攻击中",
                ConditionType.Blocking => "阻挡中",
                ConditionType.BlockedThisTurn => "被阻",
                ConditionType.WasBlocked => "被阻过",

                // 连锁条件
                ConditionType.StackHasEffects => "栈有",
                ConditionType.StackEmpty => "栈空",
                ConditionType.HasPriority => "优先权",

                // 复合条件
                ConditionType.And => "AND",
                ConditionType.Or => "OR",
                ConditionType.Not => "NOT",
                ConditionType.Custom => "自定义",

                _ => type.ToString().Substring(0, Mathf.Min(4, type.ToString().Length))
            };
        }

        private Color GetConditionTypeColor(ConditionType type)
        {
            return type switch
            {
                // 资源条件 - 蓝色
                ConditionType.MinCardsInHand => new Color(0.3f, 0.5f, 0.9f),
                ConditionType.MaxCardsInHand => new Color(0.3f, 0.4f, 0.8f),
                ConditionType.MinCardsOnField => new Color(0.4f, 0.5f, 0.8f),
                ConditionType.MaxCardsOnField => new Color(0.3f, 0.4f, 0.7f),
                ConditionType.MinManaAvailable => new Color(0.2f, 0.6f, 0.9f),
                ConditionType.SpecificManaTypeAvailable => new Color(0.3f, 0.5f, 0.8f),

                // 生命相关 - 红色/绿色
                ConditionType.ControllerHasLife => new Color(0.3f, 0.7f, 0.4f),
                ConditionType.OpponentHasLife => new Color(0.8f, 0.3f, 0.3f),
                ConditionType.DamageDealtThisTurn => new Color(0.9f, 0.3f, 0.3f),
                ConditionType.DamageTakenThisTurn => new Color(0.7f, 0.3f, 0.3f),

                // 时点条件 - 黄色
                ConditionType.OncePerTurn => new Color(0.9f, 0.7f, 0.2f),
                ConditionType.OnlyMainPhase => new Color(0.8f, 0.6f, 0.3f),
                ConditionType.OnlyOwnTurn => new Color(0.9f, 0.8f, 0.3f),
                ConditionType.OnlyOpponentTurn => new Color(0.8f, 0.7f, 0.3f),

                // 战斗条件 - 橙色
                ConditionType.DuringCombat => new Color(0.9f, 0.5f, 0.2f),
                ConditionType.Attacking => new Color(0.9f, 0.4f, 0.2f),
                ConditionType.Blocking => new Color(0.8f, 0.5f, 0.3f),

                // 复合条件 - 紫色
                ConditionType.And => new Color(0.6f, 0.3f, 0.8f),
                ConditionType.Or => new Color(0.7f, 0.4f, 0.7f),
                ConditionType.Not => new Color(0.5f, 0.3f, 0.6f),

                // 默认 - 灰色
                _ => new Color(0.6f, 0.6f, 0.6f)
            };
        }

        #endregion
    }
}