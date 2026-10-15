using UnityEngine;
namespace MagicIsland.Interaction
{
    public sealed class ChickenActor : MonoBehaviour
    {
        public int Id { get; private set; }
        public Transform HeartRoot;
        private MaterialPropertyBlock heartTint;
        private float heartTime;
        public bool HeartVisible => heartTime > 0;
        public void Initialize(int id) => Id = id;
        public void ShowHeart() { heartTime = 1.4f; HeartRoot.gameObject.SetActive(true); TickFeedback(0); }
        public void TickFeedback(float dt)
        {
            if (!HeartVisible || HeartRoot == null) return;
            heartTime = Mathf.Max(0, heartTime - dt); HeartRoot.gameObject.SetActive(HeartVisible);
            HeartRoot.localPosition = new Vector3(0, .85f + (1.4f - heartTime) * .22f, 0);
            if (heartTint == null) heartTint = new MaterialPropertyBlock();
            heartTint.SetColor("_Color", new Color(.91f, .39f, .44f, Mathf.Clamp01(heartTime / .4f))); HeartRoot.GetComponent<Renderer>().SetPropertyBlock(heartTint);
            var camera = MagicIsland.Managers.GameManager.Instance?.Players?.Player?.ViewCamera;
            if (camera != null) HeartRoot.rotation = camera.transform.rotation;
        }
        public void Tick(float time) { transform.localPosition = new Vector3(Mathf.Sin(time * .6f + Id) * .08f, 0, Mathf.Cos(time * .5f + Id) * .08f); transform.localRotation = Quaternion.Euler(0, Mathf.Sin(time * .3f + Id) * 35, 0); }
    }
}
