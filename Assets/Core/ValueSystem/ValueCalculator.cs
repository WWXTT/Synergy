using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using cfg;
using CardCore.Data;

namespace CardCore
{
    #region 效果费用类型

    /// <summary>
    /// 效果费用类型
    /// 决定效果的代价如何计算和支付
    /// </summary>
    public enum EffectCostType
    {
        /// <summary>
        /// 战吼类 - 费用计入卡牌费用
        /// 使用卡牌时一次性支付所有代价
        /// 效果价值会影响卡牌的总费用
        /// </summary>
        Battlecry,

        /// <summary>
        /// 启动式 - 费用单独支付
        /// 每次发动效果时支���代价
        /// 效果价值不影响卡牌费用，但需要单独评估平衡性
        /// </summary>
        Activated,

        /// <summary>
        /// 触发式 - 无需支付费用
        /// 满足条件自动发动
        /// 效果价值体现为触发条件的限制
        /// </summary>
        Triggered,

        /// <summary>
        /// 静态效果 - 持续在场
        /// 不进入栈，持续生效
        /// 效果价值体现为卡牌占用场地资源
        /// </summary>
        Static,

        /// <summary>
        /// 被动效果 - 无需激活
        /// 自动生效，无需选择
        /// </summary>
        Passive,
    }

    #endregion

    #region 效果价值计算结果

    /// <summary>
    /// 效果价值计算结果
    /// </summary>
    [Serializable]
    public class EffectValueResult
    {
        /// <summary>效果名称</summary>
        public string EffectName;

        /// <summary>效果费用类型</summary>
        public EffectCostType CostType;

        /// <summary>基础价值</summary>
        public float BaseValue;

        /// <summary>原子效果价值列表</summary>
        public List<AtomicEffectValue> AtomicEffectValues = new List<AtomicEffectValue>();

        /// <summary>目标修正系数</summary>
        public float TargetModifier = 1.0f;

        /// <summary>时机修正系数</summary>
        public float TimingModifier = 1.0f;

        /// <summary>条件修正系数</summary>
        public float ConditionModifier = 1.0f;

        /// <summary>触发效果修正系数</summary>
        public float TriggerModifier = 1.0f;

        /// <summary>代价价值（负面）</summary>
        public float CostValue = 0f;

        /// <summary>协同效应价值调整</summary>
        public float SynergyAdjustment = 0f;

        /// <summary>满足的协同效应组合</summary>
        public List<string> SatisfiedSynergies = new List<string>();

        /// <summary>总价值</summary>
        public float TotalValue;

        /// <summary>
        /// 计算总���值
        /// </summary>
        public void CalculateTotal()
        {
            // 原子效果价值总和
            float effectValue = AtomicEffectValues.Sum(a => a.TotalValue);

            // 应用修正系数
            float modifiedValue = effectValue * TargetModifier * TimingModifier * ConditionModifier;

            // 加上代价价值
            TotalValue = modifiedValue + CostValue;
        }

        /// <summary>
        /// 获取建议费用（仅对战吼类有意义）
        /// </summary>
        public int GetSuggestedCost()
        {
            return Math.Max(0, (int)Math.Round(TotalValue));
        }

        public override string ToString()
        {
            string costTypeStr = CostType switch
            {
                EffectCostType.Battlecry => "[战吼]",
                EffectCostType.Activated => "[启动式]",
                EffectCostType.Triggered => "[触发式]",
                EffectCostType.Static => "[静态]",
                _ => ""
            };
            return $"{costTypeStr} {EffectName}: {TotalValue:F2} BRU (建议费用: {GetSuggestedCost()})";
        }
    }

    /// <summary>
    /// 原子效果价值
    /// </summary>
    [Serializable]
    public class AtomicEffectValue
    {
        /// <summary>效果类型</summary>
        public AtomicEffectType EffectType;

        /// <summary>基础价值</summary>
        public float BaseValue;

        /// <summary>数值参数</summary>
        public int ValueParam;

        /// <summary>价值系数（来自配置）</summary>
        public float ValueCoefficient;

        /// <summary>小计价值</summary>
        public float TotalValue;

        public override string ToString()
        {
            return $"{EffectType}({ValueParam}): {TotalValue:F2}";
        }
    }

