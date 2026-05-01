using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;

namespace CardCore
{
    #region 效果执行器

    /// <summary>
    /// 效果执行器
    /// 负责解析和执行效果
    /// </summary>
    public class EffectExecutor
    {
        private ZoneManager _zoneManager;
        private ElementPoolSystem _elementPool;
        private ConditionChecker _conditionChecker;
        private EffectUsageTracker _usageTracker;

        public EffectExecutor(
            ZoneManager zoneManager,
            ElementPoolSystem elementPool)
        {
            _zoneManager = zoneManager;
            _elementPool = elementPool;
            _conditionChecker = new ConditionChecker(zoneManager);
            _usageTracker = new EffectUsageTracker();
        }

        /// <summary>
        /// 检查效果是否可以发动
        /// </summary>
        public bool CanActivate(
            EffectDefinition effect,
            Entity source,
            Player activator,
            Player activePlayer,
            PhaseType currentPhase,
            int turnNumber)
        {
            // 1. 速度检查（由 SpeedCounter 处理，这里不重复）

            // 2. 时点检查
            if (!CheckTiming(effect, currentPhase, activator, activePlayer))
                return false;

            // 3. 发动条件检查
            var context = new ConditionCheckContext
            {
                Activator = activator,
                ActivePlayer = activePlayer,
                CurrentPhase = currentPhase,
                TurnNumber = turnNumber,
                Source = source,
                ZoneManager = _zoneManager,
                ActivationsThisTurn = _usageTracker.GetTurnUsage(effect.Id),
                ActivationsThisGame = _usageTracker.GetGameUsage(effect.Id)
            };

            if (!_conditionChecker.CheckAll(effect.ActivationConditions, context))
                return false;

            // 4. 费用检查
    

            // 5. 目标有效性检查
   

            return true;
        }

        /// <summary>
        /// 执行效果
        /// </summary>
        public void Execute(EffectInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (instance.Definition == null)
                throw new EffectResolutionException(instance.SourceEffect, "Effect instance has no definition");

            var effect = instance.Definition;

            // 创建执行上下文
            var context = new EffectExecutionContext
            {
                Source = instance.Source,
                Controller = instance.Controller,
                Targets = instance.Targets,
                TriggeringEvent = instance.TriggeringEvent,
                ZoneManager = _zoneManager,
                ElementPool = _elementPool
            };

            // 代价支付
            if (effect.Costs != null && effect.Costs.Count > 0)
            {
                var costContext = new CostContext
                {
                    Payer = instance.Controller,
                    ZoneManager = _zoneManager,
                    ElementPool = _elementPool,
                    Source = instance.Source
                };
                if (!CostHandlerRegistry.PayAll(effect.Costs, costContext))
                {
                    throw new EffectResolutionException(instance.SourceEffect, $"Cost payment failed for effect {effect.Id}.");
                }
            }

            // 目标解析由每个原子效果在 EffectHandlerRegistry.ExecuteEffect 中独立完成

            // 三阶段事件：发动
            foreach (var atomicEffect in effect.Effects)
            {
                EventManager.Instance.Publish(new AtomicEffectPhaseEvent
                {
                    EffectType = atomicEffect.Type,
                    Phase = AtomicEffectPhase.Activation,
                    Source = context.Source,
                    Targets = context.Targets,
                    EffectInstance = atomicEffect,
                    Context = context
                });
            }

            // 三阶段事件：开始作用
            EventManager.Instance.Publish(new AtomicEffectPhaseEvent
            {
                EffectType = effect.Effects[0].Type,
                Phase = AtomicEffectPhase.StartApplying,
                Source = context.Source,
                Targets = context.Targets,
                EffectInstance = effect.Effects[0],
                Context = context
            });

            // 执行效果
            foreach (var atomicEffect in effect.Effects)
            {
                EffectHandlerRegistry.ExecuteEffect(atomicEffect, context);
            }

            // 三阶段事件：结算完成
            foreach (var atomicEffect in effect.Effects)
            {
                EventManager.Instance.Publish(new AtomicEffectPhaseEvent
                {
                    EffectType = atomicEffect.Type,
                    Phase = AtomicEffectPhase.ResolutionComplete,
                    Source = context.Source,
                    Targets = context.Targets,
                    EffectInstance = atomicEffect,
                    Context = context
                });
            }

            // 标记已结算
            instance.IsResolved = true;

            // 记录使用次数
            _usageTracker.RecordActivation(effect.Id);

            // 触发效果结算事件
            EventManager.Instance.Publish(new EffectResolveEvent
            {
                ResolvedEffect = null, // TODO: 需要关联到 Effect
                Context = null // TODO: 创建 EffectResolutionContext
            });
        }


