using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    /// <summary>
    /// 持续效果层类型（简化版，3-5层）
    /// </summary>
    public enum LayerType
    {
        /// <summary>
        /// Layer 1: 特征定义能力（CDAs）
        /// 优先级最高
        /// </summary>
        Layer1 = 1,

        /// <summary>
        /// Layer 2: 内部效果顺序
        /// 同一来源的效果按添加顺序排序
        /// </summary>
        Layer2 = 2,

        /// <summary>
        /// Layer 3: 按时间戳排序
        /// 先入场的优先
        /// </summary>
        Layer3 = 3,

        /// <summary>
        /// Layer 4: 状态修改效果
        /// 修改对象特征的效果
        /// </summary>
        Layer4 = 4,

        /// <summary>
        /// Layer 5: 状态检查后效果
        /// 状态检查后添加的效果
        /// </summary>
        Layer5 = 5
    }

    /// <summary>
    /// 效果影响范围
    /// </summary>
    public enum EffectRange
    {
        /// <summary>
        /// 全局
        /// </summary>
        Global,

        /// <summary>
        /// 局部
        /// </summary>
        Local,

        /// <summary>
        /// 特定目标
        /// </summary>
        Targeted
    }

    #region 特征修饰器类型

    /// <summary>
    /// 特征修饰器基接口
    /// 每种修饰器明确知道自己操作的值类型，避免装箱/拆箱
    /// </summary>
    public interface ICharacteristicModification
    {
        CharacteristicType TargetCharacteristic { get; }
    }

    /// <summary>
    /// 整数加法修饰器（用于 Power/Toughness 的增减）
    /// </summary>
    public sealed class IntAddModification : ICharacteristicModification
    {
        public CharacteristicType TargetCharacteristic { get; }
        public int Delta { get; }

        public IntAddModification(CharacteristicType target, int delta)
        {
            TargetCharacteristic = target;
            Delta = delta;
        }

        public int Apply(int original) => original + Delta;
    }

    /// <summary>
    /// 整数设定修饰器（将值设为固定值）
    /// </summary>
    public sealed class IntSetModification : ICharacteristicModification
    {
        public CharacteristicType TargetCharacteristic { get; }
        public int Value { get; }

        public IntSetModification(CharacteristicType target, int value)
        {
            TargetCharacteristic = target;
            Value = value;
        }

        public int Apply(int original) => Value;
    }

    /// <summary>
    /// 费用修饰器（修改费用字典）
    /// </summary>
    public sealed class CostAddModification : ICharacteristicModification
    {
        public CharacteristicType TargetCharacteristic => CharacteristicType.Cost;
        public Dictionary<int, float> CostDelta { get; }

        public CostAddModification(Dictionary<int, float> costDelta)
        {
            CostDelta = costDelta;
        }

        public Dictionary<int, float> Apply(Dictionary<int, float> original)
        {
            var result = new Dictionary<int, float>(original);
            foreach (var kvp in CostDelta)
            {
                if (result.ContainsKey(kvp.Key))
                    result[kvp.Key] += kvp.Value;
                else
                    result[kvp.Key] = kvp.Value;
            }
            return result;
        }
    }

    /// <summary>
    /// 颜色添加修饰器
    /// </summary>
    public sealed class ColorAddModification : ICharacteristicModification
    {
        public CharacteristicType TargetCharacteristic => CharacteristicType.Color;
        public List<ManaType> AddedColors { get; }

        public ColorAddModification(List<ManaType> addedColors)
        {
            AddedColors = addedColors;
        }

        public List<ManaType> Apply(List<ManaType> original)
        {
            var result = new List<ManaType>(original);
            foreach (var color in AddedColors)
            {
                if (!result.Contains(color))
                    result.Add(color);
            }
            return result;
        }
    }

    #endregion

    /// <summary>
    /// 持续效果记录（用于层引擎内部）
    /// </summary>
    public class LayerContinuousEffect
    {
        public Effect SourceEffect { get; set; }
        public Entity SourceEntity { get; set; }
        public Entity TargetEntity { get; set; } // 可以为空（全局效果）
        public LayerType Layer { get; set; }
        public DateTime StartTime { get; set; }
        public int SequenceNumber { get; set; }
        public DurationType Duration { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<CharacteristicType, ICharacteristicModification> Modifications { get; set; }
            = new Dictionary<CharacteristicType, ICharacteristicModification>();

        /// <summary>
        /// 检查此效果是否影响指定特征
        /// </summary>
        public bool AffectsCharacteristic(CharacteristicType type)
        {
            return Modifications.ContainsKey(type);
        }

        /// <summary>
        /// 获取指定特征的修饰器
        /// </summary>
        public ICharacteristicModification GetModification(CharacteristicType type)
        {
            return Modifications.TryGetValue(type, out var mod) ? mod : null;
        }

        /// <summary>
        /// 对整数特征应用修饰（Power/Toughness）
        /// </summary>
        public int ApplyIntModification(CharacteristicType type, int original)
        {
            if (!Modifications.TryGetValue(type, out var mod))
                return original;

            return mod switch
            {
                IntAddModification add => add.Apply(original),
                IntSetModification set => set.Apply(original),
                _ => original
            };
        }

        /// <summary>
        /// 对费用特征应用修饰
        /// </summary>
        public Dictionary<int, float> ApplyCostModification(Dictionary<int, float> original)
        {
            if (!Modifications.TryGetValue(CharacteristicType.Cost, out var mod))
                return original;

            return mod is CostAddModification costMod ? costMod.Apply(original) : original;
        }

        /// <summary>
        /// 对颜色特征应用修饰
        /// </summary>
        public List<ManaType> ApplyColorModification(List<ManaType> original)
        {
            if (!Modifications.TryGetValue(CharacteristicType.Color, out var mod))
                return original;

            return mod is ColorAddModification colorMod ? colorMod.Apply(original) : original;
        }
    }

    /// <summary>
    /// 特征定义能力（CDA）
    /// </summary>
    public class CharacteristicDefiningAbility
    {
        public Entity Source { get; set; }
        public Effect SourceEffect { get; set; }
        public CharacteristicDefiningType DefinitionType { get; set; }
        public object DefinedValue { get; set; }
        public DateTime Timestamp { get; set; }
        public int SequenceNumber { get; set; }

        /// <summary>
        /// 定义的目标实体
        /// </summary>
        public Entity DefinedEntity { get; set; }
    }

    /// <summary>
    /// 层引擎（简化版）
    /// 负责重新计算所有对象的当前状态、按层排序、时间戳排序
    /// 使用脏标记缓存机制，仅在修饰器变更时重算
    /// </summary>
    public class LayerEngine
    {
        // 实体的持续效果集合
        private Dictionary<Entity, List<LayerContinuousEffect>> _entityEffects =
            new Dictionary<Entity, List<LayerContinuousEffect>>();

        // 特征定义能力集合
        private Dictionary<Entity, List<CharacteristicDefiningAbility>> _entityCDAs =
            new Dictionary<Entity, List<CharacteristicDefiningAbility>>();

        // 全局持续效果
        private List<LayerContinuousEffect> _globalEffects = new List<LayerContinuousEffect>();

        // 全局特征定义能力
        private List<CharacteristicDefiningAbility> _globalCDAs = new List<CharacteristicDefiningAbility>();

        // 脏标记缓存
        private HashSet<Entity> _dirtyEntities = new HashSet<Entity>();
        private bool _globalDirty = true;
        private Dictionary<Entity, CharacteristicCache> _cache =
            new Dictionary<Entity, CharacteristicCache>();

        /// <summary>
        /// 特征缓存条目
        /// </summary>
        private class CharacteristicCache
        {
            public int Power;
            public int Toughness;
            public Dictionary<int, float> Cost;
            public List<ManaType> Colors;
        }

        /// <summary>
        /// 标记指定实体为脏（需要重算）
        /// </summary>
        private void MarkDirty(Entity entity)
        {
            if (entity == null)
            {
                _globalDirty = true;
                // 全局效果变更会使所有缓存失效
                _dirtyEntities.Clear();
                _cache.Clear();
            }
            else
            {
                _dirtyEntities.Add(entity);
                _cache.Remove(entity);
            }
        }

        /// <summary>
        /// 检查实体是否需要重算
        /// </summary>
        private bool IsDirty(Entity entity)
        {
            return _globalDirty || _dirtyEntities.Contains(entity);
        }

        /// <summary>
        /// 获取或创建实体缓存
        /// </summary>
        private CharacteristicCache GetOrCreateCache(Entity entity)
        {
            if (!_cache.TryGetValue(entity, out var cache))
            {
                cache = new CharacteristicCache();
                _cache[entity] = cache;
            }
            return cache;
        }

        /// <summary>
        /// 计算实体最终攻击力（带缓存）
        /// </summary>
        public int CalculatePower(Entity entity)
        {
            if (!IsDirty(entity) && _cache.TryGetValue(entity, out var cached))
                return cached.Power;

            int value = CalculatePowerInternal(entity);

            var cache = GetOrCreateCache(entity);
            cache.Power = value;

            // 如果这个特征已重算且全局不脏，从脏集合中移除
            if (!_globalDirty)
                _dirtyEntities.Remove(entity);

            return value;
        }

        /// <summary>
        /// 计算实体最终生命值（带缓存）
        /// </summary>
        public int CalculateToughness(Entity entity)
        {
            if (!IsDirty(entity) && _cache.TryGetValue(entity, out var cached))
                return cached.Toughness;

            int value = CalculateToughnessInternal(entity);

            var cache = GetOrCreateCache(entity);
            cache.Toughness = value;

            if (!_globalDirty)
                _dirtyEntities.Remove(entity);

            return value;
        }

        /// <summary>
        /// 计算实体最终费用（带缓存）
        /// </summary>
        public Dictionary<int, float> CalculateCost(Entity entity)
        {
            if (!IsDirty(entity) && _cache.TryGetValue(entity, out var cached) && cached.Cost != null)
                return cached.Cost;

            var value = CalculateCostInternal(entity);

            var cache = GetOrCreateCache(entity);
            cache.Cost = value;

            if (!_globalDirty)
                _dirtyEntities.Remove(entity);

            return value;
        }

        /// <summary>
        /// 计算实体最终颜色（带缓存）
        /// </summary>
        public List<ManaType> CalculateColors(Entity entity)
        {
            if (!IsDirty(entity) && _cache.TryGetValue(entity, out var cached) && cached.Colors != null)
                return cached.Colors;

            var value = CalculateColorsInternal(entity);

            var cache = GetOrCreateCache(entity);
            cache.Colors = value;

            if (!_globalDirty)
                _dirtyEntities.Remove(entity);

            return value;
        }

        /// <summary>
        /// 强制刷新所有缓存（当全局状态变更时调用）
        /// </summary>
        public void InvalidateAllCache()
        {
            _globalDirty = true;
            _dirtyEntities.Clear();
            _cache.Clear();
        }

        #region 内部计算方法（无缓存，由带缓存的公有方法调用）

        private int CalculatePowerInternal(Entity entity)
        {
            int baseValue = GetBasePower(entity);

            var allEffects = GetSortedEffectsForEntity(entity);
            foreach (var effect in allEffects)
            {
                if (effect.AffectsCharacteristic(CharacteristicType.Power))
                    baseValue = effect.ApplyIntModification(CharacteristicType.Power, baseValue);
            }

            var cda = GetHighestPriorityCDA(entity, CharacteristicType.Power);
            if (cda != null && cda.DefinedValue is int cdaValue)
                baseValue = cdaValue;

            return baseValue;
        }

        private int CalculateToughnessInternal(Entity entity)
        {
            int baseValue = GetBaseToughness(entity);

            var allEffects = GetSortedEffectsForEntity(entity);
            foreach (var effect in allEffects)
            {
                if (effect.AffectsCharacteristic(CharacteristicType.Toughness))
                    baseValue = effect.ApplyIntModification(CharacteristicType.Toughness, baseValue);
            }

            var cda = GetHighestPriorityCDA(entity, CharacteristicType.Toughness);
            if (cda != null && cda.DefinedValue is int cdaValue)
                baseValue = cdaValue;

            return baseValue;
        }

        private Dictionary<int, float> CalculateCostInternal(Entity entity)
        {
            var baseValue = GetBaseCost(entity);

            var allEffects = GetSortedEffectsForEntity(entity);
            foreach (var effect in allEffects)
            {
                if (effect.AffectsCharacteristic(CharacteristicType.Cost))
                    baseValue = effect.ApplyCostModification(baseValue);
            }

            return baseValue;
        }

        private List<ManaType> CalculateColorsInternal(Entity entity)
        {
            var result = GetBaseColors(entity);

            var allEffects = GetSortedEffectsForEntity(entity);
            foreach (var effect in allEffects)
            {
                if (effect.AffectsCharacteristic(CharacteristicType.Color))
                    result = effect.ApplyColorModification(result);
            }

            var cda = GetHighestPriorityCDA(entity, CharacteristicType.Color);
            if (cda != null && cda.DefinedValue is List<ManaType> cdaColors)
                result = cdaColors;

            return result;
        }

        #endregion

        /// <summary>
        /// 添加持续效果
        /// </summary>
        public void AddLayerContinuousEffect(LayerContinuousEffect effect)
        {
            if (effect.TargetEntity == null)
            {
                _globalEffects.Add(effect);
            }
            else
            {
                if (!_entityEffects.ContainsKey(effect.TargetEntity))
                    _entityEffects[effect.TargetEntity] = new List<LayerContinuousEffect>();

                _entityEffects[effect.TargetEntity].Add(effect);
            }

            MarkDirty(effect.TargetEntity);
            PublishStateChangeEvent(effect.TargetEntity, effect.Layer);
        }

        /// <summary>
        /// 移除持续效果
        /// </summary>
        public void RemoveLayerContinuousEffect(LayerContinuousEffect effect)
        {
            if (effect.TargetEntity == null)
            {
                _globalEffects.Remove(effect);
            }
            else if (_entityEffects.ContainsKey(effect.TargetEntity))
            {
                _entityEffects[effect.TargetEntity].Remove(effect);
            }

            MarkDirty(effect.TargetEntity);
            PublishStateChangeEvent(effect.TargetEntity, effect.Layer);
        }

        /// <summary>
        /// 添加特征定义能力
        /// </summary>
        public void AddCDA(CharacteristicDefiningAbility cda)
        {
            if (cda.DefinedEntity == null)
            {
                _globalCDAs.Add(cda);
            }
            else
            {
                if (!_entityCDAs.ContainsKey(cda.DefinedEntity))
                    _entityCDAs[cda.DefinedEntity] = new List<CharacteristicDefiningAbility>();

                _entityCDAs[cda.DefinedEntity].Add(cda);
            }

            MarkDirty(cda.DefinedEntity);
            PublishStateChangeEvent(cda.DefinedEntity, LayerType.Layer1);
        }

        /// <summary>
        /// 移除特征定义能力
        /// </summary>
        public void RemoveCDA(CharacteristicDefiningAbility cda)
        {
            if (cda.DefinedEntity == null)
            {
                _globalCDAs.Remove(cda);
            }
            else if (_entityCDAs.ContainsKey(cda.DefinedEntity))
            {
                _entityCDAs[cda.DefinedEntity].Remove(cda);
            }

            MarkDirty(cda.DefinedEntity);
            PublishStateChangeEvent(cda.DefinedEntity, LayerType.Layer1);
        }

        /// <summary>
        /// 获取实体的所有排序效果
        /// </summary>
        private List<LayerContinuousEffect> GetSortedEffectsForEntity(Entity entity)
        {
            var result = new List<LayerContinuousEffect>();

            // 添加全局效果
            result.AddRange(_globalEffects);

            // 添加目标效果
            if (_entityEffects.ContainsKey(entity))
                result.AddRange(_entityEffects[entity]);

            // 按层排序，然后按时间戳排序
            result.Sort((a, b) =>
            {
                int layerCompare = a.Layer.CompareTo(b.Layer);
                if (layerCompare != 0) return layerCompare;

                // 同层内按时间戳排序
                return a.SequenceNumber.CompareTo(b.SequenceNumber);
            });

            return result;
        }

        /// <summary>
        /// 获取最高优先级的CDA
        /// </summary>
        private CharacteristicDefiningAbility GetHighestPriorityCDA(Entity entity, CharacteristicType type)
        {
            List<CharacteristicDefiningAbility> candidates = new List<CharacteristicDefiningAbility>();

            // 添加全局CDA
            candidates.AddRange(_globalCDAs.Where(cda =>
                cda.DefinitionType == GetCDAFromCharacteristicType(type)));

            // 添加目标CDA
            if (_entityCDAs.ContainsKey(entity))
            {
                candidates.AddRange(_entityCDAs[entity].Where(cda =>
                    cda.DefinitionType == GetCDAFromCharacteristicType(type)));
            }

            // 如果没有CDA，返回null
            if (candidates.Count == 0)
                return null;

            // 按时间戳排序（更老的优先）
            candidates.Sort((a, b) => a.SequenceNumber.CompareTo(b.SequenceNumber));

            return candidates[0];
        }

        /// <summary>
        /// 获取基础攻击力
        /// </summary>
        private int GetBasePower(Entity entity)
        {
            // TODO: 从实体的基础属性获取
            if (entity is IHasPower hasPower)
                return hasPower.Power;
            return 0;
        }

        /// <summary>
        /// 获取基础生命值
        /// </summary>
        private int GetBaseToughness(Entity entity)
        {
            // TODO: 从实体的基础属性获取
            if (entity is IHasLife hasLife)
                return hasLife.Life;
            return 0;
        }

        /// <summary>
        /// 获取基础费用
        /// </summary>
        private Dictionary<int, float> GetBaseCost(Entity entity)
        {
            // TODO: 从实体的基础属性获取
            if (entity is IHasCost hasCost)
                return hasCost.Cost;
            return new Dictionary<int, float>();
        }

        /// <summary>
        /// 获取基础颜色
        /// </summary>
        private List<ManaType> GetBaseColors(Entity entity)
        {
            var result = new List<ManaType>();
            // TODO: 从实体的基础属性获取
            return result;
        }

        /// <summary>
        /// 将特征类型转换为CDA类型
        /// </summary>
        private CharacteristicDefiningType GetCDAFromCharacteristicType(CharacteristicType type)
        {
            switch (type)
            {
                case CharacteristicType.Color:
                    return CharacteristicDefiningType.DefinesColor;
                case CharacteristicType.Supertype:
                    return CharacteristicDefiningType.DefinesType;
                case CharacteristicType.Subtype:
                    return CharacteristicDefiningType.DefinesSubtype;
                case CharacteristicType.Toughness:
                    return CharacteristicDefiningType.DefinesStats;
                default:
                    return CharacteristicDefiningType.DefinesStats;
            }
        }

        /// <summary>
        /// 触发状态变化事件
        /// </summary>
        private void PublishStateChangeEvent(Entity entity, LayerType layer)
        {
            // 触发所有受影响特征的事件
            if (layer <= LayerType.Layer2) // CDA和内部效果优先级高
            {
                // 高优先级变化
            }
            else
            {
                // 低优先级变化
            }

            // 通过事件总线发布状态变化事件
            EventManager.Instance.Publish(new StateChangeEvent
            {
                Type = StateChangeType.Power, // TODO: 根据实际变化类型设置
                Target = entity,
                OldValue = null, // TODO: 获取旧值
                NewValue = null // TODO: 获取新值
            });
        }

        /// <summary>
        /// 重新计算所有实体状态
        /// </summary>
        public void RecalculateAll()
        {
            // 重新计算所有受影响实体的特征
            var allEntities = new HashSet<Entity>();

            // 收集所有受影响的实体
            foreach (var entity in _entityEffects.Keys)
                allEntities.Add(entity);
            foreach (var entity in _entityCDAs.Keys)
                allEntities.Add(entity);

            // 重新计算每个实体
            foreach (var entity in allEntities)
            {
                RecalculateEntity(entity);
            }
        }

        /// <summary>
        /// 重新计算指定实体的状态
        /// </summary>
        private void RecalculateEntity(Entity entity)
        {
            // 重新计算攻击力
            if (entity is IHasPower hasPower)
                hasPower.Power = CalculatePower(entity);

            // 重新计算生命值
            if (entity is IHasLife hasLife)
                hasLife.Life = CalculateToughness(entity);

            // 重新计算费用
            if (entity is IHasCost hasCost)
                hasCost.Cost = CalculateCost(entity);

            // TODO: 重新计算其他特征
        }

        /// <summary>
        /// 检查是否有指定类型的持续效果
        /// </summary>
        public bool HasEffectType(Effect effectType)
        {
            return _globalEffects.Any(e => e.SourceEffect == effectType) ||
                   _entityEffects.Values.Any(list => list.Any(e => e.SourceEffect == effectType));
        }

        /// <summary>
        /// 清空所有效果
        /// </summary>
        public void ClearAll()
        {
            _entityEffects.Clear();
            _entityCDAs.Clear();
            _globalEffects.Clear();
            _globalCDAs.Clear();
            InvalidateAllCache();
        }

        /// <summary>
        /// 处理游戏事件
        /// </summary>
        public void OnEvent<T>(T e) where T : IGameEvent
        {
            // 根据事件类型重新计算状态
            switch (e)
            {
                case StateChangeEvent stateChange:
                    RecalculateEntity(stateChange.Target);
                    break;
                case EffectResolveEvent resolveEvent:
                    // 效果结算后重新计算
                    RecalculateAll();
                    break;
            }
        }
    }
}
