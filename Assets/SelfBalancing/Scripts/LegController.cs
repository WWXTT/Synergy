using UnityEngine;

namespace FrostPunchGames
{
    [System.Serializable]
    public class LegController
    {
        private IKSolver _owner;
        private LegSetup _setup;
        private TwoBoneIK _ik;
        private GroundDetector _groundDetector;
        private FootGlueStateMachine _glueStateMachine;
        private HangingStateMachine _hangingStateMachine;

        private Transform _upperLeg;
        private Transform _lowerLeg;
        private Transform _foot;
        private float _legLength;
        private float _scaleRef;

        private Vector3 _bindUpperLegLocalPos;

        private Vector3 _animatedFootPosition;
        private Quaternion _animatedFootRotation;
        private Vector3 _animatedFootRootLocal;
        private Vector3 _snapshotFootRootLocalPos;
        private Quaternion _snapshotFootRootLocalRot;
        private Vector3 _ankleUp;
        private Vector3 _sourceIKPos;
        private Quaternion _sourceIKRot;
        private Vector3 _finalIKPos;
        private Quaternion _finalIKRot;
        private Vector3 _previousFinalIKPos;
        private Vector3 _previousFinalIKPosRootLocal;
        private Vector3 _alignedOnGroundWorld;
        private Vector3 _alignedOnGroundRootLocal;
        private Quaternion _alignedRotation;
        private Quaternion _lastAppliedAlignedRot;
        private Quaternion _lastTargetAlignedRot;
        private Vector3 _plantedWorldPosition;
        private Vector3 _plantedRootLocal;
        private Quaternion _plantedRotation;
        private Transform _plantedOnTransform;
        private Vector3 _plantedLocalToSurface;
        private Vector3 _detachStartPosition;
        private Quaternion _detachStartRotation;
        private float _currentStepHeight;
        private Vector3 _elasticDragOffset;
        private Vector3 _currentPredictionOffset;
        private Quaternion _lastGroundedFootRotation;
        private Vector3 _hangingTransitionStartPos;
        private Quaternion _hangingTransitionStartRot;
        private bool _hasStoredTransitionStart;
        private float _currentLegStretch;
        private bool _canReachGround;
        private bool _wasAligning;
        private bool _preWasAligning;
        private float _aligningBlend;
        private float _lastAlignHeightDiff;
        private Vector3 _smoothedStepTarget;
        private Quaternion _bindUpperLocal;
        private Quaternion _bindLowerLocal;
        private Quaternion _bindFootLocal;

        private Quaternion _fallenUpperLocal;
        private Quaternion _fallenLowerLocal;
        private Quaternion _fallenFootLocal;
        private Vector3 _kneeHingeAxisLocal;
        private Transform _physicsFoot;

        public string Name => _setup.Name;
        public LegSide Side => _setup.Side;
        public bool IsValid => _setup.IsValid;
        public bool IsGrounded => _canReachGround && _groundDetector.IsGrounded;
        public RaycastHit GroundHit => _groundDetector.GroundHit;
        public bool IsPlanted => _glueStateMachine != null && _glueStateMachine.IsGlued;
        public Vector3 IKTargetPosition { get; private set; }
        public Quaternion IKTargetRotation { get; private set; }
        public float BlendWeight { get; private set; }
        public Vector3 PlantedPosition => _plantedWorldPosition;
        public float LegStretch => _currentLegStretch;
        public Vector3 CurrentFootPosition => _finalIKPos;
        public float StepProgress => _glueStateMachine != null ? _glueStateMachine.GetNormalizedProgress() : 0f;
        public Vector3 InitialRootLocalPos => _snapshotFootRootLocalPos;

        public Vector3 HipPosition => _upperLeg != null ? _upperLeg.position : _owner.CharacterRoot.position;
        public Quaternion CurrentPlantedRotation => _plantedRotation;
        public Quaternion CurrentAlignedRotation => _alignedRotation;

        public LegController(IKSolver owner, LegSetup setup)
        {
            _owner = owner;
            _setup = setup;
            _upperLeg = setup.UpperLeg;
            _lowerLeg = setup.LowerLeg;
            _foot = setup.Foot;
            _ik = new TwoBoneIK(_upperLeg, _lowerLeg, _foot);
            _groundDetector = new GroundDetector();
            _glueStateMachine = new FootGlueStateMachine();
            _hangingStateMachine = new HangingStateMachine();
        }

