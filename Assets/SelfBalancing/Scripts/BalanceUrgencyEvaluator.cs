using UnityEngine;

namespace FrostPunchGames
{
    // Phase-2 arbiter. Emits a single continuous balanceUrgency (0..1) that all downstream
    // modules consume to blend between "animation performance" (urgency 0) and "physics
    // survival" (urgency 1). The metric is the equivalent COM-acceleration deviation between
    // what the animation WANTS the COM to do (ghost rig) and what the physical rig is ACTUALLY
    // doing. Small force => animation leads; large force => physics leads.
    //
    // Lives on the Physical_Rig alongside ArticulationSyncer. Runs before the syncer
    // (DefaultExecutionOrder 80 < syncer 100), so it reads the capture point from the previous
    // physics tick (a one-frame lag, acceptable for a blend signal).
    [DefaultExecutionOrder(80)]
    public class BalanceUrgencyEvaluator : MonoBehaviour
    {
        [Header("References (auto-wired by ActiveRagdollBrain)")]
        [Tooltip("Source of the physical-rig capture point / COM velocity. Auto-fetched from the syncer if left empty.")]
        public CapturePointSolver capturePoint;
        [Tooltip("Provides the physical/ghost bone pairing used to synthesize the ghost COM.")]
        public ArticulationSyncer syncer;

        [Header("Urgency Metric")]
        [Tooltip("COM-acceleration deviation (m/s^2) that maps to urgency = 1. ~0.5*g is a sane start; larger = harder to saturate.")]
        public float maxExpectedAccel = 4.9f;
        [Tooltip("How strongly the DCM divergence rate (capture point escaping support) folds into urgency.")]
        public float divergenceWeight = 0.15f;

        [Header("Adaptive Filter (EMA time constant, seconds)")]
        [Tooltip("Low-urgency time constant: long => heavy smoothing => rejects animation jitter while standing.")]
        public float longFilter = 0.05f;
        [Tooltip("High-urgency time constant: short => fast response to a real impact.")]
        public float shortFilter = 0.01f;

        [Header("Manual Override (editor preview)")]
        [Tooltip("ON: 把 Urgency 钉死为下面的滑杆值，并喂给所有下游模块——三大表现部分都会响应：步态IK(步频/落脚+髋策略)、肌肉(关节驱动总权重)、手臂(平衡偏移/武器摆动/手部支撑/COM混合/MagicBlend)，便于在固定 urgency 下预览混合后的动作。OFF: Urgency 由质心加速度偏差自动计算。")]
        public bool overrideUrgency = false;
        [Tooltip("The forced urgency value used while Manual Override is ON.")]
        [Range(0f, 1f)]
        public float overrideValue = 0f;

        // --- Outputs ---
        public float Urgency { get; private set; }
        // Ghost-COM velocity (m/s), synthesized from ghost bone positions weighted by their
        // paired physical masses. Consumed by CapturePointSolver for the effective-COM-velocity blend.
        public Vector3 GhostComVelocity { get; private set; }
        public Vector3 GhostCom { get; private set; }

        // --- Layer-3 physics correction signals (consumed by PhysicsPoseBlender) ---
        // The whole rig's gravity/LIPM "topple": the rotation that bends the ANIMATED root pose toward
        // the PHYSICAL (gravity-loaded) root pose. Identity when physics matches animation; grows as the
        // physical hips tip/fold. The blender Slerps the visible root toward this, scaled by urgency.
        public Quaternion RootTiltDelta { get; private set; } = Quaternion.identity;
        // Horizontal displacement (world space, m) of the physical COM from the animated COM.
        public Vector3 ComOffset { get; private set; }
        // LIPM lean direction: horizontal COM->capture-point vector (m). The divergent component of
        // motion — points the way the physical COM is escaping. The blender leans the body along it.
        public Vector3 PendulumLean { get; private set; }

        private Vector3 _prevPhysComVel;
        private bool _hasPrevPhysVel;
        private Vector3 _prevGhostCom;
        private Vector3 _prevGhostComVel;
        private bool _hasPrevGhost;

        private float _impulseBoost;

        // Cached root driver (the floating-base hips link): its physical body vs paired ghost bone
        // gives the whole-body tilt. Resolved lazily from the syncer's bone pairing.
        private ArticulationDriver _rootDriver;

        private float Gravity => Mathf.Abs(Physics.gravity.y);

        private void Awake()
        {
            if (syncer == null) syncer = GetComponent<ArticulationSyncer>();
            if (capturePoint == null && syncer != null) capturePoint = syncer.capturePoint;
            if (capturePoint == null) capturePoint = GetComponent<CapturePointSolver>();
        }

        // Returns a 0..1 ramp of urgency across an arbitrary response window, so each downstream
        // module can carve out its own activation band (e.g. weapon [0,0.6], arm [0.3,0.8]).
        public float Window(float lo, float hi) => Mathf.Clamp01(Mathf.InverseLerp(lo, hi, Urgency));

