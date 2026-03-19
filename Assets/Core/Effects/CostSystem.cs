using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using CardCore.Data;

namespace CardCore
{
    #region 枚举定义

    /// <summary>
    /// 资源来源区域
    /// </summary>
    public enum ResourceZone
    {
        /// <summary>手牌</summary>
        Hand,

        /// <summary>场上单位</summary>
        Battlefield,

        /// <summary>额外卡组</summary>
        ExtraDeck,

        /// <summary>墓地</summary>
        Graveyard,
    }

    /// <summary>
    /// 资源去向区域
    /// </summary>
    public enum DestinationZone
    {
        /// <summary>墓地（可回收）</summary>
        Graveyard,

        /// <summary>除外（永久移除，无法回收）</summary>
        Exile,
    }

    /// <summary>
    /// 预支类型
    /// </summary>
    public enum PrepayType
    {
        /// <summary>预支抽卡 - 本回合多抽，下回合少抽</summary>
        DrawCard,

        /// <summary>预支地牌产出 - 已横置的地牌再次产生元素</summary>
        ElementProduction,

        /// <summary>预支攻击次数</summary>
        AttackChance,

        /// <summary>预支下回合行动</summary>
        NextTurnAction,
    }

    #endregion

    #region 第一类：元素代价

    /// <summary>
    /// 元素代价
    /// 使用 ElementPoolSystem 进行元素消耗
    /// </summary>
    [Serializable]
    public class ElementCost
    {
        /// <summary>元素类型（Gray = 任意类型）</summary>
        public ManaType ManaType;

        /// <summary>数量</summary>
        public int Amount;

        /// <summary>
        /// 检查是否足够
        /// </summary>
        public bool CanPay(ElementPoolSystem elementPool, Player player)
        {
            if (ManaType == ManaType.灰色)
            {
                // 任意类型，检查总元素数
                int total = 0;
                foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
                {
                    if (type != ManaType.灰色)
                        total += elementPool.GetElementCount(type, player);
                }
                return total >= Amount;
            }
            return elementPool.CanConsume(ManaType, Amount, player);
        }

        /// <summary>
        /// 支付
        /// </summary>
        public bool Pay(ElementPoolSystem elementPool, Player player, Effect source = null)
        {
            if (ManaType == ManaType.灰色)
            {
                // 任意类型，按顺序消耗
                int remaining = Amount;
                foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
                {
                    if (type == ManaType.灰色) continue;
                    int available = elementPool.GetElementCount(type, player);
                    int consume = Math.Min(available, remaining);
                    if (consume > 0)
                    {
                        elementPool.ConsumeElement(type, consume, source, player);
                        remaining -= consume;
                    }
                    if (remaining <= 0) break;
                }
                return remaining == 0;
            }
            return elementPool.ConsumeElement(ManaType, Amount, source, player);
        }

        /// <summary>
        /// 获取描述
        /// </summary>
        public string GetDescription()
        {
            string typeName = ManaType == ManaType.灰色 ? "任意" : ManaType.ToString();
            return $"消耗{Amount}{typeName}元素";
        }
    }

    #endregion

    #region 第二类：资源代价

    /// <summary>
    /// 资源代价
    /// </summary>
    [Serializable]
    public class ResourceCost
    {
        /// <summary>来源区域</summary>
        public ResourceZone FromZone;

        /// <summary>去向区域</summary>
        public DestinationZone ToZone;

        /// <summary>数量</summary>
        public int Count = 1;

        /// <summary>筛选条件（可选）</summary>
        public TargetFilter Filter;

        /// <summary>是否需要指定特定卡牌</summary>
        public bool RequireSelection = true;

        /// <summary>
        /// 获取资源类型名称
        /// </summary>
        public string GetCostName()
        {
            string fromName = FromZone switch
            {
                ResourceZone.Hand => "手牌",
                ResourceZone.Battlefield => "场上单位",
                ResourceZone.ExtraDeck => "额外卡组",
                ResourceZone.Graveyard => "墓地资源",
                _ => "资源"
            };

            string toName = ToZone switch
            {
                DestinationZone.Graveyard => "送入墓地",
                DestinationZone.Exile => "除外",
                _ => "支付"
            };

            return $"{fromName}{toName}";
        }

