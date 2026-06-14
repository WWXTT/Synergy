using UnityEngine;

namespace FrostPunchGames
{
    [System.Serializable]
    public class HipBodySolver
    {
        private IKSolver _owner;
        private Transform _hips;
        private float _baseLegLength = 1.5f;
        private Quaternion _initialHipsLocalRot;
        private Vector3 _initialHipsLocalPos;
        private Vector3 _lastRootPosition;

        private float _standingHeight;
        private float _smoothedSpeed;

        private Vector3 _currentForwardDir;
        private Vector3 _rotDirVelocity;

        [Header("Biomechanical Weight Shift")]
        [Range(0f, 1f)]
        [Tooltip("How strongly the hips shift their center of gravity over the planted foot while walking.")]
        public float ZMPWeightShiftWalking = 0.85f;

        [Range(0f, 1f)]
        [Tooltip("How strongly the hips shift their center of gravity over the planted foot while standing idle.")]
        public float ZMPWeightShiftIdle = 0.1f;

        [Tooltip("The speed at which the system transitions between the walking and idle weight shift values.")]
        public float ZMPTransitionSpeed = 5f;

        [Tooltip("The vertical drop apex of the hips during a stride, simulating an inverted pendulum.")]
        public float InvertedPendulumApex = 0.15f;

        private float _currentZMPWeightShift;

        [Header("Active Balancing")]
        [Tooltip("How aggressively the torso leans in the opposite direction of a physical offset to counterbalance itself.")]
        public float CounterBalanceTilt = 35f;

        [Tooltip("The physical amplitude of the continuous procedural jitter applied to the hips to simulate micro-adjustments.")]
        public float MotorJitterAmplitude = 0.015f;

        [Tooltip("The speed of the procedural hip micro-adjustment jitter.")]
        public float MotorJitterSpeed = 4.5f;

        [Header("Dynamic Leaning")]
        [Tooltip("Multiplier for how far the spine leans forward dynamically based on forward velocity.")]
        public float ForwardLeanMultiplier = 4.0f;

        [Tooltip("Multiplier for how far the spine leans sideways when strafing.")]
        public float StrafeLeanMultiplier = 2.0f;

        [Range(0f, 1.5f)]
        [Tooltip("Multiplier for how much the character leans into or away from sloped terrain.")]
        public float HillLeanMultiplier = 0.6f;

        [Tooltip("Multiplier for how much the character banks their torso into a turn based on angular velocity.")]
        public float TurnBankMultiplier = 0.15f;

        [Tooltip("The absolute maximum angle the torso is allowed to bank during a sharp turn.")]
        public float MaxTurnBankAngle = 25f;

        [Tooltip("The absolute maximum angle the torso is allowed to lean forward while moving.")]
        public float MaxForwardLeanAngle = 25f;

        [Tooltip("The absolute maximum angle the torso is allowed to lean sideways while strafing.")]
        public float MaxStrafeLeanAngle = 10f;

        [Tooltip("The smoothing time applied to all procedural lean calculations.")]
        public float LeanSmoothTime = 0.15f;

        [Header("Pelvic Sway & Twist (Gait)")]
        [Tooltip("Multiplier for the vertical tilt of the pelvis during a walking stride.")]
        public float PelvicTiltMultiplier = 12f;

        [Tooltip("Multiplier for the forward and backward twist of the pelvis during a walking stride.")]
        public float PelvicTwistMultiplier = 15f;

        [Header("Human Spine (Contrapposto)")]
        [Tooltip("If true, procedural counter-rotations are applied to the spine and chest to keep the head stable.")]
        public bool EnableSpineFlexibility = true;

        [Range(0f, 2f)]
        [Tooltip("1.0 = Perfect mathematical counterbalance to keep the head plumb over the Center of Mass.")]
        public float ContrappostoStrength = 1.0f;

        private float _currentYaw;
        private float _yawVel;
        private Transform _spine;
        private Transform _chest;

        private Quaternion _initialSpineLocalRot;
        private Quaternion _initialChestLocalRot;

        private float _currentSpineYaw;
        private float _currentSpineRoll;
        private float _currentSpinePitch;

