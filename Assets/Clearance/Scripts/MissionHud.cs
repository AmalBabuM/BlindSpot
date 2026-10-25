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
        public void ConfigureSquad(SquadCoordinator value) => squad = value;
        GUIStyle title, body, small;
        float nextTextRefresh;
        string objectiveText, scoreText, assistantText, timelineText, finalScoreText;
        string[] cornerText;
        void RefreshText()
        {
            nextTextRefresh = Time.unscaledTime + .15f;
            objectiveText = mission.Office ? mission.Office.Objective : $"Corners observed: {mission.CornersChecked}/2\nSuspect: {mission.Suspect.State}\nCheck corners > command > resolve > E to secure";
            scoreText = $"SCORE  {score.Total}";
            if (squad)
            {
                assistantText = "";
                foreach (var assistant in squad.Officers)
                    assistantText += (assistantText.Length == 0 ? "" : "\n") + "Assistant: " + assistant.Status;
            }
            if (cornerText == null || cornerText.Length != mission.Corners.Length) cornerText = new string[mission.Corners.Length];
            for (int i = 0; i < cornerText.Length; i++)
            {
                var corner = mission.Corners[i];
                cornerText[i] = corner.Checked ? corner.Label + " [checked]" : corner.Label + $" {corner.Progress:P0}";
            }
            if (mission.Complete)
            {
                finalScoreText = $"Final score: {score.Total}";
                timelineText = string.Join("\n", score.Timeline);
            }
        }
        public void Configure(GroundFloorMission m, DecisionScore s, FirstPersonOfficer p, OfficerActions a)
        { mission = m; score = s; officer = p; actions = a; }
        void OnGUI()
        {
            if (scoreText == null || Time.unscaledTime >= nextTextRefresh) RefreshText();
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
                body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                small = new GUIStyle(body) { fontSize = 14 };
            }
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            Panel(new Rect(22, 22, 420, 173));
            GUI.Label(new Rect(38, 32, 390, 40), mission.Office ? "CLEARANCE / OFFICE ENTRY" : "CLEARANCE / GROUND FLOOR", title);
            GUI.Label(new Rect(38, 79, 390, 110), objectiveText, body);
            Panel(new Rect(1010, 22, 248, 98));
            GUI.Label(new Rect(1028, 34, 215, 40), scoreText, title);
            GUI.Label(new Rect(1028, 77, 215, 30), actions.Lethal ? "2 / FIREARM - LAST RESORT" : "1 / STUN DEVICE", small);
            Panel(new Rect(22, 553, 1236, 145));
            GUI.Label(new Rect(38, 565, 1200, 28), score.Latest, body);
            GUI.Label(new Rect(38, 597, 1200, 50), actions.Message, body);
            GUI.Label(new Rect(38, 658, 1200, 27), "WASD Move  /  Mouse Look  /  Shift Slow  /  F Light  /  Q Command  /  E Interact / Secure  /  1-2 Tools  /  LMB Use  /  Esc Cursor", small);
            if (!mission.Complete && (!squad || squad.ChoiceMade))
            {
                GUI.Label(new Rect(629, 340, 28, 35), "+", title);
                GUI.Label(new Rect(390, 475, 570, 65), actions.Prompt, body);
                for (int i = 0; i < mission.Corners.Length; i++)
                {
                    var c = mission.Corners[i];
                    var p = officer.View.WorldToViewportPoint(c.transform.position);
                    if (p.z <= 0 || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) continue;
                    GUI.color = c.Checked ? Color.green : Color.cyan;
                    GUI.Label(new Rect(p.x * 1280 - 75, (1 - p.y) * 720 - 25, 210, 40),
                        cornerText[i], small);
                    GUI.color = Color.white;
                }
            }
            if (mission.Complete)
            {
                Panel(new Rect(360, 155, 560, 380));
                GUI.Label(new Rect(385, 175, 520, 45), "GROUND FLOOR SECURED", title);
                GUI.Label(new Rect(385, 223, 510, 35), finalScoreText, body);
                GUI.Label(new Rect(385, 263, 510, 195), timelineText, small);
                if (GUI.Button(new Rect(385, 475, 220, 40), "Retry ground floor")) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            else if (!officer.HasControl && !officer.InteractionBlocked && (!squad || squad.ChoiceMade))
            {
                if (GUI.Button(new Rect(495, 310, 290, 65), "Resume / Capture mouse")) officer.SetCursor(true);
            }
            if (squad && !mission.Complete)
            {
                Panel(new Rect(22, 210, 420, 132));
                GUI.Label(new Rect(38, 219, 390, 27), squad.Status, body);
                GUI.Label(new Rect(38, 250, 390, 48), assistantText, small);
                GUI.Label(new Rect(38, 307, 390, 27), "C: Team cover corners   /   B: Follow me", small);
                if (!squad.ChoiceMade)
                {
                    Panel(new Rect(340, 200, 600, 290));
                    GUI.Label(new Rect(365, 220, 550, 40), "CHOOSE YOUR ENTRY", title);
                    GUI.Label(new Rect(365, 270, 550, 78), "Your assistant is ready. Lead the way, or send the assistant ahead to search both corners with the torch.", body);
                    if (GUI.Button(new Rect(365, 365, 255, 60), "L / I lead - follow me")) squad.LeadEntry();
                    if (GUI.Button(new Rect(650, 365, 265, 60), "C / Assistant first - search")) squad.SendToCorners();
                }
            }
        }
        static void Panel(Rect rect)
        {
            GUI.color = new Color(.035f, .055f, .075f, .93f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
