using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class ElevatorSmokeTest
    {
        static IEnumerator routine;
        static double next;
        static ElevatorSmokeTest() => EditorApplication.playModeStateChanged += Changed;
        [MenuItem("Clearance/Test 3D Elevator")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool("Clearance.ElevatorTest", true); EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Clearance.ElevatorTest", false))
            { SessionState.SetBool("Clearance.ElevatorTest", false); routine = Run(); next = 0; EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else Finish("PASS: 3D elevator targeting/range, floor choice/cancel, imported door animation, unsafe warning, blocked travel and restored controls.");
            }
            catch (Exception e) { Debug.LogException(e); Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            File.WriteAllText("Logs/Elevator-smoke-test.txt", result); Debug.Log(result); EditorApplication.ExitPlaymode();
        }
        static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        static IEnumerator Run()
        {
            Application.runInBackground = true;
            yield return .7f;
            var lift = UnityEngine.Object.FindFirstObjectByType<UnsafeElevator>();
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonOfficer>();
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadCoordinator>();
            Check(lift && player, "Elevator scene wiring missing");
            if (squad)
            {
                foreach (var assistant in squad.Officers) Check(assistant.Agent.isOnNavMesh, "Lift blocked assistant navigation spawn");
                squad.LeadEntry();
            }
            player.enabled = false; player.GetComponent<OfficerActions>().enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = new Vector3(20, .1f, 0);
            Check(!lift.ShowFloorChoice(player), "Lift was interactable from outside interaction range");
            player.transform.position = lift.transform.TransformPoint(new Vector3(0, .06f, -1.8f));
            player.View.transform.LookAt(lift.CallPoint);
            Physics.SyncTransforms();
            Check(Physics.Raycast(player.View.transform.position, player.View.transform.forward, out var hit, 3) &&
                hit.collider.GetComponentInParent<UnsafeElevator>() == lift, "Elevator button is blocked from view");
            Check(lift.ShowFloorChoice(player) && player.InteractionBlocked && Cursor.lockState == CursorLockMode.None, "Floor choice did not suspend controls");
            lift.Dismiss();
            Check(!lift.IsOpen && !player.InteractionBlocked, "Cancel opened the doors or failed to restore controls");
            Vector3 left = lift.LeftPanel.localPosition, right = lift.RightPanel.localPosition;
            Vector3 playerPosition = player.transform.position; var scene = SceneManager.GetActiveScene().handle;
            Check(lift.ShowFloorChoice(player), "Second interaction failed"); lift.ChooseUpperFloor();
            Check(lift.IsOpen && lift.IsDialogOpen && lift.Warning.Contains("not safe") && !lift.TravelAllowed, "Unsafe lift did not show warning or allowed travel");
            yield return 2.5f;
            Check(lift.LeftPanel.localPosition.x < left.x - .8f && lift.RightPanel.localPosition.x > right.x + .8f,
                "Imported 2D animation did not slide the real 3D panels");
            Check(SceneManager.GetActiveScene().handle == scene && Vector3.Distance(player.transform.position, playerPosition) < .01f,
                "Unsafe lift moved player to another floor");
            lift.Dismiss(); Check(!player.InteractionBlocked && !lift.IsDialogOpen, "Acknowledging warning did not restore controls");
            lift.ShowFloorChoice(player); lift.ChooseUpperFloor();
            Check(lift.OpenCount == 1, "Repeated interaction restarted the opening animation"); lift.Dismiss();
        }
    }
}
