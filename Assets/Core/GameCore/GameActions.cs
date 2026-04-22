using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 玩家可执行的操作 — 统一入口
    /// 供 UI/AI 调用，封装规则校验
    /// </summary>
    public static class GameActions
    {
        // ======================================== 准备阶段操作 ========================================

        /// <summary>
        /// 准备阶段：将手牌放入元素池
        /// </summary>
        public static bool AddToElementPool(GameCore core, Player player, Card card)
        {
            if (core == null || player == null || card == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Standby) return false;

            // 检查卡牌在手中
            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) return false;

            // 放入元素池
            var elementPool = core.ElementPool;
            if (!elementPool.AddCardToPool(card, player))
                return false;

            // 从手牌移到元素池区域
            core.ZoneManager.MoveCard(card, player, Zone.Hand, Zone.ElementPool);

            return true;
        }

        /// <summary>
        /// 准备阶段：跳过放元素池
        /// </summary>
        public static bool SkipElementPool(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Standby) return false;

            core.TurnEngine.AdvanceFromStandby();
            return true;
        }

        /// <summary>
        /// 从指示物获得一个元素（每回合一次）
        /// </summary>
        public static bool GainElementFromToken(GameCore core, Player player, ManaType type)
        {
            if (core == null || player == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;

            var elementPool = core.ElementPool;
            if (!elementPool.GainElementFromToken(type, player))
                return false;

            // 检查耗尽卡牌
            elementPool.CheckDepletedCards(player, core.ZoneManager);

            return true;
        }

        // ======================================== 主阶段操作 ========================================

        /// <summary>
        /// 主阶段：打出一张牌
        /// </summary>
        public static bool PlayCard(GameCore core, Player player, Card card)
        {
            if (core == null || player == null || card == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            // 检查卡牌在手中
            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) return false;

            // 读取费用并检查
            var cost = GetCardCost(card);
            var elementPool = core.ElementPool;

            if (!CanAfford(elementPool, cost, player))
                return false;

            // 支付费用
            elementPool.PayCost(cost, player);

            // 移动卡牌到战场
            core.ZoneManager.MoveCard(card, player, Zone.Hand, Zone.Battlefield);

            // 设置控制者
            card.SetController(player);

            // 设置战场属性
            if (card is IHasPower hasPower && card is CardDataWrapper cdw)
            {
                // 属性从 CardData 继承，已在 CardWrapper 构造器中设置
            }

            // 注册卡牌效果（含关键词触发效果）
            var cardEffects = new List<EffectDefinition>();
            var cardDataEffects = GetCardEffectDefinitions(card);
            if (cardDataEffects != null)
                cardEffects.AddRange(cardDataEffects);

            // 注册关键词触发的效果
            var keywordEffects = KeywordEffectMapper.CreateAllTriggeredEffects(card);
            if (keywordEffects != null)
                cardEffects.AddRange(keywordEffects);

            if (cardEffects.Count > 0)
            {
                var effectSystem = EffectSystemManager.Instance;
                effectSystem.RegisterCardEffects(card, cardEffects, player);
            }

            // 触发出场事件
            EventManager.Instance.Publish(new CardPlayEvent
            {
                Player = player,
                PlayedCard = card
            });

            return true;
        }

        /// <summary>
        /// 主阶段：发起攻击（炉石式，随时可攻击）
        /// </summary>
        public static bool DeclareAttack(GameCore core, Player player, Entity attacker, Entity target)
        {
            if (core == null || player == null) return false;
            if (!core.TurnEngine.CanCombatAction()) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;

            var combat = core.CombatSystem;
            if (!combat.CanDeclareAttack(attacker, player))
                return false;

            combat.DeclareAttack(attacker, target);
            return true;
        }

        // ======================================== 回合控制 ========================================

        /// <summary>
        /// 结束回合
        /// </summary>
        public static bool EndTurn(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;

            core.TurnEngine.EndTurn();
            return true;
        }

        // ======================================== 栈操作 ========================================

        /// <summary>
        /// 速度发动：玩家主动发动一个效果
        /// </summary>
        public static bool ActivateEffect(GameCore core, Player player, EffectDefinition effect, Card source, int paidBoost = 0)
        {
            if (core == null || player == null || effect == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            var pending = PendingEffect.Create(
                effect,
                source,
                player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                paidBoost: paidBoost);

            return core.StackEngine.PlayerActivateVoluntary(pending);
        }

        /// <summary>
        /// 玩家 Pass 优先权
        /// </summary>
        public static bool PassPriority(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            if (core.StackEngine.CurrentPriorityHolder != player) return false;

            core.StackEngine.PassPriority(player);
            return true;
        }

        // ======================================== 内部方法 ========================================

        /// <summary>
        /// 从卡牌读取费用
        /// </summary>
        private static Dictionary<int, float> GetCardCost(Card card)
        {
            if (card is IHasCost hasCost && hasCost.Cost != null)
                return hasCost.Cost;

            // 默认费用：灰色1点
            return new Dictionary<int, float> { { (int)ManaType.Gray, 1 } };
        }

        /// <summary>
        /// 检查是否能支付费用
        /// </summary>
        private static bool CanAfford(ElementPoolSystem elementPool, Dictionary<int, float> cost, Player player)
        {
            foreach (var kvp in cost)
            {
                ManaType type = (ManaType)kvp.Key;
                int amount = (int)kvp.Value;
                if (elementPool.GetAvailableManaCount(type, player) < amount)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 从卡牌获取效果定义（如有）
        /// </summary>
        private static List<EffectDefinition> GetCardEffectDefinitions(Card card)
        {
            // TODO: 从卡牌的 EffectData 列表转换为 EffectDefinition
            return new List<EffectDefinition>();
        }
    }

    /// <summary>
    /// CardData 包装标记接口
    /// </summary>
    public interface CardDataWrapper { }
}
