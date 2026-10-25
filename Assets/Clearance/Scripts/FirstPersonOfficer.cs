using UnityEngine;
using UnityEngine.InputSystem;

namespace Clearance
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonOfficer : MonoBehaviour
    {
        [SerializeField] Camera view;
        [SerializeField] GroundFloorMission mission;
        [SerializeField] SquadCoordinator squad;
        [SerializeField] float moveSpeed = 2.6f;
        [SerializeField] float carefulSpeed = 1.2f;
        [SerializeField] float mouseSensitivity = .12f;
        CharacterController controller;
        float pitch, fallSpeed;
        bool interactionBlocked;
        public Camera View => view;
        public bool HasControl => !interactionBlocked && Cursor.lockState == CursorLockMode.Locked && !mission.Complete && (!squad || squad.ChoiceMade);
        public bool InteractionBlocked => interactionBlocked;
        public void SetInteractionBlocked(bool value) { interactionBlocked = value; SetCursor(!value && !mission.Complete); }
        public void ConfigureSquad(SquadCoordinator value) => squad = value;
        public void Configure(Camera camera, GroundFloorMission value) { view = camera; mission = value; }
        void Awake() => controller = GetComponent<CharacterController>();
        void Start() => SetCursor(!squad || squad.ChoiceMade);
        void OnDisable() => SetCursor(false);
        public void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        void Update()
        {
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (keys == null || mouse == null) return;
            if (keys.escapeKey.wasPressedThisFrame) SetCursor(false);
            if (!HasControl) return;
            Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(0, look.x, 0);
            pitch = Mathf.Clamp(pitch - look.y, -80, 80);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Vector2 input = new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            input = Vector2.ClampMagnitude(input, 1);
            fallSpeed = controller.isGrounded ? -2 : Mathf.Max(fallSpeed - 20 * Time.deltaTime, -30);
            float speed = keys.leftShiftKey.isPressed ? carefulSpeed : moveSpeed;
            controller.Move(((transform.right * input.x + transform.forward * input.y) * speed + Vector3.up * fallSpeed) * Time.deltaTime);
        }
    }
}
