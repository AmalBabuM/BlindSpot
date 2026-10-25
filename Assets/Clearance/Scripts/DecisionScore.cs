using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clearance
{
    public sealed class DecisionScore : MonoBehaviour
    {
        [SerializeField] ScoringRules rules;
        readonly HashSet<string> awarded = new HashSet<string>();
        readonly List<string> timeline = new List<string>();
        float started;
        public ScoringRules Rules => rules;
        public int Total { get; private set; }
        public string Latest { get; private set; } = "Observe first. Every decision matters.";
        public IReadOnlyList<string> Timeline => timeline;
        public event Action Changed;
        public event Action<int, string> Awarded;
        public void Configure(ScoringRules value) => rules = value;
        void Awake() => started = Time.time;

        public void Record(string id, int points, string reason)
        {
            if (!awarded.Add(id)) return;
            Total += points;
            Latest = $"{points:+0;-0;0}  {reason}";
            timeline.Add($"{Time.time - started:000}s   {Latest}");
            Changed?.Invoke();
            Awarded?.Invoke(points, reason);
        }
    }
}