        [Header("Recovery & Elasticity")]
        [Tooltip("The stiffness of the procedural spring returning the hips to their center position after an offset.")]
        public float HipsSpringStiffness = 25f;

        [Tooltip("The damping applied to the hip spring to prevent endless bouncing.")]
        public float HipsSpringDamping = 6f;

        private Vector3 _hipsSpringVelocity;
        private Vector3 _currentHipsElasticOffset;

        [Header("Impact Reactions")]
        [Tooltip("How fast the character recovers their balance from impact stumble offsets.")]
        public float StumbleRecoverySpeed = 1.5f;

        [Tooltip("Multiplier for the torso twist and pitch applied upon receiving a physical impact.")]
        public float ImpactTorqueMultiplier = 0.5f;

        private Vector3 _stumbleOffset;
        private float _stumblePitch;
        private float _stumbleRoll;

        private float _currentPitch;
        private float _currentRoll;
        private float _pitchVel;
        private float _rollVel;

        public void Initialize(IKSolver owner)
        {
            _owner = owner;
            _hips = owner.Hips;
            _lastRootPosition = _owner.CharacterRoot.position;
            _currentZMPWeightShift = ZMPWeightShiftIdle;

            if (_owner.Animator != null && _owner.Animator.isHuman)
            {
                _spine = _owner.Animator.GetBoneTransform(HumanBodyBones.Spine);
                _chest = _owner.Animator.GetBoneTransform(HumanBodyBones.Chest);
                if (_chest == null) _chest = _owner.Animator.GetBoneTransform(HumanBodyBones.UpperChest);

                if (_spine != null) _initialSpineLocalRot = Quaternion.Inverse(_owner.CharacterRoot.rotation) * _spine.rotation;
                if (_chest != null) _initialChestLocalRot = Quaternion.Inverse(_owner.CharacterRoot.rotation) * _chest.rotation;
            }

            if (_hips != null && _owner.CharacterRoot != null)
            {
                Vector3 hipsLocal = _owner.CharacterRoot.InverseTransformPoint(_hips.position);
                _initialHipsLocalPos = hipsLocal;
                _initialHipsLocalRot = Quaternion.Inverse(_owner.CharacterRoot.rotation) * _hips.rotation;
                _currentForwardDir = _owner.CharacterRoot.forward;
                _standingHeight = Mathf.Abs(_initialHipsLocalPos.y);
                if (_standingHeight < 0.1f) _standingHeight = 0.8f;
            }
            CalculateLegMetrics();
        }

        private void CalculateLegMetrics()
        {
            if (_owner.Legs == null || _owner.Legs.Count == 0) return;
            float totalLen = 0f;
            int count = 0;
            foreach (var leg in _owner.Legs) { if (leg.IsValid) { totalLen += leg.GetLegLength(); count++; } }
            if (count > 0) _baseLegLength = totalLen / count;
        }

        public void ApplyImpact(Vector3 worldForceVector, Vector3 worldHitPoint)
        {
            if (_owner == null || _owner.CharacterRoot == null) return;
            Vector3 localForce = _owner.CharacterRoot.InverseTransformDirection(worldForceVector);

            localForce.x = Mathf.Clamp(localForce.x, -5f, 5f);
            localForce.y = Mathf.Clamp(localForce.y, -5f, 5f);
            localForce.z = Mathf.Clamp(localForce.z, -5f, 5f);

            _hipsSpringVelocity += localForce * 2.5f;

            _hipsSpringVelocity.x = Mathf.Clamp(_hipsSpringVelocity.x, -10f, 10f);
            _hipsSpringVelocity.y = Mathf.Clamp(_hipsSpringVelocity.y, -4f, 4f);
            _hipsSpringVelocity.z = Mathf.Clamp(_hipsSpringVelocity.z, -10f, 10f);

            _stumbleOffset.x += localForce.x * 0.8f;
            _stumbleOffset.z += localForce.z * 0.8f;
            _stumbleOffset.x = Mathf.Clamp(_stumbleOffset.x, -0.4f, 0.4f);
            _stumbleOffset.z = Mathf.Clamp(_stumbleOffset.z, -0.4f, 0.4f);

            Vector3 localHitOffset = _owner.CharacterRoot.InverseTransformPoint(worldHitPoint);
            Vector3 localTorque = Vector3.Cross(localHitOffset, localForce);

            _stumblePitch += localTorque.x * ImpactTorqueMultiplier;
            _stumbleRoll += localTorque.z * ImpactTorqueMultiplier;

            _stumblePitch = Mathf.Clamp(_stumblePitch, -45f, 45f);
            _stumbleRoll = Mathf.Clamp(_stumbleRoll, -45f, 45f);
        }

