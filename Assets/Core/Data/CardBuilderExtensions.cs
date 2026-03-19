using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using cfg;
using CardCore.Data;

namespace CardCore
{
    /// <summary>
    /// 卡牌到运行时Card的便捷创建工厂
    /// </summary>
    public static class CardFactory
    {
        /// <summary>
        /// 从 CardData 创建运行时 Card
        /// </summary>
        public static Card CreateCardFromData(CardData cardData)
        {
            if (cardData == null)
                throw new ArgumentNullException(nameof(cardData));

            return new CardWrapper(cardData);
        }

        /// <summary>
        /// 批量创建运行时 Card
        /// </summary>
        public static List<Card> CreateCardsFromData(List<CardData> cardDataList)
        {
            if (cardDataList == null)
                return new List<Card>();

            var cards = new List<Card>(cardDataList.Count);
            foreach (var data in cardDataList)
            {
                cards.Add(CreateCardFromData(data));
            }
            return cards;
        }

        /// <summary>
        /// 从 CardData 创建 Effect 列表（仅创建Effect，不绑定到Card）
        /// </summary>
        public static List<Effect> CreateEffectsFromCardData(CardData cardData)
        {
            var effects = new List<Effect>();
            foreach (var effectData in cardData.Effects)
            {
                Effect effect = CreateEffectFromEffectData(effectData);
                if (effect != null)
                {
                    effects.Add(effect);
                }
            }
            return effects;
        }

        /// <summary>
        /// 从 EffectData 创建 Effect
        /// </summary>
        private static Effect CreateEffectFromEffectData(EffectData effectData)
        {
            if (effectData == null) return null;

            // 根据效果速度决定效果类型
            Effect effect;
            switch (effectData.Speed)
            {
                case EffectSpeed.强制诱发:
                case EffectSpeed.可选诱发:
                    // 强制或可选诱发效果 -> 激活式效果
                    effect = ScriptableObject.CreateInstance<ActivatedAbility>();
                    effect.EffectName = effectData.Abbreviation;
                    effect.Description = effectData.Description;
                    effect.Speed = effectData.Speed;
                    effect.Logic = EffectSystemExtensions.EffectLogicFactory.Create(effectData.Abbreviation, effectData.Parameters);
                    break;

                case EffectSpeed.自由时点:
                    // 自由时点自发 -> 静态效果
                    effect = ScriptableObject.CreateInstance<StaticAbility>();
                    effect.EffectName = effectData.Abbreviation;
                    effect.Description = effectData.Description;
                    effect.Speed = effectData.Speed;
                    effect.Logic = EffectSystemExtensions.EffectLogicFactory.Create(effectData.Abbreviation, effectData.Parameters);
                    break;

                default:
                    effect = ScriptableObject.CreateInstance<ActivatedAbility>();
                    effect.EffectName = effectData.Abbreviation;
                    effect.Description = effectData.Description;
                    effect.Speed = effectData.Speed;
                    effect.Logic = EffectSystemExtensions.EffectLogicFactory.Create(effectData.Abbreviation, effectData.Parameters);
                    break;
            }

            return effect;
        }
    }

    /// <summary>
    /// 卡牌预览数据 - 专用于UI展示
    /// </summary>
    [Serializable]
    public class CardPreviewData
    {
        [Tooltip("卡牌ID")]
        public string CardID;

        [Tooltip("卡牌名称")]
        public string CardName;

        [Tooltip("卡牌类型")]
        public CardType CardType;

        [Tooltip("卡牌类型显示名称")]
        public string CardTypeName => CardType.ToString();

        [Tooltip("立绘路径")]
        public string Illustration;

        [Tooltip("生命值（战斗单位）")]
        public int? Life;

        [Tooltip("攻击力（战斗单位）")]
        public int? Power;

        [Tooltip("总法力消耗")]
        public float TotalCost;

        [Tooltip("费用明细（法力类型：数量）")]
        public List<ManaCostInfo> CostBreakdown;

        [Tooltip("效果列表（格式化后）")]
        public List<EffectPreviewInfo> EffectPreviews;

