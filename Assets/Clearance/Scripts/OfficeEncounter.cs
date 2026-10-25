using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Clearance
{
    public sealed class OfficeEncounter : MonoBehaviour
    {
        [SerializeField] FirstPersonOfficer player;
        [SerializeField] SuspectController suspect;
        [SerializeField] DecisionScore score;
        [SerializeField] OfficePerson[] people;
        [SerializeField] OfficeEscort escort;
        [SerializeField] Transform custodyPoint, nextFloor;
        [SerializeField] Transform receptionDesk;
        [SerializeField] float revealDistance = 3;
        Bounds deskBounds;
        Vector3 entryPosition;
        PrisonerPresentation suspectPresentation;
        public bool SuspectRevealed { get; private set; }
        public float RevealedAt { get; private set; }
        public FirstPersonOfficer Player => player;
        public SuspectController Suspect => suspect;
        public DecisionScore Score => score;
        public OfficePerson[] People => people;
        public OfficeEscort Escort => escort;
        public Transform CustodyPoint => custodyPoint;
        public Transform NextFloor => nextFloor;
        public bool CommandGiven { get; private set; }
        public bool AllIdentified { get { foreach (var person in people) if (!person.Identified) return false; return true; } }
        public bool Failed => people[0].Harmed || people[1].Harmed || suspect.State == SuspectState.Down || suspect.State == SuspectState.Stunned;
        public bool FailureAnimationPending => (suspect.State == SuspectState.Down || suspect.State == SuspectState.Stunned) &&
            suspectPresentation && !suspectPresentation.FailureNotificationReady;
        public string FailureReason => suspect.State == SuspectState.Down ? "You shot an unarmed suspect. Mission failed." :
            suspect.State == SuspectState.Stunned ? "You used force on an unarmed suspect. Mission failed." :
            "Someone was injured. Mission failed. Verify identities before using force.";
        public bool ReadyForNextFloor => escort.Returned && AllIdentified && !Failed;
        public string Objective => Failed ? (FailureAnimationPending ? "Wait for the situation to settle." : FailureReason) :
            !SuspectRevealed ? "Approach reception carefully while looking at the desk." :
            !CommandGiven ? "One person stood up. Q: Both of you, hands up!" : !AllIdentified ?
            "Inspect BOTH people with your flashlight from within 3.5 m." :
            suspect.State == SuspectState.VerifiedUnarmed ? "Admin is safe. Aim at identified suspect and press Q to kneel." :
            suspect.State == SuspectState.Considering ? "Suspect complying. Wait for kneeling." :
            suspect.State == SuspectState.Surrendered ? "Assistant is restraining the suspect. The admin stays kneeling." :
            !escort.CustodyComplete ? "Assistant is escorting the suspect and employee to the exit." :
            !escort.Returned ? "Suspect in custody; employee evacuated. Wait for your assistant." :
            "Office secured. Assistant ready - go through the push door to the STAIRS / NEXT FLOOR marker.";
        public void Configure(FirstPersonOfficer officer, SuspectController target, DecisionScore decisions,
            OfficePerson[] occupants, OfficeEscort assistance, Transform custody, Transform stairs)
        { player = officer; suspect = target; score = decisions; people = occupants; escort = assistance; custodyPoint = custody; nextFloor = stairs; }
        void Awake()
        {
            entryPosition = player.transform.position;
            suspectPresentation = suspect.GetComponentInChildren<PrisonerPresentation>();
            if (!receptionDesk) receptionDesk = GameObject.Find("Front Desk")?.transform;
            deskBounds = new Bounds(receptionDesk ? receptionDesk.position : suspect.transform.position, Vector3.zero);
            if (receptionDesk)
                foreach (var renderer in receptionDesk.GetComponentsInChildren<Renderer>()) deskBounds.Encapsulate(renderer.bounds);
        }
        public string Command(OfficePerson target)
        {
            if (!SuspectRevealed) return "Move closer while facing the reception desk.";
            if (!target || Vector3.Distance(player.View.transform.position, target.transform.position + Vector3.up) > 10)
                return "Aim at one of the people within command range.";
            if (!CommandGiven)
            {
                CommandGiven = true;
                foreach (var person in people) person.RaiseHands();
                return "OFFICER: Both of you, raise your hands!";
            }
            if (!AllIdentified) return "Identify both people before ordering anyone to kneel.";
            if (target.IsCivilian) return "Admin verified as a civilian. Keep him safe.";
            return suspect.Command() ? "OFFICER: Suspect, get on your knees. Surrender!" : Objective;
        }
        void Update()
        {
            if (!SuspectRevealed && !Failed)
            {
                Vector3 position = player.transform.position;
                Vector3 nearest = deskBounds.ClosestPoint(position); nearest.y = position.y;
                Vector3 towardDesk = deskBounds.center - player.View.transform.position; towardDesk.y = 0;
                Vector3 facing = player.View.transform.forward; facing.y = 0;
                Vector3 moved = position - entryPosition; moved.y = 0;
                if (moved.sqrMagnitude > .36f && Vector3.Distance(position, nearest) <= revealDistance &&
                    Vector3.Dot(facing.normalized, towardDesk.normalized) > .82f)
                {
                    SuspectRevealed = true;
                    RevealedAt = Time.time;
                    foreach (var person in people) if (!person.IsCivilian) person.StandFromCover();
                }
            }
            if (ReadyForNextFloor) score.Record("room-secured", score.Rules.roomSecured, "Admin protected; suspect in custody; assistant returned - proceed upstairs");
            if (Failed && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        void OnGUI()
        {
            if (!ReadyForNextFloor || player.InteractionBlocked || Event.current.type != EventType.Repaint) return;
            var point = player.View.WorldToViewportPoint(nextFloor.position + Vector3.up * 1.5f);
            if (point.z <= 0) return;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.yellow;
            GUI.Label(new Rect(Mathf.Clamp(point.x * Screen.width - 100, 10, Screen.width - 220),
                Mathf.Clamp((1 - point.y) * Screen.height, 150, Screen.height - 150), 240, 45),
                "STAIRS / NEXT FLOOR\n" + Vector3.Distance(player.transform.position, nextFloor.position).ToString("F0") + " m");
            GUI.color = Color.white;
        }
    }
}
