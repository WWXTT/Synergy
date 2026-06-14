using UnityEngine;
using System.Collections.Generic;

namespace FrostPunchGames
{
    // Builds the physical rig as an ArticulationBody tree (Featherstone reduced-coordinate
    // solver) instead of ConfigurableJoint + Rigidbody. Hips is the floating-base root; all
    // other bones become reduced-coordinate links connected automatically through the
    // transform hierarchy (no connectedBody / connectedAnchor bookkeeping needed).
    public static class SimpleRagdollBuilder
    {
        public struct JointConstraints
        {
            public float FlexionMin;
            public float FlexionMax;
            public float LateralSwing;
            public float AxialTwist;

            public JointConstraints(float flexMin, float flexMax, float lateral, float twist)
            {
                FlexionMin = flexMin;
                FlexionMax = flexMax;
                LateralSwing = lateral;
                AxialTwist = twist;
            }
        }

        public class SkeletonMap
        {
            public Animator Anim;
            public Transform CharacterRoot, CorePelvis, MidSpine, UpperChest, Cranium;
            public Transform UpperArmL, LowerArmL, PalmL;
            public Transform UpperArmR, LowerArmR, PalmR;
            public Transform ThighL, CalfL, AnkleL, ToeL;
            public Transform ThighR, CalfR, AnkleR, ToeR;

            public static SkeletonMap Scan(Animator targetAnim)
            {
                return new SkeletonMap
                {
                    Anim = targetAnim,
                    CharacterRoot = targetAnim.transform,
                    CorePelvis = targetAnim.GetBoneTransform(HumanBodyBones.Hips),
                    MidSpine = targetAnim.GetBoneTransform(HumanBodyBones.Spine),
                    UpperChest = targetAnim.GetBoneTransform(HumanBodyBones.Chest),
                    Cranium = targetAnim.GetBoneTransform(HumanBodyBones.Head),

                    UpperArmL = targetAnim.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                    LowerArmL = targetAnim.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                    PalmL = targetAnim.GetBoneTransform(HumanBodyBones.LeftHand),

                    UpperArmR = targetAnim.GetBoneTransform(HumanBodyBones.RightUpperArm),
                    LowerArmR = targetAnim.GetBoneTransform(HumanBodyBones.RightLowerArm),
                    PalmR = targetAnim.GetBoneTransform(HumanBodyBones.RightHand),

                    ThighL = targetAnim.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                    CalfL = targetAnim.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
                    AnkleL = targetAnim.GetBoneTransform(HumanBodyBones.LeftFoot),
                    ToeL = targetAnim.GetBoneTransform(HumanBodyBones.LeftToes),

                    ThighR = targetAnim.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                    CalfR = targetAnim.GetBoneTransform(HumanBodyBones.RightLowerLeg),
                    AnkleR = targetAnim.GetBoneTransform(HumanBodyBones.RightFoot),
                    ToeR = targetAnim.GetBoneTransform(HumanBodyBones.RightToes)
                };
            }
        }

        private enum ShapeProfile { Box, Capsule }

        public static void InitializeRagdoll(Animator targetAnimator, float totalSystemMass)
        {
            var skeleton = SkeletonMap.Scan(targetAnimator);

            PurgeLegacyPhysics(skeleton.CorePelvis);
            ConstructVolumes(skeleton);              // colliders only (no bodies)
            BindArticulations(skeleton);             // add ArticulationBody tree top-down
            CalculateVolumetricWeight(skeleton, totalSystemMass); // distribute mass by volume
        }

