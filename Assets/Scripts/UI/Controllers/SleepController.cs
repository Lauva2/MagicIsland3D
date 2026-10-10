using System;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class SleepController : IUIController
    {
        public SleepModel Model { get; } = new SleepModel();
        private readonly SleepView view;
        private readonly UnityAction confirm, cancel;
        public SleepController(SleepView view, Action confirm, Action cancel)
        {
            this.view = view; this.confirm = () => confirm(); this.cancel = () => cancel();
            view.ConfirmButton.onClick.AddListener(this.confirm);
            view.CancelButton.onClick.AddListener(this.cancel);
            view.Render(Model);
        }
        public void Tick() { }
        public void Dispose()
        {
            if (view == null) return;
            view.ConfirmButton.onClick.RemoveListener(confirm); view.CancelButton.onClick.RemoveListener(cancel);
        }
    }
}
