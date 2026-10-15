using System;
using MagicIsland.Managers;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class MapController : IUIController
    {
        private readonly MapView view;
        private readonly MapManager manager;
        private readonly UnityAction close;
        private readonly UnityAction[] selections;
        public MapController(MapView view, MapManager manager, Action close)
        {
            if (view == null || !view.IsValid || !manager.Ready || manager.Model.Locations.Count > MapView.MaximumLocations) throw new InvalidOperationException("MapPanel needs wired MVC controls and initialized locations");
            this.view = view; this.manager = manager; this.close = () => close(); view.CloseButton.onClick.AddListener(this.close);
            selections = new UnityAction[MapView.MaximumLocations];
            for (int i = 0; i < selections.Length; i++) { int index = i; selections[i] = () => { manager.Select(index); Tick(); }; view.LocationButtons[i].onClick.AddListener(selections[i]); view.MarkerButtons[i].onClick.AddListener(selections[i]); }
            Tick();
        }
        public void Tick() { manager.RefreshPlayer(); view.Render(manager.Model); }
        public void Dispose()
        {
            view.CloseButton.onClick.RemoveListener(close);
            for (int i = 0; i < selections.Length; i++) { view.LocationButtons[i].onClick.RemoveListener(selections[i]); view.MarkerButtons[i].onClick.RemoveListener(selections[i]); }
        }
    }
}
