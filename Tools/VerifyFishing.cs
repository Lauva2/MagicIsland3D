using System;
using System.Linq;
using MagicIsland.FishingSystem;
using MagicIsland.Inventory;
using MagicIsland.Stamina;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyFishing
{
    static int count;
    static void Check(bool ok, string message) { count++; if (!ok) throw new Exception(message); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("Invalid fishing input accepted"); }
    static readonly FishDefinition[] fish = { new FishDefinition("sardine", 70), new FishDefinition("bass", 30) };
    static readonly ItemDefinition[] items = { new ItemDefinition("sardine", "Sardine", ItemType.Fish, 99), new ItemDefinition("bass", "Bass", ItemType.Fish, 99), new ItemDefinition("wood", "Wood", ItemType.Material, 99) };
    static string Snap(InventoryState bag) => string.Join("|", bag.Slots.Select(s => s.ItemId + ":" + s.Quantity));
    static void Main()
    {
        var sampleState = new FishingState(fish);
        for (int i = 0; i < 1000; i++) Check(sampleState.Pick(i / 1000.0) == (i < 700 ? 0 : 1), "Weighted fish boundaries");
        Reject(() => sampleState.Pick(1)); Reject(() => sampleState.Pick(-1)); Reject(() => sampleState.Pick(double.NaN)); Reject(() => new FishingState(fish, double.PositiveInfinity)); Reject(() => new FishingState(new[] { fish[0], fish[0] }));
        for (int energy = 0; energy <= 100; energy++) for (int kind = 0; kind < 2; kind++)
        {
            var bag = new InventoryState(2, items); var stamina = new StaminaState(100); stamina.Restore(energy); var state = new FishingState(fish);
            bool success = energy >= 5; Check((state.Cast(kind, 2, bag, stamina) == FishingResult.Success) == success, "Cast stamina boundary");
            Check(stamina.Current == energy && bag.GetCount(fish[kind].ItemId) == 0, "Cast only begins temporary session");
            if (!success) continue;
            state.Tick(1.999); Check(state.Phase == FishingPhase.Waiting, "Not yet biting"); state.Tick(.0011); Check(state.Phase == FishingPhase.Bite && state.Remaining > 1.99, "Bite transition preserves remainder");
            Check(state.Reel(bag, stamina) == FishingResult.Success && state.Phase == FishingPhase.Caught && stamina.Current == energy - 5 && bag.GetCount(fish[kind].ItemId) == 1, "Catch commits exact fish and stamina");
            Check(state.Reel(bag, stamina) == FishingResult.NotActive && bag.GetCount(fish[kind].ItemId) == 1, "Duplicate reel free");
        }
        for (int stock = 0; stock <= 99; stock++)
        {
            var bag = new InventoryState(2, items); if (stock > 0) { bag.TryAdd("sardine", stock); bag.TryAdd("bass", stock); }
            var stamina = new StaminaState(100); var state = new FishingState(fish); string before = Snap(bag);
            Check((state.Cast(0, 2, bag, stamina) == FishingResult.Success) == (stock < 99), "Either potential catch must fit before cast");
            Check(Snap(bag) == before && stamina.Current == 100, "Capacity check free");
            if (stock < 99) { state.Tick(2); Check(state.Reel(bag, stamina) == FishingResult.Success && bag.GetCount("sardine") == stock + 1 && stamina.Current == 95, "Existing stacks catch capacity"); }
        }
        var inventory = new InventoryState(2, items); var energyState = new StaminaState(100); var fishing = new FishingState(fish);
        fishing.Cast(0, 2, inventory, energyState); Check(fishing.Cast(1, 2, inventory, energyState) == FishingResult.Busy, "Duplicate cast busy"); Check(fishing.Reel(inventory, energyState) == FishingResult.TooEarly && energyState.Current == 100, "Early reel free");
        fishing.Cast(0, 2, inventory, energyState); fishing.Tick(4); Check(fishing.Phase == FishingPhase.Missed && fishing.Reel(inventory, energyState) == FishingResult.NotActive && energyState.Current == 100, "Exact bite deadline expires");
        fishing.Cast(0, 2, inventory, energyState); fishing.Tick(100); Check(fishing.Phase == FishingPhase.Missed && inventory.GetCount("sardine") == 0, "Large frame skips entire bite without reward");
        fishing.Cast(0, 2, inventory, energyState); fishing.Tick(3.999); Check(fishing.Reel(inventory, energyState) == FishingResult.Success, "Last instant inside deadline works");
        inventory.TryRemove("sardine", 1); energyState.Refill(); fishing.Cast(0, 2, inventory, energyState); fishing.Cancel(); fishing.Tick(10); Check(fishing.Phase == FishingPhase.Cancelled && inventory.GetCount("sardine") == 0 && energyState.Current == 100, "Cancel destroys pending catch");
        fishing.Cast(0, 2, inventory, energyState); fishing.Tick(2); inventory.TryAdd("wood", 198); string full = Snap(inventory);
        Check(fishing.Reel(inventory, energyState) == FishingResult.BagFull && Snap(inventory) == full && energyState.Current == 100, "Catch rechecks capacity without stamina loss");
        inventory.TryRemove("wood", 198); fishing.Cast(0, 2, inventory, energyState); fishing.Tick(2); energyState.Restore(4);
        Check(fishing.Reel(inventory, energyState) == FishingResult.StaminaLow && inventory.GetCount("sardine") == 0 && energyState.Current == 4, "Catch rechecks stamina");
        energyState.Refill(); fishing.Cast(1, 2, inventory, energyState); fishing.Tick(2); int notifications = 0;
        Action catchObserver = () => { notifications++; Check(fishing.Phase == FishingPhase.Caught && fishing.CaughtItemId == "bass" && energyState.Current == 95 && inventory.GetCount("bass") == 1, "Observer sees complete catch"); Check(fishing.Reel(inventory, energyState) == FishingResult.Busy && fishing.Cast(0, 2, inventory, energyState) == FishingResult.Busy, "Reentry blocked"); fishing.Cancel(); Check(fishing.Phase == FishingPhase.Caught, "Cancel cannot interrupt commit"); };
        inventory.Changed += catchObserver; Check(fishing.Reel(inventory, energyState) == FishingResult.Success && notifications == 1, "Catch notifies once"); inventory.Changed -= catchObserver;
        Reject(() => fishing.Tick(-1)); Reject(() => fishing.Tick(double.NaN)); Reject(() => fishing.Cast(2, 2, inventory, energyState));
        var salesBag = new InventoryState(2, items); salesBag.TryAdd("sardine", 3); var gold = new EconomyState(100); var shop = new ShopTransactions(salesBag, gold, "sardine", 35);
        Check(shop.Trade(true, 1) == TradeResult.NotForSale && gold.Gold == 100 && salesBag.GetCount("sardine") == 3, "Fish cannot be purchased");
        int salesEvents = 0; salesBag.Changed += () => { salesEvents++; Check(salesBag.GetCount("sardine") == 2 && gold.Gold == 135, "Fish sale atomic"); Check(shop.Trade(false, 1) == TradeResult.Busy, "Fish sale reentry blocked"); };
        Check(shop.Trade(false, 1) == TradeResult.Success && salesEvents == 1, "Sell exactly one fish");
        gold.Restore(long.MaxValue); Check(shop.Trade(false, 1) == TradeResult.GoldLimit && salesBag.GetCount("sardine") == 2, "Overflow preserves fish");
        var storage = new StorageState(2, items, inventory); Check(storage.Transfer(true, 0, 1) == StorageResult.Success && storage.Contents.GetCount("bass") == 1, "Fish can be stored");
        var save = SaveData.Capture(new FarmState(2, 3), inventory, 0, 0, 0, 135, stamina: energyState.Current, storage: storage);
        save.Validate(2, 3, inventory.CreateEmpty(2), storageSlots: 2); Check(save.Version == SaveData.CurrentVersion && save.Storage[0].ItemId == "bass" && save.Gold == 135 && save.Stamina == 95, "Existing schema saves fish/storage/gold/stamina");
        Console.WriteLine("PASS: " + count + " fishing/timing/capacity/sale/save assertions");
    }
}
