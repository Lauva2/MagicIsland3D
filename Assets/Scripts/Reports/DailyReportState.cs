using System;
using System.Globalization;
using System.Numerics;
using MagicIsland.Farming;
using MagicIsland.Animals;
namespace MagicIsland.Reports
{
    [Serializable] public sealed class SavedDailyActivity
    { public string Income = "0", Expense = "0", Harvested = "0"; }
    [Serializable] public sealed class DailyReportSummary
    {
        public int Day, NewMatureCrops, Eggs, QualityEggs;
        public MagicIsland.Shipping.ShippingSale[] Shipping = Array.Empty<MagicIsland.Shipping.ShippingSale>();
        public SavedDailyActivity Activity = new SavedDailyActivity();
    }
    [Serializable] public sealed class SavedDailyReport
    {
        public int Day = 1;
        public SavedDailyActivity Today = new SavedDailyActivity();
        public DailyReportSummary Last = new DailyReportSummary();
        public bool Pending;
    }
    public sealed class DailyReportState
    {
        public int Day { get; private set; }
        public bool Pending { get; private set; }
        private BigInteger income, expense, harvested;
        private DailyReportSummary last;
        public DailyReportSummary Last => Copy(last);
        public DailyReportState(int day = 1)
        { if (day < 1 || day >= int.MaxValue) throw new ArgumentException("Invalid report day"); Day = day; }
        // These recorders do not notify observers during an enclosing gameplay transaction.
        public void RecordMoney(long delta)
        { if (Pending || delta == 0) return; if (delta > 0) income += delta; else expense -= new BigInteger(delta); }
        public void RecordHarvest() { if (!Pending) harvested++; }
        public void NextDay(int day, int mature, int eggs, int qualityEggs, int farmCells, MagicIsland.Shipping.ShippingSale[] shipping = null)
        {
            if (Pending || day != (long)Day + 1 || day >= int.MaxValue || !ValidProduction(mature, eggs, qualityEggs, farmCells))
                throw new InvalidOperationException("Invalid report settlement");
            if (!ValidShipping(shipping ?? Array.Empty<MagicIsland.Shipping.ShippingSale>(), Format(income))) throw new InvalidOperationException("Invalid shipping report");
            last = new DailyReportSummary { Shipping = CopySales(shipping ?? Array.Empty<MagicIsland.Shipping.ShippingSale>()), Day = Day, Activity = Activity(), NewMatureCrops = mature, Eggs = eggs, QualityEggs = qualityEggs };
            income = expense = harvested = BigInteger.Zero; Day = day; Pending = true;
        }
        public bool Acknowledge() { if (!Pending) return false; Pending = false; return true; }
        public void KeepPending() { if (last == null) throw new InvalidOperationException("No report to resume"); Pending = true; }
        private SavedDailyActivity Activity() => new SavedDailyActivity { Income = Format(income), Expense = Format(expense), Harvested = Format(harvested) };
        private static string Format(BigInteger value) => value.ToString(CultureInfo.InvariantCulture);
        public static string Net(SavedDailyActivity value) => Format(BigInteger.Parse(value.Income, CultureInfo.InvariantCulture) - BigInteger.Parse(value.Expense, CultureInfo.InvariantCulture));
        private static bool Number(string value) => value != null && BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 0 && Format(n) == value;
        private static bool ValidActivity(SavedDailyActivity a) => a != null && Number(a.Income) && Number(a.Expense) && Number(a.Harvested);
        private static bool Empty(SavedDailyActivity a) => a.Income == "0" && a.Expense == "0" && a.Harvested == "0";
        private static bool ValidProduction(int mature, int eggs, int quality, int cells) => cells > 0 && mature >= 0 && mature <= cells && eggs >= 0 && quality >= 0
            && (long)eggs + quality <= AnimalState.MaximumChickens;
        public static bool Valid(SavedDailyReport s, int day, int farmCells) => s != null && day > 0 && day < int.MaxValue && s.Day == day && ValidActivity(s.Today)
            && s.Last != null && ValidShipping(s.Last.Shipping, s.Last.Activity?.Income) && (s.Last.Day == 0 ? !s.Pending && ValidActivity(s.Last.Activity) && Empty(s.Last.Activity) && s.Last.NewMatureCrops == 0 && s.Last.Eggs == 0 && s.Last.QualityEggs == 0 && s.Last.Shipping.Length == 0 : day > 1 && s.Last.Day == day - 1 && ValidActivity(s.Last.Activity)
                && ValidProduction(s.Last.NewMatureCrops, s.Last.Eggs, s.Last.QualityEggs, farmCells))
            && (!s.Pending || (s.Last != null && Empty(s.Today)));
        private static MagicIsland.Shipping.ShippingSale[] CopySales(MagicIsland.Shipping.ShippingSale[] sales) => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(sales, s => new MagicIsland.Shipping.ShippingSale { ItemId = s.ItemId, Quantity = s.Quantity, UnitPrice = s.UnitPrice }));
        private static bool ValidShipping(MagicIsland.Shipping.ShippingSale[] sales, string income)
        {
            if (sales == null || sales.Length > MagicIsland.Shipping.ShippingState.Capacity || !Number(income)) return false;
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal); BigInteger total = 0;
            foreach (var sale in sales) { if (sale == null || string.IsNullOrWhiteSpace(sale.ItemId) || !ids.Add(sale.ItemId) || sale.Quantity < 1 || sale.Quantity > (long)MagicIsland.Shipping.ShippingState.Capacity * int.MaxValue || sale.UnitPrice < 1) return false; total += sale.Revenue; }
            return total <= BigInteger.Parse(income, CultureInfo.InvariantCulture);
        }
        private static SavedDailyActivity Copy(SavedDailyActivity a) => a == null ? null : new SavedDailyActivity { Income = a.Income, Expense = a.Expense, Harvested = a.Harvested };
        private static DailyReportSummary Copy(DailyReportSummary s) => s == null ? null : new DailyReportSummary { Shipping = CopySales(s.Shipping), Day = s.Day, Activity = Copy(s.Activity), NewMatureCrops = s.NewMatureCrops, Eggs = s.Eggs, QualityEggs = s.QualityEggs };
        public SavedDailyReport Capture() => new SavedDailyReport { Day = Day, Today = Activity(), Last = Copy(last) ?? new DailyReportSummary(), Pending = Pending };
        public void Restore(SavedDailyReport saved, int farmCells)
        {
            if (!Valid(saved, Day, farmCells)) throw new ArgumentException("Invalid saved daily report");
            income = BigInteger.Parse(saved.Today.Income, CultureInfo.InvariantCulture); expense = BigInteger.Parse(saved.Today.Expense, CultureInfo.InvariantCulture);
            harvested = BigInteger.Parse(saved.Today.Harvested, CultureInfo.InvariantCulture); last = saved.Last.Day == 0 ? null : Copy(saved.Last); Pending = saved.Pending;
        }
        public static int CountNewMature(FarmState farm, bool[] before)
        {
            if (before == null || before.Length != farm.Cells.Length) throw new ArgumentException("Invalid maturity snapshot");
            int count = 0; for (int i = 0; i < before.Length; i++) if (!before[i] && farm.Cells[i].Mature) count++; return count;
        }
    }
}
