using System;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Reports;
using MagicIsland.CalendarSystem;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class DailyReportController : IUIController
    {
        private readonly DailyReportView view;
        private readonly DailyReportManager reports;
        private readonly CalendarManager calendar;
        private readonly UnityAction next, save, menu;
        public DailyReportModel Model { get; } = new DailyReportModel();
        public DailyReportController(DailyReportView view, DailyReportManager reports, CalendarManager calendar, Action next, Action save, Action menu)
        {
            this.view = view; this.reports = reports; this.calendar = calendar; this.next = () => next(); this.save = () => save(); this.menu = () => menu();
            view.ContinueButton.onClick.AddListener(this.next); view.SaveButton.onClick.AddListener(this.save); view.ReturnMenuButton.onClick.AddListener(this.menu); Tick();
        }
        public void Tick()
        {
            if (view == null || reports.State == null) return; var last = reports.State.Last;
            Model.Date = last == null ? "今日经营" : new CalendarDate(last.Day, calendar.Config.DaysPerSeason).DisplayName + " · 经营日报";
            Model.Income = "收入\n" + (last?.Activity.Income ?? "0") + " 金币";
            Model.Expense = "支出\n" + (last?.Activity.Expense ?? "0") + " 金币";
            string net = last == null ? "0" : DailyReportState.Net(last.Activity);
            Model.Net = "金币净变化\n" + (net != "0" && !net.StartsWith("-") ? "+" : "") + net + " 金币";
            Model.Harvest = "当天收获  " + (last?.Activity.Harvested ?? "0") + " 份作物";
            Model.Mature = "今晨新成熟  " + (last?.NewMatureCrops ?? 0) + " 格作物";
            Model.Eggs = "今晨新产蛋\n普通蛋 " + (last?.Eggs ?? 0) + " 枚  ·  优质蛋 " + (last?.QualityEggs ?? 0) + " 枚";
            Model.Shipping = last == null || last.Shipping.Length == 0 ? "当天没有出货" : string.Join("\n", last.Shipping.Select(s => (GameManager.Instance?.Configuration.Items.Find(s.ItemId)?.DisplayName ?? s.ItemId) + $" × {s.Quantity} · 单价 {s.UnitPrice} · {s.Revenue} 金币"));
            Model.Feedback = reports.Feedback ?? "辛苦了一天，休息后体力已恢复。产物仍在农田和鸡舍，记得收取。";
            Model.CanRetrySleepSave = GameManager.Instance.Saves.SleepSavePending;
            Model.CanContinue = reports.State.Pending && GameManager.Instance.CanStartSavedMorning; view.Render(Model);
        }
        public void Dispose()
        { if (view == null) return; view.ContinueButton.onClick.RemoveListener(next); view.SaveButton.onClick.RemoveListener(save); view.ReturnMenuButton.onClick.RemoveListener(menu); }
    }
}
