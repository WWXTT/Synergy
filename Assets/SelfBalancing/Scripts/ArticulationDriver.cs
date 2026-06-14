using UnityEngine;

namespace FrostPunchGames
{
    // Per-bone driver for the ArticulationBody rig. Replaces the ConfigurableJoint-based
    // DynamicBone. Each driver makes one physical ArticulationBody chase its ghost bone by
    // writing reduced-coordinate drive targets (Featherstone solver keeps the chain coherent,
    // so no manual anchor/projection bookkeeping is needed).
    [System.Serializable]
    public class ArticulationDriver
    {
        [HideInInspector] public string boneName;
        public ArticulationBody body;
        public Transform ghostBone;

        [Header("Multipliers")]
        public float anchorMultiplier = 1f;
        public float driveMultiplier = 1f;
        public float damperMultiplier = 1f;

        public Transform boneTransform { get; private set; }
        public int boneIndex { get; private set; }
        public bool IsInitialized { get; private set; }

        // Bind-pose local rotation of the ghost bone. The articulation joint's zero reduced
        // coordinate corresponds to the physics bone's bind pose, so drive targets are computed
        // as the delta from this rest rotation.
        private Quaternion _ghostRestLocalRotation = Quaternion.identity;
        private Quaternion _smoothedGhostLocal = Quaternion.identity;

        private Vector3 _ghostPrevPos;
        private Vector3 _ghostVelocity;

        public void InitializeBone(ArticulationDriver[] all)
        {
            if (body == null) return;
            boneTransform = body.transform;
            boneName = boneTransform.name;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == this) { boneIndex = i; break; }
            }

            _ghostRestLocalRotation = ghostBone != null ? ghostBone.localRotation : Quaternion.identity;
            _smoothedGhostLocal = _ghostRestLocalRotation;
            if (ghostBone != null) _ghostPrevPos = ghostBone.position;

            body.maxAngularVelocity = 30f;
            IsInitialized = true;
        }

        public void CaptureGhostState(float trackingSpeed = 0f)
        {
            if (ghostBone == null) return;

            float dt = Time.fixedDeltaTime;
            Vector3 cur = ghostBone.position;
            _ghostVelocity = dt > 0f ? (cur - _ghostPrevPos) / dt : Vector3.zero;
            _ghostPrevPos = cur;

            Quaternion raw = ghostBone.localRotation;
            _smoothedGhostLocal = trackingSpeed > 0f
                ? Quaternion.Slerp(_smoothedGhostLocal, raw, dt * trackingSpeed)
                : raw;
        }

        // Writes the joint drive targets so the physical body rotates toward the ghost pose.
        // limitStiffnessFloor / limitForceFloor keep a minimum spring on every axis even when the
        // muscle weight (masterDrive) is zero. The PhysX articulation limits are not a hard wall
        // under large external forces (gravity on a slack chain, impacts), so without a residual
        // spring the joints get pushed THROUGH their anatomical range on collapse (hyperextension,
        // reversed knees). This floor is the passive "ligament" tension that holds limbs inside the
        // authored joint limits while the muscles are off; only a force exceeding it can drive a
        // joint to its limit, matching real passive range-of-motion behavior.
        public void ApplyDrive(float masterDrive, float driveSpring, float driveDamper, float maxForce,
                               float limitStiffnessFloor = 0f, float limitForceFloor = 0f)
        {
            if (body == null || body.isRoot) return;

            float muscleSpring = masterDrive * driveSpring * driveMultiplier;
            float spring = Mathf.Max(muscleSpring, limitStiffnessFloor);
            float damper = driveDamper * damperMultiplier;
            float muscleForce = maxForce * driveMultiplier;
            float force = Mathf.Max(muscleForce, limitForceFloor);

            Quaternion delta = _smoothedGhostLocal * Quaternion.Inverse(_ghostRestLocalRotation);
            Vector3 reduced = ToReducedSpace(delta);

            SetDriveAxis(0, reduced.x, spring, damper, force, body.twistLock);
            SetDriveAxis(1, reduced.y, spring, damper, force, body.swingYLock);
            SetDriveAxis(2, reduced.z, spring, damper, force, body.swingZLock);
        }

        // Converts a target rotation (delta from rest) into the joint's reduced-coordinate
        // targets, in DEGREES. Adapted from the community ArtBodyExtensions helper
        // (Gustorvo / mstevenson lineage). This frame mapping is the primary in-editor
        // verification point: if a single-bone test shows mirrored or wrong-axis motion,
        // the fix lives here (anchorRotation vs parentAnchorRotation, axis frame).
        private Vector3 ToReducedSpace(Quaternion rotation)
        {
            rotation.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x)) return Vector3.zero;
            return Quaternion.Inverse(body.anchorRotation) * (axis.normalized * angle);
        }

        private void SetDriveAxis(int axisIndex, float targetDeg, float spring, float damper, float force, ArticulationDofLock dofLock)
        {
            if (dofLock == ArticulationDofLock.LockedMotion) return;

            ArticulationDrive drive = axisIndex == 0 ? body.xDrive : (axisIndex == 1 ? body.yDrive : body.zDrive);
            drive.target = targetDeg;
            drive.stiffness = spring;
            drive.damping = damper;
            drive.forceLimit = force;

            if (axisIndex == 0) body.xDrive = drive;
            else if (axisIndex == 1) body.yDrive = drive;
            else body.zDrive = drive;
        }

        // Optional velocity match (anchor). Drive-only mode keeps masterAnchor at 0; the
        // hips' world following is handled by the root-follow PD.
        public void ApplyAnchor(float masterAnchor)
        {
            if (body == null || ghostBone == null) return;
            float w = masterAnchor * anchorMultiplier;
            if (w <= 0f) return;

            Vector3 force = (_ghostVelocity - body.linearVelocity) * w;
            if (!float.IsNaN(force.x)) body.AddForce(force, ForceMode.VelocityChange);
        }

        // The floating-base root has no joint DOFs, so drives cannot hold it up in world
        // space. This PD makes the physical hips track the ghost hips (position + rotation).
        // Gated by `weight` (= masterDriveWeight) so it fades out on fall, leaving free ragdoll.
        public void ApplyRootFollow(float weight, float posStrength, float rotStrength)
        {
            if (body == null || ghostBone == null || !body.isRoot || weight <= 0f) return;

            Vector3 posError = ghostBone.position - body.transform.position;
            Vector3 targetVel = _ghostVelocity + posError * posStrength;
            Vector3 force = (targetVel - body.linearVelocity) * weight;
            if (!float.IsNaN(force.x)) body.AddForce(force, ForceMode.VelocityChange);

            Quaternion rotError = ghostBone.rotation * Quaternion.Inverse(body.transform.rotation);
            rotError.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x))
            {
                Vector3 targetAngVel = axis.normalized * (angle * Mathf.Deg2Rad) * rotStrength;
                Vector3 torque = (targetAngVel - body.angularVelocity) * weight;
                if (!float.IsNaN(torque.x)) body.AddTorque(torque, ForceMode.VelocityChange);
            }
        }

        public void WipeVelocities()
        {
            if (body == null) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        public void SnapToGhost()
        {
            if (body == null || ghostBone == null) return;
            // Only the root can be teleported directly; child links follow via the solver.
            if (body.isRoot) body.TeleportRoot(ghostBone.position, ghostBone.rotation);
        }
    }
}
