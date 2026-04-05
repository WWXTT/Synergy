using System;
using System.Collections.Generic;
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
        private List<EffectData> _effects;
        public List<EffectData> Effects
        {
            get => _effects ??= new List<EffectData>();
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
                        if (effect.Initiative)
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
    /// 效果数据 - 可序列化的效果配置
    /// </summary>
    [Serializable]
    public class EffectData
    {
        /// <summary>
        /// 效果缩写/唯一标识
        /// </summary>
        [SerializeField]
        private string _abbreviation;
        public string Abbreviation
        {
            get => _abbreviation;
            set => _abbreviation = value;
        }

        /// <summary>
        /// 是否为主动效果
        /// </summary>
        [SerializeField]
        private bool _initiative;
        public bool Initiative
        {
            get => _initiative;
            set => _initiative = value;
        }

        /// <summary>
        /// 效果参数值
        /// </summary>
        [SerializeField]
        private float _parameters;
        public float Parameters
        {
            get => _parameters;
            set => _parameters = value;
        }

        /// <summary>
        /// 效果速度
        /// </summary>
        [SerializeField]
        private int _speed;
        public EffectSpeed Speed
        {
            get => (EffectSpeed)_speed;
            set => _speed = (int)value;
        }

        /// <summary>
        /// 法力类型
        /// </summary>
        [SerializeField]
        private int _manaType;
        public ManaType ManaType
        {
            get => (ManaType)_manaType;
            set => _manaType = (int)value;
        }

        /// <summary>
        /// 效果描述
        /// </summary>
        [SerializeField]
        private string _description;
        public string Description
        {
            get => _description;
            set => _description = value;
        }

        /// <summary>
        /// 从 Effect_table 转换
        /// </summary>
        public static EffectData FromEffectTable(Effect_table table)
        {
            return new EffectData
            {
                Abbreviation = table.Effect_Abbreviation,
                Initiative = table.Initiative,
                Parameters = table.Parameters,
                Speed = table.EffctSpeed,
                ManaType = table.Mana_type,
                Description = table.Effect_Description
            };
        }

        /// <summary>
        /// 转换为 Effect_table
        /// </summary>
        public Effect_table ToEffectTable()
        {
            return new Effect_table
            {
                Effect_Abbreviation = Abbreviation,
                Initiative = Initiative,
                Parameters = Parameters,
                EffctSpeed = Speed,
                Mana_type = ManaType,
                Effect_Description = Description
            };
        }
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
        IHasRuntimeEffects
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

            // 设置效果列表
            (this as IHasEffects).Effects = new List<Effect_table>();
            foreach (var effect in data.Effects)
            {
                (this as IHasEffects).Effects.Add(effect.ToEffectTable());
            }

            // 初始化运行时效果列表（需要从效果表中解析）
            _runtimeEffects = new List<IEffect>();
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
        private List<EffectData> _defaultEffects;
        public List<EffectData> DefaultEffects
        {
            get => _defaultEffects ??= new List<EffectData>();
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
                Effects = new List<EffectData>(DefaultEffects),
                Cost = new Dictionary<int, float>()
            };
        }
    }
}
