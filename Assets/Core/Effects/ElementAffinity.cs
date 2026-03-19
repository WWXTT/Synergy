using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using CardCore.Data;

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
        public bool IsGeneric => PrimaryColor == ManaType.灰色;

        /// <summary>是否为特殊颜色（黑/白不在本次实现）</summary>
        public bool IsSpecialColor => PrimaryColor == ManaType.黑色 || PrimaryColor == ManaType.白色;

        /// <summary>创建单色倾向</summary>
        public static ElementAffinity Single(ManaType color) => new ElementAffinity { PrimaryColor = color };

        /// <summary>创建通用倾向（灰色，可用任意颜色支付）</summary>
        public static ElementAffinity Generic => new ElementAffinity { PrimaryColor = ManaType.灰色 };

        /// <summary>
        /// 获取颜色显示名称
        /// </summary>
        public string GetColorName()
        {
            return PrimaryColor switch
            {
                ManaType.红色 => "红",
                ManaType.蓝色 => "蓝",
                ManaType.绿色 => "绿",
                ManaType.灰色 => "通用",
                ManaType.白色 => "白",
                ManaType.黑色 => "黑",
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
                if (availableMana.TryGetValue(ManaType.灰色, out int grayCount) && grayCount > 0)
                {
                    int toUse = Math.Min(grayCount, remaining);
                    availableMana[ManaType.灰色] -= toUse;
                    remaining -= toUse;
                }

                // 然后按顺序使用其他颜色
                var colors = new[] { ManaType.红色, ManaType.蓝色, ManaType.绿色 };
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
                if (availableMana.TryGetValue(ManaType.灰色, out int grayCount) && grayCount > 0)
                {
                    int toUse = Math.Min(grayCount, remaining);
                    plan[ManaType.灰色] = toUse;
                    remaining -= toUse;
                }

                // 然后按顺序使用其他颜色
                var colors = new[] { ManaType.红色, ManaType.蓝色, ManaType.绿色 };
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
        public static ElementAffinity Damage => ElementAffinity.Single(ManaType.红色);

        /// <summary>燃烧效果</summary>
        public static ElementAffinity Burn => ElementAffinity.Single(ManaType.红色);

        /// <summary>破坏效果</summary>
        public static ElementAffinity Destruction => ElementAffinity.Single(ManaType.红色);

        /// <summary>敏捷效果</summary>
        public static ElementAffinity Haste => ElementAffinity.Single(ManaType.红色);

        /// <summary>突袭效果</summary>
        public static ElementAffinity Rush => ElementAffinity.Single(ManaType.红色);

        /// <summary>双击效果</summary>
        public static ElementAffinity DoubleStrike => ElementAffinity.Single(ManaType.红色);

        /// <summary>多次攻击效果</summary>
        public static ElementAffinity MultiAttack => ElementAffinity.Single(ManaType.红色);

        #endregion

        #region 蓝色 - 控制与知识

        /// <summary>抽卡效果</summary>
        public static ElementAffinity Draw => ElementAffinity.Single(ManaType.蓝色);

        /// <summary>弹回手牌效果</summary>
        public static ElementAffinity Bounce => ElementAffinity.Single(ManaType.蓝色);

        /// <summary>反制效果</summary>
        public static ElementAffinity Counter => ElementAffinity.Single(ManaType.蓝色);

        /// <summary>横置效果</summary>
        public static ElementAffinity Tap => ElementAffinity.Single(ManaType.蓝色);

        /// <summary>冻结效果</summary>
        public static ElementAffinity Freeze => ElementAffinity.Single(ManaType.蓝色);

        /// <summary>复制效果</summary>
        public static ElementAffinity Copy => ElementAffinity.Single(ManaType.蓝色);

        /// <summary>重定向效果</summary>
        public static ElementAffinity Redirect => ElementAffinity.Single(ManaType.蓝色);

        /// <summary>偷取控制权效果</summary>
        public static ElementAffinity StealControl => ElementAffinity.Single(ManaType.蓝色);

        #endregion

        #region 绿色 - 成长与恢复

        /// <summary>法力加速效果</summary>
        public static ElementAffinity Ramp => ElementAffinity.Single(ManaType.绿色);

        /// <summary>增益效果</summary>
        public static ElementAffinity Buff => ElementAffinity.Single(ManaType.绿色);

        /// <summary>召唤效果</summary>
        public static ElementAffinity Summon => ElementAffinity.Single(ManaType.绿色);

        /// <summary>治疗效果</summary>
        public static ElementAffinity Heal => ElementAffinity.Single(ManaType.绿色);

        /// <summary>重置效果</summary>
        public static ElementAffinity Untap => ElementAffinity.Single(ManaType.绿色);

        /// <summary>践踏效果</summary>
        public static ElementAffinity Trample => ElementAffinity.Single(ManaType.绿色);

        /// <summary>指示物效果</summary>
        public static ElementAffinity Counters => ElementAffinity.Single(ManaType.绿色);

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