        public void Initialize()
        {
            _legLength = _setup.GetLegLength();
            _scaleRef = _owner.ScaleReference;

            _ik.Initialize(_owner.CharacterRoot);

            _groundDetector.Initialize(_owner.CharacterRoot, _foot);

            _snapshotFootRootLocalPos = ToRootLocalSpace(_foot.position);
            _snapshotFootRootLocalRot = Quaternion.Inverse(_owner.CharacterRoot.rotation) * _foot.rotation;
            _ankleUp = _foot.InverseTransformDirection(_owner.CharacterRoot.rotation * Vector3.up);

            _bindUpperLocal = _upperLeg.localRotation;
            _bindLowerLocal = _lowerLeg.localRotation;
            _bindFootLocal = _foot.localRotation;

            Vector3 thighVec = _lowerLeg.position - _upperLeg.position;
            Vector3 calfVec = _foot.position - _lowerLeg.position;
            Vector3 hingeWorld = Vector3.Cross(thighVec, calfVec).normalized;

            if (hingeWorld.sqrMagnitude < 0.01f) hingeWorld = _owner.CharacterRoot.right;

            if (Vector3.Dot(hingeWorld, _owner.CharacterRoot.right) < 0f) hingeWorld = -hingeWorld;

            _kneeHingeAxisLocal = Quaternion.Inverse(_upperLeg.rotation) * hingeWorld;

            Vector3 footWorldPos = _foot.position;
            _animatedFootPosition = footWorldPos;
            _animatedFootRotation = _foot.rotation;
            _animatedFootRootLocal = _snapshotFootRootLocalPos;
            _sourceIKPos = footWorldPos;
            _sourceIKRot = _foot.rotation;
            _finalIKPos = footWorldPos;
            _finalIKRot = _foot.rotation;
            _previousFinalIKPos = footWorldPos;
            _previousFinalIKPosRootLocal = _snapshotFootRootLocalPos;
            _alignedOnGroundWorld = footWorldPos;
            _alignedOnGroundRootLocal = _snapshotFootRootLocalPos;
            _alignedRotation = _foot.rotation;
            _lastAppliedAlignedRot = _foot.rotation;
            _lastTargetAlignedRot = _foot.rotation;
            _plantedWorldPosition = footWorldPos;
            _plantedRootLocal = _snapshotFootRootLocalPos;
            _plantedRotation = _foot.rotation;
            _detachStartPosition = footWorldPos;
            _detachStartRotation = _foot.rotation;
            IKTargetPosition = footWorldPos;
            IKTargetRotation = _foot.rotation;
            _lastGroundedFootRotation = _foot.rotation;
            _hangingTransitionStartPos = footWorldPos;
            _hangingTransitionStartRot = _foot.rotation;
            _hasStoredTransitionStart = false;
            _smoothedStepTarget = footWorldPos;

            _glueStateMachine.ForceReset();
            if (IsGrounded) PlantFoot();
            if (_owner.PhysicsSyncer != null)
            {
                var physicsFoot = _owner.PhysicsSyncer.GetPhysicalBodyFromGhost(_foot);
                if (physicsFoot != null) _physicsFoot = physicsFoot;
            }
        }

        public void ConfigureHangingStateMachine(float transitionSpeed, float minUngroundedTime)
        {
            _hangingStateMachine.AirborneTransitionSpeed = transitionSpeed;
            _hangingStateMachine.MinUngroundedTime = minUngroundedTime;
        }

        private Vector3 ToRootLocalSpace(Vector3 worldPos) => _owner.CharacterRoot.InverseTransformPoint(worldPos);
        private Vector3 RootSpaceToWorld(Vector3 localPos) => _owner.CharacterRoot.TransformPoint(localPos);
        private Vector3 ToRootLocalDirection(Vector3 worldDir) => _owner.CharacterRoot.InverseTransformDirection(worldDir);
        private Vector3 RootSpaceToWorldDirection(Vector3 localDir) => _owner.CharacterRoot.TransformDirection(localDir);

        public void CaptureAnimatedPose()
        {
            _animatedFootPosition = _foot.position;
            _animatedFootRotation = _foot.rotation;
            _animatedFootRootLocal = ToRootLocalSpace(_animatedFootPosition);
            _sourceIKPos = _animatedFootPosition;
            _sourceIKRot = _animatedFootRotation;
            _ik.CaptureAnimatedPose();
        }

