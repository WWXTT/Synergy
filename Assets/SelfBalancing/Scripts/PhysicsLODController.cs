using UnityEngine;

namespace FrostPunchGames
{
    // Distance-based level-of-detail for the active ragdoll. As the camera pulls away it progressively
    // lowers the physics update rate, drops the procedural-step layer, and culls the animator,
    // trading fidelity for cost. It only toggles existing components and the syncer's updateDivider; it
    // never touches the global fixedDeltaTime. Hysteresis on the level boundaries prevents flicker.
    //
    // By default it stays conservative: even at the far levels it keeps the physics syncer enabled
    // (just heavily divided) rather than disabling ArticulationBody simulation, because re-enabling a
    // stopped articulation can pop. Set aggressivePhysicsCulling to fully stop physics at LOD 2/3.
    [AddComponentMenu("Procedural Animation/Physics LOD Controller")]
    public class PhysicsLODController : MonoBehaviour
    {
        [Header("References")]
        public Transform ghostRoot;
        public Animator ghostAnimator;
        public ArticulationSyncer syncer;
        public IKSolver ikSolver;
        public StepManager stepManager;

        [Header("Settings")]
        public bool lodEnabled = true;
        [Tooltip("Distance at which LOD 1 (reduced physics rate, no stepping/arms) begins.")]
        public float lod1Distance = 15f;
        [Tooltip("Distance at which LOD 2 (IK off) begins.")]
        public float lod2Distance = 40f;
        [Tooltip("Distance at which LOD 3 (animation position only) begins.")]
        public float lod3Distance = 80f;
        [Range(0f, 0.5f), Tooltip("Fraction below a boundary the camera must retreat before stepping back up to a higher-detail level (anti-flicker).")]
        public float hysteresis = 0.2f;
        [Tooltip("Physics update divider used at LOD 1.")]
        public int lod1Divider = 2;
        [Tooltip("Physics update divider used at LOD 2+ (only when not fully culling physics).")]
        public int lod2Divider = 4;
        [Tooltip("If true, fully disable the physics syncer at LOD 2/3 instead of just dividing it.")]
        public bool aggressivePhysicsCulling = false;

        private int _currentLevel = -1;

        private void Reset() { ghostAnimator = null; }

        private void OnDisable()
        {
            // Restore full fidelity so a disabled controller never leaves the ragdoll downgraded.
            ApplyLevel(0, force: true);
        }

        private void Update()
        {
            if (!lodEnabled) { if (_currentLevel != 0) ApplyLevel(0); return; }

            Camera cam = Camera.main;
            if (cam == null || ghostRoot == null) return;

            float dist = Vector3.Distance(cam.transform.position, ghostRoot.position);
            int target = EvaluateLevel(dist, _currentLevel);
            if (target != _currentLevel) ApplyLevel(target);
        }

        // Picks the LOD level for a distance, with hysteresis: dropping to a lower-detail level uses the
        // raw boundary, but returning to a higher-detail level requires retreating below boundary*(1-h).
        private int EvaluateLevel(float dist, int current)
        {
            float h = hysteresis;
            float b1 = lod1Distance, b2 = lod2Distance, b3 = lod3Distance;

            int raw;
            if (dist >= b3) raw = 3;
            else if (dist >= b2) raw = 2;
            else if (dist >= b1) raw = 1;
            else raw = 0;

            if (raw >= current) return raw; // moving to lower detail (or same): act immediately.

            // Moving to higher detail: only if we've cleared the hysteresis band below the boundary.
            if (current == 3 && dist > b3 * (1f - h)) return 3;
            if (current >= 2 && dist > b2 * (1f - h)) return Mathf.Max(2, raw);
            if (current >= 1 && dist > b1 * (1f - h)) return Mathf.Max(1, raw);
            return raw;
        }

        private void ApplyLevel(int level, bool force = false)
        {
            if (!force && level == _currentLevel) return;
            _currentLevel = level;

            bool physicsFull = level == 0;
            bool physicsDivided = level == 1 || (!aggressivePhysicsCulling && level >= 2);
            bool physicsOff = aggressivePhysicsCulling && level >= 2;

            if (syncer != null)
            {
                syncer.enabled = !physicsOff;
                if (physicsFull) syncer.updateDivider = 1;
                else if (level == 1) syncer.updateDivider = Mathf.Max(1, lod1Divider);
                else if (physicsDivided) syncer.updateDivider = Mathf.Max(1, lod2Divider);
            }

            // IK / balance solving stays on through LOD 1, off from LOD 2.
            if (ikSolver != null) ikSolver.enabled = level <= 1;

            if (ghostAnimator != null)
            {
                ghostAnimator.cullingMode = level == 0
                    ? AnimatorCullingMode.AlwaysAnimate
                    : (level >= 3 ? AnimatorCullingMode.CullCompletely
                                  : AnimatorCullingMode.CullUpdateTransforms);
            }
        }
    }
}
