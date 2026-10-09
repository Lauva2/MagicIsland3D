using TMPro;
using MagicIsland.UI.Models;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI.Views
{
    public sealed class SettingsView : UIView
    {
        public Button CloseButton;
        public Slider VolumeSlider;
        public Toggle FullscreenToggle;
        public TMP_Text VolumeText;
        public void Render(SettingsModel model)
        {
            VolumeSlider.SetValueWithoutNotify(model.Volume);
            FullscreenToggle.SetIsOnWithoutNotify(model.Fullscreen);
            VolumeText.text = Mathf.RoundToInt(model.Volume * 100) + "%";
        }
    }
}
