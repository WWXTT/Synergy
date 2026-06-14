using UnityEngine;

namespace FrostPunchGames
{
    // Phase-2 weapon controller. Holds the weapon's ArticulationDriver and modulates its drive
    // stiffness with balance urgency: calm => stiff (the weapon rigidly tracks the held pose);
    // disturbed => soft (the weapon lags and swings passively, its inertia producing a reaction
    // torque that absorbs energy and helps the body recover). Stiffness changes ride a SmoothDamp
    // ramp so a sudden urgency spike never snaps the drive and excites oscillation.
    //
    // Attach to the weapon GameObject (the one made into an ArticulationBody by
    // SimpleRagdollBuilder.AttachWeapon).
    public class WeaponPassiveSwing : MonoBehaviour
    {
        [Header("References (auto-wired by ActiveRagdollBrain)")]
        [Tooltip("The driver entry for this weapon inside ArticulationSyncer.physicsBones.")]
        public ArticulationDriver driver;
        public BalanceUrgencyEvaluator urgency;

        [Header("Stiffness vs Urgency")]
        [Tooltip("driveMultiplier when calm (urgency 0): rigid grip.")]
        public float maxStiffnessMultiplier = 2f;
        [Tooltip("driveMultiplier when fully swinging (urgency at swingUrgencyHi): soft, passive.")]
        public float minStiffnessMultiplier = 0.1f;
        [Tooltip("Urgency at which the weapon reaches its softest (fully passive) state.")]
        public float swingUrgencyHi = 0.6f;
        [Tooltip("Smoothing time for stiffness transitions (prevents drive snaps / oscillation).")]
        public float stiffnessSmoothTime = 0.12f;

        [Header("Active Balance (reserved, not implemented this phase)")]
        [Tooltip("Placeholder for a future active weapon-balance torque. No effect while false.")]
        public bool weaponActiveBalance = false;

        private float _vel;

        public void Configure(ArticulationDriver weaponDriver, BalanceUrgencyEvaluator arbiter)
        {
            driver = weaponDriver;
            urgency = arbiter;
            if (driver != null) driver.driveMultiplier = maxStiffnessMultiplier;
        }

        private void FixedUpdate()
        {
            if (driver == null) return;

            float u = urgency != null ? urgency.Urgency : 0f;
            float swingWeight = Mathf.Clamp01(Mathf.InverseLerp(0f, swingUrgencyHi, u));
            float targetMult = Mathf.Lerp(maxStiffnessMultiplier, minStiffnessMultiplier, swingWeight);

            driver.driveMultiplier = Mathf.SmoothDamp(driver.driveMultiplier, targetMult, ref _vel, stiffnessSmoothTime);

            if (weaponActiveBalance) ApplyWeaponBalanceTorque();
        }

        // Reserved: drive the weapon's joint actively to use its mass as a balance flywheel.
        // Intentionally empty this phase (interface stub only).
        private void ApplyWeaponBalanceTorque() { }
    }
}
