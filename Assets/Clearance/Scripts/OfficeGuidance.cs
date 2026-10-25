using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Clearance
{
    public enum OfficeBriefing { Visibility, Reception, HandsUp, Identify, Kneel, Restraint, Escort, Stairs, Failed }

    // Owns the guided decision gates; MissionHud only renders their current state.
    [DefaultExecutionOrder(200)]
    public sealed class OfficeGuidance : MonoBehaviour
    {
        OfficeEncounter office;
        OfficerActions actions;
        uint shown;
        float resumeTimeScale = 1;
        public bool CardOpen { get; private set; }
        public bool FlashlightLearned { get; private set; }
        public OfficeBriefing Step { get; private set; }
        public string Title { get; private set; }
        public string Instruction { get; private set; }
        public string PrimaryChoice { get; private set; }
        public string SecondaryChoice { get; private set; }
        public string Hint { get; private set; } = "Approach reception carefully.";
        public bool DarkArrival => CardOpen && Step == OfficeBriefing.Visibility;
        public void Configure(OfficeEncounter encounter, OfficerActions controls) { office = encounter; actions = controls; }
        void Start()
        {
            if (!office) { enabled = false; return; }
            actions.SetFlashlight(false);
            Show(OfficeBriefing.Visibility);
        }
        void Update()
        {
            if (CardOpen)
            {
                if (Step == OfficeBriefing.Visibility && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                    TurnOnFlashlight();
                return;
            }
            if (office.Player.InteractionBlocked) return;
            if (office.Failed) ShowOnce(OfficeBriefing.Failed);
            else if (office.ReadyForNextFloor) ShowOnce(OfficeBriefing.Stairs);
            else if (office.Suspect.State == SuspectState.Restrained || office.Escort.Started) ShowOnce(OfficeBriefing.Escort);
            else if (office.Suspect.State == SuspectState.Surrendered) ShowOnce(OfficeBriefing.Restraint);
            else if (office.AllIdentified && office.Suspect.State == SuspectState.VerifiedUnarmed) ShowOnce(OfficeBriefing.Kneel);
            else if (office.CommandGiven && !office.AllIdentified) ShowOnce(OfficeBriefing.Identify);
            else if (office.SuspectRevealed && !office.CommandGiven && Time.time - office.RevealedAt >= .5f) ShowOnce(OfficeBriefing.HandsUp);
        }
        void ShowOnce(OfficeBriefing step) { if ((shown & (1u << (int)step)) == 0) Show(step); }
        void Show(OfficeBriefing step)
        {
            if (!CardOpen)
            {
                resumeTimeScale = Time.timeScale;
                Time.timeScale = 0;
                office.Player.SetInteractionBlocked(true);
            }
            CardOpen = true; Step = step; shown |= 1u << (int)step;
            SecondaryChoice = "Review controls";
            switch (step)
            {
                case OfficeBriefing.Visibility:
                    Title = "LOW VISIBILITY";
                    Instruction = "You have entered a dark reception. Use your flashlight before moving forward. Press F, or choose Turn on flashlight below.";
                    PrimaryChoice = "F / Turn on flashlight"; break;
                case OfficeBriefing.Reception:
                    Title = "VISIBILITY RESTORED";
                    Instruction = "Good. Your light is on. Advance slowly toward the reception desk while looking at it. Your assistant follows automatically.";
                    PrimaryChoice = "Continue carefully"; Hint = "Move toward the desk while facing it."; break;
                case OfficeBriefing.HandsUp:
                    Title = "MOVEMENT BEHIND THE DESK";
                    Instruction = "One person has stood up; the other stays on his knees. Give both a clear hands-up command before using force. Aim at either person and press Q.";
                    PrimaryChoice = "Give a clear command"; Hint = "Aim at either person. Q: Both of you, hands up."; break;
                case OfficeBriefing.Identify:
                    Title = "VERIFY BOTH PEOPLE";
                    Instruction = "Keep your flashlight on. Move around the desk and look at each person from within 3.5 metres. Hold your view steady until each identity is confirmed.";
                    PrimaryChoice = "Check both identities"; Hint = "Flashlight-check both people nearby."; break;
                case OfficeBriefing.Kneel:
                    Title = "IDENTITIES CONFIRMED";
                    Instruction = "The employee is a civilian. The other person matches the suspect briefing and has no visible weapon. Aim at the identified suspect and press Q to order him to kneel.";
                    PrimaryChoice = "Order the suspect to surrender"; Hint = "Aim at the suspect. Q: Kneel and surrender."; break;
                case OfficeBriefing.Restraint:
                    Title = "SUSPECT COMPLYING";
                    Instruction = "Keep the employee safe and cover the area while your assistant moves behind the kneeling suspect to apply handcuffs.";
                    PrimaryChoice = "Cover the assistant"; Hint = "Cover the area while the assistant applies handcuffs."; break;
                case OfficeBriefing.Escort:
                    Title = "READY TO ESCORT";
                    Instruction = "The suspect is restrained. Let your assistant accompany the suspect and employee to the exit. Stay alert until the assistant returns.";
                    PrimaryChoice = "Escort both people to safety"; Hint = "Wait for both people to reach the exit and your assistant to return."; break;
                case OfficeBriefing.Stairs:
                    Title = "RECEPTION SECURED";
                    Instruction = "Both people have reached the exit and your assistant is back. Continue through the push door to the stairs. The lift is unsafe.";
                    PrimaryChoice = "Continue to the stairs"; Hint = "Push the door and follow the stairs marker."; break;
                default:
                    Title = "PROCEDURE FAILED";
                    Instruction = "Someone was injured. Restart the encounter and verify identities before using force.";
                    PrimaryChoice = "Restart encounter"; break;
            }
        }
        public void TurnOnFlashlight()
        {
            if (!CardOpen || Step != OfficeBriefing.Visibility) return;
            actions.SetFlashlight(true); FlashlightLearned = true;
            office.Score.Record("tutorial-flashlight", 50, "Used the flashlight before advancing");
            Show(OfficeBriefing.Reception);
        }
        public void Continue()
        {
            if (!CardOpen) return;
            if (Step == OfficeBriefing.Visibility) { TurnOnFlashlight(); return; }
            if (Step == OfficeBriefing.Failed)
            {
                Close(); SceneManager.LoadScene(SceneManager.GetActiveScene().name); return;
            }
            if (Step == OfficeBriefing.Restraint) office.Score.Record("cover-restraint", 25, "Chose to cover the assistant during restraint");
            if (Step == OfficeBriefing.Escort) office.Score.Record("escort-both", 50, "Chose to escort the civilian and suspect safely");
            Close();
        }
        void Close()
        {
            if (!CardOpen) return;
            CardOpen = false; Time.timeScale = resumeTimeScale;
            office.Player.SetInteractionBlocked(false);
        }
        void OnDisable() { if (office) Close(); }
    }
}
