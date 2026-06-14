using UnityEngine;

namespace FrostPunchGames
{
    [System.Serializable]
    public class TwoBoneIK
    {
        private Transform _upper;
        private Transform _middle;
        private Transform _end;
        private Transform _root;

        private float _upperLength;
        private float _lowerLength;
        private float _totalLength;
        private Vector3 _kneeForwardAxis;
        private Vector3 _initialKneeDirection;

        private Vector3 _upperLocalForward;
        private Vector3 _middleLocalForward;

        private Quaternion _upperInitialLocalRot;
        private Quaternion _middleInitialLocalRot;
        private Quaternion _endInitialLocalRot;
        private bool _hasCapturedPose = false;

        public Vector3 KneeOutwardBias { get; set; } = Vector3.zero;
        public float Weight { get; set; } = 1f;
        public float RotationWeight { get; set; } = 1f;
        public float StretchLimit { get; set; } = 0.98f;

        public float TotalLength => _totalLength;
        public float UpperLength => _upperLength;
        public float LowerLength => _lowerLength;

        public TwoBoneIK(Transform upper, Transform middle, Transform end)
        {
            _upper = upper;
            _middle = middle;
            _end = end;
        }

        public void Initialize(Transform root)
        {
            _root = root;

            _upperLength = Vector3.Distance(_upper.position, _middle.position);
            _lowerLength = Vector3.Distance(_middle.position, _end.position);
            _totalLength = _upperLength + _lowerLength;

            _upperInitialLocalRot = _upper.localRotation;
            _middleInitialLocalRot = _middle.localRotation;
            _endInitialLocalRot = _end.localRotation;

            Vector3 upperToEnd = (_end.position - _upper.position).normalized;
            Vector3 upperToKnee = (_middle.position - _upper.position).normalized;

            _initialKneeDirection = (upperToKnee - upperToEnd * Vector3.Dot(upperToKnee, upperToEnd)).normalized;

            if (_initialKneeDirection.sqrMagnitude < 0.01f && root != null)
            {
                _initialKneeDirection = root.forward;
            }
            if (root != null)
                _kneeForwardAxis = root.InverseTransformDirection(_initialKneeDirection);

            _upperLocalForward = _upper.InverseTransformDirection(_initialKneeDirection).normalized;
            _middleLocalForward = _middle.InverseTransformDirection(_initialKneeDirection).normalized;
        }

        public void CaptureAnimatedPose()
        {
            _upperInitialLocalRot = _upper.localRotation;
            _middleInitialLocalRot = _middle.localRotation;
            _endInitialLocalRot = _end.localRotation;
            _hasCapturedPose = true;
        }

        public void RestoreToAnimatedPose()
        {
            if (!_hasCapturedPose) return;
            _upper.localRotation = _upperInitialLocalRot;
            _middle.localRotation = _middleInitialLocalRot;
            _end.localRotation = _endInitialLocalRot;
        }

        public void Solve(Vector3 targetPosition, Quaternion targetRotation, float weight = -1f)
        {
            float w = weight >= 0f ? weight : Weight;
            if (w <= 0.001f) return;

            RestoreToAnimatedPose();

            SolveTwoBoneIK(targetPosition, targetRotation, w);
        }

