using System.Collections.Generic;
using UnityEngine;

namespace FrostPunchGames
{
    // LIPM (Linear Inverted Pendulum Model) Capture Point solver.
    // Pure value-type math: no dependency on ArticulationBody / Rigidbody.
    // The driver feeds it a weighted center-of-mass and the support-polygon
    // corners each FixedUpdate; it reports whether the character can recover.
    public class CapturePointSolver : MonoBehaviour
    {
        public enum BalanceState
        {
            Balanced,   // capture point inside the support polygon
            Perturbed,  // just outside -> ankle/hip recovery is viable
            Falling     // far outside -> a step (or full fall) is required
        }

        [Header("LIPM")]
        [Tooltip("Gravity magnitude used for omega. Leave <= 0 to read Physics.gravity at runtime.")]
        public float gravityOverride = 0f;
        [Tooltip("Lower clamp for COM height above the support plane, prevents omega blow-up near the ground.")]
        public float minComHeight = 0.3f;

        [Header("Classification threshold (meters past the support edge)")]
        [Tooltip("Capture point outside the polygon but within this distance -> Perturbed (ankle/hip recovery). Beyond it -> Falling (step required).")]
        public float fallingMargin = 0.25f;

        // --- Outputs (read-only for consumers: syncer + future gait module) ---
        public Vector3 Com { get; private set; }
        // Effective (urgency-blended) COM velocity used to compute the capture point.
        public Vector3 ComVelocity { get; private set; }
        // Adaptively-filtered PHYSICAL COM velocity, unblended. Consumed by BalanceUrgencyEvaluator
        // so the arbiter measures the real physical deviation rather than the blend it itself drives.
        public Vector3 PhysicalComVelocity { get; private set; }
        public Vector3 CapturePoint { get; private set; }
        public float Omega { get; private set; }
        public BalanceState State { get; private set; } = BalanceState.Balanced;
        // Signed distance from CP to the support polygon edge.
        // Negative = inside (magnitude = distance to nearest edge), positive = outside.
        public float SignedDistanceToSupportEdge { get; private set; }
        public bool IsSupportValid { get; private set; }

        // The capture point IS the LIPM divergent component of motion (DCM, xi = x + v/omega).
        public Vector3 Dcm => CapturePoint;
        // Rate at which the DCM is escaping the support polygon (0 while inside).
        public float DcmDivergenceRate { get; private set; }

        // World-space convex-hull ring of the current support polygon, for debug gizmos.
        public IReadOnlyList<Vector3> DebugHullWorld => _hullWorld;

        private Vector3 _prevCom;
        private bool _hasPrev;
        private Vector3 _filteredPhysComVel;

        // Reusable scratch buffers to avoid per-frame GC.
        private readonly List<Vector2> _hull = new List<Vector2>(16);
        private readonly List<Vector2> _scratch = new List<Vector2>(16);
        private readonly List<Vector3> _hullWorld = new List<Vector3>(16);

        public void ResetState()
        {
            _hasPrev = false;
            ComVelocity = Vector3.zero;
            PhysicalComVelocity = Vector3.zero;
            _filteredPhysComVel = Vector3.zero;
            State = BalanceState.Balanced;
        }

        private float Gravity => gravityOverride > 0f ? gravityOverride : Mathf.Abs(Physics.gravity.y);

        // Main entry. supportCornersWorld are world-space points (e.g. foot collider
        // base corners); their XZ projection forms the support polygon, their min Y
        // defines the support plane height.
        public BalanceState Evaluate(Vector3 weightedCom, float dt, IReadOnlyList<Vector3> supportCornersWorld)
        {
            // Backward-compatible path: no animation reference, no urgency blend.
            // ghostComVelocity = physical velocity + urgency = 1 reproduces the original behavior.
            return Evaluate(weightedCom, Vector3.zero, 1f, dt, supportCornersWorld);
        }