    #endregion

    #region 卡牌价值计算结果

    /// <summary>
    /// 卡牌价值计算结果
    /// </summary>
    [Serializable]
    public class CardValueResult
    {
        /// <summary>卡牌类型基础价值</summary>
        public float CardTypeBaseValue;

        /// <summary>属性价值</summary>
        public float AttributeValue;

        /// <summary>各效果价值</summary>
        public List<EffectValueResult> EffectValues = new List<EffectValueResult>();

        /// <summary>战吼类效果总价值（计入卡牌费用）</summary>
        public float BattlecryEffectsValue;

        /// <summary>启动式效果列表（单独支付）</summary>
        public List<EffectValueResult> ActivatedEffects = new List<EffectValueResult>();

        /// <summary>触发式效果列表</summary>
        public List<EffectValueResult> TriggeredEffects = new List<EffectValueResult>();

        /// <summary>卡牌总价值（用于计算卡牌费用）</summary>
        public float TotalCardValue;

        /// <summary>建议卡牌费用</summary>
        public int SuggestedCost;

        /// <summary>
        /// 计算总价值
        /// </summary>
        public void CalculateTotal()
        {
            // 分类统计效果
            BattlecryEffectsValue = 0;
            ActivatedEffects.Clear();
            TriggeredEffects.Clear();

            foreach (var effect in EffectValues)
            {
                switch (effect.CostType)
                {
                    case EffectCostType.Battlecry:
                        BattlecryEffectsValue += effect.TotalValue;
                        break;
                    case EffectCostType.Activated:
                        ActivatedEffects.Add(effect);
                        break;
                    case EffectCostType.Triggered:
                    case EffectCostType.Static:
                    case EffectCostType.Passive:
                        // 触发式效果按一定比例计入卡牌费用
                        BattlecryEffectsValue += effect.TotalValue * 0.5f;
                        TriggeredEffects.Add(effect);
                        break;
                }
            }

            // 卡牌总价值 = 类型基础 + 属性 + 战吼类效果
            TotalCardValue = CardTypeBaseValue + AttributeValue + BattlecryEffectsValue;

            // 建议费用
            SuggestedCost = Math.Max(0, (int)Math.Round(TotalCardValue));
        }

        public override string ToString()
        {
            return $"卡牌价值: {TotalCardValue:F2} BRU, 建议费用: {SuggestedCost}\n" +
                   $"  - 属性: {AttributeValue:F2}\n" +
                   $"  - 战吼效果: {BattlecryEffectsValue:F2}\n" +
                   $"  - 启动式效果: {ActivatedEffects.Count}个";
        }

        /// <summary>
        /// 获取详细报告
        /// </summary>
        public string GetDetailedReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 卡牌价值报告 ===");
            sb.AppendLine($"卡牌类型基础价值: {CardTypeBaseValue:F2}");
            sb.AppendLine($"属性价值: {AttributeValue:F2}");
            sb.AppendLine();
            sb.AppendLine("--- 效果价值明细 ---");

