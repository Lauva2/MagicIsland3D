using UnityEngine;

namespace MagicIsland.Gathering
{
    public sealed class GatheringArtVisual : MonoBehaviour
    {
        [HideInInspector] public int ArtVersion;
        public Transform WholeMotion, DepletedMotion;
        [Range(0, 6)] public float HitAngle = 3;
        public bool IsReacting { get; private set; }
        private Quaternion restRotation;
        private Vector3 restScale, axis;
        private float age, duration;
        private bool depleted;
        public void Initialize()
        {
            restRotation = WholeMotion.localRotation; restScale = DepletedMotion.localScale;
            ResetPose();
        }
        public void PlayHit(Vector3 direction, bool isDepleted, float seconds)
        {
            ResetPose(); depleted = isDepleted; duration = Mathf.Max(.1f, seconds); age = 0;
            direction = transform.InverseTransformDirection(direction);
            axis = new Vector3(direction.z, 0, -direction.x).normalized;
            if (axis.sqrMagnitude < .01f) axis = Vector3.right;
            IsReacting = true;
        }
        public void Tick(float seconds)
        {
            if (!IsReacting) return;
            age += seconds;
            if (age >= duration) { ResetPose(); return; }
            float t = age / duration;
            if (depleted) DepletedMotion.localScale = restScale * (1 + .09f * Mathf.Sin(t * Mathf.PI));
            else WholeMotion.localRotation = restRotation * Quaternion.AngleAxis(Mathf.Sin(t * Mathf.PI * 4) * (1 - t) * HitAngle, axis);
        }
        public void ResetPose()
        {
            IsReacting = false;
            if (WholeMotion != null) WholeMotion.localRotation = restRotation;
            if (DepletedMotion != null) DepletedMotion.localScale = restScale;
        }
    }
}
