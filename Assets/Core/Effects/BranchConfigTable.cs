using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 分支配置静态注册表
    /// </summary>
    public static class BranchConfigTable
    {
        private static Dictionary<string, BranchConfig> _idMap;
        private static Dictionary<string, BranchConfig> _effectTypeMap;

        static BranchConfigTable()
        {
            Initialize();
        }

        private static void Initialize()
        {
            _idMap = new Dictionary<string, BranchConfig>();
            _effectTypeMap = new Dictionary<string, BranchConfig>();

            // --- DealDamage 分支配置 ---
            AddConfig(new BranchConfig
            {
                Id = "DealDamageBranch",
                EffectTypeName = "DealDamage",
                DisplayName = "伤害条件分支",
                MaxChainedEffects = 2,
                AllowNesting = false,
                Conditions = new List<BranchCondition>
                {
                    new BranchCondition
                    {
                        Id = "DmgExceedsLife",
                        DisplayName = "伤害超过目标生命值时",
                        Description = "当造成的伤害超过目标当前生命值时触发",
                        SubConditions = new List<BranchSubCondition>
                        {
                            new BranchSubCondition
                            {
                                Id = "DmgExceedsLife_RaceOrColor",
                                Type = BranchSubConditionType.TargetIsRaceOrColor,
                                DisplayName = "目标是否为特定种族/颜色",
                                Description = "检查目标的种族或颜色属性",
                                ParameterHint = "选择种族或颜色"
                            },
                            new BranchSubCondition
                            {
                                Id = "DmgExceedsLife_AttrCompare",
                                Type = BranchSubConditionType.TargetAttributeCompare,
                                DisplayName = "目标属性值大于/小于",
                                Description = "比较目标的属性值与阈值",
                                ParameterHint = "输入属性和阈值"
                            }
                        }
                    },
                    new BranchCondition
                    {
                        Id = "DmgKillsTarget",
                        DisplayName = "伤害将目标击杀时",
                        Description = "当造成的伤害将目标消灭时触发",
                        SubConditions = new List<BranchSubCondition>
                        {
                            new BranchSubCondition
                            {
                                Id = "DmgKillsTarget_RaceOrColor",
                                Type = BranchSubConditionType.TargetIsRaceOrColor,
                                DisplayName = "目标是否为特定种族/颜色",
                                Description = "检查目标的种族或颜色属性",
                                ParameterHint = "选择种族或颜色"
                            },
                            new BranchSubCondition
                            {
                                Id = "DmgKillsTarget_AttrCompare",
                                Type = BranchSubConditionType.TargetAttributeCompare,
                                DisplayName = "目标属性值大于/小于",
                                Description = "比较目标的属性值与阈值",
                                ParameterHint = "输入属性和阈值"
                            }
                        }
                    }
                }
            });
        }

        private static void AddConfig(BranchConfig config)
        {
            _idMap[config.Id] = config;
            _effectTypeMap[config.EffectTypeName] = config;
        }

        /// <summary>通过ID获取分支配置</summary>
        public static BranchConfig GetById(string id)
        {
            return _idMap.TryGetValue(id, out var config) ? config : null;
        }

        /// <summary>通过原子效果枚举名获取分支配置</summary>
        public static BranchConfig GetByEffectType(string effectTypeName)
        {
            return _effectTypeMap.TryGetValue(effectTypeName, out var config) ? config : null;
        }

        /// <summary>获取所有分支配置</summary>
        public static IEnumerable<BranchConfig> GetAll()
        {
            return _idMap.Values;
        }
    }
}
