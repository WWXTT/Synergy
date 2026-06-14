using System.Collections.Generic;
using UnityEngine;

namespace FrostPunchGames
{
    // Read-only diagnostic overlay for the LIPM balance loop. Draws the center of mass, capture
    // point, support polygon and DCM divergence in the Scene view, and a numeric HUD in the Game
    // view. Introduces no dynamics of its own; it only reads the capture-point solver and IK solver,
    // so it is safe to leave enabled while tuning or hunting the drift bug.
    [AddComponentMenu("Procedural Animation/Ragdoll Balance Debug")]
    public class RagdollBalanceDebug : MonoBehaviour
    {
        [Tooltip("The physics syncer that owns the CapturePointSolver (on the Physical Rig).")]
        public ArticulationSyncer syncer;
        [Tooltip("The IK solver, for CaptureAuthority and feedback state.")]
        public IKSolver ikSolver;

        [Header("Toggles")]
        public bool drawDebug = true;
        public bool drawGizmos = true;
        public bool drawHud = true;

        [Header("Sizes")]
        public float comRadius = 0.05f;
        public float cpRadius = 0.06f;

        private CapturePointSolver Solver => syncer != null ? syncer.capturePoint : null;

        private static Color StateColor(CapturePointSolver.BalanceState state)
        {
            switch (state)
            {
                case CapturePointSolver.BalanceState.Balanced: return Color.green;
                case CapturePointSolver.BalanceState.Perturbed: return Color.yellow;
                default: return Color.red;
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawDebug || !drawGizmos) return;
            var solver = Solver;
            if (solver == null) return;

            Color stateColor = StateColor(solver.State);

            // The inverted-pendulum stick spans COM (top) -> support-polygon centroid (bottom).
            // When standing still the two should be vertically aligned (COM directly above the
            // support center). The Capture Point is still used internally for the balance test,
            // but the pendulum base drawn here is the support centroid, not the velocity-projected CP.
            IReadOnlyList<Vector3> hull = solver.DebugHullWorld;
            Vector3 supportCentroid;
            if (hull != null && hull.Count >= 1)
            {
                supportCentroid = Vector3.zero;
                for (int i = 0; i < hull.Count; i++) supportCentroid += hull[i];
                supportCentroid /= hull.Count;
            }
            else
            {
                supportCentroid = solver.CapturePoint; // degenerate fallback (no support contact)
            }

            // COM (white) and pendulum base (state-colored, at the support centroid).
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(solver.Com, comRadius);

            Gizmos.color = stateColor;
            Gizmos.DrawSphere(supportCentroid, cpRadius);

            // COM -> support-centroid link (the inverted-pendulum stick).
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(solver.Com, supportCentroid);

            // Support polygon ring.
            if (hull != null && hull.Count >= 2)
            {
                Gizmos.color = solver.IsSupportValid ? new Color(0.2f, 0.8f, 1f) : new Color(1f, 0.5f, 0f);
                Vector3 centroid = Vector3.zero;
                for (int i = 0; i < hull.Count; i++)
                {
                    Vector3 a = hull[i];
                    Vector3 b = hull[(i + 1) % hull.Count];
                    Gizmos.DrawLine(a, b);
                    centroid += a;
                }
                centroid /= hull.Count;

                // DCM divergence arrow: from the support centroid toward the capture point.
                Gizmos.color = stateColor;
                Vector3 cp = solver.CapturePoint;
                Gizmos.DrawLine(centroid, cp);
                Vector3 dir = cp - centroid;
                if (dir.sqrMagnitude > 1e-5f)
                {
                    dir.Normalize();
                    Vector3 right = Vector3.Cross(Vector3.up, dir).normalized * 0.05f;
                    Gizmos.DrawLine(cp, cp - dir * 0.12f + right);
                    Gizmos.DrawLine(cp, cp - dir * 0.12f - right);
                }
            }
        }

        private void OnGUI()
        {
            if (!drawDebug || !drawHud) return;
            var solver = Solver;
            if (solver == null) return;

            float authority = ikSolver != null ? ikSolver.CaptureAuthority : 0f;
            bool active = ikSolver != null && ikSolver.BalanceFeedbackActive;

            GUI.color = StateColor(solver.State);
            GUILayout.BeginArea(new Rect(10, 10, 320, 200), GUI.skin.box);
            GUILayout.Label($"Balance State : {solver.State}");
            GUILayout.Label($"Feedback Active: {active}");
            GUILayout.Label($"Signed Dist : {solver.SignedDistanceToSupportEdge:F3} m");
            GUILayout.Label($"Capture Auth : {authority:F2}");
            GUILayout.Label($"Omega : {solver.Omega:F2}");
            GUILayout.Label($"COM Vel : {solver.ComVelocity.magnitude:F2} m/s");
            GUILayout.Label($"DCM Diverge : {solver.DcmDivergenceRate:F3}");
            GUILayout.Label($"Support Valid : {solver.IsSupportValid}");
            GUILayout.EndArea();
            GUI.color = Color.white;
        }
    }
}