        private void SolveTwoBoneIK(Vector3 targetPosition, Quaternion targetRotation, float weight)
        {
            Vector3 upperPos = _upper.position;

            Vector3 modifiedAxis = _kneeForwardAxis + KneeOutwardBias;
            Vector3 kneeHintDirection = _root.TransformDirection(modifiedAxis.normalized);
            Vector3 toTarget = targetPosition - upperPos;
            float targetDistance = toTarget.magnitude;

            float minDist = Mathf.Abs(_upperLength - _lowerLength);
            float maxDist = _totalLength * StretchLimit;

            if (targetDistance < minDist * 1.05f)
            {
                targetDistance = Mathf.Lerp(targetDistance, minDist * 1.05f, 0.5f);
            }

            targetDistance = Mathf.Clamp(targetDistance, minDist * 1.01f, maxDist);

            Vector3 targetDir = toTarget.normalized;
            targetPosition = upperPos + targetDir * targetDistance;

            float a = _lowerLength;
            float b = _upperLength;
            float c = targetDistance;

            float cosUpperAngle = (b * b + c * c - a * a) / (2f * b * c);
            cosUpperAngle = Mathf.Clamp(cosUpperAngle, -1f, 1f);
            float upperAngle = Mathf.Acos(cosUpperAngle);

            Vector3 chainPlaneNormal = Vector3.Cross(targetDir, kneeHintDirection).normalized;
            if (chainPlaneNormal.sqrMagnitude < 0.001f)
            {
                chainPlaneNormal = Vector3.Cross(targetDir, Vector3.up).normalized;
                if (chainPlaneNormal.sqrMagnitude < 0.001f)
                {
                    chainPlaneNormal = Vector3.Cross(targetDir, Vector3.forward).normalized;
                }
            }

            Vector3 upperBoneDir = Quaternion.AngleAxis(upperAngle * Mathf.Rad2Deg, chainPlaneNormal) * targetDir;
            Vector3 kneePos = upperPos + upperBoneDir * _upperLength;

            Vector3 currentUpperDir = (_middle.position - _upper.position).normalized;
            Vector3 targetUpperDir = (kneePos - upperPos).normalized;

            if (currentUpperDir.sqrMagnitude > 0.001f && targetUpperDir.sqrMagnitude > 0.001f)
            {
                Quaternion upperRotDelta = Quaternion.FromToRotation(currentUpperDir, targetUpperDir);
                Quaternion newUpperRot = upperRotDelta * _upper.rotation;

                newUpperRot = ApplyTwistConstraint(newUpperRot, targetUpperDir, kneeHintDirection, _upperLocalForward);

                _upper.rotation = Quaternion.Slerp(_upper.rotation, newUpperRot, weight);
            }

            kneePos = _upper.position + (_middle.position - _upper.position).normalized * _upperLength;

            Vector3 currentLowerDir = (_end.position - _middle.position).normalized;
            Vector3 targetLowerDir = (targetPosition - _middle.position).normalized;

            if (currentLowerDir.sqrMagnitude > 0.001f && targetLowerDir.sqrMagnitude > 0.001f)
            {
                Quaternion lowerRotDelta = Quaternion.FromToRotation(currentLowerDir, targetLowerDir);
                Quaternion newLowerRot = lowerRotDelta * _middle.rotation;

                newLowerRot = ApplyTwistConstraint(newLowerRot, targetLowerDir, kneeHintDirection, _middleLocalForward);

                _middle.rotation = Quaternion.Slerp(_middle.rotation, newLowerRot, weight);
            }

            if (RotationWeight > 0.001f)
            {
                _end.rotation = Quaternion.Slerp(_end.rotation, targetRotation, weight * RotationWeight);
            }
        }

        private Quaternion ApplyTwistConstraint(Quaternion rotation, Vector3 aimDirection, Vector3 poleDirection, Vector3 localForwardAxis)
        {
            Vector3 projectedPole = Vector3.ProjectOnPlane(poleDirection, aimDirection).normalized;

            if (projectedPole.sqrMagnitude < 0.001f)
            {
                projectedPole = Vector3.ProjectOnPlane(Vector3.up, aimDirection).normalized;
            }

            if (projectedPole.sqrMagnitude < 0.001f) return rotation;

            Vector3 currentForward = Vector3.ProjectOnPlane(rotation * localForwardAxis, aimDirection).normalized;

            if (currentForward.sqrMagnitude < 0.001f) return rotation;

            float angle = Vector3.SignedAngle(currentForward, projectedPole, aimDirection);
            Quaternion twistCorrection = Quaternion.AngleAxis(angle, aimDirection);

            return twistCorrection * rotation;
        }

        public float GetStretchValue(Vector3 targetPosition)
        {
            float distance = Vector3.Distance(_upper.position, targetPosition);
            return Mathf.Clamp01(distance / _totalLength);
        }
    }
}