        /// <summary>
        /// 获取完整描述
        /// </summary>
        public string GetDescription()
        {
            string fromName = FromZone switch
            {
                ResourceZone.Hand => "手牌",
                ResourceZone.Battlefield => "场上单位",
                ResourceZone.ExtraDeck => "额外卡组",
                ResourceZone.Graveyard => "墓地卡牌",
                _ => "资源"
            };

            string action = ToZone switch
            {
                DestinationZone.Graveyard => Count > 1 ? $"送{Count}张入墓地" : "送入墓地",
                DestinationZone.Exile => Count > 1 ? $"除外{Count}张" : "除外",
                _ => "支付"
            };

            return $"{fromName}{action}";
        }
    }

    /// <summary>
    /// 常用资源代价定义
    /// </summary>
    public static class CommonResourceCosts
    {
        /// <summary>弃牌（手牌→墓地）</summary>
        public static ResourceCost Discard(int count = 1) => new ResourceCost
        {
            FromZone = ResourceZone.Hand,
            ToZone = DestinationZone.Graveyard,
            Count = count
        };

        /// <summary>手牌除外</summary>
        public static ResourceCost ExileFromHand(int count = 1) => new ResourceCost
        {
            FromZone = ResourceZone.Hand,
            ToZone = DestinationZone.Exile,
            Count = count
        };

        /// <summary>祭祀（场上→墓地）</summary>
        public static ResourceCost Sacrifice(int count = 1) => new ResourceCost
        {
            FromZone = ResourceZone.Battlefield,
            ToZone = DestinationZone.Graveyard,
            Count = count
        };

        /// <summary>场上除外</summary>
        public static ResourceCost ExileFromField(int count = 1) => new ResourceCost
        {
            FromZone = ResourceZone.Battlefield,
            ToZone = DestinationZone.Exile,
            Count = count
        };

        /// <summary>墓地除外</summary>
        public static ResourceCost ExileFromGraveyard(int count = 1) => new ResourceCost
        {
            FromZone = ResourceZone.Graveyard,
            ToZone = DestinationZone.Exile,
            Count = count
        };

        /// <summary>额外卡组送墓</summary>
        public static ResourceCost ExtraDeckToGrave(int count = 1) => new ResourceCost
        {
            FromZone = ResourceZone.ExtraDeck,
            ToZone = DestinationZone.Graveyard,
            Count = count
        };

        /// <summary>额外卡组除外</summary>
        public static ResourceCost ExileFromExtraDeck(int count = 1) => new ResourceCost
        {
            FromZone = ResourceZone.ExtraDeck,
            ToZone = DestinationZone.Exile,
            Count = count
        };
    }

    #endregion

    #region 第三类：预支代价

    /// <summary>
    /// 预支代价
    /// </summary>
    [Serializable]
    public class PrepayCost
    {
        /// <summary>预支类型</summary>
        public PrepayType PrepayType;

        /// <summary>预支数量</summary>
        public int Amount;

        /// <summary>偿还回合数（通常为1）</summary>
        public int RepayTurns = 1;

        /// <summary>
        /// 获取预支描述
        /// </summary>
        public string GetDescription()
        {
            return PrepayType switch
            {
                PrepayType.DrawCard => Amount > 1 ? $"预支{Amount}次抽卡" : "预支1次抽卡",
                PrepayType.ElementProduction => Amount > 1 ? $"预支{Amount}次元素产出" : "预支1次元素产出",
                PrepayType.AttackChance => Amount > 1 ? $"预支{Amount}次攻击机会" : "预支1次攻击机会",
                PrepayType.NextTurnAction => $"预支下回合{Amount}次行动",
                _ => "预支"
            };
        }
    }

    /// <summary>
    /// 预支记录追踪器
    /// 追踪玩家预支状态，在对应时机结算
    /// </summary>
    public class PrepayTracker
    {
        /// <summary>已预支的抽卡次数（下回合需要扣除）</summary>
        public int PrepaidDraws { get; private set; }

