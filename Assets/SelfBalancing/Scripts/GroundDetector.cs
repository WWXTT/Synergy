using UnityEngine;

namespace FrostPunchGames
{
    [System.Serializable]
    public class GroundDetector
    {
        public enum ERaycastStyle
        {
            StraightDown,
            OriginToFoot,
            AlongBones,
            OriginToFoot_DownOnNeed
        }

        public enum ERaycastShape
        {
            Linecast,
            Spherecast
        }

        [Header("Raycast Settings")]
        public ERaycastStyle RaycastStyle = ERaycastStyle.StraightDown;
        public ERaycastShape RaycastShape = ERaycastShape.Linecast;

        public float SpherecastRadius = 0.065f;

        [Range(0f, 1f)] public float SpherecastRealign = 0.5f;

        [Header("Validation")]
        public float MaxWalkableSlope = 55f;
        public float MinGroundDot = 0.5f;
        public float MaxValidDistance = 2f;

        [Header("Smoothing")]
        [Range(0.1f, 1f)] public float NormalSmoothSpeed = 0.5f;

        public bool IsGrounded { get; private set; }
        public bool RaycastHitted { get; private set; }
        public RaycastHit GroundHit { get; private set; }
        public float DistanceToGround { get; private set; }
        public Vector3 GroundPoint => GroundHit.point;
        public Vector3 GroundNormal => _smoothedNormal;
        public float RaycastSlopeAngle { get; private set; }

        public Vector3 GroundHitRootLocal { get; private set; }

        public Vector3 AlignedAnkleWorldPos { get; private set; }

        public Vector3 AlignedAnkleRootLocal { get; private set; }

        public Quaternion AlignedRotation { get; private set; }

        private Transform _root;
        private Transform _footTransform;
        private Vector3 _smoothedNormal = Vector3.up;
        private Vector3 _lastRaycastOrigin;
        private Vector3 _lastRaycastEndPoint;
        private RaycastHit _hitResult;

        public void Initialize(Transform root, Transform footTransform)
        {
            _root = root;
            _footTransform = footTransform;
            IsGrounded = false;
            RaycastHitted = false;
            DistanceToGround = float.MaxValue;
            _smoothedNormal = Vector3.up;
            AlignedRotation = footTransform != null ? footTransform.rotation : Quaternion.identity;
        }

        public void Update(
            Vector3 footAnimatedPos,
            Vector3 hipPosition,
            LayerMask layerMask,
            float scaleReference,
            float raycastHeight,
            float extraCastDistance,
            float footHeight)
        {
            RaycastHitted = false;
            IsGrounded = false;

            switch (RaycastStyle)
            {
                case ERaycastStyle.StraightDown:
                    Raycast_StraightDown(footAnimatedPos, layerMask, scaleReference, raycastHeight, extraCastDistance);
                    break;

                case ERaycastStyle.OriginToFoot:
                    Raycast_OriginToFoot(footAnimatedPos, hipPosition, layerMask, footHeight);
                    break;

                case ERaycastStyle.AlongBones:
                    Raycast_StraightDown(footAnimatedPos, layerMask, scaleReference, raycastHeight, extraCastDistance);
                    break;

                case ERaycastStyle.OriginToFoot_DownOnNeed:
                    Raycast_OriginToFoot(footAnimatedPos, hipPosition, layerMask, footHeight);
                    if (!RaycastHitted)
                    {
                        Raycast_StraightDown(footAnimatedPos, layerMask, scaleReference, raycastHeight, extraCastDistance);
                    }
                    break;
            }

            if (RaycastHitted)
            {
                ProcessHit(footAnimatedPos, footHeight);
            }
            else
            {
                HandleNoHit(footAnimatedPos);
            }
        }