        public void CaptureFallenPose()
        {
            _fallenUpperLocal = _upperLeg.localRotation;
            _fallenLowerLocal = _lowerLeg.localRotation;
            _fallenFootLocal = _foot.localRotation;
        }
        public void CalculateProceduralPose()
        {
            _animatedFootRootLocal = _snapshotFootRootLocalPos;
            _animatedFootPosition = RootSpaceToWorld(_snapshotFootRootLocalPos);
            _animatedFootRotation = _owner.CharacterRoot.rotation * _snapshotFootRootLocalRot;
            _sourceIKPos = _animatedFootPosition;
            _sourceIKRot = _animatedFootRotation;
        }

        public void PreUpdate()
        {
            if (_owner == null || _glueStateMachine == null) return;
            BlendWeight = _owner.CurrentIKBlend * _setup.IKBlend;

            float speed = _owner.SmoothedVelocity.magnitude;
            float dynamicAttachedTime = 0.05f / (1f + speed);
            _glueStateMachine.MinAttachedDuration = Mathf.Max(0.01f, dynamicAttachedTime);
            _glueStateMachine.Update(_owner.DeltaTime);

            if (_glueStateMachine.CurrentState == FootGlueStateMachine.GlueState.Free && IsGrounded)
                PlantFoot();

            if (IsPlanted) UpdatePlantedState();

            UpdateHangingState();

            if (IsGrounded)
            {
                _lastGroundedFootRotation = _foot.rotation;
                _hasStoredTransitionStart = false;
            }
            if (_glueStateMachine.IsGlued && !IsGrounded) _glueStateMachine.ForceReset();
        }

        private void UpdatePlantedState()
        {
            if (_plantedOnTransform != null)
            {
                Vector3 newWorldPlanted = _plantedOnTransform.TransformPoint(_plantedLocalToSurface);
                float deadZone = 0.005f;
                if ((newWorldPlanted - _plantedWorldPosition).sqrMagnitude > deadZone * deadZone)
                {
                    _plantedWorldPosition = newWorldPlanted;
                }
            }
        }

        private void UpdateHangingState()
        {
            float distToGround = _groundDetector.RaycastHitted ? Vector3.Distance(_foot.position, _groundDetector.GroundHit.point) : 10.0f;
            float range = _legLength * 2.0f;
            float proximity = 1f - Mathf.Clamp01(distToGround / range);
            float speedMultiplier = 1.0f + (Mathf.Pow(proximity, 3.0f) * 8.0f);
            _hangingStateMachine.AirborneTransitionSpeed = _owner.AirborneTransitionSpeed * speedMultiplier;
            _hangingStateMachine.Update(_owner.DeltaTime, IsGrounded);
        }

        public void PostApplyIK()
        {
            if (!IsGrounded && !_hasStoredTransitionStart)
            {
                _hangingTransitionStartPos = _foot.position;
                _hangingTransitionStartRot = _foot.rotation;
                _hasStoredTransitionStart = true;
            }
        }