        /// <summary>已预支的元素产出次数</summary>
        public int PrepaidElementProduction { get; private set; }

        /// <summary>已预支的攻击次数</summary>
        public int PrepaidAttacks { get; private set; }

        /// <summary>已预支的下回合行动次数</summary>
        public int PrepaidNextTurnActions { get; private set; }

        /// <summary>
        /// 记录预支
        /// </summary>
        public void RecordPrepay(PrepayCost prepay)
        {
            switch (prepay.PrepayType)
            {
                case PrepayType.DrawCard:
                    PrepaidDraws += prepay.Amount;
                    break;
                case PrepayType.ElementProduction:
                    PrepaidElementProduction += prepay.Amount;
                    break;
                case PrepayType.AttackChance:
                    PrepaidAttacks += prepay.Amount;
                    break;
                case PrepayType.NextTurnAction:
                    PrepaidNextTurnActions += prepay.Amount;
                    break;
            }
        }

        /// <summary>
        /// 结算预支抽卡（回合开始时调用，返回需要扣除的抽卡数量）
        /// </summary>
        public int SettlePrepaidDraws()
        {
            int prepaid = PrepaidDraws;
            PrepaidDraws = 0;
            return prepaid;
        }

        /// <summary>
        /// 结算预支元素产出
        /// </summary>
        public int SettlePrepaidElements()
        {
            int prepaid = PrepaidElementProduction;
            PrepaidElementProduction = 0;
            return prepaid;
        }

        /// <summary>
        /// 结算预支攻击
        /// </summary>
        public int SettlePrepaidAttacks()
        {
            int prepaid = PrepaidAttacks;
            PrepaidAttacks = 0;
            return prepaid;
        }

        /// <summary>
        /// 检查是否可以预支（防止无限预支）
        /// </summary>
        public bool CanPrepay(PrepayType type, int amount, int maxPrepay = 3)
        {
            return type switch
            {
                PrepayType.DrawCard => PrepaidDraws + amount <= maxPrepay,
                PrepayType.ElementProduction => PrepaidElementProduction + amount <= maxPrepay,
                PrepayType.AttackChance => PrepaidAttacks + amount <= maxPrepay,
                PrepayType.NextTurnAction => PrepaidNextTurnActions + amount <= maxPrepay,
                _ => false
            };
        }

        /// <summary>
        /// 重置所有预支记录
        /// </summary>
        public void Reset()
        {
            PrepaidDraws = 0;
            PrepaidElementProduction = 0;
            PrepaidAttacks = 0;
            PrepaidNextTurnActions = 0;
        }
    }

    #endregion

    #region 完整代价定义

    /// <summary>
    /// 效果发动代价（可包含多类代价）
    /// </summary>
    [Serializable]
    public class ActivationCost
    {
        /// <summary>第一类：元素代价</summary>
        public List<ElementCost> ElementCosts = new List<ElementCost>();

        /// <summary>第二类：资源代价</summary>
        public List<ResourceCost> ResourceCosts = new List<ResourceCost>();

        /// <summary>第三类：预支代价</summary>
        public List<PrepayCost> PrepayCosts = new List<PrepayCost>();

        /// <summary>速度提升（额外支付可提升速度）</summary>
        public List<SpeedBoostPayment> SpeedBoosts = new List<SpeedBoostPayment>();

        /// <summary>
        /// 是否为空代价
        /// </summary>
        public bool IsEmpty =>
            ElementCosts.Count == 0 &&
            ResourceCosts.Count == 0 &&
            PrepayCosts.Count == 0 &&
            SpeedBoosts.Count == 0;

        /// <summary>
        /// 获取代价描述
        /// </summary>
        public string GetDescription()
        {
            if (IsEmpty)
                return "无代价";

            var parts = new List<string>();

            // 元素代价
            foreach (var cost in ElementCosts)
                parts.Add(cost.GetDescription());

            // 资源代价
            foreach (var cost in ResourceCosts)
                parts.Add(cost.GetDescription());

            // 预支代价
            foreach (var cost in PrepayCosts)
                parts.Add(cost.GetDescription());

            // 速度提升
            foreach (var boost in SpeedBoosts)
                parts.Add($"提升速度+{boost.SpeedIncrease}");

            return string.Join("，", parts);
        }