        public void Process()
        {
            if (_hips == null || _owner.CharacterRoot == null || _owner.Legs == null || _owner.Legs.Count == 0) return;

            float currentSpeed = Vector3.Distance(_owner.CharacterRoot.position, _lastRootPosition) / _owner.DeltaTime;
            _lastRootPosition = _owner.CharacterRoot.position;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, currentSpeed, _owner.DeltaTime * 15f);
            bool isMoving = _smoothedSpeed > 0.05f;

            float targetZMP = isMoving ? ZMPWeightShiftWalking : ZMPWeightShiftIdle;
            _currentZMPWeightShift = Mathf.Lerp(_currentZMPWeightShift, targetZMP, _owner.DeltaTime * ZMPTransitionSpeed);

            Vector3 supportCentroid = Vector3.zero;
            Vector3 averageGroundNormal = Vector3.zero;
            int supportCount = 0;
            float averageLegExtension = 0f;

            for (int i = 0; i < _owner.GetLegCount(); i++)
            {
                var leg = _owner.GetLegController(i);
                if (leg == null) continue;

                Vector3 footLocal = _owner.CharacterRoot.InverseTransformPoint(leg.CurrentFootPosition);

                if (leg.IsPlanted)
                {
                    supportCentroid += footLocal;
                    averageGroundNormal += leg.GroundHit.normal;
                    supportCount++;
                }

                float footDist = Vector2.Distance(Vector2.zero, new Vector2(footLocal.x, footLocal.z));
                averageLegExtension += footDist;
            }

            if (supportCount > 0)
            {
                supportCentroid /= supportCount;
                averageGroundNormal.Normalize();
            }
            else averageGroundNormal = Vector3.up;

            averageLegExtension /= _owner.GetLegCount();

            Vector3 targetElasticPos = Vector3.zero;

            if (supportCount > 0)
            {
                float jitterX = (Mathf.PerlinNoise(Time.time * MotorJitterSpeed, 0f) - 0.5f) * MotorJitterAmplitude;
                float jitterZ = (Mathf.PerlinNoise(0f, Time.time * MotorJitterSpeed) - 0.5f) * MotorJitterAmplitude;

                targetElasticPos.x = (supportCentroid.x * _currentZMPWeightShift) + jitterX;
                targetElasticPos.z = isMoving ? (supportCentroid.z * (_currentZMPWeightShift * 0.3f)) + jitterZ : jitterZ;
                targetElasticPos.y = -(averageLegExtension * InvertedPendulumApex);
            }

            _stumbleOffset = Vector3.MoveTowards(_stumbleOffset, Vector3.zero, _owner.DeltaTime * StumbleRecoverySpeed);
            targetElasticPos += _stumbleOffset;

            // LIPM hip strategy: shift the hips horizontally back over the support point to pull the
            // diverging COM/capture-point inward (complements the ankle strategy's torso lean below).
            // supportCentroid and cpLocal are both root-local. We move the hip toward -cpErr (support<-CP),
            // weighted by CaptureAuthority and clamped so the hip never exceeds a quarter leg length.
            // Sign of the shift to be visually calibrated in-editor against the CP gizmo (Module A).
            if (_owner.BalanceFeedbackActive)
            {
                float w = _owner.CaptureAuthority;
                if (w > 0f)
                {
                    Vector3 cpLocal = _owner.CharacterRoot.InverseTransformPoint(_owner.CapturePointWorld);
                    Vector3 hipShift = new Vector3(
                        -(cpLocal.x - supportCentroid.x), 0f, -(cpLocal.z - supportCentroid.z)) * _owner.CPHipShiftGain;
                    hipShift = Vector3.ClampMagnitude(hipShift, _baseLegLength * 0.25f);
                    targetElasticPos.x = Mathf.Lerp(targetElasticPos.x, targetElasticPos.x + hipShift.x, w);
                    targetElasticPos.z = Mathf.Lerp(targetElasticPos.z, targetElasticPos.z + hipShift.z, w);
                }
            }