        /// <summary>
        /// 新回合开始
        /// </summary>
        public void OnNewTurn(int turnNumber)
        {
            _usageTracker.OnNewTurn(turnNumber);
        }

        /// <summary>
        /// 重置（新游戏）
        /// </summary>
        public void Reset()
        {
            _usageTracker.Reset();
        }

        private bool CheckTiming(
            EffectDefinition effect,
            PhaseType currentPhase,
            Player activator,
            Player activePlayer)
        {
            // 检查阶段限制
            if (effect.TriggerTiming == TriggerTiming.Activate_Active)
            {
                if (currentPhase != PhaseType.Main)
                    return false;
            }

            // 检查回合限制条件
            foreach (var condition in effect.ActivationConditions)
            {
                if (condition.Type == ConditionType.OnlyOwnTurn && activator != activePlayer)
                    return false;

                if (condition.Type == ConditionType.OnlyOpponentTurn && activator == activePlayer)
                    return false;

                if (condition.Type == ConditionType.OnlyMainPhase && currentPhase != PhaseType.Main)
                    return false;
            }

            return true;
        }
    }

    #endregion

    #region 栈引擎

    /// <summary>
    /// 栈引擎
    /// 管理效果入栈、轮询、结算
    ///
    /// 流程：
    /// 1. 条件发动（事件触发）自动入栈，每个 counter++
    /// 2. 轮询阶段：双方交替，速度发动 speed > counter 才能入栈，counter++
    /// 3. 双方 Pass → 结算（LIFO），结算中不轮询，每结算一个 counter--
    /// 4. counter 归0 → 检查延迟触发 → 有则开始新一轮
    /// </summary>
    public class StackEngine
    {
        private Stack<EffectInstance> _stack = new Stack<EffectInstance>();
        private SpeedCounter _speedCounter = new SpeedCounter();
        private PendingEffectQueue _pendingQueue;
        private EffectExecutor _executor;

        private Player _activePlayer;
        private Player _priorityHolder;
        private bool _waitingForPlayer = false;
        private int _consecutivePassCount = 0;
        private PhaseType _currentPhase;

        public SpeedCounter SpeedCounter => _speedCounter;
        public PendingEffectQueue PendingQueue => _pendingQueue;
        public int StackSize => _stack.Count;
        public bool IsEmpty => _stack.Count == 0;
        public Player CurrentPriorityHolder => _priorityHolder;
        public Player ActivePlayer => _activePlayer;
        public bool WaitingForPlayer => _waitingForPlayer;
        public PhaseType CurrentPhase => _currentPhase;

        public StackEngine(EffectExecutor executor)
        {
            _executor = executor;
            _pendingQueue = new PendingEffectQueue(_speedCounter);
        }

        public void Initialize(Player startingPlayer)
        {
            _activePlayer = startingPlayer;
            _priorityHolder = startingPlayer;
            _speedCounter.Reset();
            _pendingQueue.Clear();
            _stack.Clear();
            _waitingForPlayer = false;
            _consecutivePassCount = 0;
        }

        /// <summary>
        /// 设置当前阶段（阶段变更时调用）
        /// </summary>
        public void SetPhase(PhaseType phase)
        {
            _currentPhase = phase;
        }

        /// <summary>
        /// 处理条件发动效果（强制/自动，不检查速度，自动入栈）
        /// 每轮轮询前调用
        /// </summary>
        public void ProcessTriggeredEffects()
        {
            while (true)
            {
                var next = _pendingQueue.GetNextEffect();
                if (next == null) break;

                _speedCounter.Increment();
                var instance = EffectInstance.FromPendingEffect(next);
                _stack.Push(instance);
                next.IsOnStack = true;

                EventManager.Instance.Publish(new StackAddEvent
                {
                    AddedObject = instance,
                    AddingPlayer = next.Controller
                });
            }
        }

