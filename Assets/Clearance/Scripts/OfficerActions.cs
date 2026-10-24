using UnityEngine;
using UnityEngine.InputSystem;

namespace Clearance
{
    public sealed class OfficerActions : MonoBehaviour
    {
        [SerializeField] FirstPersonOfficer officer;
        [SerializeField] GroundFloorMission mission;
        [SerializeField] DecisionScore score;
        [SerializeField] Light flashlight;
        [SerializeField] float commandRange = 10;
        [SerializeField] float restraintRange = 2.4f;
        [SerializeField] float stunRange = 7;
        float nextUse;
        int shotNumber;
        public bool Lethal { get; private set; }
        public bool FlashlightOn => flashlight && flashlight.enabled;
        public string Prompt { get; private set; }
        public string Message { get; private set; } = "Ground floor: check both marked corners, resolve the suspect, then secure the room.";
        public float LastShotAt { get; private set; } = -100;
        public void Configure(FirstPersonOfficer p, GroundFloorMission m, DecisionScore s, Light light)
        { officer = p; mission = m; score = s; flashlight = light; }
        void Update()
        {
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (keys == null || mouse == null || !officer.HasControl) return;
            if (keys.fKey.wasPressedThisFrame) flashlight.enabled = !flashlight.enabled;
            if (keys.digit1Key.wasPressedThisFrame) Lethal = false;
            if (keys.digit2Key.wasPressedThisFrame) Lethal = true;
            var view = officer.View.transform;
            bool hitSomething = Physics.Raycast(view.position, view.forward, out var hit, 30, ~0, QueryTriggerInteraction.Ignore);
            var suspect = hitSomething ? hit.collider.GetComponentInParent<SuspectController>() : null;
            var door = hitSomething ? hit.collider.GetComponentInParent<InteractableDoor>() : null;
            Prompt = suspect ? suspect.Status : door && hit.distance < 3 ? "E: Toggle door" : "";
            var encounterSuspect = mission.Suspect;
            if (suspect == encounterSuspect && hit.distance <= 3.5f)
                encounterSuspect.InspectForWeapons(Time.deltaTime, FlashlightOn);
            else encounterSuspect.ResetWeaponInspection();
            if (!suspect && encounterSuspect.State == SuspectState.Alerted &&
                Vector3.Distance(transform.position, encounterSuspect.transform.position) <= commandRange)
                Prompt = "He is watching you | Aim at him and press Q: Raise your hands!";
            if (keys.qKey.wasPressedThisFrame)
            {
                bool kneelCommand = suspect && suspect.State == SuspectState.VerifiedUnarmed;
                Message = suspect && hit.distance <= commandRange && suspect.Command()
                    ? kneelCommand ? "OFFICER: Get on your knees. Surrender!" : "OFFICER: Raise your hands!"
                    : encounterSuspect.State == SuspectState.HandsUp ? "First inspect him with the flashlight within 3.5 m."
                    : "Approach until he notices you, then aim at him and press Q.";
            }
            if (keys.eKey.wasPressedThisFrame)
            {
                if (suspect && hit.distance <= restraintRange && suspect.State == SuspectState.Surrendered) suspect.Restrain();
                else if (door && hit.distance < 3) door.Toggle();
                else Message = mission.TrySecure();
            }
            if (!mouse.leftButton.wasPressedThisFrame || Time.time < nextUse) return;
            nextUse = Time.time + .65f;
            LastShotAt = Time.time;
            shotNumber++;
            if (suspect && !suspect.Resolved && (Lethal || hit.distance <= stunRange))
            {
                bool precise = hit.point.y > suspect.transform.position.y + .7f && hit.point.y < suspect.transform.position.y + 1.5f;
                suspect.Hit(Lethal, precise);
                Message = Lethal ? "Firearm discharged. Decision recorded." : "Stun connected.";
            }
            else
            {
                if (Lethal) score.Record("miss-" + shotNumber, score.Rules.unjustifiedShot, "Unjustified firearm discharge");
                Message = "No valid hit. Stun range: 7 m. Check your aim and surroundings.";
            }
        }
    }
}
