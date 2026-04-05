using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 游戏核心 — 轻量级门面（Facade）
    /// 负责初始化和组合各子系统，不处理规则细节
    /// 业务逻辑委托给 GameStateManager、GameLoopController 等专职管理器
    /// </summary>
    public class GameCore
    {
        private static GameCore _instance;
        public static GameCore Instance => _instance ??= new GameCore();

        // 子系统注册表
        private readonly SubSystemRegistry _subSystems = new SubSystemRegistry();

        // 核心管理器（职责分离后）
        private GameStateManager _stateManager;
        private GameLoopController _loopController;

        private Player _player1;
        private Player _player2;

        #region 公共属性 — 子系统访问

        public GameState State => _stateManager.CurrentState;
        public Player Player1 => _player1;
        public Player Player2 => _player2;
        public GameStateManager StateManager => _stateManager;
        public SubSystemRegistry SubSystems => _subSystems;

        public TurnEngine TurnEngine => _subSystems.Get<TurnEngine>();
        public StackEngine StackEngine => _subSystems.Get<StackEngine>();
        public LayerEngine LayerEngine => _subSystems.Get<LayerEngine>();
        public StateBasedActions SBAEngine => _subSystems.Get<StateBasedActions>();
        public ReplacementEngine ReplacementEngine => _subSystems.Get<ReplacementEngine>();
        public TriggerEngine TriggerEngine => _subSystems.Get<TriggerEngine>();
        public ZoneManager ZoneManager => _subSystems.Get<ZoneManager>();
        public ElementPoolSystem ElementPool => _subSystems.Get<ElementPoolSystem>();
        public ControlChangeLayer ControlChangeLayer => _subSystems.Get<ControlChangeLayer>();
        public TextChangeLayer TextChangeLayer => _subSystems.Get<TextChangeLayer>();
        public CopyEffectsEngine CopyEffectsEngine => _subSystems.Get<CopyEffectsEngine>();
        public ContinuousEffectDurationTracker DurationTracker => _subSystems.Get<ContinuousEffectDurationTracker>();
        public ModifierSystem ModifierSystem => _subSystems.Get<ModifierSystem>();
        public AttributeCalculator AttributeCalculator => _subSystems.Get<AttributeCalculator>();
        public CombatSystem CombatSystem => _subSystems.Get<CombatSystem>();
        public SummonEngine SummonEngine => _subSystems.Get<SummonEngine>();

        #endregion

        private GameCore()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化所有子系统并注册到注册表
        /// </summary>
        private void Initialize()
        {
            // 初始化玩家
            _player1 = new Player("Player 1", 20);
            _player2 = new Player("Player 2", 20);
            _player1.Opponent = _player2;
            _player2.Opponent = _player1;

            // 初始化区域管理器
            var zoneManager = new ZoneManager();
            zoneManager.InitializePlayer(_player1);
            zoneManager.InitializePlayer(_player2);
            _subSystems.Register(zoneManager);

            // 初始化元素池
            var elementPool = new ElementPoolSystem();
            elementPool.InitializePlayer(_player1);
            elementPool.InitializePlayer(_player2);
            _subSystems.Register(elementPool);

            // 初始化效果执行器和栈引擎
            var executor = new EffectExecutor(zoneManager, elementPool);
            var stackEngine = new StackEngine(executor);
            stackEngine.Initialize(_player1);
            _subSystems.Register(stackEngine);

            // 初始化回合引擎
            var turnEngine = new TurnEngine();
            turnEngine.Initialize(_player1);
            _subSystems.Register(turnEngine);

            // 初始化层引擎
            var layerEngine = new LayerEngine();
            _subSystems.Register(layerEngine);

            // 初始化状态动作系统
            var sbaEngine = new StateBasedActions();
            sbaEngine.Initialize(this);
            sbaEngine.RegisterChecker(new ZeroLifeChecker());
            sbaEngine.RegisterChecker(new ZeroToughnessChecker());
            _subSystems.Register(sbaEngine);

            // 初始化替代引擎
            var replacementEngine = new ReplacementEngine();
            _subSystems.Register(replacementEngine);

            // 初始化触发引擎
            var triggerEngine = new TriggerEngine(stackEngine);
            _subSystems.Register(triggerEngine);

            // 初始化控制变更层
            var controlChangeLayer = new ControlChangeLayer();
            controlChangeLayer.InitializePlayer(_player1);
            controlChangeLayer.InitializePlayer(_player2);
            _subSystems.Register(controlChangeLayer);

            // 初始化文本变更层
            var textChangeLayer = new TextChangeLayer();
            _subSystems.Register(textChangeLayer);

            // 初始化复制效果系统
            var copyEffectsEngine = new CopyEffectsEngine();
            _subSystems.Register(copyEffectsEngine);

            // 初始化持续效果时长追踪
            var durationTracker = new ContinuousEffectDurationTracker();
            _subSystems.Register(durationTracker);

            // 初始化修改器系统
            var modifierSystem = new ModifierSystem();
            _subSystems.Register(modifierSystem);

            // 初始化属性计算器
            var attributeCalculator = new AttributeCalculator(modifierSystem);
            _subSystems.Register(attributeCalculator);

            // 初始化战斗系统
            var combatSystem = new CombatSystem(zoneManager);
            _subSystems.Register(combatSystem);

            // 初始化召唤引擎
            var summonEngine = new SummonEngine(this);
            _subSystems.Register(summonEngine);

            // 初始化状态管理器
            _stateManager = new GameStateManager();

            // 初始化循环控制器
            _loopController = new GameLoopController(
                turnEngine, stackEngine, triggerEngine,
                sbaEngine, layerEngine, combatSystem,
                durationTracker, controlChangeLayer, textChangeLayer);
        }

        #region 游戏生命周期

        /// <summary>
        /// 初始化游戏：加载卡组、洗牌、发起手、开始游戏
        /// </summary>
        /// <param name="deck1">玩家1的卡牌实例列表（牌库）</param>
        /// <param name="deck2">玩家2的卡牌实例列表（牌库）</param>
        public void InitGame(List<Card> deck1, List<Card> deck2)
        {
            if (deck1 == null || deck2 == null)
                throw new ArgumentNullException("卡组不能为null");

            // 重置游戏状态
            Reset();

            // 将卡牌加入牌库区域，设置控制者
            foreach (var card in deck1)
            {
                card.SetController(_player1);
                ZoneManager.GetZoneContainer(_player1).Add(card, Zone.Deck);
            }
            foreach (var card in deck2)
            {
                card.SetController(_player2);
                ZoneManager.GetZoneContainer(_player2).Add(card, Zone.Deck);
            }

            // 洗牌
            ZoneManagerExtensions.ShuffleDeck(ZoneManager, _player1);
            ZoneManagerExtensions.ShuffleDeck(ZoneManager, _player2);

            // 起手抽5张
            for (int i = 0; i < 5; i++)
            {
                ZoneManagerExtensions.DrawCard(ZoneManager, _player1);
                ZoneManagerExtensions.DrawCard(ZoneManager, _player2);
            }

            // 开始游戏
            StartGame();
        }

        /// <summary>
        /// 从CardData列表初始化游戏
        /// </summary>
        public void InitGame(List<CardData> deck1Data, List<CardData> deck2Data, int copiesPerCard = 3)
        {
            var deck1 = CardLoader.BuildDeck(deck1Data, copiesPerCard);
            var deck2 = CardLoader.BuildDeck(deck2Data, copiesPerCard);
            InitGame(deck1, deck2);
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            if (!_stateManager.StartGame())
                return;

            PublishEvent(new GameStartEvent
            {
                FirstPlayer = _player1,
                SecondPlayer = _player2
            });

            TurnEngine.StartNewTurn(_player1);
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void Pause() => _stateManager.Pause();

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void Resume() => _stateManager.Resume();

        /// <summary>
        /// 结束游戏
        /// </summary>
        public void EndGame(Player winner, GameOverReason reason)
        {
            if (!_stateManager.EndGame())
                return;

            PublishEvent(new GameOverEvent
            {
                Winner = winner,
                Loser = winner.Opponent,
                Reason = reason,
                TotalTurns = TurnEngine.TurnNumber
            });
        }

        /// <summary>
        /// 游戏主循环
        /// </summary>
        public void Update()
        {
            if (!_stateManager.CanPerformGameActions)
                return;

            _loopController.Update();
        }

        /// <summary>
        /// 结算栈
        /// </summary>
        public void ResolveStack()
        {
            _loopController.ResolveStack(ReplacementEngine);
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置游戏
        /// </summary>
        public void Reset()
        {
            _stateManager.Reset();

            TurnEngine.Initialize(_player1);
            StackEngine.Initialize(_player1);

            LayerEngine.ClearAll();
            SBAEngine.ClearHistory();
            ReplacementEngine.ClearAll();
            TriggerEngine.ClearAll();
            ElementPool.Reset();
            ControlChangeLayer.ClearAll();
            TextChangeLayer.ClearAll();
            CopyEffectsEngine.ClearAll();
            DurationTracker.ClearAll();
            ModifierSystem.Clear();
        }

        #endregion

        #region 查询

        public Player GetCurrentTurnPlayer() => TurnEngine.TurnPlayer;

        public Player GetOpponent(Player player) => player.Opponent;

        #endregion

        #region 事件发布

        /// <summary>
        /// 发布事件到事件总线并通知子系统
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            EventManager.Instance.Publish(e);
            TriggerEngine.OnEvent(e);
            LayerEngine?.OnEvent(e);
        }

        #endregion
    }
}
