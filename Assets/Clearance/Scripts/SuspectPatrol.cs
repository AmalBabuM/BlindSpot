using UnityEngine;

namespace Clearance
{
    [RequireComponent(typeof(SuspectController))]
    public sealed class SuspectPatrol : MonoBehaviour
    {
        [SerializeField] Transform pointA, pointB;
        [SerializeField] FirstPersonOfficer officer;
        [SerializeField, Min(.1f)] float speed = .9f;
        [SerializeField, Min(.1f)] float alertDistance = 4.2f;
        [SerializeField] float turnSpeed = 240;
        [SerializeField] float endpointPause = .6f;
        SuspectController suspect;
        bool towardB;
        float waitUntil;
        public float WalkingSpeed { get; private set; }
        public float AlertDistance => alertDistance;
        public Transform PointA => pointA;
        public Transform PointB => pointB;
        public void Configure(Transform a, Transform b, FirstPersonOfficer player) { pointA = a; pointB = b; officer = player; }
        void Awake() => suspect = GetComponent<SuspectController>();
        void Start() => suspect.BeginPatrol();
        public bool CanSeeOfficer()
        {
            if (!officer || Vector3.Distance(transform.position, officer.transform.position) > alertDistance) return false;
            Vector3 from = transform.position + Vector3.up * 1.5f;
            Vector3 to = officer.View.transform.position;
            Vector3 direction = (to - from).normalized;
            // Start outside our own hit capsule; walls and closed doors block detection.
            return !Physics.Linecast(from + direction * .4f, to, out var hit, ~0, QueryTriggerInteraction.Ignore)
                || hit.transform.IsChildOf(officer.transform);
        }
        void Update()
        {
            WalkingSpeed = 0;
            if (!pointA || !pointB || !officer || suspect.Resolved) return;
            if (suspect.State == SuspectState.Patrolling && CanSeeOfficer()) suspect.Alert();
            if (suspect.State == SuspectState.Alerted || suspect.State == SuspectState.Considering || suspect.State == SuspectState.Surrendered || suspect.State == SuspectState.HandsUp || suspect.State == SuspectState.VerifiedUnarmed)
            { Face(officer.transform.position - transform.position); return; }
            if (suspect.State != SuspectState.Patrolling || Time.time < waitUntil) return;
            Vector3 target = (towardB ? pointB : pointA).position;
            target.y = transform.position.y;
            Vector3 delta = target - transform.position;
            if (delta.magnitude < .08f)
            { towardB = !towardB; waitUntil = Time.time + endpointPause; return; }
            Face(delta);
            if (Vector3.Angle(transform.forward, delta) > 12) return;
            Vector3 step = Vector3.ClampMagnitude(delta, speed * Time.deltaTime);
            var obstacles = Physics.CapsuleCastAll(transform.position + Vector3.up * .4f,
                transform.position + Vector3.up * 1.45f, .28f, step.normalized, step.magnitude + .04f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var obstacle in obstacles)
                if (!obstacle.transform.IsChildOf(transform)) return;
            transform.position += step;
            WalkingSpeed = step.magnitude / Mathf.Max(Time.deltaTime, .0001f);
        }
        void Face(Vector3 delta)
        {
            delta.y = 0;
            if (delta.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), turnSpeed * Time.deltaTime);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, alertDistance);
            if (pointA && pointB) { Gizmos.color = Color.cyan; Gizmos.DrawLine(pointA.position, pointB.position); }
        }
    }
}
