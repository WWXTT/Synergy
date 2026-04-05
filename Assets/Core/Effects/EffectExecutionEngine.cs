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
            if (instance == null || instance.Definition == null)
                return;

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
                    UnityEngine.Debug.LogWarning($"代价支付失败: {effect.Id}");
                    return;
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

    #region 栈引擎（重构）

    /// <summary>
    /// 栈引擎（重构版）
    /// 整合速度计数器和待发效果队列
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

        /// <summary>
        /// 当前速度计数器
        /// </summary>
        public SpeedCounter SpeedCounter => _speedCounter;

        /// <summary>
        /// 待发效果队列
        /// </summary>
        public PendingEffectQueue PendingQueue => _pendingQueue;

        /// <summary>
        /// 栈大小
        /// </summary>
        public int StackSize => _stack.Count;

        /// <summary>
        /// 栈是否为空
        /// </summary>
        public bool IsEmpty => _stack.Count == 0;

        /// <summary>
        /// 当前优先权持有者
        /// </summary>
        public Player CurrentPriorityHolder => _priorityHolder;

        /// <summary>
        /// 主回合玩家
        /// </summary>
        public Player ActivePlayer => _activePlayer;

        /// <summary>
        /// 是否等待玩家选择
        /// </summary>
        public bool WaitingForPlayer => _waitingForPlayer;

        public StackEngine(EffectExecutor executor)
        {
            _executor = executor;
            _pendingQueue = new PendingEffectQueue(_speedCounter);
        }

        /// <summary>
        /// 初始化
        /// </summary>
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
        /// 尝试发动效果
        /// </summary>
        public bool TryActivateEffect(PendingEffect pending)
        {
            // 速度检查
            if (!_speedCounter.CanActivate(pending.ActivationSpeed, pending.ActivationType))
                return false;

            // 提升速度计数器
            _speedCounter.RaiseTo(pending.ActivationSpeed);

            // 创建效果实例并入栈
            var instance = EffectInstance.FromPendingEffect(pending);
            _stack.Push(instance);
            pending.IsOnStack = true;

            // 重置优先权轮转
            _priorityHolder = pending.Controller.Opponent;
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            // 触发事件
            EventManager.Instance.Publish(new StackAddEvent
            {
                AddedObject = instance,
                AddingPlayer = pending.Controller
            });

            return true;
        }

        /// <summary>
        /// 添加待发效果
        /// </summary>
        public void AddPendingEffect(PendingEffect effect)
        {
            _pendingQueue.AddPendingEffect(effect);
        }

        /// <summary>
        /// 处理待发效果（自动入栈强制/自动效果）
        /// </summary>
        public void ProcessPendingEffects()
        {
            while (true)
            {
                var next = _pendingQueue.GetNextEffect();
                if (next == null) break;

                TryActivateEffect(next);
            }
        }

        /// <summary>
        /// 玩家Pass
        /// </summary>
        public void PlayerPass(Player player)
        {
            if (player != _priorityHolder) return;

            _consecutivePassCount++;

            // 触发事件
            EventManager.Instance.Publish(new PriorityPassEvent
            {
                PassingPlayer = player,
                BothPassed = _consecutivePassCount >= 2
            });

            // 双方连续Pass，开始结算
            if (_consecutivePassCount >= 2)
            {
                BeginResolution();
                return;
            }

            // 传递优先权
            _priorityHolder = player.Opponent;
            EventManager.Instance.Publish(new PriorityGainEvent
            {
                GainingPlayer = _priorityHolder
            });
        }

        /// <summary>
        /// 玩家选择发动自由效果
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
        /// 获取可发动的自由效果列表
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
        /// 结算栈
        /// </summary>
        private void ResolveStack()
        {
            while (_stack.Count > 0 || _speedCounter.CurrentSpeed > 0)
            {
                // 1. 如果栈不为空，结算栈顶
                if (_stack.Count > 0)
                {
                    var top = _stack.Pop();
                    _executor.Execute(top);

                    // 2. 计数器-1
                    int newSpeed = _speedCounter.Decrement();

                    // 3. 检查是否有新的强制/自动效果需要入栈
                    CheckAndAddAutoEffects(newSpeed);

                    // 4. 触发结算事件
                    EventManager.Instance.Publish(new StackResolutionEndEvent
                    {
                        StackSize = _stack.Count
                    });
                }
                else
                {
                    // 栈为空但计数器>0，继续降速检查
                    int newSpeed = _speedCounter.Decrement();
                    CheckAndAddAutoEffects(newSpeed);
                }
            }

            // 结算完成
            FinishResolution();
        }

        /// <summary>
        /// 检查并添加自动效果
        /// </summary>
        private void CheckAndAddAutoEffects(int currentSpeed)
        {
            var autoEffects = _pendingQueue.GetAutoActivatableEffects(currentSpeed);

            // 按速度降序、时间戳升序排序
            var sorted = autoEffects
                .OrderByDescending(e => e.ActivationSpeed)
                .ThenBy(e => e.SequenceNumber);

            foreach (var effect in sorted)
            {
                // 提升速度计数器
                _speedCounter.RaiseTo(effect.ActivationSpeed);

                // 创建实例并入栈
                var instance = EffectInstance.FromPendingEffect(effect);
                _stack.Push(instance);
                effect.IsOnStack = true;
            }
        }

        /// <summary>
        /// 结算完成
        /// </summary>
        private void FinishResolution()
        {
            _speedCounter.Reset();
            _pendingQueue.Clear();

            EventManager.Instance.Publish(new StackEmptyEvent
            {
                LastPriorityHolder = _priorityHolder
            });
        }

        /// <summary>
        /// 新回合开始
        /// </summary>
        public void OnTurnStart(Player newTurnPlayer)
        {
            _activePlayer = newTurnPlayer;
            _priorityHolder = newTurnPlayer;
            _consecutivePassCount = 0;
            _waitingForPlayer = false;
        }

        /// <summary>
        /// 清空栈
        /// </summary>
        public void Clear()
        {
            _stack.Clear();
            _speedCounter.Reset();
            _pendingQueue.Clear();
            _waitingForPlayer = false;
            _consecutivePassCount = 0;
        }

        /// <summary>
        /// 获取栈内容（从底到顶）
        /// </summary>
        public List<EffectInstance> GetStackContents()
        {
            return _stack.Reverse().ToList();
        }

        /// <summary>
        /// 传递优先权（PlayerPass 的别名）
        /// </summary>
        public void PassPriority(Player player)
        {
            PlayerPass(player);
        }

        /// <summary>
        /// 结算栈顶一个效果
        /// </summary>
        public void ResolveTop()
        {
            if (_stack.Count > 0)
            {
                var top = _stack.Pop();
                _executor.Execute(top);
                _speedCounter.Decrement();
            }
        }

        /// <summary>
        /// 查看栈顶（不弹出）
        /// </summary>
        public EffectInstance Peek()
        {
            return _stack.Count > 0 ? _stack.Peek() : null;
        }

        /// <summary>
        /// 获取效果执行器
        /// </summary>
        public EffectExecutor GetExecutor() => _executor;
    }

    #endregion

    #region 触发引擎（扩展）

    /// <summary>
    /// 触发引擎扩展
    /// 整合效果定义系统
    /// </summary>
    public class TriggerEngine
    {
        private List<RegisteredEffect> _registeredEffects = new List<RegisteredEffect>();
        private StackEngine _stackEngine;

        /// <summary>
        /// 已注册的效果列表
        /// </summary>
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

                // 检查触发条件
                // TODO: 实现触发条件检查

                result.Add(registered);
            }

            return result;
        }

        /// <summary>
        /// 将待发触发效果放入栈（由 StackEngine 处理）
        /// </summary>
        public void PutTriggersOnStack()
        {
            _stackEngine.ProcessPendingEffects();
        }

        /// <summary>
        /// 触发效果结算后的处理
        /// </summary>
        public void OnTriggerResolved(EffectInstance effect)
        {
            // 触发效果结算后的清理工作
            // 当前不需要特殊处理
        }

        /// <summary>
        /// 清空所有注册效果
        /// </summary>
        public void Clear()
        {
            _registeredEffects.Clear();
        }

        /// <summary>
        /// 清空所有注册效果（别名）
        /// </summary>
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
        /// 注册卡牌效果
        /// </summary>
        public void RegisterCardEffects(Card card, List<EffectDefinition> effects, Player controller)
        {
            foreach (var effect in effects)
            {
                _triggerEngine.RegisterEffect(effect, card, controller);
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
