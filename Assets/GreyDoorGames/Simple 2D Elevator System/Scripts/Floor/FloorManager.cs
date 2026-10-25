using System.Collections.Generic;
using UnityEngine;
namespace GreyDoorGames
{
    public class FloorManager : MonoBehaviour
    {
        [SerializeField] private FloorDataSO floorDataSO;
        [SerializeField] private RectTransform floorContainer;
        [SerializeField] private FloorUI floorPrefab;

        [SerializeField] List<FloorUI> floorUIs = new List<FloorUI>();

        public List<FloorUI> FloorUIs => floorUIs;

        private bool isInitialized = false;

        public void Init()
        {
            CreateFloor();
        }
        private void CreateFloor()
        {
            for (int i = 0; i < floorDataSO.floorDatas.Length; i++)
            {
                FloorUI floorUI = Instantiate(floorPrefab, floorContainer);
                floorUI.SetFloorText(floorDataSO.floorDatas[i].floor.ToString());
                floorUI.SetFloorIndex(i);
                floorUI.SetFloorType(floorDataSO.floorDatas[i].floor);
                floorUI.AddListeners();
                floorUIs.Add(floorUI);
            }
            isInitialized = true;
        }

        public bool IsInitialized()
        {
            return isInitialized;
        }
    }
}