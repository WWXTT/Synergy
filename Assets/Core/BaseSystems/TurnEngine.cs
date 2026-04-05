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
            PhaseType.Draw,
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
                case PhaseType.Draw:
                    OnDrawPhaseStarted();
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
        /// 准备阶段开始
        /// </summary>
        private void OnStandbyPhaseStarted()
        {
            // 所有横置的单位恢复为立置状态
            // TODO: 实现 Unit 的 untap 逻辑
            // 触发 UntapEvent
        }

        /// <summary>
        /// 抽卡阶段开始
        /// </summary>
        private void OnDrawPhaseStarted()
        {
            // 玩家抽卡
            // TODO: 实现 ZoneManager 从 Deck 抽卡到 Hand
            // 触发 CardDrawEvent

            // 检查是否可以进入下一阶段
            CheckPhaseTransition();
        }

        /// <summary>
        /// 主阶段开始
        /// </summary>
        private void OnMainPhaseStarted()
        {
            // 战斗融入主阶段
            // 玩家可以通过 GameCore.CombatSystem.StartCombat() 开始战斗
            // 战斗不是强制性的，玩家可以选择是否进行战斗
        }

        /// <summary>
        /// 结束阶段开始
        /// </summary>
        private void OnEndPhaseStarted()
        {
            // 检查手牌上限
            // 丢弃超出手牌
            // 触发 PhaseEndEvent
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
            switch (phaseType)
            {
                case PhaseType.Draw:
                    OnDrawPhaseEnded();
                    break;
                case PhaseType.Main:
                    OnMainPhaseEnded();
                    break;
                case PhaseType.End:
                    OnEndPhaseEnded();
                    break;
            }
        }

        /// <summary>
        /// 抽卡阶段结束
        /// </summary>
        private void OnDrawPhaseEnded()
        {
            // 清理临时效果
            // 检查状态变化
        }

        /// <summary>
        /// 主阶段结束
        /// </summary>
        private void OnMainPhaseEnded()
        {
            // 清理持续效果
            // 检查战斗结果
        }

        /// <summary>
        /// 结束阶段结束
        /// </summary>
        private void OnEndPhaseEnded()
        {
            // 回合结束处理
            // 触发回合结束事件
            PublishEvent(new TurnEndEvent
            {
                TurnPlayer = _turnPlayer,
                TurnNumber = _turnNumber
            });

            // 检查游戏结束条件
            CheckGameOver();

            // 准备下一回合
            PrepareNextTurn();
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
            // 检查玩家生命值
            // TODO: 通过 Entity 获取玩家生命值
            /*
            if (_turnPlayer.Life <= 0 || _turnPlayer.Opponent.Life <= 0)
            {
                PublishEvent(new GameOverEvent
                {
                    Winner = _turnPlayer.Opponent,
                    Loser = _turnPlayer,
                    Reason = GameOverReason.LifeZero,
                    TotalTurns = _turnNumber
                });
            }
            */
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
                PhaseType.Draw,
                PhaseType.Main,
                PhaseType.End
            };

            int currentIndex = phaseOrder.IndexOf(engine.CurrentPhase.Phase);
            int nextIndex = (currentIndex + 1) % phaseOrder.Count;
            return phaseOrder[nextIndex];
        }
    }
}
