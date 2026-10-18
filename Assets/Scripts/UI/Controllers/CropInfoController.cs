using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;

namespace MagicIsland.UI.Controllers
{
    public sealed class CropInfoController : IUIController
    {
        public CropInfoModel Model { get; } = new CropInfoModel();
        private readonly CropInfoView view;
        private readonly FarmManager farm;
        private readonly InteractionManager interaction;
        private string displayedItemId;
        private (int Growth, int Required, bool Watered, bool Fertilized)? displayedDetails;
        private InteractionTarget displayedTarget;
        public CropInfoController(CropInfoView view, FarmManager farm, InteractionManager interaction)
        { this.view = view; this.farm = farm; this.interaction = interaction; view.Render(Model); Tick(); }
        public void Tick()
        {
            if (view == null) return;
            var pointer = interaction.Controller;
            int index = pointer != null && pointer.enabled ? pointer.HoverIndex : -1;
            bool visible = farm.State != null && index >= 0 && index < farm.State.Cells.Length && farm.State.Cells[index].Planted;
            bool changed = Model.Visible != visible; Model.Visible = visible;
            if (Model.Visible)
            {
                FarmCell cell = farm.State.Cells[index];
                var crop = farm.Crops.Find(cell.CropId);
                string itemId = cell.Mature ? farm.State.HarvestId(index) : crop.HarvestItemId;
                if (displayedItemId != itemId)
                {
                    displayedItemId = itemId; Model.Name = farm.Inventory.Catalog.Find(itemId).DisplayName; changed = true;
                }
                var details = (cell.Growth, cell.RequiredGrowthDays, cell.Watered, cell.Fertilized);
                if (!displayedDetails.HasValue || displayedDetails.Value != details)
                {
                    displayedDetails = details;
                    CropGrowthStage stage = CropStages.FromGrowth(cell.Growth, cell.RequiredGrowthDays);
                    string label = stage == CropGrowthStage.Mature ? "已成熟" : stage == CropGrowthStage.Growing ? "生长中" : "幼苗";
                    Model.Details = label + "  ·  成长 " + cell.Growth + " / " + cell.RequiredGrowthDays + "\n"
                        + (cell.Watered ? "今天已浇水" : "今天未浇水") + " · " + (cell.Fertilized ? "本轮已施肥" : "本轮未施肥");
                    changed = true;
                }
                var target = interaction.Current;
                if (!ReferenceEquals(displayedTarget, target))
                {
                    displayedTarget = target; Model.Hint = target.InRange && target.Ready ? target.Operation : target.Hint; changed = true;
                }
            }
            if (changed) view.Render(Model);
        }
        public void Dispose() { }
    }
}
