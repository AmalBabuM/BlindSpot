using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clearance
{
    public sealed class MissionHud : MonoBehaviour
    {
        [SerializeField] GroundFloorMission mission;
        [SerializeField] DecisionScore score;
        [SerializeField] FirstPersonOfficer officer;
        [SerializeField] OfficerActions actions;
        [SerializeField] SquadCoordinator squad;
        OfficeGuidance guidance;
        struct Toast { public string title, reason; public bool good; }
        readonly Queue<Toast> toasts = new Queue<Toast>(6);
        Toast toast;
        float toastUntil;
        bool hasToast, controlsOwnBlock;
        string scoreText;
        public bool ControlsOpen { get; private set; }
        public OfficeGuidance Guidance => guidance;
        public bool ToastVisible => hasToast;
        public void ConfigureSquad(SquadCoordinator value) => squad = value;
        public void Configure(GroundFloorMission m, DecisionScore s, FirstPersonOfficer p, OfficerActions a)
        { mission = m; score = s; officer = p; actions = a; }
        void Awake()
        {
            if (mission && mission.Office)
            {
                guidance = GetComponent<OfficeGuidance>();
                if (!guidance) guidance = gameObject.AddComponent<OfficeGuidance>();
                guidance.Configure(mission.Office, actions);
            }
        }
        void OnEnable()
        {
            if (score) { score.Awarded += Awarded; scoreText = score.Total.ToString(); }
        }
        void OnDisable()
        {
            if (score) score.Awarded -= Awarded;
            if (ControlsOpen) ToggleControls();
        }
        void Awarded(int points, string reason)
        {
            scoreText = score.Total.ToString();
            if (points == 0) return;
            if (toasts.Count >= 6) toasts.Dequeue();
            toasts.Enqueue(new Toast { title = points > 0 ? "GOOD DECISION   +" + points : "PROCEDURE ALERT   " + points,
                reason = reason, good = points > 0 });
        }
        void Update()
        {
            if (hasToast && Time.unscaledTime >= toastUntil) hasToast = false;
            if (!hasToast && toasts.Count > 0)
            { toast = toasts.Dequeue(); hasToast = true; toastUntil = Time.unscaledTime + 3.2f; }
        }
        public void ToggleControls()
        {
            ControlsOpen = !ControlsOpen;
            if (ControlsOpen)
            {
                controlsOwnBlock = !officer.InteractionBlocked;
                if (controlsOwnBlock) officer.SetInteractionBlocked(true);
            }
            else
            {
                if (controlsOwnBlock) officer.SetInteractionBlocked(false);
                controlsOwnBlock = false;
            }
        }
        void OnGUI()
        {
            // IMGUI keyboard events remain available while a decision pauses physics.
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.I) { ToggleControls(); Event.current.Use(); }
                else if (ControlsOpen && Event.current.keyCode == KeyCode.Escape) { ToggleControls(); Event.current.Use(); }
                else if (guidance && guidance.DarkArrival && Event.current.keyCode == KeyCode.F)
                { guidance.TurnOnFlashlight(); Event.current.Use(); }
            }
            FieldUi.Prepare();
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            float scale = Screen.height / 720f;
            float width = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.depth = -20; GUI.color = Color.white;
            if (guidance && guidance.DarkArrival)
                GUI.DrawTexture(new Rect(0, 0, width, 720), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .97f), 0, 0);
            FieldUi.Panel(new Rect(width - 172, 20, 152, 62));
            GUI.Label(new Rect(width - 154, 28, 120, 18), "DECISION POINTS", FieldUi.Eyebrow);
            GUI.Label(new Rect(width - 154, 45, 120, 32), scoreText ?? "0", FieldUi.Title);
            if (hasToast)
            {
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((toastUntil - Time.unscaledTime) / .4f));
                float toastWidth = Mathf.Min(420, width - 390);
                float x = (width - toastWidth) * .5f;
                FieldUi.Panel(new Rect(x, 20, toastWidth, 76));
                FieldUi.Rounded(new Rect(x + 12, 34, 3, 46), toast.good ? FieldUi.Success : new Color(1, .55f, .4f), 1);
                GUI.Label(new Rect(x + 25, 29, toastWidth - 40, 22), toast.title, FieldUi.Eyebrow);
                GUI.Label(new Rect(x + 25, 51, toastWidth - 40, 39), toast.reason, FieldUi.Small);
                GUI.color = Color.white;
            }
            if ((!guidance || !guidance.CardOpen) && officer.HasControl)
            {
                FieldUi.Rounded(new Rect(width * .5f - 2, 358, 4, 4), Color.white, 2);
                if (!string.IsNullOrEmpty(actions.Prompt))
                {
                    float promptWidth = Mathf.Min(660, width - 160);
                    float x = (width - promptWidth) * .5f;
                    FieldUi.Panel(new Rect(x, 606, promptWidth, 42));
                    GUI.Label(new Rect(x + 15, 615, promptWidth - 30, 28), actions.Prompt, FieldUi.Small);
                }
            }
            if (guidance && guidance.CardOpen) DrawInstruction(width);
            else if (mission.Complete)
            {
                FieldUi.Panel(new Rect(width * .5f - 250, 260, 500, 160));
                GUI.Label(new Rect(width * .5f - 220, 284, 440, 38), "GROUND FLOOR SECURED", FieldUi.Title);
                if (FieldUi.Button(new Rect(width * .5f - 220, 350, 440, 44), "Play again", true))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            else if (!officer.HasControl && !officer.InteractionBlocked)
            {
                if (FieldUi.Button(new Rect(width * .5f - 130, 335, 260, 48), "Resume patrol", true)) officer.SetCursor(true);
            }
            if (FieldUi.Button(new Rect(20, 656, 44, 44), "i")) ToggleControls();
            if (ControlsOpen) DrawControls(width);
            GUI.matrix = previous; GUI.color = previousColor; GUI.depth = previousDepth;
        }
        void DrawInstruction(float width)
        {
            float cardWidth = Mathf.Min(580, width - 40);
            float x = (width - cardWidth) * .5f;
            FieldUi.Panel(new Rect(x, 222, cardWidth, 270));
            GUI.Label(new Rect(x + 28, 242, cardWidth - 56, 20), "FIELD BRIEFING", FieldUi.Eyebrow);
            GUI.Label(new Rect(x + 28, 270, cardWidth - 56, 36), guidance.Title, FieldUi.Title);
            GUI.Label(new Rect(x + 28, 318, cardWidth - 56, 94), guidance.Instruction, FieldUi.Body);
            if (FieldUi.Button(new Rect(x + 28, 426, cardWidth - 228, 44), guidance.PrimaryChoice, true)) guidance.Continue();
            if (FieldUi.Button(new Rect(x + cardWidth - 184, 426, 156, 44), guidance.SecondaryChoice)) ToggleControls();
        }
        void DrawControls(float width)
        {
            float drawerWidth = Mathf.Min(960, width - 40);
            FieldUi.Panel(new Rect(20, 500, drawerWidth, 140));
            GUI.Label(new Rect(42, 516, drawerWidth - 100, 22), "FIELD CONTROLS", FieldUi.Eyebrow);
            if (FieldUi.Button(new Rect(drawerWidth - 30, 511, 36, 30), "X")) ToggleControls();
            float column = (drawerWidth - 44) / 3;
            GUI.Label(new Rect(42, 550, column, 80), "WASD  Move\nMouse  Look around\nShift  Move carefully", FieldUi.Body);
            GUI.Label(new Rect(42 + column, 550, column, 80), "F  Flashlight\nQ  Voice command\nE  Interact / push door", FieldUi.Body);
            GUI.Label(new Rect(42 + column * 2, 550, column, 80), "1 / 2  Stun / firearm\nLMB  Use selected tool\nI  Controls   /   Esc  Cursor", FieldUi.Body);
        }
    }
}
