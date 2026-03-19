using System;
using System.Collections.Generic;
using cfg;
using CardCore.Data;

namespace CardCore
{
    /// <summary>
    /// 效果构建器
    /// 提供流式API构建效果定义
    /// </summary>
    public partial class EffectBuilder
    {
        private EffectDefinition _definition = new EffectDefinition();

        #region 基本信息设置

        /// <summary>
        /// 设置效果ID
        /// </summary>
        public EffectBuilder WithId(string id)
        {
            _definition.Id = id;
            return this;
        }
        #endregion

        #region 速度设置

        /// <summary>
        /// 设置基础速度
        /// </summary>
        public EffectBuilder WithSpeed(int speed)
        {
            _definition.BaseSpeed = speed;
            return this;
        }

        /// <summary>
        /// 设置为主动发动效果（主要阶段）
        /// </summary>
        public EffectBuilder AsMainPhaseAction()
        {
            _definition.BaseSpeed = SpeedLevel.Base;
            _definition.TriggerTiming = TriggerTiming.Activate_Active;
            _definition.ActivationType = EffectActivationType.Voluntary;
            return this;
        }

        /// <summary>
        /// 设置为瞬间效果（任意时点）
        /// </summary>
        public EffectBuilder AsInstantAction()
        {
            _definition.BaseSpeed = SpeedLevel.Normal;
            _definition.TriggerTiming = TriggerTiming.Activate_Instant;
            _definition.ActivationType = EffectActivationType.Voluntary;
            return this;
        }

        /// <summary>
        /// 设置为响应效果
        /// </summary>
        public EffectBuilder AsResponseAction()
        {
            _definition.BaseSpeed = SpeedLevel.Quick;
            _definition.TriggerTiming = TriggerTiming.Activate_Response;
            _definition.ActivationType = EffectActivationType.Voluntary;
            return this;
        }

        /// <summary>
        /// 设置为强制发动
        /// </summary>
        public EffectBuilder AsMandatory()
        {
            _definition.ActivationType = EffectActivationType.Mandatory;
            return this;
        }

        /// <summary>
        /// 设置为自动发动
        /// </summary>
        public EffectBuilder AsAutomatic()
        {
            _definition.ActivationType = EffectActivationType.Automatic;
            return this;
        }

        /// <summary>
        /// 设置为自由发动（玩家选择）
        /// </summary>
        public EffectBuilder AsVoluntary()
        {
            _definition.ActivationType = EffectActivationType.Voluntary;
            return this;
        }

        #endregion

        #region 触发时点设置

        /// <summary>
        /// 设置触发时点
        /// </summary>
        public EffectBuilder TriggerOn(TriggerTiming timing)
        {
            _definition.TriggerTiming = timing;
            _definition.BaseSpeed = TriggerTimingDefaults.GetDefaultSpeed(timing);
            _definition.ActivationType = TriggerTimingDefaults.GetDefaultActivationType(timing);
            return this;
        }

        /// <summary>
        /// 入场触发
        /// </summary>
        public EffectBuilder TriggerOnEnterBattlefield()
        {
            return TriggerOn(TriggerTiming.On_EnterBattlefield);
        }

        /// <summary>
        /// 离场触发
        /// </summary>
        public EffectBuilder TriggerOnLeaveBattlefield()
        {
            return TriggerOn(TriggerTiming.On_LeaveBattlefield);
        }

        /// <summary>
        /// 死亡触发
        /// </summary>
        public EffectBuilder TriggerOnDeath()
        {
            return TriggerOn(TriggerTiming.On_Death);
        }

        /// <summary>
        /// 回合开始触发
        /// </summary>
        public EffectBuilder TriggerOnTurnStart()
        {
            return TriggerOn(TriggerTiming.On_TurnStart);
        }

        /// <summary>
        /// 回合结束触发
        /// </summary>
        public EffectBuilder TriggerOnTurnEnd()
        {
            return TriggerOn(TriggerTiming.On_TurnEnd);
        }

