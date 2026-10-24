using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Clearance
{
    public enum SquadOrder { AwaitingChoice, Follow, CoverCorners }

    [DefaultExecutionOrder(-100)]
    public sealed class SquadCoordinator : MonoBehaviour
    {
        [SerializeField] FirstPersonOfficer leader;
        [SerializeField] GroundFloorMission mission;
        [SerializeField] DecisionScore score;
        [SerializeField] AssistingOfficer[] officers;
        [SerializeField] Transform[] coverPositions;
        [SerializeField] NavMeshData navigation;
        NavMeshDataInstance navigationInstance;
        public SquadOrder Order { get; private set; }
        public bool ChoiceMade => Order != SquadOrder.AwaitingChoice;
        public AssistingOfficer[] Officers => officers;
        public void UseSingleAssistant(AssistingOfficer assistant) => officers = new[] { assistant };
        public Transform[] CoverPositions => coverPositions;
        public FirstPersonOfficer Leader => leader;
        public GroundFloorMission Mission => mission;
        public DecisionScore Score => score;
        public string Status => Order == SquadOrder.Follow ? "You lead / Officers follow" :
            Order == SquadOrder.CoverCorners ? "Team first / Covering corners" : "Choose who leads the entry";
        public void Configure(FirstPersonOfficer player, GroundFloorMission m, DecisionScore s,
            AssistingOfficer[] team, Transform[] cover, NavMeshData data)
        { leader = player; mission = m; score = s; officers = team; coverPositions = cover; navigation = data; }
        void OnEnable() { if (navigation) navigationInstance = NavMesh.AddNavMeshData(navigation); }
        void OnDisable() { if (navigationInstance.valid) navigationInstance.Remove(); }
        public void LeadEntry()
        {
            if (mission.Complete) return;
            Order = SquadOrder.Follow;
            leader.SetCursor(true);
        }
        public void SendToCorners()
        {
            if (mission.Complete) return;
            Order = SquadOrder.CoverCorners;
            leader.SetCursor(true);
        }
        void Update()
        {
            if (mission.Complete || Keyboard.current == null) return;
            var keys = Keyboard.current;
            if (!ChoiceMade)
            {
                if (keys.lKey.wasPressedThisFrame) LeadEntry();
                else if (keys.cKey.wasPressedThisFrame) SendToCorners();
                return;
            }
            if (!leader.HasControl) return;
            if (keys.cKey.wasPressedThisFrame) SendToCorners();
            if (keys.bKey.wasPressedThisFrame) LeadEntry();
        }
    }
}
