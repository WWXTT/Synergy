using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    // ================================================================
    // 代价接口与数据
    // ================================================================

    /// <summary>
    /// 代价类型
    /// </summary>
    public enum CostType
    {
        /// <summary>元素消耗</summary>
        ElementConsume,
        /// <summary>弃牌</summary>
        DiscardCard,
        /// <summary>扣除玩家生命</summary>
        LifePayment,
        /// <summary>沉睡（翻面+苏醒倒计时）</summary>
        Sleep,
        /// <summary>召唤素材（额外卡组条件）</summary>
        SummonMaterial,
    }

    /// <summary>
    /// 代价实例
    /// </summary>
    [Serializable]
    public class CostInstance
    {
        /// <summary>代价类型</summary>
        public CostType Type;

        /// <summary>数值（元素数量/弃牌数/生命值/沉睡回合数）</summary>
        public int Value;

        /// <summary>法力类型（元素消耗专用）</summary>
        public ManaType ManaType;

        /// <summary>沉睡持续回合数</summary>
        public int TurnDuration;

        /// <summary>召唤方式（召唤素材专用）</summary>
        public SummonMethod SummonMethod;

        /// <summary>素材筛选器（召唤素材专用）</summary>
        public ITargetFilter TargetFilter;
    }

    /// <summary>
    /// 代价执行上下文
    /// </summary>
    public class CostContext
    {
        /// <summary>支付者</summary>
        public Player Payer;

        /// <summary>区域管理器</summary>
        public ZoneManager ZoneManager;

        /// <summary>元素池系统</summary>
        public ElementPoolSystem ElementPool;

        /// <summary>效果来源</summary>
        public Entity Source;
    }

    // ================================================================
    // 代价处理器接口与注册表
    // ================================================================

    /// <summary>
    /// 代价处理器接口
    /// </summary>
    public interface ICostHandler
    {
        /// <summary>处理的代价类型</summary>
        CostType CostType { get; }

        /// <summary>检查是否可以支付</summary>
        bool CanPay(CostInstance cost, CostContext context);

        /// <summary>执行支付</summary>
        void Pay(CostInstance cost, CostContext context);

        /// <summary>获取代价描述</summary>
        string GetDescription(CostInstance cost);
    }

    /// <summary>
    /// 代价处理器注册表
    /// </summary>
    public static class CostHandlerRegistry
    {
        private static readonly Dictionary<CostType, ICostHandler> _handlers
            = new Dictionary<CostType, ICostHandler>();

        public static void Register(ICostHandler handler)
        {
            if (handler != null)
                _handlers[handler.CostType] = handler;
        }

        public static ICostHandler GetHandler(CostType type)
        {
            return _handlers.TryGetValue(type, out var handler) ? handler : null;
        }

        public static bool CanPay(CostInstance cost, CostContext context)
        {
            var handler = GetHandler(cost.Type);
            return handler?.CanPay(cost, context) ?? false;
        }

        public static bool Pay(CostInstance cost, CostContext context)
        {
            var handler = GetHandler(cost.Type);
            if (handler == null) return false;
            if (!handler.CanPay(cost, context)) return false;
            handler.Pay(cost, context);
            return true;
        }

        /// <summary>
        /// 检查所有代价是否都可以支付
        /// </summary>
        public static bool CanPayAll(List<CostInstance> costs, CostContext context)
        {
            return costs.All(c => CanPay(c, context));
        }

        /// <summary>
        /// 支付所有代价（不回滚：如果中途失败，已支付的不退回）
        /// </summary>
        public static bool PayAll(List<CostInstance> costs, CostContext context)
        {
            foreach (var cost in costs)
            {
                if (!Pay(cost, context))
                    return false;
            }
            return true;
        }
    }

    // ================================================================
    // 内置代价处理器
    // ================================================================

    /// <summary>
    /// 元素消耗代价处理器
    /// 复用 ElementPoolSystem 的 ConsumeElement / CanConsume
    /// </summary>
    public class ElementConsumeCostHandler : ICostHandler
    {
        public CostType CostType => CostType.ElementConsume;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.ElementPool == null || context.Payer == null) return false;
            var costDict = new Dictionary<int, float> { { (int)cost.ManaType, cost.Value } };
            return context.ElementPool.CanPayCost(costDict, context.Payer);
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            var costDict = new Dictionary<int, float> { { (int)cost.ManaType, cost.Value } };
            context.ElementPool.PayCost(costDict, context.Payer);
        }

        public string GetDescription(CostInstance cost)
        {
            return $"消耗 {cost.Value} 点{cost.ManaType} 元素";
        }
    }

    /// <summary>
    /// 弃牌代价处理器
    /// </summary>
    public class DiscardCardCostHandler : ICostHandler
    {
        public CostType CostType => CostType.DiscardCard;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.ZoneManager == null || context.Payer == null) return false;
            var hand = context.ZoneManager.GetCards(context.Payer, Zone.Hand);
            return hand.Count >= cost.Value;
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            var hand = context.ZoneManager.GetCards(context.Payer, Zone.Hand);
            for (int i = 0; i < cost.Value && i < hand.Count; i++)
            {
                var card = hand[hand.Count - 1 - i]; // 从最后一张开始弃
                context.ZoneManager.MoveCard(card, context.Payer, Zone.Hand, Zone.Graveyard);
                EventManager.Instance.Publish(new CardDiscardCostEvent
                {
                    Player = context.Payer,
                    Card = card,
                    Source = context.Source
                });
            }
        }

        public string GetDescription(CostInstance cost)
        {
            return $"弃 {cost.Value} 张牌";
        }
    }

    /// <summary>
    /// 扣除生命代价处理器
    /// </summary>
    public class LifePaymentCostHandler : ICostHandler
    {
        public CostType CostType => CostType.LifePayment;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.Payer == null) return false;
            return context.Payer.Life > cost.Value;
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            context.Payer.Life -= cost.Value;
            EventManager.Instance.Publish(new LifePaymentCostEvent
            {
                Player = context.Payer,
                Amount = cost.Value,
                Source = context.Source
            });
        }

        public string GetDescription(CostInstance cost)
        {
            return $"支付 {cost.Value} 点生命";
        }
    }

    /// <summary>
    /// 沉睡代价处理器
    /// 翻面（Tap）+ 添加"Sleeping"关键词 + 添加苏醒倒计时counter
    /// </summary>
    public class SleepCostHandler : ICostHandler
    {
        public CostType CostType => CostType.Sleep;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            // 来源必须是场上生物且未横置
            return context.Source != null
                && context.Source.IsAlive
                && !context.Source.IsTapped()
                && context.Source is Card;
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            var target = context.Source;
            int duration = cost.TurnDuration > 0 ? cost.TurnDuration : cost.Value;

            target.Tap();
            target.AddKeyword("Sleeping", DurationType.Permanent);
            target.AddCounters("Awakening", duration);

            EventManager.Instance.Publish(new SleepCostEvent
            {
                Target = target,
                TurnDuration = duration,
                Source = context.Source
            });
        }

        public string GetDescription(CostInstance cost)
        {
            int duration = cost.TurnDuration > 0 ? cost.TurnDuration : cost.Value;
            return $"沉睡 {duration} 回合";
        }
    }

    /// <summary>
    /// 召唤素材代价处理器
    /// 验证素材条件，通过后将素材送入墓地
    /// </summary>
    public class SummonMaterialCostHandler : ICostHandler
    {
        public CostType CostType => CostType.SummonMaterial;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.ZoneManager == null || context.Payer == null) return false;
            var battlefield = context.ZoneManager.GetCards(context.Payer, Zone.Battlefield);

            if (cost.TargetFilter != null)
            {
                var candidates = battlefield.Cast<Entity>().ToList();
                var effectCtx = new EffectExecutionContext { Controller = context.Payer, Source = context.Source };
                return cost.TargetFilter.Filter(candidates, effectCtx).Count > 0;
            }

            return battlefield.Count >= cost.Value;
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            if (context.ZoneManager == null || context.Payer == null) return;

            var battlefield = context.ZoneManager.GetCards(context.Payer, Zone.Battlefield);
            List<Card> materials;

            if (cost.TargetFilter != null)
            {
                var candidates = battlefield.Cast<Entity>().ToList();
                var effectCtx = new EffectExecutionContext { Controller = context.Payer, Source = context.Source };
                var filtered = cost.TargetFilter.Filter(candidates, effectCtx);
                materials = filtered.OfType<Card>().ToList();
            }
            else
            {
                materials = battlefield.Take(cost.Value).ToList();
            }

            foreach (var mat in materials)
            {
                context.ZoneManager.MoveCard(mat, context.Payer, Zone.Battlefield, Zone.Graveyard);
            }

            EventManager.Instance.Publish(new SummonMaterialCostEvent
            {
                Player = context.Payer,
                Materials = materials,
                SummonMethod = cost.SummonMethod,
                Source = context.Source
            });
        }

        public string GetDescription(CostInstance cost)
        {
            if (cost.TargetFilter != null)
                return $"使用素材: {cost.TargetFilter.DisplayName}";
            return $"使用 {cost.Value} 个素材";
        }
    }

    // ================================================================
    // 代价相关事件
    // ================================================================

    /// <summary>弃牌代价事件</summary>
    public class CardDiscardCostEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card Card { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>生命支付代价事件</summary>
    public class LifePaymentCostEvent : GameEventBase
    {
        public Player Player { get; set; }
        public int Amount { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>沉睡代价事件</summary>
    public class SleepCostEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public int TurnDuration { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>召唤素材代价事件</summary>
    public class SummonMaterialCostEvent : GameEventBase
    {
        public Player Player { get; set; }
        public List<Card> Materials { get; set; }
        public SummonMethod SummonMethod { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 注册内置代价处理器
    /// </summary>
    public static class BuiltinCostHandlers
    {
        public static void RegisterAll()
        {
            CostHandlerRegistry.Register(new ElementConsumeCostHandler());
            CostHandlerRegistry.Register(new DiscardCardCostHandler());
            CostHandlerRegistry.Register(new LifePaymentCostHandler());
            CostHandlerRegistry.Register(new SleepCostHandler());
            CostHandlerRegistry.Register(new SummonMaterialCostHandler());
        }
    }
}
