using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 回合阶段信息
    /// </summary>
    public class PhaseInfo
    {
        public PhaseType Phase { get; set; }
        public Player ActivePlayer { get; set; }
        public DateTime StartTime { get; set; }
        public PhaseState State { get; set; } = PhaseState.Active;
    }

    /// <summary>
    /// 阶段状态
    /// </summary>
    public enum PhaseState
    {
        /// <summary>
        /// 阶段未开始
        /// </summary>
        NotStarted,

        /// <summary>
        /// 阶段进行中
        /// </summary>
        Active,

        /// <summary>
        /// 阶段结束
        /// </summary>
        Ending,

        /// <summary>
        /// 阶段已完成
        /// </summary>
        Completed
    }

    /// <summary>
    /// 回合引擎
    /// 负责阶段状态机、优先权窗口生成、主动玩家轮换、阶段开始/结束触发
    /// </summary>
    public class TurnEngine
    {
        private PhaseInfo _currentPhase;
        private Player _turnPlayer;
        private int _turnNumber = 0;
        private List<PhaseType> _phaseOrder = new List<PhaseType>
        {
            PhaseType.Standby,
            PhaseType.Main,
            PhaseType.End
        };

        /// <summary>
        /// 当前阶段
        /// </summary>
        public PhaseInfo CurrentPhase => _currentPhase;

        /// <summary>
        /// 当前回合玩家
        /// </summary>
        public Player TurnPlayer => _turnPlayer;

        /// <summary>
        /// 回合数
        /// </summary>
        public int TurnNumber => _turnNumber;

        /// <summary>
        /// 初始化回合引擎
        /// </summary>
        public void Initialize(Player startingPlayer)
        {
            _turnPlayer = startingPlayer;
            _turnNumber = 1;
            _currentPhase = null;
        }

        /// <summary>
        /// 开始新回合
        /// </summary>
        public void StartNewTurn(Player player)
        {
            _turnPlayer = player;
            _turnNumber++;

            // 触发回合开始事件
            PublishEvent(new TurnStartEvent
            {
                TurnPlayer = player,
                TurnNumber = _turnNumber
            });

            // 开始第一个阶段（准备阶段）
            StartPhase(PhaseType.Standby);
        }

        /// <summary>
        /// 开始指定阶段
        /// </summary>
        public void StartPhase(PhaseType phaseType)
        {
            _currentPhase = new PhaseInfo
            {
                Phase = phaseType,
                ActivePlayer = _turnPlayer,
                StartTime = DateTime.Now,
                State = PhaseState.Active
            };

            // 触发阶段开始事件
            PublishEvent(new PhaseStartEvent
            {
                Phase = phaseType,
                ActivePlayer = _turnPlayer
            });

            // 根据阶段类型执行特定逻辑
            OnPhaseStarted(phaseType);
        }

        /// <summary>
        /// 阶段开始时的处理
        /// </summary>
        private void OnPhaseStarted(PhaseType phaseType)
        {
            switch (phaseType)
            {
                case PhaseType.Standby:
                    OnStandbyPhaseStarted();
                    break;
                case PhaseType.Main:
                    OnMainPhaseStarted();
                    break;
                case PhaseType.End:
                    OnEndPhaseStarted();
                    break;
            }
        }

        /// <summary>
        /// 准备阶段：重置一回合一次 → 抽1张 → 等待玩家放元素池
        /// </summary>
        private void OnStandbyPhaseStarted()
        {
            // 1. 重置回合使用标记
            // TODO: 遍历战场卡牌重置一回合一次计数

            // 2. 抽一张牌
            // 由 GameCore 调用 ZoneManagerExtensions.DrawCard

            // 3. 重置元素池每回合一次标记
            // 由 GameCore 调用 ElementPool.ResetTurnUsage

            // 4. 准备阶段自动完成后进入主阶段
            // 玩家在准备阶段的操作（放元素池）由 GameActions 处理
            // 完成后调用 AdvanceFromStandby()
        }

        /// <summary>
        /// 准备阶段完成，进入主阶段
        /// 由 GameActions.AddToElementPool 或 GameActions.SkipElementPool 调用
        /// </summary>
        public void AdvanceFromStandby()
        {
            if (_currentPhase?.Phase != PhaseType.Standby)
                return;
            EndCurrentPhase();
            GoToNextPhase();
        }

        /// <summary>
        /// 主阶段：玩家可出牌、攻击、发动效果
        /// </summary>
        private void OnMainPhaseStarted()
        {
            // 主阶段等待玩家操作，不做自动推进
        }

        /// <summary>
        /// 结束阶段：SBA检查 → 手牌上限 → 清理临时效果 → 切换玩家
        /// </summary>
        private void OnEndPhaseStarted()
        {
            // 由 GameCore 调用 SBA 检查和清理
            // 这里只发布事件和推进

            PublishEvent(new TurnEndEvent
            {
                TurnPlayer = _turnPlayer,
                TurnNumber = _turnNumber
            });

            CheckGameOver();
            PrepareNextTurn();
        }

        /// <summary>
        /// 玩家主动结束回合
        /// </summary>
        public void EndTurn()
        {
            if (_currentPhase?.Phase != PhaseType.Main)
                return;
            if (_currentPhase?.State != PhaseState.Active)
                return;

            EndCurrentPhase();
            GoToNextPhase();
        }

        /// <summary>
        /// 检查是否可以进入下一阶段
        /// </summary>
        public void CheckPhaseTransition()
        {
            // 只有阶段激活且没有优先权问题时才能推进
            if (_currentPhase?.State != PhaseState.Active)
                return;

            // TODO: 检查是否有未结算的栈
            // 如果栈不为空，不能进入下一阶段

            EndCurrentPhase();
            GoToNextPhase();
        }

        /// <summary>
        /// 结束当前阶段
        /// </summary>
        public void EndCurrentPhase()
        {
            if (_currentPhase == null)
                return;

            _currentPhase.State = PhaseState.Ending;

            // 触发阶段结束事件
            PublishEvent(new PhaseEndEvent
            {
                Phase = _currentPhase.Phase,
                ActivePlayer = _turnPlayer
            });

            // 根据阶段类型执行特定逻辑
            OnPhaseEnded(_currentPhase.Phase);
        }

        /// <summary>
        /// 阶段结束时的处理
        /// </summary>
        private void OnPhaseEnded(PhaseType phaseType)
        {
            // 主阶段结束时清理
            if (phaseType == PhaseType.Main)
            {
                // 清理战斗状态
            }
        }

        /// <summary>
        /// 准备下一回合
        /// </summary>
        private void PrepareNextTurn()
        {
            // 切换回合玩家
            _turnPlayer = _turnPlayer.Opponent;

            // TODO: 重置玩家状态（横置恢复等）
        }

        /// <summary>
        /// 进入下一阶段
        /// </summary>
        public void GoToNextPhase()
        {
            if (_currentPhase == null)
            {
                StartPhase(PhaseType.Standby);
                return;
            }

            int currentIndex = _phaseOrder.IndexOf(_currentPhase.Phase);
            int nextIndex = (currentIndex + 1) % _phaseOrder.Count;
            PhaseType nextPhase = _phaseOrder[nextIndex];

            // 特殊情况：结束阶段后直接开始下一回合的准备阶段
            if (nextPhase == PhaseType.Standby)
            {
                StartNewTurn(_turnPlayer.Opponent);
            }
            else
            {
                StartPhase(nextPhase);
            }
        }

        /// <summary>
        /// 检查游戏是否结束
        /// </summary>
        private void CheckGameOver()
        {
            if (_turnPlayer != null && _turnPlayer.Life <= 0)
            {
                PublishEvent(new GameOverEvent
                {
                    Winner = _turnPlayer.Opponent,
                    Loser = _turnPlayer,
                    Reason = GameOverReason.LifeZero,
                    TotalTurns = _turnNumber
                });
            }

            if (_turnPlayer?.Opponent != null && _turnPlayer.Opponent.Life <= 0)
            {
                PublishEvent(new GameOverEvent
                {
                    Winner = _turnPlayer,
                    Loser = _turnPlayer.Opponent,
                    Reason = GameOverReason.LifeZero,
                    TotalTurns = _turnNumber
                });
            }
        }

        /// <summary>
        /// 检查是否可以进行战斗行动
        /// </summary>
        public bool CanCombatAction()
        {
            return _currentPhase?.Phase == PhaseType.Main &&
                   _currentPhase?.State == PhaseState.Active;
        }

        /// <summary>
        /// 检查是否可以发动效果
        /// </summary>
        public bool CanActivateEffect()
        {
            return _currentPhase?.State == PhaseState.Active;
        }

        /// <summary>
        /// 获取当前回合数
        /// </summary>
        public int GetCurrentTurnNumber()
        {
            return _turnNumber;
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            EventManager.Instance.Publish(e);
        }
    }

    /// <summary>
    /// 回合引擎扩展方法
    /// </summary>
    public static class TurnEngineExtensions
    {
        /// <summary>
        /// 检查是否为某玩家的回合
        /// </summary>
        public static bool IsTurnPlayer(this TurnEngine engine, Player player)
        {
            return engine.TurnPlayer == player;
        }

        /// <summary>
        /// 获取下一阶段类型
        /// </summary>
        public static PhaseType GetNextPhase(this TurnEngine engine)
        {
            if (engine.CurrentPhase == null)
                return PhaseType.Standby;

            var phaseOrder = new List<PhaseType>
            {
                PhaseType.Standby,
                PhaseType.Main,
                PhaseType.End
            };

            int currentIndex = phaseOrder.IndexOf(engine.CurrentPhase.Phase);
            int nextIndex = (currentIndex + 1) % phaseOrder.Count;
            return phaseOrder[nextIndex];
        }
    }
}
