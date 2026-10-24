using UnityEngine;

namespace Clearance
{
    public sealed class ObservationPoint : MonoBehaviour
    {
        [SerializeField] string label = "Corner";
        [SerializeField] float dwellSeconds = .7f;
        [SerializeField] float maximumDistance = 7;
        float dwell;
        public bool Checked { get; private set; }
        public string Label => label;
        public float Progress => Mathf.Clamp01(dwell / dwellSeconds);
        public void Configure(string value) => label = value;
        public void Observe(Camera view, DecisionScore score, bool flashlightOn)
        {
            if (Checked) return;
            Vector3 delta = transform.position - view.transform.position;
            bool visible = delta.magnitude <= maximumDistance && Vector3.Angle(view.transform.forward, delta) < 15;
            if (visible && Physics.Raycast(view.transform.position, delta.normalized, out var hit, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                visible = hit.distance >= delta.magnitude - .25f;
            dwell = visible && flashlightOn ? dwell + Time.deltaTime : 0;
            if (dwell < dwellSeconds) return;
            Checked = true;
            score.Record(name, score.Rules.cornerChecked, label + " checked with flashlight");
        }
        void OnDrawGizmos() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, .2f); }
    }
}