        [Tooltip("是否为传奇卡牌")]
        public bool IsLegendary;

        [Tooltip("是否有效（通过规则验证）")]
        public bool IsValid;

        [Tooltip("错误消息（如果验证失败）")]
        public string ValidationError;

        [Tooltip("创建时间")]
        public DateTime CreationTime;

        /// <summary>
        /// 从 CardData 创建预览数据
        /// </summary>
        public static CardPreviewData CreateFromCardData(CardData cardData, bool validate = true)
        {
            if (cardData == null)
                return null;

            var preview = new CardPreviewData
            {
                CardID = cardData.ID,
                CardName = cardData.CardName,
                CardType = cardData.CardType,
                Illustration = cardData.Illustration,
                Life = cardData.Life,
                Power = cardData.Power,
                TotalCost = cardData.TotalCost,
                CostBreakdown = CreateCostBreakdown(cardData.Cost),
                EffectPreviews = CreateEffectPreviews(cardData.Effects),
                IsLegendary = cardData.IsLegendary,
                CreationTime = cardData.CreationTime
            };

            // 执行规则验证
            if (validate)
            {
                preview.IsValid = cardData.Validate(out string error);
                preview.ValidationError = error;
            }
            else
            {
                preview.IsValid = true;
                preview.ValidationError = null;
            }

            return preview;
        }

        /// <summary>
        /// 创建费用明细
        /// </summary>
        private static List<ManaCostInfo> CreateCostBreakdown(Dictionary<int, float> cost)
        {
            var breakdown = new List<ManaCostInfo>();
            if (cost == null) return breakdown;

            foreach (var kvp in cost)
            {
                ManaType manaType = (ManaType)kvp.Key;
                if (Enum.IsDefined(typeof(ManaType), manaType))
                {
                    breakdown.Add(new ManaCostInfo
                    {
                        ManaType = manaType,
                        ManaTypeName = manaType.ToString(),
                        ManaTypeColor = GetManaTypeColor(manaType),
                        Amount = kvp.Value
                    });
                }
            }

            // 按消耗量排序
            breakdown.Sort((a, b) => b.Amount.CompareTo(a.Amount));

            return breakdown;
        }

        /// <summary>
        /// 获取法力类型颜色
        /// </summary>
        private static string GetManaTypeColor(ManaType manaType)
        {
            return manaType switch
            {
                ManaType.灰色 => "#9CA8A8",
                ManaType.红色 => "#FF4444",
                ManaType.蓝色 => "#4488FF",
                ManaType.绿色 => "#44AA44",
                ManaType.白色 => "#FFFFFF",
                ManaType.黑色 => "#333333",
                _ => "#9CA8A8"
            };
        }

        /// <summary>
        /// 创建效果预览列表
        /// </summary>
        private static List<EffectPreviewInfo> CreateEffectPreviews(List<EffectData> effects)
        {
            var previews = new List<EffectPreviewInfo>();
            if (effects == null) return previews;

            foreach (var effectData in effects)
            {
                previews.Add(new EffectPreviewInfo
                {
                    Abbreviation = effectData.Abbreviation,
                    Description = effectData.Description,
                    EffectType = GetEffectTypeName(effectData.Speed),
                    Initiative = effectData.Initiative,
                    ManaType = effectData.ManaType,
                    ManaTypeName = effectData.ManaType.ToString(),
                    ManaTypeColor = GetManaTypeColor(effectData.ManaType),
                    Parameters = effectData.Parameters
                });
            }

            return previews;
        }

        /// <summary>
        /// 获取效果类型名称
        /// </summary>
        private static string GetEffectTypeName(EffectSpeed speed)
        {
            return speed switch
            {
                EffectSpeed.强制诱发 => "强制诱发",
                EffectSpeed.可选诱发 => "可选诱发",
                EffectSpeed.自由时点 => "自由时点",
                _ => "未知"
            };
        }
    }

    /// <summary>
    /// 法力消耗信息
    /// </summary>
    [Serializable]
    public class ManaCostInfo
    {
        public ManaType ManaType;
        public string ManaTypeName;
        public string ManaTypeColor;
        public float Amount;
    }

