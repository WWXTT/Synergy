using System;
using System.Collections.Generic;
using UnityEngine;
using cfg;

namespace CardCore.Data
{
    /// <summary>
    /// 临时效果数据 ScriptableObject
    /// 用于存储效果类型、条件类型等枚举的显示数据
    /// </summary>
    [CreateAssetMenu(fileName = "TempEffectData", menuName = "Synergy/Temp Data/Effect Data")]
    public class TempEffectDataSO : ScriptableObject
    {
        [Header("原子效果类型数据")]
        public List<AtomicEffectDisplay> atomicEffectDisplays = new List<AtomicEffectDisplay>();

        [Header("条件类型数据")]
        public List<ConditionDisplay> conditionDisplays = new List<ConditionDisplay>();

        [Header("触发时点数据")]
        public List<TriggerTimingDisplay> triggerTimingDisplays = new List<TriggerTimingDisplay>();

        [Header("持续时间类型数据")]
        public List<DurationTypeDisplay> durationTypeDisplays = new List<DurationTypeDisplay>();

        [Header("目标类型数据")]
        public List<TargetTypeDisplay> targetTypeDisplays = new List<TargetTypeDisplay>();

        [Header("区域数据")]
        public List<ZoneDisplay> zoneDisplays = new List<ZoneDisplay>();

        private void OnEnable()
        {
            InitializeDefaultData();
        }

        private void InitializeDefaultData()
        {
            if (atomicEffectDisplays.Count == 0)
            {
                foreach (AtomicEffectType type in Enum.GetValues(typeof(AtomicEffectType)))
                {
                    atomicEffectDisplays.Add(new AtomicEffectDisplay
                    {
                        type = type,
                        displayName = type.ToString(),
                        description = GetAtomicEffectDescription(type),
                        category = GetAtomicEffectCategory(type)
                    });
                }
            }

            if (conditionDisplays.Count == 0)
            {
                foreach (ConditionType type in Enum.GetValues(typeof(ConditionType)))
                {
                    conditionDisplays.Add(new ConditionDisplay
                    {
                        type = type,
                        displayName = type.ToString(),
                        description = GetConditionDescription(type),
                        requiresParameter = RequiresParameter(type)
                    });
                }
            }

            if (triggerTimingDisplays.Count == 0)
            {
                foreach (TriggerTiming type in Enum.GetValues(typeof(TriggerTiming)))
                {
                    triggerTimingDisplays.Add(new TriggerTimingDisplay
                    {
                        type = type,
                        displayName = type.ToString(),
                        description = GetTriggerTimingDescription(type)
                    });
                }
            }

            if (durationTypeDisplays.Count == 0)
            {
                foreach (DurationType type in Enum.GetValues(typeof(DurationType)))
                {
                    durationTypeDisplays.Add(new DurationTypeDisplay
                    {
                        type = type,
                        displayName = type.ToString(),
                        description = GetDurationTypeDescription(type)
                    });
                }
            }

            if (targetTypeDisplays.Count == 0)
            {
                foreach (TargetType type in Enum.GetValues(typeof(TargetType)))
                {
                    targetTypeDisplays.Add(new TargetTypeDisplay
                    {
                        type = type,
                        displayName = type.ToString(),
                        description = GetTargetTypeDescription(type)
                    });
                }
            }

            if (zoneDisplays.Count == 0)
            {
                foreach (Zone type in Enum.GetValues(typeof(Zone)))
                {
                    zoneDisplays.Add(new ZoneDisplay
                    {
                        type = type,
                        displayName = type.ToString(),
                        description = GetZoneDescription(type)
                    });
                }
            }
        }

