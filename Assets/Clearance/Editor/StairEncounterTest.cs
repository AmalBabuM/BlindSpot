using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class StairEncounterTest
    {
        const string Request = "Library/ClearanceStairsTest.request";
        static IEnumerator routine;
        static float next;
        static StairEncounterTest()
        {
            EditorApplication.delayCall += Process;
            EditorApplication.playModeStateChanged += Changed;
        }
        static void Process()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request) || File.ReadAllText(Request).Trim() != "pending") return;
            File.WriteAllText(Request, "running"); SessionState.SetBool("Clearance.StairsTest", true); EditorApplication.EnterPlaymode();
        }
        [MenuItem("Clearance/Test Stair Encounter And Pause")]
        public static void Start() { File.WriteAllText(Request, "pending"); Process(); }
        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Clearance.StairsTest", false))
            { SessionState.SetBool("Clearance.StairsTest", false); routine = Run(); next = 0; EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        }
        static void Tick()
        {
            if (Time.unscaledTime < next) return;
            try { if (routine.MoveNext()) next = Time.unscaledTime + Convert.ToSingle(routine.Current); else Finish("PASS: pause/resume, sudden-door attack, camera fall and delayed red death report; careful door opening, distinct command scores, dropped gun, animated surrender, assistant handcuffs, final score report."); }
            catch (Exception error) { Debug.LogException(error); Finish("FAIL: " + error); }
        }
        static void Finish(string message)
        {
            File.WriteAllText("Logs/Stair-encounter-test.txt", message); File.WriteAllText(Request, message.StartsWith("PASS") ? "complete" : "failed");
            Debug.Log(message); EditorApplication.update -= Tick; Time.timeScale = 1; EditorApplication.ExitPlaymode();
        }
        static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        static void Set(object target, string name, object value) => target.GetType().GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static IEnumerator Run()
        {
            Application.runInBackground = true; yield return 1f;
            var mission = UnityEngine.Object.FindFirstObjectByType<GroundFloorMission>();
            var office = mission.Office; var player = office.Player;
            var hud = UnityEngine.Object.FindFirstObjectByType<MissionHud>(); hud.Guidance.EndGuidance();
            var pause = player.GetComponent<PatrolPause>();
            player.SetInteractionBlocked(false); pause.Toggle();
            Check(pause.IsOpen && Time.timeScale == 0 && player.InteractionBlocked, "Pause did not stop game/release interaction");
            pause.Toggle(); Check(!pause.IsOpen && Time.timeScale == 1 && !player.InteractionBlocked, "Resume did not restore game state");
            player.SetInteractionBlocked(true);
            var stairs = mission.Stairs;
            Check(stairs && stairs.Doors.Length == 2 && !stairs.Suspect.gameObject.activeSelf, "Stair bootstrap/doors not configured");
            var cameraPosition = player.View.transform.localPosition; var cameraRotation = player.View.transform.localRotation;
            stairs.PlayerPushedDoor();
            Check(stairs.PlayerDying && !stairs.ReportOpen && player.InteractionBlocked, "Sudden entry did not begin death sequence");
            int deathScore = office.Score.Total; stairs.PlayerPushedDoor(); Check(office.Score.Total == deathScore, "Ambush penalty duplicated");
            yield return 1.7f;
            Check(!stairs.ReportOpen && player.View.transform.localPosition.y < .5f && Time.timeScale == 1, "Death report interrupted the camera fall");
            yield return 2.2f;
            Check(stairs.ReportOpen && Time.timeScale == 0, "Death report missing");
            ScreenCapture.CaptureScreenshot("Logs/Stairs-death-report.png", 3); yield return .2f;
            UnityEngine.Object.Destroy(stairs.gameObject == mission.gameObject ? stairs.Suspect.gameObject : null);
            UnityEngine.Object.Destroy(stairs); yield return .1f;
            Time.timeScale = 1; player.View.transform.localPosition = cameraPosition; player.View.transform.localRotation = cameraRotation;
            foreach (var person in office.People) Set(person, "Identified", true);
            Set(office.Escort, "Returned", true);
            Check(office.ReadyForNextFloor, "Test did not prepare reception completion");
            stairs = mission.gameObject.AddComponent<StairEncounter>(); stairs.Configure(office); mission.ConfigureStairs(stairs);
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadCoordinator>(); var assistant = squad.Officers[0]; assistant.enabled = true;
            var testDoor = stairs.Doors[0];
            foreach (var door in stairs.Doors)
                if (Vector3.Distance(door.transform.position, new Vector3(-20.5f, 0, 2)) < Vector3.Distance(testDoor.transform.position, new Vector3(-20.5f, 0, 2))) testDoor = door;
            stairs.OpenCarefully(testDoor);
            Check(stairs.CarefulEntry && stairs.Started && !stairs.Finished && stairs.Suspect.Weapon.activeSelf, "Careful entry did not expose armed suspect safely");
            yield return .5f; Check(testDoor.OpeningAngle > 0 && testDoor.OpeningAngle < 25, "Careful door did not open slowly");
            stairs.PlayerPushedDoor(); Check(!stairs.PlayerDying, "Collision overrode careful opening");
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            player.transform.position = new Vector3(-20.5f, .06f, 2);
            player.transform.rotation = Quaternion.LookRotation(new Vector3(stairs.Suspect.transform.position.x - player.transform.position.x, 0,
                stairs.Suspect.transform.position.z - player.transform.position.z));
            player.View.transform.LookAt(stairs.Suspect.transform.position + Vector3.up * 1.3f);
            yield return 3.2f;
            ScreenCapture.CaptureScreenshot("Logs/Stairs-armed-entry.png", 3); yield return .2f;
            stairs.Command(stairs.Suspect); Check(stairs.Suspect.State == SuspectState.HandsUp && !stairs.Suspect.Weapon.activeSelf, "Hands-up command did not disarm suspect");
            yield return 1.5f;
            Check(stairs.Suspect.State == SuspectState.VerifiedUnarmed, "Dropped weapon did not settle before surrender command");
            stairs.Command(stairs.Suspect); Check(stairs.Suspect.State == SuspectState.Considering, "Second Q did not start kneeling");
            var agent = assistant.Agent;
            Vector3 desired = player.transform.position - player.transform.forward;
            Check(NavMesh.SamplePosition(desired, out var point, 1.2f, NavMesh.AllAreas), "No navigation behind stair suspect");
            agent.Warp(point.position);
            float deadline = Time.unscaledTime + 30;
            while (!stairs.ReportOpen && Time.unscaledTime < deadline) yield return .2f;
            Check(stairs.Suspect.State == SuspectState.Restrained, "Assistant did not finish handcuffs: " + assistant.Status);
            Check(stairs.ReportOpen && Time.timeScale == 0, "Success report missing");
            ScreenCapture.CaptureScreenshot("Logs/Stairs-success-report.png", 3); yield return .2f;
            Check(office.Score.Timeline.Count > 5, "Decision review did not retain both encounters");
        }
    }
}
