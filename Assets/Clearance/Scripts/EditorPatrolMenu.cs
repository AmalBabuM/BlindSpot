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
        Texture2D menuShade;
        [SerializeField, Range(0, 1)] float overlayOpacity = 1;
        VideoPlayer video;
        bool controls, intro, muted, completed;
        Font menuFont;
        GUIStyle heading, eyebrow, subtitle, description, footer, menuButtonText, menuButtonIcon;
        GUIContent[] eyebrowLetters, subtitleLetters, footerLetters;
        float[] eyebrowWidths, subtitleWidths, footerWidths;
        static readonly Color MenuWhite = new Color32(237, 245, 252, 255);
        static readonly Color MenuTeal = new Color32(105, 217, 209, 255);
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
            // Match menu.css: a full-width fade, rather than a box with a hard edge.
            menuShade = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            menuShade.wrapMode = TextureWrapMode.Clamp; menuShade.filterMode = FilterMode.Bilinear;
            var pixels = new Color[256];
            for (int i = 0; i < pixels.Length; i++)
            {
                float x = i / 255f;
                float alpha = x <= .29f ? Mathf.Lerp(.94f, .74f, x / .29f) :
                    x <= .65f ? Mathf.Lerp(.74f, .15f, (x - .29f) / .36f) : Mathf.Lerp(.15f, 0, (x - .65f) / .35f);
                pixels[i] = new Color(2 / 255f, 9 / 255f, 18 / 255f, alpha);
            }
            menuShade.SetPixels(pixels); menuShade.Apply(false, true);
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
            PrepareMenuStyles();
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
                GUI.color = new Color(1, 1, 1, overlayOpacity);
                GUI.DrawTexture(new Rect(0, 0, width, 720), menuShade);
                GUI.color = Color.white;
                DrawLetters(new Vector2(54, 120), eyebrowLetters, eyebrowWidths, eyebrow, 2);
                GUI.Label(new Rect(50, 162, 450, 84), "<b>BLIND<color=#69D9D1>SPOT</color></b>", heading);
                DrawLetters(new Vector2(54, 250), subtitleLetters, subtitleWidths, subtitle, 5);
                GUI.Label(new Rect(54, 302, 400, 72), "Enter carefully. Verify the threat.\nBring everyone out safely.", description);
                if (MenuButton(new Rect(54, 400, 340, 54), "Start Game", "→", true)) StartIntro();
                if (MenuButton(new Rect(54, 468, 340, 54), "Controls", "i")) controls = !controls;
                if (MenuButton(new Rect(54, 536, 340, 54), muted ? "Sound: Off" : "Sound: On", ""))
                { muted = !muted; EditorPrefs.SetBool("Clearance.EditorMuted", muted); }
                DrawLetters(new Vector2(54, 626), footerLetters, footerWidths, footer, 2);
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
        void PrepareMenuStyles()
        {
            if (heading != null) return;
            menuFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            GUIStyle Style(int size, FontStyle weight, Color tint)
            {
                var style = new GUIStyle(GUI.skin.label) { font = menuFont, fontSize = size, fontStyle = weight, padding = new RectOffset(0, 0, 0, 0) };
                style.normal.textColor = tint; return style;
            }
            heading = Style(60, FontStyle.Bold, MenuWhite);
            heading.richText = true;
            eyebrow = Style(11, FontStyle.Bold, MenuTeal);
            subtitle = Style(13, FontStyle.Normal, new Color32(177, 198, 216, 255));
            description = Style(16, FontStyle.Normal, new Color32(192, 203, 216, 255)); description.wordWrap = true;
            footer = Style(10, FontStyle.Normal, new Color32(148, 170, 189, 255));
            menuButtonText = Style(16, FontStyle.Bold, MenuWhite); menuButtonText.alignment = TextAnchor.MiddleLeft;
            menuButtonIcon = new GUIStyle(menuButtonText) { alignment = TextAnchor.MiddleRight };
            CacheLetters("TACTICAL DECISIONS. REAL CONSEQUENCES.", eyebrow, out eyebrowLetters, out eyebrowWidths);
            CacheLetters("CLEARANCE BUILDING", subtitle, out subtitleLetters, out subtitleWidths);
            CacheLetters("GROUND FLOOR · RECEPTION", footer, out footerLetters, out footerWidths);
        }
        static void CacheLetters(string text, GUIStyle style, out GUIContent[] letters, out float[] widths)
        {
            letters = new GUIContent[text.Length]; widths = new float[text.Length];
            for (int i = 0; i < text.Length; i++) { letters[i] = new GUIContent(text[i].ToString()); widths[i] = style.CalcSize(letters[i]).x; }
        }
        static void DrawLetters(Vector2 origin, GUIContent[] letters, float[] widths, GUIStyle style, float spacing)
        {
            for (int i = 0; i < letters.Length; i++)
            {
                GUI.Label(new Rect(origin.x, origin.y, widths[i] + 4, style.fontSize * 1.5f), letters[i], style);
                origin.x += widths[i] + spacing;
            }
        }
        bool MenuButton(Rect rect, string text, string icon, bool primary = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Color fill = primary ? (hover ? new Color32(148, 238, 231, 255) : new Color32(99, 216, 206, 255)) :
                hover ? new Color32(35, 63, 82, 255) : new Color32(12, 25, 40, 219);
            FieldUi.Rounded(rect, primary ? fill : new Color32(82, 101, 119, 255), 13);
            if (!primary) FieldUi.Rounded(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), fill, 12);
            menuButtonText.normal.textColor = menuButtonIcon.normal.textColor = primary ? new Color32(5, 28, 36, 255) : MenuWhite;
            bool clicked = GUI.Button(new Rect(rect.x + 22, rect.y, rect.width - 44, rect.height), text, menuButtonText);
            GUI.Label(new Rect(rect.x + 22, rect.y, rect.width - 44, rect.height), icon, menuButtonIcon);
            // The full rounded surface, including the icon, is clickable.
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && hover) { clicked = true; Event.current.Use(); }
            return clicked;
        }
        void OnDestroy()
        { if (background) Destroy(background); if (menuShade) Destroy(menuShade); if (menuFont) Destroy(menuFont); if (Active == this) Active = null; }
    }
}
#endif
