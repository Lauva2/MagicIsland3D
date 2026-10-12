using System;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Inventory;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class CraftingController : IUIController
    {
        public CraftingModel Model { get; } = new CraftingModel();
        private readonly CraftingView view;
        private readonly CraftingManager manager;
        private readonly UnityAction close, save, upgrade;
        private readonly UnityAction[] actions;
        private readonly UnityAction previous, next;
        private int page;
        private int PageCount => (manager.Catalog.Recipes.Count + actions.Length - 1) / actions.Length;
        public CraftingController(CraftingView view, CraftingManager manager, Action<int> craft, Action close, Action save, Action upgrade)
        {
            this.view = view; this.manager = manager; int count = Math.Min(manager.Catalog.Recipes.Count, view.CraftButtons.Length);
            if (count == 0 || view.PreviousButton == null || view.NextButton == null || view.PageText == null) throw new InvalidOperationException("CraftingPanel is missing pagination");
            Model.Names = new string[count]; Model.Costs = new string[count]; Model.Results = new string[count]; Model.Labels = new string[count]; Model.CanCraft = new bool[count];
            actions = new UnityAction[count]; for (int i = 0; i < count; i++) { int index = i; actions[i] = () => { int recipe = page * count + index; if (recipe < manager.Catalog.Recipes.Count) craft(recipe); }; view.CraftButtons[i].onClick.AddListener(actions[i]); }
            previous = () => { if (page > 0) { page--; Tick(); } }; next = () => { if (page + 1 < PageCount) { page++; Tick(); } };
            view.PreviousButton.onClick.AddListener(previous); view.NextButton.onClick.AddListener(next);
            this.close = () => close(); this.save = () => save(); this.upgrade = () => upgrade();
            view.CloseButton.onClick.AddListener(this.close); view.SaveButton.onClick.AddListener(this.save); view.UpgradeButton.onClick.AddListener(this.upgrade); Tick();
        }
        public void Tick()
        {
            if (view == null || manager.State == null) return;
            Model.Feedback = manager.Feedback ?? "制作后打开背包，选择成品进行摆放";
            for (int i = 0; i < Model.Names.Length; i++)
            {
                int index = page * actions.Length + i;
                if (index >= manager.Catalog.Recipes.Count) { Model.Names[i] = null; Model.CanCraft[i] = false; continue; }
                var r = manager.Catalog.Recipes[index]; Model.Names[i] = r.DisplayName;
                Model.Costs[i] = string.Join("\n", r.Ingredients.Select(c => manager.Inventory.Catalog.Find(c.ItemId).DisplayName + " × " + c.Quantity + " · 持有 " + manager.Inventory.GetCount(c.ItemId)));
                Model.Results[i] = "产出 " + r.Yield + " 件 · " + (r.IsFurniture ? "小屋内摆放" : "农场摆放");
                var result = manager.Check(index); Model.CanCraft[i] = result == CraftingResult.Success;
                Model.Labels[i] = result == CraftingResult.MaterialsMissing ? "材料不足" : result == CraftingResult.BagFull ? "背包已满" : "制作一份";
            }
            view.Render(Model); view.RenderPage(page, PageCount);
        }
        public void Dispose()
        {
            if (view == null) return;
            for (int i = 0; i < actions.Length; i++) view.CraftButtons[i].onClick.RemoveListener(actions[i]);
            view.CloseButton.onClick.RemoveListener(close); view.SaveButton.onClick.RemoveListener(save); view.UpgradeButton.onClick.RemoveListener(upgrade);
            view.PreviousButton.onClick.RemoveListener(previous); view.NextButton.onClick.RemoveListener(next);
        }
    }
}
