using UnityEngine;

namespace FrostPunchGames
{
    [System.Serializable]
    public class HangingStateMachine
    {
        public enum HangingState
        {
            Grounded,
            Airborne
        }

        public HangingState CurrentState { get; private set; } = HangingState.Grounded;

        public float TimeUngrounded { get; private set; }
        public float BlendToAirborne { get; private set; }

        private float _hangingWeight;
        public float HangingWeight
        {
            get => _hangingWeight;
            private set => _hangingWeight = value;
        }

        public float AirborneTransitionSpeed { get; set; } = 8f;
        public float MinUngroundedTime { get; set; } = 0.05f;

        public void Update(float deltaTime, bool isGrounded)
        {
            if (isGrounded)
            {
                TimeUngrounded = 0f;
                HangingWeight = Mathf.MoveTowards(HangingWeight, 0f, deltaTime * AirborneTransitionSpeed * 1.5f);

                if (HangingWeight <= 0f && CurrentState != HangingState.Grounded)
                {
                    CurrentState = HangingState.Grounded;
                }
            }
            else
            {
                TimeUngrounded += deltaTime;
                if (TimeUngrounded >= MinUngroundedTime)
                {
                    CurrentState = HangingState.Airborne;
                    HangingWeight = Mathf.MoveTowards(HangingWeight, 1f, deltaTime * AirborneTransitionSpeed);
                }
            }

            BlendToAirborne = HangingWeight * HangingWeight * (3f - 2f * HangingWeight);
        }

        public bool IsAirborne => CurrentState == HangingState.Airborne;
        public bool ShouldApplyHangingPose => CurrentState == HangingState.Airborne;

        private void TransitionTo(HangingState newState)
        {
            if (CurrentState == newState) return;

            CurrentState = newState;

            switch (newState)
            {
                case HangingState.Grounded:
                    TimeUngrounded = 0f;
                    break;

                case HangingState.Airborne:
                    BlendToAirborne = 0f;
                    break;
            }
        }

        public void ForceReset()
        {
            CurrentState = HangingState.Grounded;
            TimeUngrounded = 0f;
            BlendToAirborne = 0f;
            HangingWeight = 0f;
        }
    }
}