        private static void ConstructVolumes(SkeletonMap skel)
        {
            float intersectionBuffer = 0.1f;
            Vector3 clavicleMidpoint = Vector3.Lerp(skel.UpperArmL.position, skel.UpperArmR.position, 0.5f);
            Vector3 neckOrigin = Vector3.Lerp(clavicleMidpoint, skel.Cranium.position, 0.5f);
            Vector3 shoulderVector = skel.UpperArmR.position - skel.UpperArmL.position;

            float torsoBreadth = shoulderVector.magnitude;
            float torsoDepthRatio = 0.6f;

            Vector3 pelvisTop = (skel.MidSpine != null) ? skel.MidSpine.position : ((skel.UpperChest != null) ? skel.UpperChest.position : neckOrigin);
            float pelvisWidth = (skel.MidSpine != null || skel.UpperChest != null) ? torsoBreadth * 0.8f : torsoBreadth;
            AffixVolume(skel.CorePelvis, skel.CorePelvis.position, pelvisTop, ShapeProfile.Box, intersectionBuffer, pelvisWidth, torsoDepthRatio, shoulderVector);

            Vector3 previousSegmentEnd = pelvisTop;
            if (skel.MidSpine != null)
            {
                Vector3 spineTop = (skel.UpperChest != null) ? skel.UpperChest.position : neckOrigin;
                AffixVolume(skel.MidSpine, previousSegmentEnd, spineTop, ShapeProfile.Box, intersectionBuffer, torsoBreadth * 0.75f, torsoDepthRatio, shoulderVector);
                previousSegmentEnd = spineTop;
            }

            if (skel.UpperChest != null)
            {
                AffixVolume(skel.UpperChest, previousSegmentEnd, neckOrigin, ShapeProfile.Box, intersectionBuffer, torsoBreadth, torsoDepthRatio, shoulderVector);
            }

            Vector3 craniumTop = skel.Cranium.position + (skel.CharacterRoot.up * 0.25f);
            AffixVolume(skel.Cranium, skel.Cranium.position, craniumTop, ShapeProfile.Capsule, intersectionBuffer, 0.25f);

            float armThickness = 0.35f;
            float lArmGirth = Vector3.Distance(skel.UpperArmL.position, skel.LowerArmL.position) * armThickness;
            AffixVolume(skel.UpperArmL, skel.UpperArmL.position, skel.LowerArmL.position, ShapeProfile.Capsule, intersectionBuffer, lArmGirth);
            AffixVolume(skel.LowerArmL, skel.LowerArmL.position, skel.PalmL.position, ShapeProfile.Capsule, intersectionBuffer, lArmGirth * 0.85f);

            float rArmGirth = Vector3.Distance(skel.UpperArmR.position, skel.LowerArmR.position) * armThickness;
            AffixVolume(skel.UpperArmR, skel.UpperArmR.position, skel.LowerArmR.position, ShapeProfile.Capsule, intersectionBuffer, rArmGirth);
            AffixVolume(skel.LowerArmR, skel.LowerArmR.position, skel.PalmR.position, ShapeProfile.Capsule, intersectionBuffer, rArmGirth * 0.85f);

            float legThickness = 0.3f;
            float lLegGirth = Vector3.Distance(skel.ThighL.position, skel.CalfL.position) * legThickness;
            AffixVolume(skel.ThighL, skel.ThighL.position, skel.CalfL.position, ShapeProfile.Capsule, intersectionBuffer, lLegGirth);
            AffixVolume(skel.CalfL, skel.CalfL.position, skel.AnkleL.position, ShapeProfile.Capsule, intersectionBuffer, lLegGirth * 0.85f);

            float rLegGirth = Vector3.Distance(skel.ThighR.position, skel.CalfR.position) * legThickness;
            AffixVolume(skel.ThighR, skel.ThighR.position, skel.CalfR.position, ShapeProfile.Capsule, intersectionBuffer, rLegGirth);
            AffixVolume(skel.CalfR, skel.CalfR.position, skel.AnkleR.position, ShapeProfile.Capsule, intersectionBuffer, rLegGirth * 0.85f);

            GenerateManipulator(skel.PalmL, true, skel.Anim);
            GenerateManipulator(skel.PalmR, false, skel.Anim);

            GeneratePlantarSupport(skel.AnkleL, skel.ToeL, skel.ThighL, skel.CharacterRoot);
            GeneratePlantarSupport(skel.AnkleR, skel.ToeR, skel.ThighR, skel.CharacterRoot);
        }

