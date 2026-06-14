using UnityEngine;

namespace FrostPunchGames
{
    public class FootGlueStateMachine
    {
        public enum GlueState { Free, Glued, CoolingDown }

        public GlueState CurrentState { get; private set; } = GlueState.Free;
        public float CooldownTimeRemaining;
        public float CurrentAttachedDuration;

        public float CooldownDuration = 0.25f;
        public float MinAttachedDuration = 0.1f;
        public float AttachStrictnessMultiplier = 1f;
        public float DetachStrictnessMultiplier = 1f;

        public bool IsGlued => CurrentState == GlueState.Glued;

        public void Update(float deltaTime)
        {
            if (CurrentState == GlueState.Glued)
            {
                CurrentAttachedDuration += deltaTime;
            }
            else if (CurrentState == GlueState.CoolingDown)
            {
                CooldownTimeRemaining -= deltaTime;
                if (CooldownTimeRemaining <= 0f)
                {
                    CurrentState = GlueState.Free;
                }
            }
        }
        public void ForceGlued()
        {
            CurrentState = GlueState.Glued;
            CurrentAttachedDuration = 0f;
            CooldownTimeRemaining = 0f;
        }
        public bool TryAttach()
        {
            if (CurrentState != GlueState.Free) return false;

            CurrentState = GlueState.Glued;
            CurrentAttachedDuration = 0f;
            return true;
        }

        public bool TryDetach()
        {
            if (CurrentState != GlueState.Glued) return false;

            if (CurrentAttachedDuration >= MinAttachedDuration)
            {
                CurrentState = GlueState.CoolingDown;
                CooldownTimeRemaining = CooldownDuration;
                return true;
            }
            return false;
        }

        public void ForceDetach()
        {
            CurrentState = GlueState.CoolingDown;
            CooldownTimeRemaining = CooldownDuration;
        }

        public void ForceReset()
        {
            CurrentState = GlueState.Free;
            CurrentAttachedDuration = 0f;
            CooldownTimeRemaining = 0f;
        }

        public float GetDetachThresholdMultiplier() => DetachStrictnessMultiplier;

        public float GetNormalizedProgress()
        {
            if (CurrentState != GlueState.CoolingDown || CooldownDuration <= 0f) return 0f;
            return 1f - (CooldownTimeRemaining / CooldownDuration);
        }
    }
}