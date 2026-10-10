using MagicIsland.Farming;
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
        public CropInfoController(CropInfoView view, FarmManager farm, InteractionManager interaction)
        { this.view = view; this.farm = farm; this.interaction = interaction; Tick(); }
        public void Tick()
        {
            if (view == null) return;
            var pointer = interaction.Controller;
            int index = pointer != null && pointer.enabled ? pointer.HoverIndex : -1;
            Model.Visible = farm.State != null && index >= 0 && index < farm.State.Cells.Length && farm.State.Cells[index].Planted;
            if (Model.Visible)
            {
                FarmCell cell = farm.State.Cells[index];
                var crop = farm.Crops.Find(cell.CropId);
                Model.Name = farm.Inventory.Catalog.Find(crop.HarvestItemId).DisplayName;
                CropGrowthStage stage = CropStages.FromGrowth(cell.Growth, cell.RequiredGrowthDays);
                string label = stage == CropGrowthStage.Mature ? "已成熟" : stage == CropGrowthStage.Growing ? "生长中" : "幼苗";
                Model.Details = label + "  ·  成长 " + cell.Growth + " / " + cell.RequiredGrowthDays + "\n"
                    + (cell.Watered ? "今天已浇水" : "今天未浇水");
                Model.Hint = pointer.HoverInRange ? farm.Describe(index, pointer.Tool) : "距离太远，请靠近";
            }
            view.Render(Model);
        }
        public void Dispose() { }
    }
}