        public void UpdateRaycasting()
        {
            _canReachGround = false;
            float speed = _owner.SmoothedVelocity.magnitude;
            Vector3 effectiveVelocity = (speed > 0.01f) ? _owner.SmoothedVelocity : Vector3.zero;

            if (_owner.IsAccelerating)
            {
                Vector3 accelDir = effectiveVelocity.sqrMagnitude > 0.01f ? effectiveVelocity.normalized : _owner.CharacterRoot.forward;
                effectiveVelocity = accelDir * 2.0f;
                speed = 2.0f;
            }

            float expectedSwingTime = CalculateSwingDuration(speed, 0f);
            float predictionTime = expectedSwingTime * 0.4f;
            float damp = Mathf.Clamp01(speed / 0.5f);
            Vector3 targetPrediction = effectiveVelocity * (predictionTime * damp);

            // Under high capture authority a corrective step may need to reach further toward the
            // diverging capture point, so widen the prediction cap. The hard leg-reach clamp below
            // (clampLimit) still bounds the final foot target, so the leg can never overstretch.
            float maxPrediction = _legLength * Mathf.Lerp(1.25f, 1.6f, _owner.CaptureAuthority);
            if (targetPrediction.sqrMagnitude > maxPrediction * maxPrediction)
            {
                targetPrediction = targetPrediction.normalized * maxPrediction;
            }

            float angularVel = _owner.AngularVelocity;
            targetPrediction += _owner.CharacterRoot.right * ((angularVel / 90f) * _owner.TurnPrediction);

            Vector3 rootFwd = _owner.CharacterRoot.forward;
            Vector3 rootUp = _owner.CharacterRoot.up;

            Vector3 flatForward = Vector3.ProjectOnPlane(rootFwd, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.ProjectOnPlane((rootFwd.y > 0) ? -rootUp : rootUp, Vector3.up).normalized;
            }
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;

            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;

            float widthMultiplier = (_owner.StepWidthCurve != null) ? _owner.StepWidthCurve.Evaluate(speed) : 1f;

            Vector3 hipToFootLocal = _snapshotFootRootLocalPos - _bindUpperLegLocalPos;

            float localX = hipToFootLocal.x * widthMultiplier;
            float separationBias = _owner.MinLegSeparation * 0.5f;
            if (_setup.Side == LegSide.Left) localX -= separationBias; else localX += separationBias;

            Vector3 stanceOffset = (flatRight * localX) + (flatForward * hipToFootLocal.z);

            Vector3 virtualHipPos = RootSpaceToWorld(_bindUpperLegLocalPos);
            Vector3 hipWorldPos = _upperLeg != null ? _upperLeg.position : _owner.CharacterRoot.position;

            Vector3 stableHipPos = virtualHipPos;
            stableHipPos.y = hipWorldPos.y;

            Vector3 safeOrigin = stableHipPos + stanceOffset;
            safeOrigin.y = hipWorldPos.y - (_legLength * 0.2f);

            // LIPM feedback: the capture point is the ideal place to plant the foot to arrest the COM.
            // Blend the velocity-heuristic prediction toward it, weighted by how far the CP has overshot
            // the support polygon. The leg-length clamp below still bounds the resulting target.
            if (_owner.BalanceFeedbackActive)
            {
                float cpWeight = Mathf.Clamp01(_owner.CaptureAuthority * _owner.CPFootBiasStrength);
                if (cpWeight > 0f)
                {
                    Vector3 cpOffset = _owner.CapturePointWorld - safeOrigin;
                    cpOffset.y = 0f;
                    targetPrediction = Vector3.Lerp(targetPrediction, cpOffset, cpWeight);
                }
            }

            float responsiveness = IsPlanted ? 25f : _owner.PredictionSmoothing * 2.5f;
            if (_owner.IsAccelerating) responsiveness = 50f;

            if (speed < 0.1f) responsiveness = 60f;

            _currentPredictionOffset = Vector3.Lerp(_currentPredictionOffset, targetPrediction, _owner.DeltaTime * responsiveness);

            Vector3 futureOrigin = safeOrigin + _currentPredictionOffset;

            Vector3 virtualHipPos2 = RootSpaceToWorld(_bindUpperLegLocalPos);
            Vector3 hipToFuture = futureOrigin - virtualHipPos2;
            float clampLimit = _legLength * 1.05f;
            if (hipToFuture.magnitude > clampLimit)
            {
                futureOrigin = virtualHipPos2 + (hipToFuture.normalized * clampLimit);
            }

            float hipHeightDiff = 0.5f;
            if (_upperLeg != null)
            {
                hipHeightDiff = Mathf.Max(0.2f, _upperLeg.position.y - futureOrigin.y);
            }

            float safeRayHeight = Mathf.Max(_owner.RaycastHeightFromFoot, hipHeightDiff * 0.8f);

            _groundDetector.Update(
                futureOrigin,
                virtualHipPos2,
                _owner.GroundLayers,
                _scaleRef,
                safeRayHeight,
                _legLength * 2.0f,
                _setup.GetFootHeight() + _owner.FootHeightOffset);

            if (_groundDetector.RaycastHitted)
            {
                float distToHit = Vector3.Distance(virtualHipPos2, _groundDetector.GroundHit.point);
                _canReachGround = distToHit <= (_legLength * 1.4f);
                _currentLegStretch = Mathf.Clamp01(distToHit / _legLength);

                if (_canReachGround)
                {
                    _alignedOnGroundWorld = _groundDetector.AlignedAnkleWorldPos;
                    _alignedOnGroundRootLocal = _groundDetector.AlignedAnkleRootLocal;
                    _alignedOnGroundWorld = ApplyTargetAdjustments(_alignedOnGroundWorld);
                    _alignedOnGroundRootLocal = ToRootLocalSpace(_alignedOnGroundWorld);
                    _alignedRotation = CalculateAlignedRotation();
                }
                else
                {
                    HandleUnreachableGround();
                }
            }
            else
            {
                HandleNoGround(futureOrigin);
                if (_owner.IsAccelerating) _canReachGround = true;
            }
        }

        private void HandleUnreachableGround()
        {
            Vector3 direction = (_groundDetector.GroundHit.point - _upperLeg.position).normalized;
            _alignedOnGroundWorld = _upperLeg.position + (direction * _legLength);
            _alignedOnGroundRootLocal = ToRootLocalSpace(_alignedOnGroundWorld);
            _alignedRotation = _sourceIKRot;
        }

