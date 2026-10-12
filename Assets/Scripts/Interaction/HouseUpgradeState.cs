using System;
using MagicIsland.Inventory;
using MagicIsland.Economy;
namespace MagicIsland.Interaction
{
    public enum HouseUpgradeResult { Success, InsufficientMaterials, InsufficientGold, Building, Completed, InvalidDay, Busy }
    public sealed class HouseUpgradeDefinition
    {
        public int Wood { get; }
        public int Stone { get; }
        public long Gold { get; }
        public int Days { get; }
        public HouseUpgradeDefinition(int wood, int stone, long gold, int days = 1)
        { if (wood < 1 || stone < 1 || gold < 1 || days < 1 || days > 30) throw new ArgumentException("Invalid house upgrade costs/duration"); Wood = wood; Stone = stone; Gold = gold; Days = days; }
    }
    [Serializable] public sealed class SavedHouseUpgrade { public int Level; public int CompletionDay; }
    public sealed class HouseUpgradeState
    {
        public int Level { get; private set; }
        public int CompletionDay { get; private set; }
        public bool Building => CompletionDay > 0;
        public HouseUpgradeDefinition Definition { get; }
        private bool committing;
        public HouseUpgradeState(HouseUpgradeDefinition definition) => Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        public HouseUpgradeResult Check(int day, InventoryState inventory, EconomyState economy)
        {
            if (committing) return HouseUpgradeResult.Busy;
            if (Level == 1) return HouseUpgradeResult.Completed;
            if (Building) return HouseUpgradeResult.Building;
            if (day < 1 || (long)day + Definition.Days >= int.MaxValue) return HouseUpgradeResult.InvalidDay;
            if (inventory.GetCount("wood") < Definition.Wood || inventory.GetCount("stone") < Definition.Stone) return HouseUpgradeResult.InsufficientMaterials;
            return economy.Gold < Definition.Gold ? HouseUpgradeResult.InsufficientGold : HouseUpgradeResult.Success;
        }
        public HouseUpgradeResult Request(int day, InventoryState inventory, EconomyState economy)
        {
            var result = Check(day, inventory, economy); if (result != HouseUpgradeResult.Success) return result;
            committing = true;
            try
            {
                inventory.TryRemove("wood", Definition.Wood, false); inventory.TryRemove("stone", Definition.Stone, false); economy.TrySpend(Definition.Gold, false);
                CompletionDay = day + Definition.Days;
                inventory.NotifyChanged(); economy.NotifyChanged(); return HouseUpgradeResult.Success;
            }
            finally { committing = false; }
        }
        public bool Complete(int day)
        {
            if (committing || !Building || day < 1 || day >= int.MaxValue || day < CompletionDay) return false;
            Level = 1; CompletionDay = 0; return true;
        }
        public static bool Valid(SavedHouseUpgrade saved, int day)
            => saved != null && day >= 1 && day < int.MaxValue && ((saved.Level == 0 && (saved.CompletionDay == 0
                || (saved.CompletionDay > day && (long)saved.CompletionDay <= (long)day + 30 && saved.CompletionDay < int.MaxValue)))
                || (saved.Level == 1 && saved.CompletionDay == 0));
        public void Restore(SavedHouseUpgrade saved, int day)
        { if (committing || !Valid(saved, day)) throw new ArgumentException("Invalid saved house upgrade"); Level = saved.Level; CompletionDay = saved.CompletionDay; }
        public SavedHouseUpgrade Capture() => new SavedHouseUpgrade { Level = Level, CompletionDay = CompletionDay };
    }
}
