using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
namespace MagicIsland.UI.Controllers
{
    public sealed class SceneTransitionController : IUIController
    {
        public SceneTransitionModel Model { get; } = new SceneTransitionModel();
        private readonly SceneTransitionView view;
        public SceneTransitionController(SceneTransitionView view, string text) { this.view = view; Model.Message = text; Tick(); }
        public void Tick() { if (view != null) view.Render(Model); }
        public void Dispose() { }
    }
}
