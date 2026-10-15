using MagicIsland.Configuration;
using MagicIsland.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class UIButtonAudio : MonoBehaviour
    {
        public bool PlayClick = true;
        private Button button;
        private AudioManager audioManager;
        public void Bind(AudioManager audio)
        {
            if (button == null) { button = GetComponent<Button>(); button.onClick.AddListener(Click); }
            audioManager = audio;
        }
        private void Click()
        {
            if (PlayClick && isActiveAndEnabled && button.IsInteractable()) audioManager?.PlaySound(AudioCue.UiClick);
        }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Click); audioManager = null; }
    }
}
