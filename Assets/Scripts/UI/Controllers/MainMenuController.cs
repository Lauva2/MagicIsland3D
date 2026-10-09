using System;
using MagicIsland.Loading;
using MagicIsland.UI.Models;
using UnityEngine.Events;

namespace MagicIsland.UI.Controllers
{
    public sealed class MainMenuController : IUIController
    {
        public MainMenuModel Model { get; } = new MainMenuModel();
        private readonly MainMenuView view;
        private readonly UnityAction onEnter;
        private readonly UnityAction onContinue;
        private readonly UnityAction onSettings;
        private readonly UnityAction onQuit;

        public MainMenuController(MainMenuView view, Action enterGame, Action continueGame, Action openSettings, Action quitGame, bool hasSave)
        {
            this.view = view;
            onEnter = () => { if (!Model.Busy) enterGame(); };
            onContinue = () => { if (!Model.Busy && Model.HasSave) continueGame(); };
            onSettings = () => { if (!Model.Busy) openSettings(); };
            onQuit = () => { if (!Model.Busy) quitGame(); };
            Model.HasSave = hasSave;
            view.EnterButton.onClick.AddListener(onEnter);
            view.ContinueButton.onClick.AddListener(onContinue);
            view.SettingsButton.onClick.AddListener(onSettings);
            view.QuitButton.onClick.AddListener(onQuit);
            Tick();
        }
        public void SetStatus(string text, bool busy) { Model.Status = text; Model.Busy = busy; Tick(); }
        public void SetSaveAvailable(bool available) { Model.HasSave = available; Tick(); }
        public void Tick() { if (view != null) view.Render(Model); }
        public void Dispose()
        {
            if (view == null) return;
            view.EnterButton.onClick.RemoveListener(onEnter);
            view.ContinueButton.onClick.RemoveListener(onContinue);
            view.SettingsButton.onClick.RemoveListener(onSettings);
            view.QuitButton.onClick.RemoveListener(onQuit);
        }
    }
}
