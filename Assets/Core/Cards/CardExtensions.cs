using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// Card 类的扩展方法
    /// 提供通过接口类型检查和获取卡牌属性的能力
    /// </summary>
    public static class CardExtensions
    {
        /// <summary>
        /// 尝试获取指定类型的属性接口
        /// </summary>
        public static bool TryGetProperty<T>(this Card card, out T property) where T : class
        {
            property = null;

            // 如果 Card 本身就是目标接口类型
            if (card is T cardAsT)
            {
                property = cardAsT;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查卡牌是否具有指定的属性接口
        /// </summary>
        public static bool HasProperty<T>(this Card card) where T : class
        {
            return card is T;
        }

        /// <summary>
        /// 获取指定类型的属性接口，如果不存在则返回 null
        /// </summary>
        public static T GetProperty<T>(this Card card) where T : class
        {
            return card as T;
        }

        /// <summary>
        /// 尝试获取立绘
        /// </summary>
        public static bool TryGetIllustration(this Card card, out string illustration)
        {
            illustration = null;
            if (card is IHasIllustration hasIllustration)
            {
                illustration = hasIllustration.Illustration;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取卡牌名称
        /// </summary>
        public static bool TryGetName(this Card card, out string name)
        {
            name = null;
            if (card is IHasName hasName)
            {
                name = hasName.CardName;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取生命值
        /// </summary>
        public static bool TryGetLife(this Card card, out int life)
        {
            life = 0;
            if (card is IHasLife hasLife)
            {
                life = hasLife.Life;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取攻击力
        /// </summary>
        public static bool TryGetPower(this Card card, out int power)
        {
            power = 0;
            if (card is IHasPower hasPower)
            {
                power = hasPower.Power;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取费用
        /// </summary>
        public static bool TryGetCost(this Card card, out Dictionary<int, float> cost)
        {
            cost = null;
            if (card is IHasCost hasCost)
            {
                cost = hasCost.Cost;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取效果列表
        /// </summary>
        public static bool TryGetEffects(this Card card, out List<Effect_table> effects)
        {
            effects = null;
            if (card is IHasEffects hasEffects)
            {
                effects = hasEffects.Effects;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// CardData 类的扩展方法
    /// </summary>
    public static class CardDataExtensions
    {
        /// <summary>
        /// 验证卡牌数据
        /// </summary>
        public static bool Validate(this CardData card, out List<string> errors)
        {
            return CardRuleValidator.Instance.ValidateAllRules(card, out errors);
        }

        /// <summary>
        /// 验证卡牌数据（返回第一个错误）
        /// </summary>
        public static bool Validate(this CardData card, out string error)
        {
            bool isValid = CardRuleValidator.Instance.ValidateAllRules(card, out var errors);
            error = isValid ? null : errors.FirstOrDefault();
            return isValid;
        }

        /// <summary>
        /// 检查是否有效（返回单个错误或null）
        /// </summary>
        public static string GetValidationError(this CardData card)
        {
            if (Validate(card, out string error))
            {
                return null;
            }
            return error;
        }
    }
}
