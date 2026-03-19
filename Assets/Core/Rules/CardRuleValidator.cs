using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using cfg;
using CardCore.Data;

namespace CardCore
{
    /// <summary>
    /// 规则检查器接口
    /// </summary>
    public interface IRuleChecker
    {
        /// <summary>
        /// 规则ID
        /// </summary>
        string RuleId { get; }

        /// <summary>
        /// 规则名称（用于显示）
        /// </summary>
        string RuleName { get; }

        /// <summary>
        /// 检查规则
        /// </summary>
        bool CheckRule(CardData card, out string errorMessage);
    }

    /// <summary>
    /// 卡牌规则验证器
    /// 用于验证卡牌数据是否符合游戏规则
    /// </summary>
    public class CardRuleValidator
    {
        private static CardRuleValidator _instance;
        public static CardRuleValidator Instance => _instance ??= new CardRuleValidator();

        // 规则字典
        private readonly Dictionary<string, IRuleChecker> _rules = new Dictionary<string, IRuleChecker>();

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private CardRuleValidator()
        {
            InitializeRules();
        }

        /// <summary>
        /// 初始化所有规则
        /// </summary>
        private void InitializeRules()
        {
            // 基础规则
            RegisterRule(new NameNotEmptyRule());
            RegisterRule(new CardTypeRule());
            RegisterRule(new CostTotalRule());
            RegisterRule(new InitiativeEffectRule());

            // 怪兽/传奇规则
            RegisterRule(new CombatStatsRequiredRule());
            RegisterRule(new CombatStatsMinMaxRule());
            RegisterRule(new CombatStatsSumRule());

            // 法术/领域规则
            RegisterRule(new NonCombatCardRule());
            RegisterRule(new SpellRequiresEffectRule());

            // 费用规则
            RegisterRule(new CostManaColorRule());
            RegisterRule(new CostPerManaTypeRule());

            // 效果规则
            RegisterRule(new EffectAbbreviationUniqueRule());
            RegisterRule(new EffectDescriptionNotEmptyRule());
        }

        /// <summary>
        /// 注册规则
        /// </summary>
        private void RegisterRule(IRuleChecker rule)
        {
            if (rule == null) return;
            if (string.IsNullOrEmpty(rule.RuleId)) return;

            _rules[rule.RuleId] = rule;
        }

