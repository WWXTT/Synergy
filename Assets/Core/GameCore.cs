using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 游戏状态
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// 未开始
        /// </summary>
        NotStarted,

        /// <summary>
        /// 运行中
        /// </summary>
        Running,

        /// <summary>
        /// 暂停
        /// </summary>
        Paused,

        /// <summary>
        /// 已结束
        /// </summary>
        Ended
    }

    /// <summary>
    /// 游戏核心
    /// 负责初始化、事件循环、模块协调、游戏结束判断
    /// 它是系统的顶层调度器，不处理规则细节
    /// </summary>
    public class GameCore
    {
        private static GameCore _instance;
        public static GameCore Instance => _instance ??= new GameCore();

        private GameState _state = GameState.NotStarted;
        private Player _player1;
        private Player _player2;

        // 核心子系统
        private TurnEngine _turnEngine;
        private StackEngine _stackEngine;
        private LayerEngine _layerEngine;
        private StateBasedActions _sbaEngine;
        private ReplacementEngine _replacementEngine;
        private TriggerEngine _triggerEngine;
        private ZoneManager _zoneManager;
        private ElementPoolSystem _elementPool;
        private ControlChangeLayer _controlChangeLayer;
        private TextChangeLayer _textChangeLayer;
        private CopyEffectsEngine _copyEffectsEngine;
        private ContinuousEffectDurationTracker _durationTracker;
        private ModifierSystem _modifierSystem;
        private AttributeCalculator _attributeCalculator;
        private CombatSystem _combatSystem;

        /// <summary>
        /// 当前游戏状态
        /// </summary>
        public GameState State => _state;

        /// <summary>
        /// 玩家1
        /// </summary>
        public Player Player1 => _player1;

        /// <summary>
        /// 玩家2
        /// </summary>
        public Player Player2 => _player2;

        /// <summary>
        /// 回合引擎
        /// </summary>
        public TurnEngine TurnEngine => _turnEngine;

        /// <summary>
        /// 栈/优先权引擎
        /// </summary>
        public StackEngine StackEngine => _stackEngine;

        /// <summary>
        /// 层引擎
        /// </summary>
        public LayerEngine LayerEngine => _layerEngine;

        /// <summary>
        /// 状态动作系统
        /// </summary>
        public StateBasedActions SBAEngine => _sbaEngine;

        /// <summary>
        /// 替代引擎
        /// </summary>
        public ReplacementEngine ReplacementEngine => _replacementEngine;

        /// <summary>
        /// 触发引擎
        /// </summary>
        public TriggerEngine TriggerEngine => _triggerEngine;

        /// <summary>
        /// 区域管理器
        /// </summary>
        public ZoneManager ZoneManager => _zoneManager;

        /// <summary>
        /// 元素池系统
        /// </summary>
        public ElementPoolSystem ElementPool => _elementPool;

        /// <summary>
        /// 控制变更层
        /// </summary>
        public ControlChangeLayer ControlChangeLayer => _controlChangeLayer;

        /// <summary>
        /// 文本变更层
        /// </summary>
        public TextChangeLayer TextChangeLayer => _textChangeLayer;

        /// <summary>
        /// 复制效果系统
        /// </summary>
        public CopyEffectsEngine CopyEffectsEngine => _copyEffectsEngine;

        /// <summary>
        /// 持续效果时长追踪
        /// </summary>
        public ContinuousEffectDurationTracker DurationTracker => _durationTracker;

        /// <summary>
        /// 修改器系统
        /// </summary>
        public ModifierSystem ModifierSystem => _modifierSystem;

        /// <summary>
        /// 属性计算器
        /// </summary>
        public AttributeCalculator AttributeCalculator => _attributeCalculator;

        /// <summary>
        /// 战斗系统
        /// </summary>
        public CombatSystem CombatSystem => _combatSystem;

        private GameCore()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化所有子系统
        /// </summary>
        private void Initialize()
        {
            // 初始化玩家
            _player1 = new Player("Player 1", 20);
            _player2 = new Player("Player 2", 20);
            _player1.Opponent = _player2;
            _player2.Opponent = _player1;

            // 初始化区域管理器
            _zoneManager = new ZoneManager();
            _zoneManager.InitializePlayer(_player1);
            _zoneManager.InitializePlayer(_player2);

            // 初始化元素池
            _elementPool = new ElementPoolSystem();
            _elementPool.InitializePlayer(_player1);
            _elementPool.InitializePlayer(_player2);

            // 初始化效果执行器和栈引擎（新版需要依赖注入）
            var executor = new EffectExecutor(_zoneManager, _elementPool);
            _stackEngine = new StackEngine(executor);
            _stackEngine.Initialize(_player1);

            // 初始化回合引擎
            _turnEngine = new TurnEngine();
            _turnEngine.Initialize(_player1);

            // 初始化层引擎
            _layerEngine = new LayerEngine();

            // 初始化状态动作系统
            _sbaEngine = new StateBasedActions();
            _sbaEngine.Initialize(this);
            _sbaEngine.RegisterChecker(new ZeroLifeChecker());
            _sbaEngine.RegisterChecker(new ZeroToughnessChecker());
            _sbaEngine.RegisterChecker(new LegendaryRuleChecker());

            // 初始化替代引擎
            _replacementEngine = new ReplacementEngine();

            // 初始化触发引擎（新版需要 StackEngine）
            _triggerEngine = new TriggerEngine(_stackEngine);

            // 初始化控制变更层
            _controlChangeLayer = new ControlChangeLayer();
            _controlChangeLayer.InitializePlayer(_player1);
            _controlChangeLayer.InitializePlayer(_player2);

            // 初始化文本变更层
            _textChangeLayer = new TextChangeLayer();

            // 初始化复制效果系统
            _copyEffectsEngine = new CopyEffectsEngine();

            // 初始化持续效果时长追踪
            _durationTracker = new ContinuousEffectDurationTracker();

            // 初始化修改器系统
            _modifierSystem = new ModifierSystem();

            // 初始化属性计算器
            _attributeCalculator = new AttributeCalculator(_modifierSystem);

            // 初始化战斗系统
            _combatSystem = new CombatSystem(_zoneManager);
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            if (_state != GameState.NotStarted)
            {
                return; // 已经开始
            }

            _state = GameState.Running;

            // 触发游戏开始事件
            PublishEvent(new GameStartEvent
            {
                FirstPlayer = _player1,
                SecondPlayer = _player2
            });

            // 开始第一回合
            _turnEngine.StartNewTurn(_player1);
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void Pause()
        {
            if (_state != GameState.Running)
                return;

            _state = GameState.Paused;
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void Resume()
        {
            if (_state != GameState.Paused)
                return;

            _state = GameState.Running;
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        public void EndGame(Player winner, GameOverReason reason)
        {
            _state = GameState.Ended;

            // 触发游戏结束事件
            PublishEvent(new GameOverEvent
            {
                Winner = winner,
                Loser = winner.Opponent,
                Reason = reason,
                TotalTurns = _turnEngine.TurnNumber
            });
        }

        /// <summary>
        /// 游戏主循环
        /// </summary>
        public void Update()
        {
            if (_state != GameState.Running)
                return;

            // 1. 检查并处理过期的持续效果
            _durationTracker.CheckAndCleanExpired();

            // 2. 处理过期的临时控制
            _controlChangeLayer.ProcessExpiredTemporaryControls();

            // 3. 检查文本修改
            _textChangeLayer.CheckTextModifications();

            // 4. 处理战斗阶段
            if (_combatSystem.InCombat)
            {
                // 等待玩家操作或自动推进
                // 战斗系统有自己的状态机
            }

            // 5. 检查是否有可行动作（栈为空且处于可行动阶段）
            if (_stackEngine.IsEmpty && _turnEngine.CanActivateEffect())
            {
                // 6. 收集触发式并放入栈
                _triggerEngine.PutTriggersOnStack();

                // 7. 如果栈上有对象，处理优先权
                if (_stackEngine.StackSize > 0)
                {
                    ProcessPriority();
                }
                else
                {
                    // 8. 栈为空，尝试推进阶段
                    _turnEngine.CheckPhaseTransition();
                }
            }
        }

        /// <summary>
        /// 处理优先权
        /// </summary>
        private void ProcessPriority()
        {
            // 如果栈为空，无法处理优先权
            if (_stackEngine.IsEmpty)
                return;

            // 获取当前优先权持有者
            Player currentHolder = _stackEngine.CurrentPriorityHolder;

            // 检查当前玩家是否有可执行动作
            bool hasAction = HasAvailableAction(currentHolder);

            if (hasAction)
            {
                // 有可用动作，等待玩家决策
                return;
            }

            // 没有可用动作，Pass优先权
            _stackEngine.PassPriority(currentHolder);
        }

        /// <summary>
        /// 检查玩家是否有可执行动作
        /// </summary>
        private bool HasAvailableAction(Player player)
        {
            // TODO: 检查手牌中是否有可发动的卡
            // 检查场上是否有可横置的单位
            return player.HasAvailableAction();
        }

        /// <summary>
        /// 结算栈
        /// </summary>
        public void ResolveStack()
        {
            while (_stackEngine.StackSize > 0)
            {
                // 1. 弹出栈顶对象
                var top = _stackEngine.Peek();
                _stackEngine.ResolveTop();

                if (top == null)
                    break;

                // 2. 替代引擎检查
                // TODO: 需要将 IStackObject 转换为 IGameEvent 或创建效果事件
                var replacementContext = new ReplacementContext
                {

                };
                IGameEvent finalEvent = replacementContext.GetFinalEvent();

                // 3. 结算效果
                ResolveEffect(top, finalEvent, replacementContext);

                // 4. 状态动作检查
                _sbaEngine.CheckAndExecute();

                // 5. 收集触发式并放入栈
                _triggerEngine.PutTriggersOnStack();
            }

            // 栈清空后，检查状态稳定性
            _sbaEngine.ExecuteAll();
        }

        /// <summary>
        /// 结算效果
        /// </summary>
        private void ResolveEffect(IStackObject stackObject, IGameEvent eventToProcess, ReplacementContext replacementContext)
        {
            // 1. 触发效果结算开始事件
            PublishEvent(new EffectResolveEvent
            {
                ResolvedEffect = stackObject.SourceEffect as Effect,
                Context = new EffectResolutionContext()
            });

            // 2. 执行效果逻辑 应当引用EffectExecutor处理
            //ExecuteEffectLogic(stackObject, eventToProcess, replacementContext);

            // 3. 层引擎重新计算
            _layerEngine.RecalculateAll();

            // 4. 触发效果结算结束事件
            PublishEvent(new EffectResolveEvent
            {
                ResolvedEffect = stackObject.SourceEffect as Effect,
                Context = new EffectResolutionContext()
            });

            // 5. 如果是触发式，标记已结算
            if (stackObject is TriggeredAbility)
            {
                _triggerEngine.OnTriggerResolved(stackObject as EffectInstance);
            }
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            // 通过事件总线发布到所有监听者
            GameEventBus.Publish(e);

            // 通知各子系统
            _triggerEngine.OnEvent(e);
            _layerEngine?.OnEvent(e);
        }

        /// <summary>
        /// 重置游戏
        /// </summary>
        public void Reset()
        {
            _state = GameState.NotStarted;

            _turnEngine.Initialize(_player1);
            _stackEngine.Initialize(_player1);

            // 清空所有子系统状态
            _layerEngine.ClearAll();
            _sbaEngine.ClearHistory();
            _replacementEngine.ClearAll();
            _triggerEngine.ClearAll();
            _elementPool.Reset();
            _controlChangeLayer.ClearAll();
            _textChangeLayer.ClearAll();
            _copyEffectsEngine.ClearAll();
            _durationTracker.ClearAll();
            _modifierSystem.Clear();
        }

        /// <summary>
        /// 获取当前回合玩家
        /// </summary>
        public Player GetCurrentTurnPlayer()
        {
            return _turnEngine.TurnPlayer;
        }

        /// <summary>
        /// 获取对手玩家
        /// </summary>
        public Player GetOpponent(Player player)
        {
            return player.Opponent;
        }
    }
}
