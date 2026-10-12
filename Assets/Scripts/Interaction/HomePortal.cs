using TMPro;
using UnityEngine;
namespace MagicIsland.Interaction
{
    public sealed class HomePortal : MonoBehaviour
    {
        public bool IsExit;
        public Transform ArrivalPoint;
        public Transform ReachPoint;
        public TMP_Text NameText;
        public Vector3 Position => ReachPoint != null ? ReachPoint.position : transform.position;
        private void LateUpdate() { if (NameText != null && Camera.main != null) NameText.transform.rotation = Camera.main.transform.rotation; }
    }
}
