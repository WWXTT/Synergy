using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 极简脚本 AI（P2）：仅用于驱动回合循环、产生可观测的状态变化，不追求强度或策略。
    /// 所有动作都走 GameActions / BattleController 既有入口；可行性由引擎校验（失败即跳过）。
    /// </summary>
    public sealed class SimpleAI
    {
        public void TakeTurn(BattleController ctrl)
        {
            var core = ctrl.Core;
            var me = ctrl.TurnPlayer;
            if (core == null || me == null || me != ctrl.P2) return;

            DoStandby(core, me);
            DoMain(core, me);
            DoCombat(ctrl, core, me);
            GameActions.EndTurn(core, me);
        }

        /// <summary>准备阶段：把一张手牌放进元素池（积累法力），然后跳过进入主阶段。</summary>
        private void DoStandby(GameCore core, Player me)
        {
            var hand = core.ZoneManager.GetCards(me, Zone.Hand);
            if (hand != null && hand.Count > 0)
                GameActions.AddToElementPool(core, me, hand[0]);

            GameActions.SkipElementPool(core, me);
        }

        /// <summary>主阶段：对手牌快照逐张尝试出牌；付不起 / 不合法的由引擎拒绝。</summary>
        private void DoMain(GameCore core, Player me)
        {
            // 快照：PlayCard 会改动手牌区，避免迭代时修改集合。
            var hand = new List<Card>(core.ZoneManager.GetCards(me, Zone.Hand));
            foreach (var card in hand)
            {
                // 需目标的法术：targets 传 null，引擎按各原子效果配置自动取候选前 N。
                GameActions.PlayCard(core, me, card, null);
            }
        }

        /// <summary>战斗：开战斗 → 每个可攻击的战场单位打脸 → 结算。</summary>
        private void DoCombat(BattleController ctrl, GameCore core, Player me)
        {
            ctrl.BeginCombat();

            var battlefield = new List<Card>(core.ZoneManager.GetCards(me, Zone.Battlefield));
            foreach (var unit in battlefield)
            {
                ctrl.DeclareAttack(me, unit, me.Opponent);
            }

            ctrl.ResolveCombat();
        }
    }
}
