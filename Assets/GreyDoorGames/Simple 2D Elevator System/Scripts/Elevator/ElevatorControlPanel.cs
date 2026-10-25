using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace GreyDoorGames
{
    public class ElevatorControlPanel : MonoBehaviour
    {
        [Header("Control Panel Settings")]
        [SerializeField] private CanvasGroup _panelCanvasGroup;
        [SerializeField] private RectTransform _panelRectTransform;
        [SerializeField] private RectTransform _floorButtonContainer;

        [SerializeField] private ControlButtonUI _floorControlButtonSample;

        [Header("Control Buttons")]
        [SerializeField] private Button _doorOpenButton;
        [SerializeField] private Button _doorCloseButton;
        [SerializeField] private Button _panelCloseButton;

        [Header("Elevator Details")]
        [SerializeField] private TMP_Text _elevatorIdText;

        public Action<bool> OnDoorButtonPressed;
        public Action<int, int> OnFloorButtonPressed;

        public int selectedElevatorId;
        private void Awake()
        {
            _doorOpenButton.onClick.AddListener(OnDoorOpenButtonClicked);
            _doorCloseButton.onClick.AddListener(OnDoorCloseButtonClicked);
            _panelCloseButton.onClick.AddListener(OnPanelCloseButtonClicked);
        }
        public void InitializePanel(int totalFloors)
        {
            for (int i = 0; i < totalFloors; i++)
            {
                var floorId = i;
                ControlButtonUI floorButton = Instantiate(_floorControlButtonSample, _floorButtonContainer);
                floorButton.SetFloorNumber(floorId);
                floorButton.SetButtonAction(() => AddControlButtonListener(floorId));
            }
        }
        public void ShowPanel(int elevatorId)
        {
            selectedElevatorId = elevatorId;
            _elevatorIdText.text = (elevatorId + 1).ToString();
            CommonFunctions.SetCanvasGroupStatus(_panelCanvasGroup, true);
        }
        public void AddControlButtonListener(int floorNumber)
        {
            OnFloorButtonPressed?.Invoke(selectedElevatorId, floorNumber);
        }
        private void OnDoorOpenButtonClicked()
        {
            OnDoorButtonPressed?.Invoke(true);
        }
        private void OnDoorCloseButtonClicked()
        {
            OnDoorButtonPressed?.Invoke(false);
        }
        private void OnPanelCloseButtonClicked()
        {
            CommonFunctions.SetCanvasGroupStatus(_panelCanvasGroup, false);
        }
    }
}