        private void HandleNoGround(Vector3 futureOrigin)
        {
            _canReachGround = false;
            _currentLegStretch = 1f;
            _alignedOnGroundWorld = futureOrigin + Vector3.down * (_legLength * 0.5f);
            _alignedOnGroundRootLocal = ToRootLocalSpace(_alignedOnGroundWorld);
            _alignedRotation = _sourceIKRot;
        }

        private Quaternion CalculateAlignedRotation()
        {
            if (!_owner.AlignFeetToGround || _groundDetector.GroundNormal.sqrMagnitude < 0.001f)
                return _sourceIKRot;
            Vector3 ankleUpWorld = _sourceIKRot * _ankleUp;
            Quaternion alignedRot = Quaternion.FromToRotation(ankleUpWorld, _groundDetector.GroundNormal);
            alignedRot = alignedRot * _sourceIKRot;
            return Quaternion.Slerp(_sourceIKRot, alignedRot, _owner.FootAlignmentBlend);
        }

        private Vector3 ApplyTargetAdjustments(Vector3 targetPos)
        {
            targetPos = CalculateStrafeAdjustedTarget(targetPos);
            targetPos = PreventCrossover(targetPos);
            targetPos = ApplyOtherLegAvoidance(targetPos);
            return targetPos;
        }

        private float CalculateSwingDuration(float speed, float distance)
        {
            float kinematicTime = (speed > 0.1f) ? (distance / speed) : _owner.BaseGlueDuration;
            kinematicTime /= _owner.SwingSpeedMultiplier;
            float minBallisticTime = 0.22f;
            return Mathf.Max(kinematicTime, minBallisticTime);
        }

        public void UpdateIK()
        {
            if (BlendWeight <= 0.001f) return;

            float liveBias = _setup.Side == LegSide.Left ? -_owner.KneeOutwardBias : _owner.KneeOutwardBias;
            _ik.KneeOutwardBias = new Vector3(liveBias * BlendWeight, 0f, 0f);

            _finalIKPos = _sourceIKPos;
            _finalIKRot = _sourceIKRot;

            UpdateProceduralTarget();

            if (!IsPlanted && _groundDetector.RaycastHitted && StepProgress > 0.5f)
            {
                float groundY = _groundDetector.GroundHit.point.y;
                float footHeight = _setup.GetFootHeight() + _owner.FootHeightOffset;
                if (_finalIKPos.y < groundY + footHeight - 0.001f) _finalIKPos.y = groundY + footHeight;
            }

            UpdateFootRotationAlignment();

            IKTargetPosition = _finalIKPos;
            IKTargetRotation = _finalIKRot;
        }

        private void UpdateProceduralTarget()
        {
            if (_hangingStateMachine.IsAirborne && !_hasStoredTransitionStart)
            {
                _hangingTransitionStartPos = _finalIKPos;
                _hangingTransitionStartRot = _finalIKRot;
                _hasStoredTransitionStart = true;
            }
            else if (!_hangingStateMachine.IsAirborne && _hasStoredTransitionStart)
            {
                _hasStoredTransitionStart = false;
            }

            Vector3 groundTargetPos;
            Quaternion groundTargetRot;

            if (IsPlanted && _owner.UseFootGluing)
            {
                (groundTargetPos, groundTargetRot) = UpdatePlantedTarget();
            }
            else if (_glueStateMachine.CurrentState == FootGlueStateMachine.GlueState.CoolingDown)
            {
                (groundTargetPos, groundTargetRot) = UpdateSteppingTarget();
            }
            else
            {
                groundTargetPos = _alignedOnGroundWorld;
                groundTargetRot = _alignedRotation;
            }

            Vector3 hipsPos = _owner.Hips != null ? _owner.Hips.position : _owner.CharacterRoot.position;
            float sideOffset = _setup.Side == LegSide.Left ? -0.12f : 0.12f;

            Vector3 hangingPos = hipsPos
                + (_owner.CharacterRoot.right * sideOffset)
                + (Vector3.down * (_legLength * 0.85f))
                + (_owner.CharacterRoot.forward * 0.1f);

            Quaternion hangingRot = _owner.CharacterRoot.rotation * _snapshotFootRootLocalRot;

            float t = _hangingStateMachine.BlendToAirborne;
            Vector3 currentGroundedOrigin = Vector3.Lerp(_hangingTransitionStartPos, groundTargetPos, 1f - t);

            _finalIKPos = Vector3.Lerp(currentGroundedOrigin, hangingPos, t);
            _finalIKRot = Quaternion.Slerp(_hangingTransitionStartRot, hangingRot, t);
        }

