using UnityEngine;
using System.Collections.Generic;

namespace FrostPunchGames
{
    [AddComponentMenu("Procedural Animation/IK Solver")]
    [DefaultExecutionOrder(-5)]
    public class IKSolver : MonoBehaviour
    {
        [Header("Physics Link")]
        [Tooltip("Reference to the physics syncer, allowing the IK solver to adapt to stumble mechanics and physical impacts.")]
        public ArticulationSyncer PhysicsSyncer;
        private StepManager _stepManager;

        [Header("LIPM Balance Feedback")]
        [Tooltip("Master switch. When off, gait/hip behave exactly as before (pure heuristics). When on, the physical capture point is blended into foot placement, step triggering and torso lean.")]
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
        [Tooltip("How strongly DCM divergence rate shortens step duration / lowers the step threshold (faster cadence under hard perturbation).")]
        public float CPStepUrgencyGain = 0.6f;
        [Tooltip("Phase-2 arm balance: desired COM shift per metre of capture-point error, mapped to a hand offset (ArmBalanceController).")]
        public float CPArmBalanceGain = 0.4f;

        [Header("Bone Configuration")]
        [Tooltip("The root transform of the character (usually the top level GameObject).")]
        public Transform CharacterRoot;
        [Tooltip("The hip bone of the animated ghost rig.")]
        public Transform Hips;
        [Tooltip("The head bone of the animated ghost rig.")]
        public Transform Head;
        [Tooltip("The Animator component driving the base movement that the IK will override.")]
        public Animator Animator;
        [SerializeField] private bool _autoDetectOnStart = true;

        [Header("Procedural Stance")]
        [Tooltip("If true, the solver blends its procedural IK on top of the playing animation. If false, it ignores the animation entirely and generates a pose from scratch.")]
        public bool UseAnimator = true;

        [Header("Hip & Body Control")]
        [Tooltip("Contains parameters for procedural weight shifting, spine leaning, and balancing. Expands in inspector.")]
        public HipBodySolver HipBody = new HipBodySolver();

        [Header("Leg Setup")]
        [Tooltip("A list of defined legs (Left, Right) and their corresponding bone chains. Auto-populated on generation.")]
        public List<LegSetup> Legs = new List<LegSetup>();

        [Range(0f, 0.5f)]
        [Tooltip("Forces the knees to bow slightly outward. Increases realism by preventing the knees from collapsing inward during deep crouches.")]
        public float KneeOutwardBias = 0.05f;
        [Range(0f, 1f), HideInInspector] public float IKBlend = 1f;
        [Range(0.1f, 20f), HideInInspector] public float IKTransitionSpeed = 6f;

        [Header("Gait Dynamics")]
        [Tooltip("Defines how far apart the feet should be placed based on the character's movement speed. X-axis is speed, Y-axis is width multiplier.")]
        public AnimationCurve StepWidthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(6f, 0.8f));

        [Tooltip("How quickly the internal velocity tracker smooths out sudden stops and starts. Higher = sharper response, lower = floatier, slide-heavy foot placement.")]
        public float VelocitySmoothing = 4f;

        [Header("Prediction")]
        [Range(0f, 1f)]
        [Tooltip("How far ahead (in seconds) the solver predicts the character will be when determining where to place the foot for the next step. High values create long, aggressive strides; low values cause short, choppy steps.")]
        public float StepPrediction = 0.18f;

        [Range(1f, 20f)]
        [Tooltip("How smoothly the prediction target moves. High values lock the target strictly to the velocity vector; lower values allow it to lazily drift.")]
        public float PredictionSmoothing = 4.0f;

        [Tooltip("The speed at which the solver stops predicting forward motion and begins predicting a dead stop, causing the character to plant their feet squarely.")]
        public float PredictionCutoffSpeed = 4.5f;

        [Range(0f, 0.5f)]
        [Tooltip("How much the character's angular rotation (turning) affects foot placement. High values cause the character to step wide into turns to catch their balance.")]
        public float TurnPrediction = 0.12f;

        [Header("Ground Detection")]
        [Tooltip("The physics layers that the IK solver considers solid ground. Ignore triggers and the ragdoll itself.")]
        public LayerMask GroundLayers = 1 << 0;
        [HideInInspector] public GroundDetector.ERaycastStyle RaycastStyle = GroundDetector.ERaycastStyle.StraightDown;
        [HideInInspector] public GroundDetector.ERaycastShape RaycastShape = GroundDetector.ERaycastShape.Linecast;

