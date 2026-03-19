using System;
using System.Collections.Generic;
using UnityEngine;
using cfg;
using CardCore.Data;

namespace CardCore
{
    #region 可序列化效果定义数据

    /// <summary>
    /// 可序列化的效果定义数据
    /// 用于保存和加载效果配置
    /// </summary>
    [Serializable]
    public class EffectDefinitionData
    {
        /// <summary>效果唯一ID</summary>
        public string Id;

        /// <summary>显示名称</summary>
        public string DisplayName;

        /// <summary>效果描述</summary>
        public string Description;

        #region 速度系统
        /// <summary>基础速度</summary>
        public int BaseSpeed;

        /// <summary>发动类型</summary>
        public int ActivationType;
        #endregion

        #region 触发相关
        /// <summary>触发时点</summary>
        public int TriggerTiming;

        /// <summary>触发条件</summary>
        public List<ActivationConditionData> TriggerConditions = new List<ActivationConditionData>();
        #endregion

        #region 发动条件
        /// <summary>发动条件列表</summary>
        public List<ActivationConditionData> ActivationConditions = new List<ActivationConditionData>();
        #endregion

        #region 代价
        /// <summary>发动代价</summary>
        public ActivationCostData Cost = new ActivationCostData();
        #endregion

        #region 目标
        /// <summary>目标选择器</summary>
        public TargetSelectorData TargetSelector;
        #endregion

        #region 原子效果
        /// <summary>原子效果列表</summary>
        public List<AtomicEffectData> Effects = new List<AtomicEffectData>();
        #endregion

        #region 元数据
        /// <summary>是否可选</summary>
        public bool IsOptional;

        /// <summary>持续时间类型</summary>
        public int Duration;

        /// <summary>效果标签</summary>
        public List<string> Tags = new List<string>();

        /// <summary>关联卡牌ID</summary>
        public string SourceCardId;
        #endregion

        /// <summary>
        /// 从 EffectDefinition 转换
        /// </summary>
        public static EffectDefinitionData FromDefinition(EffectDefinition definition)
        {
            if (definition == null) return null;

            var data = new EffectDefinitionData
            {
                Id = definition.Id,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                BaseSpeed = definition.BaseSpeed,
                ActivationType = (int)definition.ActivationType,
                TriggerTiming = (int)definition.TriggerTiming,
                IsOptional = definition.IsOptional,
                Duration = (int)definition.Duration,
                SourceCardId = definition.SourceCardId
            };

            // 转换触发条件
            if (definition.TriggerConditions != null)
            {
                foreach (var condition in definition.TriggerConditions)
                {
                    data.TriggerConditions.Add(ActivationConditionData.FromCondition(condition));
                }
            }

            // 转换发动条件
            if (definition.ActivationConditions != null)
            {
                foreach (var condition in definition.ActivationConditions)
                {
                    data.ActivationConditions.Add(ActivationConditionData.FromCondition(condition));
                }
            }

            // 转换代价
            if (definition.Cost != null)
            {
                data.Cost = ActivationCostData.FromCost(definition.Cost);
            }

            // 转换目标选择器
            if (definition.TargetSelector != null)
            {
                data.TargetSelector = TargetSelectorData.FromSelector(definition.TargetSelector);
            }

            // 转换原子效果
            if (definition.Effects != null)
            {
                foreach (var effect in definition.Effects)
                {
                    data.Effects.Add(AtomicEffectData.FromInstance(effect));
                }
            }

            // 转换标签
            if (definition.Tags != null)
            {
                data.Tags = new List<string>(definition.Tags);
            }

            return data;
        }

        /// <summary>
        /// 转换为 EffectDefinition
        /// </summary>
        public EffectDefinition ToDefinition()
        {
            var definition = new EffectDefinition
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                BaseSpeed = BaseSpeed,
                ActivationType = (EffectActivationType)ActivationType,
                TriggerTiming = (TriggerTiming)TriggerTiming,
                IsOptional = IsOptional,
                Duration = (DurationType)Duration,
                SourceCardId = SourceCardId
            };

            // 转换触发条件
            definition.TriggerConditions = new List<ActivationCondition>();
            foreach (var conditionData in TriggerConditions)
            {
                definition.TriggerConditions.Add(conditionData.ToCondition());
            }

