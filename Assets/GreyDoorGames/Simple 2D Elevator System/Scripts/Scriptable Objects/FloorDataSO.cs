using UnityEngine;

namespace GreyDoorGames
{
    [CreateAssetMenu(fileName = "FloorDataSO", menuName = "Scriptable Objects/FloorDataSO")]
    public class FloorDataSO : ScriptableObject
    {
        public FloorData[] floorDatas;

        private void OnValidate()
        {
            for (int i = 0; i < floorDatas.Length; i++)
            {
                if (floorDatas[i] != null)
                {
                    if (i == 0)
                    {
                        floorDatas[i].floorID = "Ground";
                    }
                    else
                    {
                        floorDatas[i].floorID = $"Floor {i}";
                    }
                }
            }
        }
    }
}