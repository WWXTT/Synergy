using System;
using System.Collections.Generic;

namespace CardCore.Attribute
{
    /// <summary>
    /// 原子效果属性表
    /// 静态数据表，包含所有原子效果的完整属性配置
    /// 对应 #Attribute.xlsm
    /// </summary>
    public static class AtomicEffectTable
    {
        private static Dictionary<int, AtomicEffectConfig> _idMap;
        private static Dictionary<string, AtomicEffectConfig> _enumNameMap;
        private static Dictionary<AtomicEffectType, AtomicEffectConfig> _typeMap;

        static AtomicEffectTable()
        {
            Initialize();
        }

        private static void Initialize()
        {
            _idMap = new Dictionary<int, AtomicEffectConfig>();
            _enumNameMap = new Dictionary<string, AtomicEffectConfig>();
            _typeMap = new Dictionary<AtomicEffectType, AtomicEffectConfig>();

            // --- 红色效果 ---
            AddConfig(new AtomicEffectConfig
            {
                Id = 1,
                EnumName = "DealDamage",
                DisplayName = "造成伤害",
                Description = "对目标造成 {Value} 点伤害",
                BaseCost = 1.0f,
                CostMultiplier = 1.0f,
                TargetType = EffectTargetType.Target,
                TargetFilter = "Creature,Player",
                TargetCount = 1,
                TargetScope = EffectTargetScope.Single,
                DurationType = EffectDurationType.Instant,
                ActivationType = EffectActivationType.Voluntary,
                Stackable = true,
                Priority = 50,
                Tags = "Damage,Red",
                AvailableTriggerTimings = "OnPlay,OnDealDamage,Activate_Instant",
                AvailableTargetScopes = "Single,AoE",
                BranchConfigs = "DealDamageBranch"
            });

            AddConfig(new AtomicEffectConfig
            {
                Id = 2,
                EnumName = "Destroy",
                DisplayName = "消灭",
                Description = "消灭目标生物",
                BaseCost = 3.0f,
                CostMultiplier = 1.5f,
                TargetType = EffectTargetType.Target,
                TargetFilter = "Creature",
                TargetCount = 1,
                TargetScope = EffectTargetScope.Single,
                DurationType = EffectDurationType.Instant,
                ActivationType = EffectActivationType.Voluntary,
                Stackable = false,
                Priority = 60,
                Tags = "Destruction,Red",
                AvailableTriggerTimings = "OnPlay,Activate_Instant",
                AvailableTargetScopes = "Single",
                BranchConfigs = ""
            });

            AddConfig(new AtomicEffectConfig
            {
                Id = 3,
                EnumName = "GrantHaste",
                DisplayName = "获得敏捷",
                Description = "目标获得敏捷（本回合可攻击）",
                BaseCost = 1.0f,
                CostMultiplier = 0.8f,
                TargetType = EffectTargetType.Target,
                TargetFilter = "Creature,Untapped",
                TargetCount = 1,
                TargetScope = EffectTargetScope.Single,
                DurationType = EffectDurationType.UntilEndOfTurn,
                ActivationType = EffectActivationType.Voluntary,
                Stackable = false,
                Priority = 30,
                Tags = "Keyword,Haste,Red",
                AvailableTriggerTimings = "OnPlay,Activate_Active,Activate_Instant",
                AvailableTargetScopes = "Single",
                BranchConfigs = ""
            });

            // --- 蓝色效果 ---
            AddConfig(new AtomicEffectConfig
            {
                Id = 4,
                EnumName = "DrawCard",
                DisplayName = "抽牌",
                Description = "抽 {Value} 张牌",
                BaseCost = 1.5f,
                CostMultiplier = 1.0f,
                TargetType = EffectTargetType.Self,
                TargetFilter = "",
                TargetCount = 0,
                TargetScope = EffectTargetScope.Single,
                DurationType = EffectDurationType.Instant,
                ActivationType = EffectActivationType.Voluntary,
                Stackable = true,
                Priority = 40,
                Tags = "Draw,Blue",
                AvailableTriggerTimings = "OnPlay,OnTurnStart,OnDraw,Activate_Instant",
                AvailableTargetScopes = "Single",
                BranchConfigs = ""
            });

            AddConfig(new AtomicEffectConfig
            {
                Id = 5,
                EnumName = "ReturnToHand",
                DisplayName = "弹回手牌",
                Description = "将目标移回拥有者手牌",
                BaseCost = 1.5f,
                CostMultiplier = 1.0f,
                TargetType = EffectTargetType.Target,
                TargetFilter = "Creature",
                TargetCount = 1,
                TargetScope = EffectTargetScope.Single,
                DurationType = EffectDurationType.Instant,
                ActivationType = EffectActivationType.Voluntary,
                Stackable = false,
                Priority = 45,
                Tags = "Bounce,Blue",
                AvailableTriggerTimings = "OnPlay,Activate_Instant,Activate_Response",
                AvailableTargetScopes = "Single",
                BranchConfigs = ""
            });

            AddConfig(new AtomicEffectConfig
            {
                Id = 6,
                EnumName = "FreezePermanent",
                DisplayName = "冻结",
                Description = "冻结目标（横置且下回合不能重置）",
                BaseCost = 2.0f,
                CostMultiplier = 1.0f,
                TargetType = EffectTargetType.Target,
                TargetFilter = "Creature,Untapped",
                TargetCount = 1,
                TargetScope = EffectTargetScope.Single,
                DurationType = EffectDurationType.UntilEndOfTurn,
                ActivationType = EffectActivationType.Voluntary,
                Stackable = false,
                Priority = 45,
                Tags = "Freeze,Blue",
                AvailableTriggerTimings = "OnPlay,Activate_Instant,Activate_Response",
                AvailableTargetScopes = "Single",
                BranchConfigs = ""
            });

            // --- 绿色效果 ---
            AddConfig(new AtomicEffectConfig
            {
                Id = 7,
                EnumName = "Heal",
                DisplayName = "治疗",
                Description = "恢复目标 {Value} 点生命",
                BaseCost = 0.8f,
                CostMultiplier = 0.8f,
                TargetType = EffectTargetType.Target,
                TargetFilter = "Creature,Player",
                TargetCount = 1,
                TargetScope = EffectTargetScope.Single,
                DurationType = EffectDurationType.Instant,
                ActivationType = EffectActivationType.Voluntary,
                Stackable = true,
                Priority = 30,
                Tags = "Heal,Green",
                AvailableTriggerTimings = "OnPlay,OnTurnStart,OnTakeDamage,Activate_Instant",
                AvailableTargetScopes = "Single,AoE",
                BranchConfigs = ""
            });

            AddConfig(new AtomicEffectConfig
            {
                Id = 8,
                EnumName = "ModifyPower",
                DisplayName = "增益攻击力",
                Description = "目标攻击力 +{Value}",
                BaseCost = 1.0f,
                CostMultiplier = 1.0f,
                TargetType = EffectTargetType.Target,
                TargetFilter = "Creature",
                TargetCount = 1,
                TargetScope = EffectTargetScope.Single,
                DurationType = EffectDurationType.UntilEndOfTurn,
                ActivationType = EffectActivationType.Voluntary,
                Stackable = true,
                Priority = 30,
                Tags = "Buff,Green",
                AvailableTriggerTimings = "OnPlay,OnTurnStart,Activate_Active,Activate_Instant",
                AvailableTargetScopes = "Single,AoE",
                BranchConfigs = ""
            });

            AddConfig(new AtomicEffectConfig
            {
                Id = 9,
                EnumName = "CreateToken",
                DisplayName = "创建衍生物",
                Description = "创建 {Value} 个衍生物",
                BaseCost = 2.0f,
                CostMultiplier = 1.2f,
                TargetType = EffectTargetType.Self,
                TargetFilter = "",
                TargetCount = 0,
                TargetScope = EffectTargetScope.Single,
                DurationType = EffectDurationType.Instant,
                ActivationType = EffectActivationType.Voluntary,
                Stackable = true,
                Priority = 40,
                Tags = "Summon,Token,Green",
                AvailableTriggerTimings = "OnPlay,OnTurnStart,OnDeath,Activate_Instant",
                AvailableTargetScopes = "Single",
                BranchConfigs = ""
            });
        }

        private static void AddConfig(AtomicEffectConfig config)
        {
            _idMap[config.Id] = config;
            _enumNameMap[config.EnumName] = config;
            if (System.Enum.TryParse<AtomicEffectType>(config.EnumName, out var type))
                _typeMap[type] = config;
        }

        /// <summary>通过AtomicEffectType获取配置</summary>
        public static AtomicEffectConfig GetByType(AtomicEffectType type)
        {
            return _typeMap.TryGetValue(type, out var config) ? config : null;
        }
    }
}
