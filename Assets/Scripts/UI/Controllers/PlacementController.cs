using System;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class PlacementController : IUIController
    {
        public PlacementModel Model { get; } = new PlacementModel();
        private readonly PlacementView view;
        private readonly PlacementManager manager;
        private readonly UnityAction confirm, rotate, cancel, save;
        public PlacementController(PlacementView view, PlacementManager manager, Action confirm, Action rotate, Action cancel, Action save)
        {
            this.view = view; this.manager = manager;
            this.confirm = () => confirm(); this.rotate = () => rotate(); this.cancel = () => cancel(); this.save = () => save();
            view.ConfirmButton.onClick.AddListener(this.confirm); view.RotateButton.onClick.AddListener(this.rotate);
            view.CancelButton.onClick.AddListener(this.cancel); view.SaveButton.onClick.AddListener(this.save); Tick();
        }
        public void Tick()
        {
            if (view == null || manager.State == null) return;
            var result = manager.CheckCandidate(); Model.CanConfirm = result == PlacementResult.Success;
            Model.Title = (manager.Inventory.Catalog.Find(manager.ItemId)?.DisplayName ?? "装饰") + " · 摆放";
            Model.Status = manager.Describe(result) + (manager.HasCandidate ? "\n位置 " + manager.GridX + ", " + manager.GridZ + " · 朝向 " + manager.QuarterTurns * 90 + "°" : "");
            Model.Feedback = manager.Feedback ?? "WASD 移动 · R 旋转 · ESC 取消"; view.Render(Model);
        }
        public void Dispose()
        {
            if (view == null) return;
            view.ConfirmButton.onClick.RemoveListener(confirm); view.RotateButton.onClick.RemoveListener(rotate);
            view.CancelButton.onClick.RemoveListener(cancel); view.SaveButton.onClick.RemoveListener(save);
        }
    }
}