        // Phase-2 entry. The effective COM velocity used for the capture point is a urgency-driven
        // blend between the animation's intended COM velocity (ghostComVelocity) and the physical
        // rig's measured COM velocity. Calm (urgency 0) => follow the animation, rejecting drive
        // noise so the balance read stays steady. Disturbed (urgency 1) => follow real physics so
        // the LIPM corrects the true deviation. The physical velocity is also adaptively low-pass
        // filtered: heavier smoothing when calm, faster response when urgent.
        // locomotionVelocity is the commanded root-motion (walk/run) velocity. The capture point
        // is evaluated in this MOVING support frame: subtracting it leaves only the balance
        // deviation, so steady locomotion isn't mistaken for a forward fall. Zero = in-place balance.
        public BalanceState Evaluate(Vector3 weightedCom, Vector3 ghostComVelocity, float urgency,
                                     float dt, IReadOnlyList<Vector3> supportCornersWorld,
                                     Vector3 locomotionVelocity = default)
        {
            urgency = Mathf.Clamp01(urgency);

            // Raw physical COM velocity via finite difference.
            Vector3 physComVel;
            if (_hasPrev && dt > 0f)
                physComVel = (weightedCom - _prevCom) / dt;
            else
                physComVel = Vector3.zero;
            _prevCom = weightedCom;
            _hasPrev = true;
            Com = weightedCom;

            // Adaptive EMA on the physical velocity (time constant shrinks with urgency).
            if (dt > 0f)
            {
                float tau = Mathf.Lerp(0.05f, 0.01f, urgency);
                float alpha = tau > 0f ? 1f - Mathf.Exp(-dt / tau) : 1f;
                _filteredPhysComVel = Vector3.Lerp(_filteredPhysComVel, physComVel, alpha);
            }
            else
            {
                _filteredPhysComVel = physComVel;
            }

            // Blend animation-intended vs physical-measured COM velocity.
            Vector3 effectiveComVel = Vector3.Lerp(ghostComVelocity, _filteredPhysComVel, urgency);
            ComVelocity = effectiveComVel;
            PhysicalComVelocity = _filteredPhysComVel;

            float supportHeight = SupportHeight(supportCornersWorld, weightedCom.y);

            float h = Mathf.Max(minComHeight, weightedCom.y - supportHeight);
            Omega = Mathf.Sqrt(Gravity / h);

            // CP = COM + V/omega, evaluated on the XZ plane in the moving support frame
            // (V = effective blended velocity MINUS commanded locomotion velocity).
            Vector3 balanceVel = effectiveComVel - locomotionVelocity;
            Vector2 comXZ = new Vector2(weightedCom.x, weightedCom.z);
            Vector2 velXZ = new Vector2(balanceVel.x, balanceVel.z);
            Vector2 cpXZ = comXZ + velXZ / Omega;
            CapturePoint = new Vector3(cpXZ.x, supportHeight, cpXZ.y);

            BuildHullXZ(supportCornersWorld);
            IsSupportValid = _hull.Count >= 3;

            if (!IsSupportValid)
            {
                // Degenerate support (single point / no contact): use distance to the
                // sole point (or COM) so the caller still gets a sane reading.
                Vector2 anchor = _hull.Count > 0 ? _hull[0] : comXZ;
                SignedDistanceToSupportEdge = Vector2.Distance(cpXZ, anchor);
            }
            else
            {
                SignedDistanceToSupportEdge = SignedDistanceToPolygon(cpXZ, _hull);
            }

            State = Classify(SignedDistanceToSupportEdge);

            // Divergence rate: how fast the DCM is escaping the support polygon.
            // Zero while the CP is inside (negative signed distance).
            DcmDivergenceRate = Omega * Mathf.Max(0f, SignedDistanceToSupportEdge);

            // Cache the hull as world-space points (at the support plane) for debug drawing.
            _hullWorld.Clear();
            for (int i = 0; i < _hull.Count; i++)
                _hullWorld.Add(new Vector3(_hull[i].x, supportHeight, _hull[i].y));

            return State;
        }

        private BalanceState Classify(float signedDist)
        {
            if (signedDist <= 0f) return BalanceState.Balanced;
            if (signedDist <= fallingMargin) return BalanceState.Perturbed;
            return BalanceState.Falling;
        }

        private static float SupportHeight(IReadOnlyList<Vector3> corners, float fallbackY)
        {
            if (corners == null || corners.Count == 0) return fallbackY - 1f;
            float minY = float.MaxValue;
            for (int i = 0; i < corners.Count; i++)
                if (corners[i].y < minY) minY = corners[i].y;
            return minY;
        }

        private void BuildHullXZ(IReadOnlyList<Vector3> corners)
        {
            _scratch.Clear();
            if (corners != null)
                for (int i = 0; i < corners.Count; i++)
                    _scratch.Add(new Vector2(corners[i].x, corners[i].z));
            ConvexHull(_scratch, _hull);
        }

        // Andrew's monotone chain convex hull on XZ-projected points.
        private static void ConvexHull(List<Vector2> points, List<Vector2> result)
        {
            result.Clear();
            int n = points.Count;
            if (n < 3)
            {
                result.AddRange(points);
                return;
            }

            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            // Lower hull.
            for (int i = 0; i < n; i++)
            {
                while (result.Count >= 2 && Cross(result[result.Count - 2], result[result.Count - 1], points[i]) <= 0f)
                    result.RemoveAt(result.Count - 1);
                result.Add(points[i]);
            }

            // Upper hull.
            int lower = result.Count + 1;
            for (int i = n - 2; i >= 0; i--)
            {
                while (result.Count >= lower && Cross(result[result.Count - 2], result[result.Count - 1], points[i]) <= 0f)
                    result.RemoveAt(result.Count - 1);
                result.Add(points[i]);
            }

            // Last point equals the first; drop it.
            if (result.Count > 1) result.RemoveAt(result.Count - 1);
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        // Returns signed distance to the polygon boundary: negative inside, positive outside.
        private static float SignedDistanceToPolygon(Vector2 p, List<Vector2> poly)
        {
            bool inside = PointInPolygon(p, poly);
            float minEdgeDist = float.MaxValue;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                float d = DistanceToSegment(p, poly[j], poly[i]);
                if (d < minEdgeDist) minEdgeDist = d;
            }
            return inside ? -minEdgeDist : minEdgeDist;
        }

        private static bool PointInPolygon(Vector2 p, List<Vector2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-8f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return Vector2.Distance(p, a + t * ab);
        }
    }
}