        private string GetAtomicEffectDescription(AtomicEffectType type)
        {
            return type switch
            {
                AtomicEffectType.造成战斗伤害 => "对目标造成战斗伤害",
                AtomicEffectType.回复生命 => "回复目标生命值",
                AtomicEffectType.生命流失 => "使目标失去生命（非伤害）",
                AtomicEffectType.范围伤害 => "对所有指定目标造成伤害",
                AtomicEffectType.抽卡 => "从牌库抽取卡牌",
                AtomicEffectType.弃牌 => "从手牌弃置卡牌",
                AtomicEffectType.返回手牌 => "将卡牌返回到手牌",
                AtomicEffectType.进入战场 => "将卡牌放置到战场",
                AtomicEffectType.销毁 => "消灭目标永久物",
                AtomicEffectType.除外 => "将卡牌放逐",
                AtomicEffectType.横置 => "横置目标永久物",
                AtomicEffectType.重置 => "重置目标永久物",
                AtomicEffectType.修改攻击力 => "修改生物攻击力",
                AtomicEffectType.修改生命值 => "修改生物生命值",
                AtomicEffectType.添加关键词 => "添加关键词能力",
                AtomicEffectType.获得控制权 => "获得目标永久物的控制权",
                AtomicEffectType.反制法术 => "反制目标法术",
                AtomicEffectType.创建衍生物 => "创建衍生物",
                AtomicEffectType.复制卡牌 => "复制目标卡牌",
                _ => type.ToString()
            };
        }

        private string GetAtomicEffectCategory(AtomicEffectType type)
        {
            return type switch
            {
                AtomicEffectType.造成战斗伤害 or AtomicEffectType.范围伤害 or AtomicEffectType.分配伤害
                    or AtomicEffectType.溢出伤害 or AtomicEffectType.不可防止伤害 => "伤害",
                AtomicEffectType.回复生命 or AtomicEffectType.生命流失 => "生命",
                AtomicEffectType.抽卡 or AtomicEffectType.弃牌 or AtomicEffectType.磨牌
                    or AtomicEffectType.返回手牌 or AtomicEffectType.进入战场 or AtomicEffectType.销毁
                    or AtomicEffectType.除外 or AtomicEffectType.洗入牌库 or AtomicEffectType.检索牌库 => "移动",
                AtomicEffectType.横置 or AtomicEffectType.重置 => "状态",
                AtomicEffectType.修改攻击力 or AtomicEffectType.修改生命值 or AtomicEffectType.设置攻击力
                    or AtomicEffectType.设置生命值 or AtomicEffectType.交换属性 => "属性",
                AtomicEffectType.添加关键词 or AtomicEffectType.移除关键词 => "关键词",
                AtomicEffectType.获得控制权 or AtomicEffectType.偷取控制权 => "控制",
                AtomicEffectType.反制法术 or AtomicEffectType.反制目标法术 or AtomicEffectType.无效化发动 => "反制",
                _ => "其他"
            };
        }

        private string GetConditionDescription(ConditionType type)
        {
            return type switch
            {
                ConditionType.手牌数量上限 => "手牌数量不超过指定值",
                ConditionType.场上卡牌数量下限 => "场上卡牌数量不少于指定值",
                ConditionType.场上卡牌数量上限 => "场上卡牌数量不超过指定值",
                ConditionType.可用元素下限 => "可用元素数量不少于指定值",
                ConditionType.控制者生命值条件 => "控制者生命值满足条件",
                ConditionType.对手生命值条件 => "对手生命值满足条件",
                ConditionType.卡牌类型条件 => "卡牌类型符合条件",
                ConditionType.卡牌已横置 => "卡牌处于横置状态",
                ConditionType.卡牌未横置 => "卡牌处于未横置状态",
                ConditionType.每回合一次 => "每回合只能使用一次",
                ConditionType.仅主要阶段 => "只能在主要阶段使用",
                ConditionType.仅自己回合 => "只能在自己回合使用",
                ConditionType.仅对手回合 => "只能在对手回合使用",
                _ => type.ToString()
            };
        }

