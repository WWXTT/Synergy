using UnityEngine;

namespace FrostPunchGames
{
    // Phase-2 hand/weapon contact sensor. When a hand (or the tip of a held weapon) presses on
    // static geometry hard enough, its contact point is offered to the balance solver as an extra
    // support-polygon vertex (via ArticulationSyncer.handContacts). A wider polygon means the
    // capture point is less likely to escape, so the character naturally leans on the surface and
    // steps less. The sensor self-gates by balance urgency so an incidental brush during calm
    // standing does not register as support.
    //
    // Attach to a hand bone (or weapon tip) that carries a NON-trigger collider.
    [RequireComponent(typeof(Collider))]
    public class HandContactSupport : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Urgency arbiter. Contact only counts as support once urgency enters the window below. Auto-wired by ActiveRagdollBrain.")]
        public BalanceUrgencyEvaluator urgency;

        [Header("Contact Gate")]
        [Tooltip("Minimum normal force (N) for a contact to count as load-bearing support.")]
        public float forceThreshold = 20f;
        [Tooltip("Layers treated as static, leanable environment.")]
        public LayerMask environmentLayers = ~0;

        [Header("Urgency Window")]
        [Tooltip("Below this urgency the hand contact is ignored (avoids false support while calm).")]
        public float urgencyLo = 0.1f;
        [Tooltip("At/above this urgency the hand contact is fully trusted as support.")]
        public float urgencyHi = 0.5f;

        [Header("Release")]
        [Tooltip("Seconds to keep reporting support after physical contact is lost, for a smooth fall-back to leg-only balance.")]
        public float releaseHold = 0.15f;

        // --- Outputs (read by ArticulationSyncer.BuildSupportCorners) ---
        public bool HasActiveSupport { get; private set; }
        public Vector3 ContactPoint { get; private set; }
        // 0..1 strength after the urgency window gate; ~0 means "ignore me".
        public float SupportWeight { get; private set; }

        private bool _touching;
        private Vector3 _rawContactPoint;
        private float _measuredNormalForce;
        private float _holdTimer;

        private void EvaluateContact(Collision collision)
        {
            if (((1 << collision.gameObject.layer) & environmentLayers) == 0)
            {
                _touching = false;
                return;
            }

            float dt = Time.fixedDeltaTime;
            // collision.impulse points along the contact normal; force ~= impulse / dt.
            _measuredNormalForce = dt > 0f ? collision.impulse.magnitude / dt : 0f;
            _rawContactPoint = collision.GetContact(0).point;
            _touching = _measuredNormalForce >= forceThreshold;
        }

        private void OnCollisionStay(Collision collision) => EvaluateContact(collision);
        private void OnCollisionEnter(Collision collision) => EvaluateContact(collision);

        private void OnCollisionExit(Collision collision)
        {
            if (((1 << collision.gameObject.layer) & environmentLayers) != 0)
                _touching = false;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // Hold the last contact for a short window after release, then decay out.
            if (_touching)
            {
                _holdTimer = releaseHold;
                ContactPoint = _rawContactPoint;
            }
            else
            {
                _holdTimer = Mathf.Max(0f, _holdTimer - dt);
            }

            float u = urgency != null ? urgency.Urgency : 1f;
            float urgencyGate = Mathf.Clamp01(Mathf.InverseLerp(urgencyLo, urgencyHi, u));
            float holdGate = (_touching || _holdTimer > 0f) ? 1f : 0f;

            SupportWeight = urgencyGate * holdGate;
            HasActiveSupport = SupportWeight > 0.05f;
        }
    }
}
