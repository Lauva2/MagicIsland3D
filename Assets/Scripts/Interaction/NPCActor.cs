using UnityEngine;
namespace MagicIsland.Interaction
{
    public sealed class NPCActor : MonoBehaviour
    {
        public string Id { get; private set; }
        public TMPro.TMP_Text NameText;
        public void Initialize(string id) => Id = id;
        private void LateUpdate()
        { if (NameText != null && Camera.main != null) NameText.transform.rotation = Camera.main.transform.rotation; }
    }
}
