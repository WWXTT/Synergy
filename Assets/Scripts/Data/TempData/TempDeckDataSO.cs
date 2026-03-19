using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardCore.Data
{
    /// <summary>
    /// 临时卡组数据 ScriptableObject
    /// 用于存储示例卡组数据，供卡组编辑器测试使用
    /// </summary>
    [CreateAssetMenu(fileName = "TempDeckData", menuName = "Synergy/Temp Data/Deck Data")]
    public class TempDeckDataSO : ScriptableObject
    {
        [Header("示例卡组列表")]
        public List<TempDeckData> decks = new List<TempDeckData>();

        private void OnEnable()
        {
            if (decks.Count == 0)
            {
                InitializeSampleDecks();
            }
        }

        private void InitializeSampleDecks()
        {
            // 红色快攻卡组
            decks.Add(new TempDeckData
            {
                id = "DECK_001",
                deckName = "红焰突袭",
                description = "以快速打击为主的红色快攻卡组",
                mainDeckCards = new List<DeckCardEntry>
                {
                    new DeckCardEntry { cardId = "CREATURE_001", quantity = 4 },
                    new DeckCardEntry { cardId = "SPELL_001", quantity = 4 },
                    new DeckCardEntry { cardId = "CREATURE_003", quantity = 2 }
                },
                extraDeckCards = new List<DeckCardEntry>(),
                colors = new List<ManaType> { ManaType.红色 }
            });

            // 蓝色控制卡组
            decks.Add(new TempDeckData
            {
                id = "DECK_002",
                deckName = "深海控制",
                description = "以控制和抽牌为主的蓝色卡组",
                mainDeckCards = new List<DeckCardEntry>
                {
                    new DeckCardEntry { cardId = "CREATURE_002", quantity = 3 },
                    new DeckCardEntry { cardId = "SPELL_002", quantity = 3 },
                    new DeckCardEntry { cardId = "DOMAIN_002", quantity = 2 }
                },
                extraDeckCards = new List<DeckCardEntry>(),
                colors = new List<ManaType> { ManaType.蓝色 }
            });

            // 红绿中速卡组
            decks.Add(new TempDeckData
            {
                id = "DECK_003",
                deckName = "自然之怒",
                description = "红色与绿色混合的中速卡组",
                mainDeckCards = new List<DeckCardEntry>
                {
                    new DeckCardEntry { cardId = "CREATURE_001", quantity = 3 },
                    new DeckCardEntry { cardId = "CREATURE_003", quantity = 4 },
                    new DeckCardEntry { cardId = "SPELL_001", quantity = 2 },
                    new DeckCardEntry { cardId = "DOMAIN_001", quantity = 2 }
                },
                extraDeckCards = new List<DeckCardEntry>
                {
                    new DeckCardEntry { cardId = "LEGENDARY_001", quantity = 1 }
                },
                colors = new List<ManaType> { ManaType.红色, ManaType.绿色 }
            });
        }
    }

    [Serializable]
    public class TempDeckData
    {
        public string id;
        public string deckName;
        public string description;
        public List<DeckCardEntry> mainDeckCards = new List<DeckCardEntry>();
        public List<DeckCardEntry> extraDeckCards = new List<DeckCardEntry>();
        public List<ManaType> colors = new List<ManaType>();

        /// <summary>
        /// 获取主卡组总数
        /// </summary>
        public int GetMainDeckCount()
        {
            int count = 0;
            foreach (var entry in mainDeckCards)
            {
                count += entry.quantity;
            }
            return count;
        }

        /// <summary>
        /// 获取额外卡组总数
        /// </summary>
        public int GetExtraDeckCount()
        {
            int count = 0;
            foreach (var entry in extraDeckCards)
            {
                count += entry.quantity;
            }
            return count;
        }

        /// <summary>
        /// 检查主卡组是否有效（40-60张）
        /// </summary>
        public bool IsMainDeckValid()
        {
            int count = GetMainDeckCount();
            return count >= 40 && count <= 60;
        }

        /// <summary>
        /// 检查额外卡组是否有效（0-15张）
        /// </summary>
        public bool IsExtraDeckValid()
        {
            int count = GetExtraDeckCount();
            return count >= 0 && count <= 15;
        }
    }

    [Serializable]
    public class DeckCardEntry
    {
        public string cardId;
        public int quantity;

        /// <summary>
        /// 检查数量是否有效（1-4张，传奇最多1张）
        /// </summary>
        public bool IsQuantityValid(bool isLegendary)
        {
            if (isLegendary)
            {
                return quantity >= 0 && quantity <= 1;
            }
            return quantity >= 0 && quantity <= 4;
        }
    }
}