using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class SquadSmokeTest
    {
        static IEnumerator routine;
        static double next;
        static SquadSmokeTest() => EditorApplication.playModeStateChanged += Changed;
        [MenuItem("Clearance/Test Assisting Officers")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool("Clearance.SquadTest", true); EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Clearance.SquadTest", false))
            { SessionState.SetBool("Clearance.SquadTest", false); routine = Run(); next = 0; EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else Finish("PASS: one assistant, torch clips, stationary hold, distance follow, sequential corner checks, regrouping and surrender/handcuff support.");
            }
            catch (Exception e) { Debug.LogException(e); Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            File.WriteAllText("Logs/Squad-smoke-test.txt", result); Debug.Log(result); EditorApplication.ExitPlaymode();
        }
        static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        static IEnumerator Run()
        {
            Application.runInBackground = true;
            yield return .7f;
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadCoordinator>();
            Check(squad && squad.Officers.Length == 1, "Expected exactly one assisting officer");
            Check(!squad.ChoiceMade && !squad.Leader.HasControl, "Entry choice did not gate movement");
            Check(Cursor.lockState == CursorLockMode.None, "Entry choice did not release cursor");
            foreach (var officer in squad.Officers)
            {
                Check(officer.Agent.enabled && officer.Agent.isOnNavMesh, "Officer did not spawn on navigation mesh");
                var animator = officer.GetComponentInChildren<Animator>();
                Check(animator && animator.isHuman && animator.runtimeAnimatorController, "Officer animations are not retargeted");
                Check(officer.UsesTorchAnimations, "Assistant is still using procedural careful-walk poses");
                var clips = animator.runtimeAnimatorController.animationClips;
                Check(Array.Exists(clips, c => c.name == "Torch Walk") && Array.Exists(clips, c => c.name == "Torch Search"), "Torch animations were not assigned");
            }
            squad.Leader.enabled = false;
            squad.Leader.GetComponent<CharacterController>().enabled = false;
            squad.LeadEntry();
            yield return .5f;
            Vector3[] held = Array.ConvertAll(squad.Officers, o => o.transform.position);
            squad.Leader.transform.Rotate(0, 180, 0);
            squad.Leader.transform.position += Vector3.right * .15f;
            yield return 1;
            for (int i = 0; i < squad.Officers.Length; i++)
                Check(Vector3.Distance(held[i], squad.Officers[i].transform.position) < .1f && !squad.Officers[i].IsFollowing,
                    "Officer orbited the leader on rotation or small movement");
            squad.SendToCorners();
            Check(squad.ChoiceMade && squad.Order == SquadOrder.CoverCorners, "Team-first choice failed");
            float until = Time.time + 35;
            yield return .3f;
            foreach (var officer in squad.Officers) Check(officer.Agent.speed <= 1.1f, "Cover advance is not cautious");
            while (Time.time < until && squad.Mission.CornersChecked < 2) yield return .2f;
            Check(squad.Mission.CornersChecked == 2, "Officers failed to reach/check both corners: " + string.Join(" / ", Array.ConvertAll(squad.Officers, o => o.Status + " @ " + o.transform.position)));
            Check(squad.Score.Total == 100, "Team corner checks did not score exactly once");
            squad.LeadEntry();
            Check(squad.Order == SquadOrder.Follow, "Regroup command failed");
            until = Time.time + 25;
            while (Time.time < until && Vector3.Distance(squad.Officers[0].transform.position, squad.Leader.transform.position) > 2.5f) yield return .2f;
            foreach (var officer in squad.Officers)
                Check(Vector3.Distance(officer.transform.position, squad.Leader.transform.position) < 3.5f, "Officer did not rejoin the leader");
            until = Time.time + 12;
            while (Time.time < until && Array.Exists(squad.Officers, o => o.IsFollowing)) yield return .2f;
            Check(!Array.Exists(squad.Officers, o => o.IsFollowing), "Officers did not settle back into idle");
            Vector3 direction = (squad.CoverPositions[0].position - squad.Leader.transform.position).normalized;
            Check(NavMesh.SamplePosition(squad.Leader.transform.position + direction * 4.8f, out var nextLeader, 1, NavMesh.AllAreas), "No walkable follow-test destination");
            squad.Leader.transform.position = nextLeader.position;
            yield return .5f;
            Check(Array.Exists(squad.Officers, o => o.IsFollowing), "Officers did not follow when the leader moved away");
            until = Time.time + 15;
            while (Time.time < until && Array.Exists(squad.Officers, o => o.IsFollowing)) yield return .2f;
            foreach (var officer in squad.Officers)
                Check(Vector3.Distance(officer.transform.position, squad.Leader.transform.position) < 3.5f && !officer.IsFollowing,
                    "Distance follow did not catch up and return to idle");
            Check(squad.Score.Total == 100, "Regrouping farmed corner rewards");
            var suspect = squad.Mission.Suspect;
            suspect.Alert();
            Check(suspect.Command(), "Could not begin hands-up sequence for assistant support");
            until = Time.time + 45;
            while (Time.time < until && Vector3.Distance(squad.Officers[0].transform.position, suspect.transform.position) > 1.8f) yield return .2f;
            Check(Vector3.Distance(squad.Officers[0].transform.position, suspect.transform.position) < 1.8f,
                "Assistant did not approach the surrendering suspect: " + squad.Officers[0].Status);
            suspect.InspectForWeapons(2, true);
            Check(suspect.Command(), "Kneel command rejected after weapon inspection");
            until = Time.time + 30;
            while (Time.time < until && suspect.State != SuspectState.Restrained) yield return .2f;
            Check(suspect.State == SuspectState.Restrained, "Assistant did not complete handcuff assistance: " + squad.Officers[0].Status);
            Check(Vector3.Distance(squad.Officers[0].transform.position, suspect.transform.position) < 1.8f, "Assistant left the suspect during restraint");
            Check(squad.Score.Total == 330, "Assisted restraint score was incorrect or duplicated");
        }
    }
}
