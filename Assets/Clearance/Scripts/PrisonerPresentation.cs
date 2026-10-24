using UnityEngine;

namespace Clearance
{
    // Gameplay owns state/movement; the Animator owns the imported character's pose.
    public sealed class PrisonerPresentation : MonoBehaviour
    {
        [SerializeField] SuspectController suspect;
        [SerializeField] Animator animator;
        SuspectPatrol patrol;
        CapsuleCollider hitCapsule;
        float handsWeight;
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int Surrender = Animator.StringToHash("Surrender");
        static readonly int Fallen = Animator.StringToHash("Fallen");
        public Animator Animator => animator;
        public bool UsesAnimator => animator && animator.runtimeAnimatorController;
        public void Configure(SuspectController value, Transform la, Transform lf, Transform lh,
            Transform ra, Transform rf, Transform rh) => suspect = value;
        public void ConfigureAnimations(SuspectController value, Animator target) { suspect = value; animator = target; }
        void Awake()
        {
            if (!suspect) suspect = GetComponentInParent<SuspectController>();
            if (!animator) animator = GetComponent<Animator>();
            patrol = suspect ? suspect.GetComponent<SuspectPatrol>() : null;
            hitCapsule = suspect ? suspect.GetComponent<CapsuleCollider>() : null;
        }
        void Update()
        {
            if (!suspect || !UsesAnimator) return;
            bool surrender = suspect.State == SuspectState.Considering || suspect.State == SuspectState.Surrendered || suspect.State == SuspectState.Restrained;
            bool fallen = suspect.State == SuspectState.Down || suspect.State == SuspectState.Stunned;
            animator.SetFloat(Speed, suspect.State == SuspectState.Patrolling && patrol ? patrol.WalkingSpeed : 0);
            animator.SetBool(Surrender, surrender);
            animator.SetBool(Fallen, fallen);
            if (fallen && hitCapsule) hitCapsule.enabled = false;
        }
        void LateUpdate()
        {
            if (!UsesAnimator || !animator.isHuman || !hitCapsule || !hitCapsule.enabled) return;
            bool handsUp = suspect.State == SuspectState.HandsUp || suspect.State == SuspectState.VerifiedUnarmed;
            handsWeight = Mathf.MoveTowards(handsWeight, handsUp ? 1 : 0, Time.deltaTime * 3);
            if (handsWeight > 0)
            {
                RaiseArm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(-.75f, .65f, 0));
                RaiseArm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(.75f, .65f, 0));
                RaiseArm(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, Vector3.up);
                RaiseArm(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, Vector3.up);
            }
            bool kneeling = suspect.State == SuspectState.Considering || suspect.State == SuspectState.Surrendered || suspect.State == SuspectState.Restrained;
            if (!kneeling) { hitCapsule.height = 1.8f; hitCapsule.center = Vector3.up * .9f; return; }
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (!head) return;
            float height = Mathf.Clamp(suspect.transform.InverseTransformPoint(head.position).y + .2f, .85f, 1.9f);
            hitCapsule.height = height;
            hitCapsule.center = Vector3.up * height * .5f;
        }
        void RaiseArm(HumanBodyBones boneId, HumanBodyBones childId, Vector3 direction)
        {
            var bone = animator.GetBoneTransform(boneId); var child = animator.GetBoneTransform(childId);
            if (!bone || !child) return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < .000001f) return;
            Quaternion target = Quaternion.FromToRotation(current, suspect.transform.TransformDirection(direction)) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, target, handsWeight);
        }
    }
}
