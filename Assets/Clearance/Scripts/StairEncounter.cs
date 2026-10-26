using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Clearance
{
    // The final encounter owns door entry, suspect response and the end-of-run report.
    public sealed class StairEncounter : MonoBehaviour
    {
        OfficeEncounter office;
        FirstPersonOfficer player;
        DecisionScore score;
        PushDoor[] doors;
        Animator animator;
        GameObject weapon, muzzle;
        PrisonerPresentation presentation;
        AudioSource gunAudio;
        AudioClip gunshot;
        Material muzzleMaterial;
        bool gunFired;
        float attackedAt = -1, handsAt, resolvedAt = -1;
        Vector3 cameraStart;
        Quaternion cameraRotation;
        string reportTitle, reportReason, reportScore;
        Vector2 reportScroll;
        public SuspectController Suspect { get; private set; }
        public bool CarefulEntry { get; private set; }
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public bool ReportOpen { get; private set; }
        public bool PlayerDying => attackedAt >= 0;
        public PushDoor[] Doors => doors;
        public string Objective => !Started ? "STAIRS: E opens the door slowly. Pushing in suddenly exposes you to an armed suspect." :
            !CarefulEntry ? "Ambushed at the stairs." : Suspect.State == SuspectState.Armed ? "Armed suspect ahead. Aim and press Q: Drop the gun, hands up!" :
            Suspect.State == SuspectState.HandsUp ? "Allow him to drop his weapon and raise his hands." :
            Suspect.State == SuspectState.VerifiedUnarmed ? "Weapon dropped. Aim and press Q: Kneel and surrender." :
            Suspect.State == SuspectState.Considering ? "Wait for him to kneel." :
            Suspect.State == SuspectState.Surrendered ? "Cover your assistant while he applies handcuffs. E nearby also restrains." : "Stair suspect restrained.";
        public void Configure(OfficeEncounter encounter)
        {
            office = encounter; player = office.Player; score = office.Score;
            var root = GameObject.Find("Open Door");
            doors = root ? root.GetComponentsInChildren<PushDoor>() : new PushDoor[0];
            foreach (var door in doors) door.StairEncounter = this;
            Suspect = Instantiate(office.Suspect, office.NextFloor.position, Quaternion.identity, transform);
            Suspect.name = "Armed_Stair_Suspect";
            var person = Suspect.GetComponent<OfficePerson>(); if (person) { person.enabled = false; Destroy(person); }
            var patrol = Suspect.GetComponent<SuspectPatrol>(); if (patrol) patrol.enabled = false;
            var agent = Suspect.GetComponent<NavMeshAgent>(); if (agent) agent.enabled = false;
            var obstacle = Suspect.GetComponent<NavMeshObstacle>(); if (obstacle) obstacle.enabled = false;
            if (NavMesh.SamplePosition(office.NextFloor.position, out var floor, 1.5f, NavMesh.AllAreas)) Suspect.transform.position = floor.position;
            presentation = Suspect.GetComponentInChildren<PrisonerPresentation>();
            presentation.ConfigureOffice(null);
            animator = presentation.Animator;
            animator.SetBool("Surrender", false); animator.SetBool("Escorted", false); animator.SetBool("Fallen", false);
            animator.Play("Idle", 0, 0);
            weapon = Suspect.Weapon;
            Suspect.ConfigureStairSuspect(score);
            gunAudio = Suspect.gameObject.AddComponent<AudioSource>(); gunAudio.playOnAwake = false; gunAudio.volume = .5f;
            gunshot = AudioClip.Create("Stair gunshot", 4410, 1, 22050, false);
            var samples = new float[4410]; var noise = new System.Random(73);
            for (int i = 0; i < samples.Length; i++) samples[i] = (float)(noise.NextDouble() * 2 - 1) * Mathf.Exp(-i / 380f);
            gunshot.SetData(samples, 0);
            if (weapon)
            {
                weapon.SetActive(true);
                // Keep the original hand-attached equipment; add a recognisable pistol grip.
                var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                grip.name = "PistolGrip"; Destroy(grip.GetComponent<Collider>());
                grip.transform.SetParent(weapon.transform, false);
                grip.transform.localPosition = new Vector3(0, -.6f, -.15f);
                grip.transform.localScale = new Vector3(.75f, 1.2f, .32f);
                grip.GetComponent<Renderer>().sharedMaterial = weapon.GetComponent<Renderer>().sharedMaterial;
                muzzle = GameObject.CreatePrimitive(PrimitiveType.Sphere); muzzle.name = "MuzzleFlash";
                Destroy(muzzle.GetComponent<Collider>()); muzzle.transform.SetParent(weapon.transform, false);
                muzzle.transform.localPosition = Vector3.forward * .65f; muzzle.transform.localScale = Vector3.one * 1.4f;
                muzzleMaterial = new Material(weapon.GetComponent<Renderer>().sharedMaterial);
                muzzleMaterial.color = new Color(1, .65f, .1f); muzzle.GetComponent<Renderer>().sharedMaterial = muzzleMaterial;
                muzzle.SetActive(false);
            }
            foreach (var collider in Suspect.GetComponentsInChildren<Collider>())
                if (collider is CapsuleCollider) collider.enabled = true;
            Suspect.gameObject.SetActive(false); // Activate with the encounter, not during reception.
        }
        void Activate()
        {
            if (Started || Finished) return;
            Started = true; Suspect.gameObject.SetActive(true);
            var look = player.transform.position - Suspect.transform.position; look.y = 0;
            if (look.sqrMagnitude > .01f) Suspect.transform.rotation = Quaternion.LookRotation(look);
        }
        public void OpenCarefully(PushDoor door)
        {
            if (Finished || Started && !CarefulEntry) return;
            if (!office.ReadyForNextFloor) return;
            CarefulEntry = true; Activate();
            score.Record("stairs-careful-entry", 100, "Used E to open the stair door carefully and kept control of entry");
            door.OpenCarefully(player.transform.position);
        }
        public void PlayerPushedDoor()
        {
            if (Finished || CarefulEntry) return;
            Activate();
            score.Record("stairs-reckless-entry", -300, "Pushed the stair door open suddenly without using E; exposed to an armed suspect");
            BeginDeath();
        }
        void BeginDeath()
        {
            Finished = true; attackedAt = Time.unscaledTime;
            cameraStart = player.View.transform.localPosition; cameraRotation = player.View.transform.localRotation;
            var guidance = FindFirstObjectByType<OfficeGuidance>(); if (guidance) guidance.EndGuidance();
            player.SetInteractionBlocked(true);
            foreach (var helper in FindObjectsByType<AssistingOfficer>(FindObjectsSortMode.None))
            { helper.enabled = false; if (helper.Agent.enabled && helper.Agent.isOnNavMesh) helper.Agent.isStopped = true; }
        }
        public string Command(SuspectController target)
        {
            if (Finished || !CarefulEntry || target != Suspect) return Objective;
            if (Suspect.State == SuspectState.Armed)
            {
                handsAt = Time.time;
                Suspect.Command();
                if (weapon)
                {
                    var dropped = Instantiate(weapon, Suspect.transform.position + Suspect.transform.right * .65f + Vector3.up * .08f,
                        Quaternion.Euler(0, 35, 90));
                    dropped.name = "Dropped_Stair_Pistol"; dropped.SetActive(true);
                    var flash = dropped.transform.Find("MuzzleFlash"); if (flash) flash.gameObject.SetActive(false);
                }
                return "OFFICER: Drop the gun! Raise your hands!";
            }
            return Suspect.Command() ? "OFFICER: Kneel and surrender!" : Objective;
        }
        void Update()
        {
            if (PlayerDying)
            {
                float elapsed = Time.unscaledTime - attackedAt;
                if (muzzle) muzzle.SetActive(elapsed > .2f && elapsed < .32f);
                if (elapsed >= .2f && !gunFired) { gunAudio.PlayOneShot(gunshot); gunFired = true; }
                float fall = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .25f) / 1.15f));
                player.View.transform.localPosition = Vector3.Lerp(cameraStart, new Vector3(cameraStart.x, .23f, cameraStart.z), fall);
                player.View.transform.localRotation = Quaternion.Slerp(cameraRotation, Quaternion.Euler(-12, 0, 72), fall);
                if (!ReportOpen && elapsed >= 3.5f) ShowReport(false, "An armed suspect shot you after the sudden stair entry.");
                return;
            }
            if (!Started || Finished) return;
            if (Suspect.State == SuspectState.HandsUp && Time.time - handsAt > 1.2f) Suspect.InspectForWeapons(2, true);
            if (Suspect.State == SuspectState.Restrained)
            {
                if (resolvedAt < 0) resolvedAt = Time.time;
                if (Time.time - resolvedAt > .8f)
                {
                    score.Record("stairs-secured", 200, "Armed stair suspect surrendered and was handcuffed; both areas secured");
                    ShowReport(true, "Reception cleared, employee protected, and the armed stair suspect restrained.");
                }
            }
            else if (Suspect.State == SuspectState.Down || Suspect.State == SuspectState.Stunned)
            {
                if (presentation.FailureNotificationReady) ShowReport(false, "The stair suspect was injured. The final objective required surrender and handcuffs.");
            }
        }
        void ShowReport(bool success, string reason)
        {
            Finished = true; ReportOpen = true;
            reportTitle = success ? "MISSION COMPLETE" : "MISSION FAILED";
            reportReason = reason; reportScore = "DECISION POINTS   " + score.Total;
            var guidance = FindFirstObjectByType<OfficeGuidance>(); if (guidance) guidance.EndGuidance();
            player.SetInteractionBlocked(true); Time.timeScale = 0;
        }
        void LateUpdate()
        {
            if (!Started || !animator || !animator.isHuman || Suspect.State != SuspectState.Armed) return;
            AimArm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
            AimArm(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            AimArm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
            AimArm(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        }
        void AimArm(HumanBodyBones bone, HumanBodyBones child)
        {
            var joint = animator.GetBoneTransform(bone); var end = animator.GetBoneTransform(child);
            if (!joint || !end) return;
            var target = player.View.transform.position - joint.position;
            if (target.sqrMagnitude > .01f) joint.rotation = Quaternion.FromToRotation(end.position - joint.position, target) * joint.rotation;
        }
        void OnGUI()
        {
            if (!Finished) return;
            FieldUi.Prepare(); FieldUi.Opacity = 1;
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            float scale = Screen.height / 720f, width = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale); GUI.depth = -80; GUI.color = Color.white;
            if (PlayerDying)
            {
                float elapsed = Time.unscaledTime - attackedAt;
                float pulse = .1f + .12f * (.5f + .5f * Mathf.Sin(elapsed * 5));
                FieldUi.Rounded(new Rect(0, 0, width, 720), new Color(.4f, 0, .015f, Mathf.Clamp01(elapsed / 1.5f) * (.4f + pulse)), 0);
                FieldUi.Rounded(new Rect(0, 0, width, 720), new Color(0, 0, 0, Mathf.Clamp01((elapsed - 1) / 3) * .85f), 0);
            }
            if (ReportOpen)
            {
                float x = width * .5f - 320;
                FieldUi.Panel(new Rect(x, 75, 640, 570));
                GUI.Label(new Rect(x + 28, 96, 584, 38), reportTitle, FieldUi.Title);
                GUI.Label(new Rect(x + 28, 140, 584, 32), reportScore, FieldUi.Eyebrow);
                GUI.Label(new Rect(x + 28, 180, 584, 65), reportReason, FieldUi.Body);
                GUI.Label(new Rect(x + 28, 248, 584, 24), "DECISION REVIEW · GOOD ACTIONS AND ISSUES", FieldUi.Eyebrow);
                reportScroll = GUI.BeginScrollView(new Rect(x + 28, 284, 584, 240), reportScroll,
                    new Rect(0, 0, 558, score.Timeline.Count * 54));
                for (int i = 0; i < score.Timeline.Count; i++) GUI.Label(new Rect(0, i * 54, 550, 50), score.Timeline[i], FieldUi.Small);
                GUI.EndScrollView();
                if (FieldUi.Button(new Rect(x + 28, 557, 278, 50), "Retry mission", true))
                { Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
                if (FieldUi.Button(new Rect(x + 324, 557, 288, 50), "Back to Menu")) PatrolPause.BackToMenu();
            }
            GUI.matrix = matrix; GUI.color = color; GUI.depth = depth;
        }
        void OnDestroy()
        { if (ReportOpen) Time.timeScale = 1; if (gunshot) Destroy(gunshot); if (muzzleMaterial) Destroy(muzzleMaterial); }
    }
}
