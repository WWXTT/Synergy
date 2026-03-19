using System;
using System.Collections.Generic;
using cfg;
using CardCore.Data;

namespace CardCore
{
    #region 效果分类定义

    /// <summary>
    /// 效果颜色（元素颜色倾向）
    /// </summary>
    public enum EffectColor
    {
        /// <summary>红色 - 伤害与破坏、进攻性</summary>
        Red,
        /// <summary>蓝色 - 控制与知识、干扰性</summary>
        Blue,
        /// <summary>绿色 - 成长与恢复、资源性</summary>
        Green,
        /// <summary>白色 - 保护与支援、防御性</summary>
        White,
        /// <summary>黑色 - 牺牲与弃牌、代价性</summary>
        Black,
        /// <summary>灰色 - 通用效果、无倾向</summary>
        Gray
    }

    /// <summary>
    /// 效果功能分类
    /// </summary>
    public enum EffectFunction
    {
        /// <summary>伤害 - 造成生命值损失</summary>
        Damage,
        /// <summary>治疗 - 恢复生命值</summary>
        Heal,
        /// <summary>移动 - 卡牌区域转移</summary>
        Movement,
        /// <summary>状态 - 修改卡牌属性/状态</summary>
        Status,
        /// <summary>控制 - 控制对手/干扰</summary>
        Control,
        /// <summary>资源 - 产生资源/加速</summary>
        Resource,
        /// <summary>保护 - 防止/免疫/护盾</summary>
        Protection,
        /// <summary>特殊 - 特殊规则/转化</summary>
        Special
    }

    /// <summary>
    /// 效果分类信息（颜色 + 功能）
    /// </summary>
    [Serializable]
    public struct EffectClassification
    {
        public EffectColor Color;
        public EffectFunction Function;
        public string Description;

        public EffectClassification(EffectColor color, EffectFunction function, string description = "")
        {
            Color = color;
            Function = function;
            Description = description;
        }
    }

    #endregion

    #region 效果分类配置字典

