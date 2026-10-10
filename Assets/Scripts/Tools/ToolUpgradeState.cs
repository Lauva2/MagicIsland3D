using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Economy;
namespace MagicIsland.Tools
{
    public enum UpgradeResult { Success, InvalidTool, AlreadyUpgraded, InsufficientMaterials, InsufficientGold, Busy }
    public sealed class ToolUpgradeDefinition
    {
        public FarmTool Tool { get; }
        public int Wood { get; }
        public int Stone { get; }
        public long Gold { get; }
        public ToolUpgradeDefinition(FarmTool tool, int wood, int stone, long gold)
        {
            if ((tool != FarmTool.Axe && tool != FarmTool.Pickaxe) || wood < 1 || stone < 1 || gold < 1) throw new ArgumentException("Invalid tool upgrade definition");
            Tool = tool; Wood = wood; Stone = stone; Gold = gold;
        }
    }
    [Serializable] public sealed class SavedToolUpgrade { public FarmTool Tool; public int Level; }
    public sealed class ToolUpgradeState
    {
        public IReadOnlyList<ToolUpgradeDefinition> Definitions { get; }
        private readonly ToolUpgradeDefinition[] definitions;
        private readonly int[] levels = new int[2];
        private bool upgrading;
        public event Action Changed;
        public ToolUpgradeState(IEnumerable<ToolUpgradeDefinition> definitions)
        {
            this.definitions = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
            if (this.definitions.Length != 2 || this.definitions.Any(d => d == null) || this.definitions[0].Tool != FarmTool.Axe || this.definitions[1].Tool != FarmTool.Pickaxe)
                throw new ArgumentException("Upgrades require axe and pickaxe in order");
            Definitions = Array.AsReadOnly(this.definitions);
        }
        private int Index(FarmTool tool) => tool == FarmTool.Axe ? 0 : tool == FarmTool.Pickaxe ? 1 : -1;
        public int Level(FarmTool tool) => Index(tool) < 0 ? 0 : levels[Index(tool)];
        public int Damage(FarmTool tool) => 1 + Level(tool);
        public UpgradeResult Check(FarmTool tool, InventoryState inventory, EconomyState economy)
        {
            if (upgrading) return UpgradeResult.Busy;
            int index = Index(tool); if (index < 0) return UpgradeResult.InvalidTool;
            if (levels[index] == 1) return UpgradeResult.AlreadyUpgraded;
            var cost = definitions[index];
            if (inventory.GetCount("wood") < cost.Wood || inventory.GetCount("stone") < cost.Stone) return UpgradeResult.InsufficientMaterials;
            return economy.Gold < cost.Gold ? UpgradeResult.InsufficientGold : UpgradeResult.Success;
        }
        public UpgradeResult Upgrade(FarmTool tool, InventoryState inventory, EconomyState economy)
        {
            var result = Check(tool, inventory, economy); if (result != UpgradeResult.Success) return result;
            upgrading = true;
            try
            {
                int index = Index(tool); var cost = definitions[index];
                // Publish only after materials, gold and level all commit.
                inventory.TryRemove("wood", cost.Wood, false); inventory.TryRemove("stone", cost.Stone, false);
                economy.TrySpend(cost.Gold, false); levels[index] = 1;
                inventory.NotifyChanged(); economy.NotifyChanged(); Changed?.Invoke(); return UpgradeResult.Success;
            }
            finally { upgrading = false; }
        }
        public SavedToolUpgrade[] Capture() => definitions.Select((d, i) => new SavedToolUpgrade { Tool = d.Tool, Level = levels[i] }).ToArray();
        public void Restore(SavedToolUpgrade[] saved)
        {
            if (upgrading || saved == null || saved.Length != 2 || saved.Where((s, i) => s == null || s.Tool != definitions[i].Tool || s.Level < 0 || s.Level > 1).Any())
                throw new ArgumentException("Invalid saved tool upgrades");
            for (int i = 0; i < 2; i++) levels[i] = saved[i].Level;
            Changed?.Invoke();
        }
    }
}
