using UnityEngine;

namespace MagicIsland.Magic
{
    // A single AA-baked effect, advanced by the unified game loop; no individual droplet objects.
    public sealed class MagicRainEffect : MonoBehaviour
    {
        public ParticleSystem Rain;
        public Transform Ring;
        private float age;
        public bool Active { get; private set; }
        public bool IsValid => Rain != null && Ring != null && Ring.GetComponent<Renderer>()?.sharedMaterial != null
            && Rain.main.maxParticles <= 128 && GetComponentsInChildren<Collider>(true).Length == 0;
        public void Begin(Vector3 center, float width)
        {
            transform.position = center; age = 0; Active = true; gameObject.SetActive(true);
            Ring.gameObject.SetActive(true); Ring.localScale = new Vector3(width, 1, width);
            var shape = Rain.shape; shape.scale = new Vector3(width, .01f, width);
            Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); Rain.Play(); Rain.Pause();
        }
        public void Tick(float seconds, bool paused)
        {
            if (!Active || paused) return;
            seconds = Mathf.Max(0, seconds); age += seconds;
            Rain.Simulate(seconds, false, false, false);
            Ring.gameObject.SetActive(age < .9f);
            if (age >= 1.5f) ResetEffect();
        }
        public void ResetEffect()
        { Active = false; age = 0; Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); Ring.gameObject.SetActive(false); gameObject.SetActive(false); }
    }
}
