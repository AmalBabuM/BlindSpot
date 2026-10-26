using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clearance
{
    public sealed class PatrolPause : MonoBehaviour
    {
        FirstPersonOfficer player;
        bool wasBlocked;
        float previousTime;
        public bool IsOpen { get; private set; }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ClearanceBackToMenu();
#endif
        void Awake() => player = GetComponent<FirstPersonOfficer>();
        public void Toggle()
        {
            var stairs = FindFirstObjectByType<StairEncounter>();
            if (!IsOpen && stairs && stairs.Finished) return;
            if (IsOpen) { IsOpen = false; Time.timeScale = previousTime; player.SetInteractionBlocked(wasBlocked); }
            else { wasBlocked = player.InteractionBlocked; previousTime = Time.timeScale; IsOpen = true; Time.timeScale = 0; player.SetInteractionBlocked(true); }
        }
        public static void BackToMenu()
        {
            Time.timeScale = 1;
#if UNITY_WEBGL && !UNITY_EDITOR
            ClearanceBackToMenu();
#else
#if UNITY_EDITOR
            UnityEditor.SessionState.SetBool("Clearance.ShowEditorMenu", true);
#endif
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
        }
        void OnGUI()
        {
            if (!IsOpen) return;
            FieldUi.Prepare(); FieldUi.Opacity = 1;
            var matrix = GUI.matrix; int depth = GUI.depth; var color = GUI.color;
            GUI.depth = -100; GUI.color = Color.white;
            float scale = Screen.height / 720f, width = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            FieldUi.Rounded(new Rect(0, 0, width, 720), new Color(0, .02f, .04f, .7f), 0);
            float x = width / 2 - 190;
            FieldUi.Panel(new Rect(x, 210, 380, 270));
            GUI.Label(new Rect(x + 28, 236, 324, 40), "PATROL PAUSED", FieldUi.Title);
            if (FieldUi.Button(new Rect(x + 28, 300, 324, 50), "Resume patrol", true)) Toggle();
            if (FieldUi.Button(new Rect(x + 28, 368, 324, 50), "Back to Menu")) BackToMenu();
            GUI.Label(new Rect(x + 28, 438, 324, 24), "Esc to resume · Returning resets this run", FieldUi.Small);
            GUI.matrix = matrix; GUI.depth = depth; GUI.color = color;
        }
        void OnDestroy() { if (IsOpen) Time.timeScale = previousTime; }
    }
}
