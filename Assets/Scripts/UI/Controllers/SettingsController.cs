using System;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;

namespace MagicIsland.UI.Controllers
{
    public sealed class SettingsController : IUIController
    {
        private readonly SettingsView view;
        private readonly SettingsManager settings;
        private readonly SettingsModel model = new SettingsModel();
        private readonly UnityAction onClose;
        private readonly UnityAction<float> onVolume;
        private readonly UnityAction<bool> onFullscreen;
        public SettingsController(SettingsView view, SettingsManager settings, Action close)
        {
            this.view = view;
            this.settings = settings;
            onClose = () => close();
            onVolume = value => { settings.SetVolume(value); Tick(); };
            onFullscreen = value => { settings.SetFullscreen(value); Tick(); };
            view.CloseButton.onClick.AddListener(onClose);
            view.VolumeSlider.onValueChanged.AddListener(onVolume);
            view.FullscreenToggle.onValueChanged.AddListener(onFullscreen);
            Tick();
        }
        public void Tick()
        {
            model.Volume = settings.Volume;
            model.Fullscreen = settings.Fullscreen;
            view.Render(model);
        }
        public void Dispose()
        {
            view.CloseButton.onClick.RemoveListener(onClose);
            view.VolumeSlider.onValueChanged.RemoveListener(onVolume);
            view.FullscreenToggle.onValueChanged.RemoveListener(onFullscreen);
        }
    }
}