            _stumblePitch = Mathf.Lerp(_stumblePitch, 0f, _owner.DeltaTime * StumbleRecoverySpeed);
            _stumbleRoll = Mathf.Lerp(_stumbleRoll, 0f, _owner.DeltaTime * StumbleRecoverySpeed);

            Vector3 force = (targetElasticPos - _currentHipsElasticOffset) * HipsSpringStiffness;
            Vector3 damping = _hipsSpringVelocity * HipsSpringDamping;
            Vector3 acceleration = force - damping;

            _hipsSpringVelocity += acceleration * _owner.DeltaTime;
            _currentHipsElasticOffset += _hipsSpringVelocity * _owner.DeltaTime;

            Vector3 baseHipsWorldPos = _owner.CharacterRoot.TransformPoint(_initialHipsLocalPos);
            Vector3 finalHipsPos = baseHipsWorldPos + _owner.CharacterRoot.TransformVector(_currentHipsElasticOffset);

            float maxStretch = _baseLegLength * 1.1f;
            if (Vector3.Distance(finalHipsPos, baseHipsWorldPos) > maxStretch)
            {
                Vector3 direction = (finalHipsPos - baseHipsWorldPos).normalized;
                finalHipsPos = baseHipsWorldPos + direction * maxStretch;
                _hipsSpringVelocity *= 0.5f;
                _currentHipsElasticOffset = _owner.CharacterRoot.InverseTransformVector(finalHipsPos - baseHipsWorldPos);
            }

            float absoluteMinY = baseHipsWorldPos.y - (_baseLegLength * 0.5f);
            if (finalHipsPos.y < absoluteMinY)
            {
                finalHipsPos.y = absoluteMinY;
                if (_hipsSpringVelocity.y < 0) _hipsSpringVelocity.y = 0f;
            }

            _hips.position = Vector3.Lerp(_hips.position, finalHipsPos, _owner.CurrentIKBlend);

            Vector3 localVelocity = _owner.CharacterRoot.InverseTransformDirection(_owner.SmoothedVelocity);
            Vector3 localNormal = _owner.CharacterRoot.InverseTransformDirection(averageGroundNormal);
            float hillPitch = Mathf.Atan2(-localNormal.z, localNormal.y) * Mathf.Rad2Deg;

            Vector3 physicalErrorOffset = _currentHipsElasticOffset + _stumbleOffset;
            float balancePitch = -physicalErrorOffset.z * CounterBalanceTilt;
            float balanceRoll = -physicalErrorOffset.x * CounterBalanceTilt;

            // LIPM ankle strategy: when the physical capture point overshoots the support polygon,
            // counter-lean the torso proportionally to the CP error (root-local), blended over the
            // spring heuristic by how far the CP has escaped. supportCentroid is already root-local.
            if (_owner.BalanceFeedbackActive)
            {
                float w = _owner.CaptureAuthority;
                if (w > 0f)
                {
                    Vector3 cpLocal = _owner.CharacterRoot.InverseTransformPoint(_owner.CapturePointWorld);
                    Vector3 cpErrLocal = cpLocal - supportCentroid;
                    float lipmPitch = -cpErrLocal.z * _owner.CPHipLeanGain;
                    float lipmRoll = -cpErrLocal.x * _owner.CPHipLeanGain;
                    balancePitch = Mathf.Lerp(balancePitch, lipmPitch, w);
                    balanceRoll = Mathf.Lerp(balanceRoll, lipmRoll, w);
                }
            }