        private (Vector3 pos, Quaternion rot) UpdatePlantedTarget()
        {
            Vector3 worldPlanted = _plantedWorldPosition;

            Vector3 hipPos = _upperLeg.position;

            float maxAllowedLen = _legLength * 1.1f;

            float heightDiff = Mathf.Abs(hipPos.y - worldPlanted.y);
            float maxFlatDist = (maxAllowedLen > heightDiff) ? Mathf.Sqrt((maxAllowedLen * maxAllowedLen) - (heightDiff * heightDiff)) : 0f;

            Vector3 hipToFootFlat = Vector3.ProjectOnPlane(worldPlanted - hipPos, Vector3.up);

            if (hipToFootFlat.magnitude > maxFlatDist)
            {
                Vector3 limitPoint = hipPos + (hipToFootFlat.normalized * maxFlatDist);
                limitPoint.y = worldPlanted.y;

                float dragSpeed = _owner.SmoothedVelocity.magnitude * 1.2f;
                _plantedWorldPosition = Vector3.MoveTowards(worldPlanted, limitPoint, dragSpeed * _owner.DeltaTime);
            }
            else
            {
                _plantedWorldPosition = worldPlanted;
            }

            return (_plantedWorldPosition, _plantedRotation);
        }

        private (Vector3 pos, Quaternion rot) UpdateSteppingTarget()
        {
            _elasticDragOffset = Vector3.zero;
            float t = _glueStateMachine.GetNormalizedProgress();
            float moveT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 rawTarget = PreventCrossover(_alignedOnGroundWorld);

            if (t < 0.05f)
            {
                _smoothedStepTarget = rawTarget;
            }
            else
            {
                _smoothedStepTarget = Vector3.Lerp(_smoothedStepTarget, rawTarget, _owner.DeltaTime * 25f);
            }

            Vector3 baseEnd = _smoothedStepTarget;

            float curveHeight = _owner.StepHeightCurve.Evaluate(t);
            float verticalOffset = curveHeight * _currentStepHeight;

            Vector3 baseStart = _detachStartPosition;
            Vector3 basePos = Vector3.Lerp(baseStart, baseEnd, moveT);

            Vector3 finalPos = basePos + (Vector3.up * verticalOffset);

            finalPos += CalculateSwingAvoidanceFromAllLegs(t);

            Quaternion baseRot = Quaternion.Lerp(_detachStartRotation, _alignedRotation, moveT);

            return (finalPos, baseRot);
        }

        private void UpdateFootRotationAlignment()
        {
            if (!_owner.AlignFeetToGround) return;
            float footHeightAboveGround = _lastAlignHeightDiff;
            float alignThreshold = _scaleRef * 0.05f;
            _preWasAligning = _wasAligning;
            _wasAligning = IsGrounded && footHeightAboveGround <= alignThreshold;

            if (_wasAligning)
            {
                _aligningBlend = Mathf.MoveTowards(_aligningBlend, 1f, _owner.DeltaTime * 8f);
                _lastTargetAlignedRot = _alignedRotation;
            }
            else
            {
                _aligningBlend = Mathf.MoveTowards(_aligningBlend, 0f, _owner.DeltaTime * 14f);
                if (_aligningBlend < 0.001f) _lastTargetAlignedRot = _finalIKRot;
                else _lastTargetAlignedRot = Quaternion.Lerp(_finalIKRot, _lastTargetAlignedRot, _aligningBlend);
            }
            _lastAppliedAlignedRot = Quaternion.Slerp(_lastAppliedAlignedRot, _lastTargetAlignedRot, _owner.DeltaTime * 12f);
            _finalIKRot = _lastAppliedAlignedRot;
        }

        private void PlantFoot()
        {
            if (!IsGrounded || !_glueStateMachine.TryAttach()) return;

            _plantedWorldPosition = _alignedOnGroundWorld;
            _plantedRootLocal = _alignedOnGroundRootLocal;
            _plantedRotation = _alignedRotation;

            if (_groundDetector.RaycastHitted && _groundDetector.GroundHit.transform != null)
                _plantedLocalToSurface = _groundDetector.GroundHit.transform.InverseTransformPoint(_plantedWorldPosition);

            _plantedOnTransform = _groundDetector.GroundHit.transform;
            _elasticDragOffset = Vector3.zero;

            float currentSpeed = _owner.SmoothedVelocity.magnitude;
            if (currentSpeed > 0.05f)
            {
                float impactForce = (0.25f + (currentSpeed * 0.1f)) * _scaleRef;

                _owner.AddHipImpact(Vector3.down * impactForce, _plantedWorldPosition);
            }
        }