        /// <summary>
        /// 造成伤害时触发
        /// </summary>
        public EffectBuilder TriggerOnDamageDealt()
        {
            return TriggerOn(TriggerTiming.On_DamageDealt);
        }

        /// <summary>
        /// 受到伤害时触发
        /// </summary>
        public EffectBuilder TriggerOnDamageTaken()
        {
            return TriggerOn(TriggerTiming.On_DamageTaken);
        }

        /// <summary>
        /// 攻击宣言时触发
        /// </summary>
        public EffectBuilder TriggerOnAttackDeclare()
        {
            return TriggerOn(TriggerTiming.On_AttackDeclare);
        }

        /// <summary>
        /// 抽卡时触发
        /// </summary>
        public EffectBuilder TriggerOnCardDraw()
        {
            return TriggerOn(TriggerTiming.On_CardDraw);
        }

        /// <summary>
        /// 使用卡牌时触发
        /// </summary>
        public EffectBuilder TriggerOnCardPlay()
        {
            return TriggerOn(TriggerTiming.On_CardPlay);
        }

        #endregion

        #region 触发条件设置

        /// <summary>
        /// 添加触发条件
        /// </summary>
        public EffectBuilder AddTriggerCondition(ActivationCondition condition)
        {
            _definition.TriggerConditions.Add(condition);
            return this;
        }

        #endregion

        #region 发动条件设置

        /// <summary>
        /// 添加发动条件
        /// </summary>
        public EffectBuilder RequireCondition(ActivationCondition condition)
        {
            _definition.ActivationConditions.Add(condition);
            return this;
        }

        /// <summary>
        /// 要求手牌数量下限
        /// </summary>
        public EffectBuilder RequireMinHandCards(int min)
        {
            _definition.ActivationConditions.Add(ActivationCondition.MinHand(min));
            return this;
        }

        /// <summary>
        /// 要求场上数量下限
        /// </summary>
        public EffectBuilder RequireMinFieldCards(int min)
        {
            _definition.ActivationConditions.Add(ActivationCondition.MinField(min));
            return this;
        }

        /// <summary>
        /// 要求生命值下限
        /// </summary>
        public EffectBuilder RequireMinLife(int min)
        {
            _definition.ActivationConditions.Add(ActivationCondition.ControllerLife(min));
            return this;
        }

        /// <summary>
        /// 要求每回合一次
        /// </summary>
        public EffectBuilder RequireOncePerTurn()
        {
            _definition.ActivationConditions.Add(ActivationCondition.OncePerTurn());
            return this;
        }

        /// <summary>
        /// 要求仅主要阶段
        /// </summary>
        public EffectBuilder RequireMainPhaseOnly()
        {
            _definition.ActivationConditions.Add(ActivationCondition.MainPhaseOnly());
            return this;
        }

        /// <summary>
        /// 要求仅自己回合
        /// </summary>
        public EffectBuilder RequireOwnTurnOnly()
        {
            _definition.ActivationConditions.Add(ActivationCondition.OwnTurnOnly());
            return this;
        }

        /// <summary>
        /// 要求卡牌未横置
        /// </summary>
        public EffectBuilder RequireUntapped()
        {
            _definition.ActivationConditions.Add(ActivationCondition.IsUntapped());
            return this;
        }

        /// <summary>
        /// 要求场上存在特定类型
        /// </summary>
        public EffectBuilder RequireFieldHasType(CardType cardType)
        {
            _definition.ActivationConditions.Add(ActivationCondition.FieldHasType(cardType));
            return this;
        }

        #endregion

        #region 代价设置

        /// <summary>
        /// 设置完整代价
        /// </summary>
        public EffectBuilder WithCost(ActivationCost cost)
        {
            _definition.Cost = cost;
            return this;
        }

        /// <summary>
        /// 消耗指定类型元素
        /// </summary>
        public EffectBuilder CostElement(ManaType type, int amount)
        {
            _definition.Cost.ElementCosts.Add(new ElementCost
            {
                ManaType = type,
                Amount = amount
            });
            return this;
        }

