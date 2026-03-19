using System;
using System.Collections.Generic;
using cfg;
using CardCore.Data;

namespace CardCore
{
    /// <summary>
    /// 卡牌参数类型
    /// </summary>
    public enum CardParameterType
    {
        /// <summary>卡牌名称（可选）</summary>
        Name,
        /// <summary>攻击力（Monster/Legend必填）</summary>
        Power,
        /// <summary>生命值（Monster/Legend必填）</summary>
        Life,
        /// <summary>耐久度（Field必填）</summary>
        Durability,
        /// <summary>效果（Magic必填，Field必填持续效果）</summary>
        Effects,
        /// <summary>立绘（可选）</summary>
        Illustration
    }

    /// <summary>
    /// 单个参数配置
    /// </summary>
    [Serializable]
    public class ParameterConfig
    {
        /// <summary>参数类型</summary>
        public CardParameterType ParameterType;

        /// <summary>是否必填</summary>
        public bool IsRequired;

        /// <summary>默认值（字符串形式）</summary>
        public string DefaultValue;

        /// <summary>最小值（用于数值参数）</summary>
        public int? MinValue;

        /// <summary>最大值（用于数值参数）</summary>
        public int? MaxValue;

        /// <summary>显示名称</summary>
        public string DisplayName;

        /// <summary>参数描述</summary>
        public string Description;
    }

    /// <summary>
    /// 卡牌参数配置 - 根据卡牌类型定义必填/可选参数
    /// </summary>
    [Serializable]
    public class CardParameterConfig
    {
        /// <summary>卡牌类型</summary>
        public CardType CardType { get; private set; }

        /// <summary>必填参数列表</summary>
        public List<ParameterConfig> RequiredParameters { get; private set; } = new List<ParameterConfig>();

        /// <summary>可选参数列表</summary>
        public List<ParameterConfig> OptionalParameters { get; private set; } = new List<ParameterConfig>();

        /// <summary>效果约束 - 持续时间类型限制</summary>
        public DurationType? EffectDurationRestriction { get; private set; }

        /// <summary>是否允许主动效果</summary>
        public bool AllowActiveEffects { get; private set; } = true;

        private CardParameterConfig() { }

        /// <summary>
        /// 获取Monster卡牌参数配置
        /// </summary>
        public static CardParameterConfig ForMonster()
        {
            return new CardParameterConfig
            {
                CardType = CardType.生物,
                RequiredParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Power,
                        IsRequired = true,
                        DefaultValue = "1",
                        MinValue = 0,
                        MaxValue = 99,
                        DisplayName = "攻击力",
                        Description = "怪兽的攻击力"
                    },
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Life,
                        IsRequired = true,
                        DefaultValue = "1",
                        MinValue = 1,
                        MaxValue = 99,
                        DisplayName = "生命值",
                        Description = "怪兽的生命值"
                    }
                },
                OptionalParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Name,
                        IsRequired = false,
                        DefaultValue = "",
                        DisplayName = "卡牌名称",
                        Description = "不填则使用哈希值作为名称"
                    },
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Effects,
                        IsRequired = false,
                        DisplayName = "效果",
                        Description = "怪兽可携带的效果"
                    },
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Illustration,
                        IsRequired = false,
                        DisplayName = "立绘",
                        Description = "卡牌的立绘图片"
                    }
                },
                AllowActiveEffects = true
            };
        }

        /// <summary>
        /// 获取Legend卡牌参数配置
        /// </summary>
        public static CardParameterConfig ForLegend()
        {
            return new CardParameterConfig
            {
                CardType = CardType.传奇,
                RequiredParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Power,
                        IsRequired = true,
                        DefaultValue = "1",
                        MinValue = 0,
                        MaxValue = 99,
                        DisplayName = "攻击力",
                        Description = "传奇的攻击力"
                    },
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Life,
                        IsRequired = true,
                        DefaultValue = "1",
                        MinValue = 1,
                        MaxValue = 99,
                        DisplayName = "生命值",
                        Description = "传奇的生命值"
                    }
                },
                OptionalParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Name,
                        IsRequired = false,
                        DefaultValue = "",
                        DisplayName = "卡牌名称",
                        Description = "不填则使用哈希值作为名称"
                    },
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Effects,
                        IsRequired = false,
                        DisplayName = "效果",
                        Description = "传奇可携带的效果"
                    },
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Illustration,
                        IsRequired = false,
                        DisplayName = "立绘",
                        Description = "卡牌的立绘图片"
                    }
                },
                AllowActiveEffects = true
            };
        }

        /// <summary>
        /// 获取Magic卡牌参数配置
        /// </summary>
        public static CardParameterConfig ForMagic()
        {
            return new CardParameterConfig
            {
                CardType = CardType.术法,
                RequiredParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Effects,
                        IsRequired = true,
                        DisplayName = "效果",
                        Description = "术法必须有效果（仅限一次性效果）"
                    }
                },
                OptionalParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Name,
                        IsRequired = false,
                        DefaultValue = "",
                        DisplayName = "卡牌名称",
                        Description = "不填则使用哈希值作为名称"
                    },
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Illustration,
                        IsRequired = false,
                        DisplayName = "立绘",
                        Description = "卡牌的立绘图片"
                    }
                },
                EffectDurationRestriction = DurationType.Once,
                AllowActiveEffects = true
            };
        }

        /// <summary>
        /// 获取Field卡牌参数配置
        /// </summary>
        public static CardParameterConfig ForField()
        {
            return new CardParameterConfig
            {
                CardType = CardType.领域,
                RequiredParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Effects,
                        IsRequired = true,
                        DisplayName = "效果",
                        Description = "领域必须有效果（仅限持续效果）"
                    },
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Durability,
                        IsRequired = true,
                        DefaultValue = "3",
                        MinValue = 1,
                        MaxValue = 99,
                        DisplayName = "耐久度",
                        Description = "每次效果发动时-1，归零时销毁"
                    }
                },
                OptionalParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Name,
                        IsRequired = false,
                        DefaultValue = "",
                        DisplayName = "卡牌名称",
                        Description = "不填则使用哈希值作为名称"
                    },
                    new ParameterConfig
                    {
                        ParameterType = CardParameterType.Illustration,
                        IsRequired = false,
                        DisplayName = "立绘",
                        Description = "卡牌的立绘图片"
                    }
                },
                EffectDurationRestriction = DurationType.Permanent,
                AllowActiveEffects = false
            };
        }

        /// <summary>
        /// 根据卡牌类型获取配置
        /// </summary>
        public static CardParameterConfig GetConfig(CardType cardType)
        {
            return cardType switch
            {
                CardType.生物 => ForMonster(),
                CardType.传奇 => ForLegend(),
                CardType.术法 => ForMagic(),
                CardType.领域 => ForField(),
                _ => ForMonster()
            };
        }

        /// <summary>
        /// 验证效果是否符合卡牌类型约束
        /// </summary>
        public bool ValidateEffect(EffectDefinitionData effect, out string error)
        {
            error = null;

            if (EffectDurationRestriction.HasValue)
            {
                if ((DurationType)effect.Duration != EffectDurationRestriction.Value)
                {
                    string expected = EffectDurationRestriction.Value switch
                    {
                        DurationType.Once => "一次性效果",
                        DurationType.Permanent => "持续效果",
                        _ => EffectDurationRestriction.Value.ToString()
                    };
                    error = $"{CardType}卡牌只能添加{expected}";
                    return false;
                }
            }

            return true;
        }
    }
}
