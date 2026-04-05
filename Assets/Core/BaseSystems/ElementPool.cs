using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 元素池中的卡牌（带指示物）
    /// 基于卡牌的费用构成放置对应数量的元素指示物
    /// 每回合可取一个指示物获得对应元素
    /// 指示物耗尽后卡牌进入墓地
    /// </summary>
    public class PooledCard
    {
        public Card SourceCard { get; }

        /// <summary>剩余指示物 {Red:2, Gray:1}</summary>
        public Dictionary<ManaType, int> Tokens { get; private set; }

        /// <summary>初始指示物数量（用于显示）</summary>
        public Dictionary<ManaType, int> TotalTokens { get; }

        public PooledCard(Card card, Dictionary<ManaType, int> tokens)
        {
            SourceCard = card;
            Tokens = new Dictionary<ManaType, int>(tokens);
            TotalTokens = new Dictionary<ManaType, int>(tokens);
        }

        public bool HasToken(ManaType type)
        {
            return Tokens.ContainsKey(type) && Tokens[type] > 0;
        }

        /// <summary>是否所有指示物已耗尽</summary>
        public bool IsDepleted => Tokens.Values.Sum() == 0;

        /// <summary>获取指示物总数</summary>
        public int TotalTokenCount => Tokens.Values.Sum();

        /// <summary>
        /// 移除一个指定颜色的指示物
        /// </summary>
        /// <returns>true 表示该卡已耗尽</returns>
        public bool RemoveToken(ManaType type)
        {
            if (!HasToken(type))
                throw new InvalidOperationException($"PooledCard 没有颜色 {type} 的指示物");

            Tokens[type]--;
            return IsDepleted;
        }

        /// <summary>获取所有有剩余指示物的颜色</summary>
        public List<ManaType> GetAvailableColors()
        {
            return Tokens.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        }
    }

    /// <summary>
    /// 玩家元素池数据
    /// </summary>
    public class PlayerElementPool
    {
        /// <summary>池中的卡牌（带指示物）</summary>
        public List<PooledCard> PooledCards { get; } = new List<PooledCard>();

        /// <summary>本回合是否已获得过元素（每回合一次）</summary>
        public bool HasGainedTokenThisTurn { get; set; }

        /// <summary>当前可用元素（从指示物获得 + 临时效果添加）</summary>
        public Dictionary<ManaType, int> AvailableMana { get; } = new Dictionary<ManaType, int>();

        public PlayerElementPool()
        {
            foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
            {
                AvailableMana[mana] = 0;
            }
        }
    }

    /// <summary>
    /// 元素池系统
    /// 核心费用体系：
    /// 1. 准备阶段放1张手牌进元素池，基于费用生成指示物
    /// 2. 每回合一次，取1个指示物 → 获得1个对应元素
    /// 3. 指示物耗尽 → 卡牌进墓地
    /// 4. 出牌/发动效果时，从可用元素中支付费用
    /// </summary>
    public class ElementPoolSystem
    {
        private const int HAND_SIZE_LIMIT = 7;
        private const int MAX_POOL_SIZE = 12;

        private Dictionary<Player, PlayerElementPool> _playerPools =
            new Dictionary<Player, PlayerElementPool>();

        /// <summary>耗尽卡牌时用于移动到墓地</summary>
        public event Action<Card, Player> OnCardDepleted;

        // ======================================== 初始化 ========================================

        public void InitializePlayer(Player player)
        {
            if (!_playerPools.ContainsKey(player))
            {
                _playerPools[player] = new PlayerElementPool();
            }
        }

        public PlayerElementPool GetPool(Player player)
        {
            if (!_playerPools.ContainsKey(player))
                InitializePlayer(player);
            return _playerPools[player];
        }

        // ======================================== 放卡进元素池 ========================================

        /// <summary>
        /// 将卡牌放入元素池（准备阶段调用）
        /// 基于卡牌的费用构成生成指示物
        /// </summary>
        public bool AddCardToPool(Card card, Player owner)
        {
            if (card == null || owner == null) return false;

            var pool = GetPool(owner);

            // 检查池大小限制
            int totalTokensInPool = pool.PooledCards.Sum(pc => pc.TotalTokenCount);
            if (totalTokensInPool >= MAX_POOL_SIZE)
                return false;

            // 从卡牌读取费用构成
            var cost = GetCardCostAsTokens(card);
            if (cost.Values.Sum() == 0)
                return false;

            // 创建带指示物的池卡
            var pooledCard = new PooledCard(card, cost);
            pool.PooledCards.Add(pooledCard);

            PublishEvent(new ElementPoolAddEvent
            {
                Player = owner,
                AddedCard = card,
                Tokens = cost
            });

            return true;
        }

        // ======================================== 从指示物获得元素 ========================================

        /// <summary>
        /// 每回合一次：从指示物获得一个元素
        /// </summary>
        /// <param name="type">要获得的元素颜色</param>
        /// <returns>是否成功</returns>
        public bool GainElementFromToken(ManaType type, Player player)
        {
            var pool = GetPool(player);

            // 每回合一次限制
            if (pool.HasGainedTokenThisTurn)
                return false;

            // 找到有该颜色指示物的池卡
            var pooledCard = pool.PooledCards.FirstOrDefault(pc => pc.HasToken(type));
            if (pooledCard == null)
                return false;

            // 移除指示物，增加可用元素
            bool depleted = pooledCard.RemoveToken(type);
            pool.AvailableMana[type]++;
            pool.HasGainedTokenThisTurn = true;

            PublishEvent(new ElementPoolGainEvent
            {
                Player = player,
                GainedType = type,
                FromCard = pooledCard.SourceCard
            });

            // 检查是否耗尽
            if (depleted)
            {
                MoveDepletedCard(pooledCard, player);
            }

            return true;
        }

        // ======================================== 支付费用 ========================================

        /// <summary>
        /// 检查是否可以支付指定费用
        /// </summary>
        public bool CanPayCost(Dictionary<int, float> cost, Player player)
        {
            var pool = GetPool(player);

            foreach (var kvp in cost)
            {
                ManaType type = (ManaType)kvp.Key;
                int amount = (int)kvp.Value;
                if (!pool.AvailableMana.ContainsKey(type) || pool.AvailableMana[type] < amount)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 支付费用（出牌时调用）
        /// </summary>
        public bool PayCost(Dictionary<int, float> cost, Player player)
        {
            var pool = GetPool(player);

            // 先检查是否够
            if (!CanPayCost(cost, player))
                return false;

            // 扣减可用元素
            foreach (var kvp in cost)
            {
                ManaType type = (ManaType)kvp.Key;
                int amount = (int)kvp.Value;
                pool.AvailableMana[type] -= amount;
            }

            PublishEvent(new ElementPoolPayEvent
            {
                Player = player,
                PaidCost = cost
            });

            return true;
        }

        // ======================================== 回合管理 ========================================

        /// <summary>
        /// 回合开始时重置使用标记
        /// </summary>
        public void ResetTurnUsage(Player player)
        {
            var pool = GetPool(player);
            pool.HasGainedTokenThisTurn = false;
        }

        /// <summary>
        /// 检查并移除所有耗尽的卡牌到墓地
        /// </summary>
        public void CheckDepletedCards(Player player)
        {
            var pool = GetPool(player);
            var depleted = pool.PooledCards.Where(pc => pc.IsDepleted).ToList();

            foreach (var pc in depleted)
            {
                MoveDepletedCard(pc, player);
            }
        }

        /// <summary>
        /// 检查并移除所有耗尽的卡牌到墓地（含区域移动）
        /// </summary>
        public void CheckDepletedCards(Player player, ZoneManager zoneManager)
        {
            var pool = GetPool(player);
            var depleted = pool.PooledCards.Where(pc => pc.IsDepleted).ToList();

            foreach (var pc in depleted)
            {
                pool.PooledCards.Remove(pc);

                PublishEvent(new ElementPoolDepleteEvent
                {
                    Player = player,
                    DepletedCard = pc.SourceCard
                });

                // 将耗尽卡牌移到墓地
                if (zoneManager != null)
                    zoneManager.MoveCard(pc.SourceCard, player, Zone.ElementPool, Zone.Graveyard);

                OnCardDepleted?.Invoke(pc.SourceCard, player);
            }
        }

        // ======================================== 查询 ========================================

        public int GetAvailableManaCount(ManaType type, Player player)
        {
            var pool = GetPool(player);
            return pool.AvailableMana.ContainsKey(type) ? pool.AvailableMana[type] : 0;
        }

        public int GetTotalAvailableMana(Player player)
        {
            return GetPool(player).AvailableMana.Values.Sum();
        }

        public bool HasGainedTokenThisTurn(Player player)
        {
            return GetPool(player).HasGainedTokenThisTurn;
        }

        public List<PooledCard> GetPooledCards(Player player)
        {
            return GetPool(player).PooledCards.ToList();
        }

        public List<ManaType> GetGainedableTypes(Player player)
        {
            var pool = GetPool(player);
            if (pool.HasGainedTokenThisTurn)
                return new List<ManaType>();

            return pool.PooledCards
                .SelectMany(pc => pc.GetAvailableColors())
                .Distinct()
                .ToList();
        }

        public int GetTotalTokensInPool(Player player)
        {
            return GetPool(player).PooledCards.Sum(pc => pc.TotalTokenCount);
        }

        // ======================================== 内部方法 ========================================

        private void MoveDepletedCard(PooledCard pooledCard, Player player)
        {
            var pool = GetPool(player);
            pool.PooledCards.Remove(pooledCard);

            PublishEvent(new ElementPoolDepleteEvent
            {
                Player = player,
                DepletedCard = pooledCard.SourceCard
            });

            OnCardDepleted?.Invoke(pooledCard.SourceCard, player);
        }

        /// <summary>
        /// 从卡牌读取费用，转换为指示物
        /// </summary>
        private Dictionary<ManaType, int> GetCardCostAsTokens(Card card)
        {
            var tokens = new Dictionary<ManaType, int>();

            // 从 IHasCost 接口读取费用
            if (card is IHasCost hasCost && hasCost.Cost != null)
            {
                foreach (var kvp in hasCost.Cost)
                {
                    ManaType type = (ManaType)kvp.Key;
                    int amount = (int)kvp.Value;
                    if (amount > 0)
                    {
                        tokens[type] = amount;
                    }
                }
            }

            // 如果卡牌没有费用（灰色1点），给一个默认灰色指示物
            if (tokens.Values.Sum() == 0)
            {
                tokens[ManaType.Gray] = 1;
            }

            return tokens;
        }

        // ======================================== 重置/工具 ========================================

        public void Reset()
        {
            foreach (var pool in _playerPools.Values)
            {
                pool.PooledCards.Clear();
                pool.HasGainedTokenThisTurn = false;
                foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
                {
                    pool.AvailableMana[mana] = 0;
                }
            }
        }

        public string GetStatusString(Player player)
        {
            var pool = GetPool(player);
            var result = $"元素池 ({pool.PooledCards.Count} 张卡):\n";

            foreach (var pc in pool.PooledCards)
            {
                var name = (pc.SourceCard as IHasName)?.CardName ?? pc.SourceCard.ID;
                var tokenStr = string.Join(", ", pc.Tokens.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));
                result += $"  [{name}] 指示物: {tokenStr}\n";
            }

            var manaStr = string.Join(", ", pool.AvailableMana.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));
            result += $"可用元素: {manaStr}\n";
            result += $"本回合已取指示物: {(pool.HasGainedTokenThisTurn ? "是" : "否")}";

            return result;
        }

        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            EventManager.Instance.Publish(e);
        }
    }

    // ======================================== 元素池事件 ========================================

    public class ElementPoolAddEvent : IGameEvent
    {
        public Player Player { get; set; }
        public Card AddedCard { get; set; }
        public Dictionary<ManaType, int> Tokens { get; set; }
    }

    public class ElementPoolGainEvent : IGameEvent
    {
        public Player Player { get; set; }
        public ManaType GainedType { get; set; }
        public Card FromCard { get; set; }
    }

    public class ElementPoolPayEvent : IGameEvent
    {
        public Player Player { get; set; }
        public Dictionary<int, float> PaidCost { get; set; }
    }

    public class ElementPoolDepleteEvent : IGameEvent
    {
        public Player Player { get; set; }
        public Card DepletedCard { get; set; }
    }

    // 保留旧事件名的兼容性
    public class ElementPoolConsumeEvent : IGameEvent
    {
        public Player Player { get; set; }
        public ManaType ManaType { get; set; }
        public int Amount { get; set; }
        public Effect Source { get; set; }
    }
}