            foreach (var effect in EffectValues)
            {
                sb.AppendLine($"  {effect}");
                foreach (var atomic in effect.AtomicEffectValues)
                {
                    sb.AppendLine($"    - {atomic}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"=== 卡牌总价值: {TotalCardValue:F2} BRU ===");
            sb.AppendLine($"=== 建议费用: {SuggestedCost} ===");

            if (ActivatedEffects.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- 启动式能力（单独支付费用）---");
                foreach (var effect in ActivatedEffects)
                {
                    sb.AppendLine($"  {effect.EffectName}: 价值 {effect.TotalValue:F2}, 代价 {effect.CostValue:F2}");
                }
            }

            return sb.ToString();
        }
    }

    #endregion

    #region 价值计算器

    /// <summary>
    /// 卡牌/效果价��计算器
    /// 基于配置文件计算卡牌和效果的价值
    /// </summary>
    public class ValueCalculator
    {
        private ValueSystemRuntimeConfig _config;

        public ValueCalculator(ValueSystemRuntimeConfig config)
        {
            _config = config;
        }

        public ValueCalculator()
        {
            _config = ValueSystemConfigManager.Instance.GetOrCreateConfig();
        }

        /// <summary>
        /// 计算效果价值
        /// </summary>
        public EffectValueResult CalculateEffectValue(EffectDefinition effect)
        {
            var result = new EffectValueResult
            {
                EffectName = effect.DisplayName ?? effect.Id,
                CostType = DetermineCostType(effect)
            };

            // 1. 计算原子效果价值
            float atomicTotal = 0f;
            foreach (var atomicInstance in effect.Effects)
            {
                var atomicValue = CalculateAtomicEffectValue(atomicInstance);
                result.AtomicEffectValues.Add(atomicValue);
                atomicTotal += atomicValue.TotalValue;
            }

            // 2. 应用目标修正系数
            float targetModifier = 1.0f;
            if (effect.TargetSelector != null)
            {
                targetModifier = _config.TargetModifierConfig.GetTargetModifier(
                    effect.TargetSelector.PrimaryTarget);

                // 多目标修正
                if (effect.TargetSelector.MaxTargets > 1)
                {
                    targetModifier *= _config.TargetModifierConfig.GetMultiTargetModifier(
                        effect.TargetSelector.MaxTargets);
                }
            }
            result.TargetModifier = targetModifier;

            // 3. 应用时机修正系数
            float timingModifier = _config.TimingModifierConfig.GetTimingModifier(effect.TriggerTiming);

            // 4. 应用条件修正系数
            float conditionModifier = _config.TimingModifierConfig.GetConditionModifier(effect.ActivationConditions);

            // 5. 计算触发式效果价值修正
            float triggerModifier = CalculateTriggerValue(effect);

            // 使用修正系数叠加（带上下限）
            result.TimingModifier = _config.TimingModifierConfig.GetCombinedModifier(
                timingModifier, conditionModifier, triggerModifier);

            // 6. 计算代价价值
            result.CostValue = _config.CostValueConfig.CalculateCostValue(
                effect.Cost,
                _config.EffectValueConfig);

            // 7. 计算总价值（使用改进的公式）
            // 总价值 = 原子效果总和 * 目标修正 * 时机修正（已包含条件和触发修正） + 代价价值
            result.TotalValue = atomicTotal * result.TargetModifier * result.TimingModifier + result.CostValue;

            return result;
        }

        /// <summary>
        /// 计算触发式效果的价值修正
        /// </summary>
        private float CalculateTriggerValue(EffectDefinition effect)
        {
            // 非触发式效果不应用折扣
            if (effect.CostType != EffectCostType.Triggered && !effect.IsTriggeredEffect)
            {
                return 1.0f;
            }

            // 如果没有配置触发价值配置，使用默认折扣
            if (_config.TriggerValueConfig == null)
            {
                return 0.8f; // 默认触发效果折扣
            }

            var triggerConfig = _config.TriggerValueConfig;

            // 自动推断触发频率（默认无限制）
            TriggerFrequency frequency = TriggerFrequency.Unlimited;
            // 可以从 ActivationConditions 中推断
            if (effect.ActivationConditions?.Any(c => c.Type == ConditionType.OncePerTurn) == true)
            {
                frequency = TriggerFrequency.OncePerTurn;
            }

            return triggerConfig.GetTriggerValue(effect.TriggerTiming, frequency);
        }

        /// <summary>
        /// 计算协同效应价值
        /// </summary>
        private float CalculateSynergyValue(
            List<AtomicEffectInstance> effects,
            float baseTotal,
            HashSet<AtomicEffectType> effectTypes)
        {
            if (_config.SynergyConfig == null) return baseTotal;

            var synergyConfig = _config.SynergyConfig;
            float result = baseTotal;

            // 1. 同类效果递减
            var groupedEffects = effects.GroupBy(e => e.Type);
            foreach (var group in groupedEffects)
            {
                int count = group.Count();
                if (count >= synergyConfig.DiscountStartsAt)
                {
                    // 计算该组效果的总价值
                    float groupTotal = group.Sum(e =>
                        _config.EffectValueConfig.GetAtomicEffectBaseValue(e.Type, e.Value, false));

                    // 应用递减折扣
                    float discountedValue = synergyConfig.CalculateSameTypeDiscount(count, groupTotal);

                    // 调整总价值
                    result = result - groupTotal + discountedValue;
                }
            }

            // 2. 组合加成
            result = synergyConfig.CalculateSynergyBonus(effectTypes, result);

            return result;
        }

        /// <summary>
        /// 计算原子效果价值
        /// </summary>
        private AtomicEffectValue CalculateAtomicEffectValue(AtomicEffectInstance instance)
        {
            var result = new AtomicEffectValue
            {
                EffectType = instance.Type,
                ValueParam = instance.Value
            };

            // 获取基础价值
            result.BaseValue = _config.EffectValueConfig.GetAtomicEffectBaseValue(
                instance.Type,
                instance.Value);

            // 应用修正器
            //if (instance.Modifiers != null && instance.Modifiers.Count > 0)
            //{
            //    float modifierTotal = 1.0f;
            //    foreach (var modifier in instance.Modifiers)
            //    {
            //        modifierTotal *= modifier.Multiplier;
            //    }
            //    result.ValueCoefficient = modifierTotal;
            //}
            //else
            {
                result.ValueCoefficient = 1.0f;
            }

            // 计算总价值
            result.TotalValue = result.BaseValue * result.ValueCoefficient;

            // 持续时间修正（完整支持所有持续时间类型）
            float durationDiscount = _config.AttributeValueConfig.GetDurationDiscount(instance.Duration);
            result.TotalValue *= durationDiscount;

            return result;
        }

        /// <summary>
        /// 计算卡牌价值
        /// </summary>
        public CardValueResult CalculateCardValue(
            CardType cardType,
            int? power,
            int? life,
            List<EffectDefinition> effects)
        {
            var result = new CardValueResult();

            // 1. 卡牌类型基础价值
            result.CardTypeBaseValue = _config.GetCardTypeBaseValue(cardType);

            // 2. 属性价值
            if (power.HasValue || life.HasValue)
            {
                result.AttributeValue = _config.AttributeValueConfig.CalculateStatValue(
                    power ?? 0,
                    life ?? 0,
                    isPermanent: true);
            }

            // 3. 各效果价值
            foreach (var effect in effects)
            {
                var effectValue = CalculateEffectValue(effect);
                result.EffectValues.Add(effectValue);
            }

            // 4. 计算总价值
            result.CalculateTotal();

            return result;
        }

        /// <summary>
        /// 从CardData计算卡牌价值
        /// </summary>
        public CardValueResult CalculateCardValue(CardData cardData)
        {
            // 将EffectData转换为EffectDefinition
            var effects = new List<EffectDefinition>();
            foreach (var effectData in cardData.Effects)
            {
                var def = ConvertToEffectDefinition(effectData, cardData);
                if (def != null)
                    effects.Add(def);
            }

            return CalculateCardValue(
                cardData.CardType,
                cardData.Power,
                cardData.Life,
                effects);
        }

        /// <summary>
        /// 确定效果费用类型
        /// </summary>
        private EffectCostType DetermineCostType(EffectDefinition effect)
        {
            // 根据触发时点判断
            if (effect.TriggerTiming == TriggerTiming.Activate_Active ||
                effect.TriggerTiming == TriggerTiming.Activate_Instant ||
                effect.TriggerTiming == TriggerTiming.Activate_Response)
            {
                // 有代价且代价不为空 -> 启动式
                // 代价为空或只有元素代价 -> 战吼类
                if (!effect.Cost.IsEmpty &&
                    effect.Cost.ResourceCosts.Count > 0)
                {
                    return EffectCostType.Activated;
                }

                // 检查是否有速度提升代价（通常是启动式的特征）
                if (effect.Cost.SpeedBoosts.Count > 0)
                {
                    return EffectCostType.Activated;
                }

                return EffectCostType.Battlecry;
            }

            // 触发式效果
            if (effect.IsTriggeredEffect)
            {
                return EffectCostType.Triggered;
            }

            return EffectCostType.Battlecry;
        }

        /// <summary>
        /// 将EffectData转换为EffectDefinition
        /// </summary>
        private EffectDefinition ConvertToEffectDefinition(EffectData effectData, CardData cardData)
        {
            var def = new EffectDefinition
            {
                Id = effectData.Abbreviation,
                DisplayName = effectData.Abbreviation,
                Description = effectData.Description,
                BaseSpeed = (int)effectData.Speed,
                SourceCardId = cardData.ID
            };

            // 根据缩写推断触发时点和效果
            // 这里需要根据实际的缩写系统来实现
            // 暂时使用默认值
            def.TriggerTiming = TriggerTiming.Activate_Active;

            // 添加原子效果
            def.Effects.Add(new AtomicEffectInstance
            {
                Type = ParseEffectType(effectData.Abbreviation),
                Value = (int)effectData.Parameters,
                ManaTypeParam = effectData.ManaType
            });

            return def;
        }

        /// <summary>
        /// 解析效果类型
        /// </summary>
        private AtomicEffectType ParseEffectType(string abbreviation)
        {
            return abbreviation?.ToUpper() switch
            {
                "DMG" => AtomicEffectType.DealDamage,
                "HEAL" => AtomicEffectType.Heal,
                "DRAW" => AtomicEffectType.DrawCard,
                "DISCARD" => AtomicEffectType.DiscardCard,
                "DESTROY" => AtomicEffectType.Destroy,
                "EXILE" => AtomicEffectType.Exile,
                "BUFF_PWR" => AtomicEffectType.ModifyPower,
                "BUFF_LIFE" => AtomicEffectType.ModifyLife,
                "TAP" => AtomicEffectType.Tap,
                "UNTAP" => AtomicEffectType.Untap,
                "NEGATE" => AtomicEffectType.NegateEffect,
                _ => AtomicEffectType.DealDamage
            };
        }

        /// <summary>
        /// 评估启动式能力的平衡性
        /// </summary>
        public string EvaluateActivatedAbilityBalance(EffectDefinition effect)
        {
            var result = CalculateEffectValue(effect);
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"启动式能力评估: {effect.DisplayName}");
            sb.AppendLine($"  效果价值: {result.TotalValue:F2} BRU");
            sb.AppendLine($"  代价价值: {result.CostValue:F2} BRU");
            sb.AppendLine($"  净价值: {result.TotalValue + result.CostValue:F2} BRU");

            float netValue = result.TotalValue + result.CostValue;

            if (netValue > 0.5f)
            {
                sb.AppendLine("  ⚠️ 警告: 净价值过高，建议增加代价或降低效果");
            }
            else if (netValue < -0.5f)
            {
                sb.AppendLine("  ⚠️ 警告: 净价值过低，可能不值得使用");
            }
            else
            {
                sb.AppendLine("  ✓ 平衡性良好");
            }

            return sb.ToString();
        }
    }

