using UnityEngine;

namespace MagicIsland.Managers
{
    // Session settings are controlled centrally; panel views only render and bind input.
    public sealed class SettingsManager : MonoBehaviour
    {
        public float Volume => AudioListener.volume;
        public bool Fullscreen => Screen.fullScreen;
        public void SetVolume(float value) => AudioListener.volume = Mathf.Clamp01(value);
        public void SetFullscreen(bool value) => Screen.fullScreen = value;
    }
}
