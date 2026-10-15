using UnityEngine;

namespace MagicIsland.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        public Camera ViewCamera;
        public Transform Visual;
        public PlayerAnimation Animation;
        public float Speed = 4f;
        public float ActionUntil { get; private set; }
        public bool ToolActionLocked { get; set; }
        private CharacterController controller;
        private float verticalSpeed;
        public float ActualPlanarSpeed { get; private set; }
        public event System.Action<Vector3,float> GroundMoved;

        private void Awake() => controller = GetComponent<CharacterController>();

        private void Update()
        {
            float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            TickMovement(new Vector2(x, z), Time.deltaTime);
        }
        internal void TickMovement(Vector2 input, float deltaTime)
        {
            if (!isActiveAndEnabled || ViewCamera == null || !controller.enabled || deltaTime <= 0) return;
            Vector3 right = Vector3.ProjectOnPlane(ViewCamera.transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(ViewCamera.transform.forward, Vector3.up).normalized;
            Vector3 direction = Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
            if (ToolActionLocked || Time.time < ActionUntil) direction = Vector3.zero;
            if (direction.sqrMagnitude > 0.001f)
                Visual.rotation = Quaternion.Slerp(Visual.rotation, Quaternion.LookRotation(direction), 14f * deltaTime);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * deltaTime;
            Vector3 before = transform.position;
            controller.Move((direction * Speed + Vector3.up * verticalSpeed) * deltaTime);
            var game = MagicIsland.Managers.GameManager.Instance;
            if (game != null && game.Terrain.Terrain != null && !game.Housing.Inside)
            {
                var safe = game.Terrain.Constrain(before,transform.position,controller.radius+controller.skinWidth);
                if (safe != transform.position) { controller.enabled=false;transform.position=safe;controller.enabled=true; }
            }
            ActualPlanarSpeed = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude / deltaTime;
            if(controller.isGrounded&&ActualPlanarSpeed>.1f)GroundMoved?.Invoke(transform.position,ActualPlanarSpeed*deltaTime);
            Animation?.SetMovement(ActualPlanarSpeed, Speed, deltaTime);
        }

        public void FaceAndAct(Vector3 target)
        {
            Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.001f) Visual.rotation = Quaternion.LookRotation(direction);
            ActionUntil = Time.time + 0.18f;
        }
        public void ResetMotion() { verticalSpeed = -2; ActionUntil = 0; ToolActionLocked = false; ActualPlanarSpeed = 0; Animation?.ResetPose(); }
        private void OnDisable() { ActualPlanarSpeed = 0; }
    }
}
