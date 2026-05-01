using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 关键词效果映射器
    /// 将关键词 ID 转换为可执行的运行时效果
    ///
    /// 关键词分两类：
    /// 1. 被动标记型（Passive）- 写入 IHasKeywords，由游戏系统查询
    /// 2. 触发效果型（Triggered）- 生成 EffectDefinition 注册到 TriggerEngine
    /// </summary>
    public static class KeywordEffectMapper
    {
        /// <summary>
        /// 关键词行为定义
        /// </summary>
        private class KeywordBehavior
        {
            public string Id;
            public string NameZh;
            public KeywordCategory Category;
            public AtomicEffectType? GrantEffect;
            public TriggerTiming? TriggerTiming;
            public Func<AtomicEffectInstance> EffectFactory;
            public string Description;
        }

        private static readonly Dictionary<string, KeywordBehavior> _behaviors;
        private static bool _initialized;

        static KeywordEffectMapper()
        {
            _behaviors = new Dictionary<string, KeywordBehavior>();
            Initialize();
        }

        private static void Initialize()
        {
            if (_initialized) return;

            // ============ 红色 - 攻击性 ============
            Add(new KeywordBehavior
            {
                Id = "Charge",
                NameZh = "冲锋",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantHaste,
                Description = "召唤当回合可以攻击"
            });
            Add(new KeywordBehavior
            {
                Id = "DoubleStrike",
                NameZh = "连击",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantDoubleStrike,
                Description = "战斗中造成两次伤害"
            });
            Add(new KeywordBehavior
            {
                Id = "Trample",
                NameZh = "穿透",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantTrample,
                Description = "溢出伤害转移给对方玩家"
            });
            Add(new KeywordBehavior
            {
                Id = "FirstStrike",
                NameZh = "先攻",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantFirstStrike,
                Description = "先于对方造成战斗伤害"
            });
            Add(new KeywordBehavior
            {
                Id = "Enrage",
                NameZh = "狂暴",
                Category = KeywordCategory.Triggered,
                GrantEffect = AtomicEffectType.GrantOverwhelm,
                TriggerTiming = TriggerTiming.On_DamageTaken,
                EffectFactory = () => new AtomicEffectInstance
                {
                    Type = AtomicEffectType.ModifyPower,
                    Value = 2,
                    Duration = DurationType.Permanent
                },
                Description = "受到伤害时攻击力+2"
            });

            // ============ 蓝色 - 规避/控制 ============
            Add(new KeywordBehavior
            {
                Id = "Flying",
                NameZh = "飞行",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantFlying,
                Description = "只能被飞行或阻断飞行的生物阻挡"
            });
            Add(new KeywordBehavior
            {
                Id = "Vigilance",
                NameZh = "警戒",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantVigilance,
                Description = "攻击时不横置"
            });
            Add(new KeywordBehavior
            {
                Id = "Stealth",
                NameZh = "潜行",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantStealth,
                Description = "直到攻击前不能被指定为目标"
            });
            Add(new KeywordBehavior
            {
                Id = "SpellShield",
                NameZh = "法术护盾",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantSpellShield,
                Description = "不能被法术或效果指定为目标"
            });
            Add(new KeywordBehavior
            {
                Id = "Guard",
                NameZh = "守卫",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantGuard,
                Description = "可代替相邻生物承受攻击"
            });

            // ============ 绿色 - 续航/成长 ============
            Add(new KeywordBehavior
            {
                Id = "Lifesteal",
                NameZh = "吸血",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantLifesteal,
                Description = "造成的伤害回复等量生命"
            });
            Add(new KeywordBehavior
            {
                Id = "Regeneration",
                NameZh = "再生",
                Category = KeywordCategory.Triggered,
                GrantEffect = AtomicEffectType.GrantRegeneration,
                TriggerTiming = TriggerTiming.On_TurnEnd,
                EffectFactory = () => new AtomicEffectInstance
                {
                    Type = AtomicEffectType.Heal,
                    Value = 999, // 恢复所有伤害
                    Duration = DurationType.Once
                },
                Description = "回合结束时恢复所有伤害"
            });
            Add(new KeywordBehavior
            {
                Id = "Growth",
                NameZh = "成长",
                Category = KeywordCategory.Triggered,
                GrantEffect = AtomicEffectType.GrantGrowth,
                TriggerTiming = TriggerTiming.On_TurnEnd,
                EffectFactory = () => new AtomicEffectInstance
                {
                    Type = AtomicEffectType.ModifyAllStats,
                    Value = 1,
                    Value2 = 1,
                    Duration = DurationType.Permanent
                },
                Description = "每回合结束获得+1/+1"
            });
            Add(new KeywordBehavior
            {
                Id = "Armor",
                NameZh = "坚韧",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantArmor,
                Description = "受到的伤害减少1点"
            });
            Add(new KeywordBehavior
            {
                Id = "DivineShield",
                NameZh = "圣盾",
                Category = KeywordCategory.Passive,
                GrantEffect = AtomicEffectType.GrantDivineShield,
                Description = "抵挡一次伤害后消失"
            });

            _initialized = true;
        }

        private static void Add(KeywordBehavior behavior)
        {
            _behaviors[behavior.Id] = behavior;
        }

        /// <summary>
        /// 获取关键词分类
        /// </summary>
        public static KeywordCategory GetCategory(string keywordId)
        {
            return _behaviors.TryGetValue(keywordId, out var b)
                ? b.Category
                : KeywordCategory.Passive;
        }

        /// <summary>
        /// 获取关键词对应的 Grant 原子效果类型
        /// </summary>
        public static AtomicEffectType? GetGrantEffectType(string keywordId)
        {
            return _behaviors.TryGetValue(keywordId, out var b)
                ? b.GrantEffect
                : null;
        }

        /// <summary>
        /// 为触发式关键词生成 EffectDefinition
        /// 返回 null 表示被动标记型，不需要 EffectDefinition
        /// </summary>
        public static EffectDefinition CreateTriggeredEffect(string keywordId, string sourceCardId)
        {
            if (!_behaviors.TryGetValue(keywordId, out var behavior))
                return null;

            if (behavior.Category != KeywordCategory.Triggered)
                return null;

            if (behavior.TriggerTiming == null || behavior.EffectFactory == null)
                return null;

            var effectDef = new EffectDefinition
            {
                Id = $"KW_{keywordId}_{sourceCardId}",
                DisplayName = behavior.NameZh,
                Description = behavior.Description,
                TriggerTiming = behavior.TriggerTiming.Value,
                ActivationType = EffectActivationType.Automatic,
                IsOptional = false,
                Duration = DurationType.Permanent,
                SourceCardId = sourceCardId,
                Effects = new List<AtomicEffectInstance>
                {
                    behavior.EffectFactory()
                }
            };

            return effectDef;
        }

        /// <summary>
        /// 批量为卡牌生成所有触发式关键词的 EffectDefinition
        /// </summary>
        public static List<EffectDefinition> CreateAllTriggeredEffects(Card card)
        {
            var results = new List<EffectDefinition>();

            if (!(card is IHasKeywords hasKw))
                return results;

            var cardId = card.ID;
            foreach (var keywordId in hasKw.Keywords)
            {
                var effectDef = CreateTriggeredEffect(keywordId, cardId);
                if (effectDef != null)
                    results.Add(effectDef);
            }

            return results;
        }

        /// <summary>
        /// 获取关键词中文名
        /// </summary>
        public static string GetNameZh(string keywordId)
        {
            return _behaviors.TryGetValue(keywordId, out var b) ? b.NameZh : keywordId;
        }

        /// <summary>
        /// 获取关键词描述
        /// </summary>
        public static string GetDescription(string keywordId)
        {
            return _behaviors.TryGetValue(keywordId, out var b) ? b.Description : "";
        }

        /// <summary>
        /// 是否为已知关键词
        /// </summary>
        public static bool IsKnownKeyword(string keywordId)
        {
            return _behaviors.ContainsKey(keywordId);
        }

        /// <summary>
        /// 获取所有已定义的关键词ID
        /// </summary>
        public static IEnumerable<string> GetAllKeywordIds()
        {
            return _behaviors.Keys;
        }
    }

    /// <summary>
    /// 关键词分类
    /// </summary>
    public enum KeywordCategory
    {
        /// <summary>被动标记型 - 由游戏系统查询</summary>
        Passive,
        /// <summary>触发效果型 - 需注册到 TriggerEngine</summary>
        Triggered
    }
}
