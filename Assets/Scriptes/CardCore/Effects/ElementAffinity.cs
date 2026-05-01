using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    /// <summary>
    /// 元素倾向 - 效果与元素颜色的关联
    /// </summary>
    [Serializable]
    public class ElementAffinity
    {
        /// <summary>主要元素颜色（Red/Blue/Green/Gray）</summary>
        public ManaType PrimaryColor { get; set; }

        /// <summary>是否可用任意颜色支付（灰色效果）</summary>
        public bool IsGeneric => PrimaryColor == ManaType.Gray;

        /// <summary>是否为特殊颜色（黑/白不在本次实现）</summary>
        public bool IsSpecialColor => PrimaryColor == ManaType.Black || PrimaryColor == ManaType.White;

        /// <summary>创建单色倾向</summary>
        public static ElementAffinity Single(ManaType color) => new ElementAffinity { PrimaryColor = color };

        /// <summary>创建通用倾向（灰色，可用任意颜色支付）</summary>
        public static ElementAffinity Generic => new ElementAffinity { PrimaryColor = ManaType.Gray };

        /// <summary>
        /// 获取颜色显示名称
        /// </summary>
        public string GetColorName()
        {
            return PrimaryColor switch
            {
                ManaType.Red => "红",
                ManaType.Blue => "蓝",
                ManaType.Green => "绿",
                ManaType.Gray => "通用",
                ManaType.White => "白",
                ManaType.Black => "黑",
                _ => PrimaryColor.ToString()
            };
        }
    }

    /// <summary>
    /// 元素支付验证器
    /// </summary>
    public static class ElementPaymentValidator
    {
        /// <summary>
        /// 验证是否可以支付指定倾向的代价
        /// </summary>
        /// <param name="affinity">元素倾向</param>
        /// <param name="availableMana">可用元素（按颜色分类）</param>
        /// <param name="amount">需要支付的数量</param>
        /// <returns>是否可以支付</returns>
        public static bool CanPay(ElementAffinity affinity, Dictionary<ManaType, int> availableMana, int amount)
        {
            if (amount <= 0) return true;

            // 特殊颜色暂不支持
            if (affinity.IsSpecialColor) return false;

            // 灰色效果：可用任意颜色支付
            if (affinity.IsGeneric)
            {
                int total = availableMana.Values.Sum();
                return total >= amount;
            }

            // 指定颜色：必须用该颜色支付
            return availableMana.TryGetValue(affinity.PrimaryColor, out int count) && count >= amount;
        }

        /// <summary>
        /// 尝试支付指定倾向的代价
        /// </summary>
        /// <param name="affinity">元素倾向</param>
        /// <param name="availableMana">可用元素（会被修改）</param>
        /// <param name="amount">需要支付的数量</param>
        /// <returns>是否支付成功</returns>
        public static bool TryPay(ElementAffinity affinity, Dictionary<ManaType, int> availableMana, int amount)
        {
            if (!CanPay(affinity, availableMana, amount))
                return false;

            if (amount <= 0) return true;

            if (affinity.IsGeneric)
            {
                // 灰色效果：从任意颜色扣除，优先使用非主要颜色
                int remaining = amount;

                // 优先使用灰色
                if (availableMana.TryGetValue(ManaType.Gray, out int grayCount) && grayCount > 0)
                {
                    int toUse = Math.Min(grayCount, remaining);
                    availableMana[ManaType.Gray] -= toUse;
                    remaining -= toUse;
                }

                // 然后按顺序使用其他颜色
                var colors = new[] { ManaType.Red, ManaType.Blue, ManaType.Green };
                foreach (var color in colors)
                {
                    if (remaining <= 0) break;
                    if (availableMana.TryGetValue(color, out int count) && count > 0)
                    {
                        int toUse = Math.Min(count, remaining);
                        availableMana[color] -= toUse;
                        remaining -= toUse;
                    }
                }

                return remaining == 0;
            }
            else
            {
                // 指定颜色：从该颜色扣除
                availableMana[affinity.PrimaryColor] -= amount;
                return true;
            }
        }

        /// <summary>
        /// 获取支付指定倾向代价所需的最优元素组合
        /// </summary>
        /// <param name="affinity">元素倾向</param>
        /// <param name="availableMana">可用元素</param>
        /// <param name="amount">需要支付的数量</param>
        /// <returns>支付方案（颜色到数量的映射），null表示无法支付</returns>
        public static Dictionary<ManaType, int> GetPaymentPlan(ElementAffinity affinity, Dictionary<ManaType, int> availableMana, int amount)
        {
            if (!CanPay(affinity, availableMana, amount))
                return null;

            var plan = new Dictionary<ManaType, int>();

            if (amount <= 0) return plan;

            if (affinity.IsGeneric)
            {
                int remaining = amount;

                // 优先使用灰色
                if (availableMana.TryGetValue(ManaType.Gray, out int grayCount) && grayCount > 0)
                {
                    int toUse = Math.Min(grayCount, remaining);
                    plan[ManaType.Gray] = toUse;
                    remaining -= toUse;
                }

                // 然后按顺序使用其他颜色
                var colors = new[] { ManaType.Red, ManaType.Blue, ManaType.Green };
                foreach (var color in colors)
                {
                    if (remaining <= 0) break;
                    if (availableMana.TryGetValue(color, out int count) && count > 0)
                    {
                        int toUse = Math.Min(count, remaining);
                        plan[color] = toUse;
                        remaining -= toUse;
                    }
                }
            }
            else
            {
                plan[affinity.PrimaryColor] = amount;
            }

            return plan;
        }
    }

    /// <summary>
    /// 预定义的元素倾向（仅红蓝绿灰）
    /// </summary>
    public static class ElementAffinities
    {
        #region 红色 - 伤害与破坏

        /// <summary>伤害效果</summary>
        public static ElementAffinity Damage => ElementAffinity.Single(ManaType.Red);

        /// <summary>燃烧效果</summary>
        public static ElementAffinity Burn => ElementAffinity.Single(ManaType.Red);

        /// <summary>破坏效果</summary>
        public static ElementAffinity Destruction => ElementAffinity.Single(ManaType.Red);

        /// <summary>敏捷效果</summary>
        public static ElementAffinity Haste => ElementAffinity.Single(ManaType.Red);

        /// <summary>突袭效果</summary>
        public static ElementAffinity Rush => ElementAffinity.Single(ManaType.Red);

        /// <summary>双击效果</summary>
        public static ElementAffinity DoubleStrike => ElementAffinity.Single(ManaType.Red);

        /// <summary>多次攻击效果</summary>
        public static ElementAffinity MultiAttack => ElementAffinity.Single(ManaType.Red);

        #endregion

        #region 蓝色 - 控制与知识

        /// <summary>抽卡效果</summary>
        public static ElementAffinity Draw => ElementAffinity.Single(ManaType.Blue);

        /// <summary>弹回手牌效果</summary>
        public static ElementAffinity Bounce => ElementAffinity.Single(ManaType.Blue);

        /// <summary>反制效果</summary>
        public static ElementAffinity Counter => ElementAffinity.Single(ManaType.Blue);

        /// <summary>横置效果</summary>
        public static ElementAffinity Tap => ElementAffinity.Single(ManaType.Blue);

        /// <summary>冻结效果</summary>
        public static ElementAffinity Freeze => ElementAffinity.Single(ManaType.Blue);

        /// <summary>复制效果</summary>
        public static ElementAffinity Copy => ElementAffinity.Single(ManaType.Blue);

        /// <summary>重定向效果</summary>
        public static ElementAffinity Redirect => ElementAffinity.Single(ManaType.Blue);

        /// <summary>偷取控制权效果</summary>
        public static ElementAffinity StealControl => ElementAffinity.Single(ManaType.Blue);

        #endregion

        #region 绿色 - 成长与恢复

        /// <summary>法力加速效果</summary>
        public static ElementAffinity Ramp => ElementAffinity.Single(ManaType.Green);

        /// <summary>增益效果</summary>
        public static ElementAffinity Buff => ElementAffinity.Single(ManaType.Green);

        /// <summary>召唤效果</summary>
        public static ElementAffinity Summon => ElementAffinity.Single(ManaType.Green);

        /// <summary>治疗效果</summary>
        public static ElementAffinity Heal => ElementAffinity.Single(ManaType.Green);

        /// <summary>重置效果</summary>
        public static ElementAffinity Untap => ElementAffinity.Single(ManaType.Green);

        /// <summary>践踏效果</summary>
        public static ElementAffinity Trample => ElementAffinity.Single(ManaType.Green);

        /// <summary>指示物效果</summary>
        public static ElementAffinity Counters => ElementAffinity.Single(ManaType.Green);

        #endregion

        #region 灰色 - 通用效果

        /// <summary>通用效果（可用任意颜色支付）</summary>
        public static ElementAffinity Generic => ElementAffinity.Generic;

        /// <summary>检索效果</summary>
        public static ElementAffinity Search => ElementAffinity.Generic;

        /// <summary>洗牌效果</summary>
        public static ElementAffinity Shuffle => ElementAffinity.Generic;

        /// <summary>流放效果</summary>
        public static ElementAffinity Exile => ElementAffinity.Generic;

        /// <summary>移动卡牌效果</summary>
        public static ElementAffinity MoveCard => ElementAffinity.Generic;

        #endregion

        /// <summary>
        /// 根据原子效果类型获取默认元素倾向
        /// </summary>
        public static ElementAffinity GetAffinityForEffect(AtomicEffectType effectType)
        {
            return effectType switch
            {
                // 红色效果
                AtomicEffectType.DealDamage => Damage,
                AtomicEffectType.DealCombatDamage => Damage,
                AtomicEffectType.AoEDamage => Damage,
                AtomicEffectType.SplitDamage => Damage,
                AtomicEffectType.TrampleDamage => Damage,
                AtomicEffectType.DamageCannotBePrevented => Damage,
                AtomicEffectType.GrantHaste => Haste,
                AtomicEffectType.GrantRush => Rush,
                AtomicEffectType.GrantDoubleStrike => DoubleStrike,
                AtomicEffectType.GrantMultiAttack => MultiAttack,
                AtomicEffectType.DestroyArtifact => Destruction,
                AtomicEffectType.DestroyRandom => Destruction,

                // 蓝色效果
                AtomicEffectType.DrawCard => Draw,
                AtomicEffectType.DrawThenDiscard => Draw,
                AtomicEffectType.ScryCards => Draw,
                AtomicEffectType.ReturnToHand => Bounce,
                AtomicEffectType.BounceToTop => Bounce,
                AtomicEffectType.BounceToBottom => Bounce,
                AtomicEffectType.CounterSpell => Counter,
                AtomicEffectType.CounterTargetSpell => Counter,
                AtomicEffectType.NegateActivation => Counter,
                AtomicEffectType.RedirectTarget => Redirect,
                AtomicEffectType.Tap => Tap,
                AtomicEffectType.FreezePermanent => Freeze,
                AtomicEffectType.CopyCard => Copy,
                AtomicEffectType.CopyExact => Copy,
                AtomicEffectType.StealControl => StealControl,
                AtomicEffectType.SwapController => StealControl,

                // 绿色效果
                AtomicEffectType.RampMana => Ramp,
                AtomicEffectType.SearchLand => Ramp,
                AtomicEffectType.UntapAll => Untap,
                AtomicEffectType.ModifyPower => Buff,
                AtomicEffectType.ModifyLife => Buff,
                AtomicEffectType.AddKeyword => Buff,
                AtomicEffectType.PutToBattlefield => Summon,
                AtomicEffectType.CreateToken => Summon,
                AtomicEffectType.Heal => Heal,
                AtomicEffectType.RestoreToFullLife => Heal,
                AtomicEffectType.RemoveDebuffs => Heal,
                AtomicEffectType.AddCounters => Counters,
                AtomicEffectType.DoubleCounters => Counters,
                AtomicEffectType.FightTarget => Buff,
                AtomicEffectType.GrantTrample => Trample,
                AtomicEffectType.GrantReach => Buff,

                // 灰色效果
                AtomicEffectType.SearchDeck => Search,
                AtomicEffectType.SearchAndReveal => Search,
                AtomicEffectType.SearchAndPlay => Search,
                AtomicEffectType.ShuffleIntoDeck => Shuffle,
                AtomicEffectType.Exile => Exile,
                AtomicEffectType.MoveToAnyZone => MoveCard,
                AtomicEffectType.ExchangePosition => MoveCard,
                AtomicEffectType.TransformInto => Generic,
                AtomicEffectType.MoveCard => MoveCard,

                // 默认灰色
                _ => Generic
            };
        }
    }
}
