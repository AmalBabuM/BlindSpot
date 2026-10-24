using UnityEngine;

namespace Clearance
{
    public enum SuspectState { Armed, Considering, Surrendered, Restrained, Stunned, Down, Patrolling, Alerted, HandsUp, VerifiedUnarmed }

    public sealed class SuspectController : MonoBehaviour
    {
        [SerializeField] DecisionScore score;
        [SerializeField] float responseSeconds = 1.6f;
        [SerializeField] Transform leftArm, rightArm;
        [SerializeField] GameObject weapon;
        [SerializeField] bool useAnimatedFall;
        float respondsAt;
        float weaponCheck;
        float handsUpAt;
        [SerializeField] float inspectionSeconds = 1.25f;
        public float WeaponCheckProgress => Mathf.Clamp01(weaponCheck / inspectionSeconds);
        public SuspectState State { get; private set; }
        public bool Resolved => State == SuspectState.Restrained || State == SuspectState.Stunned || State == SuspectState.Down;
        public string Status => State switch
        {
            SuspectState.Armed => "Armed person | Q: Drop the weapon!",
            SuspectState.Patrolling => "Patrolling | Approach carefully",
            SuspectState.Alerted => "He has noticed you | Q: Raise your hands!",
            SuspectState.HandsUp => $"Hands up | Inspect with flashlight nearby ({WeaponCheckProgress:P0})",
            SuspectState.VerifiedUnarmed => "No weapon found | Q: Kneel and surrender!",
            SuspectState.Considering => "Complying... wait for him to kneel",
            SuspectState.Surrendered => "Surrendered | Approach and press E to restrain",
            SuspectState.Restrained => "Restrained",
            SuspectState.Stunned => "Incapacitated with stun device",
            _ => "Down | Decision recorded"
        };
        public void Configure(DecisionScore value, Transform left, Transform right, GameObject heldWeapon)
        { score = value; leftArm = left; rightArm = right; weapon = heldWeapon; }
        public void ConfigureAnimatedResponse(float kneelDuration)
        { responseSeconds = Mathf.Max(.1f, kneelDuration + .15f); useAnimatedFall = true; }
        public void BeginPatrol() { if (State == SuspectState.Armed) State = SuspectState.Patrolling; }
        public void Alert() { if (State == SuspectState.Patrolling) State = SuspectState.Alerted; }
        public bool Command()
        {
            if (State == SuspectState.VerifiedUnarmed)
            {
                State = SuspectState.Considering;
                respondsAt = Time.time + responseSeconds;
                score.Record("kneel-command", 0, "Kneel command issued after verifying no weapon");
                return true;
            }
            if (State != SuspectState.Armed && State != SuspectState.Alerted) return false;
            State = SuspectState.HandsUp;
            handsUpAt = Time.time;
            if (useAnimatedFall && weapon) weapon.SetActive(false);
            score.Record("command", score.Rules.commandAccepted, "Clear command issued; allow time to comply");
            return true;
        }
        public void InspectForWeapons(float seconds, bool flashlightOn)
        {
            if (State != SuspectState.HandsUp) return;
            if (!flashlightOn || Time.time - handsUpAt < .65f) { weaponCheck = 0; return; }
            weaponCheck += Mathf.Max(0, seconds);
            if (weaponCheck < inspectionSeconds) return;
            State = SuspectState.VerifiedUnarmed;
            score.Record("weapon-inspection", 0, "Visual check completed: no weapon found");
        }
        public void ResetWeaponInspection() { if (State == SuspectState.HandsUp) weaponCheck = 0; }
        public void ConfigureUnarmedEncounter() { if (weapon) weapon.SetActive(false); }
        void Update()
        {
            if (State == SuspectState.Considering && Time.time >= respondsAt)
            {
                State = SuspectState.Surrendered;
                if (weapon) weapon.SetActive(false);
            }
            bool handsUp = State == SuspectState.HandsUp || State == SuspectState.VerifiedUnarmed || State == SuspectState.Surrendered || State == SuspectState.Restrained;
            if (leftArm) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.Euler(0, 0, handsUp ? -145 : -12), Time.deltaTime * 5);
            if (rightArm) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.Euler(0, 0, handsUp ? 145 : 25), Time.deltaTime * 5);
        }
        public void Restrain()
        {
            if (State != SuspectState.Surrendered) return;
            State = SuspectState.Restrained;
            score.Record("restraint", score.Rules.restraint, "Suspect restrained without injury");
        }
        public void Hit(bool lethal, bool precise)
        {
            if (Resolved) return;
            // Carrying a weapon alone is not an authored immediate threat in this tutorial.
            if (lethal)
            {
                score.Record("unjustified-shot", score.Rules.unjustifiedShot, "Unjustified firearm use; no immediate attack");
                State = SuspectState.Down;
            }
            else
            {
                bool complying = State == SuspectState.Surrendered || State == SuspectState.Considering || State == SuspectState.HandsUp || State == SuspectState.VerifiedUnarmed;
                if (complying)
                    score.Record("unnecessary-force", score.Rules.unnecessaryForce, "Force used while the suspect was complying");
                score.Record("stun", score.Rules.nonLethal, "Non-lethal neutralization");
                if (precise && !complying) score.Record("precise-stun", score.Rules.preciseNonLethal, "Precise non-lethal hit");
                State = SuspectState.Stunned;
            }
            if (weapon) weapon.SetActive(false);
            if (!useAnimatedFall) transform.rotation *= Quaternion.Euler(0, 0, 75);
        }
    }
}
