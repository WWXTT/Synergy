using System;
using System.Collections.Generic;

namespace CardCore
{
    public interface IGameObserver
    {
        void OnGameEvent(GameEvent e);
        void OnDecision(Player player, PlayerDecision decision);
        void OnEffectResolved(EffectInstance effect);
    }

    public class ReplayRecorder : IGameObserver
    {
        ReplayLog log;

        public ReplayRecorder(ReplayLog log)
        {
            this.log = log;
        }

        public void OnGameEvent(GameEvent e)
        {

        }

        public void OnDecision(Player player, PlayerDecision decision)
        {
            log.Decisions.Add(decision);
        }

        public void OnEffectResolved(EffectInstance effect)
        {

        }
    }

    [Serializable]
    public class ReplayLog
    {
        public int RandomSeed;

        public List<InitialDeckState> Decks = new();
        public List<PlayerDecision> Decisions = new();
        public List<ReplayEvent> Events = new();
    }

    [Serializable]
    public class InitialDeckState
    {

    }

    [Serializable]
    public class ReplayEvent
    {

    }

    [Serializable]
    public class PlayerDecision
    {
        public int Turn;
        public Player Player;

        public string SourceCardId;
        public List<string> TargetEntityIds;
    }

    public class ReplayPlayer
    {
        ReplayLog log;
        int decisionIndex;

        public ReplayPlayer(ReplayLog log)
        {
            this.log = log;
            decisionIndex = 0;
        }

        public PlayerDecision NextDecision()
        {
            return log.Decisions[decisionIndex++];
        }
    }

}