        private void Raycast_StraightDown(
            Vector3 footAnimatedPos,
            LayerMask layerMask,
            float scaleReference,
            float raycastHeight,
            float extraCastDistance)
        {
            Vector3 origin = footAnimatedPos + Vector3.up * raycastHeight;

            float totalDistance = raycastHeight + extraCastDistance;

            Vector3 rayEnd = origin + Vector3.down * totalDistance;

            _lastRaycastOrigin = origin;
            _lastRaycastEndPoint = rayEnd;

            RaycastHitted = DoRaycasting(origin, rayEnd, layerMask, scaleReference, footAnimatedPos);
        }

        private void Raycast_OriginToFoot(
            Vector3 footAnimatedPos,
            Vector3 hipPosition,
            LayerMask layerMask,
            float footHeight)
        {
            Vector3 origin = hipPosition;

            Vector3 castEndPoint = footAnimatedPos - Vector3.up * footHeight;
            Vector3 direction = castEndPoint - origin;

            float toGround = direction.magnitude * 1.05f;
            direction.Normalize();

            Vector3 rayEnd = origin + direction * toGround;

            _lastRaycastOrigin = origin;
            _lastRaycastEndPoint = rayEnd;

            if (RaycastShape == ERaycastShape.Linecast)
            {
                RaycastHitted = Physics.Linecast(origin, rayEnd, out _hitResult, layerMask);
            }
            else
            {
                float radius = 0.065f;
                float castDistance = direction.magnitude - radius;
                RaycastHitted = Physics.SphereCast(origin, radius, direction, out _hitResult, castDistance - radius, layerMask);
            }

            if (RaycastHitted)
            {
                GroundHit = _hitResult;
            }
        }

        private bool DoRaycasting(Vector3 origin, Vector3 rayEnd, LayerMask layerMask, float scaleReference, Vector3 intendedFootPos)
        {
            bool hitted;

            if (RaycastShape == ERaycastShape.Linecast)
            {
                hitted = Physics.Linecast(origin, rayEnd, out _hitResult, layerMask);
            }
            else
            {
                float radius = scaleReference * SpherecastRadius;
                Vector3 castDir = rayEnd - origin;
                float castDistance = castDir.magnitude - radius;

                hitted = Physics.SphereCast(origin, radius, castDir.normalized, out _hitResult, castDistance - radius, layerMask);

                if (hitted && SpherecastRealign > 0f && _root != null)
                {
                    Vector3 hitLocal = ToRootLocalSpace(_hitResult.point);
                    Vector3 footLocal = ToRootLocalSpace(intendedFootPos);

                    hitLocal.x = Mathf.Lerp(hitLocal.x, footLocal.x, SpherecastRealign);
                    hitLocal.z = Mathf.Lerp(hitLocal.z, footLocal.z, SpherecastRealign);

                    RaycastHit modifiedHit = _hitResult;
                    modifiedHit.point = RootSpaceToWorld(hitLocal);
                    _hitResult = modifiedHit;
                }
            }

            if (hitted)
            {
                GroundHit = _hitResult;
            }

            return hitted;
        }

        private void ProcessHit(Vector3 footAnimatedPos, float footHeight)
        {
            DistanceToGround = _hitResult.distance;

            RaycastSlopeAngle = Vector3.Angle(Vector3.up, _hitResult.normal);

            float dot = Vector3.Dot(_hitResult.normal, Vector3.up);
            bool validSlope = dot >= MinGroundDot;
            bool validDistance = DistanceToGround <= MaxValidDistance;

            if (!validSlope || !validDistance)
            {
                IsGrounded = false;
                HandleNoHit(footAnimatedPos);
                return;
            }

            IsGrounded = true;

            Vector3 hitNormal = _hitResult.normal;
            if (RaycastSlopeAngle > 45f)
            {
                float slopeBlend = Mathf.InverseLerp(45f, 90f, RaycastSlopeAngle) * 0.5f;
                hitNormal = Vector3.Slerp(hitNormal, Vector3.up, slopeBlend);
            }

            float smoothFactor = 1f - Mathf.Pow(1f - NormalSmoothSpeed, Time.deltaTime * 60f);
            _smoothedNormal = Vector3.Slerp(_smoothedNormal, hitNormal, smoothFactor);
            _smoothedNormal.Normalize();

            CalculateAlignedPosition(footHeight);
        }

