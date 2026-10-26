#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Clearance
{
    // Editor preview of the WebGL launch flow. Excluded entirely from player builds.
    public sealed class EditorPatrolMenu : MonoBehaviour
    {
        GameObject[] sceneRoots;
        Texture2D background;
        VideoPlayer video;
        bool controls, intro, muted, completed;
        GUIStyle heading;
        public bool IntroPlaying => intro;
        public bool ProbesWereActive { get; private set; }
        public static EditorPatrolMenu Active { get; private set; }
        static bool firstScene;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        { firstScene = true; SceneManager.sceneLoaded -= ShowOnPlay; SceneManager.sceneLoaded += ShowOnPlay; }
        static void ShowOnPlay(Scene scene, LoadSceneMode mode)
        {
            if (SceneManager.GetActiveScene().name != "GroundFloor") return;
            bool requested = SessionState.GetBool("Clearance.ShowEditorMenu", false);
            SessionState.EraseBool("Clearance.ShowEditorMenu");
            if (!firstScene && !requested) return;
            firstScene = false;
            if (SessionState.GetBool("Clearance.SkipEditorMenu", false))
            { SessionState.EraseBool("Clearance.SkipEditorMenu"); return; }
            string[] tests = { "OfficeTest", "StairsTest", "FeedbackTest", "SquadTest", "ElevatorTest", "LightingVerify", "SmokeTest" };
            foreach (string test in tests) if (SessionState.GetBool("Clearance." + test, false)) return;
            var roots = System.Array.FindAll(SceneManager.GetActiveScene().GetRootGameObjects(), root => root.activeSelf);
            var menu = new GameObject("Editor Main Menu").AddComponent<EditorPatrolMenu>();
            menu.sceneRoots = roots;
            foreach (var root in roots) if (root.activeSelf) root.SetActive(false);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Time.timeScale = 1;
        }
        void Awake()
        {
            Active = this;
            ProbesWereActive = FindAnyObjectByType<LightProbeGroup>() != null;
            muted = EditorPrefs.GetBool("Clearance.EditorMuted", false);
            background = new Texture2D(2, 2, TextureFormat.RGB24, false);
            background.LoadImage(File.ReadAllBytes("Assets/WebGLTemplates/ClearanceIntro/menu-background.png"), true);
            var camera = gameObject.AddComponent<Camera>(); camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            gameObject.AddComponent<AudioListener>();
            video = gameObject.AddComponent<VideoPlayer>(); video.playOnAwake = false; video.isLooping = false;
            video.source = VideoSource.Url; video.url = Path.Combine(Application.streamingAssetsPath, "Intro/Intro_Video.mp4");
            video.renderMode = VideoRenderMode.APIOnly; video.audioOutputMode = VideoAudioOutputMode.Direct;
            video.loopPointReached += _ => EnterMission();
            video.errorReceived += (_, error) => { Debug.LogWarning("Intro preview: " + error); EnterMission(); };
        }
        public void StartIntro()
        {
            if (intro || completed) return;
            intro = true; controls = false; video.SetDirectAudioMute(0, muted); video.Play();
        }
        public void EnterMission()
        {
            if (completed) return;
            completed = true; video.Stop(); gameObject.GetComponent<AudioListener>().enabled = false;
            AudioListener.volume = muted ? 0 : 1;
            foreach (var root in sceneRoots) if (root) root.SetActive(true);
            Destroy(gameObject);
        }
        void OnGUI()
        {
            if (completed) return;
            FieldUi.Prepare(); FieldUi.Opacity = 1;
            if (heading == null) { heading = new GUIStyle(FieldUi.Title) { fontSize = 58 }; }
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            float scale = Screen.height / 720f, width = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale); GUI.color = Color.white; GUI.depth = -200;
            if (intro)
            {
                GUI.DrawTexture(new Rect(0, 0, width, 720), Texture2D.blackTexture);
                if (video.texture) GUI.DrawTexture(new Rect(0, 0, width, 720), video.texture, ScaleMode.ScaleToFit);
                if (FieldUi.Button(new Rect(width - 190, 650, 160, 46), "Skip intro →")) EnterMission();
            }
            else
            {
                GUI.DrawTexture(new Rect(0, 0, width, 720), background, ScaleMode.ScaleAndCrop);
                FieldUi.Rounded(new Rect(0, 0, Mathf.Min(510, width), 720), new Color(.015f, .035f, .065f, .88f), 0);
                GUI.Label(new Rect(54, 120, 440, 26), "TACTICAL DECISIONS. REAL CONSEQUENCES.", FieldUi.Eyebrow);
                GUI.Label(new Rect(50, 162, 450, 80), "BLINDSPOT", heading);
                GUI.Label(new Rect(54, 250, 400, 30), "CLEARANCE BUILDING", FieldUi.Body);
                GUI.Label(new Rect(54, 302, 400, 72), "Enter carefully. Verify the threat.\nBring everyone out safely.", FieldUi.Body);
                if (FieldUi.Button(new Rect(54, 400, 340, 52), "Start Game  →", true)) StartIntro();
                if (FieldUi.Button(new Rect(54, 468, 340, 52), "Controls  /  i")) controls = !controls;
                if (FieldUi.Button(new Rect(54, 536, 340, 52), muted ? "Sound: Off" : "Sound: On"))
                { muted = !muted; EditorPrefs.SetBool("Clearance.EditorMuted", muted); }
                if (controls)
                {
                    float x = width * .5f - 260;
                    FieldUi.Panel(new Rect(x, 130, 520, 470));
                    GUI.Label(new Rect(x + 28, 154, 464, 42), "FIELD CONTROLS", FieldUi.Title);
                    GUI.Label(new Rect(x + 28, 218, 464, 290),
                        "WASD  Move     /     Mouse  Look\nShift  Move carefully\nF  Flashlight\nQ  Voice command\nE  Interact / open stair door carefully\n1 / 2  Stun device / firearm\nLeft click  Use selected tool\nI  Instructions and controls\nEsc  Pause / Back to Menu", FieldUi.Body);
                    if (FieldUi.Button(new Rect(x + 28, 530, 464, 46), "Close", true)) controls = false;
                }
            }
            GUI.matrix = matrix; GUI.color = color; GUI.depth = depth;
        }
        void OnDestroy() { if (background) Destroy(background); if (Active == this) Active = null; }
    }
}
#endif
