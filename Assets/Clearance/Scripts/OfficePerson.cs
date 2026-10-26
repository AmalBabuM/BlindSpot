using UnityEngine;
using UnityEngine.AI;

namespace Clearance
{
    public sealed class OfficePerson : MonoBehaviour
    {
        [SerializeField] OfficeEncounter encounter;
        [SerializeField] bool civilian;
        [SerializeField] Animator animator;
        [SerializeField] string identityEvidence;
        float inspection, commandedAt, handsWeight;
        NavMeshAgent escortAgent;
        CapsuleCollider hitCapsule;
        public bool IsEscorted { get; private set; }
        public bool IsKneeling => civilian ? !IsEscorted : !encounter.SuspectRevealed;
        public bool HandsRaised { get; private set; }
        public bool Identified { get; private set; }
        public bool Harmed { get; private set; }
        public bool IsCivilian => civilian;
        public Animator Animator => animator;
        string cachedStatus;
        float nextStatus;
        public string Status
        {
            get { if (cachedStatus == null || Time.unscaledTime >= nextStatus) { nextStatus = Time.unscaledTime + .15f; cachedStatus = BuildStatus(); } return cachedStatus; }
        }
        string BuildStatus() => Harmed ? "Person injured" : !encounter.SuspectRevealed ? "Person sheltering behind reception" :
            !HandsRaised ? "Unidentified person | Q: Both of you, hands up!" :
            !Identified ? $"Unidentified | Inspect with flashlight nearby ({Mathf.Clamp01(inspection / 1.25f):P0})" :
            civilian ? "IDENTIFIED: Office admin / Civilian - protect him" : !encounter.AllIdentified ?
            "IDENTIFIED: Suspect / Identify the other person first" : encounter.Suspect.Status;
        public void Configure(OfficeEncounter office, bool isCivilian, Animator animation, string evidence)
        { encounter = office; civilian = isCivilian; animator = animation; identityEvidence = evidence; }
        void Start()
        {
            hitCapsule = GetComponent<CapsuleCollider>();
            if (!animator) return;
            animator.SetBool("Surrender", true);
            animator.Play("Kneel", 0, 1);
            animator.Update(0);
        }
        public void StandFromCover()
        {
            if (!animator || Harmed) return;
            animator.SetBool("Surrender", false);
            animator.CrossFadeInFixedTime("Idle", .35f);
        }
        public void BeginEscort(NavMeshAgent movement)
        {
            escortAgent = movement; IsEscorted = true;
            if (!animator) return;
            animator.SetBool("Surrender", false);
            animator.SetBool("Escorted", true);
            animator.CrossFadeInFixedTime("Idle", .35f);
        }
        public void RaiseHands()
        {
            if (HandsRaised || Harmed) return;
            HandsRaised = true; commandedAt = Time.time;
            if (!civilian) encounter.Suspect.Command();
        }
        public void Inspect(float seconds, bool flashlightOn)
        {
            if (!HandsRaised || Identified || Harmed) return;
            if (!flashlightOn || Time.time - commandedAt < .65f) { inspection = 0; return; }
            inspection += Mathf.Max(0, seconds);
            if (inspection < 1.25f) return;
            Identified = true;
            if (!civilian) encounter.Suspect.InspectForWeapons(2, true);
            encounter.Score.Record(civilian ? "identity-admin" : "identity-suspect", 50, identityEvidence);
        }
        public void ResetInspection() { if (!Identified) inspection = 0; }
        public void ReceiveHit(bool lethal, bool precise)
        {
            if (civilian)
            {
                if (Harmed) return;
                Harmed = true;
                encounter.Score.Record("civilian-hit", -500, "Civilian injured: identity and restraint procedure violated");
                if (animator) animator.SetBool("Fallen", true);
                foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            }
            else encounter.Suspect.Hit(lethal, precise);
        }
        void Update()
        {
            if (!animator) return;
            if (!civilian)
            {
                if (encounter.SuspectRevealed && !HandsRaised && !Harmed)
                {
                    Vector3 towardPlayer = encounter.Player.transform.position - transform.position; towardPlayer.y = 0;
                    if (towardPlayer.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(towardPlayer), 140 * Time.deltaTime);
                }
                return;
            }
            animator.SetFloat("Speed", escortAgent && escortAgent.enabled && escortAgent.isOnNavMesh ? escortAgent.velocity.magnitude : 0);
            animator.SetBool("Surrender", IsKneeling); animator.SetBool("Fallen", Harmed);
            if (HandsRaised && !Identified && !Harmed && !IsEscorted)
            {
                Vector3 look = encounter.Player.transform.position - transform.position; look.y = 0;
                if (look.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 140 * Time.deltaTime);
            }
        }
        void LateUpdate()
        {
            if (!civilian || !animator || !animator.isHuman || Harmed) return;
            if (hitCapsule)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                float height = IsKneeling && head ? Mathf.Clamp(transform.InverseTransformPoint(head.position).y + .2f, .85f, 1.9f) : 1.8f;
                hitCapsule.height = height; hitCapsule.center = Vector3.up * height * .5f;
            }
            handsWeight = Mathf.MoveTowards(handsWeight, HandsRaised && !Identified ? 1 : 0, Time.deltaTime * 3);
            if (handsWeight <= 0) return;
            Raise(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(-.75f, .65f, 0));
            Raise(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(.75f, .65f, 0));
            Raise(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, Vector3.up);
            Raise(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, Vector3.up);
        }
        void Raise(HumanBodyBones boneId, HumanBodyBones childId, Vector3 direction)
        {
            var bone = animator.GetBoneTransform(boneId); var child = animator.GetBoneTransform(childId);
            if (!bone || !child) return;
            var target = Quaternion.FromToRotation(child.position - bone.position, transform.TransformDirection(direction)) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, target, handsWeight);
        }
    }
}