            float forwardMovementFactor = Mathf.Clamp01(localVelocity.z / 3f);
            float turnBankRoll = _owner.AngularVelocity * -TurnBankMultiplier * forwardMovementFactor;
            turnBankRoll = Mathf.Clamp(turnBankRoll, -MaxTurnBankAngle, MaxTurnBankAngle);

            float combinedPitch = (localVelocity.z * ForwardLeanMultiplier) + (hillPitch * HillLeanMultiplier) + balancePitch + _stumblePitch;
            float combinedRoll = (localVelocity.x * -StrafeLeanMultiplier) + balanceRoll + _stumbleRoll + turnBankRoll;

            float targetPitch = Mathf.Clamp(combinedPitch, -MaxForwardLeanAngle, MaxForwardLeanAngle);
            float targetRoll = Mathf.Clamp(combinedRoll, -(MaxStrafeLeanAngle + MaxTurnBankAngle), MaxStrafeLeanAngle + MaxTurnBankAngle);
            float targetYaw = 0f;

            if (isMoving && supportCount > 0)
            {
                float swayRoll = -supportCentroid.x * PelvicTiltMultiplier;
                targetRoll += swayRoll;

                float leftZ = 0f, rightZ = 0f;
                int lCount = 0, rCount = 0;

                for (int i = 0; i < _owner.GetLegCount(); i++)
                {
                    var leg = _owner.GetLegController(i);
                    Vector3 localFoot = _owner.CharacterRoot.InverseTransformPoint(leg.CurrentFootPosition);
                    if (leg.Side == LegSide.Left) { leftZ += localFoot.z; lCount++; }
                    else { rightZ += localFoot.z; rCount++; }
                }

                if (lCount > 0 && rCount > 0)
                {
                    leftZ /= lCount;
                    rightZ /= rCount;
                    targetYaw = (leftZ - rightZ) * PelvicTwistMultiplier;
                }
            }

            _currentPitch = Mathf.SmoothDamp(_currentPitch, targetPitch, ref _pitchVel, LeanSmoothTime);
            _currentRoll = Mathf.SmoothDamp(_currentRoll, targetRoll, ref _rollVel, LeanSmoothTime);
            _currentYaw = Mathf.SmoothDamp(_currentYaw, targetYaw, ref _yawVel, LeanSmoothTime);

            bool hasValidAnimator = _owner.Animator != null && _owner.Animator.enabled && _owner.Animator.runtimeAnimatorController != null;
            bool useAnimator = _owner.UseAnimator && hasValidAnimator;
            if (_owner.ForceProceduralPoseIfNoAnimation && !hasValidAnimator) useAnimator = false;

            Quaternion baseRot = useAnimator ? _hips.rotation : (_owner.CharacterRoot.rotation * _initialHipsLocalRot);
            Quaternion hipLocalToRoot = Quaternion.Inverse(_owner.CharacterRoot.rotation) * baseRot;

            Quaternion spineBase = Quaternion.identity;
            Quaternion chestBase = Quaternion.identity;

            if (EnableSpineFlexibility)
            {
                if (_spine != null) spineBase = useAnimator ? _spine.rotation : (_owner.CharacterRoot.rotation * _initialSpineLocalRot);
                if (_chest != null) chestBase = useAnimator ? _chest.rotation : (_owner.CharacterRoot.rotation * _initialChestLocalRot);
            }

            Quaternion leanRotation = Quaternion.Euler(_currentPitch, _currentYaw, _currentRoll);
            Quaternion targetHipRot = _owner.CharacterRoot.rotation * leanRotation * hipLocalToRoot;
            _hips.rotation = Quaternion.Slerp(_hips.rotation, targetHipRot, _owner.CurrentIKBlend);

