using UnityEngine;

namespace FrostPunchGames
{
    // Phase-2 arm balance. While urgency is low the arms keep whatever pose MagicBlend produced
    // (e.g. a weapon-hold pose). As urgency rises the controller blends in a balance offset that
    // moves the hands to shift the COM back over the support polygon (LIPM hip/arm strategy at the
    // end-effector level). At full urgency the arms are free to swing wide to recover.
    //
    // Lives on the Ghost_Rig. Runs after MagicBlend has written the upper-body pose (MagicBlend
    // evaluates in the Animator's fixed pass, before MonoBehaviour FixedUpdate) and before the
    // ArticulationSyncer (order 100) captures the ghost pose. Order 20 sits safely between.
    [DefaultExecutionOrder(20)]
    public class ArmBalanceController : MonoBehaviour
    {
        [Header("References (auto-wired by ActiveRagdollBrain)")]
        public IKSolver solver;
        public BalanceUrgencyEvaluator urgency;
        public Transform characterRoot;

        [Header("Left Arm Chain")]
        public Transform leftShoulder;
        public Transform leftElbow;
        public Transform leftHand;

        [Header("Right Arm Chain")]
        public Transform rightShoulder;
        public Transform rightElbow;
        public Transform rightHand;

        [Header("Urgency Window")]
        [Tooltip("Below this urgency the arms fully keep their MagicBlend pose.")]
        public float urgencyLo = 0.3f;
        [Tooltip("At/above this urgency the balance offset reaches full strength.")]
        public float urgencyHi = 0.8f;

        [Header("Balance Offset")]
        [Tooltip("Maximum hand displacement (m) the balance offset may apply.")]
        public float maxHandOffset = 0.05f;
        [Tooltip("Smoothing time for the applied offset, prevents pops when urgency changes quickly.")]
        public float offsetSmoothTime = 0.08f;

        private TwoBoneIK _leftArm;
        private TwoBoneIK _rightArm;
        private bool _ready;

        private Vector3 _leftOffset, _leftOffsetVel;
        private Vector3 _rightOffset, _rightOffsetVel;

        public void Configure(IKSolver ikSolver, BalanceUrgencyEvaluator arbiter, Transform root)
        {
            solver = ikSolver;
            urgency = arbiter;
            characterRoot = root;
        }

        private void Start() => TryInitialize();

        private void TryInitialize()
        {
            if (_ready) return;
            Transform root = characterRoot != null ? characterRoot
                : (solver != null ? solver.CharacterRoot : transform);

            if (leftShoulder != null && leftElbow != null && leftHand != null)
            {
                _leftArm = new TwoBoneIK(leftShoulder, leftElbow, leftHand);
                _leftArm.Initialize(root);
            }
            if (rightShoulder != null && rightElbow != null && rightHand != null)
            {
                _rightArm = new TwoBoneIK(rightShoulder, rightElbow, rightHand);
                _rightArm.Initialize(root);
            }
            _ready = _leftArm != null || _rightArm != null;
        }

        private void FixedUpdate()
        {
            if (!_ready) { TryInitialize(); if (!_ready) return; }
            if (solver == null) return;

            float u = urgency != null ? urgency.Urgency : 0f;
            float armWeight = Mathf.Clamp01(Mathf.InverseLerp(urgencyLo, urgencyHi, u));

            // Desired COM shift opposes the capture-point error (push the COM back over support).
            // delta = -k * (CP - supportCentroid), clamped to a small hand-scale displacement.
            Vector3 cpError = solver.CapturePointWorld - solver.SupportCentroidWorld;
            cpError.y = 0f;
            Vector3 desiredShift = Vector3.ClampMagnitude(-cpError * solver.CPArmBalanceGain, maxHandOffset);

            SolveArm(_leftArm, leftHand, armWeight, desiredShift, ref _leftOffset, ref _leftOffsetVel);
            SolveArm(_rightArm, rightHand, armWeight, desiredShift, ref _rightOffset, ref _rightOffsetVel);
        }

        private void SolveArm(TwoBoneIK arm, Transform hand, float armWeight, Vector3 desiredShift,
                              ref Vector3 smoothedOffset, ref Vector3 offsetVel)
        {
            if (arm == null || hand == null) return;

            // Snapshot the MagicBlend-produced pose as this arm's animated base, then read the
            // resulting hand position as the offset origin.
            arm.CaptureAnimatedPose();
            Vector3 basePos = hand.position;
            Quaternion baseRot = hand.rotation;

            Vector3 target = desiredShift * armWeight;
            smoothedOffset = Vector3.SmoothDamp(smoothedOffset, target, ref offsetVel, offsetSmoothTime);

            if (armWeight <= 0.001f && smoothedOffset.sqrMagnitude < 1e-6f)
                return; // fully keep MagicBlend pose

            arm.Solve(basePos + smoothedOffset, baseRot, armWeight);
        }

        public void ResetState()
        {
            _leftOffset = _rightOffset = Vector3.zero;
            _leftOffsetVel = _rightOffsetVel = Vector3.zero;
        }
    }
}
