using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using cfg;
using CardCore.Data;

namespace CardCore
{
    /// <summary>
    /// 卡牌构建器 - 用于逐步构建卡牌数据
    /// 采用链式调用API
    /// </summary>
    public class CardTemplateBuilder
    {
        private CardData _cardData;
        private bool _isBuilt = false;

        /// <summary>
        /// 创建新的构建器
        /// </summary>
        public CardTemplateBuilder(CardType cardType)
        {
            _cardData = new CardData
            {
                CardType = cardType,
                CardName = "Unnamed Card",
                CreationTime = DateTime.Now
            };
        }

        /// <summary>
        /// 从现有卡牌数据创建构建器（用于编辑）
        /// </summary>
        public CardTemplateBuilder(CardData existingData)
        {
            if (existingData == null)
                throw new ArgumentNullException(nameof(existingData));

            // 深拷贝 - 使用JSON序列化
            string json = JsonUtility.ToJson(existingData);
            _cardData = JsonUtility.FromJson<CardData>(json);

            // 触发反序列化后处理（恢复Cost等）
            _cardData.OnAfterDeserialize();
        }

        /// <summary>
        /// 设置卡牌名称
        /// </summary>
        public CardTemplateBuilder SetName(string name)
        {
            CheckNotBuilt();
            _cardData.CardName = name;
            return this;
        }

        /// <summary>
        /// 设置立绘
        /// </summary>
        public CardTemplateBuilder SetIllustration(string illustration)
        {
            CheckNotBuilt();
            _cardData.Illustration = illustration;
            return this;
        }

        /// <summary>
        /// 设置生命值（仅战斗单位）
        /// </summary>
        public CardTemplateBuilder SetLife(int life)
        {
            CheckNotBuilt();
            _cardData.Life = life;
            return this;
        }

        /// <summary>
        /// 移除生命值
        /// </summary>
        public CardTemplateBuilder RemoveLife()
        {
            CheckNotBuilt();
            _cardData.Life = null;
            return this;
        }

        /// <summary>
        /// 设置攻击力（仅战斗单位）
        /// </summary>
        public CardTemplateBuilder SetPower(int power)
        {
            CheckNotBuilt();
            _cardData.Power = power;
            return this;
        }

        /// <summary>
        /// 移除攻击力
        /// </summary>
        public CardTemplateBuilder RemovePower()
        {
            CheckNotBuilt();
            _cardData.Power = null;
            return this;
        }

        /// <summary>
        /// 设置法力消耗
        /// </summary>
        public CardTemplateBuilder SetManaCost(ManaType manaType, float amount)
        {
            CheckNotBuilt();
            int key = (int)manaType;

            if (amount <= 0)
            {
                _cardData.Cost.Remove(key);
            }
            else
            {
                _cardData.Cost[key] = amount;
            }
            return this;
        }

        /// <summary>
        /// 批量设置法力消耗
        /// </summary>
        public CardTemplateBuilder SetManaCosts(Dictionary<ManaType, float> costs)
        {
            CheckNotBuilt();
            _cardData.Cost.Clear();

            foreach (var kvp in costs)
            {
                if (kvp.Value > 0)
                {
                    _cardData.Cost[(int)kvp.Key] = kvp.Value;
                }
            }
            return this;
        }

        /// <summary>
        /// 清空所有法力消耗
        /// </summary>
        public CardTemplateBuilder ClearManaCosts()
        {
            CheckNotBuilt();
            _cardData.Cost.Clear();
            return this;
        }

        /// <summary>
        /// 添加效果
        /// </summary>
        public CardTemplateBuilder AddEffect(EffectData effect)
        {
            CheckNotBuilt();
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            // 检查是否已存在相同缩写的效果
            var existing = _cardData.Effects.FirstOrDefault(e => e.Abbreviation == effect.Abbreviation);
            if (existing != null)
            {
                throw new InvalidOperationException($"Effect with abbreviation '{effect.Abbreviation}' already exists in card.");
            }

            _cardData.Effects.Add(effect);
            return this;
        }

        /// <summary>
        /// 从 Effect_table 添加效果
        /// </summary>
        public CardTemplateBuilder AddEffect(Effect_table effect)
        {
            CheckNotBuilt();
            return AddEffect(EffectData.FromEffectTable(effect));
        }

        /// <summary>
        /// 批量添加效果
        /// </summary>
        public CardTemplateBuilder AddEffects(IEnumerable<EffectData> effects)
        {
            CheckNotBuilt();
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));

