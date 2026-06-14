using UnityEngine;

namespace FrostPunchGames
{
    public class ShadowRigFollower : MonoBehaviour
    {
        [HideInInspector] public Transform[] PhysicsBones;
        [HideInInspector] public Transform[] ShadowBones;

        void LateUpdate()
        {
            if (PhysicsBones == null || ShadowBones == null) return;

            for (int i = 0; i < PhysicsBones.Length; i++)
            {
                if (PhysicsBones[i] != null && ShadowBones[i] != null)
                {
                    ShadowBones[i].SetPositionAndRotation(PhysicsBones[i].position, PhysicsBones[i].rotation);
                }
            }
        }
    }
}