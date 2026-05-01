using CardCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    #region 栈对象接口

    /// <summary>
    /// 栈对象接口
    /// </summary>
    public interface IStackObject
    {
        StackObjectType Type { get; }
        int BaseSpeed { get; }
        bool IsCounter { get; }
        Effect SourceEffect { get; }
    }

    public enum StackObjectType
    {
        ActivatedAbility,
        TriggeredAbility,
        Spell,
        Effect,
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
        public string Id;
        public string DisplayName;
        public string Description;
        #endregion

        #region 速度系统
        /// <summary>
        /// 卡牌速度加成（编辑阶段设置，默认0）
        /// 最终速度 = 默认(阶段+回合归属) + BaseSpeed + 动态支付
        /// </summary>
        public int BaseSpeed = 0;

        /// <summary>发动类型</summary>
        public EffectActivationType ActivationType = EffectActivationType.Voluntary;
        #endregion

        #region 触发相关
        public TriggerTiming TriggerTiming;
        public List<ActivationCondition> TriggerConditions = new List<ActivationCondition>();
        #endregion

        #region 发动条件
        public List<ActivationCondition> ActivationConditions = new List<ActivationCondition>();
        #endregion

        #region 元数据
        public bool IsOptional = false;
        public DurationType Duration = DurationType.Permanent;
        public List<AtomicEffectInstance> Effects = new List<AtomicEffectInstance>();
        public List<CostInstance> Costs = new List<CostInstance>();
        public List<string> Tags = new List<string>();
        public string SourceCardId;
        public EffectTargetType TargetType;
        #endregion

        #region 方法

        /// <summary>
        /// 计算实际发动速度
        /// 三层叠加: 默认(阶段+回合归属) + BaseSpeed + 动态支付
        /// </summary>
        public int CalculateActivationSpeed(Player activator, Player activePlayer, PhaseType phase, int paidBoost = 0)
        {
            int defaultSpeed = SpeedCalculator.GetDefaultSpeed(activator, activePlayer, phase);
            return SpeedCalculator.CalculateSpeed(defaultSpeed, BaseSpeed, paidBoost);
        }

        public string GetFullDescription()
        {
            var parts = new List<string>();
            if (IsTriggeredEffect)
                parts.Add($"[{GetTriggerDescription()}]");
            if (ActivationType == EffectActivationType.Mandatory)
                parts.Add("[强制]");
            return string.Join("：", parts);
        }

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

        public bool IsTriggeredEffect =>
            TriggerTiming != TriggerTiming.Activate_Active &&
            TriggerTiming != TriggerTiming.Activate_Instant &&
            TriggerTiming != TriggerTiming.Activate_Response;

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
    /// </summary>
    public class PendingEffect : ITimestamped
    {
        public EffectDefinition Effect { get; set; }
        public Entity Source { get; set; }
        public Player Controller { get; set; }
        public int ActivationSpeed { get; set; }
        public EffectActivationType ActivationType { get; set; }
        public TimestampInfo TimestampInfo { get; set; }
        public DateTime CreationTime => TimestampInfo.DateTime;
        public uint SequenceNumber => TimestampInfo.Sequence;
        public IGameEvent TriggeringEvent { get; set; }
        public List<Entity> SelectedTargets { get; set; } = new List<Entity>();
        public int PaidSpeedBoost { get; set; }
        public bool IsOnStack { get; set; }

        /// <summary>
        /// 创建待发效果
        /// 条件发动 speed=Max 绕过速度检查
        /// 速度发动 三层叠加: 默认 + BaseSpeed + paidBoost
        /// </summary>
        public static PendingEffect Create(
            EffectDefinition effect,
            Entity source,
            Player controller,
            Player activePlayer,
            PhaseType currentPhase,
            int paidBoost = 0,
            IGameEvent triggeringEvent = null)
        {
            int speed = (effect.ActivationType != EffectActivationType.Voluntary)
                ? int.MaxValue
                : SpeedCalculator.CalculateSpeed(
                    SpeedCalculator.GetDefaultSpeed(controller, activePlayer, currentPhase),
                    effect.BaseSpeed,
                    paidBoost);

            return new PendingEffect
            {
                Effect = effect,
                Source = source,
                Controller = controller,
                ActivationSpeed = speed,
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
        /// 条件发动不检查速度直接入队
        /// </summary>
        public void AddPendingEffect(PendingEffect effect)
        {
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
        /// 获取下一个要入栈的条件发动效果（仅强制/自动）
        /// </summary>
        public PendingEffect GetNextEffect()
        {
            if (_mandatoryEffects.Count > 0)
                return PopHighestSpeed(_mandatoryEffects);
            if (_automaticEffects.Count > 0)
                return PopHighestSpeed(_automaticEffects);
            return null;
        }

        /// <summary>
        /// 获取可发动的速度效果列表（speed > counter）
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
        /// 玩家选择发动效果
        /// </summary>
        public PendingEffect PlayerChooseEffect(PendingEffect effect)
        {
            if (_voluntaryEffects.Remove(effect))
                return effect;
            return null;
        }

        public void RemoveEffect(PendingEffect effect)
        {
            _mandatoryEffects.Remove(effect);
            _automaticEffects.Remove(effect);
            _voluntaryEffects.Remove(effect);
        }

        public void Clear()
        {
            _mandatoryEffects.Clear();
            _automaticEffects.Clear();
            _voluntaryEffects.Clear();
        }

        public bool HasPendingEffects =>
            _mandatoryEffects.Count > 0 ||
            _automaticEffects.Count > 0 ||
            _voluntaryEffects.Count > 0;

        public bool HasAutoEffects =>
            _mandatoryEffects.Count > 0 ||
            _automaticEffects.Count > 0;

        private PendingEffect PopHighestSpeed(List<PendingEffect> list)
        {
            if (list.Count == 0) return null;

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

    #region 触发时点映射

    /// <summary>
    /// 触发时点默认配置
    /// </summary>
    public static class TriggerTimingDefaults
    {
        public static EffectActivationType GetDefaultActivationType(TriggerTiming timing)
        {
            return timing switch
            {
                TriggerTiming.Activate_Active => EffectActivationType.Voluntary,
                TriggerTiming.Activate_Instant => EffectActivationType.Voluntary,
                TriggerTiming.Activate_Response => EffectActivationType.Voluntary,
                _ => EffectActivationType.Automatic
            };
        }

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

    [Serializable]
    public class AtomicEffectInstance
    {
        public AtomicEffectType Type;
        public int Value;
        public int Value2;
        public string StringValue;
        public ManaType ManaTypeParam;
        public Zone ZoneParam;
        public DurationType Duration;

        public string GetDescription()
        {
            return AtomicEffectTypeExtensions.GetEffectDescription(Type, Value);
        }
    }

    #endregion
}