        private bool RequiresParameter(ConditionType type)
        {
            return type switch
            {
                ConditionType.手牌数量上限 or ConditionType.场上卡牌数量下限 or ConditionType.场上卡牌数量上限
                    or ConditionType.可用元素下限 or ConditionType.控制者生命值条件 or ConditionType.对手生命值条件
                    or ConditionType.卡牌攻击力条件 or ConditionType.卡牌生命值条件 => true,
                _ => false
            };
        }

        private string GetTriggerTimingDescription(TriggerTiming type)
        {
            return type switch
            {
                TriggerTiming.瞬间发动 => "即时发动，无需等待",
                TriggerTiming.响应式发动 => "响应其他效果发动",
                TriggerTiming.入场时 => "卡牌进入战场时触发",
                TriggerTiming.离场时 => "卡牌离开战场时触发",
                TriggerTiming.死亡时 => "生物被消灭时触发",
                TriggerTiming.回合开始 => "回合开始时触发",
                TriggerTiming.回合结束 => "回合结束时触发",
                TriggerTiming.攻击宣言时 => "宣告攻击时触发",
                TriggerTiming.阻拦宣言时 => "宣告阻挡时触发",
                TriggerTiming.造成伤害时 => "造成伤害时触发",
                TriggerTiming.受到伤害时 => "受到伤害时触发",
                _ => type.ToString()
            };
        }

        private string GetDurationTypeDescription(DurationType type)
        {
            return type switch
            {
                DurationType.一次性 => "效果立即结算",
                DurationType.永久 => "效果持续到游戏结束",
                DurationType.直到回合结束 => "效果持续到当前回合结束",
                DurationType.直到离开战场 => "效果持续到卡牌离开战场",
                DurationType.条件满足时 => "效果持续直到条件不满足",
                _ => type.ToString()
            };
        }

        private string GetTargetTypeDescription(TargetType type)
        {
            return type switch
            {
                TargetType.自己 => "选择自己",
                TargetType.对手 => "选择对手",
                TargetType.任意玩家 => "选择任意玩家",
                TargetType.指定卡牌 => "选择指定卡牌",
                TargetType.所有卡牌 => "选择所有卡牌",
                TargetType.随机卡牌 => "随机选择卡牌",
                TargetType.这张卡 => "选择这张卡牌本身",
                TargetType.手牌中的卡 => "选择手牌中的卡牌",
                TargetType.场上的卡 => "选择战场上的卡牌",
                TargetType.墓地中的卡 => "选择墓地中的卡牌",
                TargetType.牌库中的卡 => "选择牌库中的卡牌",
                _ => type.ToString()
            };
        }

        private string GetZoneDescription(Zone type)
        {
            return type switch
            {
                Zone.战场 => "战场区域",
                Zone.坟墓场 => "坟墓场区域",
                Zone.流放区 => "流放区（除外区）",
                Zone.牌库 => "牌库区域",
                Zone.元素池 => "元素池（法力池）",
                Zone.无 => "无区域",
                Zone.准备阶段 => "准备阶段区域",
                _ => type.ToString()
            };
        }
    }

    [Serializable]
    public class AtomicEffectDisplay
    {
        public AtomicEffectType type;
        public string displayName;
        public string description;
        public string category;
    }

    [Serializable]
    public class ConditionDisplay
    {
        public ConditionType type;
        public string displayName;
        public string description;
        public bool requiresParameter;
    }

    [Serializable]
    public class TriggerTimingDisplay
    {
        public TriggerTiming type;
        public string displayName;
        public string description;
    }

    [Serializable]
    public class DurationTypeDisplay
    {
        public DurationType type;
        public string displayName;
        public string description;
    }

    [Serializable]
    public class TargetTypeDisplay
    {
        public TargetType type;
        public string displayName;
        public string description;
    }

    [Serializable]
    public class ZoneDisplay
    {
        public Zone type;
        public string displayName;
        public string description;
    }
}