using System.Collections;
using UnityEngine;

namespace GreyDoorGames
{
    public class ElevatorDoorAnimation : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        [Header("DoorAnimation Duration")]
        public float doorOpenDuration = 5;
        public float doorCloseDuration = 3;

        public bool doorStatus = false;
        public bool IsPlaying
        {
            get
            {
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

                return (stateInfo.IsName("OpenDoor") || stateInfo.IsName("CloseDoor"))
                       && stateInfo.normalizedTime < 1f;
            }
        }

        public void PlayAnimation(string animationName, bool doorStatus)
        {
            if (IsPlaying)
                return;

            _animator.Play(animationName);
            this.doorStatus = doorStatus;
        }
    }
}

