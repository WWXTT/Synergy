using CardCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    #region 栈对象接口

    /// <summary>
    /// 栈对象接口
    /// 所有可以进入栈的对象都必须实现此接口
    /// </summary>
    public interface IStackObject
    {
        /// <summary>栈对象类型</summary>
        StackObjectType Type { get; }

        /// <summary>基础速度</summary>
        int BaseSpeed { get; }

        /// <summary>是否为响应���果</summary>
        bool IsCounter { get; }

        /// <summary>来源效果（可选）</summary>
        Effect SourceEffect { get; }
    }

    /// <summary>
    /// 栈对象类型
    /// </summary>
    public enum StackObjectType
    {
        /// <summary>激活式能力</summary>
        ActivatedAbility,
        /// <summary>触发式能力</summary>
        TriggeredAbility,
        /// <summary>法术</summary>
        Spell,
        /// <summary>效果</summary>
        Effect,
        /// <summary>攻击宣言</summary>
        AttackDeclaration
    }

    #endregion

    #region 效果定义

    /// <summary>
    /// 效果定义
    /// 完整的效果配置，可在卡牌构建界面配置
    /// </summary>
    [Serializable]
    public partial class EffectDefinition
    {
        #region 基本信息
        /// <summary>效果唯一ID</summary>
        public string Id;

        /// <summary>显示名称</summary>
        public string DisplayName;

        /// <summary>效果描述</summary>
        public string Description;
        #endregion

        #region 速度系统
        /// <summary>基础速度（从0开始）</summary>
        public int BaseSpeed = 0;

        /// <summary>发动类型（强制/自动/自由）</summary>
        public EffectActivationType ActivationType = EffectActivationType.Voluntary;
        #endregion

        #region 触发相关
        /// <summary>触发时点</summary>
        public TriggerTiming TriggerTiming;

        /// <summary>触发条件（用于触发式效果）</summary>
        public List<ActivationCondition> TriggerConditions = new List<ActivationCondition>();
        #endregion

        #region 发动条件
        /// <summary>发动条件列表（必须满足才能发动）</summary>
        public List<ActivationCondition> ActivationConditions = new List<ActivationCondition>();
        #endregion

        #region 元数据
        /// <summary>是否可选（对触发式效果）</summary>
        public bool IsOptional = false;

        /// <summary>持续时间类型</summary>
        public DurationType Duration = DurationType.Permanent;

        /// <summary>原子效果列表</summary>
        public List<AtomicEffectInstance> Effects = new List<AtomicEffectInstance>();

        /// <summary>代价列表（发动前需全部支付）</summary>
        public List<CostInstance> Costs = new List<CostInstance>();

        /// <summary>效果标签（用于效果分类）</summary>
        public List<string> Tags = new List<string>();

        /// <summary>关联卡牌ID</summary>
        public string SourceCardId;

        /// <summary>目标可选范围</summary>
        public EffectTargetType TargetType;
        #endregion

        #region 方法

        /// <summary>
        /// 计算实际发动速度
        /// </summary>
        public int CalculateActivationSpeed(Player activator, Player activePlayer, int paidBoost = 0)
        {
            return SpeedCalculator.CalculateActivationSpeed(BaseSpeed, activator, activePlayer, paidBoost);
        }

        /// <summary>
        /// 获取完整描述
        /// </summary>
        public string GetFullDescription()
        {
            var parts = new List<string>();

            // 触发时点
            if (TriggerTiming != TriggerTiming.Activate_Active &&
                TriggerTiming != TriggerTiming.Activate_Instant)
            {
                parts.Add($"[{GetTriggerDescription()}]");
            }

            // 发动类型
            if (ActivationType == EffectActivationType.Mandatory)
                parts.Add("[强制]");


            return string.Join("：", parts);
        }

        /// <summary>
        /// 获取触发时点描述
        /// </summary>
        private string GetTriggerDescription()
        {
            return TriggerTiming switch
            {
                TriggerTiming.Activate_Instant => "瞬间发动",
                TriggerTiming.Activate_Response => "响应发动",
                TriggerTiming.On_EnterBattlefield => "入场时",
                TriggerTiming.On_LeaveBattlefield => "离场时",
                TriggerTiming.On_Death => "死亡时",
                TriggerTiming.On_TurnStart => "回合开始时",
                TriggerTiming.On_TurnEnd => "回合结束时",
                TriggerTiming.On_PhaseStart => "阶段开始时",
                TriggerTiming.On_PhaseEnd => "阶段结束时",
                TriggerTiming.On_AttackDeclare => "攻击宣言时",
                TriggerTiming.On_BlockDeclare => "阻拦时",
                TriggerTiming.On_DamageDealt => "造成伤害时",
                TriggerTiming.On_DamageTaken => "受到伤害时",
                TriggerTiming.On_CardDraw => "抽卡时",
                TriggerTiming.On_CardPlay => "使用卡牌时",
                TriggerTiming.On_AtomicEffectActivation => "原子效果发动时",
                TriggerTiming.On_AtomicEffectStartApplying => "原子效果开始作用时",
                TriggerTiming.On_AtomicEffectResolution => "原子效果结算完成时",
                _ => TriggerTiming.ToString()
            };
        }

        /// <summary>
        /// 检查是否为触发式效果
        /// </summary>
        public bool IsTriggeredEffect =>
            TriggerTiming != TriggerTiming.Activate_Active &&
            TriggerTiming != TriggerTiming.Activate_Instant &&
            TriggerTiming != TriggerTiming.Activate_Response;

        /// <summary>
        /// 检查是否为主动式效果
        /// </summary>
        public bool IsActivatedEffect =>
            TriggerTiming == TriggerTiming.Activate_Active ||
            TriggerTiming == TriggerTiming.Activate_Instant ||
            TriggerTiming == TriggerTiming.Activate_Response;

        #endregion
    }

    #endregion

    #region 待发效果

    /// <summary>
    /// 待发效果
    /// 表示一个等待入栈的效果实例
    /// </summary>
    public class PendingEffect : ITimestamped
    {
        /// <summary>效果定义</summary>
        public EffectDefinition Effect { get; set; }

        /// <summary>来源实体（发动效果的卡牌）</summary>
        public Entity Source { get; set; }

        /// <summary>控制者</summary>
        public Player Controller { get; set; }

        /// <summary>发动速度（基础+加成+付费提升）</summary>
        public int ActivationSpeed { get; set; }

        /// <summary>发动类型</summary>
        public EffectActivationType ActivationType { get; set; }

        /// <summary>时间戳信息</summary>
        public TimestampInfo TimestampInfo { get; set; }

        /// <summary>创建时间</summary>
        public DateTime CreationTime => TimestampInfo.DateTime;

        /// <summary>序列号</summary>
        public uint SequenceNumber => TimestampInfo.Sequence;

        /// <summary>触发事件（如果是触发式）</summary>
        public IGameEvent TriggeringEvent { get; set; }

        /// <summary>选中的目标</summary>
        public List<Entity> SelectedTargets { get; set; } = new List<Entity>();

        /// <summary>支付的速度提升值</summary>
        public int PaidSpeedBoost { get; set; }

        /// <summary>是否已在栈中</summary>
        public bool IsOnStack { get; set; }

        /// <summary>
        /// 创建待发效果
        /// </summary>
        public static PendingEffect Create(
            EffectDefinition effect,
            Entity source,
            Player controller,
            Player activePlayer,
            int paidBoost = 0,
            IGameEvent triggeringEvent = null)
        {
            var activationSpeed = effect.CalculateActivationSpeed(controller, activePlayer, paidBoost);

            return new PendingEffect
            {
                Effect = effect,
                Source = source,
                Controller = controller,
                ActivationSpeed = activationSpeed,
                ActivationType = effect.ActivationType,
                TimestampInfo = TimestampSystem.CreateTimestamp(),
                TriggeringEvent = triggeringEvent,
                PaidSpeedBoost = paidBoost,
                IsOnStack = false
            };
        }
    }

    #endregion

    #region 待发效果队列

    /// <summary>
    /// 待发效果队列
    /// 管理等待入栈的效果
    /// </summary>
    public class PendingEffectQueue
    {
        private List<PendingEffect> _mandatoryEffects = new List<PendingEffect>();
        private List<PendingEffect> _automaticEffects = new List<PendingEffect>();
        private List<PendingEffect> _voluntaryEffects = new List<PendingEffect>();

        private SpeedCounter _speedCounter;

        public PendingEffectQueue(SpeedCounter speedCounter)
        {
            _speedCounter = speedCounter;
        }

        /// <summary>
        /// 添加待发效果
        /// </summary>
        public void AddPendingEffect(PendingEffect effect)
        {
            // 速度检查
            if (!_speedCounter.CanActivate(effect.ActivationSpeed, effect.ActivationType))
                return;

            switch (effect.ActivationType)
            {
                case EffectActivationType.Mandatory:
                    _mandatoryEffects.Add(effect);
                    break;
                case EffectActivationType.Automatic:
                    _automaticEffects.Add(effect);
                    break;
                case EffectActivationType.Voluntary:
                    _voluntaryEffects.Add(effect);
                    break;
            }
        }

        /// <summary>
        /// 获取下一个要入栈的效果（按优先级和时间戳）
        /// </summary>
        public PendingEffect GetNextEffect()
        {
            // 1. 优先处理强制发动
            if (_mandatoryEffects.Count > 0)
            {
                return PopHighestSpeed(_mandatoryEffects);
            }

            // 2. 其次处理自动发动
            if (_automaticEffects.Count > 0)
            {
                return PopHighestSpeed(_automaticEffects);
            }

            // 3. 自由发动需要玩家选择
            return null;
        }

        /// <summary>
        /// 获取等待玩家选择的效果
        /// </summary>
        public List<PendingEffect> GetVoluntaryEffects()
        {
            return _voluntaryEffects
                .Where(e => e.ActivationSpeed > _speedCounter.CurrentSpeed)
                .OrderByDescending(e => e.ActivationSpeed)
                .ThenBy(e => e.SequenceNumber)
                .ToList();
        }

        /// <summary>
        /// 获取可发动的强制/自动效果（结算中检查用）
        /// </summary>
        public List<PendingEffect> GetAutoActivatableEffects(int currentSpeed)
        {
            var mandatory = _mandatoryEffects
                .Where(e => e.ActivationSpeed > currentSpeed)
                .OrderByDescending(e => e.ActivationSpeed)
                .ThenBy(e => e.SequenceNumber);

            var automatic = _automaticEffects
                .Where(e => e.ActivationSpeed > currentSpeed)
                .OrderByDescending(e => e.ActivationSpeed)
                .ThenBy(e => e.SequenceNumber);

            return mandatory.Concat(automatic).ToList();
        }

        /// <summary>
        /// 玩家选择发动自由效果
        /// </summary>
        public PendingEffect PlayerChooseEffect(PendingEffect effect)
        {
            if (_voluntaryEffects.Remove(effect))
                return effect;
            return null;
        }

        /// <summary>
        /// 移除待发效果
        /// </summary>
        public void RemoveEffect(PendingEffect effect)
        {
            _mandatoryEffects.Remove(effect);
            _automaticEffects.Remove(effect);
            _voluntaryEffects.Remove(effect);
        }

        /// <summary>
        /// 清空所有待发效果
        /// </summary>
        public void Clear()
        {
            _mandatoryEffects.Clear();
            _automaticEffects.Clear();
            _voluntaryEffects.Clear();
        }

        /// <summary>
        /// 是否有待发效果
        /// </summary>
        public bool HasPendingEffects =>
            _mandatoryEffects.Count > 0 ||
            _automaticEffects.Count > 0 ||
            _voluntaryEffects.Count > 0;

        /// <summary>
        /// 是否有强制/自动效果
        /// </summary>
        public bool HasAutoEffects =>
            _mandatoryEffects.Count > 0 ||
            _automaticEffects.Count > 0;

        /// <summary>
        /// 弹出最高速度的效果（相同速度按时间戳排序）
        /// </summary>
        private PendingEffect PopHighestSpeed(List<PendingEffect> list)
        {
            if (list.Count == 0) return null;

            // 按速度降序，时间戳升序排序
            var sorted = list
                .OrderByDescending(e => e.ActivationSpeed)
                .ThenBy(e => e.TimestampInfo.Sequence)
                .ToList();

            var result = sorted.First();
            list.Remove(result);
            return result;
        }
    }

    #endregion

    #region 触发时点默认速度

    /// <summary>
    /// 触发时点默认速度
    /// </summary>
    public static class TriggerTimingDefaults
    {
        /// <summary>
        /// 获取触发时点的默认速度
        /// </summary>
        public static int GetDefaultSpeed(TriggerTiming timing)
        {
            return timing switch
            {
                TriggerTiming.Activate_Active => SpeedLevel.Base,        // 0
                TriggerTiming.Activate_Instant => SpeedLevel.Normal,     // 1
                TriggerTiming.Activate_Response => SpeedLevel.Quick,     // 2
                TriggerTiming.On_AtomicEffectActivation => SpeedLevel.Normal,     // 1
                TriggerTiming.On_AtomicEffectStartApplying => SpeedLevel.Normal,  // 1
                TriggerTiming.On_AtomicEffectResolution => SpeedLevel.Normal,     // 1
                _ => SpeedLevel.Normal                                     // 触发式默认1
            };
        }

        /// <summary>
        /// 获取触发时点的默认发动类型
        /// </summary>
        public static EffectActivationType GetDefaultActivationType(TriggerTiming timing)
        {
            return timing switch
            {
                TriggerTiming.Activate_Active => EffectActivationType.Voluntary,
                TriggerTiming.Activate_Instant => EffectActivationType.Voluntary,
                TriggerTiming.Activate_Response => EffectActivationType.Voluntary,
                _ => EffectActivationType.Automatic  // 触发式默认自动
            };
        }

        /// <summary>
        /// 获取触发时点对应的事件类型
        /// </summary>
        public static Type GetEventType(TriggerTiming timing)
        {
            return timing switch
            {
                TriggerTiming.On_EnterBattlefield => typeof(CardPutToBattlefieldEvent),
                TriggerTiming.On_LeaveBattlefield => typeof(CardLeaveBattlefieldEvent),
                TriggerTiming.On_Death => typeof(CardDestroyEvent),
                TriggerTiming.On_TurnStart => typeof(TurnStartEvent),
                TriggerTiming.On_TurnEnd => typeof(TurnEndEvent),
                TriggerTiming.On_PhaseStart => typeof(PhaseStartEvent),
                TriggerTiming.On_PhaseEnd => typeof(PhaseEndEvent),
                TriggerTiming.On_AttackDeclare => typeof(AttackDeclarationEvent),
                TriggerTiming.On_DamageDealt => typeof(DamageEvent),
                TriggerTiming.On_DamageTaken => typeof(DamageEvent),
                TriggerTiming.On_CardDraw => typeof(CardDrawEvent),
                TriggerTiming.On_CardPlay => typeof(CardPlayEvent),
                TriggerTiming.On_GameStart => typeof(GameStartEvent),
                TriggerTiming.On_AtomicEffectActivation => typeof(AtomicEffectPhaseEvent),
                TriggerTiming.On_AtomicEffectStartApplying => typeof(AtomicEffectPhaseEvent),
                TriggerTiming.On_AtomicEffectResolution => typeof(AtomicEffectPhaseEvent),
                _ => null
            };
        }
    }

    #endregion

    #region 原子效果实例

    /// <summary>
    /// 原子效果实例
    /// 表示效果定义中一个具体的原子效果
    /// </summary>
    [Serializable]
    public class AtomicEffectInstance
    {
        /// <summary>效果类型</summary>
        public AtomicEffectType Type;

        /// <summary>数值参数</summary>
        public int Value;

        /// <summary>数值参数2</summary>
        public int Value2;

        /// <summary>字符串参数</summary>
        public string StringValue;

        /// <summary>法力类型参数</summary>
        public ManaType ManaTypeParam;

        /// <summary>区域参数</summary>
        public Zone ZoneParam;

        /// <summary>持续时间</summary>
        public DurationType Duration;

        /// <summary>
        /// 获取效果描述
        /// </summary>
        public string GetDescription()
        {
            return AtomicEffectTypeExtensions.GetEffectDescription(Type, Value);
        }
    }

    #endregion
}
