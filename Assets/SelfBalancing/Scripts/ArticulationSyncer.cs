using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FrostPunchGames
{
    public interface IBoneCollisionListener
    {
        void OnBoneCollision(Collision collision, BoneCollisionBroadcaster broadcaster);
    }

    public class BoneCollisionBroadcaster : MonoBehaviour
    {
        public IBoneCollisionListener listener;
        public ArticulationDriver bone;

        private void OnCollisionEnter(Collision collision) { if (listener != null) listener.OnBoneCollision(collision, this); }
        private void OnCollisionStay(Collision collision) { if (listener != null) listener.OnBoneCollision(collision, this); }
    }

    [System.Serializable]
    public class CollisionReactionGroup
    {
        public string groupName;
        [Tooltip("被击中时需要放松的骨骼索引。")]
        public int[] boneIndices = new int[0];

        [Range(0f, 1f)]
        [Tooltip("受击时 anchor 权重放松到的倍率。")]
        public float anchorRelaxMultiplier = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("受击时驱动权重放松到的倍率。")]
        public float driveRelaxMultiplier = 0.5f;
        [Tooltip("受击期间施加的额外阻尼。")]
        public float collisionDrag = 2f;
        [Tooltip("放松效果淡入时间（秒）。")]
        public float blendInTime = 0.05f;
        [Tooltip("放松效果淡出时间（秒）。")]
        public float blendOutTime = 1f;

        public bool isReacting { get; private set; }

        private float reactionAmount = 0f;
        private float reactionTimer = -100f;
        private float reactionVel;

        public void ProcessImpact(Collision collision, BoneCollisionBroadcaster broadcaster)
        {
            bool match = false;
            for (int i = 0; i < boneIndices.Length; i++) { if (broadcaster.bone.boneIndex == boneIndices[i]) { match = true; break; } }
            if (!match) return;
            reactionTimer = Time.time;
            isReacting = true;
        }

        public void UpdateReaction(ArticulationSyncer syncer)
        {
            if (!isReacting) return;

            bool isCollapsed = syncer.masterAnchorWeight <= 0f;
            float targetReaction = Time.time > reactionTimer + 0.2f ? 0f : 1f;
            if (isCollapsed) targetReaction = 1f;

            float sDampTime = targetReaction > reactionAmount ? blendInTime : blendOutTime;
            reactionAmount = Mathf.SmoothDamp(reactionAmount, targetReaction, ref reactionVel, sDampTime);
            if (targetReaction < reactionAmount && reactionAmount < 0.001f) reactionAmount = 0f;

            float drag = isCollapsed ? 0f : collisionDrag * reactionAmount;

            for (int i = 0; i < boneIndices.Length; i++)
            {
                int idx = boneIndices[i];
                if (idx < 0 || idx >= syncer.physicsBones.Length) continue;
                var driver = syncer.physicsBones[idx];
                driver.anchorMultiplier = Mathf.Lerp(1f, anchorRelaxMultiplier, reactionAmount);
                driver.driveMultiplier = Mathf.Lerp(1f, driveRelaxMultiplier, reactionAmount);
                if (driver.body != null)
                {
                    driver.body.linearDamping = drag;
                    driver.body.angularDamping = isCollapsed ? 0.05f : drag;
                }
            }

            if (reactionAmount <= 0f) isReacting = false;
        }
    }

    [DefaultExecutionOrder(100)]
    public class ArticulationSyncer : MonoBehaviour, IBoneCollisionListener
    {
        [Header("核心引用")]
        [Tooltip("可见的物理布偶根节点（通常是 Hips 髋部）。")]
        public Transform physRoot;
        [Tooltip("隐藏的、由动画驱动的 Ghost 骨架根节点，它决定了目标姿势。")]
        public Transform ghostRoot;
        [Tooltip("物理头部骨骼，用于计算质心和摔倒阈值。")]
        public Transform physHead;

        [Header("肌肉力（权重与刚度）")]
        [Range(0f, 1f)]
        [Tooltip("在关节驱动之上额外施加的速度匹配强度。0 = 纯驱动（ArticulationBody 推荐）。髋部的世界跟随由 root-follow PD 处理。")]
        public float masterAnchorWeight = 0f;

        [Range(0f, 1f)]
        [Tooltip("[控制站稳] 肌肉总开关。1.0 = 关节全力维持动画姿势；0.0 = 关节自由摆动（瘫倒）。摔倒时系统会把它清零。")]
        public float masterDriveWeight = 1f;

        [Range(0f, 30f)]
        [Tooltip("在关节驱动响应前，先平滑掉 Ghost 动画里的高频抖动。0 = 即时追随；5~25 = 平滑追随。不影响能否站住，影响追随是否干净。")]
        public float poseTrackingSpeed = 5f;

        [Range(0f, 20000f)]
        [Tooltip("[控制站稳] 肌肉刚度——把关节拉回目标姿势的力，是站稳的主力。调大更硬更撑得住，太大会抖；太小撑不住自重。")]
        public float driveSpring = 5000f;

        [Range(0f, 500f)]
        [Tooltip("[控制站稳] 肌肉阻尼——抑制回弹和抖动。调大更稳但反应变慢；太小关节会像弹簧一样颤。")]
        public float driveDamper = 50f;

        [Range(0f, 2000f)]
        [Tooltip("[控制站稳] 每个关节能输出的最大力矩上限。常见瓶颈：刚度再大也会被这个值削顶，撑不住自重时优先调大它。")]
        public float servoMaxForce = 150f;

        [Range(0f, 200f)]
        [Tooltip("[控制站稳] 髋部位置跟随力。物理骨架是浮动基座，靠这个 PD 把髋部拉到 Ghost 髋部位置，防止整体下沉到地面。会乘以 masterDriveWeight。")]
        public float rootFollowPosStrength = 25f;

        [Range(0f, 200f)]
        [Tooltip("[控制站稳] 髋部朝向跟随力矩。把躯干转正立直。太小上身会歪倒。会乘以 masterDriveWeight。")]
        public float rootFollowRotStrength = 25f;

        [Range(0f, 2000f)]
        [Tooltip("[防反折] 关节限位维持刚度。即使肌肉力归零（瓦倒），每个关节仍保留这个最小弹簧把肢体拉回解剖学限位内，防止膝盖反折/手臂超伸。0 = 关闭（瓦倒时关节会被重力/撞击压穿限位）。")]
        public float limitStiffnessFloor = 300f;

        [Range(0f, 2000f)]
        [Tooltip("[防反折] 配合限位维持刚度的最小力矩上限。太小则限位弹簧有刚度但出不了力，仍会被压穿。")]
        public float limitForceFloor = 200f;

        [Header("肌肉 vs 平衡仲裁 (balanceUrgency 统一控制)")]
        [Tooltip("[统一仲裁] 让肌肉总权重受 balanceUrgency 控制：urgency 升高时肌肉从'维持动画'平滑过渡到'交给物理求生'，替代过去站立=1 / 摔倒=0 的二值切换。关闭 = 肌肉恒为 masterDriveWeight（旧行为）。")]
        public bool urgencyDrivesMuscle = true;

        [Range(0f, 1f)]
        [Tooltip("[统一仲裁] 低于此 urgency，肌肉全力维持动画姿势（动画表演主导）。")]
        public float muscleUrgencyLo = 0.6f;

        [Range(0f, 1f)]
        [Tooltip("[统一仲裁] 达到此 urgency，肌肉放松到下面的最低权重（物理求生主导）。")]
        public float muscleUrgencyHi = 1f;

        [Range(0f, 1f)]
        [Tooltip("[统一仲裁] urgency 饱和时肌肉总权重的下限。0 = 完全瘫软交给物理；0.2~0.3 = 保留一点姿态张力，让动画'被物理压变形'而非直接散架。真正摔倒仍由独立的 FallOver 把驱动清零。")]
        public float muscleDriveAtFullUrgency = 0.25f;

        [Header("骨骼与碰撞")]
        [Tooltip("会触发受击反应和踉跄机制的物理层。")]
        public LayerMask impactLayers;

        [Tooltip("定义被击中时临时放松驱动的骨骼分组。")]
        public CollisionReactionGroup[] impactReactionGroups = new CollisionReactionGroup[0];

        [Tooltip("把物理 ArticulationBody 与对应 Ghost 骨骼关联起来的内部数组。由 ActiveRagdollBrain 生成器自动填充。")]
        public ArticulationDriver[] physicsBones = new ArticulationDriver[0];

        [Tooltip("LOD 物理更新分频。1 = 每个 FixedUpdate；2 = 每隔一帧（半速），以此类推。由 PhysicsLODController 设置。")]
        public int updateDivider = 1;

        [Header("平衡（LIPM 倒立摆）")]
        [Tooltip("可选的捕获点（Capture Point）解算器。留空会在本物体上自动创建。")]
        public CapturePointSolver capturePoint;

        [Tooltip("可选的紧迫度仲裁器。提供 Ghost 质心速度和紧迫度，用于在捕获点的质心速度上混合（动画 vs 物理）。为空 = 纯物理行为。")]
        public BalanceUrgencyEvaluator urgency;

        [Tooltip("可选的手部/武器接触支撑源。其激活的接触点会扩展支撑多边形。")]
        public HandContactSupport[] handContacts = new HandContactSupport[0];

        [Header("生存与摔倒逻辑")]
        [Tooltip("是否启用摔倒判定。关闭后角色永远不会自动摔倒。")]
        public bool enableFallLogic = true;
        [Tooltip("碰撞冲量转化为摔倒判定的灵敏度系数。")]
        public float collisionSensitivity = 0.5f;
        [Tooltip("下肢碰撞冲量超过此阈值即判定摔倒。")]
        public float fallThreshold = 10.0f;
        [Tooltip("脚部偏离 Ghost 脚部超过此距离（米）计为失衡。")]
        public float maxFootDragDistance = 0.8f;
        [Tooltip("失衡（捕获点/倾角/脚部拖拽）必须持续这么多秒才真正瘫倒。给肌肉驱动时间把身体拉回动画姿势，避免单帧瞬时扰动就锁死永久摔倒。")]
        public float fallConfirmTime = 0.4f;

        [Header("悬崖摔落检测")]
        [Tooltip("是否启用悬崖检测：脚下没有地面时立即瘫倒（无防抖延迟）。注意：地面层（GroundLayers）配错会导致一开始就瞬间瘫倒。")]
        public bool enableLedgeFall = true;
        [Tooltip("脚下向下检测地面的距离（米）。超过此距离没有地面即判定悬空。")]
        public float ledgeFallThreshold = 0.4f;
        [Tooltip("质心相对支撑点的倾斜角超过此值（度）计为失衡。")]
        public float maxLeanAngle = 35f;

        [Header("平衡恢复（起身后）")]
        [Tooltip("起身动画把控制权交回平衡循环后，肌肉/IK 权重在这么多秒内逐渐恢复到基准值。")]
        public float balanceResumeTime = 1.5f;

        public bool IsFallen { get; private set; }
        public bool IsUnstable { get; private set; }
        public bool IsFullyCollapsed { get; private set; }

        private HashSet<Transform> _lowerBodyBones = new HashSet<Transform>();
        private float _baseDriveWeight;
        private float _getUpGraceTimer = 0f;

        [Tooltip("判定已站立的髋部高度阈值。")]
        public float standUpHeight = 0.9f;
        public float GetUpGraceTimer => _getUpGraceTimer;
        private float _baseAnchorWeight;
        private IKSolver _ikSolver;
        private float _fallTimer;
        private float _characterHeight = 1.5f;
        private float _minY, _maxY;

        private readonly List<BoxCollider> _footColliders = new List<BoxCollider>();
        private readonly List<Vector3> _supportCorners = new List<Vector3>(16);

        private void Start()
        {
            if (capturePoint == null)
                capturePoint = GetComponent<CapturePointSolver>() ?? gameObject.AddComponent<CapturePointSolver>();

            _baseAnchorWeight = masterAnchorWeight;
            _baseDriveWeight = masterDriveWeight;
            // The running Animator lives on the Ghost_Rig (the physical rig has none, so its bones
            // are not fought over). Source the head bone and the lower-body collision-ignore set
            // from the ghost animator, then map each ghost bone to its paired physical transform
            // via physicsBones (Ghost is a structural clone of Physical, so the pairing is 1:1).
            var ghostAnim = ghostRoot != null ? ghostRoot.GetComponentInParent<Animator>() : null;

            if (ghostAnim != null)
            {
                Transform ghostHead = ghostAnim.GetBoneTransform(HumanBodyBones.Head);

                var ghostLowerBody = new HashSet<Transform>();
                if (ghostAnim.isHuman)
                {
                    ghostLowerBody.Add(ghostAnim.GetBoneTransform(HumanBodyBones.LeftLowerLeg));
                    ghostLowerBody.Add(ghostAnim.GetBoneTransform(HumanBodyBones.RightLowerLeg));
                    ghostLowerBody.Add(ghostAnim.GetBoneTransform(HumanBodyBones.LeftFoot));
                    ghostLowerBody.Add(ghostAnim.GetBoneTransform(HumanBodyBones.RightFoot));
                    ghostLowerBody.Add(ghostAnim.GetBoneTransform(HumanBodyBones.LeftToes));
                    ghostLowerBody.Add(ghostAnim.GetBoneTransform(HumanBodyBones.RightToes));
                    ghostLowerBody.RemoveWhere(t => t == null);
                }

                foreach (var b in physicsBones)
                {
                    if (b == null || b.ghostBone == null || b.body == null) continue;
                    Transform physBone = b.body.transform;
                    if (physHead == null && b.ghostBone == ghostHead) physHead = physBone;
                    if (ghostLowerBody.Contains(b.ghostBone)) _lowerBodyBones.Add(physBone);
                }
                _lowerBodyBones.RemoveWhere(t => t == null);
            }

            if (ghostRoot != null)
            {
                _maxY = float.MinValue;
                _minY = float.MaxValue;
                foreach (var b in physicsBones)
                {
                    if (b.ghostBone != null)
                    {
                        float y = ghostRoot.InverseTransformPoint(b.ghostBone.position).y;
                        if (y > _maxY) _maxY = y;
                        if (y < _minY) _minY = y;
                    }
                }
                _characterHeight = Mathf.Max(0.1f, _maxY - _minY);
            }

            foreach (var b in physicsBones)
            {
                b.InitializeBone(physicsBones);
                if (b.body != null)
                {
                    var broadcaster = b.body.gameObject.AddComponent<BoneCollisionBroadcaster>();
                    broadcaster.listener = this;
                    broadcaster.bone = b;
                }
            }
            CacheFootColliders();
            IgnoreInternalCollisions();

            if (ghostRoot != null)
            {
                _ikSolver = ghostRoot.GetComponent<IKSolver>();
                if (_ikSolver != null) _ikSolver.PhysicsSyncer = this;
            }

            StartCoroutine(DiagnoseComAndPoseError());
            StartCoroutine(DiagnoseSpineDrive());
        }

        // Drive-vs-actual introspection for the fold initiator (Spine1). Test A (servoMaxForce 150->600
        // still folds) ruled out saturation, so the question is whether the CUSTOM reduced-space drive
        // is even commanding "upright". Each pass logs:
        //  - tgt(x,y,z): the drive targets the custom ApplyDrive wrote (degrees). If the ghost spine is
        //    upright these should be ~0; a large forward target means ToReducedSpace / axis mapping is
        //    sending the joint forward (a target bug, not a strength bug).
        //  - actual: the body's real jointPosition per DOF (degrees). If tgt~0 but actual grows, the
        //    drive commands upright yet the joint won't follow (drive ineffective on the fold axis /
        //    wrong axis / limit pushing it) — strength won't fix that.
        //  - locks + xK/xF: which axes are driven and the stiffness/force actually applied, to confirm
        //    the fold axis is genuinely receiving a spring.
        // Remove after diagnosis.
        private IEnumerator DiagnoseSpineDrive()
        {
            ArticulationDriver spine = null;
            foreach (var b in physicsBones)
                if (b != null && b.body != null && b.boneName != null &&
                    b.boneName.IndexOf("Spine1", System.StringComparison.OrdinalIgnoreCase) >= 0) { spine = b; break; }
            if (spine == null) { Debug.Log("<color=magenta>[SpineDrive]</color> Spine1 not found in physicsBones"); yield break; }
            var body = spine.body;

            Debug.Log($"<color=magenta>[SpineDrive]</color> locks: twist={body.twistLock} swingY={body.swingYLock} swingZ={body.swingZLock} | dofCount={body.dofCount} (jointPosition lists unlocked DOFs in x,y,z order)");

            yield return new WaitForSeconds(2f);
            for (int pass = 0; pass < 9; pass++)
            {
                var jp = body.jointPosition;
                string actual = "";
                for (int i = 0; i < body.dofCount; i++)
                    actual += (jp[i] * Mathf.Rad2Deg).ToString("F1") + (i < body.dofCount - 1 ? "," : "");
                float localErr = spine.ghostBone != null ? Quaternion.Angle(body.transform.localRotation, spine.ghostBone.localRotation) : 0f;

                Debug.Log($"<color=magenta>[SpineDrive t={2f + pass * 1.5f:F1}s]</color> " +
                          $"tgt(x,y,z)=({body.xDrive.target:F1},{body.yDrive.target:F1},{body.zDrive.target:F1})° " +
                          $"actual=({actual})° | xK={body.xDrive.stiffness:F0} xF={body.xDrive.forceLimit:F0} | localErr={localErr:F1}°");
                yield return new WaitForSeconds(1.5f);
            }
        }

        // Progressive-fold tracker. The forward lean is NOT a transient that settles: it slowly folds
        // the torso forward until a joint hits its anatomical limit (~90° at the waist). A 4s window or
        // a root-only tilt probe misses it (the hips barely move while the SPINE folds). This samples
        // the full fold over ~17s and logs, each pass:
        //  - COMfwd: COM offset ahead of the foot-support center along the ghost's forward axis (+前).
        //    Watch it climb from negative toward positive as the upper body folds over the feet.
        //  - headTilt / rootTilt: world tilt-from-start of the HEAD (end of the torso chain) vs the
        //    HIPS (root). headTilt >> rootTilt confirms the fold is in the spine, not the base.
        //  - worst joints: the top-3 LOCAL per-joint tracking errors (physical local vs ghost local),
        //    which name the actual joint that is creeping toward its limit. The drive on THAT joint is
        //    the one too weak / force-limited to hold the pose against gravity.
        // Remove after diagnosis.
        private IEnumerator DiagnoseComAndPoseError()
        {
            ArticulationDriver head = null, root = null;
            foreach (var b in physicsBones)
            {
                if (b == null || b.body == null) continue;
                if (b.body.isRoot) root = b;
                if (head == null && b.boneName != null && b.boneName.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0) head = b;
            }
            yield return new WaitForSeconds(2f);
            Quaternion headInit = head != null ? head.body.transform.rotation : Quaternion.identity;
            Quaternion rootInit = root != null ? root.body.transform.rotation : Quaternion.identity;

            for (int pass = 0; pass < 10; pass++)
            {
                Vector3 comSum = Vector3.zero; float massSum = 0f;
                foreach (var b in physicsBones)
                    if (b != null && b.body != null) { comSum += b.body.worldCenterOfMass * b.body.mass; massSum += b.body.mass; }
                Vector3 com = massSum > 0f ? comSum / massSum : Vector3.zero;

                Vector3 support = Vector3.zero; int n = 0;
                foreach (var box in _footColliders)
                    if (box != null) { support += box.bounds.center; n++; }
                if (n == 0)
                    foreach (var b in physicsBones)
                        if (b != null && b.boneTransform != null && IsFootBone(b.boneName)) { support += b.boneTransform.position; n++; }
                if (n > 0) support /= n;

                Vector3 fwd = ghostRoot != null ? ghostRoot.forward : Vector3.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 off = com - support; off.y = 0f;
                float fOff = Vector3.Dot(off, fwd) * 100f;

                float headTilt = head != null ? Quaternion.Angle(headInit, head.body.transform.rotation) : 0f;
                float rootTilt = root != null ? Quaternion.Angle(rootInit, root.body.transform.rotation) : 0f;

                // Top-3 LOCAL per-joint tracking error (which joint is flexing away from the animation).
                ArticulationDriver w1 = null, w2 = null, w3 = null;
                float a1 = 0f, a2 = 0f, a3 = 0f;
                foreach (var b in physicsBones)
                {
                    if (b == null || b.body == null || b.ghostBone == null) continue;
                    float ang = Quaternion.Angle(b.body.transform.localRotation, b.ghostBone.localRotation);
                    if (ang > a1) { a3 = a2; w3 = w2; a2 = a1; w2 = w1; a1 = ang; w1 = b; }
                    else if (ang > a2) { a3 = a2; w3 = w2; a2 = ang; w2 = b; }
                    else if (ang > a3) { a3 = ang; w3 = b; }
                }

                float t = 2f + pass * 1.5f;
                Debug.Log($"<color=cyan>[Fold t={t:F1}s]</color> COMfwd={fOff:F1}cm | headTilt={headTilt:F0}° rootTilt={rootTilt:F0}° | worst joints: " +
                          $"{(w1 != null ? w1.boneName : "-")}={a1:F0}° {(w2 != null ? w2.boneName : "-")}={a2:F0}° {(w3 != null ? w3.boneName : "-")}={a3:F0}°");

                yield return new WaitForSeconds(1.5f);
            }
        }

        private void CacheFootColliders()
        {
            _footColliders.Clear();
            if (physRoot == null) return;
            foreach (var box in physRoot.GetComponentsInChildren<BoxCollider>())
            {
                if (box.name == "Foot_Collider") _footColliders.Add(box);
            }
        }

        private void IgnoreInternalCollisions()
        {
            if (physRoot == null) return;
            var bodies = physRoot.GetComponentsInChildren<ArticulationBody>();
            foreach (var body in bodies)
            {
                var parent = body.transform.parent != null ? body.transform.parent.GetComponentInParent<ArticulationBody>() : null;
                if (parent == null) continue;

                foreach (var myCol in body.GetComponentsInChildren<Collider>())
                {
                    if (myCol.transform.GetComponentInParent<ArticulationBody>() != body) continue;
                    foreach (var parentCol in parent.GetComponents<Collider>())
                        Physics.IgnoreCollision(myCol, parentCol, true);
                }
            }
        }

        // Returns the physical bone transform that mirrors the given ghost transform.
        public Transform GetPhysicalBodyFromGhost(Transform ghostTransform)
        {
            foreach (var b in physicsBones)
            {
                if (b.ghostBone == ghostTransform) return b.boneTransform;
            }
            return null;
        }

        // Explicit external-force entry point: injects a world-space impulse at a contact point into
        // the physical rig. The force is applied to the nearest non-foot bone (feet are the support
        // base; pushing them would just kick the support polygon). The disturbance then propagates
        // through the COM and surfaces in the capture point, so the balance loop responds naturally.
        public void ApplyExternalImpulse(Vector3 worldPoint, Vector3 worldForce)
        {
            if (physicsBones == null || physicsBones.Length == 0) return;

            ArticulationBody best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < physicsBones.Length; i++)
            {
                var b = physicsBones[i];
                if (b == null || b.body == null) continue;
                if (IsFootBone(b.boneName)) continue;

                float sqr = (b.body.worldCenterOfMass - worldPoint).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = b.body; }
            }

            if (best != null)
                best.AddForceAtPosition(worldForce, worldPoint, ForceMode.Impulse);
        }

        private static bool IsFootBone(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return false;
            return boneName.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) >= 0
                || boneName.IndexOf("Toe", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void FixedUpdate()
        {
            // LOD frame-skip: at a distance the physics solve runs at a fraction of the fixed rate.
            if (updateDivider > 1 && Time.frameCount % updateDivider != 0) return;

            ProcessSurvivalLogic(Time.fixedDeltaTime);
            ProcessImpactReactions();

            foreach (var b in physicsBones) b.CaptureGhostState(poseTrackingSpeed);

            // The passive limit spring only matters while collapsed: with muscles off, it stops the
            // joints being pushed through their anatomical range. While standing the muscle spring far
            // exceeds it, so pass 0 to leave normal balance behavior untouched.
            float limStiff = IsFallen ? limitStiffnessFloor : 0f;
            float limForce = IsFallen ? limitForceFloor : 0f;

            // Unification: the muscle master weight is governed by the balance arbiter so the
            // animation->physics handoff is continuous (urgency 0 = full animation pose-holding;
            // rising urgency relaxes the muscles toward the physics-survival blend). masterDriveWeight
            // still carries the fall/get-up gating, so this only modulates it while alive and standing.
            // The joint-rigidity floor (limStiff/limForce) is intentionally NOT urgency-gated.
            float effectiveDrive = masterDriveWeight * MuscleUrgencyScale();

            foreach (var b in physicsBones)
            {
                b.ApplyDrive(effectiveDrive, driveSpring, driveDamper, servoMaxForce, limStiff, limForce);
                if (b.body != null && b.body.isRoot)
                    b.ApplyRootFollow(effectiveDrive, rootFollowPosStrength, rootFollowRotStrength);
                else if (masterAnchorWeight > 0f)
                    b.ApplyAnchor(masterAnchorWeight);
            }
        }

        // Maps balance urgency -> a 0..1 multiplier on the muscle master weight. Below muscleUrgencyLo
        // the muscles fully hold the animation pose; past it they relax toward muscleDriveAtFullUrgency,
        // giving the smooth "animation deforms under physics" handoff before the discrete FallOver cuts
        // drive entirely. Skipped during the get-up grace ramp (that ramp owns masterDriveWeight) and
        // when no arbiter is wired, both of which return 1 (unchanged behavior).
        private float MuscleUrgencyScale()
        {
            if (!urgencyDrivesMuscle || urgency == null || _getUpGraceTimer > 0f) return 1f;
            float relax = Mathf.Clamp01(Mathf.InverseLerp(muscleUrgencyLo, muscleUrgencyHi, urgency.Urgency));
            return Mathf.Lerp(1f, muscleDriveAtFullUrgency, relax);
        }

        private void ProcessImpactReactions()
        {
            foreach (var group in impactReactionGroups) group.UpdateReaction(this);
        }

        public void OnBoneCollision(Collision collision, BoneCollisionBroadcaster broadcaster)
        {
            if (!enableFallLogic || IsFallen) return;
            if (collision.collider.transform.root == transform) return;
            if ((impactLayers.value & (1 << collision.collider.gameObject.layer)) == 0) return;

            bool isLowerLeg = _lowerBodyBones.Contains(broadcaster.bone.boneTransform);

            // Lower legs ignore flat ground contacts so the character doesn't "trip" on the floor.
            if (isLowerLeg && collision.contacts.Length > 0 && collision.contacts[0].normal.y > 0.5f)
                return;

            foreach (var group in impactReactionGroups) group.ProcessImpact(collision, broadcaster);

            if (isLowerLeg)
            {
                float impactForce = collision.impulse.magnitude * collisionSensitivity;
                if (impactForce > fallThreshold) FallOver();
            }
        }

        private void ProcessSurvivalLogic(float dt)
        {
            // Collapsed: stay limp on the ground, muscles/IK off, until an external get-up
            // animation finishes and calls ResumeBalancing(). Self-balancing's job ends here.
            if (IsFallen)
            {
                if (_ikSolver != null)
                {
                    _ikSolver.IKBlend = 0f;
                    _ikSolver.ResetBlendTarget(0f);
                    _ikSolver.UseFootGluing = false;
                }
                foreach (var b in physicsBones)
                    if (b.body != null) b.body.maxDepenetrationVelocity = 4f;
                return;
            }

            // Re-engagement ramp triggered by ResumeBalancing(): smoothly fade muscle/IK weights
            // back to their baseline so the hand-off from the get-up animation has no pop/collapse.
            if (_getUpGraceTimer > 0f)
            {
                _getUpGraceTimer -= dt;

                if (ghostRoot != null)
                {
                    Vector3 eulers = ghostRoot.rotation.eulerAngles;
                    ghostRoot.rotation = Quaternion.Euler(0f, eulers.y, 0f);
                }

                float s = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(_getUpGraceTimer / Mathf.Max(balanceResumeTime, 0.01f)));
                masterAnchorWeight = Mathf.Lerp(0.15f, _baseAnchorWeight, s);
                masterDriveWeight = Mathf.Lerp(0.05f, _baseDriveWeight, s);

                if (_ikSolver != null)
                {
                    _ikSolver.IKBlend = s;
                    _ikSolver.ResetBlendTarget(s);
                }

                float depen = Mathf.Lerp(0.5f, 10f, s);
                foreach (var b in physicsBones)
                {
                    if (b.body != null)
                    {
                        b.body.maxDepenetrationVelocity = depen;
                        b.body.angularVelocity = Vector3.Lerp(b.body.angularVelocity, Vector3.zero, dt * 15f);
                    }
                }
                if (enableFallLogic) CalculateStability(dt);
            }
            else if (enableFallLogic)
            {
                CalculateStability(dt);
            }
        }

        private void CalculateStability(float dt)
        {
            // Manual-override preview: keep evaluating the capture point (for debug/COM blend)
            // but never auto-collapse, so the editor can preview the blended pose at a fixed
            // urgency without the fall logic killing the rig. Turn override off for real falls.
            bool previewLock = urgency != null && urgency.overrideUrgency;

            Vector3 comSum = Vector3.zero;
            float massSum = 0f;
            float lowestY = float.MaxValue;

            foreach (var b in physicsBones)
            {
                if (b.body != null)
                {
                    comSum += b.body.worldCenterOfMass * b.body.mass;
                    massSum += b.body.mass;
                    float py = b.boneTransform.position.y;
                    if (py < lowestY) lowestY = py;
                }
            }
            if (massSum <= 0) return;
            Vector3 centerOfMass = comSum / massSum;

            Vector3 supportCenter = Vector3.zero;
            int supportCount = 0;
            foreach (var b in physicsBones)
            {
                if (b.body != null && b.boneTransform.position.y <= lowestY + 0.2f)
                {
                    supportCenter += b.boneTransform.position;
                    supportCount++;
                }
            }
            if (supportCount > 0) supportCenter /= supportCount;
            else supportCenter = physRoot.position;

            // Soft triggers below mark the rig as "losing balance" but DON'T collapse immediately.
            // The muscle drives are given fallConfirmTime to pull the rig back to the animated pose;
            // only sustained instability commits to FallOver. This is what lets the muscle module
            // hold the pose against gravity instead of a one-frame transient latching a permanent fall.
            bool losingBalance = false;

            // LIPM capture-point balance test (primary fall trigger).
            if (capturePoint != null)
            {
                BuildSupportCorners();
                CapturePointSolver.BalanceState state;
                if (urgency != null)
                {
                    Vector3 locomotionVel = _ikSolver != null ? _ikSolver.SmoothedVelocity : Vector3.zero;
                    state = capturePoint.Evaluate(centerOfMass, urgency.GhostComVelocity, urgency.Urgency, dt, _supportCorners, locomotionVel);
                }
                else
                    state = capturePoint.Evaluate(centerOfMass, dt, _supportCorners);
                IsUnstable = state != CapturePointSolver.BalanceState.Balanced;
                if (state == CapturePointSolver.BalanceState.Falling) losingBalance = true;
            }

            // Manual-override preview never collapses (debug/preview only).
            if (previewLock) { _fallTimer = 0f; return; }

            // Ledge fall is a HARD condition (no ground underfoot): collapse instantly, no debounce.
            if (enableLedgeFall)
            {
                float rayStartHeight = 0.2f;
                Vector3 rayStart = supportCenter + (Vector3.up * rayStartHeight);
                float checkDistance = ledgeFallThreshold + rayStartHeight;
                LayerMask groundMask = _ikSolver != null ? _ikSolver.GroundLayers : impactLayers;

                if (!Physics.Raycast(rayStart, Vector3.down, checkDistance, groundMask))
                {
                    FallOver(true);
                    return;
                }
            }

            // Secondary fallbacks (lean angle + foot drag), retained from the original system.
            Vector3 supportToCoM = centerOfMass - supportCenter;
            float currentLeanAngle = Vector3.Angle(Vector3.up, supportToCoM);
            if (capturePoint == null) IsUnstable = currentLeanAngle > (maxLeanAngle * 0.7f);

            if (currentLeanAngle > maxLeanAngle) losingBalance = true;

            if (!losingBalance)
            {
                foreach (var b in physicsBones)
                {
                    string n = b.boneName.ToLower();
                    if (n.Contains("foot") || n.Contains("toe") || n.Contains("calf") || n.Contains("shin"))
                    {
                        if (b.ghostBone == null) continue;
                        float dragDistance = Vector3.Distance(b.boneTransform.position, b.ghostBone.position);
                        if (dragDistance > maxFootDragDistance) { losingBalance = true; break; }
                    }
                }
            }

            // Debounce: collapse only after instability persists past fallConfirmTime; recover resets it.
            if (losingBalance)
            {
                _fallTimer += dt;
                if (_fallTimer >= fallConfirmTime) { FallOver(); return; }
            }
            else
            {
                _fallTimer = 0f;
            }
        }

        private void BuildSupportCorners()
        {
            _supportCorners.Clear();
            foreach (var box in _footColliders)
            {
                if (box == null) continue;
                AppendBoxBaseCorners(box, _supportCorners);
            }

            // Phase-2: a hand (or weapon tip) pressing on static geometry extends the support
            // polygon. The contact source self-gates by urgency, so a light brush won't register.
            if (handContacts != null)
            {
                foreach (var hc in handContacts)
                {
                    if (hc != null && hc.HasActiveSupport)
                        _supportCorners.Add(hc.ContactPoint);
                }
            }
        }

        // Appends the 4 lowest corners of the box collider in world space (its ground-contact face).
        private static void AppendBoxBaseCorners(BoxCollider box, List<Vector3> dst)
        {
            Vector3 c = box.center;
            Vector3 e = box.size * 0.5f;
            Transform t = box.transform;
            // 8 corners, then we keep all of them; the solver's convex hull + min-Y handle selection.
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 local = c + new Vector3(e.x * sx, e.y * sy, e.z * sz);
                        dst.Add(t.TransformPoint(local));
                    }
        }

        private void FallOver(bool instant = false)
        {
            IsFallen = true;
            IsFullyCollapsed = true;
            _getUpGraceTimer = 0f;
            _fallTimer = 0f;

            masterAnchorWeight = 0f;
            masterDriveWeight = 0f;

            if (capturePoint != null) capturePoint.ResetState();

            // The Animator is never disabled — animation runs by default. On fall the physical rig
            // goes slack (drive/anchor weights zeroed above); the ghost keeps playing its pose so a
            // future get-up animation can drive it back to standing before ResumeBalancing() is called.

            if (_ikSolver != null)
            {
                _ikSolver.IKBlend = 0f;
                _ikSolver.ResetBlendTarget(0f);
                _ikSolver.UseFootGluing = false;
            }
        }

        // Hands control back to the self-balancing loop after an external get-up animation finishes.
        // The caller is responsible for having already driven the (kinematic) ghost to a standing pose
        // at the correct world position. This method re-enables animators/IK and ramps muscle and IK
        // weights from near-zero back to baseline over balanceResumeTime (handled in ProcessSurvivalLogic).
        public void ResumeBalancing(float rampTime = -1f)
        {
            if (!IsFallen) return;
            IsFallen = false;
            IsFullyCollapsed = false;
            _fallTimer = 0f;
            if (capturePoint != null) capturePoint.ResetState();

            // Animators are never disabled, so there is nothing to re-enable here.

            if (_ikSolver != null)
            {
                if (!_ikSolver.enabled) _ikSolver.enabled = true;
                var stepMgr = _ikSolver.GetComponent<StepManager>();
                if (stepMgr != null && !stepMgr.enabled) stepMgr.enabled = true;
                _ikSolver.UseFootGluing = true;
                _ikSolver.HipBody.ResetDynamics();
                _ikSolver.IKBlend = 0f;
                _ikSolver.ResetBlendTarget(0f);
                _ikSolver.ForceReglueAll();
            }

            _getUpGraceTimer = (rampTime > 0f) ? rampTime : balanceResumeTime;
        }

        public Vector3 GetPhysicalVelocity()
        {
            if (physicsBones.Length > 0 && physicsBones[0].body != null)
                return physicsBones[0].body.linearVelocity;
            return Vector3.zero;
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(ArticulationSyncer))]
        public class ArticulationSyncerEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                ArticulationSyncer script = (ArticulationSyncer)target;
                GUILayout.Space(10);
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("自动查找骨骼", GUILayout.Height(30)))
                {
                    Undo.RecordObject(script, "Auto Find Bones");
                    AutoFindBones(script);
                    EditorUtility.SetDirty(script);
                }
                GUI.backgroundColor = Color.white;
            }

            private void AutoFindBones(ArticulationSyncer script)
            {
                if (script.physRoot == null || script.ghostRoot == null)
                {
                    Debug.LogWarning("ArticulationSyncer：自动查找骨骼前请先指定 physRoot 和 ghostRoot。");
                    return;
                }

                var newBones = new List<ArticulationDriver>();
                Animator physAnim = script.physRoot.GetComponentInParent<Animator>();
                Animator ghostAnim = script.ghostRoot.GetComponentInParent<Animator>();
                bool useHumanoidMapping = (physAnim != null && physAnim.isHuman && ghostAnim != null && ghostAnim.isHuman);

                var bodies = script.physRoot.GetComponentsInChildren<ArticulationBody>();
                foreach (var body in bodies)
                {
                    Transform ghost = null;
                    if (useHumanoidMapping)
                    {
                        foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones)))
                        {
                            if (boneType == HumanBodyBones.LastBone) continue;
                            if (physAnim.GetBoneTransform(boneType) == body.transform)
                            {
                                ghost = ghostAnim.GetBoneTransform(boneType);
                                break;
                            }
                        }
                    }
                    if (ghost == null) ghost = FindMatchingBone(script.ghostRoot, body.name);
                    if (ghost == null) continue;

                    newBones.Add(new ArticulationDriver
                    {
                        body = body,
                        ghostBone = ghost,
                        boneName = body.name,
                        anchorMultiplier = 1f,
                        driveMultiplier = 1f,
                        damperMultiplier = 1f
                    });
                }

                script.physicsBones = newBones.ToArray();
            }

            private Transform FindMatchingBone(Transform root, string name)
            {
                if (root.name == name) return root;
                foreach (Transform child in root) { var res = FindMatchingBone(child, name); if (res != null) return res; }
                return null;
            }
        }
#endif
    }
}
