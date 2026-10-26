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
        PatrolPause pause;
        struct Toast { public string title, reason; public bool good; }
        readonly Queue<Toast> toasts = new Queue<Toast>(6);
        Toast toast;
        float toastUntil;
        float toastStarted, cardOpacity;
        OfficeBriefing displayedStep;
        string cardTitle, cardInstruction, cardPrimary, cardSecondary;
        public float CardOpacity => cardOpacity;
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
            if (!officer.GetComponent<FrameBudgetSample>()) officer.gameObject.AddComponent<FrameBudgetSample>();
            pause = officer.GetComponent<PatrolPause>();
            if (!pause) pause = officer.gameObject.AddComponent<PatrolPause>();
            if (mission && mission.Office)
            {
                var stairs = mission.GetComponent<StairEncounter>();
                if (!stairs) stairs = mission.gameObject.AddComponent<StairEncounter>();
                stairs.Configure(mission.Office); mission.ConfigureStairs(stairs);
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
            if (mission.Office && mission.Office.Failed)
            {
                if (hasToast && toast.good) hasToast = false;
                while (toasts.Count > 0 && toasts.Peek().good) toasts.Dequeue();
            }
            if (hasToast && Time.unscaledTime >= toastUntil) hasToast = false;
            if (!hasToast && toasts.Count > 0 && (!mission.Office || !mission.Office.FailureAnimationPending))
            { toast = toasts.Dequeue(); hasToast = true; toastStarted = Time.unscaledTime; toastUntil = toastStarted + 3.8f; }
            bool wantCard = guidance && guidance.CardOpen && !ControlsOpen;
            if (wantCard && cardOpacity <= .001f)
            {
                displayedStep = guidance.Step; cardTitle = guidance.Title; cardInstruction = guidance.Instruction;
                cardPrimary = guidance.PrimaryChoice; cardSecondary = guidance.SecondaryChoice;
            }
            bool sameCard = wantCard && displayedStep == guidance.Step;
            cardOpacity = Mathf.MoveTowards(cardOpacity, sameCard ? 1 : 0, Time.unscaledDeltaTime / .5f);
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
            if (pause && pause.IsOpen || mission.Stairs && mission.Stairs.Finished) return;
            if (guidance && Event.current.type == EventType.Repaint) guidance.NotifyViewReady();
            // IMGUI keyboard events remain available while a decision pauses physics.
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.I) { ToggleControls(); Event.current.Use(); }
                else if (ControlsOpen && Event.current.keyCode == KeyCode.Escape) { ToggleControls(); Event.current.Use(); }
                else if (guidance && guidance.FlashlightInstructionActive && Event.current.keyCode == KeyCode.F)
                { guidance.TurnOnFlashlight(); Event.current.Use(); }
            }
            FieldUi.Prepare();
            FieldUi.Opacity = 1;
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            float scale = Screen.height / 720f;
            float width = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.depth = -20; GUI.color = Color.white;
            FieldUi.Panel(new Rect(width - 172, 20, 152, 62));
            GUI.Label(new Rect(width - 154, 28, 120, 18), "DECISION POINTS", FieldUi.Eyebrow);
            GUI.Label(new Rect(width - 154, 45, 120, 32), scoreText ?? "0", FieldUi.Title);
            if (hasToast)
            {
                float alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min((Time.unscaledTime - toastStarted) / .45f, (toastUntil - Time.unscaledTime) / .6f)));
                GUI.color = new Color(1, 1, 1, alpha); FieldUi.Opacity = alpha;
                float toastWidth = Mathf.Min(420, width - 390);
                float x = (width - toastWidth) * .5f;
                FieldUi.Panel(new Rect(x, 20, toastWidth, 76));
                FieldUi.Rounded(new Rect(x + 12, 34, 3, 46), toast.good ? FieldUi.Success : new Color(1, .55f, .4f), 1);
                GUI.Label(new Rect(x + 25, 29, toastWidth - 40, 22), toast.title, FieldUi.Eyebrow);
                GUI.Label(new Rect(x + 25, 51, toastWidth - 40, 39), toast.reason, FieldUi.Small);
                GUI.color = Color.white; FieldUi.Opacity = 1;
            }
            if ((!guidance || !guidance.CardOpen) && officer.HasControl)
            {
                FieldUi.Rounded(new Rect(width * .5f - 2, 358, 4, 4), Color.white, 2);
                if (!mission.Office || !mission.Office.Failed) DrawScanFeedback(width);
                if (!string.IsNullOrEmpty(actions.Prompt))
                {
                    float promptWidth = Mathf.Min(660, width - 160);
                    float x = (width - promptWidth) * .5f;
                    FieldUi.Panel(new Rect(x, 606, promptWidth, 42));
                    GUI.Label(new Rect(x + 15, 615, promptWidth - 30, 28), actions.Prompt, FieldUi.Small);
                }
            }
            if (guidance && cardOpacity > .001f && !ControlsOpen) DrawInstruction(width);
            else if (mission.Complete)
            {
                FieldUi.Panel(new Rect(width * .5f - 250, 260, 500, 160));
                GUI.Label(new Rect(width * .5f - 220, 284, 440, 38), "GROUND FLOOR SECURED", FieldUi.Title);
                if (FieldUi.Button(new Rect(width * .5f - 220, 350, 440, 44), "Play again", true))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            else if (!officer.HasControl && !officer.InteractionBlocked && (!guidance || !guidance.IntroPending))
            {
                if (FieldUi.Button(new Rect(width * .5f - 130, 335, 260, 48), "Resume patrol", true)) officer.SetCursor(true);
            }
            if (FieldUi.Button(new Rect(20, 656, 44, 44), "i")) ToggleControls();
            if (ControlsOpen) DrawControls(width);
            GUI.matrix = previous; GUI.color = previousColor; GUI.depth = previousDepth;
        }
        void DrawInstruction(float width)
        {
            float alpha = Mathf.SmoothStep(0, 1, cardOpacity);
            GUI.color = new Color(1, 1, 1, alpha); FieldUi.Opacity = alpha;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && guidance.CardOpen && guidance.Step == displayedStep && cardOpacity >= .95f;
            float cardWidth = Mathf.Min(580, width - 40);
            float x = (width - cardWidth) * .5f;
            FieldUi.Panel(new Rect(x, 222, cardWidth, 270));
            GUI.Label(new Rect(x + 28, 242, cardWidth - 56, 20), "FIELD BRIEFING", FieldUi.Eyebrow);
            GUI.Label(new Rect(x + 28, 270, cardWidth - 56, 36), cardTitle, FieldUi.Title);
            GUI.Label(new Rect(x + 28, 318, cardWidth - 56, 94), cardInstruction, FieldUi.Body);
            if (FieldUi.Button(new Rect(x + 28, 426, cardWidth - 228, 44), cardPrimary, true)) guidance.Continue();
            if (FieldUi.Button(new Rect(x + cardWidth - 184, 426, 156, 44), cardSecondary)) guidance.Dismiss();
            GUI.enabled = previousEnabled; GUI.color = Color.white; FieldUi.Opacity = 1;
        }
        void DrawScanFeedback(float width)
        {
            ObservationPoint active = null, recent = null;
            foreach (var corner in mission.Corners)
            {
                if (corner.IsScanning && (!active || corner.Progress > active.Progress)) active = corner;
                if (corner.Checked && Time.unscaledTime - corner.CompletedAt < 2 && (!recent || corner.CompletedAt > recent.CompletedAt)) recent = corner;
            }
            var point = active ? active : recent;
            if (!point) return;
            float x = width * .5f - 190;
            FieldUi.Panel(new Rect(x, 524, 380, 68));
            string label = active ? (point.ProgressFor(officer.View) > 0 ? "SCANNING CORNER" : "ASSISTANT SCANNING CORNER") : "CORNER SCANNED";
            GUI.Label(new Rect(x + 16, 533, 348, 20), label, FieldUi.Eyebrow);
            GUI.Label(new Rect(x + 16, 553, 348, 22), point.Label, FieldUi.Small);
            FieldUi.Rounded(new Rect(x + 16, 580, 348, 4), new Color(.16f, .22f, .29f), 2);
            FieldUi.Rounded(new Rect(x + 16, 580, 348 * (active ? point.Progress : 1), 4), active ? FieldUi.Accent : FieldUi.Success, 2);
        }
        void DrawControls(float width)
        {
            float drawerWidth = Mathf.Min(960, width - 40);
            FieldUi.Panel(new Rect(20, 390, drawerWidth, 250));
            GUI.Label(new Rect(42, 406, drawerWidth - 100, 22), "CURRENT / NEXT ACTION", FieldUi.Eyebrow);
            if (FieldUi.Button(new Rect(drawerWidth - 30, 401, 36, 30), "X")) ToggleControls();
            GUI.Label(new Rect(42, 442, drawerWidth - 44, 63), guidance ? guidance.CurrentTask : mission.Suspect.Status, FieldUi.Body);
            GUI.Label(new Rect(42, 516, drawerWidth - 100, 22), "FIELD CONTROLS", FieldUi.Eyebrow);
            float column = (drawerWidth - 44) / 3;
            GUI.Label(new Rect(42, 550, column, 80), "WASD  Move\nMouse  Look around\nShift  Move carefully", FieldUi.Body);
            GUI.Label(new Rect(42 + column, 550, column, 80), "F  Flashlight\nQ  Voice command\nE  Interact / push door", FieldUi.Body);
            GUI.Label(new Rect(42 + column * 2, 550, column, 80), "1 / 2  Stun / firearm\nLMB  Use selected tool\nI  Controls   /   Esc  Pause", FieldUi.Body);
        }
    }
}
