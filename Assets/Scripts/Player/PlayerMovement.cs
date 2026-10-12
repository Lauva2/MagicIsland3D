using UnityEngine;

namespace MagicIsland.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        public Camera ViewCamera;
        public Transform Visual;
        public float Speed = 4f;
        public float ActionUntil { get; private set; }
        public bool ToolActionLocked { get; set; }
        private CharacterController controller;
        private float verticalSpeed;

        private void Awake() => controller = GetComponent<CharacterController>();

        private void Update()
        {
            float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            Vector3 right = Vector3.ProjectOnPlane(ViewCamera.transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(ViewCamera.transform.forward, Vector3.up).normalized;
            Vector3 direction = Vector3.ClampMagnitude(right * x + forward * z, 1f);
            if (ToolActionLocked || Time.time < ActionUntil) direction = Vector3.zero;
            if (direction.sqrMagnitude > 0.001f)
                Visual.rotation = Quaternion.Slerp(Visual.rotation, Quaternion.LookRotation(direction), 14f * Time.deltaTime);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            controller.Move((direction * Speed + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        public void FaceAndAct(Vector3 target)
        {
            Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.001f) Visual.rotation = Quaternion.LookRotation(direction);
            ActionUntil = Time.time + 0.18f;
        }
        public void ResetMotion() { verticalSpeed = -2; ActionUntil = 0; ToolActionLocked = false; }
    }
}