        public void TriggerStep(float forcedDuration)
        {
            if (_glueStateMachine.TryDetach())
            {
                _glueStateMachine.CooldownDuration = forcedDuration;
                _glueStateMachine.CooldownTimeRemaining = forcedDuration;

                _detachStartPosition = _finalIKPos;
                _detachStartRotation = _finalIKRot;

                float speed = _owner.SmoothedVelocity.magnitude;
                if (speed < 0.2f) speed = 0f;
                float dynamicHeight = _owner.StepHeight * (1f + (speed * 0.15f));
                _currentStepHeight = Mathf.Min(dynamicHeight, _owner.StepHeight * 2.0f);

                Vector3 target = _alignedOnGroundWorld;
                float distance = Vector3.Distance(_detachStartPosition, target);
                float distFactor = Mathf.Clamp01(distance / (_legLength * 0.5f));
                float heightDiff = target.y - _detachStartPosition.y;

                if (heightDiff > 0.05f)
                {
                    _currentStepHeight = Mathf.Max(_currentStepHeight, _owner.StepHeight);
                }
                else
                {
                    _currentStepHeight = Mathf.Lerp(_currentStepHeight * 0.3f, _currentStepHeight, distFactor);
                }
            }
        }

        public void TriggerStep(bool isCorrective = false)
        {
            float dur = isCorrective ? 0.4f : _owner.BaseGlueDuration;
            TriggerStep(dur);
            if (isCorrective) _currentStepHeight = _owner.StepHeight * 0.15f;
        }

        public void ForceReglue()
        {
            _glueStateMachine.ForceReset();
            if (IsGrounded) PlantFoot();
        }

        private Vector3 PreventCrossover(Vector3 targetPosition)
        {
            if (_owner == null || _owner.CharacterRoot == null) return targetPosition;

            Vector3 flatRight = Vector3.ProjectOnPlane(_owner.CharacterRoot.right, Vector3.up).normalized;
            if (flatRight == Vector3.zero) flatRight = Vector3.right;

            Vector3 toTarget = targetPosition - _owner.CharacterRoot.position;
            float flatX = Vector3.Dot(toTarget, flatRight);

            float absoluteMinClearance = (_owner.MinLegSeparation * _scaleRef * 0.5f) + 0.02f;

            if (_setup.Side == LegSide.Left)
            {
                if (flatX > -absoluteMinClearance)
                {
                    targetPosition += flatRight * (-absoluteMinClearance - flatX);
                }
            }
            else
            {
                if (flatX < absoluteMinClearance)
                {
                    targetPosition += flatRight * (absoluteMinClearance - flatX);
                }
            }

            return targetPosition;
        }

        private Vector3 CalculateStrafeAdjustedTarget(Vector3 baseTarget)
        {
            if (_owner == null || _owner.CharacterRoot == null) return baseTarget;
            Vector3 velocity = _owner.SmoothedVelocity;
            if (velocity.magnitude < 0.1f) return baseTarget;
            Vector3 localVelocity = ToRootLocalDirection(velocity);
            float strafeAmount = Mathf.Abs(localVelocity.x) / velocity.magnitude;
            if (strafeAmount < 0.3f) return baseTarget;

            Vector3 localTarget = ToRootLocalSpace(baseTarget);
            bool strafingRight = localVelocity.x > 0;
            bool isLeadingLeg = (_setup.Side == LegSide.Right && strafingRight) || (_setup.Side == LegSide.Left && !strafingRight);
            if (!isLeadingLeg) localTarget.z += _owner.StrafeForwardOffset * _scaleRef * strafeAmount;
            else localTarget.z -= _owner.StrafeForwardOffset * _scaleRef * strafeAmount * 0.5f;
            return RootSpaceToWorld(localTarget);
        }

