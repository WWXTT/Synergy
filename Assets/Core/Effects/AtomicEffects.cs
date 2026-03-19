using System;
using System.Collections.Generic;
using System.Linq;
using cfg;

namespace CardCore
{
    #region 效果分类扩展方法

    /// <summary>
    /// 效果类型扩展方法
    /// </summary>
    public static class AtomicEffectTypeExtensions
    {
        /// <summary>
        /// 获取效果显示名称
        /// </summary>
        public static string GetDisplayName(this AtomicEffectType effectType)
        {
            var classification = EffectClassificationConfig.GetClassification(effectType);
            return classification.Description;
        }

        /// <summary>
        /// 获取效果的完整描述（带颜色和功能信息）
        /// </summary>
        public static string GetFullDescription(this AtomicEffectType effectType)
        {
            var classification = EffectClassificationConfig.GetClassification(effectType);
            var colorName = EffectClassificationConfig.GetColorDisplayName(classification.Color);
            var functionName = EffectClassificationConfig.GetFunctionDisplayName(classification.Function);
            return $"{classification.Description} [{colorName} / {functionName}]";
        }

        /// <summary>
        /// 获取效果的颜色分类
        /// </summary>
        public static EffectColor GetColor(this AtomicEffectType effectType)
        {
            return EffectClassificationConfig.GetColor(effectType);
        }

        /// <summary>
        /// 获取效果的功能分类
        /// </summary>
        public static EffectFunction GetFunction(this AtomicEffectType effectType)
        {
            return EffectClassificationConfig.GetFunction(effectType);
        }

        /// <summary>
        /// 获取效果描述模板
        /// </summary>
        public static string GetEffectDescription(AtomicEffectType effectType, int value = 0)
        {
            string template = effectType switch
            {
                // 伤害与治疗
                AtomicEffectType.造成战斗伤害 => "对目标造成 {value} 点战斗伤害",
                AtomicEffectType.回复生命 => "为目标回复 {value} 点生命",
                AtomicEffectType.生命流失 => "目标失去 {value} 点生命",
                AtomicEffectType.范围伤害 => "对所有目标造成 {value} 点伤害",
                AtomicEffectType.分配伤害 => "分配 {value} 点伤害",
                AtomicEffectType.溢出伤害 => "溢出伤害",
                AtomicEffectType.不可防止伤害 => "造成 {value} 点不可防止的伤害",

                // 卡牌移动
                AtomicEffectType.抽卡 => "抽 {value} 张卡",
                AtomicEffectType.弃牌 => "弃掉 {value} 张牌",
                AtomicEffectType.磨牌 => "磨掉 {value} 张牌",
                AtomicEffectType.返回手牌 => "将目标返回手牌",
                AtomicEffectType.进入战场 => "将目标放入战场",
                AtomicEffectType.销毁 => "销毁目标",
                AtomicEffectType.除外 => "将目标除外",
                AtomicEffectType.洗入牌库 => "将目标洗入牌库",
                AtomicEffectType.检索牌库 => "从牌库检索 {value} 张牌",

                // 状态变更
                AtomicEffectType.横置 => "横置目标",
                AtomicEffectType.重置 => "重置目标",
                AtomicEffectType.修改攻击力 => "目标攻击力 +{value}",
                AtomicEffectType.修改生命值 => "目标生命值 +{value}",
                AtomicEffectType.设置攻击力 => "将目标攻击力设为 {value}",
                AtomicEffectType.设置生命值 => "将目标生命值设为 {value}",
                AtomicEffectType.交换属性 => "交换目标的攻击力和生命值",
                AtomicEffectType.添加指示物 => "在目标上放置 {value} 个指示物",
                AtomicEffectType.翻倍指示物 => "翻倍目标上的指示物",

                // 资源相关
                AtomicEffectType.添加法力 => "添加 {value} 点法力",
                AtomicEffectType.消耗法力 => "消耗 {value} 点法力",
                AtomicEffectType.法力加速 => "从牌库搜索地牌放入战场",
                AtomicEffectType.搜索地牌 => "搜索 {value} 张地牌",
                AtomicEffectType.全部重置 => "重置所有己方单位",

                // 控制相关
                AtomicEffectType.获得控制权 => "获得目标控制权",
                AtomicEffectType.偷取控制权 => "偷取目标控制权",
                AtomicEffectType.交换控制者 => "交换两个目标的控制者",
                AtomicEffectType.防止伤害 => "防止 {value} 点伤害",
                AtomicEffectType.反制法术 => "反制目标法术",
                AtomicEffectType.反制目标法术 => "反制目标法术",
                AtomicEffectType.无效化发动 => "无效化目标的发动",
                AtomicEffectType.无效效果 => "无效化效果",
                AtomicEffectType.重定向目标 => "将目标效果重定向到新目标",
                AtomicEffectType.无效化卡牌 => "无效化卡牌",
                AtomicEffectType.抽后弃 => "抽 {value} 张牌后弃掉��同数量",
                AtomicEffectType.预见 => "预见 {value} 张牌",

                // 保护相关
                AtomicEffectType.赋予敏捷 => "目标获得敏捷",
                AtomicEffectType.赋予突袭 => "目标获得突袭",
                AtomicEffectType.赋予双击 => "目标获得双击",
                AtomicEffectType.赋予多次攻击 => "目标获得 +{value} 次攻击",
                AtomicEffectType.赋予践踏 => "目标获得践踏",
                AtomicEffectType.赋予阻断飞行 => "目标获得阻断飞行",
                AtomicEffectType.赋予不可被指定 => "目标获得不可被指定",
                AtomicEffectType.赋予法术护盾 => "目标获得法术护盾",
                AtomicEffectType.赋予免疫 => "目标获得免疫",
                AtomicEffectType.赋予不受影响 => "目标获得不受影响",
                AtomicEffectType.恢复满生命 => "恢复目标满生命",
                AtomicEffectType.移除减益 => "移除目标的所有减益",

                // 破坏效果
                AtomicEffectType.破坏神器 => "破坏目标神器",
                AtomicEffectType.随机破坏 => "随机破坏一个敌方单位",

                // 特殊效果
                AtomicEffectType.创建衍生物 => "创建一个衍生物",
                AtomicEffectType.复制卡牌 => "复制目标卡牌",
                AtomicEffectType.精确复制 => "精确复制目标卡牌",
                AtomicEffectType.转化卡牌 => "转化目标卡牌",
                AtomicEffectType.转化为指定卡牌 => "将目标转化为指定卡牌",
                AtomicEffectType.进化生物 => "使目标生物进化",
                AtomicEffectType.与目标战斗 => "使目标与敌方单位战斗",

                // 反规则效果
                AtomicEffectType.修改游戏规则 => "修改游戏规则",
                AtomicEffectType.覆盖限制 => "覆盖限制",

                _ => effectType.ToString()
            };

            // 替换占位符
            if (value > 0)
                template = template.Replace("{value}", value.ToString());
            else
                template = template.Replace("{value}", "X");

            return template;
        }
    }

