using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace FrostPunchGames
{
    public class StepManager : MonoBehaviour
    {
        private IKSolver _solver;
        private float _lastStepTime;
        private LegController _lastSteppedLeg;
        private Dictionary<LegController, float> _legCooldowns = new Dictionary<LegController, float>();

        private float _timeSinceStop = 0f;
        private Vector3 _lastFrameRootPos;

        public float StartupDelay = 0.5f;
        private float _startupTimer = 0f;

        private void Start()
        {
            _solver = GetComponent<IKSolver>();
            if (_solver != null && _solver.CharacterRoot != null)
                _lastFrameRootPos = _solver.CharacterRoot.position;

            _startupTimer = 0f;
        }

        public void RegisterLeg(LegController leg)
        {
            if (!_legCooldowns.ContainsKey(leg)) _legCooldowns.Add(leg, 0f);
        }

        private void Update()
        {
            if (_solver == null) return;

            if (_solver.PhysicsSyncer != null)
            {
                if (_solver.PhysicsSyncer.IsFullyCollapsed) return;

                if (_solver.IKBlend <= 0.01f) return;
            }

            float dt = _solver.DeltaTime;

            if (_startupTimer < StartupDelay)
            {
                _startupTimer += dt;
                foreach (var leg in _legCooldowns.Keys)
                {
                    if (leg.IsGrounded && !leg.IsPlanted) leg.ForceReglue();
                }
                if (_solver.CharacterRoot != null)
                {
                    _lastFrameRootPos = _solver.CharacterRoot.position;
                }
                return;
            }

            var gait = _solver.CalculateGait();

            float kinematicSpeed = 0f;
            if (_solver.CharacterRoot != null)
            {
                float dist = Vector3.Distance(_solver.CharacterRoot.position, _lastFrameRootPos);
                kinematicSpeed = dist / Mathf.Max(Time.deltaTime, 0.0001f);
                _lastFrameRootPos = _solver.CharacterRoot.position;
            }

            float angularSpeed = Mathf.Abs(_solver.AngularVelocity);
            bool isRotating = angularSpeed > 25f;
            bool isIdle = kinematicSpeed < 0.1f && !isRotating && !_solver.IsAccelerating;

            if (isIdle) _timeSinceStop += dt;
            else _timeSinceStop = 0f;

            var legKeys = _legCooldowns.Keys.ToList();
            foreach (var key in legKeys)
            {
                float decayRate = (kinematicSpeed > 0.1f) ? dt * 1.5f : dt;
                _legCooldowns[key] = Mathf.Max(0f, _legCooldowns[key] - decayRate);
            }

            // LIPM feedback: when the capture point escapes the support polygon, force a corrective
            // step toward the escape side (in addition to the normal stretch/twist scoring below).
            if (_solver.BalanceFeedbackActive && _solver.CaptureSignedDistance > _solver.CPStepTriggerMargin)
            {
                LegController cpLeg = SelectLegForCapturePoint();
                // The faster the DCM is diverging, the shorter the rhythm gate, so a hard shove
                // triggers a corrective step almost immediately instead of waiting out the cadence.
                float urgency = Mathf.Clamp01(_solver.CaptureDivergenceRate * _solver.CPStepUrgencyGain * 0.1f);
                float cadenceGate = gait.StepDuration * Mathf.Lerp(0.2f, 0.02f, urgency);
                if (cpLeg != null && HasSupport(cpLeg) && Time.time > _lastStepTime + cadenceGate)
                {
                    TriggerStep(cpLeg, gait.StepDuration);
                    return;
                }
            }

            LegController bestLeg = null;
            float maxScore = -1f;

            foreach (var leg in legKeys)
            {
                if (!leg.IsPlanted) continue;

                float physicalStretch = Vector3.Distance(leg.HipPosition, leg.PlantedPosition) / leg.GetLegLength();
                bool criticalStretch = physicalStretch > 1.15f;

                float angleError = Quaternion.Angle(leg.CurrentPlantedRotation, leg.CurrentAlignedRotation);
                bool criticalTwist = angleError > 40f;

                if (criticalStretch || criticalTwist)
                {
                    _legCooldowns[leg] = 0f;
                }

                if (_legCooldowns[leg] > 0f) continue;

                float error;
                float threshold = gait.StepThreshold;

                if (_timeSinceStop > _solver.StanceSettleTime)
                {
                    error = leg.GetStepError();
                    error += (angleError / 90f) * 0.1f;
                    threshold = Mathf.Max(0.15f * _solver.ScaleReference, gait.StepThreshold * 0.75f);
                }
                else
                {
                    error = leg.GetStepError();
                    error += (angleError / 90f) * gait.StepThreshold;

                    if (isRotating && kinematicSpeed < 0.1f)
                    {
                        threshold *= 0.6f;
                    }

                    if (kinematicSpeed > _solver.SmoothedVelocity.magnitude + 0.1f || _solver.IsAccelerating)
                    {
                        threshold *= 0.35f;
                    }

                    if (criticalStretch) error += 20f;
                    if (criticalTwist) error += 10f;
                }

                Vector3 localVel = _solver.CharacterRoot.InverseTransformDirection(_solver.SmoothedVelocity);
                if (Mathf.Abs(localVel.x) > 0.15f)
                {
                    bool isStrafingRight = localVel.x > 0;
                    if (isStrafingRight && leg.Side == LegSide.Left) threshold *= 2.5f;
                    if (!isStrafingRight && leg.Side == LegSide.Right) threshold *= 2.5f;
                }

                if (leg == _lastSteppedLeg) threshold *= 5.0f;

                float ratio = error / Mathf.Max(threshold, 0.001f);

                if (gait.RequireSupport && !HasSupport(leg) && !criticalStretch) continue;

                if (ratio > 1.0f)
                {
                    if (ratio > maxScore) { maxScore = ratio; bestLeg = leg; }
                }
            }

            if (bestLeg != null)
            {
                float rhythmDelay = gait.StepDuration * 0.5f;

                if (_timeSinceStop > _solver.StanceSettleTime) rhythmDelay = gait.StepDuration * 0.25f;
                if (isRotating && kinematicSpeed < 0.1f) rhythmDelay = gait.StepDuration * 0.25f;

                float stretchFactor = Vector3.Distance(bestLeg.HipPosition, bestLeg.PlantedPosition) / bestLeg.GetLegLength();

                if (stretchFactor > 1.15f || Quaternion.Angle(bestLeg.CurrentPlantedRotation, bestLeg.CurrentAlignedRotation) > 40f)
                {
                    rhythmDelay = 0f;
                }

                if (Time.time > _lastStepTime + rhythmDelay)
                {
                    float duration = (_timeSinceStop > _solver.StanceSettleTime) ?
                                     gait.StepDuration * 1.5f : gait.StepDuration;

                    TriggerStep(bestLeg, duration);
                }
            }
        }

        private void TriggerStep(LegController leg, float duration)
        {
            leg.TriggerStep(duration);
            _lastStepTime = Time.time;
            _lastSteppedLeg = leg;
            _legCooldowns[leg] = duration * 1.1f;
        }

        // Picks the planted leg on the side the capture point is escaping toward, so the corrective
        // step plants a foot under the falling COM and widens the base in that direction.
        private LegController SelectLegForCapturePoint()
        {
            if (_solver.CharacterRoot == null) return null;

            Vector3 escapeLocal = _solver.CharacterRoot.InverseTransformDirection(
                _solver.CapturePointWorld - _solver.CharacterRoot.position);
            escapeLocal.y = 0f;

            LegController best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < _solver.GetLegCount(); i++)
            {
                var leg = _solver.GetLegController(i);
                if (leg == null || !leg.IsPlanted) continue;
                if (_legCooldowns.TryGetValue(leg, out float cd) && cd > 0f) continue;

                Vector3 footLocal = _solver.CharacterRoot.InverseTransformPoint(leg.PlantedPosition);
                // Same lateral sign as the escape direction => this foot is on the escape side.
                float score = footLocal.x * escapeLocal.x + footLocal.z * escapeLocal.z;
                if (score > bestScore) { bestScore = score; best = leg; }
            }
            return best;
        }

        private bool HasSupport(LegController leg)
        {
            foreach (var other in _legCooldowns.Keys)
            {
                if (other == leg) continue;
                if (!other.IsPlanted) return false;
            }
            return true;
        }
    }
}