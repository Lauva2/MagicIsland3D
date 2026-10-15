using System;
using System.Linq;
using MagicIsland.Reports;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Saving;
class VerifyDailyReports
{
    static int checks;
    static void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } Check(rejected, "Invalid daily report rejected"); }
    static void Main()
    {
        for (int day = 1; day <= 100; day++)
        {
            var r = new DailyReportState(day); var gold = new EconomyState(1000); gold.SetLedger(r.RecordMoney);
            int events = 0; gold.Changed += () => { events++; Check(r.Capture().Today.Income == "70" && r.Capture().Today.Expense == "25", "Ledger committed before gold observers"); };
            gold.TryEarn(70, false); gold.TrySpend(25); Check(events == 1, "Silent and notifying transactions both tracked exactly once"); gold.NotifyChanged();
            Check(!gold.TryEarn(-1) && !gold.TrySpend(9999) && r.Capture().Today.Income == "70" && r.Capture().Today.Expense == "25", "Rejected payments excluded");
            r.RecordHarvest(); r.RecordHarvest(); r.NextDay(day + 1, 3, 1, 1, 9);
            Check(r.Pending && r.Day == day + 1 && r.Last.Day == day && r.Last.Activity.Harvested == "2" && DailyReportState.Net(r.Last.Activity) == "45", "Previous-day snapshot and next-day reset");
            Check(r.Last.NewMatureCrops == 3 && r.Last.Eggs == 1 && r.Last.QualityEggs == 1 && r.Capture().Today.Income == "0", "Production deltas and empty next-day ledger");
            var copy = r.Capture(); copy.Last.Activity.Income = "999"; r.Last.Activity.Income = "333"; Check(r.Last.Activity.Income == "70", "Capture and summary query are independent");
            var loaded = new DailyReportState(day + 1); loaded.Restore(r.Capture(), 9); Check(loaded.Pending && loaded.Last.Activity.Income == "70", "Pending report restores exactly");
            loaded.RecordMoney(99); loaded.RecordHarvest(); Check(loaded.Capture().Today.Income == "0" && loaded.Capture().Today.Harvested == "0", "Pending summary cannot accumulate next-day activities");
            Reject(() => loaded.NextDay(day + 2, 0, 0, 0, 9)); Check(loaded.Acknowledge() && !loaded.Acknowledge() && loaded.Day == day + 1, "Acknowledgement only clears flag, once");
            loaded.KeepPending(); Check(loaded.Pending, "Failed acknowledgement save can resume same report"); loaded.Acknowledge(); loaded.RecordMoney(-12); loaded.RecordHarvest(); loaded.NextDay(day + 2, 0, 0, 0, 9);
            Check(loaded.Last.Activity.Income == "0" && loaded.Last.Activity.Expense == "12" && loaded.Last.Activity.Harvested == "1" && DailyReportState.Net(loaded.Last.Activity) == "-12", "No carryover or duplicate previous production");
        }
        var huge = new DailyReportState(); var economy = new EconomyState(0); economy.SetLedger(huge.RecordMoney);
        for (int i = 0; i < 5; i++) { economy.TryEarn(long.MaxValue, false); economy.TrySpend(long.MaxValue, false); }
        huge.NextDay(2, 0, 0, 0, 4); Check(huge.Last.Activity.Income == "46116860184273879035" && huge.Last.Activity.Expense == huge.Last.Activity.Income && DailyReportState.Net(huge.Last.Activity) == "0", "Daily turnover above Int64 remains exact and serializable");
        var h = new DailyReportState(2); h.Restore(huge.Capture(), 4); Check(h.Last.Activity.Income == huge.Last.Activity.Income, "Large turnover reload without precision loss");
        foreach (Action<SavedDailyReport> mutate in new Action<SavedDailyReport>[] {
            s => s.Day = 3, s => s.Last.Day = 0, s => s.Last = null, s => s.Today = null, s => s.Last.Activity = null,
            s => s.Today.Income = "01", s => s.Today.Expense = "-1", s => s.Today.Harvested = "1", s => s.Last.Activity.Income = "NaN",
            s => s.Last.Activity.Expense = null, s => s.Last.Activity.Harvested = "1.5", s => s.Last.NewMatureCrops = 5,
            s => s.Last.Eggs = -1, s => s.Last.QualityEggs = 3 })
        { var bad = huge.Capture(); mutate(bad); Reject(() => h.Restore(bad, 4)); Check(h.Pending && h.Last.Activity.Income == huge.Last.Activity.Income, "Invalid restore leaves report untouched"); }
        var farm = new FarmState(2); var cells = farm.Cells;
        for (int i = 0; i < 3; i++) { cells[i].Tilled = cells[i].Planted = true; cells[i].CropId = "turnip"; cells[i].Growth = i == 0 ? 3 : 2; cells[i].Watered = i != 2; }
        var before = cells.Select(c => c.Mature).ToArray(); farm.AdvanceDay(); Check(DailyReportState.CountNewMature(farm, before) == 1 && cells[0].Mature && !cells[2].Mature, "Count newly mature cells, excludes old maturity and dry plants");
        var items = new[] { new ItemDefinition("seed", "Seed", ItemType.Seed, 99), new ItemDefinition("crop", "Crop", ItemType.Crop, 99) };
        var bag = new InventoryState(1, items); var harvest = new DailyReportState(farm.Day); var actions = new FarmActions(farm, bag, "seed", "crop"); actions.SetHarvestRecorder(harvest.RecordHarvest);
        bag.TryAdd("seed", 99); Check(!actions.TryAct(0, FarmTool.Hoe, out _) && harvest.Capture().Today.Harvested == "0", "Full bag harvest excluded"); bag.TryRemove("seed", 99);
        bag.Changed += () => Check(harvest.Capture().Today.Harvested == "1" && !cells[0].Planted, "Harvest ledger complete before inventory observers"); Check(actions.TryAct(0, FarmTool.Hoe, out _), "Successful crop harvest counted");
        for (int version = 1; version <= 23; version++)
        {
            var save = SaveData.Capture(farm, bag, 0, 0, 0, 123); save.Version = version; save.Upgrade(100);
            Check(save.Version == SaveData.CurrentVersion && save.DailyReport.Day == farm.Day && !save.DailyReport.Pending && save.DailyReport.Last.Day == 0 && save.DailyReport.Today.Income == "0", "Legacy migration starts empty current-day ledger without invented report");
        }
        var current = SaveData.Capture(farm, bag, 0, 0, 0, dailyReport: huge); current.Upgrade(100); current.Validate(2, 3, bag.CreateEmpty(1));
        Check(current.DailyReport.Pending && current.DailyReport.Last.Activity.Income == huge.Last.Activity.Income, "Current save upgrade and validation preserve pending report");
        Console.WriteLine("PASS: " + checks + " daily report ledger, production, modal state, migration and precision assertions");
    }
}
