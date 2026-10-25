using GreyDoorGames;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clearance
{
    public sealed class UnsafeElevator : MonoBehaviour
    {
        [SerializeField] ElevatorDoorAnimation importedAnimation;
        [SerializeField] RectTransform animatedLeft, animatedRight;
        [SerializeField] Transform leftPanel, rightPanel, callPoint;
        [SerializeField] float interactionRange = 3;
        [SerializeField] float panelTravel = .95f;
        [SerializeField] string warning = "Using the lift is not safe. Use the stairs to reach the upper floor.";
        FirstPersonOfficer player;
        Vector3 leftClosed, rightClosed;
        bool choosing, warningDialog;
        public bool IsOpen { get; private set; }
        public int OpenCount { get; private set; }
        public bool IsDialogOpen => choosing || warningDialog;
        public string Warning => warning;
        public Transform CallPoint => callPoint;
        public Transform LeftPanel => leftPanel;
        public Transform RightPanel => rightPanel;
        public bool TravelAllowed => false;
        public void Configure(ElevatorDoorAnimation animation, RectTransform left, RectTransform right,
            Transform panelLeft, Transform panelRight, Transform button)
        { importedAnimation = animation; animatedLeft = left; animatedRight = right; leftPanel = panelLeft; rightPanel = panelRight; callPoint = button; }
        void Awake() { leftClosed = leftPanel.localPosition; rightClosed = rightPanel.localPosition; }
        public bool ShowFloorChoice(FirstPersonOfficer officer)
        {
            if (!officer || Vector3.Distance(officer.View.transform.position, callPoint.position) > interactionRange) return false;
            player = officer; choosing = true; warningDialog = false;
            player.SetInteractionBlocked(true);
            return true;
        }
        public void ChooseUpperFloor()
        {
            if (!choosing) return;
            choosing = false; warningDialog = true;
            if (IsOpen) return;
            IsOpen = true; OpenCount++;
            importedAnimation.PlayAnimation("OpenDoor", true);
        }
        public void Dismiss()
        {
            choosing = false; warningDialog = false;
            if (player) player.SetInteractionBlocked(false);
        }
        void Update()
        {
            if (IsDialogOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Dismiss();
        }
        void LateUpdate()
        {
            // The source clip animates UI coordinates; map that motion onto real mesh panels.
            float left = Mathf.InverseLerp(-3.1f, -62.7f, animatedLeft.anchoredPosition.x);
            float right = Mathf.InverseLerp(-57.9f, 0, animatedRight.anchoredPosition.x);
            leftPanel.localPosition = leftClosed + Vector3.left * panelTravel * left;
            rightPanel.localPosition = rightClosed + Vector3.right * panelTravel * right;
        }
        void OnDisable() { if (player && IsDialogOpen) player.SetInteractionBlocked(false); }
        void OnGUI()
        {
            if (!IsDialogOpen) return;
            FieldUi.Prepare();
            var previous = GUI.matrix;
            float scale = Screen.height / 720f;
            float x = Screen.width / scale * .5f - 290;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            FieldUi.Panel(new Rect(x, 222, 580, 270));
            GUI.Label(new Rect(x + 28, 242, 524, 20), "FLOOR ACCESS", FieldUi.Eyebrow);
            GUI.Label(new Rect(x + 28, 270, 524, 36), warningDialog ? "LIFT UNSAFE" : "USE ELEVATOR", FieldUi.Title);
            GUI.Label(new Rect(x + 28, 318, 524, 94), warningDialog ? warning : "Choose how to continue to the next floor.", FieldUi.Body);
            if (warningDialog)
            {
                if (FieldUi.Button(new Rect(x + 28, 426, 524, 44), "Understood - use the stairs", true)) Dismiss();
            }
            else
            {
                if (FieldUi.Button(new Rect(x + 28, 426, 250, 44), "Use lift / Upper floor", true)) ChooseUpperFloor();
                if (FieldUi.Button(new Rect(x + 300, 426, 252, 44), "Stay on this floor")) Dismiss();
            }
            GUI.matrix = previous;
        }
    }
}