    #endregion

    #region 效果定义扩展

    /// <summary>
    /// EffectDefinition 扩展
    /// 添加费用类型标记和价值缓存
    /// </summary>
    public partial class EffectDefinition
    {
        /// <summary>
        /// 效果费用类型
        /// </summary>
        public EffectCostType CostType { get; set; } = EffectCostType.Battlecry;

        /// <summary>
        /// 缓存的价值结果
        /// </summary>
        [NonSerialized]
        private EffectValueResult _cachedValueResult;

        /// <summary>
        /// 缓存时的配置版本号
        /// </summary>
        [NonSerialized]
        private int _cachedConfigVersion = -1;

        /// <summary>
        /// 缓存时的定义哈希
        /// </summary>
        [NonSerialized]
        private string _cachedDefinitionHash;

        /// <summary>
        /// 获取效果价值（带缓存一致性检查）
        /// </summary>
        public EffectValueResult GetValueResult()
        {
            var currentConfigVersion = ValueSystemConfigManager.Instance.ConfigVersion;
            var currentHash = ComputeDefinitionHash();

            // 检查是否需要重新计算
            if (_cachedValueResult == null ||
                _cachedConfigVersion != currentConfigVersion ||
                _cachedDefinitionHash != currentHash)
            {
                var calculator = new ValueCalculator();
                _cachedValueResult = calculator.CalculateEffectValue(this);
                _cachedConfigVersion = currentConfigVersion;
                _cachedDefinitionHash = currentHash;
            }

            return _cachedValueResult;
        }

