using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 重复检测工具类
    /// 用于检测卡牌和效果是否存在重复
    /// </summary>
    public static class DuplicateChecker
    {
        /// <summary>
        /// 检测卡牌是否重复
        /// </summary>
        /// <param name="card">待检测的卡牌数据</param>
        /// <param name="existingName">如果重复，输出已存在的卡牌名称</param>
        /// <returns>是否重复</returns>
        public static bool CheckCardDuplicate(CardData card, out string existingName)
        {
                existingName = null;

                if (card == null)
                {
                    return false;
                }

                // 生成ID
                string cardId = card.CalculateID();

                // 检查注册表中是否存在相同ID的卡牌
                var existingCard = CardDataRegistry.Instance.GetCard(cardId);
                if (existingCard != null)
                {
                    existingName = existingCard.CardName;
                    return true;
                }

                // 如果有名称，也检查名称是否重复
                if (!string.IsNullOrEmpty(card.CardName))
                {
                    var cardByName = CardDataRegistry.Instance.GetCardByName(card.CardName);
                    if (cardByName != null && cardByName.ID != card.ID)
                    {
                        existingName = cardByName.CardName;
                        return true;
                    }
                }

                return false;
        }

        /// <summary>
        /// 检测效果是否重复
        /// </summary>
        /// <param name="effect">待检测的效果数据</param>
        /// <param name="existingName">如果重复，输出已存在的效果名称</param>
        /// <returns>是否重复</returns>
        public static bool CheckEffectDuplicate(EffectDefinitionData effect, out string existingName)
        {
                existingName = null;

                if (effect == null)
                {
                    return false;
                }

                // 生成哈希
                string effectHash = CalculateEffectHash(effect);

                // 检查存储中是否存在相同哈希的效果
                var cachedEffects = EffectDefinitionStorage.Instance.GetCachedEffects();
                foreach (var cachedEffect in cachedEffects)
                {
                    string cachedHash = CalculateEffectHash(cachedEffect);
                    if (cachedHash == effectHash)
                    {
                        existingName = cachedEffect.DisplayName;
                        return true;
                    }
                }

                return false;
        }

        /// <summary>
        /// 计算效果哈希值
        /// </summary>
        /// <param name="effect">效果数据</param>
        /// <returns>哈希字符串</returns>
        public static string CalculateEffectHash(EffectDefinitionData effect)
        {
                if (effect == null)
                {
                    return string.Empty;
                }

                // 使用关键属性生成哈希
                string content = $"{effect.DisplayName}|{effect.Description}|{effect.BaseSpeed}|{effect.ActivationType}|{effect.TriggerTiming}|{effect.Duration}";

                // 添加原子效果
                if (effect.Effects != null)
                {
                    foreach (var atomicEffect in effect.Effects)
                    {
                        content += $"|{atomicEffect.Type}|{atomicEffect.Value}|{atomicEffect.Value2}|{atomicEffect.StringValue}";
                    }
                }

                // 添加发动条件
                if (effect.ActivationConditions != null)
                {
                    foreach (var condition in effect.ActivationConditions)
                    {
                        content += $"|{condition.Type}|{condition.Value}|{condition.Value2}";
                    }
                }

                // 添加代价
                if (effect.Cost != null)
                {
                    if (effect.Cost.ElementCosts != null)
                    {
                        foreach (var elementCost in effect.Cost.ElementCosts)
                        {
                            content += $"|EC:{elementCost.ManaType}|{elementCost.Amount}";
                        }
                    }
                    if (effect.Cost.ResourceCosts != null)
                    {
                        foreach (var resourceCost in effect.Cost.ResourceCosts)
                        {
                            content += $"|RC:{resourceCost.FromZone}|{resourceCost.ToZone}|{resourceCost.Count}";
                        }
                    }
                }

                // 添加目标选择器
                if (effect.TargetSelector != null)
                {
                    content += $"|TS:{effect.TargetSelector.PrimaryTarget}|{effect.TargetSelector.MinTargets}|{effect.TargetSelector.MaxTargets}";
                }

                uint hash = MurmurHash3.Hash32(content);
                return hash.ToString("X8");
        }

        /// <summary>
        /// 检测卡牌内容是否重复（不包含名称）
        /// </summary>
        /// <param name="card">待检测的卡牌数据</param>
        /// <param name="existingName">如果重复，输出已存在的卡牌名称</param>
        /// <returns>是否重复</returns>
        public static bool CheckCardContentDuplicate(CardData card, out string existingName)
        {
                existingName = null;

                if (card == null)
                {
                    return false;
                }

                // 生成内容哈希（不包含名称）
                string contentHash = CalculateCardContentHash(card);

                // 遍历所有已存在的卡牌，检查内容哈希
                foreach (var existingCard in CardDataRegistry.Instance.AllCards)
                {
                    if (existingCard.ID == card.ID)
                    {
                        continue; // 跳过自身
                    }

                    string existingContentHash = CalculateCardContentHash(existingCard);
                    if (existingContentHash == contentHash)
                    {
                        existingName = existingCard.CardName;
                        return true;
                    }
                }

                return false;
        }

        /// <summary>
        /// 计算卡牌内容哈希（不包含名称）
        /// </summary>
        /// <param name="card">卡牌数据</param>
        /// <returns>哈希字符串</returns>
        private static string CalculateCardContentHash(CardData card)
        {
                if (card == null)
                {
                    return string.Empty;
                }

                // 使用卡牌内容生成哈希，不包含名称
                string content = $"{(int)card.CardType}|{card.Power}|{card.Life}|{card.IsLegendary}";

                // 添加效果
                if (card.Effects != null)
                {
                    foreach (var effect in card.Effects)
                    {
                        content += $"|{effect.Abbreviation}|{effect.Initiative}|{effect.Parameters}|{(int)effect.Speed}|{(int)effect.ManaType}|{effect.Description}";
                    }
                }

                // 添加费用
                if (card.Cost != null)
                {
                    foreach (var kvp in card.Cost)
                    {
                        content += $"|C:{kvp.Key}|{kvp.Value}";
                    }
                }

                uint hash = MurmurHash3.Hash32(content);
                return hash.ToString("X8");
        }
    }
}
