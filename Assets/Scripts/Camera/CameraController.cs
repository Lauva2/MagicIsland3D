using UnityEngine;

namespace MagicIsland.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        public Transform Target;
        private Camera view;
        private Vector3 velocity;
        private Vector3 offset;
        private float minSize = 5f, maxSize = 9f;

        private void Awake()
        {
            view = GetComponent<Camera>();
            view.orthographic = true;
            view.orthographicSize = 7f;
            transform.rotation = Quaternion.Euler(42f, 0f, 0f);
            offset = -transform.forward * 18f;
        }

        public void SnapToTarget() => transform.position = Target.position + Vector3.up * 0.6f + offset;

        public void Configure(float pitch, float size)
        {
            view.orthographicSize = size;
            minSize = Mathf.Max(1f, size - 2f);
            maxSize = size + 2f;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            offset = -transform.forward * 18f;
        }

        private void LateUpdate()
        {
            if (Target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position,
                Target.position + Vector3.up * 0.6f + offset, ref velocity, 0.15f);
            view.orthographicSize = Mathf.Clamp(view.orthographicSize - Input.mouseScrollDelta.y * 0.4f, minSize, maxSize);
        }
    }
}
