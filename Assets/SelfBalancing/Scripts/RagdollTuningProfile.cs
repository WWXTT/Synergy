using UnityEngine;

namespace FrostPunchGames
{
    [CreateAssetMenu(fileName = "New Tuning Profile", menuName = "FrostPunchGames/Tuning Profile")]
    public class RagdollTuningProfile : ScriptableObject
    {
        [Header("Master Rigidity")]
        [Tooltip("The total physical mass (in KG) distributed across the generated ragdoll's Rigidbodies.")]
        public float TotalWeight = 80f;

        [Tooltip("The baseline spring tension for all joints. Higher = robotic and stiff. Lower = sloppy and organic.")]
        public float MuscleSpring = 15000f;

        [Tooltip("The braking force applied to joint springs to prevent vibration. Balance this against MuscleSpring to prevent explosive physics jitter.")]
        public float MuscleDamper = 300f;

        [Range(0f, 1f)]
        [Tooltip("Global rigid-anchor weight pinning physical bones to the ghost pose. 0 = pure joint-drive following (default after the ArticulationBody rewrite).")]
        public float MuscleAnchorWeight = 0f;

        [Range(0f, 1f)]
        [Tooltip("Global joint-servo strength multiplier across the whole body.")]
        public float MuscleDriveWeight = 1f;

        [Tooltip("How fast the physical pose chases the ghost pose. Higher = snappier tracking.")]
        public float PoseTrackingSpeed = 5f;

        [Tooltip("Maximum motor torque any single joint drive may exert.")]
        public float ServoMaxForce = 150f;

        [Header("Gait & Step (IK Solver)")]
        [Range(0f, 1f)]
        [Tooltip("Master IK blend. 1 = full procedural foot/leg IK, 0 = raw animation pose.")]
        public float MasterIKBlend = 1f;

        [Tooltip("How high the foot lifts during the swing phase of a stride.")]
        public float StepHeight = 0.18f;

        [Range(0f, 1f)]
        [Tooltip("Multiplier for how far ahead the IK predicts foot placement based on velocity. High = long strides, Low = short chops.")]
        public float StepPrediction = 0.18f;

        [Tooltip("The speed threshold where the step rhythm switches from a walk cadence to a run cadence.")]
        public float RunSpeedThreshold = 6f;

        [Header("Hip Body Balancing")]
        [Range(0f, 1f)]
        [Tooltip("How strongly the hips shift their center of gravity over the planted foot while walking (Zero Moment Point). 1.0 = heavy waddle, 0.0 = hips stay dead center.")]
        public float ZMPWeightShiftWalking = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("How strongly the hips shift over the planted foot while idling.")]
        public float ZMPWeightShiftIdle = 0.1f;

        [Tooltip("How aggressively the torso leans in the opposite direction of a physical offset to counterbalance itself. If pushed left, it leans right.")]
        public float CounterBalanceTilt = 60f;

        [Tooltip("Multiplier for how far forward the spine leans dynamically based on forward velocity (naruto run).")]
        public float ForwardLeanMultiplier = 4f;

        [Tooltip("Multiplier for how far the spine leans sideways when strafing (banking into a curve).")]
        public float StrafeLeanMultiplier = 10f;

        [Tooltip("Hip contrapposto: how strongly the pelvis counter-rotates against the shoulders for a natural stance.")]
        public float ContrappostoStrength = 1f;

        [Tooltip("Pelvis vertical spring stiffness (bounce resistance for the hip body).")]
        public float HipsSpringStiffness = 25f;

        [Tooltip("Pelvis vertical spring damping (drag on the hip bounce).")]
        public float HipsSpringDamping = 6f;

        [Header("LIPM Balance Feedback")]
        [Tooltip("Master switch. When off, gait/hip behave exactly as before (pure heuristics).")]
        public bool UseBalanceFeedback = true;

        [Tooltip("Capture-point overshoot (m past the support edge) at which CP feedback reaches full authority. Smaller = CP takes over sooner.")]
        public float CPFullAuthorityMargin = 0.15f;

        [Tooltip("Capture-point overshoot (m past the support edge) beyond which a corrective step is forced.")]
        public float CPStepTriggerMargin = 0.04f;

        [Tooltip("Weight of the capture point when biasing the swing-leg foot placement target.")]
        public float CPFootBiasStrength = 1f;

        [Tooltip("Degrees of torso counter-lean per metre of capture-point error (LIPM ankle strategy).")]
        public float CPHipLeanGain = 30f;

        [Tooltip("LIPM hip strategy: how strongly the hips translate horizontally toward the support point to pull the COM back over the feet.")]
        public float CPHipShiftGain = 0.15f;

        [Tooltip("How strongly DCM divergence rate speeds up the gait cadence and lowers the step threshold under hard perturbation.")]
        public float CPStepUrgencyGain = 0.6f;

        [Tooltip("Draw the balance debug overlay (COM/CP/support polygon gizmos + HUD).")]
        public bool ShowBalanceDebug = false;

        [Header("Physics LOD")]
        [Tooltip("Enable distance-based level-of-detail downgrades.")]
        public bool LODEnable = true;

        [Tooltip("Distance at which LOD 1 (reduced physics rate, no stepping/arms) begins.")]
        public float LOD1Distance = 15f;

        [Tooltip("Distance at which LOD 2 (IK off) begins.")]
        public float LOD2Distance = 40f;

        [Tooltip("Distance at which LOD 3 (animation position only) begins.")]
        public float LOD3Distance = 80f;

        [Header("Balance Urgency (Phase-2)")]
        [Tooltip("COM-acceleration deviation (m/s^2) mapped to urgency = 1. ~0.5*g is a sane start; larger = harder to saturate.")]
        public float MaxExpectedAccel = 4.9f;

        [Tooltip("Urgency EMA time constant (s) when calm. Long => heavy smoothing => rejects animation jitter.")]
        public float UrgencyLongFilter = 0.05f;

        [Tooltip("Urgency EMA time constant (s) when urgent. Short => fast response to a real impact.")]
        public float UrgencyShortFilter = 0.01f;

        [Tooltip("How strongly the DCM divergence rate (capture point escaping support) folds into urgency.")]
        public float UrgencyDivergenceWeight = 0.15f;

        [Header("Arm Balance (Phase-2)")]
        [Tooltip("Hand displacement per metre of capture-point error (LIPM arm strategy gain).")]
        public float CPArmBalanceGain = 0.4f;

        [Tooltip("Below this urgency the arms fully keep their MagicBlend pose.")]
        public float ArmBalanceUrgencyLo = 0.3f;

        [Tooltip("At/above this urgency the arm balance offset reaches full strength.")]
        public float ArmBalanceUrgencyHi = 0.8f;

        [Tooltip("Maximum hand displacement (m) the balance offset may apply.")]
        public float ArmBalanceMaxHandOffset = 0.05f;

        [Tooltip("Smoothing time (s) for the applied hand offset; prevents pops when urgency changes quickly.")]
        public float ArmBalanceOffsetSmoothTime = 0.08f;

#if UNITY_EDITOR
        private void Reset()
        {
            AssignIcon();
        }

        [ContextMenu("Refresh Icon")]
        private void AssignIcon()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("ProfileIcon t:Texture2D");

            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                Texture2D icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                UnityEditor.EditorGUIUtility.SetIconForObject(this, icon);
            }
            else
            {
                Debug.LogWarning("Could not find an image named 'ProfileIcon' anywhere in the project.");
            }
        }
#endif
    }
}