        /// <summary>
        /// 消耗任意元素
        /// </summary>
        public EffectBuilder CostAnyElement(int amount)
        {
            _definition.Cost.ElementCosts.Add(new ElementCost
            {
                ManaType = ManaType.灰色,
                Amount = amount
            });
            return this;
        }

        /// <summary>
        /// 弃牌代价
        /// </summary>
        public EffectBuilder CostDiscard(int count = 1)
        {
            _definition.Cost.ResourceCosts.Add(CommonResourceCosts.Discard(count));
            return this;
        }

        /// <summary>
        /// 手牌除外代价
        /// </summary>
        public EffectBuilder CostExileFromHand(int count = 1)
        {
            _definition.Cost.ResourceCosts.Add(CommonResourceCosts.ExileFromHand(count));
            return this;
        }

        /// <summary>
        /// 祭祀代价
        /// </summary>
        public EffectBuilder CostSacrifice(int count = 1, TargetFilter filter = null)
        {
            var cost = CommonResourceCosts.Sacrifice(count);
            cost.Filter = filter;
            _definition.Cost.ResourceCosts.Add(cost);
            return this;
        }

        /// <summary>
        /// 场上除外代价
        /// </summary>
        public EffectBuilder CostExileFromField(int count = 1)
        {
            _definition.Cost.ResourceCosts.Add(CommonResourceCosts.ExileFromField(count));
            return this;
        }

        /// <summary>
        /// 墓地除外代价
        /// </summary>
        public EffectBuilder CostExileFromGraveyard(int count = 1)
        {
            _definition.Cost.ResourceCosts.Add(CommonResourceCosts.ExileFromGraveyard(count));
            return this;
        }

        /// <summary>
        /// 预支抽卡
        /// </summary>
        public EffectBuilder CostPrepayDraw(int count = 1)
        {
            _definition.Cost.PrepayCosts.Add(new PrepayCost
            {
                PrepayType = PrepayType.DrawCard,
                Amount = count
            });
            return this;
        }

        /// <summary>
        /// 预支元素产出
        /// </summary>
        public EffectBuilder CostPrepayElement(int count = 1)
        {
            _definition.Cost.PrepayCosts.Add(new PrepayCost
            {
                PrepayType = PrepayType.ElementProduction,
                Amount = count
            });
            return this;
        }

        /// <summary>
        /// 支付代价提升速度
        /// </summary>
        public EffectBuilder BoostSpeed(int amount, ActivationCost boostCost)
        {
            _definition.Cost.SpeedBoosts.Add(new SpeedBoostPayment
            {
                SpeedIncrease = amount,
                ActualCost = boostCost
            });
            return this;
        }

        #endregion

        #region 目标设置

        /// <summary>
        /// 设置目标选择器
        /// </summary>
        public EffectBuilder Target(TargetSelector selector)
        {
            _definition.TargetSelector = selector;
            return this;
        }

        /// <summary>
        /// 目标：自己
        /// </summary>
        public EffectBuilder TargetSelf()
        {
            _definition.TargetSelector = TargetSelector.Self();
            return this;
        }

        /// <summary>
        /// 目标：对手
        /// </summary>
        public EffectBuilder TargetOpponent()
        {
            _definition.TargetSelector = TargetSelector.Opponent();
            return this;
        }

        /// <summary>
        /// 目标：任意玩家
        /// </summary>
        public EffectBuilder TargetAnyPlayer()
        {
            _definition.TargetSelector = TargetSelector.AnyPlayer();
            return this;
        }

        /// <summary>
        /// 目标：指定卡牌
        /// </summary>
        public EffectBuilder TargetCard(TargetFilter filter = null, int min = 1, int max = 1)
        {
            _definition.TargetSelector = TargetSelector.TargetCard(filter, min, max);
            return this;
        }

        /// <summary>
        /// 目标：场上单位
        /// </summary>
        public EffectBuilder TargetBattlefieldUnit(TargetFilter filter = null)
        {
            _definition.TargetSelector = TargetSelector.BattlefieldUnit(filter);
            return this;
        }

