using MagicIsland.Configuration;
using MagicIsland.Farming;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class FarmManager : MonoBehaviour
    {
        public FarmGrid Grid { get; private set; }
        public FarmState State => Grid != null ? Grid.State : null;
        public InventoryManager Inventory { get; private set; }
        public string SeedItemId { get; private set; }
        public string CropItemId { get; private set; }
        private FarmActions actions;
        public void Initialize(FarmGrid grid, GameConfig config, InventoryManager inventory)
        {
            Grid = grid;
            Inventory = inventory;
            SeedItemId = config.SeedItemId;
            CropItemId = config.HarvestItemId;
            grid.Initialize(config.FarmSize, config.CropGrowthDays);
            actions = new FarmActions(State, inventory.State, SeedItemId, CropItemId);
        }
        public void AdvanceDay() { if (Grid != null) { State.AdvanceDay(); Grid.Refresh(); } }
        public bool TryAct(int index, FarmTool tool, out string message)
        {
            bool acted = actions.TryAct(index, tool, out message);
            if (acted) Grid.Refresh();
            return acted;
        }
        public string Describe(int index, FarmTool tool) => actions.Describe(index, tool);
        public void Clear() { Grid = null; Inventory = null; actions = null; }
    }
}