    /// <summary>
    /// 效果预览信息
    /// </summary>
    [Serializable]
    public class EffectPreviewInfo
    {
        public string Abbreviation;
        public string Description;
        public string EffectType;
        public bool Initiative;
        public ManaType ManaType;
        public string ManaTypeName;
        public string ManaTypeColor;
        public float Parameters;
    }

    /// <summary>
    /// 效果选择器 - 从EffectLogicLibrary中查找效果
    /// </summary>
    public static class EffectSelector
    {
        private static Dictionary<string, EffectLogic> _effectsCache = new Dictionary<string, EffectLogic>();



        /// <summary>
        /// 根据缩写获取效果逻辑
        /// </summary>
        public static EffectLogic GetEffectLogic(string abbreviation)
        {
            if (string.IsNullOrEmpty(abbreviation))
                return null;

            string key = abbreviation.ToUpper();
            return _effectsCache.GetValueOrDefault(key);
        }

        /// <summary>
        /// 获取所有可用效果
        /// </summary>
        public static List<EffectEntry> GetAllEffects()
        {
            var entries = new List<EffectEntry>();


            return entries;
        }

        /// <summary>
        /// 根据缩写搜索效果
        /// </summary>
        public static List<EffectEntry> SearchEffects(string query)
        {
            if (string.IsNullOrEmpty(query))
                return new List<EffectEntry>();

            string upperQuery = query.ToUpper();
            return GetAllEffects()
                .Where(e => !string.IsNullOrEmpty(e.Abbreviation) &&
                               e.Abbreviation.ToUpper().Contains(upperQuery))
                .ToList();
        }

        /// <summary>
        /// 根据法力类型筛选效果
        /// </summary>
        public static List<EffectEntry> FilterEffectsByManaType(ManaType manaType)
        {
            return GetAllEffects()
                .Where(e => e.ManaType == manaType)
                .ToList();
        }

        /// <summary>
        /// 获取效果类型列表
        /// </summary>
        public static List<string> GetAvailableManaTypes()
        {
            var types = new HashSet<ManaType>();
            foreach (var entry in GetAllEffects())
            {
                types.Add(entry.ManaType);
            }
            return types.OrderBy(t => t.ToString()).Select(t => t.ToString()).ToList();
        }
    }

    /// <summary>
    /// 效果条目（用于显示）
    /// </summary>
    [Serializable]
    public class EffectEntry
    {
        public string Abbreviation;
        public EffectLogic Logic;
        public string Description;
        public ManaType ManaType;
        public float DefaultParameter;
        public bool RequiresTarget;
    }