        private Vector3 ApplyOtherLegAvoidance(Vector3 targetPos)
        {
            if (_owner == null || _owner.CharacterRoot == null) return targetPos;
            Vector3 localTarget = ToRootLocalSpace(targetPos);

            for (int i = 0; i < _owner.GetLegCount(); i++)
            {
                LegController other = _owner.GetLegController(i);
                if (other == null || other == this) continue;

                Vector3 otherPos = other.IsPlanted ? other.PlantedPosition : other.CurrentFootPosition;
                Vector3 otherLocal = ToRootLocalSpace(otherPos);

                Vector2 myXZ = new Vector2(localTarget.x, localTarget.z);
                Vector2 otherXZ = new Vector2(otherLocal.x, otherLocal.z);
                float horizontalDist = Vector2.Distance(myXZ, otherXZ);

                float hardRadius = _owner.MinLegSeparation * _owner.ScaleReference * 1.5f;

                if (horizontalDist < hardRadius && horizontalDist > 0.001f)
                {
                    Vector2 pushDir = (myXZ - otherXZ).normalized;

                    pushDir.y *= 0.3f;
                    pushDir = pushDir.normalized;

                    float pushAmount = (hardRadius - horizontalDist);
                    myXZ += pushDir * pushAmount;

                    localTarget.x = myXZ.x;
                    localTarget.z = myXZ.y;
                }
            }
            return RootSpaceToWorld(localTarget);
        }

        private Vector3 CalculateSwingAvoidanceFromAllLegs(float swingProgress)
        {
            if (_owner == null) return Vector3.zero;
            Vector3 totalAvoidance = Vector3.zero;
            for (int i = 0; i < _owner.GetLegCount(); i++)
            {
                LegController other = _owner.GetLegController(i);
                if (other == this) continue;
                totalAvoidance += CalculateSwingAvoidance(other, swingProgress);
            }
            return totalAvoidance;
        }

        private Vector3 CalculateSwingAvoidance(LegController otherLeg, float swingProgress)
        {
            if (otherLeg == null || !otherLeg.IsPlanted || _owner == null) return Vector3.zero;
            float avoidanceStrength = _owner.StepHeightCurve.Evaluate(swingProgress);
            if (avoidanceStrength < 0.1f) return Vector3.zero;

            Vector3 myFootPos = _finalIKPos;
            Vector3 otherFootPos = otherLeg.PlantedPosition;
            Vector3 toOther = otherFootPos - myFootPos;
            toOther.y = 0;
            float distance = toOther.magnitude;
            float minSeparation = _owner.MinLegSeparation * _scaleRef;

            if (distance < minSeparation * 2f && distance > 0.001f)
            {
                Vector3 pushDir = -toOther.normalized;
                Vector3 travelDir = (_alignedOnGroundWorld - _detachStartPosition).normalized;
                Vector3 lateralDir = Vector3.Cross(Vector3.up, travelDir).normalized;
                if (Vector3.Dot(lateralDir, pushDir) < 0) lateralDir = -lateralDir;
                pushDir = Vector3.Lerp(pushDir, lateralDir, 0.5f).normalized;
                float proximityFactor = 1f - Mathf.Clamp01(distance / (minSeparation * 2f));
                float offsetMagnitude = proximityFactor * avoidanceStrength * minSeparation;
                return pushDir * offsetMagnitude;
            }
            return Vector3.zero;
        }

        public void ApplyIK()
        {
            if ((!IsGrounded && !_hangingStateMachine.ShouldApplyHangingPose) || BlendWeight <= 0.001f) return;

            bool isBlending = BlendWeight < 0.999f;

            if (_owner.UseAdditiveMode && !isBlending)
            {
                Vector3 localDelta = ToRootLocalSpace(_finalIKPos) - _animatedFootRootLocal;
                Vector3 blendedPos = RootSpaceToWorld(_animatedFootRootLocal + localDelta * BlendWeight);
                Quaternion blendedRot = Quaternion.Slerp(_animatedFootRotation, _finalIKRot, BlendWeight);

                _ik.Solve(blendedPos, blendedRot, 1f);
            }
            else
            {
                _ik.Solve(_finalIKPos, _finalIKRot, BlendWeight);
            }
        }

        public float GetLegLength() => _legLength;
        public float GetStepError()
        {
            if (!_owner.UseFootGluing) return 0f;
            if (!IsGrounded && !_owner.IsAccelerating) return 0f;

            Vector3 d = _plantedWorldPosition - _alignedOnGroundWorld;
            d.y = 0;
            return d.magnitude;
        }

        public void DrawDebug(Color color)
        {
            _groundDetector.DrawDebug(Color.green, Color.red);
            Debug.DrawLine(_animatedFootPosition, _finalIKPos, Color.magenta);
            if (IsPlanted) Debug.DrawRay(_plantedWorldPosition, Vector3.up * 0.15f, Color.blue);
            Debug.DrawRay(_alignedOnGroundWorld, Vector3.up * 0.1f, Color.yellow);
        }
    }
}