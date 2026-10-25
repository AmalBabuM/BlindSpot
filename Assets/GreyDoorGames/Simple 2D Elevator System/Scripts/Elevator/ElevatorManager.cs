using System.Collections.Generic;
using UnityEngine;
namespace GreyDoorGames
{
    public class ElevatorManager : MonoBehaviour
    {
        [SerializeField] private ElevatorDataSO elevatorDataSO;
        [SerializeField] private ElevatorUI elevatorUIPrefab;
        [SerializeField] private ElevatorMovingPart elevatorMovingPrefab;

        private List<ElevatorMovingPart> elevators = new List<ElevatorMovingPart>();
        private List<ElevatorUI> elevatorUIs = new List<ElevatorUI>();

        [SerializeField] private float floorHeight = 264f;

        public ElevatorControlPanel elevatorControlPanel;

        private int floorCount;

        private void Awake()
        {
            elevatorControlPanel.OnFloorButtonPressed += RequestElevator;
            elevatorControlPanel.OnDoorButtonPressed += ToggleDoorAnimation;
        }
        private void OnDestroy()
        {
            elevatorControlPanel.OnFloorButtonPressed -= RequestElevator;
            elevatorControlPanel.OnDoorButtonPressed -= ToggleDoorAnimation;
        }
        public void SetFloorCount(int count)
        {
            floorCount = count;
        }
        public void CreateElevatorUI(RectTransform elevator, int floorIndex)
        {
            for (int i = 0; i < elevatorDataSO.elevatorCount; i++)
            {
                ElevatorUI elevatorUI = Instantiate(elevatorUIPrefab, elevator);
                elevatorUI.elevatorDoorIndex = i;
                elevatorUI.elevatorFloorIndex = floorIndex;
                elevatorUIs.Add(elevatorUI);
            }
        }

        public List<int> GetNeighbors(int index, int count)
        {
            List<int> neighbors = new List<int>();

            if (index - 1 >= 0)
                neighbors.Add(index - 1);

            if (index + 1 < count)
                neighbors.Add(index + 1);

            return neighbors;
        }

        public void CreateElevator(RectTransform elevatorContainer)
        {
            for (int i = 0; i < elevatorDataSO.elevatorCount; i++)
            {
                var elevatorID = i;
                ElevatorMovingPart elevator = Instantiate(elevatorMovingPrefab, elevatorContainer);
                elevator.SetElevatorID(elevatorID);
                elevator.AddPanelButtonListener(() => OnElevatorPanelButtonClicked(elevatorID));

                elevators.Add(elevator);
            }
        }
        private void OnElevatorPanelButtonClicked(int elevatorID)
        {
            ElevatorMovingPart elevator = GetElevator(elevatorID);
            if (elevator != null)
            {
                if (elevator.isInUse)
                    return;

                elevatorControlPanel.ShowPanel(elevatorID);
            }
        }
        private void ToggleDoorAnimation(bool status)
        {
            ElevatorMovingPart elevator = GetElevator(elevatorControlPanel.selectedElevatorId);
            if (elevator.isInUse)
            {
                return;
            }
            var elevatorUI = GetElevatorUI(elevator.GetElevatorID(), elevator.currentFloorIndex);
            if (elevatorUI != null)
            {
                elevator.SetElevatorUI(elevatorUI);
            }
            elevator.PlayDoorAnimation(status == true ? DoorAction.OpenDoor : DoorAction.CloseDoor);
        }

        public void RequestElevator(int elevatorId, int floorIndex)
        {
            var elevator = GetElevator(elevatorId);

            if (elevator == null)
                return;

            if (elevator.isInUse)
                return;

            Vector3 target = new Vector3(
                elevator.rectTransform.localPosition.x,
                floorIndex * floorHeight,
                elevator.rectTransform.localPosition.z
            );

            elevator.MoveToFloor(floorIndex, floorHeight, target, () =>
            {
                var elevatorUI = GetElevatorUI(elevatorId, elevator.currentFloorIndex);
                if (elevatorUI != null)
                {
                    elevator.SetElevatorUI(elevatorUI);
                }
                ChangeDirection(elevator);
            });
        }


        public void RequestElevator(int floorIndex, Direction direction)
        {
            var elevator = GetNearestElevator(floorIndex, direction);

            if (elevator == null)
                return;

            if (elevator.isInUse)
                return;

            Vector3 target = new Vector3(
                elevator.rectTransform.localPosition.x,
                floorIndex * floorHeight,
                elevator.rectTransform.localPosition.z
            );

            elevator.SetDirection(direction);

            elevator.MoveToFloor(floorIndex, floorHeight, direction, target, () =>
            {
                var elevatorUI = GetElevatorUI(elevator.GetElevatorID(), floorIndex);
                if (elevatorUI != null)
                {
                    elevator.SetElevatorUI(elevatorUI);
                }
               
                ChangeDirection(elevator);
            }, 
            () =>
            {
                var elevatorUI = GetElevatorUI(elevator.GetElevatorID(), elevator.currentFloorIndex);
                if (elevatorUI != null)
                {
                    elevator.SetElevatorUI(elevatorUI);
                }
            });

        }

        private void ChangeDirection(ElevatorMovingPart elevator)
        {
            elevator.PlayOnStopAnimation();
            if (elevator.currentFloorIndex == floorCount - 1)
            {
                elevator.SetDirection(Direction.Down);
            }
            else if (elevator.currentFloorIndex == 0)
            {
                elevator.SetDirection(Direction.Up);
            }
        }

        public List<ElevatorMovingPart> freeLifts = new List<ElevatorMovingPart>();
        private ElevatorMovingPart GetNearestElevator(int floorIndex)
        {
            ElevatorMovingPart bestElevator = null;
            float bestDistance = float.MaxValue;

            foreach (var elevator in elevators)
            {
                if (elevator.isInUse) continue;

                float distance = Mathf.Abs(elevator.currentFloorIndex - floorIndex);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestElevator = elevator;
                }
            }

            if (bestElevator != null)
                return bestElevator;

            bestDistance = float.MaxValue;

            foreach (var elevator in elevators)
            {
                float distance = Mathf.Abs(elevator.currentFloorIndex - floorIndex);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestElevator = elevator;
                }
            }

            return bestElevator;
        }

        private ElevatorMovingPart GetNearestElevator(int floorIndex, Direction direction)
        {
            ElevatorMovingPart bestElevator = null;
            float bestDistance = float.MaxValue;

            foreach (var elevator in elevators)
            {
                if (elevator.isInUse) continue;

                float distance = Mathf.Abs(elevator.currentFloorIndex - floorIndex);

                if (distance < bestDistance && (direction == elevator.currentMoveDirection || direction == Direction.None))
                {
                    bestDistance = distance;
                    bestElevator = elevator;
                }
            }

            if (bestElevator != null)
                return bestElevator;

            bestDistance = float.MaxValue;

            foreach (var elevator in elevators)
            {
                float distance = Mathf.Abs(elevator.currentFloorIndex - floorIndex);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestElevator = elevator;
                }
            }

            return bestElevator;
        }

        public ElevatorMovingPart GetElevator(int elevatorId)
        {
            foreach (var elevator in elevators)
            {
                if (elevator.GetElevatorID() == elevatorId)
                {
                    return elevator;
                }
            }
            return null;
        }
        public ElevatorUI GetElevatorUI(int elevatorId, int floorIndex)
        {
            foreach (var elevator in elevatorUIs)
            {
                if (elevator.elevatorDoorIndex == elevatorId && elevator.elevatorFloorIndex == floorIndex)
                {
                    return elevator;
                }
            }
            return null;
        }
    }
}