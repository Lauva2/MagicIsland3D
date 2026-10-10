using System;
using MagicIsland.Economy;
using MagicIsland.Inventory;
internal static class VerifyShop
{
    private static int checks;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    public static int Main()
    {
        var items = new[] { new ItemDefinition("seed", "Seed", ItemType.Seed, 99), new ItemDefinition("crop", "Crop", ItemType.Crop, 99) };
        var bag = new InventoryState(2, items); var gold = new EconomyState(100);
        var shop = new ShopTransactions(bag, gold, "seed", "crop", 10, 25);
        int moneyEvents = 0, bagEvents = 0;
        gold.Changed += () => { moneyEvents++; Check(gold.Gold >= 0, "Observer sees nonnegative gold"); };
        bag.Changed += () => bagEvents++;
        Check(shop.Trade(true, 3) == TradeResult.Success && gold.Gold == 70 && bag.GetCount("seed") == 3, "Purchase quantity and price");
        Check(moneyEvents == 1 && bagEvents == 1, "One notification per successful side");
        Check(shop.Trade(true, 8) == TradeResult.InsufficientGold && gold.Gold == 70 && bag.GetCount("seed") == 3, "Insufficient funds changes nothing");
        Check(shop.Trade(false, 1) == TradeResult.InsufficientItems && gold.Gold == 70, "No free sale income");
        bag.TryAdd("crop", 2);
        Check(shop.Trade(false, 2) == TradeResult.Success && gold.Gold == 120 && bag.GetCount("crop") == 0, "Sale removes stock and pays correct amount");
        bag.TryAdd("seed", 195);
        int beforeGoldEvents = moneyEvents, beforeBagEvents = bagEvents;
        Check(shop.Trade(true, 1) == TradeResult.BagFull && gold.Gold == 120 && bag.GetCount("seed") == 198, "Full bag does not spend gold");
        Check(moneyEvents == beforeGoldEvents && bagEvents == beforeBagEvents, "Failed trade sends no events");
        Check(shop.Trade(true, 0) == TradeResult.InvalidQuantity && shop.Trade(false, -1) == TradeResult.InvalidQuantity, "Invalid quantities rejected");
        bag.TryRemove("seed", 99); bag.TryAdd("crop", 1); gold.Restore(long.MaxValue);
        Check(shop.Trade(false, 1) == TradeResult.GoldLimit && gold.Gold == long.MaxValue && bag.GetCount("crop") == 1, "Gold overflow preserves stock");
        gold.Restore(100); bag.TryRemove("seed", 99);
        TradeResult reentrant = TradeResult.Success;
        bag.Changed += () => { reentrant = shop.Trade(true, 1); Check(gold.Gold == 90 && bag.GetCount("seed") == 1, "Inventory observers see coherent purchase"); };
        Check(shop.Trade(true, 1) == TradeResult.Success && reentrant == TradeResult.Busy, "Nested trades are rejected");
        var bigBag = new InventoryState(1, items); var maxGold = new EconomyState(long.MaxValue);
        var bigShop = new ShopTransactions(bigBag, maxGold, "seed", "crop", int.MaxValue, int.MaxValue);
        Check(bigShop.Trade(true, int.MaxValue) == TradeResult.BagFull && maxGold.Gold == long.MaxValue, "Large multiplication and capacity check are safe");
        Check(!maxGold.TryEarn(1) && !maxGold.TrySpend(-1), "Overflow and negative spending rejected");
        Console.WriteLine("PASS: " + checks + " shop assertions"); return 0;
    }
}
