using System;
using System.Linq;
using MagicIsland.Economy;
namespace MagicIsland.Inventory
{
    [Serializable] public sealed class SavedInventoryUpgrade { public int BaseSlots = 10, Level; }
    public sealed class InventoryUpgradeDefinition
    {
        private readonly int[] costs;
        public InventoryUpgradeDefinition(params int[] costs)
        { if (costs == null || costs.Length != InventoryUpgradeState.MaximumLevel || costs.Any(c => c <= 0)) throw new ArgumentException("Invalid backpack upgrade costs"); this.costs = costs.ToArray(); }
        public int Cost(int level) => level >= 0 && level < costs.Length ? costs[level] : 0;
    }
    public enum InventoryUpgradeResult { Success, InsufficientGold, MaximumLevel, Busy, InvalidInventory }
    public sealed class InventoryUpgradeState
    {
        public const int SlotsPerUpgrade = 10, MaximumLevel = 2, MaximumBaseSlots = 36;
        public const int MaximumCapacity = MaximumBaseSlots + SlotsPerUpgrade * MaximumLevel;
        public int BaseSlots { get; }
        public int Level { get; private set; }
        public int Capacity => BaseSlots + Level * SlotsPerUpgrade;
        public int NextCapacity => Level == MaximumLevel ? Capacity : Capacity + SlotsPerUpgrade;
        public InventoryUpgradeDefinition Definition { get; }
        public int NextCost => Definition.Cost(Level);
        private bool upgrading;
        public InventoryUpgradeState(InventoryUpgradeDefinition definition, int baseSlots = 10)
        { if (definition == null || baseSlots < 1 || baseSlots > MaximumBaseSlots) throw new ArgumentException("Invalid backpack base capacity"); Definition = definition; BaseSlots = baseSlots; }
        public InventoryUpgradeResult Check(InventoryState bag, EconomyState gold) => upgrading ? InventoryUpgradeResult.Busy
            : bag == null || gold == null || bag.Capacity != Capacity ? InventoryUpgradeResult.InvalidInventory
            : Level >= MaximumLevel ? InventoryUpgradeResult.MaximumLevel : !bag.CanExpand(NextCapacity) ? InventoryUpgradeResult.Busy
            : gold.Gold < NextCost ? InventoryUpgradeResult.InsufficientGold : InventoryUpgradeResult.Success;
        public InventoryUpgradeResult Upgrade(InventoryState bag, EconomyState gold)
        {
            var result = Check(bag, gold); if (result != InventoryUpgradeResult.Success) return result;
            upgrading = true;
            try
            {
                gold.TrySpend(NextCost, false); bag.TryExpand(NextCapacity, false); Level++;
                bag.NotifyChanged(); gold.NotifyChanged(); return InventoryUpgradeResult.Success;
            }
            finally { upgrading = false; }
        }
        public SavedInventoryUpgrade Capture() => new SavedInventoryUpgrade { BaseSlots = BaseSlots, Level = Level };
        public static bool Valid(SavedInventoryUpgrade saved) => saved != null && saved.BaseSlots >= 1 && saved.BaseSlots <= MaximumBaseSlots && saved.Level >= 0 && saved.Level <= MaximumLevel;
        public static int SavedCapacity(SavedInventoryUpgrade saved)
        { if (!Valid(saved)) throw new ArgumentException("Invalid saved backpack upgrade"); return saved.BaseSlots + saved.Level * SlotsPerUpgrade; }
        public void Restore(SavedInventoryUpgrade saved)
        { if (upgrading || !Valid(saved) || saved.BaseSlots != BaseSlots) throw new ArgumentException("Invalid saved backpack upgrade"); Level = saved.Level; }
    }
}
