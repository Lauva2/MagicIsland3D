using System;
using System.Linq;
using System.Collections.Generic;
using MagicIsland.Inventory;
using MagicIsland.Shipping;
using MagicIsland.Economy;
using MagicIsland.Reports;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyShipping
{
    static int checks;
    static void Check(bool b, string why) { checks++; if (!b) throw new Exception(why); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid shipping save rejected"); }
    static ItemDefinition[] Items(int max = 99) => new[] {
        new ItemDefinition("turnip", "Turnip", ItemType.Crop, max), new ItemDefinition("sardine", "Sardine", ItemType.Fish, max),
        new ItemDefinition("egg", "Egg", ItemType.AnimalProduct, max), new ItemDefinition("quality_egg", "Quality egg", ItemType.AnimalProduct, max),
        new ItemDefinition("seed", "Seed", ItemType.Seed, max) };
    static KeyValuePair<string,int>[] Prices(int price = 25) => new[] { new KeyValuePair<string,int>("turnip", price), new KeyValuePair<string,int>("sardine", 35), new KeyValuePair<string,int>("egg", 25), new KeyValuePair<string,int>("quality_egg", 40) };
    static string Order(InventoryState s) => string.Join("|", s.Slots.Select(x => (x.ItemId ?? "") + ":" + x.Quantity));
    static void Main()
    {
        foreach (int capacity in new[] {10, 20, 30, 32, 56})
        {
            var defs = Items(); var bag = new InventoryState(capacity, defs); var box = new ShippingState(bag, defs, Prices());
            bag.TryAdd("seed", 10); bag.TryAdd("turnip", 5); bag.TryAdd("egg", 2); bag.TryAdd("quality_egg", 3);
            string original = Order(bag); Check(box.Transfer(true, 0, 1) == StorageResult.NoMatchingItems && Order(bag) == original, "Reject seeds without mutation");
            Check(box.Transfer(true, 1, 5) == StorageResult.Success && box.ExpectedIncome == 125 && bag.GetCount("turnip") == 0, "Deposit does not pay immediately");
            Check(box.Transfer(false, 0, 1) == StorageResult.Success && box.ExpectedIncome == 100 && bag.GetCount("turnip") == 1, "Same-day retrieval adjusts preview");
            Check(box.Transfer(true, 2, 2) == StorageResult.Success && box.Transfer(true, 3, 3) == StorageResult.Success && box.ExpectedIncome == 270, "Quality and normal eggs priced separately");
            var saved = box.Capture(); var loaded = new ShippingState(bag, defs, Prices()); loaded.Restore(saved, 1); saved.Slots[0].Quantity = 99;
            Check(loaded.ExpectedIncome == 270 && box.ExpectedIncome == 270, "Save capture and restore own their contents");
            foreach (Action<SavedShipping> mutate in new Action<SavedShipping>[] { s => s.LastSettlementDay=1, s => s.LastSettlementDay=-1, s=>s.Slots=null, s=>s.Slots[0]=null, s=>s.Slots[0].Quantity=100, s=>s.Slots[0].Quantity=-1, s=>s.Slots[0].ItemId="seed", s=>s.Slots[0].ItemId="unknown" })
            { var bad=box.Capture(); mutate(bad); Reject(()=>box.Restore(bad,1)); Check(box.ExpectedIncome==270,"Rejected restore leaves state untouched"); }
            var economy = new EconomyState(100); var report = new DailyReportState(); economy.SetLedger(report.RecordMoney); int changed=0;
            economy.Changed += ()=> { changed++; Check(economy.Gold==370 && box.ExpectedIncome==0 && box.LastSettlementDay==1,"Observers see committed payout, cleared box and settlement marker"); Check(!box.Settle(1,economy,out _),"Observer cannot settle twice"); Check(box.Transfer(true,1,1)==StorageResult.Busy,"Observer cannot deposit during settlement"); };
            Check(box.Settle(1,economy,out var sales) && changed==1 && sales.Length==3,"One batch payout and notification");
            report.NextDay(2,0,0,0,4,sales); Check(report.Last.Activity.Income=="270" && report.Last.Shipping.Sum(s=>(long)s.Revenue)==270,"Shipping counted in ended day's report");
            sales[0].Quantity=999; Check(report.Last.Shipping.All(s=>s.Quantity<999),"Report owns line details");
            Check(!box.Settle(1,economy,out _) && economy.Gold==370,"Duplicate settlement does not pay");
            var settled = new ShippingState(bag,defs,Prices()); settled.Restore(box.Capture(),2); Check(settled.ExpectedIncome==0 && !settled.Settle(1,economy,out _),"Reload does not replay payout");
            Check(settled.Settle(2,economy,out var empty) && empty.Length==0 && settled.LastSettlementDay==2,"Empty day has no payout");
            var farm=new FarmState(2); farm.AdvanceDay(); var data=SaveData.Capture(farm,bag,0,0,0,370,dailyReport:report,shipping:box);
            if (capacity==56) data.InventoryUpgrade = new SavedInventoryUpgrade { BaseSlots=36, Level=2 };
            data.Validate(2,3,bag.CreateEmpty(capacity),shippingPrices:Prices()); Check(data.Version==SaveData.CurrentVersion,"Full current save validates shipping and report");
            data.Version=25; data.Upgrade(100); Check(data.Shipping.Slots.All(s=>s.Quantity==0) && data.Shipping.LastSettlementDay==0 && data.DailyReport.Last.Shipping.Length==0 && data.Gold==370,"Version 25 migration starts empty box without invented payment");
        }
        var small=new InventoryState(1,Items()); small.TryAdd("turnip",99); var full=new ShippingState(small,Items(),Prices()); full.Transfer(true,0,99); small.TryAdd("seed",99);
        Check(full.Transfer(false,0,1)==StorageResult.DestinationFull && full.ExpectedIncome==2475,"Full backpack leaves shipping intact");
        var hugeBag=new InventoryState(1,Items(int.MaxValue)); hugeBag.TryAdd("turnip",int.MaxValue); var huge=new ShippingState(hugeBag,Items(int.MaxValue),Prices(int.MaxValue)); huge.Transfer(true,0,int.MaxValue);
        var overflow=new EconomyState(long.MaxValue); Check(!huge.Settle(1,overflow,out _) && huge.Contents.GetCount("turnip")==int.MaxValue && huge.LastSettlementDay==0,"Gold overflow retains all goods and unsettled marker");
        overflow.Restore(0); Check(huge.Settle(1,overflow,out _) && overflow.Gold==(long)int.MaxValue*int.MaxValue,"Large exact payout without int overflow");
        var many=new ShippingState(hugeBag,Items(int.MaxValue),Prices(int.MaxValue)); var snapshot=many.Capture(); foreach(var slot in snapshot.Slots) { slot.ItemId="turnip"; slot.Quantity=int.MaxValue; } many.Restore(snapshot,1);
        Check(many.ExpectedIncome>long.MaxValue && !many.Settle(1,new EconomyState(0),out _) && many.LastSettlementDay==0,"Total above Int64 cannot destroy goods");
        Console.WriteLine("PASS: "+checks+" shipping transfer, validation, settlement, report and migration assertions");
    }
}