        /// <summary>
        /// 尝试发动效果（速度发动入口）
        /// 速度检查： speed > counter
        /// 入栈后 counter +1（不是 RaiseTo）
        /// </summary>
        public bool TryActivateEffect(PendingEffect pending)
        {
            if (!_speedCounter.CanActivate(pending.ActivationSpeed, pending.ActivationType))
                return false;

            _speedCounter.Increment(); // ★ 记速器 +1

            var instance = EffectInstance.FromPendingEffect(pending);
            _stack.Push(instance);
            pending.IsOnStack = true;

            _priorityHolder = pending.Controller.Opponent;
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            EventManager.Instance.Publish(new StackAddEvent
            {
                AddedObject = instance,
                AddingPlayer = pending.Controller
            });

            return true;
        }

        /// <summary>
        /// 添加待发效果到队列
        /// </summary>
        public void AddPendingEffect(PendingEffect effect)
        {
            _pendingQueue.AddPendingEffect(effect);
        }

        /// <summary>
        /// 处理待发效果（条件发动自动入栈）
        /// </summary>
        public void ProcessPendingEffects()
        {
            ProcessTriggeredEffects();
        }

        /// <summary>
        /// 玩家 Pass（让出优先权）
        /// </summary>
        public void PlayerPass(Player player)
        {
            if (player != _priorityHolder) return;

            _consecutivePassCount++;

            EventManager.Instance.Publish(new PriorityPassEvent
            {
                PassingPlayer = player,
                BothPassed = _consecutivePassCount >= 2
            });

            if (_consecutivePassCount >= 2)
            {
                BeginResolution();
                return;
            }

            _priorityHolder = player.Opponent;
            EventManager.Instance.Publish(new PriorityGainEvent
            {
                GainingPlayer = _priorityHolder
            });
        }