        private static void GenerateManipulator(Transform palm, bool isLeftSide, Animator rigAnim)
        {
            Transform knuckle = rigAnim.GetBoneTransform(isLeftSide ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            if (!knuckle)
            {
                float backupSpacing = Vector3.Distance(palm.position, palm.parent.position) * 0.5f;
                Vector3 projectedEnd = palm.position + (palm.position - palm.parent.position).normalized * backupSpacing;
                AffixVolume(palm, palm.position, projectedEnd, ShapeProfile.Capsule, 0.1f, backupSpacing);
                return;
            }
            AffixVolume(palm, palm.position, knuckle.position, ShapeProfile.Box, 0f, 0.08f, 0.3f);
        }

        private static void GeneratePlantarSupport(Transform ankle, Transform toe, Transform hip, Transform root)
        {
            float supportLength;
            Vector3 groundAlignment;

            float verticalElevation = Mathf.Abs(Vector3.Dot(ankle.position - root.position, root.up));
            if (verticalElevation < 0.05f) verticalElevation = 0.1f;

            if (toe != null)
            {
                Vector3 projectedForward = Vector3.ProjectOnPlane(toe.position - ankle.position, root.up);
                if (projectedForward.sqrMagnitude < 0.001f) projectedForward = root.forward;
                groundAlignment = projectedForward.normalized;
                supportLength = Vector3.Distance(ankle.position, toe.position) * 1.7f;
            }
            else
            {
                float totalLegReach = Vector3.Distance(hip.position, ankle.position);
                supportLength = totalLegReach * 0.25f;
                groundAlignment = root.forward;
            }

            float supportWidth = supportLength * 0.5f;
            float supportHeight = verticalElevation * 1.1f;

            GameObject plantarNode = new GameObject("Foot_Collider");
            plantarNode.transform.SetParent(ankle);
            plantarNode.transform.position = ankle.position;
            plantarNode.transform.rotation = Quaternion.LookRotation(groundAlignment, root.up);

            BoxCollider baseCollider = plantarNode.AddComponent<BoxCollider>();
            baseCollider.size = new Vector3(supportWidth, supportHeight, supportLength);

            float yOffset = -verticalElevation + (supportHeight * 0.5f);
            float zOffset = supportLength * 0.25f;
            baseCollider.center = new Vector3(0, yOffset, zOffset);
            // No Rigidbody here: the foot pad collider belongs to the ankle's ArticulationBody link.
        }

        private static void AffixVolume(Transform bone, Vector3 origin, Vector3 terminus, ShapeProfile geometricShape, float padding, float girth, float depthAspect = 1f, Vector3 customWidthAxis = default)
        {
            Vector3 spanVector = terminus - origin;
            float overallLength = spanVector.magnitude * (1f + padding);
            float scaleFactor = (bone.lossyScale.x + bone.lossyScale.y + bone.lossyScale.z) / 3f;
            Vector3 longitudinalAxis = DetermineLocalAxis(bone, spanVector);

            if (geometricShape == ShapeProfile.Capsule)
            {
                var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                capsule.height = Mathf.Abs(overallLength / scaleFactor);
                capsule.radius = Mathf.Abs((girth * 0.8f) / scaleFactor);

                int primaryIndex = 2; // Default Z
                if (Mathf.Abs(longitudinalAxis.x) > Mathf.Abs(longitudinalAxis.y) && Mathf.Abs(longitudinalAxis.x) > Mathf.Abs(longitudinalAxis.z)) primaryIndex = 0;
                else if (Mathf.Abs(longitudinalAxis.y) > Mathf.Abs(longitudinalAxis.x) && Mathf.Abs(longitudinalAxis.y) > Mathf.Abs(longitudinalAxis.z)) primaryIndex = 1;

                capsule.direction = primaryIndex;
                capsule.center = bone.InverseTransformPoint(Vector3.Lerp(origin, terminus, 0.5f));
            }
            else
            {
                Vector3 transverseAxis = DetermineLocalAxis(bone, customWidthAxis != default ? customWidthAxis : Vector3.right);
                if (Mathf.Abs(Vector3.Dot(longitudinalAxis, transverseAxis)) > 0.9f) transverseAxis = new Vector3(longitudinalAxis.y, longitudinalAxis.z, longitudinalAxis.x);
                Vector3 sagittalAxis = Vector3.Cross(longitudinalAxis, transverseAxis);

                var box = bone.gameObject.AddComponent<BoxCollider>();

                Vector3 absoluteLongitudinal = new Vector3(Mathf.Abs(longitudinalAxis.x), Mathf.Abs(longitudinalAxis.y), Mathf.Abs(longitudinalAxis.z));
                Vector3 absoluteTransverse = new Vector3(Mathf.Abs(transverseAxis.x), Mathf.Abs(transverseAxis.y), Mathf.Abs(transverseAxis.z));
                Vector3 absoluteSagittal = new Vector3(Mathf.Abs(sagittalAxis.x), Mathf.Abs(sagittalAxis.y), Mathf.Abs(sagittalAxis.z));

                Vector3 boxDimensions = Vector3.Scale(absoluteLongitudinal, new Vector3(overallLength, overallLength, overallLength)) +
                                        Vector3.Scale(absoluteTransverse, new Vector3(girth, girth, girth)) +
                                        Vector3.Scale(absoluteSagittal, new Vector3(girth * depthAspect, girth * depthAspect, girth * depthAspect));

                box.size = new Vector3(Mathf.Max(boxDimensions.x, 0.01f), Mathf.Max(boxDimensions.y, 0.01f), Mathf.Max(boxDimensions.z, 0.01f)) / scaleFactor;
                box.center = bone.InverseTransformPoint(Vector3.Lerp(origin, terminus, 0.5f));
            }
            // No Rigidbody: ArticulationBody (added in BindArticulations) carries mass/inertia.
        }

        // ---- ArticulationBody construction ----------------------------------------------------

        private static void BindArticulations(SkeletonMap skel)
        {
            // Root link: floating-base hips, no joint DOFs of its own.
            var hips = AddBody(skel.CorePelvis);
            hips.immovable = false;

            JointConstraints spineLimits = new JointConstraints(-25f, 15f, 20f, 20f);

            // Spine chain (spherical, twist along the spine toward the next segment).
            if (skel.MidSpine != null)
            {
                Transform child = skel.UpperChest != null ? skel.UpperChest : skel.Cranium;
                var b = AddBody(skel.MidSpine);
                ConfigureArticulation(b, child.position - skel.MidSpine.position, spineLimits, false);
            }

            if (skel.UpperChest != null)
            {
                var b = AddBody(skel.UpperChest);
                ConfigureArticulation(b, skel.Cranium.position - skel.UpperChest.position, spineLimits, false);
            }

            // Head.
            {
                Transform torso = skel.UpperChest != null ? skel.UpperChest : (skel.MidSpine != null ? skel.MidSpine : skel.CorePelvis);
                var b = AddBody(skel.Cranium);
                ConfigureArticulation(b, skel.Cranium.position - torso.position, new JointConstraints(-35f, 35f, 30f, 75f), false);
            }

            JointConstraints shoulder = new JointConstraints(-40f, 115f, 80f, 40f);
            JointConstraints elbow = new JointConstraints(0f, 135f, 5f, 35f);
            JointConstraints wrist = new JointConstraints(-45f, 45f, 40f, 20f);

            JointConstraints hip = new JointConstraints(-110f, 40f, 75f, 40f);
            JointConstraints knee = new JointConstraints(0f, 135f, 5f, 35f);
            JointConstraints ankle = new JointConstraints(-45f, 45f, 40f, 20f);

            ConfigureLimb(skel.UpperArmL, skel.LowerArmL, skel.PalmL, skel.CharacterRoot, shoulder, elbow, wrist, isArm: true);
            ConfigureLimb(skel.UpperArmR, skel.LowerArmR, skel.PalmR, skel.CharacterRoot, shoulder, elbow, wrist, isArm: true);

            ConfigureLimb(skel.ThighL, skel.CalfL, skel.AnkleL, skel.CharacterRoot, hip, knee, ankle, isArm: false);
            ConfigureLimb(skel.ThighR, skel.CalfR, skel.AnkleR, skel.CharacterRoot, hip, knee, ankle, isArm: false);
        }

        // proximal (shoulder/hip) = spherical, mid (elbow/knee) = revolute hinge,
        // distal (wrist/ankle) = spherical. Added parent-before-child so the Featherstone
        // solver links each body to the nearest ArticulationBody ancestor automatically.
        private static void ConfigureLimb(Transform proximal, Transform mid, Transform distal, Transform root,
                                          JointConstraints limitsProximal, JointConstraints limitsMid, JointConstraints limitsDistal,
                                          bool isArm)
        {
            Vector3 upperVec = (mid.position - proximal.position).normalized;
            Vector3 lowerVec = (distal.position - mid.position).normalized;

            // Hinge rotation axis for the mid joint, perpendicular to the limb plane.
            Vector3 hingeNormal = -Vector3.Cross(upperVec, lowerVec);
            float bend = Vector3.Angle(upperVec, lowerVec);
            bool alignedWithGravity = Mathf.Abs(Vector3.Dot(upperVec, root.up)) > 0.5f;
            float flexionThreshold = alignedWithGravity ? 1f : 0.01f;

            // Straight (un-bent) limb at bind pose: cross product degenerates, pick a sane axis.
            if (bend < flexionThreshold)
            {
                if (alignedWithGravity)
                    hingeNormal = Vector3.Dot(upperVec, root.up) > 0f ? root.right : -root.right;
                else
                    hingeNormal = Vector3.Dot(upperVec, root.right) > 0f ? root.up : -root.up;
            }

            // Arms and legs bend in opposite directions at the bind pose, so the hinge axis derived
            // above (tuned correct for legs) comes out mirrored on the arms. Negate it for arms so the
            // elbow flexes the anatomically correct way instead of hyperextending backward.
            if (isArm) hingeNormal = -hingeNormal;

            var proximalBody = AddBody(proximal);
            ConfigureArticulation(proximalBody, upperVec, limitsProximal, false);

            var midBody = AddBody(mid);
            ConfigureArticulation(midBody, hingeNormal, limitsMid, true);

            if (distal != null)
            {
                var distalBody = AddBody(distal);
                ConfigureArticulation(distalBody, lowerVec, limitsDistal, false);
            }
        }

        private static ArticulationBody AddBody(Transform bone)
        {
            var body = bone.GetComponent<ArticulationBody>();
            if (body == null) body = bone.gameObject.AddComponent<ArticulationBody>();
            body.useGravity = true;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.05f;
            return body;
        }

        // Phase-2: generic weapon attach. Parents the weapon under the hand bone and turns it into
        // an ArticulationBody link of the existing chain (the Featherstone solver auto-connects it
        // to the hand's body via the transform hierarchy). Its mass then participates in the COM
        // automatically. A RevoluteJoint lets the weapon swing passively under load (the runtime
        // ArticulationDriver softens its drive at high urgency for damping); FixedJoint = rigid grip.
        // Returns the weapon's ArticulationBody so the caller can register a driver for it.
        public static ArticulationBody AttachWeapon(Transform handBone, GameObject weapon, float mass,
                                                     bool passiveSwing)
        {
            if (handBone == null || weapon == null) return null;

            // The hand must already be an articulation link for the solver to chain the weapon.
            if (handBone.GetComponent<ArticulationBody>() == null) return null;

            if (weapon.transform.parent != handBone)
                weapon.transform.SetParent(handBone, true);

            var body = AddBody(weapon.transform);
            // Twist axis along the grip direction (hand forward) gives a natural swing hinge.
            body.anchorPosition = Vector3.zero;
            body.anchorRotation = ComputeAnchorRotation(weapon.transform, handBone.forward);

            if (passiveSwing)
            {
                body.jointType = ArticulationJointType.RevoluteJoint;
                body.twistLock = ArticulationDofLock.LimitedMotion;
                var xd = body.xDrive;
                xd.lowerLimit = -45f;
                xd.upperLimit = 45f;
                body.xDrive = xd;
            }
            else
            {
                body.jointType = ArticulationJointType.FixedJoint;
            }

            if (mass > 0f) body.mass = mass;
            return body;
        }

        // Sets joint type, the anchor frame (twist axis = X), and the DOF limits.
        // The runtime ArticulationDriver overwrites target/stiffness/damping/forceLimit each
        // FixedUpdate; only the limits and joint frame are authored here.
        private static void ConfigureArticulation(ArticulationBody body, Vector3 twistAxisWorld, JointConstraints limits, bool revolute)
        {
            body.anchorPosition = Vector3.zero;
            body.anchorRotation = ComputeAnchorRotation(body.transform, twistAxisWorld);
            // matchAnchors (default true) auto-derives the parent anchor to coincide at bind pose.

            if (revolute)
            {
                body.jointType = ArticulationJointType.RevoluteJoint;
                body.twistLock = ArticulationDofLock.LimitedMotion;

                var xd = body.xDrive;
                xd.lowerLimit = limits.FlexionMin;
                xd.upperLimit = limits.FlexionMax;
                body.xDrive = xd;
            }
            else
            {
                body.jointType = ArticulationJointType.SphericalJoint;
                body.twistLock = ArticulationDofLock.LimitedMotion;   // X = twist
                body.swingYLock = ArticulationDofLock.LimitedMotion;  // Y = lateral swing
                body.swingZLock = ArticulationDofLock.LimitedMotion;  // Z = flexion swing

                var xd = body.xDrive;
                xd.lowerLimit = -limits.AxialTwist;
                xd.upperLimit = limits.AxialTwist;
                body.xDrive = xd;

                var yd = body.yDrive;
                yd.lowerLimit = -limits.LateralSwing;
                yd.upperLimit = limits.LateralSwing;
                body.yDrive = yd;

                var zd = body.zDrive;
                zd.lowerLimit = limits.FlexionMin;
                zd.upperLimit = limits.FlexionMax;
                body.zDrive = zd;
            }
        }

        // Builds a bone-local rotation whose local +X points along the desired world twist axis.
        // This is the joint reduced-coordinate frame; ArticulationDriver.ToReducedSpace projects
        // drive targets through Inverse(anchorRotation), so this must stay consistent with it.
        private static Quaternion ComputeAnchorRotation(Transform bone, Vector3 twistAxisWorld)
        {
            Vector3 x = twistAxisWorld.sqrMagnitude > 1e-6f ? twistAxisWorld.normalized : bone.right;
            Vector3 up = Mathf.Abs(Vector3.Dot(x, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 z = Vector3.Cross(x, up).normalized;
            Vector3 y = Vector3.Cross(z, x);
            Quaternion frameWorld = Quaternion.LookRotation(z, y); // local X -> cross(y,z) = x
            return Quaternion.Inverse(bone.rotation) * frameWorld;
        }

        private static Vector3 DetermineLocalAxis(Transform referenceFrame, Vector3 targetWorldDirection)
        {
            targetWorldDirection = targetWorldDirection.normalized;

            float projX = Mathf.Abs(Vector3.Dot(targetWorldDirection, referenceFrame.right));
            float projY = Mathf.Abs(Vector3.Dot(targetWorldDirection, referenceFrame.up));
            float projZ = Mathf.Abs(Vector3.Dot(targetWorldDirection, referenceFrame.forward));

            Vector3 bestMatchAxis = Vector3.right;
            if (projY > projX && projY > projZ) bestMatchAxis = Vector3.up;
            if (projZ > projX && projZ > projY) bestMatchAxis = Vector3.forward;

            if (Vector3.Dot(targetWorldDirection, referenceFrame.rotation * bestMatchAxis) < 0f)
            {
                bestMatchAxis = -bestMatchAxis;
            }

            return bestMatchAxis;
        }

        private static void CalculateVolumetricWeight(SkeletonMap skel, float systemMassCap)
        {
            var bodies = skel.Anim.GetComponentsInChildren<ArticulationBody>();
            float totalCalculatedVolume = 0f;
            Dictionary<ArticulationBody, float> componentVolumes = new Dictionary<ArticulationBody, float>();

            foreach (var body in bodies)
            {
                float calculatedVolume = EvaluateLinkVolume(body);
                if (calculatedVolume <= 0) calculatedVolume = 0.01f;
                componentVolumes[body] = calculatedVolume;
                totalCalculatedVolume += calculatedVolume;
            }

            if (totalCalculatedVolume <= 0f) return;

            foreach (var kvp in componentVolumes)
            {
                kvp.Key.mass = Mathf.Max(0.05f, (kvp.Value / totalCalculatedVolume) * systemMassCap);
            }
        }

        // Volume of every collider that belongs to this link (own collider + descendant
        // colliders, e.g. the Foot_Collider pad, whose nearest ArticulationBody is this body).
        private static float EvaluateLinkVolume(ArticulationBody body)
        {
            float volume = 0f;
            foreach (var col in body.GetComponentsInChildren<Collider>())
            {
                if (col.GetComponentInParent<ArticulationBody>() != body) continue;
                volume += EvaluateHullVolume(col);
            }
            return volume;
        }

        private static float EvaluateHullVolume(Collider targetCol)
        {
            if (targetCol == null) return 0f;
            if (targetCol is BoxCollider box)
            {
                return box.size.x * box.size.y * box.size.z;
            }
            if (targetCol is CapsuleCollider cap)
            {
                float radius = cap.radius;
                float coreCylinder = Mathf.Max(0, cap.height - (2 * radius));
                return ((4f / 3f) * Mathf.PI * Mathf.Pow(radius, 3)) + (Mathf.PI * Mathf.Pow(radius, 2) * coreCylinder);
            }
            return 0.1f;
        }

        private static void PurgeLegacyPhysics(Transform rootAnchor)
        {
            if (rootAnchor == null) return;
            bool engineIsRunning = Application.isPlaying;

            foreach (var node in rootAnchor.GetComponentsInChildren<Transform>())
            {
                if (node.name == "Foot_Collider")
                {
                    if (engineIsRunning) Object.Destroy(node.gameObject);
                    else Object.DestroyImmediate(node.gameObject);
                    continue;
                }

                if (node.TryGetComponent(out Joint jointComp))
                {
                    if (engineIsRunning) Object.Destroy(jointComp);
                    else Object.DestroyImmediate(jointComp);
                }

                if (node.TryGetComponent(out Rigidbody rigidComp))
                {
                    if (engineIsRunning) Object.Destroy(rigidComp);
                    else Object.DestroyImmediate(rigidComp);
                }

                if (node.TryGetComponent(out ArticulationBody articulationComp))
                {
                    if (engineIsRunning) Object.Destroy(articulationComp);
                    else Object.DestroyImmediate(articulationComp);
                }

                foreach (var colliderComp in node.GetComponents<Collider>())
                {
                    if (engineIsRunning) Object.Destroy(colliderComp);
                    else Object.DestroyImmediate(colliderComp);
                }
            }
        }
    }
}
