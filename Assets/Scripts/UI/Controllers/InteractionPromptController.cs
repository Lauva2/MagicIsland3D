using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;

namespace MagicIsland.UI.Controllers
{
    public sealed class InteractionPromptController : IUIController
    {
        public InteractionPromptModel Model { get; } = new InteractionPromptModel();
        private readonly InteractionPromptView view;
        private readonly InteractionManager interaction;
        private readonly FarmManager farm;
        private InteractionTarget displayedTarget;
        public InteractionPromptController(InteractionPromptView view, InteractionManager interaction, FarmManager farm)
        { this.view = view; this.interaction = interaction; this.farm = farm; interaction.Changed += Tick; Tick(); }
        public void Tick()
        {
            if (view == null) return;
            var target = interaction.Current;
            // Planted plots already have their own crop details card.
            bool visible = target.Visible && !(target.Kind == InteractionKind.Farm && farm.State != null && farm.State.Cells[target.Index].Planted);
            if (ReferenceEquals(displayedTarget, target) && Model.Visible == visible) return;
            displayedTarget = target; Model.Visible = visible;
            Model.Name = target.Name; Model.Operation = target.Operation;
            Model.InRange = target.InRange; Model.Ready = target.Ready;
            Model.Status = !target.InRange ? "请靠近" : target.Ready ? "可操作" : "需准备";
            Model.Details = !target.InRange ? "走近后可以操作" : target.Details;
            view.Render(Model);
        }
        public void Dispose() => interaction.Changed -= Tick;
    }
}