        /// <summary>
        /// 创建空代价
        /// </summary>
        public static ActivationCost None => new ActivationCost();

        /// <summary>
        /// 合并代价
        /// </summary>
        public ActivationCost Merge(ActivationCost other)
        {
            var result = new ActivationCost();
            result.ElementCosts.AddRange(ElementCosts);
            result.ElementCosts.AddRange(other.ElementCosts);
            result.ResourceCosts.AddRange(ResourceCosts);
            result.ResourceCosts.AddRange(other.ResourceCosts);
            result.PrepayCosts.AddRange(PrepayCosts);
            result.PrepayCosts.AddRange(other.PrepayCosts);
            result.SpeedBoosts.AddRange(SpeedBoosts);
            result.SpeedBoosts.AddRange(other.SpeedBoosts);
            return result;
        }
    }

    #endregion

    #region 代价支付器

    /// <summary>
    /// 代价支付器
    /// 负责检查和执行代价支付
    /// </summary>
    public class CostPayer
    {
        private ZoneManager _zoneManager;
        private ElementPoolSystem _elementPoolSystem;

        public CostPayer(ZoneManager zoneManager, ElementPoolSystem elementPoolSystem)
        {
            _zoneManager = zoneManager;
            _elementPoolSystem = elementPoolSystem;
        }

