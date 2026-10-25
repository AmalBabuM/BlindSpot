using System.Collections.Generic;
using UnityEngine;

namespace GreyDoorGames
{
    public static class CommonFunctions
    {
        private static Dictionary<int, WaitForSeconds> _waitForSecondsDict = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _waitForSecondsDict.Clear();
        }

        public static void SetCanvasGroupStatus(CanvasGroup canvasGroup, bool isActive)
        {
            if (canvasGroup == null)
            {
                Debug.LogWarning("CanvasGroup reference is null.");
                return;
            }
            canvasGroup.alpha = isActive ? 1 : 0;
            canvasGroup.interactable = isActive;
            canvasGroup.blocksRaycasts = isActive;
        }

        public static WaitForSeconds GetWaitForSeconds(int seconds)
        {
            if (!_waitForSecondsDict.TryGetValue(seconds, out var wait))
            {
                wait = new WaitForSeconds(seconds);
                _waitForSecondsDict[seconds] = wait;
            }

            return wait;
        }
    }
}


