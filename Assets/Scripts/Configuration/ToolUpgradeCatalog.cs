using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Tools;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [Serializable] public sealed class ToolUpgradeConfig { public FarmTool Tool; public string DisplayName; public int Wood, Stone; public long Gold; }
    [CreateAssetMenu(menuName = "Magic Island/Tool Upgrade Catalog")]
    public sealed class ToolUpgradeCatalog : ScriptableObject
    {
        public const string Address = "ToolUpgradeCatalog";
        public string WorkbenchAddress = "ToolWorkbench";
        public List<ToolUpgradeConfig> Upgrades = new List<ToolUpgradeConfig>();
        public IEnumerable<ToolUpgradeDefinition> Definitions() => Upgrades.Select(c => new ToolUpgradeDefinition(c.Tool, c.Wood, c.Stone, c.Gold));
        public bool Validate(ItemCatalog items)
        {
            try { return !string.IsNullOrWhiteSpace(WorkbenchAddress) && items.Find("wood")?.Type == ItemType.Material && items.Find("stone")?.Type == ItemType.Material
                && Upgrades.All(c => c != null && !string.IsNullOrWhiteSpace(c.DisplayName)) && new ToolUpgradeState(Definitions()) != null; }
            catch (ArgumentException) { return false; }
        }
    }
}
