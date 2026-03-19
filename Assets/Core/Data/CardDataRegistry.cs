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
    /// 卡牌数据注册表
    /// 负责卡牌的保存、加载、查询和管理
    /// </summary>
    public class CardDataRegistry
    {
        private static CardDataRegistry _instance;
        public static CardDataRegistry Instance => _instance ??= new CardDataRegistry();

        // 保存路径
        private string SavePath => Path.Combine(Application.persistentDataPath, "Cards", "cardRegistry.json");

        // 内存存储
        private Dictionary<string, CardData> _cards = new Dictionary<string, CardData>();
        private Dictionary<string, CardData> _cardsByName = new Dictionary<string, CardData>();
        private Dictionary<CardType, List<string>> _cardsByType = new Dictionary<CardType, List<string>>();
        private Dictionary<ManaType, List<string>> _cardsByManaType = new Dictionary<ManaType, List<string>>();

        // 是否已加载
        private bool _isLoaded = false;

        /// <summary>
        /// 所有卡牌的只读访问
        /// </summary>
        public IReadOnlyCollection<CardData> AllCards => _cards.Values;

        /// <summary>
        /// 卡牌数量
        /// </summary>
        public int Count => _cards.Count;

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private CardDataRegistry() { }

        /// <summary>
        /// 注册表是否已加载
        /// </summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>
        /// 加载所有卡牌数据
        /// </summary>
        public async UniTask LoadAsync()
        {
            if (_isLoaded) return;

            _cards.Clear();
            _cardsByName.Clear();
            _cardsByType.Clear();
            _cardsByManaType.Clear();

            // 初始化字典
            foreach (CardType type in Enum.GetValues(typeof(CardType)))
            {
                _cardsByType[type] = new List<string>();
            }
            foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
            {
                _cardsByManaType[mana] = new List<string>();
            }

            if (File.Exists(SavePath))
            {
                try
                {
                    string json = await UniTask.RunOnThreadPool(() => File.ReadAllText(SavePath));
                    var registryWrapper = JsonUtility.FromJson<CardRegistryWrapper>(json);

                    // 重建索引
                    _cards.Clear();
                    _cardsByName.Clear();
                    foreach (var type in _cardsByType.Values)
                    {
                        type.Clear();
                    }
                    foreach (var list in _cardsByManaType.Values)
                    {
                        list.Clear();
                    }

                    foreach (var kvp in registryWrapper.Cards)
                    {
                        _cards[kvp.Key] = kvp.Value;
                        AddCardToIndexes(kvp.Value);
                    }

                    _isLoaded = true;
                    Debug.Log($"Loaded {_cards.Count} cards from registry.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load card registry: {e.Message}");
                    _isLoaded = true;
                }
            }
            else
            {
                Debug.Log("No existing card registry found. Starting with empty registry.");
                _isLoaded = true;
            }
        }

        /// <summary>
        /// 保存所有卡牌数据
        /// </summary>
        public async UniTask SaveAsync()
        {
            try
            {
                var wrapper = new CardRegistryWrapper
                {
                    Version = "1.0",
                    LastModified = DateTime.Now.Ticks,
                    Cards = _cards
                };

                string json = await UniTask.RunOnThreadPool(() => JsonUtility.ToJson(wrapper, true));

                // 确保目录存在
                string directory = Path.GetDirectoryName(SavePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await UniTask.RunOnThreadPool(() => File.WriteAllText(SavePath, json));

                Debug.Log($"Saved {_cards.Count} cards to registry.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save card registry: {e.Message}");
            }
        }

        /// <summary>
        /// 注册新卡牌
        /// </summary>
        public bool RegisterCard(CardData card, out string errorMessage)
        {
            errorMessage = null;

            if (card == null)
            {
                errorMessage = "卡牌数据不能为空";
                return false;
            }

            // 验证卡牌规则
            if (!card.Validate(out errorMessage))
            {
                return false;
            }

            // 确保ID已生成
            if (string.IsNullOrEmpty(card.ID))
            {
                card.ID = card.CalculateID();
            }

            // 检查是否已存在
            if (_cards.ContainsKey(card.ID))
            {
                errorMessage = $"ID为 {card.ID} 的卡牌已存在";
                return false;
            }

            // 检查名称是否重复
            if (_cardsByName.ContainsKey(card.CardName))
            {
                errorMessage = $"名为 '{card.CardName}' 的卡牌已存在";
                return false;
            }

            // 添加到注册表
            _cards[card.ID] = card;
            AddCardToIndexes(card);

            Debug.Log($"注册新卡牌: {card.CardName} (ID: {card.ID})");
            return true;
        }

        /// <summary>
        /// 更新卡牌
        /// </summary>
        public bool UpdateCard(CardData card, out string errorMessage)
        {
            errorMessage = null;

            if (card == null)
            {
                errorMessage = "卡牌数据不能为空";
                return false;
            }

            // 验证卡牌规则
            if (!card.Validate(out errorMessage))
            {
                return false;
            }

            if (string.IsNullOrEmpty(card.ID) || !_cards.ContainsKey(card.ID))
            {
                errorMessage = "卡牌在注册表中不存在";
                return false;
            }

            // 移除旧索引
            RemoveCardFromIndexes(card.ID);

            // 更新数据
            _cards[card.ID] = card;

            // 重建索引
            AddCardToIndexes(card);

            Debug.Log($"更新卡牌: {card.CardName} (ID: {card.ID})");
            return true;
        }

        /// <summary>
        /// 删除卡牌
        /// </summary>
        public bool DeleteCard(string cardId, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(cardId))
            {
                errorMessage = "Card ID cannot be null or empty.";
                return false;
            }

            if (!_cards.ContainsKey(cardId))
            {
                errorMessage = "Card not found in registry.";
                return false;
            }

            // 移除索引
            RemoveCardFromIndexes(cardId);

            // 删除卡牌
            _cards.Remove(cardId);

            return true;
        }

        /// <summary>
        /// 根据ID获取卡牌
        /// </summary>
        public CardData GetCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return null;

            return _cards.TryGetValue(cardId, out var card) ? card : null;
        }

        /// <summary>
        /// 根据名称获取卡牌
        /// </summary>
        public CardData GetCardByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return _cardsByName.TryGetValue(name, out var card) ? card : null;
        }

        /// <summary>
        /// 根据卡牌类型获取所有卡牌
        /// </summary>
        public List<CardData> GetCardsByType(CardType cardType)
        {
            if (!_cardsByType.ContainsKey(cardType))
                return new List<CardData>();

            return _cardsByType[cardType]
                .Select(id => _cards[id])
                .Where(c => c != null)
                .ToList();
        }

        /// <summary>
        /// 根据法力类型获取所有卡牌
        /// </summary>
        public List<CardData> GetCardsByManaType(ManaType manaType)
        {
            if (!_cardsByManaType.ContainsKey(manaType))
                return new List<CardData>();

            return _cardsByManaType[manaType]
                .Select(id => _cards[id])
                .Where(c => c != null)
                .ToList();
        }

        /// <summary>
        /// 查询卡牌（支持多条件组合）
        /// </summary>
        public List<CardData> QueryCards(
            List<CardType> cardTypes = null,
            List<ManaType> manaTypes = null,
            float? minCost = null,
            float? maxCost = null,
            bool? hasActiveEffect = null)
        {
            IEnumerable<CardData> query = _cards.Values;

            // 按卡牌类型过滤
            if (cardTypes != null && cardTypes.Count > 0)
            {
                query = query.Where(c => cardTypes.Contains(c.CardType));
            }

            // 按法力类型过滤
            if (manaTypes != null && manaTypes.Count > 0)
            {
                query = query.Where(c => c.Cost.Any(kvp => manaTypes.Contains((ManaType)kvp.Key)));
            }

            // 按费用范围过滤
            if (minCost.HasValue)
            {
                query = query.Where(c => c.TotalCost >= minCost.Value);
            }
            if (maxCost.HasValue)
            {
                query = query.Where(c => c.TotalCost <= maxCost.Value);
            }

            // 按主动效果过滤
            if (hasActiveEffect.HasValue)
            {
                query = query.Where(c => c.HasActiveEffect == hasActiveEffect.Value);
            }

            return query.ToList();
        }

        /// <summary>
        /// 清空注册表
        /// </summary>
        public void Clear()
        {
            _cards.Clear();
            _cardsByName.Clear();
            foreach (var list in _cardsByType.Values)
            {
                list.Clear();
            }
            foreach (var list in _cardsByManaType.Values)
            {
                list.Clear();
            }
            _isLoaded = true;
        }

        /// <summary>
        /// 重建索引
        /// </summary>
        private void RebuildIndexes()
        {
            _cardsByName.Clear();
            foreach (var list in _cardsByType.Values)
            {
                list.Clear();
            }
            foreach (var list in _cardsByManaType.Values)
            {
                list.Clear();
            }

            foreach (var kvp in _cards)
            {
                AddCardToIndexes(kvp.Value);
            }
        }

        /// <summary>
        /// 添加卡牌到索引
        /// </summary>
        private void AddCardToIndexes(CardData card)
        {
            _cardsByName[card.CardName] = card;
            _cardsByType[card.CardType].Add(card.ID);

            foreach (var kvp in card.Cost)
            {
                ManaType manaType = (ManaType)kvp.Key;
                if (Enum.IsDefined(typeof(ManaType), manaType))
                {
                    if (!_cardsByManaType[manaType].Contains(card.ID))
                    {
                        _cardsByManaType[manaType].Add(card.ID);
                    }
                }
            }
        }

        /// <summary>
        /// 从索引中移除卡牌
        /// </summary>
        private void RemoveCardFromIndexes(string cardId)
        {
            var card = _cards.GetValueOrDefault(cardId);
            if (card == null) return;

            // 移除名称索引
            if (_cardsByName.ContainsKey(card.CardName))
            {
                _cardsByName.Remove(card.CardName);
            }

            // 移除类型索引
            _cardsByType[card.CardType].Remove(cardId);

            // 移除法力类型索引
            foreach (var list in _cardsByManaType.Values)
            {
                list.Remove(cardId);
            }
        }

        /// <summary>
        /// 序列化包装类
        /// </summary>
        [Serializable]
        private class CardRegistryWrapper
        {
            [SerializeField]
            private List<CardEntry> _entries;
            public Dictionary<string, CardData> Cards { get; set; } = new Dictionary<string, CardData>();

            public string Version { get; set; }
            public long LastModified { get; set; }

            /// <summary>
            /// 序列化前转换
            /// </summary>
            public void OnBeforeSerialize()
            {
                _entries = new List<CardEntry>();
                foreach (var kvp in Cards)
                {
                    _entries.Add(new CardEntry { ID = kvp.Key, Json = JsonUtility.ToJson(kvp.Value, true) });
                }
            }

            /// <summary>
            /// 反序列化后转换
            /// </summary>
            public void OnAfterDeserialize()
            {
                Cards = new Dictionary<string, CardData>();
                if (_entries == null) return;

                foreach (var entry in _entries)
                {
                    try
                    {
                        var card = JsonUtility.FromJson<CardData>(entry.Json);
                        Cards[entry.ID] = card;
                    }
                    catch
                    {
                        // 跳过解析失败的数据
                    }
                }
            }

            [Serializable]
            private class CardEntry
            {
                public string ID;
                public string Json;
            }
        }
    }

    /// <summary>
    /// 卡牌导入导出工具
    /// </summary>
    public static class CardDataIO
    {
        /// <summary>
        /// 导出单张卡牌为文件
        /// </summary>
        public static async UniTask<bool> ExportCardToFile(CardData card, string filePath)
        {
            try
            {
                string json = await UniTask.RunOnThreadPool(() => JsonUtility.ToJson(card, true));
                await UniTask.RunOnThreadPool(() => File.WriteAllText(filePath, json));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to export card: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从文件导入卡牌
        /// </summary>
        public static async UniTask<CardData> ImportCardFromFile(string filePath)
        {
            try
            {
                string json = await UniTask.RunOnThreadPool(() => File.ReadAllText(filePath));
                return await UniTask.RunOnThreadPool(() => JsonUtility.FromJson<CardData>(json));
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import card: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 导出卡牌为JSON（便于调试和分享）
        /// </summary>
        public static string ExportToJson(CardData card)
        {
            return JsonUtility.ToJson(card, true);
        }

        /// <summary>
        /// 从JSON导入卡牌
        /// </summary>
        public static CardData ImportFromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<CardData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import card from JSON: {e.Message}");
                return null;
            }
        }
    }
}
