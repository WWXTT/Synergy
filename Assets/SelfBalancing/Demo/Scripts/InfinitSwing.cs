namespace FrostPunchGames
{
    using UnityEngine;

    public class InfiniteSwing : MonoBehaviour
    {
        public float targetSpeed = 10f;
        private Rigidbody rb;

        void Start() => rb = GetComponent<Rigidbody>();

        void FixedUpdate()
        {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
            Vector3 currentVelocity = rb.linearVelocity;
#else
            Vector3 currentVelocity = rb.velocity;
#endif

            
            if (currentVelocity.magnitude > 0.1f && currentVelocity.magnitude < targetSpeed)
            {
                
                rb.AddForce(currentVelocity.normalized * 0.5f, ForceMode.Acceleration);
            }
        }
    }
}