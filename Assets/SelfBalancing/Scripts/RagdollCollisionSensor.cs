using UnityEngine;

namespace FrostPunchGames
{
    public class RagdollCollisionSensor : MonoBehaviour
    {
        public bool IsInContact { get; private set; }

        void OnCollisionStay(Collision collision)
        {
            IsInContact = true;
        }

        void OnCollisionExit(Collision collision)
        {
            IsInContact = false;
        }
    }
}