        /// <summary>
        /// 检查是否可以支付
        /// </summary>
        public bool CanPay(Player player, ActivationCost cost)
        {
            // 检查元素代价
            foreach (var elementCost in cost.ElementCosts)
            {
                if (!elementCost.CanPay(_elementPoolSystem, player))
                    return false;
            }

            // 检查资源代价
            foreach (var resourceCost in cost.ResourceCosts)
            {
                int available = CountAvailableResources(player, resourceCost);
                if (available < resourceCost.Count)
                    return false;
            }

            // 检查预支代价
            foreach (var prepayCost in cost.PrepayCosts)
            {
                if (!CanPrepay(player, prepayCost))
                    return false;
            }

            // 检查速度提升代价
            foreach (var speedBoost in cost.SpeedBoosts)
            {
                if (!CanPay(player, speedBoost.ActualCost))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 执行支付
        /// </summary>
        /// <param name="player">支付者</param>
        /// <param name="cost">代价</param>
        /// <param name="selectedCards">选中的卡牌（用于需要指定目标的资源代价）</param>
        /// <returns>是否支付成功</returns>
        public bool Pay(Player player, ActivationCost cost, Dictionary<ResourceCost, List<Card>> selectedCards = null)
        {
            if (!CanPay(player, cost))
                return false;

            // 1. 支付元素代价
            foreach (var elementCost in cost.ElementCosts)
            {
                elementCost.Pay(_elementPoolSystem, player);
            }

            // 2. 支付资源代价
            foreach (var resourceCost in cost.ResourceCosts)
            {
                List<Card> cards = null;
                if (selectedCards != null && selectedCards.ContainsKey(resourceCost))
                    cards = selectedCards[resourceCost];

                PayResourceCost(player, resourceCost, cards);
            }

            // 3. 记录预支代价
            foreach (var prepayCost in cost.PrepayCosts)
            {
                player.PrepayTracker.RecordPrepay(prepayCost);
                ExecutePrepay(player, prepayCost);
            }

            // 4. 支付速度提升代价
            foreach (var speedBoost in cost.SpeedBoosts)
            {
                Pay(player, speedBoost.ActualCost, selectedCards);
            }

            return true;
        }

        /// <summary>
        /// 获取需要玩家选择的资源代价
        /// </summary>
        public List<ResourceCost> GetCostsRequiringSelection(ActivationCost cost)
        {
            return cost.ResourceCosts.Where(c => c.RequireSelection).ToList();
        }

        /// <summary>
        /// 获取可选的卡牌列表
        /// </summary>
        public List<Card> GetSelectableCards(Player player, ResourceCost cost)
        {
            var cards = GetCardsInZone(player, cost.FromZone);

            if (cost.Filter != null)
                cards = ApplyFilter(cards, cost.Filter);

            return cards;
        }

        #region 私有方法

        private int CountAvailableResources(Player player, ResourceCost cost)
        {
            var cards = GetCardsInZone(player, cost.FromZone);

            if (cost.Filter != null)
                cards = ApplyFilter(cards, cost.Filter);

            return cards.Count;
        }

        private List<Card> GetCardsInZone(Player player, ResourceZone zone)
        {
            var container = _zoneManager.GetZoneContainer(player);
            return zone switch
            {
                ResourceZone.Hand => container.GetCards(Zone.Hand),
                ResourceZone.Battlefield => container.GetCards(Zone.Battlefield),
                ResourceZone.Graveyard => container.GetCards(Zone.Graveyard),
                ResourceZone.ExtraDeck => new List<Card>(), // TODO: 额外卡组支持
                _ => new List<Card>()
            };
        }

        private List<Card> ApplyFilter(List<Card> cards, TargetFilter filter)
        {
            if (filter == null)
                return cards;

            var result = new List<Card>();
            foreach (var card in cards)
            {
                if (filter.Matches(card, null, null))
                    result.Add(card);
            }
            return result;
        }

        private void PayResourceCost(Player player, ResourceCost cost, List<Card> selectedCards)
        {
            var container = _zoneManager.GetZoneContainer(player);

            // 如果没有指定卡牌，自动选择
            if (selectedCards == null || selectedCards.Count == 0)
            {
                var available = GetSelectableCards(player, cost);
                selectedCards = available.Take(cost.Count).ToList();
            }

            foreach (var card in selectedCards.Take(cost.Count))
            {
                // 从来源区域移除
                Zone fromZone = cost.FromZone switch
                {
                    ResourceZone.Hand => Zone.Hand,
                    ResourceZone.Battlefield => Zone.Battlefield,
                    ResourceZone.Graveyard => Zone.Graveyard,
                    ResourceZone.ExtraDeck => Zone.Deck, // 暂用Deck代替
                    _ => Zone.Hand
                };
                container.Remove(card, fromZone);

                // 添加到目标区域
                Zone toZone = cost.ToZone switch
                {
                    DestinationZone.Graveyard => Zone.Graveyard,
                    DestinationZone.Exile => Zone.Exile,
                    _ => Zone.Graveyard
                };
                container.Add(card, toZone);
            }
        }

        private bool CanPrepay(Player player, PrepayCost prepay)
        {
            // 检查预支上限
            return player.PrepayTracker.CanPrepay(prepay.PrepayType, prepay.Amount);
        }

        private void ExecutePrepay(Player player, PrepayCost prepay)
        {
            switch (prepay.PrepayType)
            {
                case PrepayType.DrawCard:
                    // 立即抽卡
                    for (int i = 0; i < prepay.Amount; i++)
                    {
                        var container = _zoneManager.GetZoneContainer(player);
                        var deck = container.GetCards(Zone.Deck);
                        if (deck.Count > 0)
                        {
                            var card = deck[0];
                            container.Move(card, Zone.Deck, Zone.Hand);
                        }
                    }
                    break;

                case PrepayType.ElementProduction:
                    // 预支元素产出 - 直接添加临时元素
                    var pool = _elementPoolSystem.GetPool(player);
                    // 添加任意类型元素（灰色）
                    if (!pool.AvailableMana.ContainsKey(ManaType.灰色))
                        pool.AvailableMana[ManaType.灰色] = 0;
                    pool.AvailableMana[ManaType.灰色] += prepay.Amount;
                    break;

                case PrepayType.AttackChance:
                    // 增加本回合攻击次数
                    // TODO: 需要战斗系统支持
                    break;

                case PrepayType.NextTurnAction:
                    // 预支下回合行动
                    // TODO: 需要回合系统支持
                    break;
            }
        }

        private void DrawCard(Player player)
        {
            var container = _zoneManager.GetZoneContainer(player);
            var deck = container.GetCards(Zone.Deck);
            if (deck.Count > 0)
            {
                var card = deck[0];
                container.Move(card, Zone.Deck, Zone.Hand);
            }
        }

        #endregion
    }

    #endregion
}