        /// <summary>
        /// 清除缓存的价值
        /// </summary>
        public void ClearCachedValue()
        {
            _cachedValueResult = null;
            _cachedConfigVersion = -1;
            _cachedDefinitionHash = null;
        }

        /// <summary>
        /// 计算定义哈希（用于检测定义是否变化）
        /// </summary>
        private string ComputeDefinitionHash()
        {
            // 基于关键属性计算哈希
            var hashBuilder = new System.Text.StringBuilder();
            hashBuilder.Append(Id ?? "null");
            hashBuilder.Append("|");
            hashBuilder.Append(BaseSpeed);
            hashBuilder.Append("|");
            hashBuilder.Append((int)TriggerTiming);
            hashBuilder.Append("|");
            hashBuilder.Append(Cost?.GetHashCode() ?? 0);
            hashBuilder.Append("|");
            hashBuilder.Append(Effects?.Count ?? 0);

            // 包含原子效果的简要信息
            if (Effects != null)
            {
                foreach (var effect in Effects)
                {
                    hashBuilder.Append("|");
                    hashBuilder.Append((int)effect.Type);
                    hashBuilder.Append(":");
                    hashBuilder.Append(effect.Value);
                }
            }

            return hashBuilder.ToString();
        }

        /// <summary>
        /// 获取建议费用（仅对战吼类有意义）
        /// </summary>
        public int GetSuggestedCost()
        {
            return GetValueResult().GetSuggestedCost();
        }
    }