    /// <summary>
    /// 效果类型分类配置
    /// 以 AtomicEffectType 为键，值为颜色分类和功能分类
    /// </summary>
    public static class EffectClassificationConfig
    {
        /// <summary>
        /// 效果分类字典
        /// </summary>
        public static readonly Dictionary<AtomicEffectType, EffectClassification> Classifications = new()
        {
            // ============ 伤害与治疗 ============
            { AtomicEffectType.DealDamage, new EffectClassification(EffectColor.Red, EffectFunction.Damage, "造成伤害") },
            { AtomicEffectType.DealCombatDamage, new EffectClassification(EffectColor.Red, EffectFunction.Damage, "战斗伤害") },
            { AtomicEffectType.LifeLoss, new EffectClassification(EffectColor.Black, EffectFunction.Damage, "生命流失") },
            { AtomicEffectType.Heal, new EffectClassification(EffectColor.Green, EffectFunction.Heal, "回复生命") },
            { AtomicEffectType.AoEDamage, new EffectClassification(EffectColor.Red, EffectFunction.Damage, "范围伤害") },
            { AtomicEffectType.SplitDamage, new EffectClassification(EffectColor.Red, EffectFunction.Damage, "分配伤害") },
            { AtomicEffectType.TrampleDamage, new EffectClassification(EffectColor.Red, EffectFunction.Damage, "溢出伤害") },
            { AtomicEffectType.DamageCannotBePrevented, new EffectClassification(EffectColor.Red, EffectFunction.Damage, "不可防止伤害") },

            // ============ 卡牌移动 ============
            { AtomicEffectType.DrawCard, new EffectClassification(EffectColor.Blue, EffectFunction.Movement, "抽卡") },
            { AtomicEffectType.DiscardCard, new EffectClassification(EffectColor.Black, EffectFunction.Movement, "弃牌") },
            { AtomicEffectType.MillCard, new EffectClassification(EffectColor.Black, EffectFunction.Movement, "磨牌") },
            { AtomicEffectType.ReturnToHand, new EffectClassification(EffectColor.Blue, EffectFunction.Movement, "返回手牌") },
            { AtomicEffectType.PutToBattlefield, new EffectClassification(EffectColor.Green, EffectFunction.Movement, "进入战场") },
            { AtomicEffectType.Destroy, new EffectClassification(EffectColor.Red, EffectFunction.Movement, "销毁") },
            { AtomicEffectType.Exile, new EffectClassification(EffectColor.Gray, EffectFunction.Movement, "除外") },
            { AtomicEffectType.ShuffleIntoDeck, new EffectClassification(EffectColor.Gray, EffectFunction.Movement, "洗入牌库") },
            { AtomicEffectType.SearchDeck, new EffectClassification(EffectColor.Green, EffectFunction.Movement, "检索牌库") },
            { AtomicEffectType.BounceToTop, new EffectClassification(EffectColor.Blue, EffectFunction.Movement, "弹回牌库顶") },
            { AtomicEffectType.BounceToBottom, new EffectClassification(EffectColor.Blue, EffectFunction.Movement, "弹回牌库底") },
            { AtomicEffectType.MoveToAnyZone, new EffectClassification(EffectColor.Gray, EffectFunction.Movement, "移动到任意区域") },
            { AtomicEffectType.ExchangePosition, new EffectClassification(EffectColor.Gray, EffectFunction.Movement, "交换位置") },
            { AtomicEffectType.SearchAndReveal, new EffectClassification(EffectColor.Blue, EffectFunction.Movement, "检索并展示") },
            { AtomicEffectType.SearchAndPlay, new EffectClassification(EffectColor.Green, EffectFunction.Movement, "检索并使用") },
            { AtomicEffectType.MoveCard, new EffectClassification(EffectColor.Gray, EffectFunction.Movement, "移动卡牌") },

            // ============ 状态变更 ============
            { AtomicEffectType.Tap, new EffectClassification(EffectColor.Blue, EffectFunction.Status, "横置") },
            { AtomicEffectType.Untap, new EffectClassification(EffectColor.Green, EffectFunction.Status, "重置") },
            { AtomicEffectType.ModifyPower, new EffectClassification(EffectColor.Green, EffectFunction.Status, "修改攻击力") },
            { AtomicEffectType.ModifyLife, new EffectClassification(EffectColor.Green, EffectFunction.Status, "修改生命值") },
            { AtomicEffectType.SetPower, new EffectClassification(EffectColor.Green, EffectFunction.Status, "设置攻击力") },
            { AtomicEffectType.SetLife, new EffectClassification(EffectColor.Green, EffectFunction.Status, "设置生命值") },
            { AtomicEffectType.SwapStats, new EffectClassification(EffectColor.Blue, EffectFunction.Status, "交换属性") },
            { AtomicEffectType.AddCounters, new EffectClassification(EffectColor.Green, EffectFunction.Status, "添加指示物") },
            { AtomicEffectType.DoubleCounters, new EffectClassification(EffectColor.Green, EffectFunction.Status, "翻倍指示物") },
            { AtomicEffectType.AddCardType, new EffectClassification(EffectColor.Gray, EffectFunction.Status, "添加卡牌类型") },
            { AtomicEffectType.RemoveCardType, new EffectClassification(EffectColor.Gray, EffectFunction.Status, "移除卡牌类型") },
            { AtomicEffectType.AddKeyword, new EffectClassification(EffectColor.Green, EffectFunction.Status, "添加关键词") },
            { AtomicEffectType.RemoveKeyword, new EffectClassification(EffectColor.Gray, EffectFunction.Status, "移除关键词") },
            { AtomicEffectType.FreezePermanent, new EffectClassification(EffectColor.Blue, EffectFunction.Status, "冻结永久物") },

            // ============ 资源相关 ============
            { AtomicEffectType.AddMana, new EffectClassification(EffectColor.Green, EffectFunction.Resource, "添加法力") },
            { AtomicEffectType.ConsumeMana, new EffectClassification(EffectColor.Gray, EffectFunction.Resource, "消耗法力") },
            { AtomicEffectType.RampMana, new EffectClassification(EffectColor.Green, EffectFunction.Resource, "法力加速") },
            { AtomicEffectType.SearchLand, new EffectClassification(EffectColor.Green, EffectFunction.Resource, "搜索地牌") },
            { AtomicEffectType.UntapAll, new EffectClassification(EffectColor.Green, EffectFunction.Resource, "全部重置") },

            // ============ 控制相关 ============
            { AtomicEffectType.GainControl, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "获得控制权") },
            { AtomicEffectType.StealControl, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "偷取控制权") },
            { AtomicEffectType.SwapController, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "交换控制者") },
            { AtomicEffectType.PreventDamage, new EffectClassification(EffectColor.White, EffectFunction.Control, "防止伤害") },
            { AtomicEffectType.CounterSpell, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "反制法术") },
            { AtomicEffectType.CounterTargetSpell, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "反制目标法术") },
            { AtomicEffectType.NegateActivation, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "无效化发动") },
            { AtomicEffectType.NegateEffect, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "无效效果") },
            { AtomicEffectType.RedirectTarget, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "重定向目标") },
            { AtomicEffectType.Nullify, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "无效化卡牌") },
            { AtomicEffectType.DrawThenDiscard, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "抽后弃") },
            { AtomicEffectType.ScryCards, new EffectClassification(EffectColor.Blue, EffectFunction.Control, "预见") },

            // ============ 保护相关 ============
            { AtomicEffectType.GrantHaste, new EffectClassification(EffectColor.Red, EffectFunction.Protection, "赋予敏捷") },
            { AtomicEffectType.GrantRush, new EffectClassification(EffectColor.Red, EffectFunction.Protection, "赋予突袭") },
            { AtomicEffectType.GrantDoubleStrike, new EffectClassification(EffectColor.Red, EffectFunction.Protection, "赋予双击") },
            { AtomicEffectType.GrantMultiAttack, new EffectClassification(EffectColor.Red, EffectFunction.Protection, "赋予多次攻击") },
            { AtomicEffectType.GrantTrample, new EffectClassification(EffectColor.Green, EffectFunction.Protection, "赋予践踏") },
            { AtomicEffectType.GrantReach, new EffectClassification(EffectColor.Green, EffectFunction.Protection, "赋予阻断飞行") },
            { AtomicEffectType.GrantCannotBeTargeted, new EffectClassification(EffectColor.White, EffectFunction.Protection, "赋予不可被指定") },
            { AtomicEffectType.GrantSpellShield, new EffectClassification(EffectColor.White, EffectFunction.Protection, "赋予法术护盾") },
            { AtomicEffectType.GrantImmunity, new EffectClassification(EffectColor.White, EffectFunction.Protection, "赋予免疫") },
            { AtomicEffectType.GrantUnaffected, new EffectClassification(EffectColor.White, EffectFunction.Protection, "赋予不受影响") },
            { AtomicEffectType.RestoreToFullLife, new EffectClassification(EffectColor.Green, EffectFunction.Heal, "恢复满生命") },
            { AtomicEffectType.RemoveDebuffs, new EffectClassification(EffectColor.White, EffectFunction.Protection, "移除减益") },

            // ============ 破坏效果 ============
            { AtomicEffectType.DestroyArtifact, new EffectClassification(EffectColor.Red, EffectFunction.Movement, "破坏神器") },
            { AtomicEffectType.DestroyRandom, new EffectClassification(EffectColor.Red, EffectFunction.Movement, "随机破坏") },

            // ============ 特殊效果 ============
            { AtomicEffectType.CreateToken, new EffectClassification(EffectColor.Gray, EffectFunction.Special, "创建衍生物") },
            { AtomicEffectType.CopyCard, new EffectClassification(EffectColor.Blue, EffectFunction.Special, "复制卡牌") },
            { AtomicEffectType.CopyExact, new EffectClassification(EffectColor.Blue, EffectFunction.Special, "精确复制") },
            { AtomicEffectType.TransformCard, new EffectClassification(EffectColor.Gray, EffectFunction.Special, "转化卡牌") },
            { AtomicEffectType.TransformInto, new EffectClassification(EffectColor.Gray, EffectFunction.Special, "转化为指定卡牌") },
            { AtomicEffectType.EvolveCreature, new EffectClassification(EffectColor.Green, EffectFunction.Special, "进化生物") },
            { AtomicEffectType.FightTarget, new EffectClassification(EffectColor.Green, EffectFunction.Special, "与目标战斗") },

            // ============ 反规则效果 ============
            { AtomicEffectType.ModifyGameRule, new EffectClassification(EffectColor.Gray, EffectFunction.Special, "修改游戏规则") },
            { AtomicEffectType.OverrideRestriction, new EffectClassification(EffectColor.Gray, EffectFunction.Special, "覆盖限制") },
        };

        #region 查询方法

        /// <summary>
        /// 获取效果的颜色分类
        /// </summary>
        public static EffectColor GetColor(AtomicEffectType effectType)
        {
            return Classifications.TryGetValue(effectType, out var classification)
                ? classification.Color
                : EffectColor.Gray;
        }

        /// <summary>
        /// 获取效果的功能分类
        /// </summary>
        public static EffectFunction GetFunction(AtomicEffectType effectType)
        {
            return Classifications.TryGetValue(effectType, out var classification)
                ? classification.Function
                : EffectFunction.Special;
        }

        /// <summary>
        /// 获取效果的完整分类
        /// </summary>
        public static EffectClassification GetClassification(AtomicEffectType effectType)
        {
            return Classifications.TryGetValue(effectType, out var classification)
                ? classification
                : new EffectClassification(EffectColor.Gray, EffectFunction.Special, "未知效果");
        }

        /// <summary>
        /// 获取指定颜色的所有效果
        /// </summary>
        public static List<AtomicEffectType> GetEffectsByColor(EffectColor color)
        {
            var result = new List<AtomicEffectType>();
            foreach (var kvp in Classifications)
            {
                if (kvp.Value.Color == color)
                    result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// 获取指定功能的所有效果
        /// </summary>
        public static List<AtomicEffectType> GetEffectsByFunction(EffectFunction function)
        {
            var result = new List<AtomicEffectType>();
            foreach (var kvp in Classifications)
            {
                if (kvp.Value.Function == function)
                    result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// 获取颜色显示名称
        /// </summary>
        public static string GetColorDisplayName(EffectColor color)
        {
            return color switch
            {
                EffectColor.Red => "红色 - 伤害与破坏",
                EffectColor.Blue => "蓝色 - 控制与知识",
                EffectColor.Green => "绿色 - 成长与恢复",
                EffectColor.White => "白色 - 保护与支援",
                EffectColor.Black => "黑色 - 牺牲与弃牌",
                EffectColor.Gray => "灰色 - 通用效果",
                _ => color.ToString()
            };
        }

        /// <summary>
        /// 获取功能显示名称
        /// </summary>
        public static string GetFunctionDisplayName(EffectFunction function)
        {
            return function switch
            {
                EffectFunction.Damage => "伤害",
                EffectFunction.Heal => "治疗",
                EffectFunction.Movement => "移动",
                EffectFunction.Status => "状态",
                EffectFunction.Control => "控制",
                EffectFunction.Resource => "资源",
                EffectFunction.Protection => "保护",
                EffectFunction.Special => "特殊",
                _ => function.ToString()
            };
        }

        #endregion
    }

    #endregion

    #region 代价分类定义

    /// <summary>
    /// 代价类型（资源/行动/限制）
    /// </summary>
    public enum CostCategory
    {
        /// <summary>资源代价 - 消耗游戏资源（元素、卡牌等）</summary>
        Resource,
        /// <summary>行动代价 - 消耗行动能力（横置、攻击次数等）</summary>
        Action,
        /// <summary>限制代价 - 施加限制条件（时机、次数等）</summary>
        Restriction,
        /// <summary>预支代价 - 预支未来资源</summary>
        Prepay
    }

    /// <summary>
    /// 代价严重程度
    /// </summary>
    public enum CostSeverity
    {
        /// <summary>轻微 - 基本无影响</summary>
        Minor,
        /// <summary>中等 - 需要考虑</summary>
        Moderate,
        /// <summary>严重 - 重大代价</summary>
        Severe,
        /// <summary>极端 - 难以支付</summary>
        Extreme
    }

    /// <summary>
    /// 代价分类信息
    /// </summary>
    [Serializable]
    public struct CostClassification
    {
        public CostCategory Category;
        public CostSeverity Severity;
        public string Description;

        public CostClassification(CostCategory category, CostSeverity severity, string description = "")
        {
            Category = category;
            Severity = severity;
            Description = description;
        }
    }

    #endregion

    #region 代价分类配置字典

    /// <summary>
    /// 代价类型分类配置
    /// </summary>
    public static class CostClassificationConfig
    {
        /// <summary>
        /// 元素代价分类
        /// </summary>
        public static readonly Dictionary<ManaType, CostClassification> ElementCostClassifications = new()
        {
            { ManaType.灰色, new CostClassification(CostCategory.Resource, CostSeverity.Minor, "任意元素") },
            { ManaType.红色, new CostClassification(CostCategory.Resource, CostSeverity.Minor, "红色元素") },
            { ManaType.蓝色, new CostClassification(CostCategory.Resource, CostSeverity.Minor, "蓝色元素") },
            { ManaType.绿色, new CostClassification(CostCategory.Resource, CostSeverity.Minor, "绿色元素") },
            { ManaType.白色, new CostClassification(CostCategory.Resource, CostSeverity.Minor, "白色元素") },
            { ManaType.黑色, new CostClassification(CostCategory.Resource, CostSeverity.Minor, "黑色元素") },
        };

        /// <summary>
        /// 资源区域代价分类
        /// </summary>
        public static readonly Dictionary<ResourceZone, CostClassification> ResourceZoneClassifications = new()
        {
            { ResourceZone.Hand, new CostClassification(CostCategory.Resource, CostSeverity.Moderate, "手牌") },
            { ResourceZone.Battlefield, new CostClassification(CostCategory.Resource, CostSeverity.Severe, "场上单位") },
            { ResourceZone.Graveyard, new CostClassification(CostCategory.Resource, CostSeverity.Minor, "墓地") },
            { ResourceZone.ExtraDeck, new CostClassification(CostCategory.Resource, CostSeverity.Moderate, "额外卡组") },
        };

        /// <summary>
        /// 去向区域代价分类
        /// </summary>
        public static readonly Dictionary<DestinationZone, CostClassification> DestinationZoneClassifications = new()
        {
            { DestinationZone.Graveyard, new CostClassification(CostCategory.Resource, CostSeverity.Moderate, "送墓") },
            { DestinationZone.Exile, new CostClassification(CostCategory.Resource, CostSeverity.Severe, "除外") },
        };

        /// <summary>
        /// 预支类型代价分类
        /// </summary>
        public static readonly Dictionary<PrepayType, CostClassification> PrepayTypeClassifications = new()
        {
            { PrepayType.DrawCard, new CostClassification(CostCategory.Prepay, CostSeverity.Moderate, "预支抽卡") },
            { PrepayType.ElementProduction, new CostClassification(CostCategory.Prepay, CostSeverity.Minor, "预支元素产出") },
            { PrepayType.AttackChance, new CostClassification(CostCategory.Prepay, CostSeverity.Moderate, "预支攻击") },
            { PrepayType.NextTurnAction, new CostClassification(CostCategory.Prepay, CostSeverity.Severe, "预支下回合行动") },
        };

        /// <summary>
        /// 发动条件代价分类
        /// </summary>
        public static readonly Dictionary<ConditionType, CostClassification> ConditionCostClassifications = new()
        {
            { ConditionType.OncePerTurn, new CostClassification(CostCategory.Restriction, CostSeverity.Minor, "每回合一次") },
            { ConditionType.OnlyMainPhase, new CostClassification(CostCategory.Restriction, CostSeverity.Minor, "仅主要阶段") },
            { ConditionType.OnlyOwnTurn, new CostClassification(CostCategory.Restriction, CostSeverity.Minor, "仅自己回合") },
            { ConditionType.OnlyOpponentTurn, new CostClassification(CostCategory.Restriction, CostSeverity.Minor, "仅对手回合") },
            { ConditionType.FirstTimeThisGame, new CostClassification(CostCategory.Restriction, CostSeverity.Moderate, "本局首次") },
            { ConditionType.CardIsTapped, new CostClassification(CostCategory.Action, CostSeverity.Minor, "需要横置") },
        };

        #region 查询方法

        /// <summary>
        /// 获取资源区域代价的严重程度
        /// </summary>
        public static CostSeverity GetResourceZoneSeverity(ResourceZone zone)
        {
            return ResourceZoneClassifications.TryGetValue(zone, out var classification)
                ? classification.Severity
                : CostSeverity.Moderate;
        }

        /// <summary>
        /// 获取去向区域代价的严重程度
        /// </summary>
        public static CostSeverity GetDestinationZoneSeverity(DestinationZone zone)
        {
            return DestinationZoneClassifications.TryGetValue(zone, out var classification)
                ? classification.Severity
                : CostSeverity.Moderate;
        }

        /// <summary>
        /// 获取预支代价的严重程度
        /// </summary>
        public static CostSeverity GetPrepayTypeSeverity(PrepayType prepayType)
        {
            return PrepayTypeClassifications.TryGetValue(prepayType, out var classification)
                ? classification.Severity
                : CostSeverity.Moderate;
        }

        /// <summary>
        /// 获取代价类型显示名称
        /// </summary>
        public static string GetCategoryDisplayName(CostCategory category)
        {
            return category switch
            {
                CostCategory.Resource => "资源代价",
                CostCategory.Action => "行动代价",
                CostCategory.Restriction => "限制代价",
                CostCategory.Prepay => "预支代价",
                _ => category.ToString()
            };
        }

        /// <summary>
        /// 获取严重程度显示名称
        /// </summary>
        public static string GetSeverityDisplayName(CostSeverity severity)
        {
            return severity switch
            {
                CostSeverity.Minor => "轻微",
                CostSeverity.Moderate => "中等",
                CostSeverity.Severe => "严重",
                CostSeverity.Extreme => "极端",
                _ => severity.ToString()
            };
        }

        #endregion
    }

    #endregion
}