        /// <summary>
        /// 玩家选择发动速度效果
        /// </summary>
        public bool PlayerActivateVoluntary(PendingEffect effect)
        {
            if (effect.ActivationType != EffectActivationType.Voluntary)
                return false;

            var chosen = _pendingQueue.PlayerChooseEffect(effect);
            if (chosen == null) return false;

            if (TryActivateEffect(chosen))
            {
                _consecutivePassCount = 0;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取可发动的速度效果列表
        /// </summary>
        public List<PendingEffect> GetActivatableVoluntaryEffects()
        {
            return _pendingQueue.GetVoluntaryEffects();
        }

        /// <summary>
        /// 开始结算
        /// </summary>
        private void BeginResolution()
        {
            _speedCounter.BeginResolution();
            _waitingForPlayer = false;

            EventManager.Instance.Publish(new StackResolutionStartEvent
            {
                StackSize = _stack.Count
            });

            ResolveStack();
        }

        /// <summary>
        /// 结算栈（LIFO）
        /// 结算中不轮询，不插入任何效果
        /// 结算期间产生的新条件触发效果存入延迟队列
        /// </summary>
        private void ResolveStack()
        {
            while (_stack.Count > 0)
            {
                var top = _stack.Pop();
                _executor.Execute(top);
                _speedCounter.Decrement();

                EventManager.Instance.Publish(new StackResolutionEndEvent
                {
                    StackSize = _stack.Count
                });
            }

            FinishResolution();
        }

        /// <summary>
        /// 结算完成 — 检查延迟触发，可能开始新一轮
        /// </summary>
        private void FinishResolution()
        {
            _speedCounter.Reset();
            _waitingForPlayer = false;

            EventManager.Instance.Publish(new StackEmptyEvent
            {
                LastPriorityHolder = _priorityHolder
            });

            // 结算期间产生的条件触发效果 → 新一轮
            if (_pendingQueue.HasAutoEffects)
            {
                ProcessTriggeredEffects();
                if (_stack.Count > 0)
                {
                    _waitingForPlayer = true;
                    _consecutivePassCount = 0;
                    return;
                }
            }
        }

        public void OnTurnStart(Player newTurnPlayer)
        {
            _activePlayer = newTurnPlayer;
            _priorityHolder = newTurnPlayer;
            _consecutivePassCount = 0;
            _waitingForPlayer = false;
        }

        public void Clear()
        {
            _stack.Clear();
            _speedCounter.Reset();
            _pendingQueue.Clear();
            _waitingForPlayer = false;
            _consecutivePassCount = 0;
        }

        public List<EffectInstance> GetStackContents()
        {
            return _stack.Reverse().ToList();
        }

        public void PassPriority(Player player)
        {
            PlayerPass(player);
        }

        public void ResolveTop()
        {
            if (_stack.Count > 0)
            {
                var top = _stack.Pop();
                _executor.Execute(top);
                _speedCounter.Decrement();
            }
        }

        public EffectInstance Peek()
        {
            return _stack.Count > 0 ? _stack.Peek() : null;
        }

        public EffectExecutor GetExecutor() => _executor;
    }

    #endregion

    #region 触发引擎（扩展）

    /// <summary>
    /// 触发引擎
    /// 处理条件发动效果（基于事件触发）
    /// </summary>
    public class TriggerEngine
    {
        private List<RegisteredEffect> _registeredEffects = new List<RegisteredEffect>();
        private StackEngine _stackEngine;

        public List<RegisteredEffect> RegisteredEffects => _registeredEffects;

        public TriggerEngine(StackEngine stackEngine)
        {
            _stackEngine = stackEngine;
        }

        /// <summary>
        /// 注册效果
        /// </summary>
        public void RegisterEffect(EffectDefinition effect, Entity source, Player controller)
        {
            if (!effect.IsTriggeredEffect)
                return;

            _registeredEffects.Add(new RegisteredEffect
            {
                Effect = effect,
                Source = source,
                Controller = controller
            });
        }

        /// <summary>
        /// 注销效果
        /// </summary>
        public void UnregisterEffect(EffectDefinition effect)
        {
            _registeredEffects.RemoveAll(r => r.Effect == effect);
        }

        /// <summary>
        /// 注销实体的所有效果
        /// </summary>
        public void UnregisterEntityEffects(Entity source)
        {
            _registeredEffects.RemoveAll(r => r.Source == source);
        }

        /// <summary>
        /// 处理游戏事件
        /// 匹配触发时点 → 创建 PendingEffect → 加入待发队列
        /// </summary>
        public void OnEvent(IGameEvent gameEvent)
        {
            var matchingEffects = FindMatchingEffects(gameEvent);

            foreach (var match in matchingEffects)
            {
                var pending = PendingEffect.Create(
                    match.Effect,
                    match.Source,
                    match.Controller,
                    _stackEngine.ActivePlayer,
                    _stackEngine.CurrentPhase,
                    triggeringEvent: gameEvent
                );

                _stackEngine.AddPendingEffect(pending);
            }
        }

        /// <summary>
        /// 查找匹配的效果
        /// </summary>
        private List<RegisteredEffect> FindMatchingEffects(IGameEvent gameEvent)
        {
            var result = new List<RegisteredEffect>();
            var eventType = gameEvent.GetType();

            foreach (var registered in _registeredEffects)
            {
                var effect = registered.Effect;

                // 检查触发时点
                var expectedType = TriggerTimingDefaults.GetEventType(effect.TriggerTiming);
                if (expectedType != eventType)
                    continue;

                // 检查来源是否在场
                if (registered.Source != null && !registered.Source.IsAlive)
                    continue;

                // TODO: 检查触发条件

                result.Add(registered);
            }

            return result;
        }

        /// <summary>
        /// 将待发触发效果放入栈
        /// </summary>
        public void PutTriggersOnStack()
        {
            _stackEngine.ProcessTriggeredEffects();
        }

        public void OnTriggerResolved(EffectInstance effect)
        {
            // 触发效果结算后的清理
        }

        public void Clear()
        {
            _registeredEffects.Clear();
        }

        public void ClearAll()
        {
            Clear();
        }
    }

    /// <summary>
    /// 已注册的效果
    /// </summary>
    public class RegisteredEffect
    {
        public EffectDefinition Effect { get; set; }
        public Entity Source { get; set; }
        public Player Controller { get; set; }
    }

    #endregion

    #region 效果系统管理器

    /// <summary>
    /// 效果系统管理器
    /// 统一管理所有效果相关组件
    /// </summary>
    public class EffectSystemManager
    {
        private static EffectSystemManager _instance;
        public static EffectSystemManager Instance => _instance ??= new EffectSystemManager();

        private ZoneManager _zoneManager;
        private ElementPoolSystem _elementPool;
        private EffectExecutor _executor;
        private StackEngine _stackEngine;
        private TriggerEngine _triggerEngine;

        public ZoneManager ZoneManager => _zoneManager;
        public ElementPoolSystem ElementPool => _elementPool;
        public EffectExecutor Executor => _executor;
        public StackEngine StackEngine => _stackEngine;
        public TriggerEngine TriggerEngine => _triggerEngine;

        private EffectSystemManager()
        {
            Initialize();
        }

        private void Initialize()
        {
            _zoneManager = new ZoneManager();
            _elementPool = new ElementPoolSystem();
            _executor = new EffectExecutor(_zoneManager, _elementPool);
            _stackEngine = new StackEngine(_executor);
            _triggerEngine = new TriggerEngine(_stackEngine);

            RegisterBuiltinHandlers();
            BuiltinCostHandlers.RegisterAll();
        }

        private void RegisterBuiltinHandlers()
        {
            // 基础效果处理器
            var handlers = new IAtomicEffectHandler[]
            {
                new DealDamageHandler(),
                new DestroyHandler(),
                new GrantHasteHandler(),
                new DrawCardHandler(),
                new ReturnToHandHandler(),
                new FreezePermanentHandler(),
                new HealHandler(),
                new ModifyPowerHandler(),
                new CreateTokenHandler(),
            };
            foreach (var handler in handlers)
                EffectHandlerRegistry.Register(handler);

            // 注册所有关键词授予处理器
            foreach (var handler in GrantKeywordHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);
        }

        /// <summary>
        /// 初始化玩家
        /// </summary>
        public void InitializePlayer(Player player)
        {
            _zoneManager.InitializePlayer(player);
            _elementPool.InitializePlayer(player);
        }

        /// <summary>
        /// 游戏开始
        /// </summary>
        public void StartGame(Player firstPlayer)
        {
            _stackEngine.Initialize(firstPlayer);
        }

        /// <summary>
        /// 新回合开始
        /// </summary>
        public void OnTurnStart(Player turnPlayer, int turnNumber)
        {
            _stackEngine.OnTurnStart(turnPlayer);
            _executor.OnNewTurn(turnNumber);
        }

        /// <summary>
        /// 注册卡牌效果（含关键词触发效果）
        /// </summary>
        public void RegisterCardEffects(Card card, List<EffectDefinition> effects, Player controller)
        {
            foreach (var effect in effects)
            {
                _triggerEngine.RegisterEffect(effect, card, controller);
            }

            // 注册关键词触发的效果（狂暴、成长、再生等）
            var keywordEffects = KeywordEffectMapper.CreateAllTriggeredEffects(card);
            foreach (var kwEffect in keywordEffects)
            {
                _triggerEngine.RegisterEffect(kwEffect, card, controller);
            }
        }

        /// <summary>
        /// 注销卡牌效果
        /// </summary>
        public void UnregisterCardEffects(Card card)
        {
            _triggerEngine.UnregisterEntityEffects(card);
        }

        /// <summary>
        /// 处理游戏事件
        /// </summary>
        public void OnGameEvent(IGameEvent gameEvent)
        {
            _triggerEngine.OnEvent(gameEvent);
        }

        /// <summary>
        /// 主循环更新
        /// </summary>
        public void Update()
        {
            // 处理待发效果
            _stackEngine.ProcessPendingEffects();

            // 如果等待玩家选择，不自动推进
            if (_stackEngine.WaitingForPlayer)
                return;
        }

        /// <summary>
        /// 重置系统
        /// </summary>
        public void Reset()
        {
            _stackEngine.Clear();
            _triggerEngine.Clear();
            _executor.Reset();
            _elementPool.Reset();
        }
    }

    #endregion
}
