using MagicIsland.Inventory;

namespace MagicIsland.Farming
{
    // Coordinate farm changes with inventory changes before notifying inventory observers.
    public sealed class FarmActions
    {
        private readonly FarmState farm;
        private readonly InventoryState inventory;
        private readonly string seedId, cropId;
        public FarmActions(FarmState farm, InventoryState inventory, string seedId, string cropId)
        { this.farm = farm; this.inventory = inventory; this.seedId = seedId; this.cropId = cropId; }

        public string Describe(int index, FarmTool tool)
        {
            FarmCell cell = farm.Cells[index];
            if (cell.Mature) return inventory.CanAdd(cropId, 1) ? "收获" : "背包已满，先腾出空间";
            if (tool == FarmTool.Seeds && cell.Tilled && !cell.Planted && inventory.GetCount(seedId) < 1)
                return "种子不足";
            return farm.Describe(index, tool);
        }

        public bool TryAct(int index, FarmTool tool, out string message)
        {
            message = Describe(index, tool);
            FarmCell cell = farm.Cells[index];
            if (cell.Mature)
            {
                if (!inventory.CanAdd(cropId, 1)) return false;
                farm.TryAct(index, tool, out message);
                inventory.TryAdd(cropId, 1);
                return true;
            }
            if (tool == FarmTool.Seeds)
            {
                if (!cell.Tilled || cell.Planted || inventory.GetCount(seedId) < 1) return false;
                farm.TryAct(index, tool, out message);
                inventory.TryRemove(seedId, 1);
                return true;
            }
            return farm.TryAct(index, tool, out message);
        }
    }
}
