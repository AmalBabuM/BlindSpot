using System.Collections;
using UnityEngine;
namespace GreyDoorGames
{
    public class MainController : MonoBehaviour
    {
        [SerializeField] private FloorManager floorManager;
        [SerializeField] private ElevatorManager elevatorManager;
        private IEnumerator Start()
        {
            if (floorManager == null)
            {
                floorManager = FindFirstObjectByType<FloorManager>();
            }
            if (elevatorManager == null)
            {
                elevatorManager = FindFirstObjectByType<ElevatorManager>();
            }

            floorManager.Init();

            while (!floorManager.IsInitialized())
            {
                yield return null;
            }
            int floorIndex = 0;
            foreach (var floorUI in floorManager.FloorUIs)
            {
                if (floorUI.IsGroundFloor())
                {
                    elevatorManager.CreateElevator(floorUI.elevatorContainer);
                }
                elevatorManager.CreateElevatorUI(floorUI.elevatorUIContainer,floorIndex);
                floorUI.floorContentTransform.SetParent(floorUI.elevatorUIContainer);
                floorIndex++;
            }

            elevatorManager.SetFloorCount(floorManager.FloorUIs.Count);
            elevatorManager.elevatorControlPanel.InitializePanel(floorManager.FloorUIs.Count);

            foreach (var floor in floorManager.FloorUIs)
            {
                floor.OnUpClicked += HandleUpClicked;
                floor.OnDownClicked += HandleDownClicked;
            }
        }

        private void OnDestroy()
        {
            foreach (var floor in floorManager.FloorUIs)
            {
                floor.OnUpClicked -= HandleUpClicked;
                floor.OnDownClicked -= HandleDownClicked;
            }
        }

        private void HandleUpClicked(int floor)
        {
            elevatorManager.RequestElevator(floor, Direction.Up);
        }

        private void HandleDownClicked(int floor)
        {
            elevatorManager.RequestElevator(floor, Direction.Down);
        }
    }
}