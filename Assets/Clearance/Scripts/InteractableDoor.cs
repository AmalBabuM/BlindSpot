using UnityEngine;

namespace Clearance
{
    public sealed class InteractableDoor : MonoBehaviour
    {
        [SerializeField] float openAngle = 95;
        Quaternion closed;
        bool open;
        public bool IsPassable => Quaternion.Angle(closed, transform.localRotation) >= 65;
        public void Open() => open = true;
        void Awake() => closed = transform.localRotation;
        public void Toggle() => open = !open;
        void Update() => transform.localRotation = Quaternion.RotateTowards(transform.localRotation,
            closed * Quaternion.Euler(0, open ? openAngle : 0, 0), 120 * Time.deltaTime);
    }
}
