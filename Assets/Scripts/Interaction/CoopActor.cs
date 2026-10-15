using TMPro;
using UnityEngine;
namespace MagicIsland.Interaction
{
    public sealed class CoopActor : MonoBehaviour
    {
        public Transform[] ChickenSpots;
        public GameObject EggsMarker;
        public TMP_Text NameText;
        private void LateUpdate() { if (NameText != null && Camera.main != null) NameText.transform.rotation = Camera.main.transform.rotation; }
    }
}
