using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardCore
{
    // ======================================== JSON 数据结构 ========================================

    /// <summary>
    /// 关键词定义
    /// </summary>
    [Serializable]
    public class KeywordDefinition
    {
        public string id;
        public string nameZh;
        public string nameEn;
        public string color;
        public string description;
        public bool isPassive;
        public string atomicEffect;
    }

    /// <summary>
    /// 关键词配置包装（JsonUtility 需要顶层对象）
    /// </summary>
    [Serializable]
    public class KeywordsConfigWrapper
    {
        public List<KeywordDefinition> keywords;
    }

    /// <summary>
    /// 卡牌配置条目（JSON 中单张卡的数据）
    /// </summary>
    [Serializable]
    public class CardConfigEntry
    {
        public string id;
        public string cardName;
        public string supertype;
        public int power;
        public int life;
        public List<CostJsonEntry> costList;
        public List<string> keywords;
        public List<string> tags;
    }

    /// <summary>
    /// 费用 JSON 条目
    /// </summary>
    [Serializable]
    public class CostJsonEntry
    {
        public int manaType;
        public float amount;
    }

    /// <summary>
    /// 卡组配置
    /// </summary>
    [Serializable]
    public class DeckConfig
    {
        public int copiesPerCard = 3;
    }

    /// <summary>
    /// 测试卡牌配置包装
    /// </summary>
    [Serializable]
    public class TestCardsConfigWrapper
    {
        public List<CardConfigEntry> cards;
        public DeckConfig deckConfig;
    }

    // ======================================== 卡牌加载器 ========================================

    /// <summary>
    /// 卡牌加载器 - 从 JSON 配置构建 CardData 和测试卡组
    /// </summary>
    public static class CardLoader
    {
        private static Dictionary<string, KeywordDefinition> _keywordCache;

        /// <summary>
        /// 加载关键词配置
        /// </summary>
        public static Dictionary<string, KeywordDefinition> LoadKeywords(string jsonPath)
        {
            var json = Resources.Load<TextAsset>(jsonPath);
            if (json == null)
            {
                Debug.LogWarning($"[CardLoader] 关键词配置未找到: {jsonPath}");
                return new Dictionary<string, KeywordDefinition>();
            }

            var wrapper = JsonUtility.FromJson<KeywordsConfigWrapper>(json.text);
            _keywordCache = new Dictionary<string, KeywordDefinition>();
            foreach (var kw in wrapper.keywords)
            {
                _keywordCache[kw.id] = kw;
            }
            return _keywordCache;
        }

        /// <summary>
        /// 获取已加载的关键词定义
        /// </summary>
        public static KeywordDefinition GetKeywordDefinition(string keywordId)
        {
            if (_keywordCache != null && _keywordCache.TryGetValue(keywordId, out var def))
                return def;
            return null;
        }

        /// <summary>
        /// 从 JSON 加载测试卡牌列表
        /// </summary>
        public static List<CardData> LoadCards(string jsonPath)
        {
            var json = Resources.Load<TextAsset>(jsonPath);
            if (json == null)
            {
                Debug.LogWarning($"[CardLoader] 卡牌配置未找到: {jsonPath}");
                return new List<CardData>();
            }

            var wrapper = JsonUtility.FromJson<TestCardsConfigWrapper>(json.text);
            var result = new List<CardData>();

            foreach (var entry in wrapper.cards)
            {
                var cardData = CreateCardData(entry);
                result.Add(cardData);
            }

            return result;
        }

        /// <summary>
        /// 直接从 JSON 文本加载卡牌（用于测试，不依赖 Resources）
        /// </summary>
        public static List<CardData> LoadCardsFromText(string jsonText)
        {
            var wrapper = JsonUtility.FromJson<TestCardsConfigWrapper>(jsonText);
            var result = new List<CardData>();

            foreach (var entry in wrapper.cards)
            {
                var cardData = CreateCardData(entry);
                result.Add(cardData);
            }

            return result;
        }

        /// <summary>
        /// 将 CardData 列表转换为 CardWrapper 列表
        /// </summary>
        public static List<Card> ToCardInstances(List<CardData> cardsData)
        {
            var result = new List<Card>();
            foreach (var data in cardsData)
            {
                result.Add(new CardWrapper(data));
            }
            return result;
        }

        /// <summary>
        /// 构建测试卡组（每张卡 copiesPerCard 份）
        /// </summary>
        public static List<Card> BuildTestDeck(string jsonPath, int copiesPerCard = 3)
        {
            var cardsData = LoadCards(jsonPath);
            return BuildDeck(cardsData, copiesPerCard);
        }

        /// <summary>
        /// 从文本构建测试卡组（用于测试）
        /// </summary>
        public static List<Card> BuildTestDeckFromText(string jsonText, int copiesPerCard = 3)
        {
            var cardsData = LoadCardsFromText(jsonText);
            return BuildDeck(cardsData, copiesPerCard);
        }

        /// <summary>
        /// 从 CardData 列表构建卡组
        /// </summary>
        public static List<Card> BuildDeck(List<CardData> cardsData, int copiesPerCard)
        {
            var deck = new List<Card>();
            foreach (var data in cardsData)
            {
                for (int i = 0; i < copiesPerCard; i++)
                {
                    deck.Add(new CardWrapper(data));
                }
            }
            return deck;
        }

        // ======================================== 内部方法 ========================================

        /// <summary>
        /// 从配置条目创建 CardData
        /// </summary>
        private static CardData CreateCardData(CardConfigEntry entry)
        {
            var cardData = new CardData
            {
                ID = entry.id,
                CardName = entry.cardName,
                Supertype = ParseCardtype(entry.supertype),
                Power = entry.power,
                Life = entry.life,
                Cost = ParseCost(entry.costList),
                Keywords = entry.keywords ?? new List<string>(),
                Tags = entry.tags ?? new List<string>(),
                Effects = new List<EffectData>()
            };

            return cardData;
        }

        /// <summary>
        /// 解析卡牌类型
        /// </summary>
        private static Cardtype ParseCardtype(string typeStr)
        {
            if (Enum.TryParse<Cardtype>(typeStr, out var result))
                return result;
            return Cardtype.Creature;
        }

        /// <summary>
        /// 解析费用列表
        /// </summary>
        private static Dictionary<int, float> ParseCost(List<CostJsonEntry> costList)
        {
            var cost = new Dictionary<int, float>();
            if (costList == null) return cost;
            foreach (var entry in costList)
            {
                cost[entry.manaType] = entry.amount;
            }
            return cost;
        }
    }
}
