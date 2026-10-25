using UnityEngine;
using UnityEngine.AI;

namespace Clearance
{
    [DefaultExecutionOrder(-20)]
    public sealed class OfficeEscort : MonoBehaviour
    {
        [SerializeField] OfficeEncounter encounter;
        [SerializeField] NavMeshAgent suspectAgent;
        NavMeshAgent assistant;
        NavMeshAgent employeeAgent;
        OfficePerson employee;
        float nextPath, waitUntil;
        public bool Started { get; private set; }
        public bool CustodyComplete { get; private set; }
        public bool Returned { get; private set; }
        public bool IsActive => Started && !Returned;
        public string Status { get; private set; }
        public float SuspectSpeed => suspectAgent && suspectAgent.enabled && suspectAgent.isOnNavMesh ? suspectAgent.velocity.magnitude : 0;
        public void Configure(OfficeEncounter office, NavMeshAgent prisoner) { encounter = office; suspectAgent = prisoner; }
        void Awake() => assistant = GetComponent<NavMeshAgent>();
        void Update()
        {
            if (Returned || encounter.Failed || !assistant.enabled || !assistant.isOnNavMesh) return;
            if (!Started)
            {
                if (!encounter.AllIdentified || encounter.Suspect.State != SuspectState.Restrained) return;
                if (!employee) foreach (var person in encounter.People) if (person.IsCivilian) employee = person;
                if (!employee || !NavMesh.SamplePosition(employee.transform.position, out var employeeStart, 1, NavMesh.AllAreas)) return;
                var obstacle = encounter.Suspect.GetComponent<NavMeshObstacle>(); if (obstacle) obstacle.enabled = false;
                if (!NavMesh.SamplePosition(encounter.Suspect.transform.position, out var start, 1, NavMesh.AllAreas)) return;
                employeeAgent = employee.GetComponent<NavMeshAgent>();
                if (!employeeAgent) employeeAgent = employee.gameObject.AddComponent<NavMeshAgent>();
                employeeAgent.radius = .27f; employeeAgent.height = 1.8f;
                employeeAgent.speed = 1.1f; employeeAgent.acceleration = 3; employeeAgent.angularSpeed = 180;
                employeeAgent.stoppingDistance = .2f;
                employeeAgent.enabled = true; employeeAgent.Warp(employeeStart.position);
                var employeeCollider = employee.GetComponent<CapsuleCollider>(); if (employeeCollider) employeeCollider.isTrigger = true;
                employee.BeginEscort(employeeAgent);
                suspectAgent.enabled = true; suspectAgent.Warp(start.position);
                encounter.Suspect.GetComponent<CapsuleCollider>().isTrigger = true;
                Started = true; waitUntil = Time.time + .6f;
            }
            if (Time.time < waitUntil) { assistant.isStopped = true; return; }
            assistant.updateRotation = true; assistant.isStopped = false;
            assistant.speed = CustodyComplete ? 2.3f : 1.4f; assistant.acceleration = 3;
            assistant.stoppingDistance = CustodyComplete ? 1.3f : .2f;
            if (Time.time >= nextPath)
            {
                nextPath = Time.time + .2f;
                Vector3 goal = CustodyComplete ? encounter.Player.transform.position :
                    employee.transform.position - employee.transform.forward * 1.1f;
                if (NavMesh.SamplePosition(goal, out var destination, 1.5f, NavMesh.AllAreas)) assistant.SetDestination(destination.position);
                if (!CustodyComplete)
                {
                    suspectAgent.speed = 1.1f; suspectAgent.isStopped = false;
                    if (NavMesh.SamplePosition(encounter.CustodyPoint.position, out var destinationPoint, 1, NavMesh.AllAreas)) suspectAgent.SetDestination(destinationPoint.position);
                    employeeAgent.isStopped = false;
                    Vector3 employeeGoal = encounter.Suspect.transform.position - encounter.Suspect.transform.forward * 1.05f;
                    if (NavMesh.SamplePosition(employeeGoal, out var employeeDestination, 1, NavMesh.AllAreas)) employeeAgent.SetDestination(employeeDestination.position);
                }
            }
            if (!CustodyComplete)
            {
                Status = "Escorting suspect and employee / Moving to exit";
                if (Vector3.Distance(transform.position, encounter.Suspect.transform.position) > 3.8f ||
                    Vector3.Distance(employee.transform.position, encounter.Suspect.transform.position) > 2.3f) suspectAgent.isStopped = true;
                if (Vector3.Distance(transform.position, employee.transform.position) > 2.6f) employeeAgent.isStopped = true;
                if (Vector3.Distance(encounter.Suspect.transform.position, encounter.CustodyPoint.position) < .65f &&
                    Vector3.Distance(employee.transform.position, encounter.CustodyPoint.position) < 1.5f &&
                    Vector3.Distance(transform.position, employee.transform.position) < 2.1f)
                {
                    CustodyComplete = true;
                    suspectAgent.isStopped = true; encounter.Suspect.gameObject.SetActive(false);
                    employeeAgent.isStopped = true; employee.gameObject.SetActive(false);
                    encounter.Score.Record("custody", 0, "Suspect handed into custody and employee safely evacuated at the exit; door stayed closed");
                }
            }
            else
            {
                Status = "Returning to you after custody handoff";
                if (Vector3.Distance(transform.position, encounter.Player.transform.position) < 2.1f)
                {
                    Returned = true; assistant.isStopped = true; assistant.ResetPath();
                    GetComponent<AssistingOfficer>().ResumeAfterEscort();
                    encounter.Score.Record("assistant-returned", 0, "Assistant returned and is ready to proceed upstairs");
                }
            }
        }
    }
}