        [Header("Stepping Mechanics")]
        [Tooltip("The arc of the foot during a step. Y-axis is the height multiplier, X-axis is the normalized step progress (0 to 1).")]
        public AnimationCurve StepHeightCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

        [Tooltip("The absolute maximum vertical lift of the foot during a step. High values clear obstacles better but look like marching.")]
        public float StepHeight = 0.18f;

        [Range(0f, 1f)]
        [Tooltip("How eager the system is to lift a foot. 1.0 = feet snap up instantly when over-stretched. 0.1 = feet drag on the ground as long as possible before stepping.")]
        public float StepTriggerSensitivity = 0.5f;

        [Range(0f, 0.95f)]
        [Tooltip("Prevents rapid 'tap dancing'. Reduces step sensitivity immediately after a step is completed, allowing the character to settle.")]
        public float MaxSensitivityReduction = 0.7f;

        [Tooltip("The arc of the foot's rotation (pitch) during a step to simulate heel-toe rolling.")]
        public AnimationCurve FootRollCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

        [Tooltip("Maximum angle of the heel-toe foot roll during the peak of the step swing.")]
        public float FootRollStrength = 12f;

        [Tooltip("A flat vertical offset added to the foot target. Use this if the character's feet are clipping slightly into the floor mesh.")]
        public float FootHeightOffset = 0.0f;

        [Tooltip("Adds extra height to the step specifically to clear obstacles detected by raycasts in front of the foot.")]
        public float SwingClearance = 0.0f;

        [Tooltip("The absolute minimum distance allowed between the two feet. Prevents the legs from crossing over each other when strafing.")]
        public float MinLegSeparation = 0.08f;

        [Tooltip("The radius used to detect if the feet are currently overlapping or colliding with each other.")]
        public float FootOverlapRadius = 0.0f;

        [Header("Gait Thresholds")]
        [Tooltip("The velocity threshold at which the system switches from 'Walking' step rhythms to 'Running' step rhythms.")]
        public float RunSpeedThreshold = 6.0f;

        [Tooltip("When strafing sideways, offsets the leading foot forward by this amount to prevent the character from tripping over their own heels.")]
        public float StrafeForwardOffset = 0.06f;

        [Tooltip("The amount the hips twist to accommodate heavy sideways strafing.")]
        public float StrafeHipTwist = 15f;

        [Tooltip("If true, a planted foot is mathematically locked in world space and cannot slide, forcing the IK to stretch until a step is triggered.")]
        public bool UseFootGluing = true;

        [Range(0.05f, 0.4f)]
        [Tooltip("How far the stride can stretch beyond the baseline prediction when walking before a step is forced.")]
        public float IdleStrideStretching = 0.13f;

        [Range(0.4f, 1.0f)]
        [Tooltip("How far the stride can stretch when running. Higher values equal longer sprint strides.")]
        public float RunStrideStretching = 0.48f;

        [Tooltip("How quickly the stride length smoothly blends between the Idle and Run stretching values.")]
        public float StrideBlendSpeed = 4.0f;

        [Tooltip("The absolute minimum time (in seconds) a foot must be planted before it is allowed to lift again. Prevents machine-gun stepping.")]
        public float MinStepDuration = 0.3f;

        [Tooltip("The baseline time (in seconds) a foot is glued to the floor during a normal walking cadence.")]
        public float BaseGlueDuration = 0.18f;

        [Range(0.5f, 2f)]
        [Tooltip("Speed multiplier for the actual swing animation of the foot moving through the air. Higher = faster, snappier steps.")]
        public float SwingSpeedMultiplier = 1.15f;

        [Range(0.3f, 1f)]
        [Tooltip("Strictness of the gluing attachment. Lower values allow the foot to slide slightly into position before locking.")]
        public float GlueAttachStrictness = 0.75f;

        [Range(1f, 2f)]
        [Tooltip("Strictness of the detach threshold. Higher values require the leg to be stretched further before tearing off the floor.")]
        public float GlueDetachStrictness = 1.2f;

        public struct GaitParams
        {
            public float StrideLength;
            public float StepDuration;
            public float StepThreshold;
            public bool AllowFlight;
            public bool RequireSupport;
        }

