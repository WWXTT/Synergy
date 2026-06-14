using UnityEngine;

namespace FrostPunchGames
{
    [System.Serializable]
    public class LegSetup
    {
        public string Name = "Leg";
        public Transform UpperLeg;
        public Transform LowerLeg;
        public Transform Foot;
        public Transform Toe;
        public LegSide Side = LegSide.Left;

        [Range(0f, 1f)]
        public float IKBlend = 1f;

        public Vector3 AnkleToHeelOffset = new Vector3(0f, -0.05f, -0.02f);

        public bool IsValid => UpperLeg != null && LowerLeg != null && Foot != null;

        public float GetLegLength()
        {
            if (!IsValid) return 0f;
            float upperLength = Vector3.Distance(UpperLeg.position, LowerLeg.position);
            float lowerLength = Vector3.Distance(LowerLeg.position, Foot.position);
            return upperLength + lowerLength;
        }

        public float GetFootHeight()
        {
            if (!IsValid) return 0.05f;

            float footHeight = AnkleToHeelOffset.magnitude;

            if (Toe != null)
            {
                Vector3 ankleToToe = Toe.position - Foot.position;
                float toeHeight = Mathf.Abs(ankleToToe.y);
                footHeight = Mathf.Max(footHeight, toeHeight);
            }

            return footHeight;
        }
    }

    public enum LegSide
    {
        Left,
        Right
    }
}