using UnityEngine;
namespace MagicIsland.Interaction
{
    public sealed class FishingSpot : MonoBehaviour
    {
        public Transform Bobber, Label;
        public Transform ReachPoint;
        public BoxCollider Deck;
        [HideInInspector] public int CoastArtVersion;
        public Vector3 Position => ReachPoint != null ? ReachPoint.position : transform.position;
        private void LateUpdate() { if (Label != null && Camera.main != null) Label.rotation = Camera.main.transform.rotation; }
    }
}
