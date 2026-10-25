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
        GUIStyle titleStyle, bodyStyle;
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
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            GUI.color = new Color(.025f, .04f, .055f, .98f);
            GUI.DrawTexture(new Rect(335, 210, 610, 280), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
                bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            }
            GUI.Label(new Rect(360, 232, 560, 45), warningDialog ? "LIFT UNSAFE" : "USE ELEVATOR", titleStyle);
            GUI.Label(new Rect(360, 291, 555, 90), warningDialog ? warning : "Choose how to continue to the next floor.", bodyStyle);
            if (warningDialog)
            {
                if (GUI.Button(new Rect(360, 407, 555, 48), "Understood - use the stairs")) Dismiss();
            }
            else
            {
                if (GUI.Button(new Rect(360, 407, 265, 48), "Use lift / Go to upper floor")) ChooseUpperFloor();
                if (GUI.Button(new Rect(650, 407, 265, 48), "Stay on this floor")) Dismiss();
            }
        }
    }
}
