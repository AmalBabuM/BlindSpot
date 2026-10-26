using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class EditorMenuCheck
    {
        static IEnumerator routine;
        static float next;
        const string Request = "Library/ClearanceMenuCheck.request";
        static EditorMenuCheck()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.delayCall += Process;
        }
        static void Process()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Request) && File.ReadAllText(Request).Trim() == "pending") Start();
        }
        [MenuItem("Clearance/Test Editor Main Menu")]
        public static void Start()
        {
            var importer = AssetImporter.GetAtPath("Assets/Resources/Launch/MenuBackground.png") as TextureImporter;
            if (importer && importer.mipmapEnabled) { importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport(); }
            File.WriteAllText(Request, "running"); SessionState.SetBool("Clearance.MenuCheck", true); EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Clearance.MenuCheck", false))
            { SessionState.EraseBool("Clearance.MenuCheck"); routine = Run(); next = 0; EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        }
        static void Tick()
        {
            if (Time.unscaledTime < next) return;
            try { if (routine.MoveNext()) next = Time.unscaledTime + Convert.ToSingle(routine.Current); else Finish("PASS: Editor starts at main menu; intro and skip work; original inactive lighting objects stay inactive; Back to Menu restores the menu."); }
            catch (Exception error) { Debug.LogException(error); Finish("FAIL: " + error); }
        }
        static void Finish(string result)
        {
            File.WriteAllText("Logs/Editor-menu-check.txt", result); Debug.Log(result);
            File.WriteAllText(Request, result.StartsWith("PASS") ? "complete" : "failed");
            EditorApplication.update -= Tick; EditorApplication.ExitPlaymode();
        }
        static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        static IEnumerator Run()
        {
            Application.runInBackground = true; yield return 1f;
            var menu = PatrolMainMenu.Active; Check(menu, "Editor main menu missing");
            bool probesWereActive = menu.ProbesWereActive;
            Check(UnityEngine.Object.FindAnyObjectByType<FirstPersonOfficer>() == null, "Gameplay running behind menu");
            ScreenCapture.CaptureScreenshot("Logs/Editor-main-menu.png", 2); yield return .2f;
            menu.StartIntro(); yield return .8f; Check(menu.IntroPlaying, "Start Game did not start intro");
            float deadline = Time.unscaledTime + 8;
            while (!menu.IntroHasFrame && Time.unscaledTime < deadline) yield return .2f;
            Check(menu.IntroHasFrame, "Intro did not decode a video frame");
            menu.EnterMission(); yield return .5f;
            var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonOfficer>();
            Check(player, "Skip did not enter game");
            Check((UnityEngine.Object.FindAnyObjectByType<LightProbeGroup>() != null) == probesWereActive, "Probe group activation changed");
            Check(!UnityEngine.Object.FindAnyObjectByType<MissionHud>().Guidance.CardOpen, "Tutorial timer ran behind menu");
            PatrolPause.BackToMenu(); yield return .6f;
            Check(PatrolMainMenu.Active && !UnityEngine.Object.FindAnyObjectByType<FirstPersonOfficer>(), "Back to Menu did not restore menu");
        }
    }
}
