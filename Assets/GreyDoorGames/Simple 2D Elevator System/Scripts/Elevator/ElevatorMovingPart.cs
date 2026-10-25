using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace GreyDoorGames
{
    public class ElevatorMovingPart : MonoBehaviour
    {
        public int currentFloorIndex = 0;
        public Direction currentMoveDirection;
        public float speed = 200f;

        public RectTransform rectTransform;
        public bool isInUse = false;

        [Header("Elevator Details")]
        [SerializeField] private Button _elevatorPanelButton;
        [SerializeField] private int _elevatorID;

        private ElevatorUI _elevatorUI;
        private Coroutine _moveCoroutine;
        private Coroutine _doorCoroutine;

        private float floorHeight;
        public void SetElevatorUI(ElevatorUI elevatorUI)
        {
            _elevatorUI = elevatorUI;
        }
        public int GetElevatorID() => _elevatorID;
        public void AddPanelButtonListener(UnityAction onClicked)
        {
            _elevatorPanelButton.onClick.AddListener(onClicked);
        }
        public void SetElevatorID(int id)
        {
            _elevatorID = id;
        }
        public void SetDirection(Direction direction)
        {
            currentMoveDirection = direction;
        }
        public void PlayDoorAnimation(DoorAction doorAction)
        {
            bool status = false;

            switch (doorAction)
            {
                case DoorAction.OpenDoor:
                    status = true;
                    break;

                case DoorAction.CloseDoor:
                    status = false;
                    break;
            }

            _elevatorUI.elevatorDoorAnimation.PlayAnimation(doorAction.ToString(), status);
        }

        private void StopDoorRoutine()
        {
            if (_doorCoroutine != null)
            {
                StopCoroutine(_doorCoroutine);
                _doorCoroutine = null;
            }
        }
        public void PlayOnStopAnimation()
        {
            StopDoorRoutine();
            _doorCoroutine = StartCoroutine(OnStopSequence());
        }
        public IEnumerator OnStopSequence()
        {
            var doorAnimation = _elevatorUI.elevatorDoorAnimation;
            PlayDoorAnimation(DoorAction.OpenDoor);
            yield return CommonFunctions.GetWaitForSeconds((int)doorAnimation.doorOpenDuration);
            PlayDoorAnimation(DoorAction.CloseDoor);
            yield return CommonFunctions.GetWaitForSeconds((int)doorAnimation.doorCloseDuration);
        }
        public void MoveToFloor(int targetFloorIndex, float floorHeight, Direction movingDirection, Vector3 targetPosition, Action onComplete, Action onstartAction)
        {
            if (rectTransform == null)
            {
                Debug.LogError("RectTransform is not assigned in Elevator script.");
                return;
            }

            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
            }
            isInUse = true;
            this.floorHeight = floorHeight;
            SetDirection(movingDirection);
            _moveCoroutine = StartCoroutine(MoveRoutine(targetFloorIndex, targetPosition, onComplete, onstartAction));
        }
        public void MoveToFloor(int targetFloorIndex, float floorHeight, Vector3 targetPosition, Action onComplete)
        {
            if (rectTransform == null)
            {
                Debug.LogError("RectTransform is not assigned in Elevator script.");
                return;
            }
            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
            }
            isInUse = true;
            this.floorHeight = floorHeight;
            _moveCoroutine = StartCoroutine(MoveRoutine(targetFloorIndex, targetPosition, onComplete));
        }
        private IEnumerator WaitForDoorCompletion()
        {
            if (_elevatorUI != null && _elevatorUI.elevatorDoorAnimation.doorStatus)
            {
                PlayDoorAnimation(DoorAction.CloseDoor);
                yield return CommonFunctions.GetWaitForSeconds((int)_elevatorUI.elevatorDoorAnimation.doorCloseDuration);
            }
        }
        IEnumerator MoveRoutine(int targetFloorIndex, Vector3 targetPosition, Action onComplete = null, Action onStartAction = null)
        {
            onStartAction?.Invoke();
            yield return WaitForDoorCompletion();
            Vector3 startPosition = rectTransform.localPosition;

            float distance = Vector3.Distance(startPosition, targetPosition);

            float duration = distance / speed;

            float elapsed = 0f;
            int direction = (targetFloorIndex > currentFloorIndex) ? 1 : -1;
            int nextFloorCheck = currentFloorIndex + direction;


            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = elapsed / duration;
                t = Mathf.Clamp01(t);

                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                rectTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, smoothT);

                if (nextFloorCheck != targetFloorIndex + direction)
                {
                    float crossY = nextFloorCheck * floorHeight;

                    bool crossed = direction == 1
                        ? rectTransform.localPosition.y >= crossY
                        : rectTransform.localPosition.y <= crossY;

                    if (crossed)
                    {
                        currentFloorIndex = nextFloorCheck;
                        nextFloorCheck += direction;
                    }

                    yield return null;
                }
            }

            rectTransform.localPosition = targetPosition;
            currentFloorIndex = targetFloorIndex;
            isInUse = false;
            onComplete?.Invoke();
        }
    }
}