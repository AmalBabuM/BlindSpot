using UnityEngine;
namespace GreyDoorGames
{
    [CreateAssetMenu(fileName = "ElevatorDataSO", menuName = "Scriptable Objects/ElevatorDataSO")]
    public class ElevatorDataSO : ScriptableObject
    {
        public int elevatorCount;
    }
}