            // 转换发动条件
            definition.ActivationConditions = new List<ActivationCondition>();
            foreach (var conditionData in ActivationConditions)
            {
                definition.ActivationConditions.Add(conditionData.ToCondition());
            }

            // 转换代价
            definition.Cost = Cost?.ToCost() ?? new ActivationCost();

            // 转换目标选择器
            definition.TargetSelector = TargetSelector?.ToSelector();

            // 转换原子效果
            definition.Effects = new List<AtomicEffectInstance>();
            foreach (var effectData in Effects)
            {
                definition.Effects.Add(effectData.ToInstance());
            }

            // 转换标签
            definition.Tags = new List<string>(Tags);

            return definition;
        }
    }

    #endregion

    #region 原子效果数据

    /// <summary>
    /// 可序列化的原子效果数据
    /// </summary>
    [Serializable]
    public class AtomicEffectData
    {
        /// <summary>效果类型</summary>
        public int Type;

        /// <summary>数值参数</summary>
        public int Value;

        /// <summary>数值参数2</summary>
        public int Value2;

        /// <summary>字符串参数</summary>
        public string StringValue;

        /// <summary>法力类型参数</summary>
        public int ManaTypeParam;

        /// <summary>区域参数</summary>
        public int ZoneParam;

        /// <summary>持续时间</summary>
        public int Duration;

        /// <summary>
        /// 从 AtomicEffectInstance 转换
        /// </summary>
        public static AtomicEffectData FromInstance(AtomicEffectInstance instance)
        {
            if (instance == null) return null;

            return new AtomicEffectData
            {
                Type = (int)instance.Type,
                Value = instance.Value,
                Value2 = instance.Value2,
                StringValue = instance.StringValue,
                ManaTypeParam = (int)instance.ManaTypeParam,
                ZoneParam = (int)instance.ZoneParam,
                Duration = (int)instance.Duration
            };
        }

        /// <summary>
        /// 转换为 AtomicEffectInstance
        /// </summary>
        public AtomicEffectInstance ToInstance()
        {
            return new AtomicEffectInstance
            {
                Type = (AtomicEffectType)Type,
                Value = Value,
                Value2 = Value2,
                StringValue = StringValue,
                ManaTypeParam = (ManaType)ManaTypeParam,
                ZoneParam = (Zone)ZoneParam,
                Duration = (DurationType)Duration
            };
        }

        /// <summary>
        /// 获取效果描述
        /// </summary>
        public string GetDescription()
        {
            var instance = ToInstance();
            return instance?.GetDescription() ?? ((AtomicEffectType)Type).ToString();
        }
    }

    #endregion

    #region 目标选择器数据

    /// <summary>
    /// 可序列化的目标选择��数据
    /// </summary>
    [Serializable]
    public class TargetSelectorData
    {
        /// <summary>主目标类型</summary>
        public int PrimaryTarget;

        /// <summary>目标分类（一级）</summary>
        public TargetCategory TargetCategory;

        /// <summary>子目标类型（二级）</summary>
        public SubTargetType SubTargetType;

        /// <summary>目标费用系数</summary>
        public float TargetCoefficient = 1.0f;

        /// <summary>最小目标数</summary>
        public int MinTargets = 1;

        /// <summary>最大目标数</summary>
        public int MaxTargets = 1;

        /// <summary>目标是否可选</summary>
        public bool Optional;

        /// <summary>是否需要玩家选择</summary>
        public bool RequiresPlayerSelection = true;

        /// <summary>目标筛选条件</summary>
        public TargetFilterData Filter;

        /// <summary>
        /// 从 TargetSelector 转换
        /// </summary>
        public static TargetSelectorData FromSelector(TargetSelector selector)
        {
            if (selector == null) return null;

            return new TargetSelectorData
            {
                PrimaryTarget = (int)selector.PrimaryTarget,
                TargetCategory = selector.TargetCategory,
                SubTargetType = selector.SubTargetType,
                TargetCoefficient = selector.TargetCoefficient,
                MinTargets = selector.MinTargets,
                MaxTargets = selector.MaxTargets,
                Optional = selector.Optional,
                RequiresPlayerSelection = selector.RequiresPlayerSelection,
                Filter = TargetFilterData.FromFilter(selector.Filter)
            };
        }

        /// <summary>
        /// 转换为 TargetSelector
        /// </summary>
        public TargetSelector ToSelector()
        {
            return new TargetSelector
            {
                PrimaryTarget = (TargetType)PrimaryTarget,
                TargetCategory = TargetCategory,
                SubTargetType = SubTargetType,
                TargetCoefficient = TargetCoefficient,
                MinTargets = MinTargets,
                MaxTargets = MaxTargets,
                Optional = Optional,
                RequiresPlayerSelection = RequiresPlayerSelection,
                Filter = Filter?.ToFilter()
            };
        }

        /// <summary>
        /// 获取描述
        /// </summary>
        public string GetDescription()
        {
            string subTargetName = TargetCoefficientConfig.GetDisplayName(SubTargetType);
            float coef = TargetCoefficient;

            if (TargetCoefficientConfig.NeedsTargetCountInput(TargetCategory) && MaxTargets > 1)
            {
                return $"{subTargetName} × {MaxTargets} (系数: {coef:F2})";
            }

            return $"{subTargetName} (系数: {coef:F2})";
        }
    }

    /// <summary>
    /// 可序列化的目标筛选条件数据
    /// </summary>
    [Serializable]
    public class TargetFilterData
    {
        /// <summary>卡牌类型筛选</summary>
        public int? CardType;

        /// <summary>法力类型筛选</summary>
        public int? ManaType;

        /// <summary>攻击力条件运算符</summary>
        public int PowerConditionOperator;

        /// <summary>攻击力条件值</summary>
        public int PowerConditionValue;

        /// <summary>是否有攻击力条件</summary>
        public bool HasPowerCondition;

        /// <summary>生命值条件运算符</summary>
        public int LifeConditionOperator;

        /// <summary>生命值条件值</summary>
        public int LifeConditionValue;

        /// <summary>是否有生命值条件</summary>
        public bool HasLifeCondition;

        /// <summary>区域筛选</summary>
        public int? TargetZone;

        /// <summary>横置状态筛选</summary>
        public bool? IsTapped;

        /// <summary>名称包含</summary>
        public string NameContains;

        /// <summary>排除自身</summary>
        public bool ExcludeSelf;

        /// <summary>排除来源</summary>
        public bool ExcludeSource = true;

        /// <summary>
        /// 从 TargetFilter 转换
        /// </summary>
        public static TargetFilterData FromFilter(TargetFilter filter)
        {
            if (filter == null) return null;

            var data = new TargetFilterData
            {
                CardType = filter.CardType.HasValue ? (int)filter.CardType.Value : null,
                ManaType = filter.ManaType.HasValue ? (int)filter.ManaType.Value : null,
                TargetZone = filter.TargetZone.HasValue ? (int)filter.TargetZone.Value : null,
                IsTapped = filter.IsTapped,
                NameContains = filter.NameContains,
                ExcludeSelf = filter.ExcludeSelf,
                ExcludeSource = filter.ExcludeSource
            };

            if (filter.PowerCondition != null)
            {
                data.HasPowerCondition = true;
                data.PowerConditionOperator = (int)filter.PowerCondition.Operator;
                data.PowerConditionValue = filter.PowerCondition.Value;
            }

            if (filter.LifeCondition != null)
            {
                data.HasLifeCondition = true;
                data.LifeConditionOperator = (int)filter.LifeCondition.Operator;
                data.LifeConditionValue = filter.LifeCondition.Value;
            }

            return data;
        }

        /// <summary>
        /// 转换为 TargetFilter
        /// </summary>
        public TargetFilter ToFilter()
        {
            var filter = new TargetFilter
            {
                CardType = CardType.HasValue ? (CardCore.CardType)CardType.Value : null,
                ManaType = ManaType.HasValue ? (ManaType)ManaType.Value : null,
                TargetZone = TargetZone.HasValue ? (Zone)TargetZone.Value : null,
                IsTapped = IsTapped,
                NameContains = NameContains,
                ExcludeSelf = ExcludeSelf,
                ExcludeSource = ExcludeSource
            };

            if (HasPowerCondition)
            {
                filter.PowerCondition = new ComparisonCondition
                {
                    Operator = (ComparisonOperator)PowerConditionOperator,
                    Value = PowerConditionValue
                };
            }

            if (HasLifeCondition)
            {
                filter.LifeCondition = new ComparisonCondition
                {
                    Operator = (ComparisonOperator)LifeConditionOperator,
                    Value = LifeConditionValue
                };
            }

            return filter;
        }
    }

    #endregion

    #region 发动条件数据

    /// <summary>
    /// 可序列化的发动条件数据
    /// </summary>
    [Serializable]
    public class ActivationConditionData
    {
        /// <summary>条件类型</summary>
        public int Type;

        /// <summary>数值参数</summary>
        public int Value;

        /// <summary>数值参数2</summary>
        public int Value2;

        /// <summary>卡牌类型参数</summary>
        public int? CardTypeParam;

        /// <summary>法力类型参数</summary>
        public int? ManaTypeParam;

        /// <summary>字符串参数</summary>
        public string StringValue;

        /// <summary>是否取反</summary>
        public bool Negate;

        /// <summary>自定义条件ID</summary>
        public string CustomConditionId;

        /// <summary>
        /// 从 ActivationCondition 转换
        /// </summary>
        public static ActivationConditionData FromCondition(ActivationCondition condition)
        {
            if (condition == null) return null;

            return new ActivationConditionData
            {
                Type = (int)condition.Type,
                Value = condition.Value,
                Value2 = condition.Value2,
                CardTypeParam = condition.CardTypeParam.HasValue ? (int)condition.CardTypeParam.Value : null,
                ManaTypeParam = condition.ManaTypeParam.HasValue ? (int)condition.ManaTypeParam.Value : null,
                StringValue = condition.StringValue,
                Negate = condition.Negate,
                CustomConditionId = condition.CustomConditionId
            };
        }

        /// <summary>
        /// 转换为 ActivationCondition
        /// </summary>
        public ActivationCondition ToCondition()
        {
            return new ActivationCondition
            {
                Type = (ConditionType)Type,
                Value = Value,
                Value2 = Value2,
                CardTypeParam = CardTypeParam.HasValue ? (CardCore.CardType)CardTypeParam.Value : null,
                ManaTypeParam = ManaTypeParam.HasValue ? (ManaType)ManaTypeParam.Value : null,
                StringValue = StringValue,
                Negate = Negate,
                CustomConditionId = CustomConditionId
            };
        }

        /// <summary>
        /// 获取条件描述
        /// </summary>
        public string GetDescription()
        {
            var condition = ToCondition();
            return condition?.GetDescription() ?? ((ConditionType)Type).ToString();
        }
    }

    #endregion

    #region 代价数据

    /// <summary>
    /// 可序列化的发动代价数据
    /// </summary>
    [Serializable]
    public class ActivationCostData
    {
        /// <summary>元素代价列表</summary>
        public List<ElementCostData> ElementCosts = new List<ElementCostData>();

        /// <summary>资源代价列表</summary>
        public List<ResourceCostData> ResourceCosts = new List<ResourceCostData>();

        /// <summary>预支代价列表</summary>
        public List<PrepayCostData> PrepayCosts = new List<PrepayCostData>();

        /// <summary>
        /// 从 ActivationCost 转换
        /// </summary>
        public static ActivationCostData FromCost(ActivationCost cost)
        {
            if (cost == null) return null;

            var data = new ActivationCostData();

            if (cost.ElementCosts != null)
            {
                foreach (var elementCost in cost.ElementCosts)
                {
                    data.ElementCosts.Add(ElementCostData.FromElementCost(elementCost));
                }
            }

            if (cost.ResourceCosts != null)
            {
                foreach (var resourceCost in cost.ResourceCosts)
                {
                    data.ResourceCosts.Add(ResourceCostData.FromResourceCost(resourceCost));
                }
            }

            if (cost.PrepayCosts != null)
            {
                foreach (var prepayCost in cost.PrepayCosts)
                {
                    data.PrepayCosts.Add(PrepayCostData.FromPrepayCost(prepayCost));
                }
            }

            return data;
        }

        /// <summary>
        /// 转换为 ActivationCost
        /// </summary>
        public ActivationCost ToCost()
        {
            var cost = new ActivationCost();

            foreach (var elementData in ElementCosts)
            {
                cost.ElementCosts.Add(elementData.ToElementCost());
            }

            foreach (var resourceData in ResourceCosts)
            {
                cost.ResourceCosts.Add(resourceData.ToResourceCost());
            }

            foreach (var prepayData in PrepayCosts)
            {
                cost.PrepayCosts.Add(prepayData.ToPrepayCost());
            }

            return cost;
        }

        /// <summary>
        /// 是否为空代价
        /// </summary>
        public bool IsEmpty => ElementCosts.Count == 0 && ResourceCosts.Count == 0 && PrepayCosts.Count == 0;

        /// <summary>
        /// 获取代价描述
        /// </summary>
        public string GetDescription()
        {
            if (IsEmpty) return "无代价";

            var cost = ToCost();
            return cost.GetDescription();
        }
    }

    /// <summary>
    /// 可序列化的元素代价数据
    /// </summary>
    [Serializable]
    public class ElementCostData
    {
        /// <summary>元素类型</summary>
        public int ManaType;

        /// <summary>数量</summary>
        public int Amount;

        /// <summary>
        /// 从 ElementCost 转换
        /// </summary>
        public static ElementCostData FromElementCost(ElementCost cost)
        {
            if (cost == null) return null;

            return new ElementCostData
            {
                ManaType = (int)cost.ManaType,
                Amount = cost.Amount
            };
        }

        /// <summary>
        /// 转换为 ElementCost
        /// </summary>
        public ElementCost ToElementCost()
        {
            return new ElementCost
            {
                ManaType = (ManaType)ManaType,
                Amount = Amount
            };
        }
    }

    /// <summary>
    /// 可序列化的资源代价数据
    /// </summary>
    [Serializable]
    public class ResourceCostData
    {
        /// <summary>来源区域</summary>
        public int FromZone;

        /// <summary>去向区域</summary>
        public int ToZone;

        /// <summary>数量</summary>
        public int Count = 1;

        /// <summary>是否需要选择</summary>
        public bool RequireSelection = true;

        /// <summary>
        /// 从 ResourceCost 转换
        /// </summary>
        public static ResourceCostData FromResourceCost(ResourceCost cost)
        {
            if (cost == null) return null;

            return new ResourceCostData
            {
                FromZone = (int)cost.FromZone,
                ToZone = (int)cost.ToZone,
                Count = cost.Count,
                RequireSelection = cost.RequireSelection
            };
        }

        /// <summary>
        /// 转换为 ResourceCost
        /// </summary>
        public ResourceCost ToResourceCost()
        {
            return new ResourceCost
            {
                FromZone = (ResourceZone)FromZone,
                ToZone = (DestinationZone)ToZone,
                Count = Count,
                RequireSelection = RequireSelection
            };
        }

        /// <summary>
        /// 获取描述
        /// </summary>
        public string GetDescription()
        {
            var cost = ToResourceCost();
            return cost.GetDescription();
        }
    }

    /// <summary>
    /// 可序列化的预支代价数据
    /// </summary>
    [Serializable]
    public class PrepayCostData
    {
        /// <summary>预支类型</summary>
        public int PrepayType;

        /// <summary>预支数量</summary>
        public int Amount;

        /// <summary>偿还回合数</summary>
        public int RepayTurns = 1;

        /// <summary>
        /// 从 PrepayCost 转换
        /// </summary>
        public static PrepayCostData FromPrepayCost(PrepayCost cost)
        {
            if (cost == null) return null;

            return new PrepayCostData
            {
                PrepayType = (int)cost.PrepayType,
                Amount = cost.Amount,
                RepayTurns = cost.RepayTurns
            };
        }

        /// <summary>
        /// 转换为 PrepayCost
        /// </summary>
        public PrepayCost ToPrepayCost()
        {
            return new PrepayCost
            {
                PrepayType = (PrepayType)PrepayType,
                Amount = Amount,
                RepayTurns = RepayTurns
            };
        }

        /// <summary>
        /// 获取描述
        /// </summary>
        public string GetDescription()
        {
            var cost = ToPrepayCost();
            return cost.GetDescription();
        }
    }

    #endregion

    #region 数据列表包装类（用于JSON序列化）

    /// <summary>
    /// 效果定义列表包装类
    /// </summary>
    [Serializable]
    public class EffectDefinitionDataList
    {
        public List<EffectDefinitionData> Effects = new List<EffectDefinitionData>();
    }

    #endregion
}