    #endregion

    #region 效果构建器扩展

    /// <summary>
    /// EffectBuilder 扩展
    /// 添加费用类型和价值评估方法
    /// </summary>
    public partial class EffectBuilder
    {
        /// <summary>
        /// 设置效果费用类型
        /// </summary>
        public EffectBuilder WithCostType(EffectCostType costType)
        {
            _definition.CostType = costType;
            return this;
        }

        /// <summary>
        /// 设置为战吼类效果（费用计入卡牌费用）
        /// </summary>
        public EffectBuilder AsBattlecry()
        {
            _definition.CostType = EffectCostType.Battlecry;
            return this;
        }

        /// <summary>
        /// 设置为启动式效果（费用单独支付）
        /// </summary>
        public EffectBuilder AsActivated()
        {
            _definition.CostType = EffectCostType.Activated;
            return this;
        }

        /// <summary>
        /// 设置为触发式效果
        /// </summary>
        public EffectBuilder AsTriggered()
        {
            _definition.CostType = EffectCostType.Triggered;
            return this;
        }

        /// <summary>
        /// 设置为静态效果
        /// </summary>
        public EffectBuilder AsStatic()
        {
            _definition.CostType = EffectCostType.Static;
            return this;
        }

        /// <summary>
        /// 评估效果价值
        /// </summary>
        public EffectValueResult EvaluateValue()
        {
            var calculator = new ValueCalculator();
            return calculator.CalculateEffectValue(_definition);
        }

        /// <summary>
        /// 构建并返回价值评估
        /// </summary>
        public (EffectDefinition Definition, EffectValueResult ValueResult) BuildWithValue()
        {
            var definition = Build();
            var valueResult = definition.GetValueResult();
            return (definition, valueResult);
        }
    }

    #endregion

    #region 卡牌构建器

    /// <summary>
    /// 卡牌构建器
    /// 用于构建卡牌并评估总价��
    /// </summary>
    public class CardBuilder
    {
        private CardType _cardType;
        private string _cardName;
        private string _illustration;
        private int? _power;
        private int? _life;
        private bool _isLegendary;
        private List<EffectDefinition> _effects = new List<EffectDefinition>();
        private Dictionary<int, float> _cost = new Dictionary<int, float>();

        private ValueCalculator _calculator;

        public CardBuilder()
        {
            _calculator = new ValueCalculator();
        }

        /// <summary>
        /// 设置卡牌类型
        /// </summary>
        public CardBuilder WithType(CardType type)
        {
            _cardType = type;
            return this;
        }

        /// <summary>
        /// 设置卡牌名称
        /// </summary>
        public CardBuilder WithName(string name)
        {
            _cardName = name;
            return this;
        }

        /// <summary>
        /// 设置立绘
        /// </summary>
        public CardBuilder WithIllustration(string illustration)
        {
            _illustration = illustration;
            return this;
        }