    #endregion

    #region 原子效果基类

    /// <summary>
    /// 原子效果基类
    /// </summary>
    [Serializable]
    public abstract class AtomicEffectBase : IAtomicEffect
    {
        /// <summary>效果类型</summary>
        public abstract AtomicEffectType EffectType { get; }

        /// <summary>效果数值</summary>
        public int Value;

        /// <summary>覆盖目标（可选）</summary>
        public TargetSelector OverrideTarget { get; set; }

        /// <summary>效果修正器</summary>
        public List<EffectModifier> Modifiers { get; set; } = new List<EffectModifier>();

        /// <summary>
        /// 执行效果
        /// </summary>
        public abstract void Execute(EffectExecutionContext context);

        /// <summary>
        /// 获取效果描述
        /// </summary>
        public virtual string GetDescription()
        {
            return AtomicEffectTypeExtensions.GetEffectDescription(EffectType, Value);
        }
    }

    /// <summary>
    /// 效果修正器
    /// </summary>
    [Serializable]
    public class EffectModifier
    {
        public int Apply(int baseValue) => baseValue;
    }

    /// <summary>
    /// 效果执行上下文
    /// </summary>
    public class EffectExecutionContext
    {
        /// <summary>效果来源实体</summary>
        public Entity Source { get; set; }

        /// <summary>效果控制者</summary>
        public Player Controller { get; set; }

        /// <summary>目标列表</summary>
        public List<Entity> Targets { get; set; } = new List<Entity>();

        /// <summary>主要目标</summary>
        public Entity PrimaryTarget => Targets.Count > 0 ? Targets[0] : null;

        /// <summary>目标上下文</summary>
        public TargetContext TargetContext { get; set; } = new TargetContext();

        /// <summary>触发事件</summary>
        public IGameEvent TriggeringEvent { get; set; }

        /// <summary>区域管理器</summary>
        public ZoneManager ZoneManager { get; set; }

        /// <summary>元素池系统</summary>
        public ElementPoolSystem ElementPool { get; set; }

        /// <summary>
        /// 获取效果数值修正后的值
        /// </summary>
        public int GetValueAfterModifiers(int baseValue)
        {
            return baseValue;
        }
    }

    #endregion
}