        /// <summary>
        /// 验证所有规则
        /// </summary>
        public bool ValidateAllRules(CardData card, out List<string> errors)
        {
            errors = new List<string>();

            foreach (var rule in _rules.Values)
            {
                if (rule.CheckRule(card, out string error))
                {
                    continue;
                }
                errors.Add(error);
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// 验证指定规则
        /// </summary>
        public bool ValidateRule(string ruleId, CardData card, out string error)
        {
            error = null;

            if (_rules.TryGetValue(ruleId, out var rule))
            {
                return rule.CheckRule(card, out error);
            }

            error = $"规则 '{ruleId}' 不存在";
            return false;
        }

        // ==================== 基础规则 ====================

        /// <summary>
        /// 卡牌名称不能为空
        /// </summary>
        private class NameNotEmptyRule : IRuleChecker
        {
            public string RuleId => "NameNotEmpty";
            public string RuleName => "卡牌名称不能为空";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;
                if (string.IsNullOrWhiteSpace(card.CardName))
                {
                    errorMessage = "卡牌名称不能为空";
                    return false;
                }
                if (card.CardName.Length > 50)
                {
                    errorMessage = "卡牌名称不能超过50个字符";
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 卡牌类型规则
        /// </summary>
        private class CardTypeRule : IRuleChecker
        {
            public string RuleId => "CardType";
            public string RuleName => "卡牌类型";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;
                // 确保卡牌类型在有效范围内
                if (!Enum.IsDefined(typeof(CardType), card.CardType))
                {
                    errorMessage = "无效的卡牌类型";
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 总费用规则
        /// </summary>
        private class CostTotalRule : IRuleChecker
        {
            public string RuleId => "CostTotal";
            public string RuleName => "总费用限制";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;
                float totalCost = card.TotalCost;

                // 根据卡牌类型设置不同的费用上限
                float maxCost = GetMaxCostForCardType(card.CardType);

                if (totalCost > maxCost)
                {
                    errorMessage = $"{card.CardType}卡牌的总费用不能超过{maxCost}（当前：{totalCost}）";
                    return false;
                }

                if (totalCost <= 0 && card.CardType != CardType.领域)
                {
                    errorMessage = "卡牌必须至少消耗1点法力";
                    return false;
                }

                return true;
            }

            private float GetMaxCostForCardType(CardType type)
            {
                switch (type)
                {
                    case CardType.生物:
                    case CardType.传奇:
                        return 10f;
                    case CardType.术法:
                        return 8f;
                    case CardType.领域:
                        return 15f;  // 领域卡可以有0费用
                    default:
                        return 10f;
                }
            }
        }

        /// <summary>
        /// 至少一个主动效果规则
        /// </summary>
        private class InitiativeEffectRule : IRuleChecker
        {
            public string RuleId => "InitiativeEffect";
            public string RuleName => "主动效果要求";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;

                // 领域卡不需要主动效果
                if (card.CardType == CardType.领域)
                {
                    return true;
                }

                // 检查是否有主动效果
                if (!card.HasActiveEffect)
                {
                    errorMessage = "卡牌必须至少有一个主动效果才能使用";
                    return false;
                }

                return true;
            }
        }

        // ==================== 怪兽/传奇规则 ====================

        /// <summary>
        /// 战斗单位必须有战斗属性规则
        /// </summary>
        private class CombatStatsRequiredRule : IRuleChecker
        {
            public string RuleId => "CombatStatsRequired";
            public string RuleName => "战斗属性要求";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;

                // 只有战斗单位需要战斗属性
                if (card.CardType == CardType.生物 || card.CardType == CardType.传奇)
                {
                    if (!card.HasCombatStats)
                    {
                        errorMessage = "怪兽和传奇卡牌必须至少有一个战斗属性（生命或攻击）";
                        return false;
                    }
                }
                else
                {
                    // 非战斗单位不能有战斗属性
                    if (card.HasCombatStats)
                    {
                        errorMessage = "法术和领域卡牌不能有战斗属性";
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// 战斗属性最小值规则
        /// </summary>
        private class CombatStatsMinMaxRule : IRuleChecker
        {
            public string RuleId => "CombatStatsMinMax";
            public string RuleName => "战斗属性范围";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;

                if (!card.HasCombatStats)
                    return true;

                // 最小值检查
                if (card.Life.HasValue && card.Life.Value < 1)
                {
                    errorMessage = "生命值不能小于1";
                    return false;
                }

                if (card.Power.HasValue && card.Power.Value < 0)
                {
                    errorMessage = "攻击力不能小于0";
                    return false;
                }

                // 最大值检查（基于总费用）
                float maxStat = card.TotalCost * 2 + 5;  // 费用*2 + 5

                if (card.Life.HasValue && card.Life.Value > maxStat)
                {
                    errorMessage = $"生命值不能超过{maxStat}（基于费用{card.TotalCost}计算）";
                    return false;
                }

                if (card.Power.HasValue && card.Power.Value > maxStat)
                {
                    errorMessage = $"攻击力不能超过{maxStat}（基于费用{card.TotalCost}计算）";
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// 战斗属性总和规则
        /// </summary>
        private class CombatStatsSumRule : IRuleChecker
        {
            public string RuleId => "CombatStatsSum";
            public string RuleName => "战斗属性总和";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;

                if (!card.HasCombatStats)
                    return true;

                int life = card.Life ?? 0;
                int power = card.Power ?? 0;
                int sum = life + power;

                // 总和不能超过费用*3 + 5
                float maxSum = card.TotalCost * 3 + 5;

                if (sum > maxSum)
                {
                    errorMessage = $"生命值与攻击力之和不能超过{maxSum}（当前：{sum}）";
                    return false;
                }

                return true;
            }
        }

        // ==================== 法术/领域规则 ====================

        /// <summary>
        /// 非战斗卡牌规则
        /// </summary>
        private class NonCombatCardRule : IRuleChecker
        {
            public string RuleId => "NonCombatCard";
            public string RuleName => "非战斗卡牌规则";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;

                // 非战斗卡牌不能有战斗属性
                if (card.CardType == CardType.术法 || card.CardType == CardType.领域)
                {
                    if (card.HasCombatStats)
                    {
                        errorMessage = "法术和领域卡牌不能有战斗属性";
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// 法术必须有效果规则
        /// </summary>
        private class SpellRequiresEffectRule : IRuleChecker
        {
            public string RuleId => "SpellRequiresEffect";
            public string RuleName => "法术效果要求";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;

                if (card.CardType == CardType.术法)
                {
                    if (card.Effects == null || card.Effects.Count == 0)
                    {
                        errorMessage = "法术卡牌必须至少有一个效果";
                        return false;
                    }
                }

                return true;
            }
        }

        // ==================== 费用规则 ====================

        /// <summary>
        /// 费用颜色规则
        /// </summary>
        private class CostManaColorRule : IRuleChecker
        {
            public string RuleId => "CostManaColor";
            public string RuleName => "费用法力颜色";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;

                if (card.Cost == null || card.Cost.Count == 0)
                {
                    errorMessage = "卡牌必须设置法力消耗";
                    return false;
                }

                // 领域卡可以有多种颜色
                if (card.CardType == CardType.领域)
                {
                    return true;
                }

                // 其他卡牌建议使用单一颜色（但不强制）
                return true;
            }
        }

        /// <summary>
        /// 每种法力类型费用规则
        /// </summary>
        private class CostPerManaTypeRule : IRuleChecker
        {
            public string RuleId => "CostPerManaType";
            public string RuleName => "单色费用限制";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;

                if (card.Cost == null || card.Cost.Count == 0)
                    return true;

                // 每种颜色的费用不能超过5
                foreach (var cost in card.Cost)
                {
                    if (cost.Value > 5)
                    {
                        ManaType manaType = (ManaType)cost.Key;
                        errorMessage = $"单色{manaType}法力消耗不能超过5（当前：{cost.Value}）";
                        return false;
                    }
                }

                return true;
            }
        }

        // ==================== 效果规则 ====================

        /// <summary>
        /// 效果缩写唯一规则
        /// </summary>
        private class EffectAbbreviationUniqueRule : IRuleChecker
        {
            public string RuleId => "EffectAbbreviationUnique";
            public string RuleName => "效果缩写唯一";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;

                if (card.Effects == null || card.Effects.Count == 0)
                    return true;

                var abbreviations = new HashSet<string>();
                foreach (var effect in card.Effects)
                {
                    if (string.IsNullOrEmpty(effect.Abbreviation))
                    {
                        errorMessage = "效果缩写不能为空";
                        return false;
                    }

                    string key = effect.Abbreviation.ToUpper();
                    if (abbreviations.Contains(key))
                    {
                        errorMessage = $"卡牌中不能重复添加相同的效果（缩写：{effect.Abbreviation}）";
                        return false;
                    }
                    abbreviations.Add(key);
                }

                return true;
            }
        }

        /// <summary>
        /// 效果描述不能为空规则
        /// </summary>
        private class EffectDescriptionNotEmptyRule : IRuleChecker
        {
            public string RuleId => "EffectDescriptionNotEmpty";
            public string RuleName => "效果描述要求";

            public bool CheckRule(CardData card, out string errorMessage)
            {
                errorMessage = null;

                if (card.Effects == null || card.Effects.Count == 0)
                    return true;

                foreach (var effect in card.Effects)
                {
                    if (string.IsNullOrWhiteSpace(effect.Description))
                    {
                        errorMessage = "效果描述不能为空";
                        return false;
                    }

                    if (effect.Description.Length > 200)
                    {
                        errorMessage = "效果描述不能超过200个字符";
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>
    /// 规则检查结果
    /// </summary>
    [Serializable]
    public class RuleCheckResult
    {
        public bool IsValid;
        public List<string> Errors;
        public List<string> Warnings;

        public RuleCheckResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }
    }
}
