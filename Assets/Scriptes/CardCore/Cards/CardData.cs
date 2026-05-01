using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardCore
{
    /// <summary>
    /// 卡牌数据类 - 可序列化的卡牌实体
    /// 通过实现多个接口来组合不同的卡牌属性
    /// </summary>
    [Serializable]
    public class CardData
    {
        /// <summary>
        /// 卡牌唯一标识ID（基于内容Hash生成）
        /// </summary>
        [SerializeField]
        private string _id;
        public string ID
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>
        /// 卡牌类型
        /// </summary>
        [SerializeField]
        private Cardtype _supertype;
        public Cardtype Supertype
        {
            get => _supertype;
            set => _supertype = value;
        }

        /// <summary>
        /// 卡牌名称
        /// </summary>
        [SerializeField]
        private string _cardName;
        public string CardName
        {
            get => _cardName;
            set => _cardName = value;
        }

        /// <summary>
        /// 立绘路径
        /// </summary>
        [SerializeField]
        private string _illustration;
        public string Illustration
        {
            get => _illustration;
            set => _illustration = value;
        }

        /// <summary>
        /// 生命值（可选）
        /// </summary>
        [SerializeField]
        private int? _life;
        public int? Life
        {
            get => _life;
            set => _life = value;
        }

        /// <summary>
        /// 攻击力（可选）
        /// </summary>
        [SerializeField]
        private int? _power;
        public int? Power
        {
            get => _power;
            set => _power = value;
        }

        /// <summary>
        /// 法力消耗
        /// </summary>
        [SerializeField]
        private string _costJson;
        public Dictionary<int, float> Cost { get; set; } = new Dictionary<int, float>();

        /// <summary>
        /// 效果列表
        /// </summary>
        [SerializeField]
        private List<CardEffectData> _effects;
        public List<CardEffectData> Effects
        {
            get => _effects ??= new List<CardEffectData>();
            set => _effects = value;
        }
        

        /// <summary>
        /// 创建时间（用于时间戳）
        /// </summary>
        [SerializeField]
        private long _creationTicks;
        public DateTime CreationTime
        {
            get => new DateTime(_creationTicks);
            set => _creationTicks = value.Ticks;
        }

        /// <summary>
        /// 卡牌标签（用于存储额外属性，如Durability等）
        /// </summary>
        [SerializeField]
        private List<string> _tags;
        public List<string> Tags
        {
            get => _tags ??= new List<string>();
            set => _tags = value;
        }

        /// <summary>
        /// 关键词列表（如冲锋、飞行、圣盾等自身被动效果）
        /// </summary>
        [SerializeField]
        private List<string> _keywords;
        public List<string> Keywords
        {
            get => _keywords ??= new List<string>();
            set => _keywords = value;
        }

        /// <summary>
        /// 总费用计算
        /// </summary>
        [NonSerialized]
        private float _totalCost = -1;
        public float TotalCost
        {
            get
            {
                if (_totalCost < 0)
                {
                    float total = 0;
                    foreach (var cost in Cost.Values)
                    {
                        total += cost;
                    }
                    _totalCost = total;
                }
                return _totalCost;
            }
        }

        /// <summary>
        /// 是否有主动效果
        /// </summary>
        [NonSerialized]
        private bool? _hasActiveEffect;
        public bool HasActiveEffect
        {
            get
            {
                if (!_hasActiveEffect.HasValue)
                {
                    foreach (var effect in Effects)
                    {
                        if (effect.TriggerTiming == (int)TriggerTiming.Activate_Active ||
                            effect.TriggerTiming == (int)TriggerTiming.Activate_Instant)
                        {
                            _hasActiveEffect = true;
                            return true;
                        }
                    }
                    _hasActiveEffect = false;
                }
                return _hasActiveEffect.Value;
            }
        }

        /// <summary>
        /// 是否有战斗属性
        /// </summary>
        public bool HasCombatStats => Life.HasValue || Power.HasValue;

        /// <summary>
        /// 序列化前处理
        /// </summary>
        public void OnBeforeSerialize()
        {
            // 将 Cost 序列化为 JSON
            _costJson = CostToJson();
        }

        /// <summary>
        /// 反序列化后处理
        /// </summary>
        public void OnAfterDeserialize()
        {
            // 从 JSON 反序列化 Cost
            Cost = JsonFromCost(_costJson);
        }

        /// <summary>
        /// 根据卡牌类型计算Hash作为ID
        /// </summary>
        public string CalculateID()
        {
            string content = JsonUtility.ToJson(this);
            uint hash = MurmurHash3.Hash32(content);
            return hash.ToString("X8");
        }

        /// <summary>
        /// 将 Cost 字典序列化为 JSON
        /// </summary>
        private string CostToJson()
        {
            var costList = new List<CostEntry>();
            foreach (var kvp in Cost)
            {
                costList.Add(new CostEntry { ManaType = kvp.Key, Value = kvp.Value });
            }
            return JsonUtility.ToJson(costList, true);
        }

        /// <summary>
        /// 从 JSON 反序列化 Cost 字典
        /// </summary>
        private Dictionary<int, float> JsonFromCost(string json)
        {
            var result = new Dictionary<int, float>();
            if (string.IsNullOrEmpty(json)) return result;

            try
            {
                var costList = JsonUtility.FromJson<CostEntryList>(json);
                foreach (var entry in costList.entries)
                {
                    result[entry.ManaType] = entry.Value;
                }
            }
            catch
            {
                // 解析失败返回空字典
            }
            return result;
        }

        /// <summary>
        /// 重置缓存
        /// </summary>
        public void ResetCache()
        {
            _totalCost = -1;
            _hasActiveEffect = null;
        }

        /// <summary>
        /// 成本条目
        /// </summary>
        [Serializable]
        private class CostEntry
        {
            public int ManaType;
            public float Value;
        }

        /// <summary>
        /// 成本条目列表包装
        /// </summary>
        [Serializable]
        private class CostEntryList
        {
            public List<CostEntry> entries;
        }
    }

    /// <summary>
    /// 原子效果条目 —— 描述单个原子效果的所有参数
    /// 直接映射为 AtomicEffectInstance
    /// </summary>
    [Serializable]
    public class AtomicEffectEntry
    {
        public string EffectType;      // AtomicEffectType 枚举名，如 "DealDamage"
        public int Value;              // 主数值（伤害量、抽卡数、攻血修改量）
        public int Value2;             // 副数值（ModifyAllStats 的 life 值）
        public string StringValue;     // 字符串参数（token ID、关键词 ID）
        public int ManaTypeParam;      // ManaType 枚举值
        public int ZoneParam;          // Zone 枚举值
        public int Duration;           // DurationType 枚举值，0 = 使用表默认
    }

    /// <summary>
    /// 代价条目 —— 卡牌效果的费用配置
    /// </summary>
    [Serializable]
    public class CostEntry
    {
        public int CostType;           // CostType 枚举值
        public int Value;              // 代价数值
        public int ManaType;           // 元素消耗的 ManaType
        public int TurnDuration;       // 沉睡回合数
    }

    /// <summary>
    /// 发动条件条目
    /// </summary>
    [Serializable]
    public class ActivationConditionData
    {
        public int Type;               // ConditionType 枚举值
        public int Value;
        public int Value2;
        public string StringValue;
        public bool Negate;
    }

    /// <summary>
    /// 卡牌效果数据 —— 描述卡牌的一个完整效果
    /// 包含触发时点、条件、代价、以及有序的原子效果列表
    /// 转换后成为一个 EffectDefinition
    /// </summary>
    [Serializable]
    public class CardEffectData
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public int TriggerTiming;      // TriggerTiming 枚举值
        public int ActivationType;     // 0=强制, 1=自动, 2=主动
        public int BaseSpeed;
        public bool IsOptional;
        public int Duration;           // 整体效果的 DurationType

        public List<ActivationConditionData> ActivationConditions;
        public List<ActivationConditionData> TriggerConditions;
        public List<AtomicEffectEntry> AtomicEffects;
        public List<CostEntry> Costs;
        public List<string> Tags;
    }

    /// <summary>
    /// 卡牌数据包装器 - 用于将CardData包装为可运行时使用的卡牌
    /// </summary>
    public class CardWrapper : Card,
        IHasSupertype,
        IHasName,
        IHasIllustration,
        IHasLife,
        IHasPower,
        IHasCost,
        IHasEffects,
        IHasRuntimeEffects,
        IHasKeywords,
        CardDataWrapper
    {
        private readonly CardData _data;
        private List<IEffect> _runtimeEffects;

        // IHasSupertype
        Cardtype IHasSupertype.Supertype { get; set ; }

        // IHasName
        string IHasName.CardName { get; set; }

        // IHasIllustration
        string IHasIllustration.Illustration { get; set; }

        // IHasLife
        int IHasLife.Life { get; set; }

        // IHasPower
        int IHasPower.Power { get; set; }

        // IHasCost
        Dictionary<int, float> IHasCost.Cost { get; set; }

        // IHasEffects
        List<Effect_table> IHasEffects.Effects { get; set; }

        // IHasRuntimeEffects
        List<IEffect> IHasRuntimeEffects.RuntimeEffects
        {
            get => _runtimeEffects;
            set => _runtimeEffects = value;
        }

        // IHasKeywords - 直接使用 Card._keywords，与 EntityEffectExtensions 统一
        HashSet<string> IHasKeywords.Keywords
        {
            get => _keywords;
            set
            {
                _keywords.Clear();
                if (value != null)
                    foreach (var kw in value)
                        _keywords.Add(kw);
            }
        }

        void IHasKeywords.AddKeyword(string keywordId)
        {
            _keywords.Add(keywordId);
        }

        void IHasKeywords.RemoveKeyword(string keywordId)
        {
            _keywords.Remove(keywordId);
        }

        bool IHasKeywords.HasKeyword(string keywordId)
        {
            return _keywords.Contains(keywordId);
        }

        /// <summary>
        /// 从 CardData 创建 CardWrapper
        /// </summary>
        public CardWrapper(CardData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));

            ID = data.ID;

            // 设置接口属性
            (this as IHasSupertype).Supertype = data.Supertype;
            (this as IHasName).CardName = data.CardName;
            (this as IHasIllustration).Illustration = data.Illustration;

            // 设置可选属性
            if (data.Life.HasValue)
                (this as IHasLife).Life = data.Life.Value;
            else
                (this as IHasLife).Life = 0;

            if (data.Power.HasValue)
                (this as IHasPower).Power = data.Power.Value;
            else
                (this as IHasPower).Power = 0;

            // 设置费用
            (this as IHasCost).Cost = data.Cost;

            // 设置效果列表（Editor路径使用 Effect_table，运行时由 CardEffectConverter 转换）
            (this as IHasEffects).Effects = new List<Effect_table>();

            // 初始化运行时效果列表（由 CardEffectConverter 在 PlayCard 时填充）
            _runtimeEffects = new List<IEffect>();

            // 注入关键词到 Card._keywords（与 EntityEffectExtensions 统一）
            foreach (var kw in data.Keywords)
            {
                _keywords.Add(kw);
            }
        }

        /// <summary>
        /// 获取原始数据
        /// </summary>
        public CardData GetData() => _data;
    }

    /// <summary>
    /// 预定义的卡牌模板
    /// </summary>
    [Serializable]
    public class CardTemplate
    {
        [SerializeField]
        private Cardtype _supertype;
        public Cardtype Supertype
        {
            get => _supertype;
            set => _supertype = value;
        }

        [SerializeField]
        private string _cardName;
        public string CardName
        {
            get => _cardName;
            set => _cardName = value;
        }

        [SerializeField]
        private string _illustration;
        public string Illustration
        {
            get => _illustration;
            set => _illustration = value;
        }

        [SerializeField]
        private int? _defaultLife;
        public int? DefaultLife
        {
            get => _defaultLife;
            set => _defaultLife = value;
        }

        [SerializeField]
        private int? _defaultPower;
        public int? DefaultPower
        {
            get => _defaultPower;
            set => _defaultPower = value;
        }

        [SerializeField]
        private List<CardEffectData> _defaultEffects;
        public List<CardEffectData> DefaultEffects
        {
            get => _defaultEffects ??= new List<CardEffectData>();
            set => _defaultEffects = value;
        }

        /// <summary>
        /// 创建基础卡牌数据
        /// </summary>
        public CardData CreateCardData()
        {
            return new CardData
            {
                Supertype = Supertype,
                CardName = CardName,
                Illustration = Illustration,
                Life = DefaultLife,
                Power = DefaultPower,
                Effects = new List<CardEffectData>(DefaultEffects),
                Cost = new Dictionary<int, float>()
            };
        }
    }
}
