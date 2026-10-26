using UnityEngine;
using UnityEngine.AI;

namespace Clearance
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class AssistingOfficer : MonoBehaviour
    {
        [SerializeField] SquadCoordinator squad;
        [SerializeField] int slot;
        [SerializeField] Camera observationView;
        [SerializeField] Light flashlight;
        [SerializeField] Animator animator;
        [SerializeField] float sideSpacing = .85f;
        [SerializeField] float followDistance = .8f;
        [SerializeField] float followStartDistance = 3.2f;
        [SerializeField] float followStopDistance = 1.9f;
        [SerializeField] float followSpeed = 2.3f;
        [SerializeField] float carefulSpeed = .85f;
        [SerializeField] bool useTorchAnimations;
        [SerializeField] float cuffSeconds = 2.5f;
        [SerializeField] OfficeEncounter office;
        public void ConfigureOffice(OfficeEncounter value) => office = value;
        public void ResumeAfterEscort() { following = false; applyingCuffs = false; squad.ResumeFollowAfterEscort(); }
        NavMeshAgent agent;
        float nextDestinationUpdate;
        Vector3 leaderMotionSample, travelDirection;
        bool following;
        SquadOrder previousOrder;
        float carefulPose;
        int coverIndex;
        float cuffTime;
        bool applyingCuffs;
        SuspectController cuffTarget;
        readonly Collider[] nearbyDoors = new Collider[32];
        float nextDoorScan;
        public string Status { get; private set; } = "Ready";
        public bool AtCover { get; private set; }
        public NavMeshAgent Agent => agent;
        public bool IsFollowing => following;
        public float FollowStartDistance => followStartDistance;
        public float FollowStopDistance => followStopDistance;
        public bool UsesTorchAnimations => useTorchAnimations;
        public Light Flashlight => flashlight;
        public void UseTorchAnimations() => useTorchAnimations = true;
        public void Configure(SquadCoordinator team, int index, Camera view, Light light, Animator animation)
        { squad = team; slot = index; observationView = view; flashlight = light; animator = animation; }
        void Awake() => agent = GetComponent<NavMeshAgent>();
        void Start()
        {
            if (!NavMesh.SamplePosition(transform.position, out var hit, 2, NavMesh.AllAreas))
            { Status = "No walkable spawn"; return; }
            transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(hit.position);
            leaderMotionSample = squad.Leader.transform.position;
            travelDirection = squad.Leader.transform.forward;
        }
        void Update()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            if (animator) animator.SetFloat("Speed", agent.velocity.magnitude);
            var stairs = squad.Mission.Stairs;
            if (stairs && stairs.Finished) { agent.isStopped = true; applyingCuffs = false; return; }
            if (stairs && stairs.Started && stairs.CarefulEntry && office && office.Escort.Returned)
            {
                var state = stairs.Suspect.State;
                if (state == SuspectState.HandsUp || state == SuspectState.VerifiedUnarmed || state == SuspectState.Considering ||
                    state == SuspectState.Surrendered || state == SuspectState.Restrained) { SupportSurrender(stairs.Suspect); return; }
            }
            if (office && office.Escort.IsActive)
            { applyingCuffs = false; Status = office.Escort.Status; return; }
            if (!squad.ChoiceMade || squad.Mission.Complete)
            { agent.isStopped = true; Status = squad.Mission.Complete ? "Room secured" : "Awaiting your order"; return; }
            var suspectState = squad.Mission.Suspect.State;
            bool supporting = suspectState == SuspectState.HandsUp || suspectState == SuspectState.VerifiedUnarmed ||
                suspectState == SuspectState.Considering || suspectState == SuspectState.Surrendered || suspectState == SuspectState.Restrained;
            if (supporting && !(office && office.Escort.Returned)) { SupportSurrender(squad.Mission.Suspect); return; }
            applyingCuffs = false;
            bool covering = squad.Order == SquadOrder.CoverCorners;
            Vector3 movement = squad.Leader.transform.position - leaderMotionSample;
            movement.y = 0;
            if (movement.sqrMagnitude > .15f * .15f)
            {
                travelDirection = movement.normalized;
                leaderMotionSample = squad.Leader.transform.position;
            }
            float distanceToLeader = Vector3.Distance(transform.position, squad.Leader.transform.position);
            if (squad.Order != previousOrder)
            {
                // An explicit recall regroups distant officers; the initial lead choice holds nearby officers.
                following = previousOrder == SquadOrder.CoverCorners && !covering && distanceToLeader > followStopDistance;
                previousOrder = squad.Order;
            }
            agent.speed = covering ? carefulSpeed : followSpeed;
            agent.acceleration = covering ? 1.5f : 7;
            agent.angularSpeed = covering ? 110 : 240;
            agent.updateRotation = true;
            if (animator) animator.speed = useTorchAnimations ? 1 : covering ? .6f : 1;
            if (!covering)
            {
                if (!following && distanceToLeader > followStartDistance) following = true;
                if (following && distanceToLeader <= followStopDistance) following = false;
                if (!following)
                {
                    AtCover = false;
                    agent.isStopped = true;
                    agent.ResetPath();
                    agent.updateRotation = false;
                    squad.Mission.Corners[coverIndex].StopObserving(observationView);
                    Status = "Holding position / Waiting for you";
                    return;
                }
            }
            if (covering && squad.Mission.Corners[coverIndex].Checked)
                for (int i = 0; i < squad.Mission.Corners.Length; i++)
                    if (!squad.Mission.Corners[i].Checked) { coverIndex = i; break; }
            var target = covering ? squad.CoverPositions[coverIndex].position : FollowPosition();
            agent.stoppingDistance = covering ? .16f : .45f;
            agent.isStopped = false;
            if (Time.time >= nextDestinationUpdate)
            {
                nextDestinationUpdate = Time.time + .18f;
                if (NavMesh.SamplePosition(target, out var hit, .9f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            }
            OpenDoorsAhead();
            AtCover = covering && !agent.pathPending && Vector3.Distance(transform.position, target) < .45f;
            if (agent.pathStatus == NavMeshPathStatus.PathPartial || agent.pathStatus == NavMeshPathStatus.PathInvalid)
                Status = "Route blocked / Waiting";
            else if (AtCover)
            {
                agent.isStopped = true;
                var corner = squad.Mission.Corners[coverIndex];
                Vector3 direction = corner.transform.position - observationView.transform.position;
                Vector3 flat = direction; flat.y = 0;
                if (flat.sqrMagnitude > .01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 240 * Time.deltaTime);
                observationView.transform.rotation = Quaternion.LookRotation(direction);
                flashlight.transform.rotation = observationView.transform.rotation;
                corner.Observe(observationView, squad.Score, flashlight.enabled, transform);
                Status = corner.Checked ? "Corner checked / Holding cover" : "Checking corner";
            }
            else
            {
                squad.Mission.Corners[coverIndex].StopObserving(observationView);
                observationView.transform.localRotation = Quaternion.identity;
                flashlight.transform.localRotation = Quaternion.identity;
                Status = covering ? "Advancing carefully / Watching ahead" : "Catching up with you";
            }
        }
        void SupportSurrender(SuspectController suspect)
        {
            if (cuffTarget != suspect) { cuffTime = 0; applyingCuffs = false; }
            cuffTarget = suspect;
            bool kneeling = suspect.State == SuspectState.Considering || suspect.State == SuspectState.Surrendered || suspect.State == SuspectState.Restrained;
            Vector3 desired = kneeling ? suspect.transform.position - suspect.transform.forward * .75f :
                suspect.transform.position + suspect.transform.right * 1.25f - suspect.transform.forward * .25f;
            following = false; AtCover = false;
            squad.Mission.Corners[coverIndex].StopObserving(observationView);
            agent.speed = carefulSpeed; agent.acceleration = 1.5f; agent.angularSpeed = 110;
            agent.stoppingDistance = .12f; agent.updateRotation = true; agent.isStopped = false;
            if (animator) animator.speed = 1;
            if (Time.time >= nextDestinationUpdate)
            {
                nextDestinationUpdate = Time.time + .2f;
                if (NavMesh.SamplePosition(desired, out var sample, 1.2f, NavMesh.AllAreas)) agent.SetDestination(sample.position);
            }
            OpenDoorsAhead();
            bool arrived = Vector3.Distance(transform.position, agent.destination) < .4f &&
                Vector3.Distance(transform.position, suspect.transform.position) < 1.8f;
            if (arrived)
            {
                agent.isStopped = true; agent.updateRotation = false;
                Vector3 look = suspect.transform.position - transform.position; look.y = 0;
                if (look.sqrMagnitude > .001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 150 * Time.deltaTime);
                observationView.transform.LookAt(suspect.transform.position + Vector3.up * (kneeling ? 1 : 1.5f));
                applyingCuffs = suspect.State == SuspectState.Surrendered;
                if (applyingCuffs)
                {
                    cuffTime += Time.deltaTime;
                    Status = $"Applying handcuffs... {Mathf.Clamp01(cuffTime / cuffSeconds):P0}";
                    if (cuffTime >= cuffSeconds) suspect.Restrain();
                }
                else Status = suspect.State == SuspectState.Restrained ? "Suspect restrained / Standing guard" : "Beside suspect / Supporting surrender";
            }
            else
            {
                applyingCuffs = false; cuffTime = 0;
                Status = kneeling ? "Moving behind suspect to restrain" : "Approaching suspect / Covering surrender";
            }
        }
        Vector3 FollowPosition()
        {
            var player = squad.Leader.transform;
            float side = slot == 0 ? -1 : 1;
            Vector3 right = Vector3.Cross(Vector3.up, travelDirection);
            Vector3 flank = player.position + right * (side * sideSpacing) - travelDirection * Mathf.Max(1.3f, followDistance);
            if (NavMesh.SamplePosition(flank, out var sample, .6f, NavMesh.AllAreas) && Vector3.Distance(sample.position, flank) < .4f)
                return sample.position;
            // Narrow passages collapse the side-by-side formation into a short file.
            return player.position - travelDirection * (1.3f + slot * .6f);
        }
        void LateUpdate()
        {
            if (!animator || !animator.isHuman) return;
            if (flashlight) flashlight.transform.rotation = observationView.transform.rotation;
            if (applyingCuffs)
            {
                var suspectAnimator = cuffTarget ? cuffTarget.GetComponentInChildren<Animator>() : null;
                if (suspectAnimator && suspectAnimator.isHuman)
                {
                    ReachWrist(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, suspectAnimator.GetBoneTransform(HumanBodyBones.LeftHand));
                    ReachWrist(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, suspectAnimator.GetBoneTransform(HumanBodyBones.RightHand));
                    ReachWrist(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, suspectAnimator.GetBoneTransform(HumanBodyBones.LeftHand));
                    ReachWrist(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, suspectAnimator.GetBoneTransform(HumanBodyBones.RightHand));
                }
            }
            if (useTorchAnimations) return;
            bool careful = squad.ChoiceMade && squad.Order == SquadOrder.CoverCorners && !squad.Mission.Complete;
            carefulPose = Mathf.MoveTowards(carefulPose, careful ? 1 : 0, Time.deltaTime * 3);
            if (carefulPose <= 0) return;
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (spine) spine.rotation = Quaternion.AngleAxis(8 * carefulPose, transform.right) * spine.rotation;
            ReadyArm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(-.3f, -.65f, .7f));
            ReadyArm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(.25f, -.65f, .7f));
            ReadyArm(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, new Vector3(.3f, -.1f, 1));
            ReadyArm(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, new Vector3(0, -.1f, 1));
        }
        void ReachWrist(HumanBodyBones boneId, HumanBodyBones childId, Transform wrist)
        {
            var bone = animator.GetBoneTransform(boneId); var child = animator.GetBoneTransform(childId);
            if (!bone || !child || !wrist) return;
            var desired = wrist.position - bone.position;
            if (desired.sqrMagnitude < .001f) return;
            var target = Quaternion.FromToRotation(child.position - bone.position, desired) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, target, Mathf.Clamp01(cuffTime * 3));
        }
        void ReadyArm(HumanBodyBones boneId, HumanBodyBones childId, Vector3 direction)
        {
            var bone = animator.GetBoneTransform(boneId); var child = animator.GetBoneTransform(childId);
            if (!bone || !child) return;
            var target = Quaternion.FromToRotation(child.position - bone.position, transform.TransformDirection(direction)) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, target, carefulPose);
        }
        void OpenDoorsAhead()
        {
            if (Time.time < nextDoorScan) return;
            nextDoorScan = Time.time + .15f;
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, 1.7f, nearbyDoors, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = nearbyDoors[i];
                var door = collider.GetComponentInParent<InteractableDoor>();
                var pushDoor = collider.GetComponentInParent<PushDoor>();
                if (pushDoor)
                {
                    if (pushDoor.StairEncounter && !pushDoor.StairEncounter.CarefulEntry) { agent.isStopped = true; continue; }
                    pushDoor.Push(collider.bounds.center, agent.desiredVelocity);
                    if (!pushDoor.IsPassable) agent.isStopped = true;
                    continue;
                }
                if (!door) continue;
                Vector3 toDoor = collider.bounds.center - transform.position; toDoor.y = 0;
                if (Vector3.Dot(agent.desiredVelocity.normalized, toDoor.normalized) < .25f) continue;
                door.Open();
                if (!door.IsPassable) agent.isStopped = true;
            }
        }
    }
}