        /// <summary>
        /// 目标：敌方单位
        /// </summary>
        public EffectBuilder TargetEnemyUnit()
        {
            var filter = new TargetFilter
            {
                TargetZone = Zone.Battlefield
            };
            // TODO: 设置为敌方
            _definition.TargetSelector = TargetSelector.BattlefieldUnit(filter);
            return this;
        }

        /// <summary>
        /// 目标：所有场上单位
        /// </summary>
        public EffectBuilder TargetAllUnits(TargetFilter filter = null)
        {
            _definition.TargetSelector = TargetSelector.AllBattlefieldUnits(filter);
            return this;
        }

        /// <summary>
        /// 目标：这张卡
        /// </summary>
        public EffectBuilder TargetThisCard()
        {
            _definition.TargetSelector = TargetSelector.ThisCard();
            return this;
        }

        /// <summary>
        /// 目标：手牌
        /// </summary>
        public EffectBuilder TargetCardsInHand(TargetFilter filter = null, int min = 1, int max = 1)
        {
            _definition.TargetSelector = TargetSelector.CardsInHand(filter, min, max);
            return this;
        }

        /// <summary>
        /// 目标：墓地卡牌
        /// </summary>
        public EffectBuilder TargetCardsInGraveyard(TargetFilter filter = null, int min = 1, int max = 1)
        {
            _definition.TargetSelector = TargetSelector.CardsInGraveyard(filter, min, max);
            return this;
        }

        /// <summary>
        /// 目标：随机卡牌
        /// </summary>
        public EffectBuilder TargetRandomCard(TargetFilter filter = null, int count = 1)
        {
            _definition.TargetSelector = TargetSelector.RandomCard(filter, count);
            return this;
        }

        /// <summary>
        /// 设置目标为可选
        /// </summary>
        public EffectBuilder TargetOptional()
        {
            if (_definition.TargetSelector != null)
                _definition.TargetSelector.Optional = true;
            return this;
        }

        #endregion

        #region 原子效果添加