        /// <summary>
        /// 设置攻击力
        /// </summary>
        public CardBuilder WithPower(int power)
        {
            _power = power;
            return this;
        }

        /// <summary>
        /// 设置生命值
        /// </summary>
        public CardBuilder WithLife(int life)
        {
            _life = life;
            return this;
        }

        /// <summary>
        /// 设置为传奇
        /// </summary>
        public CardBuilder AsLegendary()
        {
            _isLegendary = true;
            return this;
        }

        /// <summary>
        /// 添加效果
        /// </summary>
        public CardBuilder AddEffect(EffectDefinition effect)
        {
            _effects.Add(effect);
            return this;
        }

        /// <summary>
        /// 添加效果（使用构建器）
        /// </summary>
        public CardBuilder AddEffect(Action<EffectBuilder> buildAction)
        {
            var builder = new EffectBuilder();
            buildAction(builder);
            _effects.Add(builder.Build());
            return this;
        }

        /// <summary>
        /// 设置费用
        /// </summary>
        public CardBuilder WithCost(ManaType type, float amount)
        {
            _cost[(int)type] = amount;
            return this;
        }

        /// <summary>
        /// 添加费用
        /// </summary>
        public CardBuilder AddCost(ManaType type, float amount)
        {
            if (!_cost.ContainsKey((int)type))
                _cost[(int)type] = 0;
            _cost[(int)type] += amount;
            return this;
        }

        /// <summary>
        /// 评估卡牌价值
        /// </summary>
        public CardValueResult EvaluateValue()
        {
            return _calculator.CalculateCardValue(_cardType, _power, _life, _effects);
        }

        /// <summary>
        /// 构建卡牌数据
        /// </summary>
        public CardData Build()
        {
            var cardData = new CardData
            {
                CardType = _cardType,
                CardName = _cardName ?? "Unnamed Card",
                Illustration = _illustration,
                Power = _power,
                Life = _life,
                IsLegendary = _isLegendary,
                Cost = new Dictionary<int, float>(_cost)
            };

            // 转换效果
            foreach (var effect in _effects)
            {
                cardData.Effects.Add(ConvertToEffectData(effect));
            }

            return cardData;
        }

        /// <summary>
        /// 构建并返回价值评估
        /// </summary>
        public (CardData CardData, CardValueResult ValueResult) BuildWithValue()
        {
            var valueResult = EvaluateValue();
            var cardData = Build();
            return (cardData, valueResult);
        }

        /// <summary>
        /// 自动设置建议费用
        /// </summary>
        public CardBuilder AutoSetCost()
        {
            var valueResult = EvaluateValue();
            int suggestedCost = valueResult.SuggestedCost;

            // 将建议费用转换为灰色法力
            _cost.Clear();
            if (suggestedCost > 0)
            {
                _cost[(int)ManaType.灰色] = suggestedCost;
            }

            return this;
        }

        private EffectData ConvertToEffectData(EffectDefinition effect)
        {
            return new EffectData
            {
                Abbreviation = effect.DisplayName ?? effect.Id,
                Description = effect.Description,
                Speed = (EffectSpeed)effect.BaseSpeed,
                Parameters = effect.Effects.FirstOrDefault()?.Value ?? 0,
                ManaType = effect.Effects.FirstOrDefault()?.ManaTypeParam ?? ManaType.灰色,
                Initiative = effect.ActivationType == EffectActivationType.Voluntary
            };
        }

        /// <summary>
        /// 创建生物卡构建器
        /// </summary>
        public static CardBuilder Creature()
        {
            return new CardBuilder().WithType(CardType.生物);
        }

        /// <summary>
        /// 创建术法卡构建器
        /// </summary>
        public static CardBuilder Spell()
        {
            return new CardBuilder().WithType(CardType.术法);
        }

        /// <summary>
        /// 创建传奇卡构建器
        /// </summary>
        public static CardBuilder Legend()
        {
            return new CardBuilder().WithType(CardType.传奇).AsLegendary();
        }

        /// <summary>
        /// 创建领域卡构建器
        /// </summary>
        public static CardBuilder Field()
        {
            return new CardBuilder().WithType(CardType.领域);
        }
    }

    #endregion
}