    /// <summary>
    /// 卡牌批量导入导出扩展
    /// </summary>
    public static class CardDataIOBatch
    {
        /// <summary>
        /// 导出卡牌库为单个JSON文件
        /// </summary>
        public static async UniTask<bool> ExportDeckToJson(List<CardData> cards, string filePath)
        {
            try
            {
                var deckData = new DeckExportData
                {
                    Version = "1.0",
                    ExportDate = DateTime.Now.Ticks,
                    CardCount = cards.Count,
                    Cards = cards
                };

                string json = await UniTask.RunOnThreadPool(() => JsonUtility.ToJson(deckData, true));
                await UniTask.RunOnThreadPool(() => File.WriteAllText(filePath, json));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to export deck: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从JSON文件导入卡牌库
        /// </summary>
        public static async UniTask<(List<CardData>, string error)> ImportDeckFromJson(string filePath)
        {
            try
            {
                string json = await UniTask.RunOnThreadPool(() => File.ReadAllText(filePath));
                var deckData = JsonUtility.FromJson<DeckExportData>(json);

                if (deckData == null || deckData.Cards == null)
                {
                    return (new List<CardData>(), "文件格式错误");
                }

                // 验证导入的卡牌
                var validCards = new List<CardData>();
                var errors = new List<string>();

                foreach (var cardData in deckData.Cards)
                {
                    if (!cardData.Validate(out string error))
                    {
                        errors.Add($"卡牌 [{cardData.CardName}]: {error}");
                    }
                    else
                    {
                        // 确保ID已设置
                        if (string.IsNullOrEmpty(cardData.ID))
                        {
                            cardData.ID = cardData.CalculateID();
                        }
                        validCards.Add(cardData);
                    }
                }

                // 记录所有错误
                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        Debug.LogWarning($"Import error: {error}");
                    }
                    Debug.Log($"导入成功：{validCards.Count}/{deckData.Cards.Count} 张卡牌");

                    return (validCards, errors.Count > 0 ? string.Join("; ", errors) : null);
                }
                else
                {
                    return (validCards, null);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import deck: {e.Message}");
                return (null, e.Message);
            }
        }

        /// <summary>
        /// 导出卡牌为单张文件（便于单独分享）
        /// </summary>
        public static async UniTask<bool> ExportSingleCardToFile(CardData card, string directory)
        {
            try
            {
                string fileName = $"{card.CardName}_{card.ID}.json";
                string filePath = Path.Combine(directory, fileName);
                return await CardDataIO.ExportCardToFile(card, filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to export single card: {e.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 卡牌库导出数据
    /// </summary>
    [Serializable]
    public class DeckExportData
    {
        public string Version;
        public long ExportDate;
        public int CardCount;
        public List<CardData> Cards;
    }

    /// <summary>
    /// 卡牌组/套牌管理
    /// </summary>

    /// <summary>
    /// 卡牌组/套牌 - 代表玩家构建的牌组
    /// </summary>
    [Serializable]
    public class CardDeck
    {
        /// <summary>
        /// 牌组唯一标识ID
        /// </summary>
        [Tooltip("牌组ID")]
        public string DeckID { get; set; }

        /// <summary>
        /// 牌组名称
        /// </summary>
        [Tooltip("牌组名称")]
        public string DeckName { get; set; }

        /// <summary>
        /// 牌组描述
        /// </summary>
        [TextArea(2, 5)]
        [Tooltip("牌组描述")]
        public string Description { get; set; }

        /// <summary>
        /// 牌组中包含的卡牌ID列表
        /// </summary>
        [Tooltip("牌组中的卡牌ID列表")]
        public List<string> CardIDs { get; set; } = new List<string>();

        /// <summary>
        /// 创建时间
        /// </summary>
        [HideInInspector]
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        [HideInInspector]
        public DateTime ModifiedTime { get; set; }

        /// <summary>
        /// 牌组中的卡牌数量
        /// </summary>
        public int CardCount => CardIDs?.Count ?? 0;

        /// <summary>
        /// 获取牌组中的卡牌数据列表（从注册表）
        /// </summary>
        public List<CardData> GetCards()
        {
            var cards = new List<CardData>();
            if (CardIDs == null) return cards;

            foreach (var cardId in CardIDs)
            {
                var card = CardDataRegistry.Instance.GetCard(cardId);
                if (card != null)
                {
                    cards.Add(card);
                }
            }
            return cards;
        }

        /// <summary>
        /// 添加卡牌到牌组
        /// </summary>
        public void AddCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            if (CardIDs == null) CardIDs = new List<string>();

            if (!CardIDs.Contains(cardId))
            {
                CardIDs.Add(cardId);
                ModifiedTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 从牌组中移除卡牌
        /// </summary>
        public void RemoveCard(string cardId)
        {
            if (CardIDs == null) return;
            if (CardIDs.Remove(cardId))
            {
                ModifiedTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 清空牌组
        /// </summary>
        public void ClearCards()
        {
            CardIDs?.Clear();
            ModifiedTime = DateTime.Now;
        }

        /// <summary>
        /// 检查牌组是否包含指定卡牌
        /// </summary>
        public bool ContainsCard(string cardId)
        {
            return CardIDs?.Contains(cardId) ?? false;
        }

        /// <summary>
        /// 创建新的牌组
        /// </summary>
        public static CardDeck CreateNew(string deckName)
        {
            return new CardDeck
            {
                DeckID = Guid.NewGuid().ToString(),
                DeckName = deckName,
                Description = string.Empty,
                CardIDs = new List<string>(),
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now
            };
        }

        /// <summary>
        /// 计算牌组统计信息
        /// </summary>
        public DeckStatistics GetStatistics()
        {
            var stats = new DeckStatistics();
            var cards = GetCards();

            stats.TotalCards = cards.Count;
            stats.MonsterCount = cards.Count(c => c.CardType == CardType.生物);
            stats.LegendCount = cards.Count(c => c.CardType == CardType.传奇);
            stats.MagicCount = cards.Count(c => c.CardType == CardType.术法);
            stats.FieldCount = cards.Count(c => c.CardType == CardType.领域);
            stats.AverageCost = cards.Count > 0 ? cards.Average(c => c.TotalCost) : 0;

            // 按法力类型统计
            foreach (ManaType manaType in Enum.GetValues(typeof(ManaType)))
            {
                stats.CardsByManaType[manaType] = cards.Count(c => c.Cost.ContainsKey((int)manaType));
            }

            // 按费用区间统计
            stats.CardsByCostRange[0] = cards.Count(c => c.TotalCost <= 2);
            stats.CardsByCostRange[1] = cards.Count(c => c.TotalCost > 2 && c.TotalCost <= 4);
            stats.CardsByCostRange[2] = cards.Count(c => c.TotalCost > 4 && c.TotalCost <= 6);
            stats.CardsByCostRange[3] = cards.Count(c => c.TotalCost > 6);

            return stats;
        }
    }

    /// <summary>
    /// 牌组统计信息
    /// </summary>
    [Serializable]
    public class DeckStatistics
    {
        public int TotalCards;
        public int MonsterCount;
        public int LegendCount;
        public int MagicCount;
        public int FieldCount;
        public float AverageCost;

        /// <summary>
        /// 按法力类型统计
        /// </summary>
        public Dictionary<ManaType, int> CardsByManaType = new Dictionary<ManaType, int>();

        /// <summary>
        /// 按费用区间统计 [0-2, 3-4, 5-6, 7+]
        /// </summary>
        public Dictionary<int, int> CardsByCostRange = new Dictionary<int, int>
        {
            { 0, 0 }, { 1, 0 }, { 2, 0 }, { 3, 0 }
        };
    }

    /// <summary>
    /// 卡牌组管理器
    /// </summary>
    [CreateAssetMenu(fileName = "CardDecks", menuName = "Custom Data/Card Decks")]
    public class CardDeckManager : ScriptableObject
    {
        [SerializeField]
        private List<CardDeck> _decks = new List<CardDeck>();

        public List<CardDeck> Decks
        {
            get => _decks;
            set => _decks = value;
        }

        /// <summary>
        /// 添加牌组
        /// </summary>
        public void AddDeck(CardDeck deck)
        {
            if (deck == null) return;

            _decks.Add(deck);
        }

        /// <summary>
        /// 移除牌组
        /// </summary>
        public void RemoveDeck(string deckId)
        {
            _decks.RemoveAll(d => d.DeckID == deckId);
        }

        /// <summary>
        /// 获取牌组
        /// </summary>
        public CardDeck GetDeck(string deckId)
        {
            return _decks.FirstOrDefault(d => d.DeckID == deckId);
        }

        /// <summary>
        /// 更新牌组
        /// </summary>
        public void UpdateDeck(CardDeck deck)
        {
            if (deck == null) return;

            int index = _decks.FindIndex(d => d.DeckID == deck.DeckID);
            if (index >= 0)
            {
                _decks[index] = deck;
                _decks[index].ModifiedTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 获取所有牌组
        /// </summary>
        public List<CardDeck> GetAllDecks()
        {
            return new List<CardDeck>(_decks);
        }
    }

    /// <summary>
    /// 卡牌标签系统
    /// </summary>
    [Serializable]
    public class CardTag
    {
        [Tooltip("标签ID")]
        public string TagID;

        [Tooltip("标签名称")]
        public string TagName;

        [Tooltip("标签颜色")]
        public string TagColor;

        [Tooltip("标签描述")]
        [TextArea(1, 3)]
        public string Description;
    }

    /// <summary>
    /// 卡牌标签管理器
    /// </summary>
    [CreateAssetMenu(fileName = "CardTags", menuName = "Custom Data/Card Tags")]
    public class CardTagManager : ScriptableObject
    {
        [SerializeField]
        private List<CardTag> _tags = new List<CardTag>();

        public List<CardTag> Tags
        {
            get => _tags;
            set => _tags = value;
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        public void AddTag(CardTag tag)
        {
            if (tag == null) return;

            // 检查ID是否重复
            if (_tags.Any(t => t.TagID == tag.TagID))
            {
                Debug.LogWarning($"Tag ID already exists: {tag.TagID}");
                return;
            }

            _tags.Add(tag);
        }

        /// <summary>
        /// 移除标签
        /// </summary>
        public void RemoveTag(string tagId)
        {
            _tags.RemoveAll(t => t.TagID == tagId);
        }

        /// <summary>
        /// 获取标签
        /// </summary>
        public CardTag GetTag(string tagId)
        {
            return _tags.FirstOrDefault(t => t.TagID == tagId);
        }

        /// <summary>
        /// 获取所有标签
        /// </summary>
        public List<CardTag> GetAllTags()
        {
            return new List<CardTag>(_tags);
        }

        /// <summary>
        /// 按卡牌获取标签
        /// </summary>
        public List<CardTag> GetTagsForCard(string cardId)
        {
            // TODO: 实现卡牌-标签关联关系
            return new List<CardTag>();
        }
    }

    /// <summary>
    /// 卡牌统计分析工具
    /// </summary>
    public static class CardStatistics
    {
        /// <summary>
        /// 卡牌统计信息
        /// </summary>
        [Serializable]
        public class StatisticsData
        {
            public int TotalCards;
            public int MonsterCards;
            public int LegendCards;
            public int MagicCards;
            public int FieldCards;
            public float AverageCost;
            public Dictionary<ManaType, int> CardsByManaType;
            public Dictionary<EffectSpeed, int> CardsByEffectSpeed;
            public List<string> MostUsedEffects;
        }

        /// <summary>
        /// 计算卡牌库统计
        /// </summary>
        public static StatisticsData CalculateStatistics(List<CardData> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                return new StatisticsData();
            }

            var stats = new StatisticsData
            {
                TotalCards = cards.Count,
                MonsterCards = cards.Count(c => c.CardType == CardType.生物),
                LegendCards = cards.Count(c => c.CardType == CardType.传奇),
                MagicCards = cards.Count(c => c.CardType == CardType.术法),
                FieldCards = cards.Count(c => c.CardType == CardType.领域),
                AverageCost = cards.Average(c => c.TotalCost)
            };

            // 按法力类型统计
            stats.CardsByManaType = new Dictionary<ManaType, int>();
            foreach (ManaType manaType in Enum.GetValues(typeof(ManaType)))
            {
                stats.CardsByManaType[manaType] = cards.Count(c => c.Cost.ContainsKey((int)manaType));
            }

            // 按效果速度统计
            stats.CardsByEffectSpeed = new Dictionary<EffectSpeed, int>();
            foreach (EffectSpeed speed in Enum.GetValues(typeof(EffectSpeed)))
            {
                stats.CardsByEffectSpeed[speed] = cards.Count(c => c.Effects.Any(e => e.Speed == speed));
            }

            // 统计最常用效果
            var effectCounts = new Dictionary<string, int>();
            foreach (var card in cards)
            {
                foreach (var effect in card.Effects)
                {
                    string key = effect.Abbreviation?.ToUpper();
                    if (!string.IsNullOrEmpty(key))
                    {
                        if (effectCounts.ContainsKey(key))
                            effectCounts[key]++;
                        else
                            effectCounts[key] = 1;
                    }
                }
            }

            // 取前10个最常用效果
            stats.MostUsedEffects = effectCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .Select(kvp => kvp.Key)
                .ToList();

            return stats;
        }
    }
}
