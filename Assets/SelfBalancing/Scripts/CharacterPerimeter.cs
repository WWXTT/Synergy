using UnityEngine;
using UnityEngine.Events;

namespace FrostPunchGames
{
    [System.Serializable]
    public class PerimeterColliderEvent : UnityEvent<Collider> { }

    // A trigger capsule that surrounds the character and follows the ghost rig. It is the explicit
    // "sense external force" channel the balance loop otherwise lacks: gameplay code (or another
    // collider entering the trigger) can call ApplyExternalImpulse / OnPerimeterHit, which routes the
    // impulse into the physical rig via ArticulationSyncer. The disturbance then shows up in the COM
    // and capture point, so the LIPM recovery responds naturally. Also serves as the sole collision
    // proxy when the physics rig is downgraded at distance (see PhysicsLODController).
    [AddComponentMenu("Procedural Animation/Character Perimeter")]
    [RequireComponent(typeof(CapsuleCollider))]
    public class CharacterPerimeter : MonoBehaviour
    {
        [Tooltip("The physics syncer that receives injected impulses.")]
        public ArticulationSyncer syncer;
        [Tooltip("The ghost rig transform the perimeter tracks horizontally.")]
        public Transform ghostRoot;

        [Header("Capsule")]
        [Tooltip("Radius of the perimeter capsule.")]
        public float radius = 0.45f;
        [Tooltip("Total height of the perimeter capsule.")]
        public float height = 1.8f;

        [Header("Events")]
        public PerimeterColliderEvent OnPerimeterEnter;
        public PerimeterColliderEvent OnPerimeterExit;

        private CapsuleCollider _capsule;

        private void Awake()
        {
            _capsule = GetComponent<CapsuleCollider>();
            _capsule.isTrigger = true;
            ApplyShape();
        }

        private void ApplyShape()
        {
            if (_capsule == null) return;
            _capsule.radius = radius;
            _capsule.height = height;
            _capsule.direction = 1; // Y-axis
            _capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }

        // Lets the brain / profile resize the capsule after construction.
        public void Configure(float newRadius, float newHeight)
        {
            radius = newRadius;
            height = newHeight;
            if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
            ApplyShape();
        }

        private void LateUpdate()
        {
            // The perimeter component lives on the master root, which is the PARENT of the ghost rig.
            // Moving this transform onto the ghost would drag the ghost (its child) by the same delta
            // each frame, creating an unbounded position runaway. Track the ghost with the capsule's
            // local center instead, leaving the master root stationary.
            if (ghostRoot == null || _capsule == null) return;
            Vector3 local = transform.InverseTransformPoint(ghostRoot.position);
            _capsule.center = local + new Vector3(0f, height * 0.5f, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            OnPerimeterEnter?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            OnPerimeterExit?.Invoke(other);
        }

        // Inject a world-space impulse into the physical rig at a contact point.
        public void OnPerimeterHit(Vector3 worldPoint, Vector3 worldForce)
        {
            ApplyExternalImpulse(worldPoint, worldForce);
        }

        public void ApplyExternalImpulse(Vector3 worldPoint, Vector3 worldForce)
        {
            if (syncer != null) syncer.ApplyExternalImpulse(worldPoint, worldForce);
        }

        private void OnValidate()
        {
            if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
            ApplyShape();
        }
    }
}