            foreach (var effect in effects)
            {
                AddEffect(effect);
            }
            return this;
        }

        /// <summary>
        /// 批量添加效果（从 Effect_table）
        /// </summary>
        public CardTemplateBuilder AddEffects(IEnumerable<Effect_table> effects)
        {
            CheckNotBuilt();
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));

            foreach (var effect in effects)
            {
                AddEffect(effect);
            }
            return this;
        }

        /// <summary>
        /// 移除效果（通过缩写）
        /// </summary>
        public CardTemplateBuilder RemoveEffect(string abbreviation)
        {
            CheckNotBuilt();
            var effect = _cardData.Effects.FirstOrDefault(e => e.Abbreviation == abbreviation);
            if (effect != null)
            {
                _cardData.Effects.Remove(effect);
            }
            return this;
        }

        /// <summary>
        /// 清空所有效果
        /// </summary>
        public CardTemplateBuilder ClearEffects()
        {
            CheckNotBuilt();
            _cardData.Effects.Clear();
            return this;
        }

        /// <summary>
        /// 设置传奇状态
        /// </summary>
        public CardTemplateBuilder SetLegendary(bool isLegendary)
        {
            CheckNotBuilt();
            _cardData.IsLegendary = isLegendary;
            return this;
        }

        /// <summary>
        /// 获取当前构建的卡牌数据（不标记为完成）
        /// </summary>
        public CardData Peek()
        {
            return _cardData;
        }

        /// <summary>
        /// 构建最终的卡牌数据
        /// </summary>
        public CardData Build()
        {
            CheckNotBuilt();
            _isBuilt = true;

            // 自动生成ID
            _cardData.ID = _cardData.CalculateID();

            return _cardData;
        }

        /// <summary>
        /// 构建卡牌包装器（用于游戏运行时）
        /// </summary>
        public CardWrapper BuildWrapper()
        {
            CheckNotBuilt();
            _isBuilt = true;

            // 自动生成ID
            _cardData.ID = _cardData.CalculateID();

            return new CardWrapper(_cardData);
        }

        /// <summary>
        /// 重置构建状态，允许继续修改
        /// </summary>
        public CardTemplateBuilder Reset()
        {
            _isBuilt = false;
            return this;
        }

        /// <summary>
        /// 验证卡牌数据
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            errorMessage = null;

            // 检查名称
            if (string.IsNullOrWhiteSpace(_cardData.CardName))
            {
                errorMessage = "Card name cannot be empty.";
                return false;
            }

            // 检查卡牌类型对应的属性
            switch (_cardData.CardType)
            {
                case CardType.生物:
                case CardType.传奇:
                    if (!_cardData.HasCombatStats)
                    {
                        errorMessage = "Monster and Legend cards must have at least one combat stat (Life or Power).";
                        return false;
                    }
                    break;
            }

            // 检查是否有主动效果（根据规则）
            // 可以在这里添加更多验证规则

            return true;
        }

        private void CheckNotBuilt()
        {
            if (_isBuilt)
            {
                throw new InvalidOperationException("Card has already been built. Create a new builder or call Reset().");
            }
        }
    }

    /// <summary>
    /// 卡牌构建器工厂 - 用于快速创建常见卡牌
    /// </summary>
    public static class CardBuilderFactory
    {
        /// <summary>
        /// 创建基础怪兽卡
        /// </summary>
        public static CardTemplateBuilder CreateMonster(string name, int life, int power, ManaType mainMana, float mainCost)
        {
            return new CardTemplateBuilder(CardType.生物)
                .SetName(name)
                .SetLife(life)
                .SetPower(power)
                .SetManaCost(mainMana, mainCost);
        }

        /// <summary>
        /// 创建基础法术卡
        /// </summary>
        public static CardTemplateBuilder CreateMagic(string name, ManaType mainMana, float mainCost)
        {
            return new CardTemplateBuilder(CardType.术法)
                .SetName(name)
                .SetManaCost(mainMana, mainCost);
        }

        /// <summary>
        /// 创建基础领域卡
        /// </summary>
        public static CardTemplateBuilder CreateField(string name, ManaType mainMana, float mainCost)
        {
            return new CardTemplateBuilder(CardType.领域)
                .SetName(name)
                .SetManaCost(mainMana, mainCost);
        }

        /// <summary>
        /// 从模板创建
        /// </summary>
        public static CardTemplateBuilder FromTemplate(CardTemplate template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            var builder = new CardTemplateBuilder(template.CardType);
            var data = template.CreateCardData();

            builder.SetName(data.CardName);
            builder.SetIllustration(data.Illustration);
            if (data.Life.HasValue)
            {
                if (data.Life.Value > 0)
                    builder.SetLife(data.Life.Value);
            }
            if (data.Power.HasValue)
            {
                if (data.Power.Value > 0)
                    builder.SetPower(data.Power.Value);
            }
            builder.AddEffects(data.Effects);

            return builder;
        }
    }
}
