using System;
using cfg;

namespace CardCore
{
    public abstract class GameEvent : IGameEvent
    {
        private static uint _nextEventId = 0;

        public DateTime Timestamp { get; } = DateTime.Now;
        public uint EventId { get; } = ++_nextEventId;
    }

    public class UnitDiedEvent : GameEvent
    {
        public Unit Unit;
    }

    public class TimingEvent : GameEvent
    {
        public TriggerTiming Point;
        public Player TurnPlayer;
        public object Context; // 可选：攻击者、被攻击者等
    }

}