        /// <summary>
        /// 造成伤害
        /// </summary>
        public EffectBuilder DealDamage(int amount)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.DealDamage,
                Value = amount
            });
            return this;
        }

        /// <summary>
        /// 造成战斗伤害
        /// </summary>
        public EffectBuilder DealCombatDamage(int amount)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.DealCombatDamage,
                Value = amount
            });
            return this;
        }

        /// <summary>
        /// 回复生命
        /// </summary>
        public EffectBuilder Heal(int amount)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.Heal,
                Value = amount
            });
            return this;
        }

        /// <summary>
        /// 生命流失
        /// </summary>
        public EffectBuilder LifeLoss(int amount)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.LifeLoss,
                Value = amount
            });
            return this;
        }

        /// <summary>
        /// 抽卡
        /// </summary>
        public EffectBuilder DrawCards(int count)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.DrawCard,
                Value = count
            });
            return this;
        }

        /// <summary>
        /// 弃牌
        /// </summary>
        public EffectBuilder DiscardCards(int count, bool random = false)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.DiscardCard,
                Value = count,
                Value2 = random ? 1 : 0
            });
            return this;
        }

        /// <summary>
        /// 磨牌
        /// </summary>
        public EffectBuilder MillCards(int count)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.MillCard,
                Value = count
            });
            return this;
        }

        /// <summary>
        /// 返回手牌
        /// </summary>
        public EffectBuilder ReturnToHand()
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.ReturnToHand
            });
            return this;
        }

        /// <summary>
        /// 销毁目标
        /// </summary>
        public EffectBuilder DestroyTarget()
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.Destroy
            });
            return this;
        }

        /// <summary>
        /// 除外目标
        /// </summary>
        public EffectBuilder ExileTarget(Zone fromZone = Zone.Battlefield)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.Exile,
                ZoneParam = fromZone
            });
            return this;
        }

        /// <summary>
        /// 放入战场
        /// </summary>
        public EffectBuilder PutToBattlefield(Zone fromZone = Zone.Hand)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.PutToBattlefield,
                ZoneParam = fromZone
            });
            return this;
        }

        /// <summary>
        /// 横置目标
        /// </summary>
        public EffectBuilder TapTarget()
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.Tap
            });
            return this;
        }

        /// <summary>
        /// 重置目标
        /// </summary>
        public EffectBuilder UntapTarget()
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.Untap
            });
            return this;
        }

        /// <summary>
        /// 修改攻击力
        /// </summary>
        public EffectBuilder ModifyPower(int amount, DurationType duration = DurationType.UntilEndOfTurn)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.ModifyPower,
                Value = amount,
                Duration = duration
            });
            return this;
        }

        /// <summary>
        /// 修改生命值
        /// </summary>
        public EffectBuilder ModifyLife(int amount, DurationType duration = DurationType.UntilEndOfTurn)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.ModifyLife,
                Value = amount,
                Duration = duration
            });
            return this;
        }

        /// <summary>
        /// 设置攻击力
        /// </summary>
        public EffectBuilder SetPower(int value)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.SetPower,
                Value = value
            });
            return this;
        }

        /// <summary>
        /// 设置生命值
        /// </summary>
        public EffectBuilder SetLife(int value)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.SetLife,
                Value = value
            });
            return this;
        }

        /// <summary>
        /// 添加法力
        /// </summary>
        public EffectBuilder AddMana(ManaType type, int amount)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.AddMana,
                ManaTypeParam = type,
                Value = amount
            });
            return this;
        }

        /// <summary>
        /// 获得控制权
        /// </summary>
        public EffectBuilder GainControl()
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.GainControl
            });
            return this;
        }

        /// <summary>
        /// 防止伤害
        /// </summary>
        public EffectBuilder PreventDamage(int amount = 0)
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.PreventDamage,
                Value = amount
            });
            return this;
        }

        /// <summary>
        /// 无效效果
        /// </summary>
        public EffectBuilder NegateEffect()
        {
            _definition.Effects.Add(new AtomicEffectInstance
            {
                Type = AtomicEffectType.NegateEffect
            });
            return this;
        }

        #endregion

        #region 元数据设置

        /// <summary>
        /// 设置为可选（触发式）
        /// </summary>
        public EffectBuilder SetOptional(bool optional = true)
        {
            _definition.IsOptional = optional;
            return this;
        }

        /// <summary>
        /// 设置持续时间
        /// </summary>
        public EffectBuilder SetDuration(DurationType duration)
        {
            _definition.Duration = duration;
            return this;
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        public EffectBuilder AddTag(string tag)
        {
            _definition.Tags.Add(tag);
            return this;
        }

        #endregion

        #region 构建

        /// <summary>
        /// 构建效果定义
        /// </summary>
        public EffectDefinition Build()
        {
            // 生成ID（如果未设置）
            if (string.IsNullOrEmpty(_definition.Id))
            {
                _definition.Id = GenerateId();
            }

            // 生成描述（如果未设置）
            if (string.IsNullOrEmpty(_definition.Description))
            {
                _definition.Description = _definition.GetFullDescription();
            }

            return _definition;
        }

        /// <summary>
        /// 生成效果ID
        /// </summary>
        private string GenerateId()
        {
            return $"effect_{DateTime.Now.Ticks % 1000000}_{Guid.NewGuid().ToString().Substring(0, 8)}";
        }

        #endregion

        #region 静态工厂

        /// <summary>
        /// 创建新的效果构建器
        /// </summary>
        public static EffectBuilder Create()
        {
            return new EffectBuilder();
        }

        /// <summary>
        /// 创建入场效果
        /// </summary>
        public static EffectBuilder OnEnterBattlefield()
        {
            return Create().TriggerOnEnterBattlefield();
        }

        /// <summary>
        /// 创建主动效果（主要阶段）
        /// </summary>
        public static EffectBuilder MainPhaseEffect()
        {
            return Create().AsMainPhaseAction();
        }

        /// <summary>
        /// 创建瞬间效果
        /// </summary>
        public static EffectBuilder InstantEffect()
        {
            return Create().AsInstantAction();
        }

        /// <summary>
        /// 创建响应效果
        /// </summary>
        public static EffectBuilder ResponseEffect()
        {
            return Create().AsResponseAction();
        }

        #endregion
    }
}
