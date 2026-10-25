using UnityEngine;
using System.Collections.Generic;

namespace Clearance
{
    public sealed class ObservationPoint : MonoBehaviour
    {
        [SerializeField] string label = "Corner";
        [SerializeField] float dwellSeconds = .7f;
        [SerializeField] float maximumDistance = 7;
        float dwell;
        readonly Dictionary<Camera, float> observerDwell = new Dictionary<Camera, float>();
        readonly RaycastHit[] visibilityHits = new RaycastHit[32];
        public bool Checked { get; private set; }
        public string Label => label;
        public float Progress => Mathf.Clamp01(dwell / dwellSeconds);
        public bool IsScanning => !Checked && dwell > 0;
        public float CompletedAt { get; private set; } = -100;
        public float ProgressFor(Camera observer) => observerDwell.TryGetValue(observer, out float value) ? Mathf.Clamp01(value / dwellSeconds) : 0;
        public void Configure(string value) => label = value;
        public void Observe(Camera view, DecisionScore score, bool flashlightOn, Transform observerRoot = null)
        {
            if (Checked) return;
            Vector3 delta = transform.position - view.transform.position;
            bool visible = delta.magnitude <= maximumDistance && Vector3.Angle(view.transform.forward, delta) < 15;
            if (visible)
            {
                int count = Physics.RaycastNonAlloc(view.transform.position, delta.normalized, visibilityHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                // A full buffer may omit an occluder; require another clear observation.
                if (count == visibilityHits.Length) visible = false;
                for (int i = 0; visible && i < count; i++)
                {
                    var hit = visibilityHits[i];
                    if ((!observerRoot || !hit.transform.IsChildOf(observerRoot)) && hit.distance < delta.magnitude - .25f) visible = false;
                }
            }
            observerDwell.TryGetValue(view, out float elapsed);
            observerDwell[view] = visible && flashlightOn ? elapsed + Time.deltaTime : 0;
            dwell = 0;
            foreach (float value in observerDwell.Values) dwell = Mathf.Max(dwell, value);
            if (observerDwell[view] < dwellSeconds) return;
            Checked = true;
            CompletedAt = Time.unscaledTime;
            score.Record(name, score.Rules.cornerChecked, label + " checked with flashlight");
        }
        public void StopObserving(Camera view)
        {
            observerDwell.Remove(view); dwell = 0;
            foreach (float value in observerDwell.Values) dwell = Mathf.Max(dwell, value);
        }
        void OnDrawGizmos() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, .2f); }
    }
}