        private void CalculateAlignedPosition(float footHeight)
        {
            if (_root == null) return;

            GroundHitRootLocal = ToRootLocalSpace(GroundHit.point);

            float normalDotUp = Vector3.Dot(_smoothedNormal, Vector3.up);

            Vector3 verticalOffset = Vector3.up * footHeight;
            Vector3 normalOffset = _smoothedNormal * footHeight;

            float slopeT = Mathf.Clamp01(RaycastSlopeAngle / 45f);
            Vector3 finalOffset = Vector3.Lerp(verticalOffset, normalOffset, slopeT);

            Vector3 ankleWorldPos = GroundHit.point + finalOffset;

            if (RaycastSlopeAngle > 5f)
            {
                float correction = (1f - normalDotUp) * footHeight * 0.5f;
                ankleWorldPos += Vector3.up * correction;
            }

            AlignedAnkleRootLocal = ToRootLocalSpace(ankleWorldPos);
            AlignedAnkleWorldPos = ankleWorldPos;

            if (_footTransform != null)
            {
                AlignedRotation = GetAlignedRotation(_footTransform.rotation, _smoothedNormal);
            }
        }

        private void HandleNoHit(Vector3 footAnimatedPos)
        {
            DistanceToGround = float.MaxValue;
            _smoothedNormal = Vector3.up;
            RaycastSlopeAngle = 0f;

            if (_root != null)
            {
                Vector3 fakeHitRootLocal = ToRootLocalSpace(footAnimatedPos);
                fakeHitRootLocal.y = 0f;

                GroundHitRootLocal = fakeHitRootLocal;
                AlignedAnkleRootLocal = fakeHitRootLocal;
                AlignedAnkleWorldPos = RootSpaceToWorld(fakeHitRootLocal);
            }
            else
            {
                AlignedAnkleWorldPos = footAnimatedPos;
                AlignedAnkleRootLocal = Vector3.zero;
                GroundHitRootLocal = Vector3.zero;
            }

            if (_footTransform != null)
            {
                AlignedRotation = _footTransform.rotation;
            }
        }

        public Quaternion GetAlignedRotation(Quaternion sourceRotation, Vector3 groundNormal)
        {
            Quaternion alignedRot = Quaternion.FromToRotation(sourceRotation * Vector3.up, groundNormal);
            alignedRot *= sourceRotation;
            return alignedRot;
        }

        public Quaternion GetAlignedRotationFromBindPose(Quaternion bindLocalRotation, Vector3 groundNormal)
        {
            if (_root == null) return Quaternion.identity;

            Quaternion bindWorldRot = _root.rotation * bindLocalRotation;

            Quaternion surfaceTilt = Quaternion.FromToRotation(Vector3.up, groundNormal);

            return surfaceTilt * bindWorldRot;
        }

        private Vector3 ToRootLocalSpace(Vector3 worldPos)
        {
            return _root != null ? _root.InverseTransformPoint(worldPos) : worldPos;
        }

        private Vector3 RootSpaceToWorld(Vector3 localPos)
        {
            return _root != null ? _root.TransformPoint(localPos) : localPos;
        }

        public void DrawDebug(Color groundedColor, Color ungroundedColor)
        {
            Color color = IsGrounded ? groundedColor : ungroundedColor;

            Debug.DrawLine(_lastRaycastOrigin, _lastRaycastEndPoint, color);

            if (RaycastHitted)
            {
                Debug.DrawRay(GroundHit.point, _smoothedNormal * 0.3f, Color.green);

                Debug.DrawRay(AlignedAnkleWorldPos, Vector3.up * 0.1f, Color.cyan);
            }
        }
    }
}