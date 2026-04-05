using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    /// <summary>
    /// 元素池卡
    /// 放置在元素池中的卡牌（背面向上）
    /// </summary>
    public class ElementCard
    {
        public Card Card { get; set; }
        public ManaType ManaType { get; set; }
        public DateTime PlacedTime { get; set; }
        public int SequenceNumber { get; set; }
        public bool IsConsumed { get; set; }
    }

    /// <summary>
    /// 元素池系统
    /// 负责管理元素池，支持按颜色分类、优先消耗先入场的元素
    /// 按玩家分开管理
    /// </summary>
    public class ElementPoolSystem
    {
        private const int MAX_TOTAL_COST = 12;

        // 按玩家分类的元素池
        private Dictionary<Player, PlayerElementPool> _playerPools =
            new Dictionary<Player, PlayerElementPool>();

        /// <summary>
        /// 玩家元素池数据
        /// </summary>
        public class PlayerElementPool
        {
            public Dictionary<ManaType, Queue<ElementCard>> Elements =
                new Dictionary<ManaType, Queue<ElementCard>>();
            public int TotalCost = 0;

            /// <summary>临时可用元素（效果直接添加，不通过卡牌）</summary>
            public Dictionary<ManaType, int> AvailableMana = new Dictionary<ManaType, int>();

            public PlayerElementPool()
            {
                foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
                {
                    Elements[mana] = new Queue<ElementCard>();
                    AvailableMana[mana] = 0;
                }
            }
        }

        /// <summary>
        /// 初始化玩家元素池
        /// </summary>
        public void InitializePlayer(Player player)
        {
            if (!_playerPools.ContainsKey(player))
            {
                _playerPools[player] = new PlayerElementPool();
            }
        }

        /// <summary>
        /// 获取玩家元素池
        /// </summary>
        public PlayerElementPool GetPool(Player player)
        {
            if (!_playerPools.ContainsKey(player))
                InitializePlayer(player);
            return _playerPools[player];
        }

        /// <summary>
        /// 获取玩家当前总费用
        /// </summary>
        public int GetTotalCost(Player player)
        {
            return GetPool(player).TotalCost;
        }

        /// <summary>
        /// 检查玩家元素池是否已满
        /// </summary>
        public bool IsFull(Player player)
        {
            return GetPool(player).TotalCost >= MAX_TOTAL_COST;
        }

        /// <summary>
        /// 获取玩家剩余空间
        /// </summary>
        public int GetRemainingSpace(Player player)
        {
            return MAX_TOTAL_COST - GetPool(player).TotalCost;
        }

        /// <summary>
        /// 添加元素到池中
        /// </summary>
        public bool AddElement(Card card, ManaType manaType, Player owner)
        {
            var pool = GetPool(owner);

            // 检查是否已满
            if (pool.TotalCost >= MAX_TOTAL_COST)
                return false;

            // 检查卡牌是否已经在池中
            if (pool.Elements[manaType].Any(e => e.Card == card))
                return false;

            // 创建元素卡
            var elementCard = new ElementCard
            {
                Card = card,
                ManaType = manaType,
                PlacedTime = DateTime.Now,
                SequenceNumber = pool.Elements[manaType].Count,
                IsConsumed = false
            };

            // 添加到对应颜色的队列
            pool.Elements[manaType].Enqueue(elementCard);

            // 更新总费用
            pool.TotalCost += GetCardCost(card);

            // 触发事件
            PublishEvent(new ElementPoolAddEvent
            {
                Player = owner,
                AddedCard = card,
                ManaType = manaType
            });

            return true;
        }

        /// <summary>
        /// 消耗元素
        /// </summary>
        public bool ConsumeElement(ManaType manaType, int amount, Effect source, Player player)
        {
            var pool = GetPool(player);
            var queue = pool.Elements[manaType];

            // 检查是否有足够元素
            if (GetElementCount(manaType, player) < amount)
                return false;

            // 消耗指定数量的元素（先入先出）
            for (int i = 0; i < amount; i++)
            {
                if (queue.Count > 0)
                {
                    var element = queue.Dequeue();
                    element.IsConsumed = true;
                    pool.TotalCost -= GetCardCost(element.Card);
                }
            }

            // 触发事件
            PublishEvent(new ElementPoolConsumeEvent
            {
                Player = player,
                ManaType = manaType,
                Amount = amount,
                Source = source
            });

            return true;
        }

        /// <summary>
        /// 检查是否可以消耗指定数量的元素
        /// </summary>
        public bool CanConsume(ManaType manaType, int amount, Player player)
        {
            return GetElementCount(manaType, player) >= amount;
        }

        /// <summary>
        /// 获取指定颜色的元素数量
        /// </summary>
        public int GetElementCount(ManaType manaType, Player player)
        {
            var pool = GetPool(player);
            return pool.Elements.ContainsKey(manaType) ? pool.Elements[manaType].Count : 0;
        }

        /// <summary>
        /// 获取玩家所有元素卡
        /// </summary>
        public List<ElementCard> GetAllElements(Player player)
        {
            var result = new List<ElementCard>();
            var pool = GetPool(player);
            foreach (var kvp in pool.Elements)
            {
                result.AddRange(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// 获取指定颜色的元素队列
        /// </summary>
        public Queue<ElementCard> GetElementQueue(ManaType manaType, Player player)
        {
            return GetPool(player).Elements[manaType];
        }

        /// <summary>
        /// 移除指定的元素卡
        /// </summary>
        public bool RemoveElement(ElementCard element, Player player)
        {
            var pool = GetPool(player);
            var queue = pool.Elements[element.ManaType];

            if (!queue.Contains(element))
                return false;

            var tempList = queue.ToList();
            tempList.Remove(element);
            pool.Elements[element.ManaType] = new Queue<ElementCard>(tempList);
            pool.TotalCost -= GetCardCost(element.Card);

            return true;
        }

        /// <summary>
        /// 移除卡牌对应的所有元素
        /// </summary>
        public void RemoveElementsByCard(Card card, Player player)
        {
            var pool = GetPool(player);
            foreach (var kvp in pool.Elements)
            {
                var toRemove = kvp.Value.Where(e => e.Card == card).ToList();
                foreach (var element in toRemove)
                {
                    RemoveElement(element, player);
                }
            }
        }

        /// <summary>
        /// 重置所有元素池
        /// </summary>
        public void Reset()
        {
            foreach (var pool in _playerPools.Values)
            {
                foreach (var queue in pool.Elements.Values)
                {
                    queue.Clear();
                }
                pool.TotalCost = 0;
            }
        }

        /// <summary>
        /// 获取卡牌费用
        /// </summary>
        private int GetCardCost(Card card)
        {
            return 1;
        }

        /// <summary>
        /// 获取玩家元素池状态字符串
        /// </summary>
        public string GetStatusString(Player player)
        {
            var pool = GetPool(player);
            var result = $"元素池 ({pool.TotalCost}/{MAX_TOTAL_COST}):\n";

            foreach (ManaType manaType in Enum.GetValues(typeof(ManaType)))
            {
                int count = GetElementCount(manaType, player);
                if (count > 0)
                {
                    result += $"  {manaType}: {count}\n";
                }
            }

            return result;
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            EventManager.Instance.Publish(e);
        }
    }

    /// <summary>
    /// 元素池扩展方法
    /// </summary>
    public static class ElementPoolExtensions
    {
        /// <summary>
        /// 计算多色卡的可选元素类型
        /// </summary>
        public static List<ManaType> GetAvailableManaTypes(
            this Card card,
            ElementPoolSystem elementPool,
            Player player)
        {
            var result = new List<ManaType>();

            foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
            {
                if (elementPool.GetElementCount(mana, player) > 0)
                {
                    result.Add(mana);
                }
            }

            return result;
        }
    }
}
