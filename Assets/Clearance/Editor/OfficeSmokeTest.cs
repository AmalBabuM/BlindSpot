using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class OfficeSmokeTest
    {
        static IEnumerator routine; static double next;
        static OfficeSmokeTest() => EditorApplication.playModeStateChanged += Changed;
        [MenuItem("Clearance/Test Office Encounter")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool("Clearance.OfficeTest", true); EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Clearance.OfficeTest", false))
            { SessionState.SetBool("Clearance.OfficeTest", false); routine = Run(); next = 0; EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        }
        static void Tick()
        {
            if (Time.unscaledTime < next) return;
            try
            {
                if (routine.MoveNext()) next = Time.unscaledTime + Convert.ToDouble(routine.Current);
                else Finish("PASS: automatic assistant follow; flashlight/controls/decision gates and one-time reward notifications; both start kneeling; only suspect stands; identification, restraint, two-person escort, return, stairs, doors and saved lighting preserved.");
            }
            catch (Exception e) { Debug.LogException(e); Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            EditorApplication.update -= Tick; File.WriteAllText("Logs/Office-smoke-test.txt", result);
            Debug.Log(result); EditorApplication.ExitPlaymode();
        }
        static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
        static IEnumerator Run()
        {
            Application.runInBackground = true; yield return .8f;
            var office = UnityEngine.Object.FindFirstObjectByType<OfficeEncounter>();
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadCoordinator>();
            Check(office && office.People.Length == 2, "Office participants were not configured");
            var hud = UnityEngine.Object.FindFirstObjectByType<MissionHud>();
            var guidance = hud.Guidance;
            var actions = office.Player.GetComponent<OfficerActions>();
            Check(squad.ChoiceMade && squad.Order == SquadOrder.Follow, "Assistant did not follow automatically");
            Check(guidance, "Guidance was not created");
            if (guidance.FirstCardShownAt < 0)
                Check(guidance.IntroPending && !guidance.CardOpen && hud.CardOpacity == 0, "Instruction appeared before the four-second delay");
            yield return 4.2f;
            Check(guidance.FirstCardShownAt - guidance.ArrivalVisibleAt >= 4, "Instruction did not wait four seconds after the first visible frame");
            Check(guidance && guidance.CardOpen && guidance.Step == OfficeBriefing.Visibility && Time.timeScale == 0 && office.Player.InteractionBlocked,
                "Torch instruction failed to pause controls");
            Check(guidance.Instruction.Contains("reception lights are on") && !actions.FlashlightOn, "Startup did not distinguish room lighting from the player's torch");
            Check(hud.CardOpacity > .95f && guidance.SecondaryChoice == "Close", "Instruction fade-in / Close choice missing");
            Check(!actions.FlashlightOn && !hud.ControlsOpen, "Flashlight/help did not start closed");
            hud.ToggleControls(); Check(hud.ControlsOpen, "Controls drawer failed to open");
            hud.ToggleControls(); Check(!hud.ControlsOpen && office.Player.InteractionBlocked, "Closing controls bypassed instruction gate");
            int beforeClose = office.Score.Total;
            guidance.Dismiss();
            Check(!guidance.CardOpen && guidance.CurrentTask.Contains("Press F") && office.Score.Total == beforeClose, "Close bypassed task or removed its help instruction");
            yield return .6f;
            Check(hud.CardOpacity < .01f && Time.timeScale == 1, "Dismissed instruction failed to fade out / resume");
            int beforeLight = office.Score.Total;
            guidance.TurnOnFlashlight();
            Check(!guidance.CardOpen && actions.FlashlightOn, "Next notification appeared without a gap");
            yield return 1.6f;
            Check(actions.FlashlightOn && guidance.Step == OfficeBriefing.Reception && office.Score.Total == beforeLight + 50, "Flashlight action/reward missing");
            guidance.TurnOnFlashlight(); Check(office.Score.Total == beforeLight + 50, "Flashlight reward could be farmed");
            guidance.Continue();
            Check(!guidance.CardOpen && Time.timeScale == 1 && !office.Player.InteractionBlocked, "Continue failed to resume patrol");
            yield return .6f;
            Check(hud.ToastVisible, "Good decision notification missing");
            hud.ToggleControls(); Check(office.Player.InteractionBlocked, "Help drawer did not release mouse safely");
            hud.ToggleControls(); Check(!office.Player.InteractionBlocked, "Closing help failed to resume controls");
            office.Player.enabled = false; actions.enabled = false;
            office.Player.GetComponent<CharacterController>().enabled = false;
            var admin = office.People[0]; var adminAnimator = admin.Animator;
            Check(!office.SuspectRevealed && admin.IsKneeling && office.People[1].IsKneeling, "People did not start sheltering on their knees");
            Check(adminAnimator.GetCurrentAnimatorStateInfo(0).IsName("Kneel") && office.People[1].Animator.GetCurrentAnimatorStateInfo(0).IsName("Kneel"), "Initial kneeling animations missing");
            office.Command(admin); Check(!office.CommandGiven, "Command skipped approach reveal");
            office.Player.transform.position = office.Suspect.transform.position + Vector3.left * 3;
            Vector3 deskDirection = GameObject.Find("Front Desk").transform.position - office.Player.View.transform.position; deskDirection.y = 0;
            office.Player.View.transform.rotation = Quaternion.LookRotation(-deskDirection);
            yield return .3f;
            Check(!office.SuspectRevealed, "Suspect stood while player faced away from desk");
            office.Player.View.transform.rotation = Quaternion.LookRotation(deskDirection);
            yield return .8f;
            Check(office.SuspectRevealed && admin.IsKneeling && !office.People[1].IsKneeling, "Approach did not reveal only suspect");
            if (Time.time - office.RevealedAt < 3.5f) Check(!guidance.CardOpen, "Suspect instruction interrupted the observation delay");
            yield return 2.9f;
            Check(guidance.CardOpen && guidance.Step == OfficeBriefing.HandsUp, "Desk command instruction missing");
            Check(Time.time - office.RevealedAt >= 3.5f, "Suspect instruction appeared before stand-up plus observation delay");
            guidance.Continue();
            Check(office.People[1].Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Suspect failed to stand from cover");
            Check(Lightmapping.lightingSettings && Lightmapping.lightingSettings.bakedGI && !Lightmapping.lightingSettings.realtimeGI, "Lighting is not configured for baking");
            var door = UnityEngine.Object.FindFirstObjectByType<PushDoor>();
            Check(door && door.GetComponent<Rigidbody>().isKinematic, "Push doors have no controlled 3D pivot");
            var collider = door.GetComponentInChildren<Collider>();
            Quaternion initial = door.transform.localRotation;
            Vector3 lever = collider.bounds.center - door.transform.position;
            door.Push(collider.bounds.center, Vector3.Cross(Vector3.up, lever));
            yield return .8f;
            if (Quaternion.Angle(initial, door.transform.localRotation) < 1)
            { door.Push(collider.bounds.center, -Vector3.Cross(Vector3.up, lever)); yield return .8f; }
            bool doorWorked = Quaternion.Angle(initial, door.transform.localRotation) > 1;
            var path = new NavMeshPath();
            Check(NavMesh.CalculatePath(office.Suspect.transform.position, office.CustodyPoint.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                "No complete navigation route from suspect to outside custody point");
            office.Player.transform.position = office.People[0].transform.position + Vector3.left * 2;
            Check(!office.People[0].Identified && !office.People[1].Identified, "Identities were revealed before inspection");
            office.Command(office.People[0]);
            Check(office.People[0].HandsRaised && office.People[1].HandsRaised && office.Suspect.State == SuspectState.HandsUp, "Group command did not affect both people");
            yield return 1.1f;
            Check(guidance.CardOpen && guidance.Step == OfficeBriefing.Identify, "Identification instruction missing");
            guidance.Continue();
            yield return .9f;
            Check(adminAnimator.isHuman && adminAnimator.GetBoneTransform(HumanBodyBones.LeftHand).position.y > adminAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y,
                "Admin did not play hands-up pose");
            Check(admin.IsKneeling && adminAnimator.GetCurrentAnimatorStateInfo(0).IsName("Kneel"), "Employee stood during hands-up command");
            office.People[1].Inspect(2, true); office.Command(office.People[1]);
            Check(office.Suspect.State == SuspectState.VerifiedUnarmed, "Suspect could kneel before the admin was identified");
            admin.Inspect(2, false); Check(!admin.Identified, "Civilian identification accepted without flashlight");
            admin.Inspect(2, true); Check(office.AllIdentified, "Identification did not finish");
            yield return 1.1f;
            Check(guidance.CardOpen && guidance.Step == OfficeBriefing.Kneel, "Verified-suspect instruction missing");
            guidance.Continue();
            office.Command(admin); Check(office.Suspect.State == SuspectState.VerifiedUnarmed, "Civilian was treated as suspect");
            office.Command(office.People[1]); Check(office.Suspect.State == SuspectState.Considering, "Verified suspect did not kneel");
            float until = Time.time + 45;
            bool covered = false;
            while (Time.time < until && !office.Escort.Started)
            {
                if (guidance.CardOpen)
                {
                    Check(guidance.Step == OfficeBriefing.Restraint, "Unexpected instruction before cuffing");
                    covered = true; guidance.Continue();
                }
                yield return .2f;
            }
            Check(office.Suspect.State == SuspectState.Restrained && office.Escort.Started, "Assistant did not restrain and start escort: " + squad.Officers[0].Status);
            Check(covered, "Cover-assistant decision was never offered");
            if (guidance.CardOpen) { Check(guidance.Step == OfficeBriefing.Escort, "Escort decision missing"); guidance.Continue(); }
            until = Time.time + 60;
            bool suspectLed = false;
            var exitDoors = Array.FindAll(UnityEngine.Object.FindObjectsByType<InteractableDoor>(FindObjectsSortMode.None), d => d.enabled && d.transform.position.x < -33);
            while (Time.time < until && !office.Escort.CustodyComplete)
            {
                if (guidance.CardOpen) guidance.Continue();
                Vector3 direction = office.CustodyPoint.position - office.Suspect.transform.position;
                if (direction.magnitude > 2)
                    suspectLed |= Vector3.Dot(office.Suspect.transform.position - squad.Officers[0].transform.position, direction.normalized) > .35f;
                yield return .2f;
            }
            Check(suspectLed, "Suspect did not walk in front of the accompanying assistant");
            Check(office.Escort.CustodyComplete && !office.Suspect.gameObject.activeSelf,
                "Suspect was not delivered outside: assistant=" + squad.Officers[0].transform.position + ", suspect=" + office.Suspect.transform.position);
            Check(admin.IsEscorted && !admin.gameObject.activeSelf, "Employee was left behind instead of evacuated with suspect");
            until = Time.time + 35;
            while (Time.time < until && !office.Escort.Returned) yield return .2f;
            Check(office.Escort.Returned && office.ReadyForNextFloor && office.Objective.Contains("NEXT FLOOR"), "Assistant did not return / next-floor objective missing");
            yield return .1f;
            Check(guidance.CardOpen && guidance.Step == OfficeBriefing.Stairs, "Next-floor instruction missing");
            guidance.Continue();
            Check(!admin.Harmed && squad.Order == SquadOrder.Follow, "Admin was harmed or assistant did not resume assisting");
            foreach (var exit in exitDoors) Check(!exit.IsPassable, "Custody escort opened the exit door");
            int score = office.Score.Total;
            office.Suspect.Restrain(); admin.Inspect(2, true); yield return .3f;
            Check(office.Score.Total == score, "Repeated actions farmed office rewards");
            Check(doorWorked, "Stair door did not swing when pushed");
            office.Player.SetCursor(false);
        }
    }
}