        // Lets the external-force channel (CharacterPerimeter / collisions) nudge urgency upward
        // immediately on a hit, instead of waiting for the acceleration estimate to catch up.
        public void RegisterImpulse(float magnitude01)
        {
            _impulseBoost = Mathf.Max(_impulseBoost, Mathf.Clamp01(magnitude01));
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            if (capturePoint == null && syncer != null) capturePoint = syncer.capturePoint;

            ComputeGhostCom(dt);
            ComputeCorrectionSignals();

            // Manual override (editor preview): pin Urgency to the slider value so all downstream
            // modules blend at a fixed urgency. Ghost COM is still computed above so the COM-velocity
            // blend has a valid ghost reference; we just skip the acceleration-based estimate.
            if (overrideUrgency)
            {
                Urgency = Mathf.Clamp01(overrideValue);
                _impulseBoost = 0f;
                // Keep the velocity history fresh so there's no accel spike on the frame override turns off.
                _prevPhysComVel = capturePoint != null ? capturePoint.PhysicalComVelocity : Vector3.zero;
                _hasPrevPhysVel = true;
                _prevGhostComVel = GhostComVelocity;
                return;
            }

            // Physical COM acceleration: differentiate the solver's UNBLENDED physical COM velocity.
            // (capturePoint.ComVelocity is the urgency-blended value, which would make the arbiter
            // measure its own blend and go blind to impacts at low urgency.)
            Vector3 physComVel = capturePoint != null ? capturePoint.PhysicalComVelocity : Vector3.zero;
            Vector3 physComAccel = _hasPrevPhysVel ? (physComVel - _prevPhysComVel) / dt : Vector3.zero;
            _prevPhysComVel = physComVel;
            _hasPrevPhysVel = true;

            // Ghost COM acceleration: how the animation intends the COM to accelerate.
            Vector3 ghostComAccel = _hasPrevGhost ? (GhostComVelocity - _prevGhostComVel) / dt : Vector3.zero;
            _prevGhostComVel = GhostComVelocity;

            float deviationAccel = (physComAccel - ghostComAccel).magnitude;
            float urgencyRaw = Mathf.Clamp01(deviationAccel / Mathf.Max(0.001f, maxExpectedAccel));

            // Fold in divergence + any pending impulse boost.
            if (capturePoint != null)
                urgencyRaw = Mathf.Clamp01(urgencyRaw + capturePoint.DcmDivergenceRate * divergenceWeight);
            urgencyRaw = Mathf.Clamp01(urgencyRaw + _impulseBoost);
            _impulseBoost = Mathf.MoveTowards(_impulseBoost, 0f, dt * 2f); // decays over ~0.5s

            // Adaptive EMA: longer time constant when calm, shorter when urgent.
            float tau = Mathf.Lerp(longFilter, shortFilter, Urgency);
            float alpha = tau > 0f ? 1f - Mathf.Exp(-dt / tau) : 1f;
            Urgency = Mathf.Lerp(Urgency, urgencyRaw, alpha);
        }

        // Weighted ghost COM using the SAME mass distribution as the physical rig, so the two
        // COMs are directly comparable. Masses are fixed at generation time, ghost positions move.
        private void ComputeGhostCom(float dt)
        {
            if (syncer == null || syncer.physicsBones == null || syncer.physicsBones.Length == 0)
            {
                GhostComVelocity = Vector3.zero;
                return;
            }

            Vector3 comSum = Vector3.zero;
            float massSum = 0f;
            foreach (var b in syncer.physicsBones)
            {
                if (b != null && b.body != null && b.ghostBone != null)
                {
                    comSum += b.ghostBone.position * b.body.mass;
                    massSum += b.body.mass;
                }
            }
            if (massSum <= 0f)
            {
                GhostComVelocity = Vector3.zero;
                return;
            }

            GhostCom = comSum / massSum;
            GhostComVelocity = _hasPrevGhost ? (GhostCom - _prevGhostCom) / dt : Vector3.zero;
            _prevGhostCom = GhostCom;
            _hasPrevGhost = true;
        }

        // Synthesizes the layer-3 correction signals (RootTiltDelta / ComOffset / PendulumLean) from
        // the physical rig's current state. These are raw, unscaled physics readings; PhysicsPoseBlender
        // applies the urgency weighting and smoothing when it writes them onto the visible Ghost pose.
        private void ComputeCorrectionSignals()
        {
            ResolveRootDriver();

            // Whole-body tilt: rotation taking the ANIMATED (ghost) root orientation to the PHYSICAL one.
            // physRot = tilt * ghostRot  =>  tilt = physRot * inverse(ghostRot).
            if (_rootDriver != null && _rootDriver.body != null && _rootDriver.ghostBone != null)
            {
                Quaternion physRot = _rootDriver.body.transform.rotation;
                Quaternion ghostRot = _rootDriver.ghostBone.rotation;
                RootTiltDelta = physRot * Quaternion.Inverse(ghostRot);
            }
            else
            {
                RootTiltDelta = Quaternion.identity;
            }

            // COM offset: physical COM minus animated COM, flattened to the horizontal plane (the
            // vertical component is mostly bob/contact, not a balance signal).
            if (capturePoint != null)
            {
                Vector3 off = capturePoint.Com - GhostCom;
                off.y = 0f;
                ComOffset = off;

                // LIPM lean: horizontal COM -> capture-point divergent vector.
                Vector3 lean = capturePoint.CapturePoint - capturePoint.Com;
                lean.y = 0f;
                PendulumLean = lean;
            }
            else
            {
                ComOffset = Vector3.zero;
                PendulumLean = Vector3.zero;
            }
        }

        private void ResolveRootDriver()
        {
            if (_rootDriver != null && _rootDriver.body != null) return;
            if (syncer == null || syncer.physicsBones == null) return;
            foreach (var b in syncer.physicsBones)
            {
                if (b != null && b.body != null && b.body.isRoot)
                {
                    _rootDriver = b;
                    return;
                }
            }
        }

        public void ResetState()
        {
            Urgency = 0f;
            _hasPrevPhysVel = false;
            _hasPrevGhost = false;
            _impulseBoost = 0f;
            RootTiltDelta = Quaternion.identity;
            ComOffset = Vector3.zero;
            PendulumLean = Vector3.zero;
        }
    }
}