        [Header("Step Heatmap")]
        [HideInInspector]
        public AnimationCurve HeatmapPenaltyCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));
        public AnimationCurve HeatmapSameSidePenaltyCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 1f), new Keyframe(0.8f, 0f));

        [Header("Airborne Behavior")]
        [Tooltip("If true, the IK legs will smoothly blend into a dangling, hanging pose when the character jumps or falls off a ledge.")]
        public bool UseHangingPose = true;

        [Range(1f, 15f)]
        [Tooltip("How fast the legs transition from a standing pose into the hanging airborne pose.")]
        public float AirborneTransitionSpeed = 6f;

        [Range(0f, 0.2f)]
        [Tooltip("How long the character must be detached from the ground before the hanging pose actually begins. Prevents dangling over tiny bumps.")]
        public float AirborneMinUngroundedTime = 0.1f;

        [Header("Alignment & Stance")]
        [Tooltip("How long (in seconds) the character must be stationary before they settle back into a perfect idle stance.")]
        public float StanceSettleTime = 0.25f;

        [Tooltip("If true, the ankle rotates to perfectly match the normal (slope) of the ground it is planted on.")]
        public bool AlignFeetToGround = true;

        [Range(0f, 1f)]
        [Tooltip("A master blend for foot alignment. 1.0 = feet map perfectly to slopes. 0.0 = feet remain perfectly flat (ignoring slopes).")]
        public float FootAlignmentBlend = 1f;

        [Tooltip("Forces the feet to snap downwards to maintain ground contact, preventing floating feet over dips in the terrain.")]
        public bool EnforceGroundContact = true;

        [Range(5f, 30f)]
        [Tooltip("The speed at which the foot snaps down to touch the ground when Enforce Ground Contact is true.")]
        public float GroundContactSpeed = 18f;

        [Header("Performance & Debug")]
        [Tooltip("If true, IK solvers run on unscaled time, ignoring slow-motion effects. Usually leave false.")]
        public bool UseUnscaledTime = false;

        [Tooltip("Draws lines and spheres in the Scene view to visualize raycasts, foot targets, and ground alignment.")]
        public bool DrawDebug = false;
        public Color DebugColor = Color.green;
        [HideInInspector] public bool UseAdditiveMode = true;
        [HideInInspector] public float RaycastHeightFromFoot = 0.12f;

        [Tooltip("If the Animator has no controller or is disabled, this forces the system to generate a rigid standing pose purely via math.")]
        public bool ForceProceduralPoseIfNoAnimation = true;

        public float DeltaTime { get; private set; }
        public float ScaleReference { get; private set; }
        public bool IsInitialized { get; private set; }
        public float CurrentIKBlend { get; private set; }
        public Vector3 SmoothedVelocity { get; private set; }
        public Vector3 CurrentRawVelocity { get; private set; }
        public float AngularVelocity { get; private set; }
        public bool IsAccelerating { get; private set; }

        // --- LIPM balance feedback (read-only convenience over PhysicsSyncer.capturePoint) ---
        // All gait/hip consumers go through these so the null gating + coordinate handling live in one place.
        public bool BalanceFeedbackActive =>
            UseBalanceFeedback
            && PhysicsSyncer != null
            && PhysicsSyncer.capturePoint != null
            && PhysicsSyncer.capturePoint.IsSupportValid
            && !PhysicsSyncer.IsFullyCollapsed;

        // World-space capture point from the physical rig (XZ meaningful; Y is the support plane).
        public Vector3 CapturePointWorld => PhysicsSyncer != null && PhysicsSyncer.capturePoint != null
            ? PhysicsSyncer.capturePoint.CapturePoint
            : Vector3.zero;

        // Signed distance of the CP to the support polygon edge: negative inside, positive outside.
        public float CaptureSignedDistance => PhysicsSyncer != null && PhysicsSyncer.capturePoint != null
            ? PhysicsSyncer.capturePoint.SignedDistanceToSupportEdge
            : -1f;

        public CapturePointSolver.BalanceState BalanceState => PhysicsSyncer != null && PhysicsSyncer.capturePoint != null
            ? PhysicsSyncer.capturePoint.State
            : CapturePointSolver.BalanceState.Balanced;

        // 0 when CP is inside the support polygon (heuristics rule), ramps to 1 as the CP overshoots
        // past CPFullAuthorityMargin (CP correction takes over). 0 whenever feedback is inactive.
        public float CaptureAuthority
        {
            get
            {
                if (!BalanceFeedbackActive) return 0f;
                // Manual-override preview: when the arbiter is pinned, route the previewed urgency
                // into the gait/hip authority so the editor slider reaches the gait-IK (step cadence)
                // and hip strategy. The muscle + arm subsystems already read Urgency directly; this
                // closes the loop so all three respond to Manual Override. Live runtime keeps using the
                // geometric capture-point distance below.
                var arbiter = PhysicsSyncer != null ? PhysicsSyncer.urgency : null;
                if (arbiter != null && arbiter.overrideUrgency) return Mathf.Clamp01(arbiter.overrideValue);
                float margin = Mathf.Max(0.001f, CPFullAuthorityMargin);
                return Mathf.Clamp01(CaptureSignedDistance / margin);
            }
        }

        // LIPM natural frequency omega = sqrt(g/h) from the physical rig. 0 when feedback inactive.
        public float CaptureOmega => PhysicsSyncer != null && PhysicsSyncer.capturePoint != null
            ? PhysicsSyncer.capturePoint.Omega
            : 0f;

        // World-space COM velocity of the physical rig (XZ meaningful). Zero when unavailable.
        public Vector3 CaptureComVelocity => PhysicsSyncer != null && PhysicsSyncer.capturePoint != null
            ? PhysicsSyncer.capturePoint.ComVelocity
            : Vector3.zero;

        // Rate at which the DCM is escaping the support polygon (0 while inside or inactive).
        public float CaptureDivergenceRate => PhysicsSyncer != null && PhysicsSyncer.capturePoint != null
            ? PhysicsSyncer.capturePoint.DcmDivergenceRate
            : 0f;

        // World-space centroid of the current support polygon (mean of the convex-hull ring).
        // Used by ArmBalanceController to derive the capture-point error direction. Falls back to
        // the capture point itself (zero error) when the hull is unavailable.
        public Vector3 SupportCentroidWorld
        {
            get
            {
                if (PhysicsSyncer == null || PhysicsSyncer.capturePoint == null) return Vector3.zero;
                var hull = PhysicsSyncer.capturePoint.DebugHullWorld;
                if (hull == null || hull.Count == 0) return PhysicsSyncer.capturePoint.CapturePoint;
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < hull.Count; i++) sum += hull[i];
                return sum / hull.Count;
            }
        }

        private Vector3 _lastRootPosition;
        private Quaternion _lastRootRotation;
        private float _currentSpeed;
        private Vector3 _currentDir;
        private List<LegController> _legControllers = new List<LegController>();
        private float _ikBlendVelocity;
        private Vector3 _lastFrameRootPos;

        private void OnEnable()
        {
            if (CharacterRoot != null)
            {
                _lastRootPosition = CharacterRoot.position;
                _lastRootRotation = CharacterRoot.rotation;
                _currentDir = CharacterRoot.forward;
                _lastFrameRootPos = CharacterRoot.position;
            }
        }
        private void Awake() => ValidateSetup();
        private void Start()
        {
            Initialize();
            if (CharacterRoot != null)
            {
                _lastRootPosition = CharacterRoot.position;
                _lastRootRotation = CharacterRoot.rotation;
                _currentDir = CharacterRoot.forward;
                _lastFrameRootPos = CharacterRoot.position;
            }

            if (PhysicsSyncer != null)
            {
                DeltaTime = Time.fixedDeltaTime;
                foreach (var leg in _legControllers) leg.PreUpdate();
                RunIKPass();
            }
        }

        private void FixedUpdate()
        {
            if (!IsInitialized || PhysicsSyncer == null) return;

            DeltaTime = Time.fixedDeltaTime;
            UpdateBlends();
            UpdateMotionDynamics();
            foreach (var leg in _legControllers) leg.PreUpdate();

            RunIKPass();
        }

        private void Update()
        {
            if (!IsInitialized || PhysicsSyncer != null) return;

            UpdateDeltaTime();
            UpdateBlends();
            UpdateMotionDynamics();
            foreach (var leg in _legControllers) leg.PreUpdate();
        }

        private void LateUpdate()
        {
            if (!IsInitialized || PhysicsSyncer != null) return;

            RunIKPass();
        }

        // Routes the animator's root motion into CharacterRoot so it drives BOTH the IK
        // locomotion (UpdateMotionDynamics reads this transform) and, since the ghost Hips
        // ride along, the physical rig's root-follow PD (translation force + turning torque).
        // In Fixed update mode this fires inside the physics loop, so deltaPosition/deltaRotation
        // are per-fixed-step increments coherent with the PD's clock.
        private void OnAnimatorMove()
        {
            if (Animator == null || !Animator.applyRootMotion) return;
            Transform root = CharacterRoot != null ? CharacterRoot : transform;
            root.position += Animator.deltaPosition;
            root.rotation = Animator.deltaRotation * root.rotation;
        }

        private void RunIKPass()
        {
            if (CurrentIKBlend > 0.001f)
            {
                HipBody.Process();
            }

            bool hasValidAnimator = Animator != null && Animator.enabled && Animator.runtimeAnimatorController != null;
            bool shouldUseAnimator = UseAnimator && hasValidAnimator;
            if (ForceProceduralPoseIfNoAnimation && !hasValidAnimator) shouldUseAnimator = false;

            foreach (var leg in _legControllers)
            {
                if (shouldUseAnimator) leg.CaptureAnimatedPose();
                else leg.CalculateProceduralPose();
            }
            UpdateLegRaycasting();
            UpdateLegIK();
            ApplyLegIK();
            if (DrawDebug) DrawDebugVisualization();
        }

        private void ValidateSetup()
        {
            if (CharacterRoot == null) CharacterRoot = transform;
            if (Animator == null) Animator = GetComponent<Animator>();
        }
        public void Initialize()
        {
            if (IsInitialized) return;
            if (_autoDetectOnStart && Legs.Count == 0 && Animator != null && Animator.isHuman) AutoDetectHumanoidLegBones();
            if (Hips == null && Animator != null && Animator.isHuman) Hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            HipBody.Initialize(this);
            ScaleReference = CalculateScaleReference();
            _legControllers.Clear();
            foreach (var legSetup in Legs)
            {
                if (legSetup.IsValid)
                {
                    var controller = new LegController(this, legSetup);
                    controller.Initialize();
                    controller.ConfigureHangingStateMachine(AirborneTransitionSpeed, AirborneMinUngroundedTime);
                    _legControllers.Add(controller);
                }
            }
            _stepManager = GetComponent<StepManager>();
            if (_stepManager == null) _stepManager = gameObject.AddComponent<StepManager>();
            foreach (var leg in _legControllers) _stepManager.RegisterLeg(leg);
            CurrentIKBlend = IKBlend;
            IsInitialized = true;
        }
        public void CaptureFallenPoses()
        {
            foreach (var leg in _legControllers) leg.CaptureFallenPose();
        }
        public void AutoDetectHumanoidLegBones()
        {
            if (Animator == null) Animator = GetComponent<Animator>();
            if (Animator == null || !Animator.isHuman) return;
            Legs.Clear();
            if (Hips == null) Hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            if (Head == null) Head = Animator.GetBoneTransform(HumanBodyBones.Head);

            var lUpper = Animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var lLower = Animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var lFoot = Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var lToes = Animator.GetBoneTransform(HumanBodyBones.LeftToes);
            if (lUpper && lLower && lFoot) Legs.Add(new LegSetup { Name = "Left Leg", Side = LegSide.Left, UpperLeg = lUpper, LowerLeg = lLower, Foot = lFoot, Toe = lToes });

            var rUpper = Animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var rLower = Animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            var rFoot = Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var rToes = Animator.GetBoneTransform(HumanBodyBones.RightToes);
            if (rUpper && rLower && rFoot) Legs.Add(new LegSetup { Name = "Right Leg", Side = LegSide.Right, UpperLeg = rUpper, LowerLeg = rLower, Foot = rFoot, Toe = rToes });
        }

        public void DetectBones() { AutoDetectHumanoidLegBones(); Reinitialize(); }
        public void Reinitialize() { IsInitialized = false; Initialize(); }

        private float CalculateScaleReference()
        {
            if (CharacterRoot == null) return 1f;
            float scale = CharacterRoot.lossyScale.y;
            if (Hips != null)
            {
                float hipsHeight = Hips.position.y - CharacterRoot.position.y;
                if (hipsHeight > 0.1f) scale = Mathf.Max(scale, hipsHeight);
            }
            return Mathf.Max(0.1f, scale);
        }

        private void UpdateDeltaTime() => DeltaTime = UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        private void UpdateBlends() => CurrentIKBlend = Mathf.SmoothDamp(CurrentIKBlend, IKBlend, ref _ikBlendVelocity, 1f / IKTransitionSpeed, 100f, DeltaTime);
        private void UpdateMotionDynamics()
        {
            if (DeltaTime <= 0f) return;
            Vector3 currentPos = CharacterRoot.position;
            Vector3 rawVelocity = (currentPos - _lastRootPosition) / DeltaTime;

            if (rawVelocity.sqrMagnitude > 400f)
            {
                rawVelocity = Vector3.zero;
                _currentSpeed = 0f;
                SmoothedVelocity = Vector3.zero;
            }

            CurrentRawVelocity = rawVelocity;

            float rawSpeed = rawVelocity.magnitude;
            float smoothSpeed = SmoothedVelocity.magnitude;
            float effectiveSmoothing = (rawSpeed > smoothSpeed) ? 25f : VelocitySmoothing;
            if (rawSpeed < 0.1f) effectiveSmoothing = 40f;
            _currentSpeed = Mathf.Lerp(_currentSpeed, rawSpeed, DeltaTime * effectiveSmoothing);
            Vector3 rawDir = (rawSpeed > 0.001f) ? rawVelocity.normalized : _currentDir;
            _currentDir = Vector3.Slerp(_currentDir, rawDir, DeltaTime * 25f);
            SmoothedVelocity = _currentDir * _currentSpeed;

            Quaternion currentRot = CharacterRoot.rotation;
            float angleDiff = Quaternion.Angle(_lastRootRotation, currentRot);
            Vector3 cross = Vector3.Cross(_lastRootRotation * Vector3.forward, currentRot * Vector3.forward);
            float dir = Vector3.Dot(cross, Vector3.up);
            float sign = (dir >= 0) ? 1f : -1f;
            float rawAngular = (angleDiff / DeltaTime) * sign;
            AngularVelocity = Mathf.Lerp(AngularVelocity, rawAngular, DeltaTime * 8f);
            _lastRootPosition = currentPos;
            _lastRootRotation = currentRot;
        }

        public void AddHipImpact(Vector3 force, Vector3 hitPoint)
        {
            HipBody.ApplyImpact(force, hitPoint);
        }

        private void UpdateLegRaycasting() { foreach (var leg in _legControllers) leg.UpdateRaycasting(); }
        private void UpdateLegIK() { foreach (var leg in _legControllers) leg.UpdateIK(); }
        private void ApplyLegIK() { foreach (var leg in _legControllers) { leg.ApplyIK(); leg.PostApplyIK(); } }
        private void DrawDebugVisualization() { foreach (var leg in _legControllers) leg.DrawDebug(DebugColor); }

        public GaitParams CalculateGait()
        {
            Vector3 rawVelocity = CurrentRawVelocity;
            rawVelocity.y = 0f;

            float smoothSpeed = SmoothedVelocity.magnitude;
            GaitParams gait = new GaitParams();

            float speedFactor = Mathf.Clamp01(smoothSpeed / StrideBlendSpeed);
            float strideStretch = Mathf.Lerp(IdleStrideStretching, RunStrideStretching, speedFactor);
            float legLen = GetAverageLegLength();

            gait.StrideLength = strideStretch * legLen * 2.2f;
            gait.StepThreshold = gait.StrideLength * 0.45f;

            float dynamicDuration = BaseGlueDuration / (1f + (smoothSpeed * 0.8f));
            gait.StepDuration = Mathf.Clamp(dynamicDuration, MinStepDuration, BaseGlueDuration);

            // DCM step modulation: the further/faster the capture point is diverging, the more urgent
            // the corrective step. Higher urgency -> shorter step duration (faster cadence) and a lower
            // step threshold (step sooner). Authority alone covers a static overshoot; the divergence
            // rate term reacts to how fast it is escaping.
            if (BalanceFeedbackActive)
            {
                float urgency = Mathf.Clamp01(CaptureAuthority + CaptureDivergenceRate * CPStepUrgencyGain * 0.1f);
                gait.StepDuration = Mathf.Lerp(gait.StepDuration, MinStepDuration, urgency);
                gait.StepThreshold = Mathf.Lerp(gait.StepThreshold, gait.StepThreshold * 0.4f, urgency);
            }

            IsAccelerating = (rawVelocity.magnitude > smoothSpeed + 0.15f) && (rawVelocity.magnitude > 0.1f);

            gait.RequireSupport = true;
            gait.AllowFlight = false;

            return gait;
        }
        public void ResetBlendTarget(float startValue)
        {
            CurrentIKBlend = startValue;
            _ikBlendVelocity = 0f;
        }
        private float GetAverageLegLength()
        {
            if (_legControllers.Count == 0) return 1f;
            float t = 0; foreach (var l in _legControllers) t += l.GetLegLength();
            return t / _legControllers.Count;
        }
        public float GetEffectiveStepPrediction() => StepPrediction;
        public void ForceReglueAll() { foreach (var leg in _legControllers) leg.ForceReglue(); }
        public LegController GetLegController(int index) => (index >= 0 && index < _legControllers.Count) ? _legControllers[index] : null;
        public int GetLegCount() => _legControllers.Count;
    }
}