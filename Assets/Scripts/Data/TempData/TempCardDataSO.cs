using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardCore.Data
{
    /// <summary>
    /// 临时卡牌数据 ScriptableObject
    /// 用于存储示例卡牌数据，供编辑器测试使用
    /// </summary>
    [CreateAssetMenu(fileName = "TempCardData", menuName = "Synergy/Temp Data/Card Data")]
    public class TempCardDataSO : ScriptableObject
    {
        [Header("示例卡牌列表")]
        public List<TempCardData> cards = new List<TempCardData>();

        private void OnEnable()
        {
            if (cards.Count == 0)
            {
                InitializeSampleCards();
            }
        }

        private void InitializeSampleCards()
        {
            // 生物卡牌示例
            cards.Add(new TempCardData
            {
                id = "CREATURE_001",
                cardName = "火焰精灵",
                cardType = CardType.生物,
                isLegendary = false,
                power = 3,
                life = 2,
                manaCost = new List<ManaCostEntry>
                {
                    new ManaCostEntry { manaType = ManaType.红色, amount = 1 },
                    new ManaCostEntry { manaType = ManaType.灰色, amount = 1 }
                },
                description = "入场时对目标造成1点伤害。",
                effects = new List<TempEffectRef>
                {
                    new TempEffectRef { effectId = "EFFECT_DAMAGE_1", parameters = "1" }
                }
            });

            cards.Add(new TempCardData
            {
                id = "CREATURE_002",
                cardName = "水晶守卫",
                cardType = CardType.生物,
                isLegendary = false,
                power = 2,
                life = 4,
                manaCost = new List<ManaCostEntry>
                {
                    new ManaCostEntry { manaType = ManaType.蓝色, amount = 2 }
                },
                description = "防护：受到的法术伤害减半。",
                effects = new List<TempEffectRef>()
            });

            cards.Add(new TempCardData
            {
                id = "CREATURE_003",
                cardName = "森林德鲁伊",
                cardType = CardType.生物,
                isLegendary = false,
                power = 1,
                life = 1,
                manaCost = new List<ManaCostEntry>
                {
                    new ManaCostEntry { manaType = ManaType.绿色, amount = 1 }
                },
                description = "入场时回复2点生命。",
                effects = new List<TempEffectRef>
                {
                    new TempEffectRef { effectId = "EFFECT_HEAL_2", parameters = "2" }
                }
            });

            // 传奇生物示例
            cards.Add(new TempCardData
            {
                id = "LEGENDARY_001",
                cardName = "炽炎龙王·奥古斯都",
                cardType = CardType.传奇,
                isLegendary = true,
                power = 6,
                life = 6,
                manaCost = new List<ManaCostEntry>
                {
                    new ManaCostEntry { manaType = ManaType.红色, amount = 2 },
                    new ManaCostEntry { manaType = ManaType.灰色, amount = 4 }
                },
                description = "飞行，践踏\n入场时对所有敌方生物造成3点伤害。",
                effects = new List<TempEffectRef>
                {
                    new TempEffectRef { effectId = "EFFECT_AOE_DAMAGE_3", parameters = "3" }
                }
            });

            // 术法示例
            cards.Add(new TempCardData
            {
                id = "SPELL_001",
                cardName = "火焰冲击",
                cardType = CardType.术法,
                isLegendary = false,
                power = null,
                life = null,
                manaCost = new List<ManaCostEntry>
                {
                    new ManaCostEntry { manaType = ManaType.红色, amount = 1 },
                    new ManaCostEntry { manaType = ManaType.灰色, amount = 1 }
                },
                description = "对目标生物造成3点伤害。",
                effects = new List<TempEffectRef>
                {
                    new TempEffectRef { effectId = "EFFECT_DAMAGE_TARGET_3", parameters = "3" }
                }
            });

            cards.Add(new TempCardData
            {
                id = "SPELL_002",
                cardName = "时间扭曲",
                cardType = CardType.术法,
                isLegendary = false,
                power = null,
                life = null,
                manaCost = new List<ManaCostEntry>
                {
                    new ManaCostEntry { manaType = ManaType.蓝色, amount = 3 }
                },
                description = "抽两张牌。",
                effects = new List<TempEffectRef>
                {
                    new TempEffectRef { effectId = "EFFECT_DRAW_2", parameters = "2" }
                }
            });

            // 领域示例
            cards.Add(new TempCardData
            {
                id = "DOMAIN_001",
                cardName = "火山领地",
                cardType = CardType.领域,
                isLegendary = false,
                power = null,
                life = null,
                manaCost = new List<ManaCostEntry>
                {
                    new ManaCostEntry { manaType = ManaType.红色, amount = 1 }
                },
                description = "你施放的红色法术费用减少1。",
                effects = new List<TempEffectRef>
                {
                    new TempEffectRef { effectId = "EFFECT_COST_REDUCTION_RED", parameters = "1" }
                }
            });

            cards.Add(new TempCardData
            {
                id = "DOMAIN_002",
                cardName = "知识殿堂",
                cardType = CardType.领域,
                isLegendary = false,
                power = null,
                life = null,
                manaCost = new List<ManaCostEntry>
                {
                    new ManaCostEntry { manaType = ManaType.蓝色, amount = 1 }
                },
                description = "回合开始时，检视牌库顶的牌。",
                effects = new List<TempEffectRef>
                {
                    new TempEffectRef { effectId = "EFFECT_SCRY_1", parameters = "1" }
                }
            });
        }
    }

    [Serializable]
    public class TempCardData
    {
        public string id;
        public string cardName;
        public CardType cardType;
        public bool isLegendary;
        public int? power;
        public int? life;
        public string description;
        public List<ManaCostEntry> manaCost = new List<ManaCostEntry>();
        public List<TempEffectRef> effects = new List<TempEffectRef>();

        /// <summary>
        /// 获取总法力费用
        /// </summary>
        public int GetTotalCost()
        {
            int total = 0;
            foreach (var cost in manaCost)
            {
                total += cost.amount;
            }
            return total;
        }

        /// <summary>
        /// 获取法力费用字符串
        /// </summary>
        public string GetCostString()
        {
            var result = new System.Text.StringBuilder();
            foreach (var cost in manaCost)
            {
                string symbol = cost.manaType switch
                {
                    ManaType.灰色 => "{G}",
                    ManaType.红色 => "{R}",
                    ManaType.蓝色 => "{U}",
                    ManaType.绿色 => "{Gr}",
                    ManaType.白色 => "{W}",
                    ManaType.黑色 => "{B}",
                    _ => "{?}"
                };
                for (int i = 0; i < cost.amount; i++)
                {
                    result.Append(symbol);
                }
            }
            return result.ToString();
        }
    }

    [Serializable]
    public class ManaCostEntry
    {
        public ManaType manaType;
        public int amount;
    }

    [Serializable]
    public class TempEffectRef
    {
        public string effectId;
        public string parameters;
    }
}