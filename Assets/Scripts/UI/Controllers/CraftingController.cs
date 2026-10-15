using System;
using System.Collections.Generic;
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
        private readonly List<int[]> pages = new List<int[]>();
        private readonly List<string> pageNames = new List<string>();
        private int PageCount => pages.Count;
        public CraftingController(CraftingView view, CraftingManager manager, Action<int> craft, Action close, Action save, Action upgrade)
        {
            this.view = view; this.manager = manager; int count = Math.Min(manager.Catalog.Recipes.Count, view.CraftButtons.Length);
            if (count == 0 || view.PreviousButton == null || view.NextButton == null || view.PageText == null) throw new InvalidOperationException("CraftingPanel is missing pagination");
            AddPages(r => r.IsPlaceable && !r.IsFurniture, "农场装饰", count);
            AddPages(r => r.IsPlaceable && r.IsFurniture, "室内家具", count);
            AddPages(r => r.IsSupply && !r.IsFarmingSupply, "养殖用品", count);
            AddPages(r => r.IsFarmingSupply, "农田用品", count);
            if (PageCount == 0) throw new InvalidOperationException("CraftingPanel has no recipe category");
            Model.Names = new string[count]; Model.Costs = new string[count]; Model.Results = new string[count]; Model.Labels = new string[count]; Model.CanCraft = new bool[count];
            actions = new UnityAction[count]; for (int i = 0; i < count; i++) { int index = i; actions[i] = () => { if (index < pages[page].Length) craft(pages[page][index]); }; view.CraftButtons[i].onClick.AddListener(actions[i]); }
            previous = () => { if (page > 0) { page--; Tick(); } }; next = () => { if (page + 1 < PageCount) { page++; Tick(); } };
            view.PreviousButton.onClick.AddListener(previous); view.NextButton.onClick.AddListener(next);
            this.close = () => close(); this.save = () => save(); this.upgrade = () => upgrade();
            view.CloseButton.onClick.AddListener(this.close); view.SaveButton.onClick.AddListener(this.save); view.UpgradeButton.onClick.AddListener(this.upgrade); Tick();
        }
        private void AddPages(Func<MagicIsland.Configuration.CraftingRecipeConfig, bool> filter, string name, int count)
        {
            var indices = Enumerable.Range(0, manager.Catalog.Recipes.Count).Where(i => filter(manager.Catalog.Recipes[i])).ToArray();
            for (int start = 0; start < indices.Length; start += count)
            { pages.Add(indices.Skip(start).Take(count).ToArray()); pageNames.Add(name); }
        }
        public void Tick()
        {
            if (view == null || manager.State == null) return;
            Model.Feedback = manager.Feedback ?? (pageNames[page] == "养殖用品" ? "用品制作后进入背包 · 带到鸡舍喂食" : pageNames[page] == "农田用品" ? "肥料制作后进入背包 · 选择 7 肥料，再点击已耕作农田" : "制作后打开背包，选择成品进行摆放");
            for (int i = 0; i < Model.Names.Length; i++)
            {
                if (i >= pages[page].Length) { Model.Names[i] = null; Model.CanCraft[i] = false; continue; }
                int index = pages[page][i];
                var r = manager.Catalog.Recipes[index]; Model.Names[i] = r.DisplayName;
                Model.Costs[i] = string.Join("\n", r.Ingredients.Select(c => manager.Inventory.Catalog.Find(c.ItemId).DisplayName + " × " + c.Quantity + " · 持有 " + manager.State.Ingredients.Count(manager.Inventory.State, c.ItemId)));
                Model.Results[i] = "产出 " + r.Yield + (r.IsSupply ? (r.IsFarmingSupply ? " 份 · 农田用品" : " 份 · 养殖用品") : " 件 · " + (r.IsFurniture ? "小屋内摆放" : "农场摆放"));
                var result = manager.Check(index); Model.CanCraft[i] = result == CraftingResult.Success;
                Model.Labels[i] = result == CraftingResult.MaterialsMissing ? "材料不足" : result == CraftingResult.BagFull ? "背包已满" : "制作一份";
            }
            view.Render(Model); view.RenderPage(page, PageCount, pageNames[page]);
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
