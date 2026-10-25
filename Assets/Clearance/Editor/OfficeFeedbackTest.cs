using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Clearance.Editor
{
    public sealed class FeedbackScanDriver : MonoBehaviour
    {
        public ObservationPoint Corner;
        public Camera Observer;
        public DecisionScore Score;
        void Update() => Corner.Observe(Observer, Score, true, Observer.transform);
    }
    [InitializeOnLoad]
    public static class OfficeFeedbackTest
    {
        const string Request = "Library/ClearanceFeedbackTest.request";
        static IEnumerator routine;
        static float next;
        static OfficeFeedbackTest()
        {
            EditorApplication.delayCall += Process;
            EditorApplication.playModeStateChanged += Changed;
        }
        static void Process()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = File.ReadAllText(Request).Trim();
            if (request == "pending" || request == "stun")
            { File.WriteAllText(Request, "running"); SessionState.SetBool("Clearance.FeedbackStun", request == "stun"); Start(); }
        }
        [MenuItem("Clearance/Test Death And Scan Feedback")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "running") SessionState.SetBool("Clearance.FeedbackStun", false);
            SessionState.SetBool("Clearance.FeedbackTest", true); EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Clearance.FeedbackTest", false))
            { SessionState.SetBool("Clearance.FeedbackTest", false); routine = Run(); next = 0; EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        }
        static void Tick()
        {
            if (Time.unscaledTime < next) return;
            try
            {
                if (routine.MoveNext()) next = Time.unscaledTime + Convert.ToSingle(routine.Current);
                else Finish("PASS (" + (SessionState.GetBool("Clearance.FeedbackStun", false) ? "stun device" : "firearm") + "): scan checks pass; falling clip completes and body rests on floor before failure card or force notification.");
            }
            catch (Exception e) { Debug.LogException(e); Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Office-feedback-test.txt", result);
            File.WriteAllText(Request, result.StartsWith("PASS") ? "complete" : "failed");
            Debug.Log(result); EditorApplication.ExitPlaymode();
        }
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static IEnumerator Run()
        {
            bool lethal = !SessionState.GetBool("Clearance.FeedbackStun", false);
            Application.runInBackground = true; yield return .2f;
            var office = UnityEngine.Object.FindAnyObjectByType<OfficeEncounter>();
            var hud = UnityEngine.Object.FindAnyObjectByType<MissionHud>();
            var guidance = hud.Guidance;
            double deadline = EditorApplication.timeSinceStartup + 15;
            while (guidance.IntroPending && EditorApplication.timeSinceStartup < deadline) yield return .2f;
            Check(!guidance.IntroPending, "Intro did not become visible");
            guidance.Dismiss();
            office.Player.enabled = false; office.Player.GetComponent<OfficerActions>().enabled = false;
            office.Player.GetComponent<CharacterController>().enabled = false;
            yield return .2f;
            var cameraObject = new GameObject("ScanTestObserver", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            cameraObject.transform.position = new Vector3(0, 40, 0);
            var cornerObject = new GameObject("FeedbackTestCorner", typeof(ObservationPoint));
            var corner = cornerObject.GetComponent<ObservationPoint>(); corner.Configure("Test corner");
            cornerObject.transform.position = new Vector3(0, 40, 2);
            corner.Observe(camera, office.Score, false, camera.transform);
            Check(!corner.IsScanning && corner.Progress == 0, "Scan advanced without flashlight");
            corner.Observe(camera, office.Score, true, camera.transform);
            Check(corner.IsScanning && corner.ProgressFor(camera) > 0, "Scan progress unavailable to HUD");
            corner.StopObserving(camera);
            Check(!corner.IsScanning && corner.Progress == 0, "Scan remained active after observer left");
            int before = office.Score.Total;
            float scanStarted = Time.unscaledTime;
            var scanDriver = cornerObject.AddComponent<FeedbackScanDriver>();
            scanDriver.Corner = corner; scanDriver.Observer = camera; scanDriver.Score = office.Score;
            float until = Time.unscaledTime + 4;
            while (!corner.Checked && Time.unscaledTime < until)
                yield return .05f;
            Check(corner.Checked && corner.CompletedAt >= scanStarted, "Scan completion feedback missing: progress=" + corner.Progress);
            Check(office.Score.Total == before + office.Score.Rules.cornerChecked, "Scan reward missing");
            corner.Observe(camera, office.Score, true, camera.transform);
            Check(office.Score.Total == before + office.Score.Rules.cornerChecked, "Repeated scan farmed points");
            UnityEngine.Object.Destroy(cameraObject); UnityEngine.Object.Destroy(cornerObject);
            // Let the scan reward toast finish before checking that the penalty is deferred.
            yield return 4.2f;
            var presentation = office.Suspect.GetComponentInChildren<PrisonerPresentation>();
            before = office.Score.Total;
            office.Suspect.Hit(lethal, true);
            Check(office.FailureAnimationPending && !presentation.FallAnimationFinished, "Death was reported finished before the fall clip started");
            yield return .2f;
            Check(!guidance.CardOpen && Time.timeScale == 1 && !hud.ToastVisible, "Failure notification paused/interrupted the death animation");
            deadline = EditorApplication.timeSinceStartup + 15;
            bool floorPauseObserved = false;
            while (!guidance.CardOpen && EditorApplication.timeSinceStartup < deadline)
            {
                if (presentation.FallAnimationFinished && office.FailureAnimationPending)
                {
                    floorPauseObserved = true;
                    Check(Time.timeScale == 1 && !hud.ToastVisible, "UI interrupted the pause after the suspect reached the floor");
                }
                yield return .1f;
            }
            Check(floorPauseObserved, "No visible pause on the floor before notification");
            Check(presentation.FallAnimationFinished, "Fall clip did not finish");
            Check(guidance.CardOpen && guidance.Step == OfficeBriefing.Failed && guidance.Title == "MISSION FAILED", "Failure card missing after death");
            Check(guidance.Instruction == (lethal ? "You shot an unarmed suspect. Mission failed." : "You used force on an unarmed suspect. Mission failed."), "Wrong unarmed-suspect failure message");
            int expected = before + (lethal ? office.Score.Rules.unjustifiedShot : office.Score.Rules.nonLethal + office.Score.Rules.preciseNonLethal);
            Check(Time.timeScale == 0 && office.Score.Total == expected, "Failure pause/score incorrect");
            office.Suspect.Hit(lethal, true);
            Check(office.Score.Total == expected, "Repeat hits duplicated score");
        }
    }
}
