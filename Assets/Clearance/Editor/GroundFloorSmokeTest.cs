using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class GroundFloorSmokeTest
    {
        const string Pending = "Clearance.SmokeTest";
        static IEnumerator run;
        static double next;
        static GroundFloorSmokeTest() => EditorApplication.playModeStateChanged += OnPlayState;
        [MenuItem("Clearance/Run Ground Floor Smoke Test %#t")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            {
                SessionState.SetBool(Pending, false);
                run = Run(); next = 0;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (run.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(run.Current);
                else Finish("PASS: ground-floor integration checks completed.");
            }
            catch (Exception e) { Debug.LogException(e); Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/GroundFloor-smoke-test.txt", result);
            Debug.Log("CLEARANCE TEST: " + result);
            EditorApplication.ExitPlaymode();
        }
        static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
        static IEnumerator Run()
        {
            Application.runInBackground = true;
            yield return .5f;
            var mission = UnityEngine.Object.FindFirstObjectByType<GroundFloorMission>();
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonOfficer>();
            var score = UnityEngine.Object.FindFirstObjectByType<DecisionScore>();
            Check(mission && player && score, "Missing scene wiring");
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadCoordinator>();
            if (squad)
            {
                squad.LeadEntry();
                foreach (var teammate in squad.Officers) teammate.gameObject.SetActive(false);
                squad.enabled = false;
            }
            player.enabled = false;
            player.GetComponent<OfficerActions>().enabled = false;
            player.SetCursor(true);
            Check(Physics.Raycast(player.transform.position + Vector3.up, Vector3.down, 2), "No floor below spawn");
            Check(!mission.Complete && mission.TrySecure() != "Ground floor complete.", "Premature clearance accepted");
            var patrol = mission.Suspect.GetComponent<SuspectPatrol>();
            var presentation = mission.Suspect.GetComponentInChildren<PrisonerPresentation>();
            if (patrol)
            {
                Check(mission.Suspect.State == SuspectState.Patrolling, "Suspect alerted before player approached");
                Check(!mission.Suspect.Command(), "Patrolling suspect accepted Q before becoming alert");
                bool visitedA = false, visitedB = false;
                bool playedWalk = false;
                float deadline = Time.time + 14;
                while (Time.time < deadline && !(visitedA && visitedB))
                {
                    visitedA |= Vector3.Distance(mission.Suspect.transform.position, patrol.PointA.position) < .15f;
                    visitedB |= Vector3.Distance(mission.Suspect.transform.position, patrol.PointB.position) < .15f;
                    playedWalk |= presentation && presentation.UsesAnimator && presentation.Animator.GetCurrentAnimatorStateInfo(0).IsName("Walking");
                    yield return .1f;
                }
                Check(visitedA && visitedB, "Patrol did not reach both A and B");
                if (presentation && presentation.UsesAnimator) Check(playedWalk, "Patrol never played the walking clip");
            }
            // Use the room's observation doorway for this check, regardless of the user's spawn placement.
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = new Vector3(-12, .06f, 2.3f);
            player.View.transform.LookAt(mission.Suspect.transform.position + Vector3.up * 1.1f);
            Check(Physics.Raycast(player.View.transform.position, player.View.transform.forward, out var hit, 15)
                && hit.collider.GetComponentInParent<SuspectController>() == mission.Suspect,
                $"Suspect blocked from doorway: hit={hit.collider?.name}, suspect={mission.Suspect.transform.position}, collider={mission.Suspect.GetComponent<CapsuleCollider>().bounds}, camera={player.View.transform.position}");
            var first = mission.Corners[0];
            for (int i = 0; i < 50; i++) first.Observe(player.View, score, false);
            Check(!first.Checked, "Corner accepted with flashlight off");
            foreach (var corner in mission.Corners)
            {
                player.View.transform.LookAt(corner.transform);
                // Exercise observation even when the editor's Scene tab owns input focus.
                float until = Time.time + 1.1f;
                while (Time.time < until)
                {
                    corner.Observe(player.View, score, true);
                    yield return 0f;
                }
                Check(corner.Checked, "Observation marker unreachable: " + corner.Label);
            }
            Check(score.Total == 100, "Corner score or early-entry detection incorrect");
            score.Record(first.name, 50, "Duplicate check");
            Check(score.Total == 100, "Duplicate observation scored twice");
            if (patrol)
            {
                player.GetComponent<CharacterController>().enabled = false;
                player.transform.position = mission.Suspect.transform.position + Vector3.forward * 2;
                yield return .8f;
                Check(mission.Suspect.State == SuspectState.Alerted, "Proximity failed to alert the suspect");
                Check(mission.Suspect.Status.Contains("Q:"), "Alert state has no command prompt");
                Vector3 stopped = mission.Suspect.transform.position;
                yield return .5f;
                Check(Vector3.Distance(stopped, mission.Suspect.transform.position) < .01f, "Alerted suspect kept walking");
                if (presentation && presentation.UsesAnimator)
                    Check(presentation.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Alerted suspect did not play idle");
                Vector3 facing = player.transform.position - stopped; facing.y = 0;
                Check(Vector3.Angle(mission.Suspect.transform.forward, facing) < 5, "Alerted suspect did not look at player");
            }
            Check(mission.Suspect.Command(), "First command rejected");
            Check(!mission.Suspect.Command(), "Repeated command accepted");
            Check(mission.Suspect.State == SuspectState.HandsUp, "First Q skipped hands-up stage");
            yield return .8f;
            if (presentation && presentation.UsesAnimator)
            {
                Check(presentation.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "First command started kneeling too early");
                var a = presentation.Animator;
                Check(a.GetBoneTransform(HumanBodyBones.LeftHand).position.y > a.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y,
                    "First command did not raise the hands");
            }
            mission.Suspect.InspectForWeapons(2, false);
            Check(mission.Suspect.State == SuspectState.HandsUp, "Weapon inspection accepted without flashlight");
            mission.Suspect.InspectForWeapons(.7f, true);
            mission.Suspect.ResetWeaponInspection();
            Check(mission.Suspect.WeaponCheckProgress == 0, "Interrupted inspection did not reset");
            mission.Suspect.InspectForWeapons(.7f, true);
            Check(!mission.Suspect.Command(), "Second command accepted before inspection completed");
            mission.Suspect.InspectForWeapons(.6f, true);
            Check(mission.Suspect.State == SuspectState.VerifiedUnarmed, "Inspection did not reveal the kneel prompt");
            Check(mission.Suspect.Command(), "Verified kneel command rejected");
            Check(!mission.Suspect.Command(), "Kneel command can be repeated");
            Check(mission.Suspect.State == SuspectState.Considering, "Kneel command has no compliance phase");
            float surrenderDeadline = Time.time + 30;
            while (mission.Suspect.State == SuspectState.Considering && Time.time < surrenderDeadline) yield return .1f;
            Check(mission.Suspect.State == SuspectState.Surrendered, "Suspect never surrendered");
            var prisoner = mission.Suspect.GetComponentInChildren<PrisonerPresentation>();
            if (prisoner && prisoner.UsesAnimator)
            {
                var state = prisoner.Animator.GetCurrentAnimatorStateInfo(0);
                Check(state.IsName("Kneel") && state.normalizedTime >= .95f, "Surrender did not complete the kneeling clip");
                Check(!state.loop, "Kneeling clip must hold its final pose without looping");
                var hips = prisoner.Animator.GetBoneTransform(HumanBodyBones.Hips);
                Check(hips.position.y - mission.Suspect.transform.position.y < .8f, "Kneel animation ended standing instead of surrendering");
            }
            else if (prisoner)
            {
                var bones = prisoner.GetComponentsInChildren<Transform>();
                foreach (string side in new[] { "Left", "Right" })
                {
                    var hand = System.Array.Find(bones, b => b.name.EndsWith(":" + side + "Hand"));
                    var arm = System.Array.Find(bones, b => b.name.EndsWith(":" + side + "Arm"));
                    Check(hand && arm && hand.position.y > arm.position.y, side + " prisoner hand did not rise during surrender");
                }
            }
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = new Vector3(-12, .06f, -2.7f);
            yield return .1f;
            mission.Suspect.Restrain(); mission.Suspect.Restrain();
            Check(score.Total == 330, "Restraint scoring incorrect or farmable");
            mission.TrySecure();
            Check(mission.Complete && score.Total == 430, "Peaceful path cannot complete at expected score");
            Check(Cursor.lockState == CursorLockMode.None, "Result did not release cursor");
            var testObject = new GameObject("TemporaryAlternateOutcomeTest");
            var testScore = testObject.AddComponent<DecisionScore>(); testScore.Configure(score.Rules);
            var testSuspect = testObject.AddComponent<SuspectController>(); testSuspect.Configure(testScore, null, null, null);
            testSuspect.Hit(true, true);
            Check(testScore.Total == -300 && testSuspect.Resolved, "Unjustified firearm outcome incorrect");
            var second = new GameObject("TemporaryStunTest");
            var stunScore = second.AddComponent<DecisionScore>(); stunScore.Configure(score.Rules);
            var stunSuspect = second.AddComponent<SuspectController>(); stunSuspect.Configure(stunScore, null, null, null);
            stunSuspect.Hit(false, true); stunSuspect.Hit(false, true);
            Check(stunScore.Total == 250 && stunSuspect.State == SuspectState.Stunned, "Stun outcome incorrect or repeatable");
            if (presentation && presentation.UsesAnimator)
            {
                var clone = UnityEngine.Object.Instantiate(mission.Suspect.gameObject, new Vector3(-45, .05f, -4.5f), Quaternion.identity);
                var target = clone.GetComponent<SuspectController>();
                var fallPresentation = clone.GetComponentInChildren<PrisonerPresentation>();
                target.Configure(testScore, null, null, null);
                target.Hit(true, true);
                yield return .4f;
                Check(fallPresentation.Animator.GetCurrentAnimatorStateInfo(0).IsName("Fall"), "Bullet hit did not play the fall clip");
                Check(Quaternion.Angle(clone.transform.rotation, Quaternion.identity) < .1f, "Fall animation still tips the entire actor root");
                float fallDeadline = Time.time + 30;
                while (fallPresentation.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1 && Time.time < fallDeadline) yield return .1f;
                Check(!fallPresentation.Animator.GetCurrentAnimatorStateInfo(0).loop, "Fall animation loops instead of holding");
                Check(fallPresentation.Animator.GetBoneTransform(HumanBodyBones.Head).position.y - clone.transform.position.y < 1,
                    "Fall animation did not lower the character to the floor");
            }
        }
    }
}
