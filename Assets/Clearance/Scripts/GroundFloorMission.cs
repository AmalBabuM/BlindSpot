using UnityEngine;

namespace Clearance
{
    public sealed class GroundFloorMission : MonoBehaviour
    {
        [SerializeField] FirstPersonOfficer officer;
        [SerializeField] OfficerActions actions;
        [SerializeField] DecisionScore score;
        [SerializeField] SuspectController suspect;
        [SerializeField] ObservationPoint[] corners;
        [SerializeField] Bounds room = new Bounds(new Vector3(-12, 1, 4), new Vector3(5, 3, 5));
        [SerializeField] OfficeEncounter office;
        public OfficeEncounter Office => office;
        public void ConfigureOffice(OfficeEncounter value) => office = value;
        bool entered;
        public bool Complete { get; private set; }
        public int CornersChecked { get { int n = 0; foreach (var c in corners) if (c.Checked) n++; return n; } }
        public ObservationPoint[] Corners => corners;
        public SuspectController Suspect => suspect;
        public void Configure(FirstPersonOfficer p, OfficerActions a, DecisionScore s, SuspectController target, ObservationPoint[] points, Bounds bounds)
        { officer = p; actions = a; score = s; suspect = target; corners = points; room = bounds; }
        void Update()
        {
            if (Complete || !officer.HasControl || office && office.Failed) return;
            foreach (var corner in corners) corner.Observe(officer.View, score, actions.FlashlightOn);
            if (!office && !entered && room.Contains(officer.transform.position + Vector3.up))
            {
                entered = true;
                if (CornersChecked != corners.Length) score.Record("entry", score.Rules.recklessEntry, "Entered before checking both corners");
            }
        }
        public string TrySecure()
        {
            if (office) return office.Objective;
            if (Complete) return "Room already secure.";
            if (!room.Contains(officer.transform.position + Vector3.up)) return "Enter the marked room to confirm clearance.";
            if (CornersChecked != corners.Length) return "Check both marked corners with the flashlight before securing.";
            if (!suspect.Resolved) return "Resolve the suspect before securing the room.";
            score.Record("room-secured", score.Rules.roomSecured, "Ground-floor room secured");
            Complete = true;
            officer.SetCursor(false);
            return "Ground floor complete.";
        }
        void OnDrawGizmosSelected() { Gizmos.color = Color.green; Gizmos.DrawWireCube(room.center, room.size); }
    }
}
