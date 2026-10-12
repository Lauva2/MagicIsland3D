using UnityEngine;
namespace MagicIsland.Interaction
{
    public sealed class FishingSpot : MonoBehaviour
    {
        public Transform Bobber, Label;
        private void LateUpdate() { if (Label != null && Camera.main != null) Label.rotation = Camera.main.transform.rotation; }
    }
}
