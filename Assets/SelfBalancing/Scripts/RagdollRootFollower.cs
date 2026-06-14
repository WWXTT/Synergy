namespace FrostPunchGames
{
    using UnityEngine;

    public class RagdollRootFollower : MonoBehaviour
    {
        [Header("Target")]
        public Transform physicsHips;

        [Header("Settings")]
        public bool ignoreYAxis = true;

        public Vector3 offset = Vector3.zero;

        void LateUpdate()
        {
            if (physicsHips == null) return;

            Vector3 initialHipsPos = physicsHips.position;
            Quaternion initialHipsRot = physicsHips.rotation;

            Vector3 targetPos = initialHipsPos + offset;

            if (ignoreYAxis)
            {
                targetPos.y = transform.position.y;
            }

            transform.position = targetPos;

            physicsHips.position = initialHipsPos;
            physicsHips.rotation = initialHipsRot;
        }
    }
}