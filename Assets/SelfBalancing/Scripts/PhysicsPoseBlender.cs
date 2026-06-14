using UnityEngine;

namespace FrostPunchGames
{
    // Layer-3 of the 3-layer animation-enhancer model (Route B: a post-overlay on top of the
    // MagicBlend-produced animation pose, NOT a plugin edit). The visible Ghost_Rig plays the
    // animation (layer 1) + MagicBlend fusion (layer 2); this blender then bends that pose toward the
    // physical rig's gravity/LIPM "topple", scaled by a single balanceUrgency scalar (0 = pure
    // animation, no fold; 1 = the visible body leans by the physical rig's actual response).
    //
    // The physical correction arrives as a few scalars/vectors (RootTiltDelta / ComOffset /
    // PendulumLean) from BalanceUrgencyEvaluator, NOT as per-bone pose deltas. We distribute the
    // single whole-body tilt as a cumulative world-space lean across the hips->spine->head chain so
    // the upper body topples while the legs stay owned by the foot IK. Arms are intentionally never
    // mapped here — they remain on ArmBalanceController.
    //
    // Lives on the Ghost_Rig. Order 50 sits after IKSolver(-5)/ArmBalanceController(20) (so we layer
    // on their result) and before ArticulationSyncer(100) (so the physical rig tracks the corrected
    // visible pose). It reads the evaluator's signals from the previous physics tick (one-frame lag,
    // fine for a blend signal — the same lag the arbiter itself accepts).
    [DefaultExecutionOrder(50)]
    public class PhysicsPoseBlender : MonoBehaviour
    {
        [Header("References (auto-wired by ActiveRagdollBrain)")]
        public BalanceUrgencyEvaluator urgency;

        [Header("Lean Chain (hips -> spine -> head, weights sum-normalized at runtime)")]
        [Tooltip("Ordered root->tip. Each bone takes a share of the whole-body tilt; cumulative lean at the tip approximates the full physical topple. Legs are omitted (owned by foot IK).")]
        public Transform[] chainBones = new Transform[0];
        [Tooltip("Per-bone share of the tilt, parallel to chainBones. Normalized at runtime; larger near the hips.")]
        public float[] chainWeights = new float[0];

        [Header("Blend")]
        [Tooltip("Smoothing time for the applied blend factor, prevents pops when urgency jumps.")]
        public float blendSmoothTime = 0.06f;
        [Tooltip("Extra horizontal lean (deg per meter) added along the LIPM capture-point direction. 0 = use the measured root tilt only.")]
        public float pendulumLeanGain = 0f;
        [Tooltip("Hard cap on the total applied lean (deg), so a degenerate physics frame can't whip the visible pose.")]
        public float maxLeanAngle = 75f;

        private float _smoothedBlend, _blendVel;
        private bool _warnedMismatch;

        public void Configure(BalanceUrgencyEvaluator arbiter)
        {
            urgency = arbiter;
        }

        private void OnEnable()
        {
            NormalizeWeights();
        }

        private void NormalizeWeights()
        {
            if (chainBones == null) return;
            if (chainWeights == null || chainWeights.Length != chainBones.Length)
            {
                // Default falloff from the hips outward if weights weren't supplied.
                chainWeights = new float[chainBones.Length];
                for (int i = 0; i < chainWeights.Length; i++) chainWeights[i] = 1f / (i + 1f);
            }
            float sum = 0f;
            for (int i = 0; i < chainWeights.Length; i++) sum += Mathf.Max(0f, chainWeights[i]);
            if (sum <= 0f) return;
            for (int i = 0; i < chainWeights.Length; i++) chainWeights[i] = Mathf.Max(0f, chainWeights[i]) / sum;
        }

        private void FixedUpdate()
        {
            if (chainBones == null || chainBones.Length == 0) return;
            if (chainWeights == null || chainWeights.Length != chainBones.Length)
            {
                NormalizeWeights();
                if (chainWeights.Length != chainBones.Length)
                {
                    if (!_warnedMismatch) { Debug.LogWarning("[PhysicsPoseBlender] chainBones/chainWeights length mismatch."); _warnedMismatch = true; }
                    return;
                }
            }

            float u = urgency != null ? Mathf.Clamp01(urgency.Urgency) : 0f;
            _smoothedBlend = Mathf.SmoothDamp(_smoothedBlend, u, ref _blendVel, blendSmoothTime);

            // Below a tiny threshold the visible pose is exactly the animation pose (layer 1+2 only).
            if (_smoothedBlend <= 0.001f) return;

            Quaternion tilt = urgency != null ? urgency.RootTiltDelta : Quaternion.identity;

            // Optional predictive lean: tip the body along the LIPM capture-point escape direction.
            if (pendulumLeanGain > 0f && urgency != null)
            {
                Vector3 lean = urgency.PendulumLean; // horizontal COM->CP vector (m)
                float mag = lean.magnitude;
                if (mag > 1e-4f)
                {
                    // Lean axis is horizontal, perpendicular to the escape direction (tip toward it).
                    Vector3 dir = lean / mag;
                    Vector3 axis = Vector3.Cross(Vector3.up, dir);
                    if (axis.sqrMagnitude > 1e-6f)
                    {
                        float deg = mag * pendulumLeanGain;
                        tilt = Quaternion.AngleAxis(deg, axis.normalized) * tilt;
                    }
                }
            }

            // Clamp the total tilt magnitude so a degenerate physics frame can't snap the pose.
            tilt.ToAngleAxis(out float tiltAngle, out Vector3 tiltAxis);
            if (tiltAngle > 180f) tiltAngle -= 360f;
            if (float.IsNaN(tiltAxis.x) || float.IsInfinity(tiltAxis.x)) return;
            float clamped = Mathf.Clamp(tiltAngle, -maxLeanAngle, maxLeanAngle);
            tilt = Quaternion.AngleAxis(clamped, tiltAxis.normalized);

            // Apply the lean root->tip. Each bone adds its share of the whole-body world-space tilt on
            // top of the animation pose. Because we go root->tip and write world rotation, a child
            // inherits its parent's already-applied lean and stacks its own share, so the cumulative
            // lean at the tip approximates `tilt`. MagicBlend rewrites every bone's localRotation fresh
            // each frame before this runs, so there's no residual to clear — at urgency 0 the pose is
            // exactly the animation pose.
            for (int i = 0; i < chainBones.Length; i++)
            {
                Transform bone = chainBones[i];
                if (bone == null) continue;

                float share = _smoothedBlend * chainWeights[i];
                if (share <= 0.0001f) continue;

                Quaternion qWorld = Quaternion.Slerp(Quaternion.identity, tilt, share);
                bone.rotation = qWorld * bone.rotation;
            }
        }

        public void ResetState()
        {
            _smoothedBlend = 0f;
            _blendVel = 0f;
        }
    }
}
