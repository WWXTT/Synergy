namespace FrostPunchGames
{
    using UnityEngine;
    using System.Collections.Generic;
    using System.Linq;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    public class ActiveRagdollBrain : MonoBehaviour
    {
        [Header("Build Settings")]
        [Tooltip("Defines which physics layers are considered walkable surfaces. The IK Solver uses this to plant feet and ground-check. If set incorrectly, the ragdoll will ignore the floor and fall through the world.")]
        public LayerMask GroundLayer = 1 << 0;

        [Tooltip("Defines which physics layers can trigger stumble mechanics or muscle relaxation on impact. If an object's layer isn't in this mask, the ragdoll will treat it as a ghost and won't react to being hit by it.")]
        public LayerMask ImpactLayers = -1;

        [Tooltip("The specific physics layer index (0-31) that will be recursively assigned to the Physical Rig's bones. Used to ensure the Ghost Rig doesn't physically collide with the Physical Rig it's trying to drive.")]
        public int RagdollLayerIndex = 0;

        [Header("Master Tuning")]
        [Tooltip("The central data container holding all physics values (muscle springs, hover forces, step prediction, etc.). Swapping this out at runtime will instantly alter how the ragdoll balances, walks, and reacts to physics.")]
        public RagdollTuningProfile ActiveProfile;

        [Header("Hierarchy References")]
        [Tooltip("The hidden, kinematically-animated rig. It solves procedural IK and calculates the 'ideal' pose. If this is missing or deleted, the Physical Rig will have no target to follow and will immediately collapse.")]
        public GameObject GhostRig;

        [Tooltip("The visible, physics-driven rig made of Rigidbodies and ConfigurableJoints. If this is missing, the brain cannot apply physical forces, and the character will not interact with the world.")]
        public GameObject PhysicalRig;

        [Tooltip("An optional, stripped-down rig used for casting specific shadows or housing trigger colliders without interfering with the heavy joint physics. If missing, the system safely ignores it.")]
        public GameObject ShadowMimic;

        // Mirrors the source Animator's Apply Root Motion at generation time, so the reconnect
        // path can restore the same setting on the ghost without re-reading a (destroyed) source.
        [HideInInspector] public bool ghostApplyRootMotion;

        [HideInInspector] public ArticulationSyncer hiddenSyncer;
        [HideInInspector] public RagdollRootFollower hiddenRootFollower;
        [HideInInspector] public IKSolver hiddenIKSolver;
        [HideInInspector] public StepManager hiddenStepManager;
        [HideInInspector] public ShadowRigFollower hiddenShadowFollower;
        [HideInInspector] public RagdollBalanceDebug hiddenBalanceDebug;
        [HideInInspector] public PhysicsLODController hiddenLOD;
        [HideInInspector] public CharacterPerimeter hiddenPerimeter;

        [Header("Phase-2 Hidden References")]
        [HideInInspector] public BalanceUrgencyEvaluator hiddenUrgency;
        [HideInInspector] public ArmBalanceController hiddenArmBalance;
        [HideInInspector] public PhysicsPoseBlender hiddenPoseBlender;
        // NOTE: layer 2 (MagicBlend fusion) is controlled by the plugin's own globalWeight on the
        // animation side, fully independent of layer 3 (physics). The old MagicBlendUrgencyBridge that
        // coupled physics-urgency into globalWeight has been removed — each layer owns one parameter.

        // Wires up all internal references, re-applies layer masks, and syncs the tuning profile.
        // Called once at the end of generation (SetupEverything). NOT a user-facing refresh: the joint
        // authoring is built only by SimpleRagdollBuilder during generation, so any joint-config change
        // requires a full regenerate, not a re-wire.
        private void WireReferences()
        {
            if (PhysicalRig == null) PhysicalRig = transform.Find("Physical_Rig")?.gameObject;
            if (GhostRig == null) GhostRig = transform.Find("Ghost_Rig")?.gameObject;

            if (ShadowMimic == null)
            {
                ShadowMimic = transform.Find("Shadow_Mimic")?.gameObject;
                if (ShadowMimic == null && GhostRig != null)
                    ShadowMimic = GhostRig.transform.Find("Shadow_Mimic")?.gameObject;
            }

            if (PhysicalRig == null || GhostRig == null)
            {
                Debug.LogError($"<color=red>Brain:</color> Missing Rig References.");
                return;
            }

            if (GhostRig != null)
            {
                SetLayerRecursively(GhostRig.transform, RagdollLayerIndex);
            }

            if (PhysicalRig != null)
            {
                SetLayerRecursively(PhysicalRig.transform, RagdollLayerIndex);
            }

            hiddenSyncer = PhysicalRig.GetComponent<ArticulationSyncer>();
            hiddenRootFollower = PhysicalRig.GetComponent<RagdollRootFollower>();
            hiddenIKSolver = GhostRig.GetComponent<IKSolver>();
            hiddenStepManager = GhostRig.GetComponent<StepManager>();
            hiddenArmBalance = GhostRig.GetComponent<ArmBalanceController>();
            hiddenPoseBlender = GhostRig.GetComponent<PhysicsPoseBlender>();
            hiddenUrgency = PhysicalRig.GetComponent<BalanceUrgencyEvaluator>();
            if (ShadowMimic != null) hiddenShadowFollower = ShadowMimic.GetComponent<ShadowRigFollower>();

            var ghostAnim = GhostRig.GetComponent<Animator>();
            if (ghostAnim != null)
            {
                ghostAnim.applyRootMotion = ghostApplyRootMotion;
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
                ghostAnim.updateMode = AnimatorUpdateMode.Fixed;
#else
                ghostAnim.updateMode = AnimatorUpdateMode.AnimatePhysics;
#endif
                ghostAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            // The Animator lives only on the Ghost_Rig. Physical_Rig has no Animator, so its head/hips
            // bones are resolved by name (Ghost is a structural clone, so bone names match 1:1).
            Transform ghostHead = ghostAnim != null ? ghostAnim.GetBoneTransform(HumanBodyBones.Head) : null;
            Transform ghostHips = ghostAnim != null ? ghostAnim.GetBoneTransform(HumanBodyBones.Hips) : null;
            Transform physHead = ghostHead != null ? FindBoneByName(PhysicalRig.transform, ghostHead.name) : null;
            Transform physHips = ghostHips != null ? FindBoneByName(PhysicalRig.transform, ghostHips.name) : null;

            LayerMask safeGroundMask = GroundLayer;
            if (RagdollLayerIndex != 0) safeGroundMask &= ~(1 << RagdollLayerIndex);

            if (RagdollLayerIndex != 0)
            {
                SetLayerRecursive(PhysicalRig, RagdollLayerIndex);

                if (GhostRig.TryGetComponent<Rigidbody>(out var ghostRbReinit))
                {
#if UNITY_2022_2_OR_NEWER
                    ghostRbReinit.excludeLayers = 1 << RagdollLayerIndex;
#endif
                }

                if (ShadowMimic != null)
                {
                    foreach (var col in ShadowMimic.GetComponentsInChildren<Collider>())
                    {
#if UNITY_2022_2_OR_NEWER
                        col.excludeLayers = 1 << RagdollLayerIndex;
#endif
                    }
                }
            }

            if (hiddenSyncer != null)
            {
                hiddenSyncer.ghostRoot = GhostRig.transform;
                hiddenSyncer.physRoot = PhysicalRig.transform;
                if (physHead != null) hiddenSyncer.physHead = physHead;
                hiddenSyncer.impactLayers = ImpactLayers;
            }

            if (hiddenIKSolver != null)
            {
                hiddenIKSolver.CharacterRoot = GhostRig.transform;
                hiddenIKSolver.Animator = ghostAnim;
                if (ghostHips != null) hiddenIKSolver.Hips = ghostHips;
                if (ghostHead != null) hiddenIKSolver.Head = ghostHead;
                hiddenIKSolver.GroundLayers = safeGroundMask;
                hiddenIKSolver.Reinitialize();
            }

            if (hiddenRootFollower != null && physHips != null)
            {
                hiddenRootFollower.physicsHips = physHips;
            }

            if (hiddenShadowFollower != null && GhostRig != null && ShadowMimic != null)
            {
                // Render flip: the Shadow_Mimic (outward collision proxy) now follows the VISIBLE
                // Ghost_Rig animation pose, not the hidden Physical_Rig. Ghost and Shadow are clones of
                // the same source hierarchy, but Shadow is parented UNDER Ghost, so Ghost's transform
                // list contains the Shadow subtree and won't index-align. Pair by relative path instead.
                var shadowBones = ShadowMimic.GetComponentsInChildren<Transform>(true);
                var sourceList = new System.Collections.Generic.List<Transform>(shadowBones.Length);
                var shadowList = new System.Collections.Generic.List<Transform>(shadowBones.Length);
                Transform shadowRoot = ShadowMimic.transform;
                Transform ghostRoot = GhostRig.transform;
                foreach (var sb in shadowBones)
                {
                    string rel = RelativePath(sb, shadowRoot);
                    Transform gb = rel.Length == 0 ? ghostRoot : ghostRoot.Find(rel);
                    if (gb != null)
                    {
                        sourceList.Add(gb);
                        shadowList.Add(sb);
                    }
                }
                hiddenShadowFollower.PhysicsBones = sourceList.ToArray();
                hiddenShadowFollower.ShadowBones = shadowList.ToArray();
            }

            // Phase-2 arbiter: the syncer reads urgency to blend the effective COM velocity, and the
            // evaluator reads the syncer's bone pairing to synthesize the ghost COM.
            if (hiddenUrgency != null)
            {
                hiddenUrgency.syncer = hiddenSyncer;
                if (hiddenSyncer != null) hiddenSyncer.urgency = hiddenUrgency;
            }

            if (hiddenArmBalance != null)
            {
                hiddenArmBalance.Configure(hiddenIKSolver, hiddenUrgency, GhostRig.transform);
                MapArmBalanceBones(hiddenArmBalance, ghostAnim);
            }

            if (hiddenPoseBlender != null)
            {
                hiddenPoseBlender.Configure(hiddenUrgency);
                MapPoseBlenderChain(hiddenPoseBlender, ghostAnim);
            }

            ApplyProfile(ActiveProfile);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (hiddenSyncer != null) EditorUtility.SetDirty(hiddenSyncer);
                if (hiddenIKSolver != null) EditorUtility.SetDirty(hiddenIKSolver);
                if (PhysicalRig != null) EditorUtility.SetDirty(PhysicalRig);
                if (GhostRig != null) EditorUtility.SetDirty(GhostRig);


                if (gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif


            Debug.Log($"<color=cyan>Wired:</color> Brain reference sync complete. Layers applied.");
        }

        public void ApplyProfile(RagdollTuningProfile profile)
        {
            if (profile == null) return;
            ActiveProfile = profile;

            if (hiddenSyncer != null)
            {
                hiddenSyncer.driveSpring = profile.MuscleSpring;
                hiddenSyncer.driveDamper = profile.MuscleDamper;
                hiddenSyncer.masterAnchorWeight = profile.MuscleAnchorWeight;
                hiddenSyncer.masterDriveWeight = profile.MuscleDriveWeight;
                hiddenSyncer.poseTrackingSpeed = profile.PoseTrackingSpeed;
                hiddenSyncer.servoMaxForce = profile.ServoMaxForce;
            }

            if (hiddenIKSolver != null)
            {
                hiddenIKSolver.IKBlend = profile.MasterIKBlend;
                hiddenIKSolver.StepHeight = profile.StepHeight;
                hiddenIKSolver.StepPrediction = profile.StepPrediction;
                hiddenIKSolver.RunSpeedThreshold = profile.RunSpeedThreshold;

                hiddenIKSolver.HipBody.ZMPWeightShiftWalking = profile.ZMPWeightShiftWalking;
                hiddenIKSolver.HipBody.ZMPWeightShiftIdle = profile.ZMPWeightShiftIdle;
                hiddenIKSolver.HipBody.CounterBalanceTilt = profile.CounterBalanceTilt;
                hiddenIKSolver.HipBody.ForwardLeanMultiplier = profile.ForwardLeanMultiplier;
                hiddenIKSolver.HipBody.StrafeLeanMultiplier = profile.StrafeLeanMultiplier;
                hiddenIKSolver.HipBody.ContrappostoStrength = profile.ContrappostoStrength;
                hiddenIKSolver.HipBody.HipsSpringStiffness = profile.HipsSpringStiffness;
                hiddenIKSolver.HipBody.HipsSpringDamping = profile.HipsSpringDamping;

                hiddenIKSolver.UseBalanceFeedback = profile.UseBalanceFeedback;
                hiddenIKSolver.CPFullAuthorityMargin = profile.CPFullAuthorityMargin;
                hiddenIKSolver.CPStepTriggerMargin = profile.CPStepTriggerMargin;
                hiddenIKSolver.CPFootBiasStrength = profile.CPFootBiasStrength;
                hiddenIKSolver.CPHipLeanGain = profile.CPHipLeanGain;
                hiddenIKSolver.CPHipShiftGain = profile.CPHipShiftGain;
                hiddenIKSolver.CPStepUrgencyGain = profile.CPStepUrgencyGain;
                hiddenIKSolver.CPArmBalanceGain = profile.CPArmBalanceGain;
            }

            if (hiddenUrgency != null)
            {
                hiddenUrgency.maxExpectedAccel = profile.MaxExpectedAccel;
                hiddenUrgency.longFilter = profile.UrgencyLongFilter;
                hiddenUrgency.shortFilter = profile.UrgencyShortFilter;
                hiddenUrgency.divergenceWeight = profile.UrgencyDivergenceWeight;
            }

            if (hiddenArmBalance != null)
            {
                hiddenArmBalance.urgencyLo = profile.ArmBalanceUrgencyLo;
                hiddenArmBalance.urgencyHi = profile.ArmBalanceUrgencyHi;
                hiddenArmBalance.maxHandOffset = profile.ArmBalanceMaxHandOffset;
                hiddenArmBalance.offsetSmoothTime = profile.ArmBalanceOffsetSmoothTime;
            }

            if (hiddenBalanceDebug != null)
            {
                hiddenBalanceDebug.drawDebug = profile.ShowBalanceDebug;
            }

            if (hiddenLOD != null)
            {
                hiddenLOD.lodEnabled = profile.LODEnable;
                hiddenLOD.lod1Distance = profile.LOD1Distance;
                hiddenLOD.lod2Distance = profile.LOD2Distance;
                hiddenLOD.lod3Distance = profile.LOD3Distance;
            }
        }

        [ContextMenu("Generate Complete Active Ragdoll")]
        public void SetupEverything()
        {
#if UNITY_EDITOR
            if (ActiveProfile == null)
            {
                Debug.LogError("Generation Failed: Please assign a Tuning Profile first!");
                return;
            }

            Animator anim = GetComponent<Animator>();
            if (anim == null) { Debug.LogError("No Animator found!"); return; }
            // Root motion temporarily disabled: feeding the clip's root motion into the ghost root made
            // the rig drift/lean forward. The plumbing (ghostApplyRootMotion + OnAnimatorMove) is kept
            // for when locomotion is wired up; to re-enable, restore: ghostApplyRootMotion = anim.applyRootMotion;
            ghostApplyRootMotion = false;

            Transform hipsBone = anim.GetBoneTransform(HumanBodyBones.Hips);
            Transform headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            if (hipsBone == null) { Debug.LogError("No Hips bone found on Animator!"); return; }

            Vector3 originalPos = transform.position;
            Quaternion originalRot = transform.rotation;
            string rootName = gameObject.name;

            GameObject masterRoot = new GameObject(rootName + "_RobotRoot");
            masterRoot.transform.position = originalPos;
            masterRoot.transform.rotation = originalRot;

            ActiveRagdollBrain rootMaster = masterRoot.AddComponent<ActiveRagdollBrain>();
            CopySettingsTo(rootMaster);

            // Pose the source rig into its upright animated pose BEFORE the ArticulationBodies are
            // created. The joint reduced-coordinate zero is fixed at body-creation time; if the rig is
            // at bind/T-pose here, every muscle drive must command a large T-pose->upright rotation,
            // which ToReducedSpace maps lossily/with bias and produces a forward-leaned steady state.
            // Sampling upright first makes steady-state drive targets ~= identity (small, accurate).
            SampleDefaultPose(anim);

            SimpleRagdollBuilder.InitializeRagdoll(anim, ActiveProfile.TotalWeight);

            PhysicalRig = this.gameObject;
            PhysicalRig.name = "Physical_Rig";
            SetLayerRecursive(PhysicalRig, RagdollLayerIndex);

            var syncer = PhysicalRig.GetComponent<ArticulationSyncer>() ?? PhysicalRig.AddComponent<ArticulationSyncer>();
            syncer.hideFlags = HideFlags.HideInInspector;
            rootMaster.hiddenSyncer = syncer;

            var rootFollower = PhysicalRig.GetComponent<RagdollRootFollower>() ?? PhysicalRig.AddComponent<RagdollRootFollower>();
            rootFollower.hideFlags = HideFlags.HideInInspector;
            rootMaster.hiddenRootFollower = rootFollower;

            syncer.masterAnchorWeight = 0f; // drive-only: ArticulationBody joints maintain the pose; hips world-follow handled by root-follow PD
            syncer.masterDriveWeight = 1f;
            syncer.poseTrackingSpeed = 5f;
            syncer.driveSpring = ActiveProfile.MuscleSpring;
            syncer.driveDamper = ActiveProfile.MuscleDamper;
            syncer.servoMaxForce = 150f;
            if (headBone != null) syncer.physHead = headBone;
            syncer.enableFallLogic = true;
            syncer.collisionSensitivity = 1f;
            syncer.fallThreshold = 0.5f;
            syncer.maxLeanAngle = 35f;
            syncer.standUpHeight = 0.9f;

            syncer.impactLayers = ImpactLayers;

            GhostRig = Instantiate(this.gameObject, transform.position, transform.rotation);
            GhostRig.name = "Ghost_Rig";

            // Render flip (3-layer enhancer model): the Ghost_Rig is now the ONLY visible skeleton —
            // it plays the animation (Animator + MagicBlend + IK) plus the post-overlay physics blend,
            // so it keeps its SkinnedMeshRenderers. The Physical_Rig becomes a non-rendered physics
            // reference/sensing layer; strip its visuals so the player never sees its raw physics fold.
            // Must strip AFTER the Ghost clone is taken above, or the clone would inherit the stripped state.
            StripVisuals(PhysicalRig);

            Animator ghostAnim = GhostRig.GetComponent<Animator>();
            ghostAnim.enabled = true;
            ghostAnim.applyRootMotion = ghostApplyRootMotion;
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
            ghostAnim.updateMode = AnimatorUpdateMode.Fixed;
#else
            ghostAnim.updateMode = AnimatorUpdateMode.AnimatePhysics;
#endif
            ghostAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // NOTE: GhostRig keeps its visuals (render flip) — it is the visible animated skeleton.
            StripScripts(GhostRig, typeof(ArticulationSyncer), typeof(RagdollRootFollower));
            CleanupGhostPhysics(GhostRig);

            // The ghost root is a pure animation-driven reference skeleton: kinematic with gravity off.
            // It never falls or tumbles, so it needs no hover/upright/drag support. The physical rig
            // tracks it via joint drives + root-follow PD; balance is maintained physically.
            Rigidbody ghostRb = GhostRig.GetComponent<Rigidbody>();
            if (ghostRb == null) ghostRb = GhostRig.AddComponent<Rigidbody>();
            ghostRb.isKinematic = true;
            ghostRb.useGravity = false;
            ghostRb.mass = 70f;
            ghostRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

#if UNITY_2022_2_OR_NEWER
            ghostRb.excludeLayers = 1 << RagdollLayerIndex;
#endif

            var ikSolver = GhostRig.AddComponent<IKSolver>();
            ikSolver.hideFlags = HideFlags.HideInInspector;
            rootMaster.hiddenIKSolver = ikSolver;

            var stepManager = GhostRig.AddComponent<StepManager>();
            stepManager.hideFlags = HideFlags.HideInInspector;
            rootMaster.hiddenStepManager = stepManager;

            LayerMask safeGroundMask = GroundLayer;
            if (RagdollLayerIndex != 0) safeGroundMask &= ~(1 << RagdollLayerIndex);

            ikSolver.PhysicsSyncer = syncer;
            ikSolver.CharacterRoot = GhostRig.transform;
            ikSolver.Animator = ghostAnim;
            ikSolver.UseAnimator = true;

            ikSolver.HipBody.ZMPTransitionSpeed = 5f;
            ikSolver.HipBody.InvertedPendulumApex = 0.15f;
            ikSolver.HipBody.MotorJitterAmplitude = 0.015f;
            ikSolver.HipBody.MotorJitterSpeed = 60f;
            ikSolver.HipBody.HillLeanMultiplier = 0.6f;
            ikSolver.HipBody.MaxForwardLeanAngle = 60f;
            ikSolver.HipBody.MaxStrafeLeanAngle = 60f;
            ikSolver.HipBody.LeanSmoothTime = 0.15f;
            ikSolver.HipBody.HipsSpringStiffness = 50f;
            ikSolver.HipBody.HipsSpringDamping = 20f;
            ikSolver.HipBody.StumbleRecoverySpeed = 50f;
            ikSolver.HipBody.ImpactTorqueMultiplier = 20f;

            ikSolver.KneeOutwardBias = 0.05f;
            ikSolver.IKBlend = 1f;
            ikSolver.IKTransitionSpeed = 6f;
            ikSolver.VelocitySmoothing = 4f;
            ikSolver.PredictionSmoothing = 4f;
            ikSolver.PredictionCutoffSpeed = 4.5f;
            ikSolver.TurnPrediction = 0.12f;
            ikSolver.GroundLayers = safeGroundMask;
            ikSolver.RaycastStyle = GroundDetector.ERaycastStyle.StraightDown;
            ikSolver.RaycastShape = GroundDetector.ERaycastShape.Linecast;
            ikSolver.StepTriggerSensitivity = 0.5f;
            ikSolver.MaxSensitivityReduction = 0.7f;
            ikSolver.FootRollStrength = 12f;
            ikSolver.FootHeightOffset = 0f;
            ikSolver.SwingClearance = 0f;
            ikSolver.MinLegSeparation = 0.08f;
            ikSolver.FootOverlapRadius = 0f;
            ikSolver.StrafeForwardOffset = 0.06f;
            ikSolver.StrafeHipTwist = 15f;
            ikSolver.UseFootGluing = true;
            ikSolver.IdleStrideStretching = 0.13f;
            ikSolver.RunStrideStretching = 0.48f;
            ikSolver.StrideBlendSpeed = 4f;
            ikSolver.MinStepDuration = 0.3f;
            ikSolver.BaseGlueDuration = 0.18f;
            ikSolver.SwingSpeedMultiplier = 1.15f;
            ikSolver.GlueAttachStrictness = 0.75f;
            ikSolver.GlueDetachStrictness = 1.2f;
            ikSolver.UseHangingPose = true;
            ikSolver.AirborneTransitionSpeed = 6f;
            ikSolver.AirborneMinUngroundedTime = 0.1f;
            ikSolver.StanceSettleTime = 0.25f;
            ikSolver.AlignFeetToGround = true;
            ikSolver.FootAlignmentBlend = 1f;
            ikSolver.EnforceGroundContact = true;
            ikSolver.GroundContactSpeed = 18f;
            ikSolver.UseUnscaledTime = false;
            ikSolver.DrawDebug = false;
            ikSolver.UseAdditiveMode = true;
            ikSolver.RaycastHeightFromFoot = 0.12f;
            ikSolver.ForceProceduralPoseIfNoAnimation = true;

            // Phase-2: RoboticArmAnimator is retired. MagicBlend (configured in-editor) is the sole
            // upper-body authority (locomotion swing + weapon hold); ArmBalanceController only layers
            // a balance offset on top at high urgency.
            var armBalance = GhostRig.AddComponent<ArmBalanceController>();
            armBalance.hideFlags = HideFlags.HideInInspector;
            rootMaster.hiddenArmBalance = armBalance;
            MapArmBalanceBones(armBalance, ghostAnim);
            // Configured with the arbiter once the evaluator is created below.

            // Layer-3 physics overlay: bends the visible animation pose toward the physical rig's
            // gravity/LIPM topple, scaled by balanceUrgency. Lives on the Ghost (the visible rig),
            // runs after MagicBlend/IK/ArmBalance and before the syncer captures the corrected pose.
            var poseBlender = GhostRig.AddComponent<PhysicsPoseBlender>();
            poseBlender.hideFlags = HideFlags.HideInInspector;
            rootMaster.hiddenPoseBlender = poseBlender;
            MapPoseBlenderChain(poseBlender, ghostAnim);
            // Configured with the arbiter once the evaluator is created below.

            ShadowMimic = Instantiate(this.gameObject, transform.position, transform.rotation);
            ShadowMimic.name = "Shadow_Mimic";
            ShadowMimic.transform.parent = GhostRig.transform;
            ShadowMimic.transform.localPosition = Vector3.zero;
            ShadowMimic.transform.localRotation = Quaternion.identity;

            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer != -1) SetLayerRecursive(ShadowMimic, ignoreRaycastLayer);

            StripVisuals(ShadowMimic);
            StripScripts(ShadowMimic, typeof(ArticulationSyncer), typeof(RagdollRootFollower), typeof(Animator));
            SetupShadowPhysics(ShadowMimic, ghostAnim, RagdollLayerIndex);

            var shadowFollower = ShadowMimic.AddComponent<ShadowRigFollower>();
            shadowFollower.hideFlags = HideFlags.HideInInspector;
            rootMaster.hiddenShadowFollower = shadowFollower;

            PhysicalRig.transform.parent = masterRoot.transform;
            GhostRig.transform.parent = masterRoot.transform;

            rootMaster.PhysicalRig = PhysicalRig;
            rootMaster.GhostRig = GhostRig;
            rootMaster.ShadowMimic = ShadowMimic;

            syncer.physRoot = PhysicalRig.transform;
            syncer.ghostRoot = GhostRig.transform;
            AutoMapDynamicBones(syncer, anim, ghostAnim);

            // The Animator lives only on the Ghost_Rig (and runs by default). Physical_Rig must have
            // no Animator — an enabled Animator would fight the ArticulationBody solver for the bones.
            // anim has now served its last purpose (bone source for AutoMapDynamicBones), so remove it.
            DestroyImmediate(anim);

            // Phase-2 arbiter on the physical rig (alongside the syncer). Reads the syncer's bone
            // pairing to synthesize the ghost COM; the syncer reads its urgency back for COM blending.
            var urgencyEval = PhysicalRig.GetComponent<BalanceUrgencyEvaluator>() ?? PhysicalRig.AddComponent<BalanceUrgencyEvaluator>();
            urgencyEval.hideFlags = HideFlags.HideInInspector;
            urgencyEval.syncer = syncer;
            urgencyEval.capturePoint = syncer.capturePoint;
            syncer.urgency = urgencyEval;
            rootMaster.hiddenUrgency = urgencyEval;

            // Phase-2 arm balance reads urgency too.
            armBalance.Configure(ikSolver, urgencyEval, GhostRig.transform);
            // Layer-3 physics overlay reads the same arbiter.
            poseBlender.Configure(urgencyEval);

            // Balance debug overlay, distance LOD, and the external-force perimeter all live on the
            // master root and read the rigs created above. Created before ApplyProfile so the profile
            // can configure them in the same pass.
            var balanceDebug = masterRoot.AddComponent<RagdollBalanceDebug>();
            balanceDebug.hideFlags = HideFlags.HideInInspector;
            balanceDebug.syncer = syncer;
            balanceDebug.ikSolver = ikSolver;
            rootMaster.hiddenBalanceDebug = balanceDebug;

            var lod = masterRoot.AddComponent<PhysicsLODController>();
            lod.hideFlags = HideFlags.HideInInspector;
            lod.ghostRoot = GhostRig.transform;
            lod.ghostAnimator = ghostAnim;
            lod.syncer = syncer;
            lod.ikSolver = ikSolver;
            lod.stepManager = stepManager;
            rootMaster.hiddenLOD = lod;

            var perimeter = masterRoot.AddComponent<CharacterPerimeter>();
            perimeter.hideFlags = HideFlags.HideInInspector;
            perimeter.syncer = syncer;
            perimeter.ghostRoot = GhostRig.transform;
            rootMaster.hiddenPerimeter = perimeter;

            rootMaster.ApplyProfile(ActiveProfile);
            rootMaster.WireReferences();
            if (GhostRig != null)
            {
                SetLayerRecursively(GhostRig.transform, RagdollLayerIndex);
            }

            if (PhysicalRig != null)
            {
                SetLayerRecursively(PhysicalRig.transform, RagdollLayerIndex);
            }
            Debug.Log($"<color=green>Ragdoll Generated!</color> Assigned Profile: {ActiveProfile.name}");
            DestroyImmediate(this);
#endif
        }
        // Samples the source Animator's default state clip at t=0 onto the rig, posing it into the
        // upright animated pose so ArticulationBody joint zeros are created upright (see call site).
        // SampleAnimation is a runtime-safe API; the AnimatorController cast (to find the default
        // state's motion) is editor-only, with a runtime fallback to the first clip.
        private static void SampleDefaultPose(Animator anim)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;

            AnimationClip clip = null;
#if UNITY_EDITOR
            var ac = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (ac != null && ac.layers.Length > 0 && ac.layers[0].stateMachine.defaultState != null)
                clip = ac.layers[0].stateMachine.defaultState.motion as AnimationClip;
#endif
            if (clip == null)
            {
                var clips = anim.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0) clip = clips[0];
            }

            if (clip != null) clip.SampleAnimation(anim.gameObject, 0f);
        }

        private void SetLayerRecursively(Transform obj, int newLayer)
        {
            if (obj == null) return;
            obj.gameObject.layer = newLayer;

            foreach (Transform child in obj)
            {
                SetLayerRecursively(child, newLayer);
            }
        }
        private void AutoMapDynamicBones(ArticulationSyncer script, Animator physAnim, Animator ghostAnim)
        {
            if (script.physRoot == null || script.ghostRoot == null) return;
            var newBones = new List<ArticulationDriver>();
            bool useHumanoidMapping = (physAnim != null && physAnim.isHuman && ghostAnim != null && ghostAnim.isHuman);

            if (useHumanoidMapping)
            {
                Transform physHips = physAnim.GetBoneTransform(HumanBodyBones.Hips);
                Transform ghostHips = ghostAnim.GetBoneTransform(HumanBodyBones.Hips);

                // Root hips link: contributes mass/COM to stability but has no drivable joint.
                if (physHips != null && ghostHips != null)
                {
                    var hipsBody = physHips.GetComponent<ArticulationBody>();
                    if (hipsBody != null)
                    {
                        newBones.Add(new ArticulationDriver
                        {
                            body = hipsBody,
                            ghostBone = ghostHips,
                            boneName = physHips.name,
                            anchorMultiplier = 1f,
                            driveMultiplier = 1f,
                            damperMultiplier = 1f
                        });
                    }
                }

                var bodies = script.physRoot.GetComponentsInChildren<ArticulationBody>();
                foreach (var body in bodies)
                {
                    if (physHips != null && body.transform == physHips) continue;

                    HumanBodyBones matchedBone = HumanBodyBones.LastBone;
                    foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones)))
                    {
                        if (boneType == HumanBodyBones.LastBone) continue;
                        if (physAnim.GetBoneTransform(boneType) == body.transform)
                        {
                            matchedBone = boneType; break;
                        }
                    }

                    if (matchedBone == HumanBodyBones.LastBone) continue;

                    Transform correspondingGhostBone = ghostAnim.GetBoneTransform(matchedBone);
                    if (correspondingGhostBone == null) continue;

                    var b = new ArticulationDriver
                    {
                        body = body,
                        ghostBone = correspondingGhostBone,
                        boneName = body.name
                    };

                    switch (matchedBone)
                    {
                        case HumanBodyBones.LeftUpperLeg:
                        case HumanBodyBones.RightUpperLeg:
                        case HumanBodyBones.LeftLowerLeg:
                        case HumanBodyBones.RightLowerLeg:
                            b.anchorMultiplier = 1f; b.driveMultiplier = 2f; b.damperMultiplier = 1f; break;
                        case HumanBodyBones.LeftFoot:
                        case HumanBodyBones.RightFoot:
                        case HumanBodyBones.LeftToes:
                        case HumanBodyBones.RightToes:
                            b.anchorMultiplier = 1f; b.driveMultiplier = 4f; b.damperMultiplier = 1f; break;
                        case HumanBodyBones.Spine:
                        case HumanBodyBones.Chest:
                        case HumanBodyBones.UpperChest:
                            b.anchorMultiplier = 1f; b.driveMultiplier = 0.2f; b.damperMultiplier = 1f; break;
                        case HumanBodyBones.LeftUpperArm:
                        case HumanBodyBones.RightUpperArm:
                        case HumanBodyBones.LeftLowerArm:
                        case HumanBodyBones.RightLowerArm:
                        case HumanBodyBones.LeftHand:
                        case HumanBodyBones.RightHand:
                            b.anchorMultiplier = 0f; b.driveMultiplier = 2f; b.damperMultiplier = 5f; break;
                        case HumanBodyBones.Head:
                        case HumanBodyBones.Neck:
                            b.anchorMultiplier = 0.5f; b.driveMultiplier = 0.2f; b.damperMultiplier = 1f; break;
                        default:
                            b.anchorMultiplier = 1f; b.driveMultiplier = 1f; b.damperMultiplier = 1f; break;
                    }
                    newBones.Add(b);
                }
            }
            script.physicsBones = newBones.ToArray();
        }

        // Recursive name lookup over a bone hierarchy. Used to resolve Physical_Rig bones (which has
        // no Animator) by the matching Ghost_Rig bone name — Ghost is a structural clone, so names are 1:1.
        private static Transform FindBoneByName(Transform root, string boneName)
        {
            if (root == null || string.IsNullOrEmpty(boneName)) return null;
            if (root.name == boneName) return root;
            foreach (Transform child in root)
            {
                var r = FindBoneByName(child, boneName);
                if (r != null) return r;
            }
            return null;
        }

        private void MapArmBalanceBones(ArmBalanceController armBalance, Animator anim)
        {
            if (anim == null || armBalance == null) return;
            armBalance.leftShoulder = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            armBalance.leftElbow = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            armBalance.leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            armBalance.rightShoulder = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            armBalance.rightElbow = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            armBalance.rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        }

        // Layer-3 lean chain: hips -> spine -> chest -> head, root->tip. The whole-body physical tilt
        // is distributed across these with a hips-heavy falloff, so the upper body topples while the
        // legs stay owned by the foot IK. Arms are intentionally excluded (ArmBalanceController owns
        // them). Missing optional bones (some rigs lack Spine/Chest/UpperChest/Neck) are skipped.
        private void MapPoseBlenderChain(PhysicsPoseBlender blender, Animator anim)
        {
            if (anim == null || blender == null) return;

            var bones = new System.Collections.Generic.List<Transform>(6);
            var weights = new System.Collections.Generic.List<float>(6);
            void Add(HumanBodyBones b, float w)
            {
                Transform t = anim.GetBoneTransform(b);
                if (t != null) { bones.Add(t); weights.Add(w); }
            }

            Add(HumanBodyBones.Hips, 1.0f);
            Add(HumanBodyBones.Spine, 0.7f);
            Add(HumanBodyBones.Chest, 0.5f);
            Add(HumanBodyBones.UpperChest, 0.35f);
            Add(HumanBodyBones.Neck, 0.15f);
            Add(HumanBodyBones.Head, 0.1f);

            blender.chainBones = bones.ToArray();
            blender.chainWeights = weights.ToArray();
        }

        private void CleanupGhostPhysics(GameObject ghostRoot)
        {
            var joints = ghostRoot.GetComponentsInChildren<Joint>();
            foreach (var j in joints) DestroyImmediate(j);
            var articulations = ghostRoot.GetComponentsInChildren<ArticulationBody>();
            foreach (var ab in articulations) DestroyImmediate(ab);
            var rbs = ghostRoot.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs) if (rb.gameObject != ghostRoot) DestroyImmediate(rb);
            var cols = ghostRoot.GetComponentsInChildren<Collider>();
            foreach (var c in cols) DestroyImmediate(c);
        }

        private void CopySettingsTo(ActiveRagdollBrain target)
        {
            target.ActiveProfile = this.ActiveProfile;
            target.GroundLayer = this.GroundLayer;
            target.ImpactLayers = this.ImpactLayers;
            target.RagdollLayerIndex = this.RagdollLayerIndex;
        }

        private void StripVisuals(GameObject root)
        {
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>()) DestroyImmediate(smr);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>()) DestroyImmediate(mf);
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>()) DestroyImmediate(mr);
        }

        // Slash-delimited path of `t` relative to `root` (empty string if t == root). Used to pair
        // bones across two clones of the same hierarchy by name rather than fragile index alignment.
        private static string RelativePath(Transform t, Transform root)
        {
            if (t == root) return string.Empty;
            var stack = new System.Collections.Generic.List<string>();
            while (t != null && t != root)
            {
                stack.Add(t.name);
                t = t.parent;
            }
            stack.Reverse();
            return string.Join("/", stack);
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform) SetLayerRecursive(child.gameObject, layer);
        }

        private void StripScripts(GameObject root, params System.Type[] typesToRemove)
        {
            var allScripts = root.GetComponentsInChildren<MonoBehaviour>(true).ToList();
            foreach (var script in allScripts)
            {
                if (script == null) continue;
                if (script is ActiveRagdollBrain) { DestroyImmediate(script); continue; }
                foreach (var t in typesToRemove) if (script.GetType() == t) DestroyImmediate(script);
            }
        }

        private void SetupShadowPhysics(GameObject shadow, Animator originalAnim, int targetPhysicsLayer)
        {
            var triggerBoneNames = new HashSet<string>();
            if (originalAnim != null && originalAnim.isHuman)
            {
                HumanBodyBones[] lowerBones = { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };
                foreach (var b in lowerBones) { Transform t = originalAnim.GetBoneTransform(b); if (t != null) triggerBoneNames.Add(t.name); }
            }

            foreach (var jt in shadow.GetComponentsInChildren<Joint>()) DestroyImmediate(jt);
            foreach (var ab in shadow.GetComponentsInChildren<ArticulationBody>()) DestroyImmediate(ab);
            foreach (var rb in shadow.GetComponentsInChildren<Rigidbody>()) DestroyImmediate(rb);

            foreach (var col in shadow.GetComponentsInChildren<Collider>())
            {
#if UNITY_2022_2_OR_NEWER
                col.excludeLayers = 1 << targetPhysicsLayer;
#endif
                string lowerName = col.transform.name.ToLower();
                bool isLowerBody = triggerBoneNames.Contains(col.transform.name) || lowerName.Contains("calf") || lowerName.Contains("shin") || lowerName.Contains("foot") || lowerName.Contains("toe");
                col.isTrigger = isLowerBody;
            }
        }
    }
}