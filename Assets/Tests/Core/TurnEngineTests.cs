using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CardCore.Tests
{
    /// <summary>
    /// TurnEngine 单元测试
    /// 测试回合流程：阶段转换、回合切换、阶段开始/结束事件
    /// </summary>
    [TestFixture]
    public class TurnEngineTests
    {
        private TurnEngine _turnEngine;
        private Player _player1;
        private Player _player2;
        private List<IGameEvent> _publishedEvents;

        [SetUp]
        public void SetUp()
        {
            _player1 = new Player("P1", 20);
            _player2 = new Player("P2", 20);
            _player1.Opponent = _player2;
            _player2.Opponent = _player1;

            _turnEngine = new TurnEngine();
            _turnEngine.Initialize(_player1);

            _publishedEvents = new List<IGameEvent>();
            EventManager.Instance.ClearAll();
            EventManager.Instance.Subscribe<IGameEvent>(e => _publishedEvents.Add(e));
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.Instance.ClearAll();
        }

        #region 初始化测试

        [Test]
        public void Initialize_SetsTurnPlayer()
        {
            Assert.AreEqual(_player1, _turnEngine.TurnPlayer);
        }

        [Test]
        public void Initialize_SetsTurnNumberToOne()
        {
            Assert.AreEqual(1, _turnEngine.TurnNumber);
        }

        [Test]
        public void Initialize_SetsCurrentPhaseToNull()
        {
            Assert.IsNull(_turnEngine.CurrentPhase);
        }

        #endregion

        #region StartNewTurn 测试

        [Test]
        public void StartNewTurn_IncrementsTurnNumber()
        {
            int initialTurn = _turnEngine.TurnNumber;
            _turnEngine.StartNewTurn(_player2);
            Assert.AreEqual(initialTurn + 1, _turnEngine.TurnNumber);
        }

        [Test]
        public void StartNewTurn_SetsTurnPlayer()
        {
            _turnEngine.StartNewTurn(_player2);
            Assert.AreEqual(_player2, _turnEngine.TurnPlayer);
        }

        [Test]
        public void StartNewTurn_PublishesTurnStartEvent()
        {
            _turnEngine.StartNewTurn(_player1);
            var turnStart = _publishedEvents.OfType<TurnStartEvent>().FirstOrDefault();
            Assert.IsNotNull(turnStart);
            Assert.AreEqual(_player1, turnStart.TurnPlayer);
        }

        [Test]
        public void StartNewTurn_StartsStandbyPhase()
        {
            _turnEngine.StartNewTurn(_player1);
            Assert.IsNotNull(_turnEngine.CurrentPhase);
            Assert.AreEqual(PhaseType.Standby, _turnEngine.CurrentPhase.Phase);
            Assert.AreEqual(PhaseState.Active, _turnEngine.CurrentPhase.State);
        }

        #endregion

        #region 阶段转换测试

        [Test]
        public void GoToNextPhase_FromStandby_GoesToDraw()
        {
            _turnEngine.StartNewTurn(_player1); // Starts at Standby
            _turnEngine.GoToNextPhase();
            Assert.AreEqual(PhaseType.Draw, _turnEngine.CurrentPhase.Phase);
        }

        [Test]
        public void GoToNextPhase_FromDraw_GoesToMain()
        {
            _turnEngine.StartNewTurn(_player1); // Standby
            _turnEngine.GoToNextPhase(); // Draw
            _turnEngine.GoToNextPhase(); // Main
            Assert.AreEqual(PhaseType.Main, _turnEngine.CurrentPhase.Phase);
        }

        [Test]
        public void GoToNextPhase_FromMain_GoesToEnd()
        {
            _turnEngine.StartNewTurn(_player1); // Standby
            _turnEngine.GoToNextPhase(); // Draw
            _turnEngine.GoToNextPhase(); // Main
            _turnEngine.GoToNextPhase(); // End
            Assert.AreEqual(PhaseType.End, _turnEngine.CurrentPhase.Phase);
        }

        [Test]
        public void GoToNextPhase_FromEnd_StartsNewTurn()
        {
            _turnEngine.StartNewTurn(_player1); // Standby, turn 2
            int turnBefore = _turnEngine.TurnNumber;

            // Go through all phases
            _turnEngine.GoToNextPhase(); // Draw
            _turnEngine.GoToNextPhase(); // Main
            _turnEngine.GoToNextPhase(); // End -> triggers new turn

            // After End phase, a new turn should start for the opponent
            Assert.AreEqual(_player2, _turnEngine.TurnPlayer);
            Assert.AreEqual(PhaseType.Standby, _turnEngine.CurrentPhase.Phase);
        }

        [Test]
        public void FullTurnCycle_SwitchesPlayers()
        {
            _turnEngine.StartNewTurn(_player1); // P1's turn
            _turnEngine.GoToNextPhase(); // Draw
            _turnEngine.GoToNextPhase(); // Main
            _turnEngine.GoToNextPhase(); // End -> P2's turn starts

            Assert.AreEqual(_player2, _turnEngine.TurnPlayer);
        }

        #endregion

        #region 阶段状态测试

        [Test]
        public void StartPhase_SetsActiveState()
        {
            _turnEngine.StartPhase(PhaseType.Main);
            Assert.AreEqual(PhaseState.Active, _turnEngine.CurrentPhase.State);
        }

        [Test]
        public void EndCurrentPhase_SetsEndingState()
        {
            _turnEngine.StartPhase(PhaseType.Main);
            _turnEngine.EndCurrentPhase();
            Assert.AreEqual(PhaseState.Ending, _turnEngine.CurrentPhase.State);
        }

        [Test]
        public void EndCurrentPhase_WhenNull_DoesNothing()
        {
            Assert.DoesNotThrow(() => _turnEngine.EndCurrentPhase());
        }

        #endregion

        #region 事件发布测试

        [Test]
        public void StartPhase_PublishesPhaseStartEvent()
        {
            _turnEngine.StartPhase(PhaseType.Main);
            var phaseEvent = _publishedEvents.OfType<PhaseStartEvent>().FirstOrDefault();
            Assert.IsNotNull(phaseEvent);
            Assert.AreEqual(PhaseType.Main, phaseEvent.Phase);
            Assert.AreEqual(_player1, phaseEvent.ActivePlayer);
        }

        [Test]
        public void EndCurrentPhase_PublishesPhaseEndEvent()
        {
            _turnEngine.StartPhase(PhaseType.Main);
            _publishedEvents.Clear();
            _turnEngine.EndCurrentPhase();
            var phaseEnd = _publishedEvents.OfType<PhaseEndEvent>().FirstOrDefault();
            Assert.IsNotNull(phaseEnd);
            Assert.AreEqual(PhaseType.Main, phaseEnd.Phase);
        }

        #endregion

        #region 行动检查测试

        [Test]
        public void CanCombatAction_InMainPhase_ReturnsTrue()
        {
            _turnEngine.StartPhase(PhaseType.Main);
            Assert.IsTrue(_turnEngine.CanCombatAction());
        }

        [Test]
        public void CanCombatAction_InDrawPhase_ReturnsFalse()
        {
            _turnEngine.StartPhase(PhaseType.Draw);
            Assert.IsFalse(_turnEngine.CanCombatAction());
        }

        [Test]
        public void CanCombatAction_WhenNullPhase_ReturnsFalse()
        {
            Assert.IsFalse(_turnEngine.CanCombatAction());
        }

        [Test]
        public void CanActivateEffect_WhenActivePhase_ReturnsTrue()
        {
            _turnEngine.StartPhase(PhaseType.Main);
            Assert.IsTrue(_turnEngine.CanActivateEffect());
        }

        [Test]
        public void CanActivateEffect_WhenNullPhase_ReturnsFalse()
        {
            Assert.IsFalse(_turnEngine.CanActivateEffect());
        }

        #endregion

        #region 扩展方法测试

        [Test]
        public void IsTurnPlayer_WhenCorrectPlayer_ReturnsTrue()
        {
            Assert.IsTrue(_turnEngine.IsTurnPlayer(_player1));
        }

        [Test]
        public void IsTurnPlayer_WhenWrongPlayer_ReturnsFalse()
        {
            Assert.IsFalse(_turnEngine.IsTurnPlayer(_player2));
        }

        [Test]
        public void GetNextPhase_ReturnsCorrectNextPhase()
        {
            _turnEngine.StartPhase(PhaseType.Standby);
            Assert.AreEqual(PhaseType.Draw, _turnEngine.GetNextPhase());
        }

        [Test]
        public void GetNextPhase_FromEnd_ReturnsStandby()
        {
            _turnEngine.StartPhase(PhaseType.End);
            Assert.AreEqual(PhaseType.Standby, _turnEngine.GetNextPhase());
        }

        [Test]
        public void GetNextPhase_WhenNull_ReturnsStandby()
        {
            Assert.AreEqual(PhaseType.Standby, _turnEngine.GetNextPhase());
        }

        #endregion
    }
}