            if (EnableSpineFlexibility)
            {
                float normalizedSpeed = Mathf.Clamp01(_smoothedSpeed / 5f);
                float dynamicLag = Mathf.Lerp(0.35f, 0.08f, normalizedSpeed);

                float mathematicalTargetRoll = (-_currentRoll * 1.0f) + (_currentHipsElasticOffset.x * 5f);

                float mathematicalTargetYaw = -_currentYaw * 1.0f;

                float mathematicalTargetPitch = -_currentPitch * 0.4f * normalizedSpeed;

                float targetSpineRoll = mathematicalTargetRoll * ContrappostoStrength;
                float targetSpineYaw = mathematicalTargetYaw * ContrappostoStrength;
                float targetSpinePitch = mathematicalTargetPitch * ContrappostoStrength;

                float reactSpeed = 1f / Mathf.Max(0.01f, dynamicLag);

                _currentSpineYaw = Mathf.Lerp(_currentSpineYaw, targetSpineYaw, 1f - Mathf.Exp(-reactSpeed * _owner.DeltaTime));
                _currentSpineRoll = Mathf.Lerp(_currentSpineRoll, targetSpineRoll, 1f - Mathf.Exp(-reactSpeed * _owner.DeltaTime));
                _currentSpinePitch = Mathf.Lerp(_currentSpinePitch, targetSpinePitch, 1f - Mathf.Exp(-reactSpeed * _owner.DeltaTime));


                if (_spine != null && _chest != null)
                {
                    float spineTotalPitch = _currentPitch + (_currentSpinePitch * 0.5f);
                    float chestTotalPitch = _currentPitch + _currentSpinePitch;

                    Quaternion spineLean = Quaternion.Euler(spineTotalPitch, _currentSpineYaw * 0.4f, _currentSpineRoll * 0.4f);
                    Quaternion spineLocalToRoot = Quaternion.Inverse(_owner.CharacterRoot.rotation) * spineBase;
                    Quaternion targetSpineRot = _owner.CharacterRoot.rotation * spineLean * spineLocalToRoot;
                    _spine.rotation = Quaternion.Slerp(_spine.rotation, targetSpineRot, _owner.CurrentIKBlend);

                    Quaternion chestLean = Quaternion.Euler(chestTotalPitch, _currentSpineYaw, _currentSpineRoll);
                    Quaternion chestLocalToRoot = Quaternion.Inverse(_owner.CharacterRoot.rotation) * chestBase;
                    Quaternion targetChestRot = _owner.CharacterRoot.rotation * chestLean * chestLocalToRoot;
                    _chest.rotation = Quaternion.Slerp(_chest.rotation, targetChestRot, _owner.CurrentIKBlend);
                }
                else if (_spine != null)
                {
                    float spineTotalPitch = _currentPitch + _currentSpinePitch;

                    Quaternion spineLean = Quaternion.Euler(spineTotalPitch, _currentSpineYaw, _currentSpineRoll);
                    Quaternion spineLocalToRoot = Quaternion.Inverse(_owner.CharacterRoot.rotation) * spineBase;
                    Quaternion targetSpineRot = _owner.CharacterRoot.rotation * spineLean * spineLocalToRoot;
                    _spine.rotation = Quaternion.Slerp(_spine.rotation, targetSpineRot, _owner.CurrentIKBlend);
                }
            }
        }
        public void ResetDynamics()
        {
            _hipsSpringVelocity = Vector3.zero;
            _currentHipsElasticOffset = Vector3.zero;
            _stumbleOffset = Vector3.zero;
            _stumblePitch = 0f;
            _stumbleRoll = 0f;
            _currentPitch = 0f;
            _currentRoll = 0f;
            _currentYaw = 0f;
            _pitchVel = 0f;
            _rollVel = 0f;
            _yawVel = 0f;
            _currentSpineYaw = 0f;
            _currentSpineRoll = 0f;
            _currentSpinePitch = 0f;
        }
        public void SnapToIdleStance(float snapSpeed = 16f)
        {
            if (_hips == null || _owner.CharacterRoot == null) return;
            Vector3 targetWorldPos = _owner.CharacterRoot.TransformPoint(_initialHipsLocalPos);
            Quaternion targetWorldRot = _owner.CharacterRoot.rotation * _initialHipsLocalRot;
            _rotDirVelocity = Vector3.zero;
            _hips.position = Vector3.Lerp(_hips.position, targetWorldPos, Time.deltaTime * snapSpeed);
            _hips.rotation = Quaternion.Slerp(_hips.rotation, targetWorldRot, Time.deltaTime * snapSpeed);
        }
    }
}