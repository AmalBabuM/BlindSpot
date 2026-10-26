using UnityEngine;

namespace Clearance
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PushDoor : MonoBehaviour
    {
        [SerializeField] float pushSpeed = 38;
        [SerializeField] float damping = 35;
        [SerializeField, Range(1, 90)] float maximumAngle = 85;
        Rigidbody body;
        Quaternion closed;
        float angle, velocity;
        bool careful;
        float carefulTarget;
        public StairEncounter StairEncounter { get; set; }
        public float OpeningAngle => Mathf.Abs(angle);
        public void OpenCarefully(Vector3 from)
        {
            careful = true;
            var collider = GetComponentInChildren<Collider>();
            Vector3 point = collider ? collider.bounds.center : transform.position + transform.right;
            Vector3 direction = point - from; direction.y = 0;
            float torque = Vector3.Dot(Vector3.Cross(point - transform.position, direction), Vector3.up);
            carefulTarget = (torque < 0 ? -1 : 1) * maximumAngle;
            velocity = 0;
        }
        public bool IsPassable => Mathf.Abs(angle) > 55;
        void Awake() { body = GetComponent<Rigidbody>(); closed = transform.localRotation; }
        public void Push(Vector3 point, Vector3 direction)
        {
            if (careful) return;
            direction.y = 0;
            float torque = Vector3.Dot(Vector3.Cross(point - transform.position, direction), Vector3.up);
            if (Mathf.Abs(torque) > .01f) velocity = Mathf.Clamp(velocity + Mathf.Sign(torque) * pushSpeed, -100, 100);
        }
        void FixedUpdate()
        {
            if (careful)
            {
                angle = Mathf.MoveTowards(angle, carefulTarget, 25 * Time.fixedDeltaTime);
                Quaternion rotation = closed * Quaternion.Euler(0, angle, 0);
                body.MoveRotation(transform.parent ? transform.parent.rotation * rotation : rotation);
                return;
            }
            if (Mathf.Abs(velocity) < .0001f) return;
            angle = Mathf.Clamp(angle + velocity * Time.fixedDeltaTime, -maximumAngle, maximumAngle);
            velocity = Mathf.MoveTowards(velocity, 0, damping * Time.fixedDeltaTime);
            if (Mathf.Abs(angle) >= maximumAngle) velocity = 0;
            Quaternion local = closed * Quaternion.Euler(0, angle, 0);
            body.MoveRotation(transform.parent ? transform.parent.rotation * local : local);
        }
    }
}
