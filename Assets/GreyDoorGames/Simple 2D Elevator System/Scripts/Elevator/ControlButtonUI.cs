using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace GreyDoorGames
{
    public class ControlButtonUI : MonoBehaviour
    {
        public TMP_Text floorNumberText;
        public Button controlButton;

        public void SetFloorNumber(int floorNumber)
        {
            if (floorNumberText != null)
            {
                floorNumberText.text = floorNumber.ToString();
            }
            else
            {
                Debug.LogWarning("Floor number text reference is missing.");
            }
        }

        public void SetButtonAction(UnityEngine.Events.UnityAction action)
        {
            if (controlButton != null)
            {
                controlButton.onClick.RemoveAllListeners();
                controlButton.onClick.AddListener(action);
            }
            else
            {
                Debug.LogWarning("Control button reference is missing.");
            }
        }
    }
}