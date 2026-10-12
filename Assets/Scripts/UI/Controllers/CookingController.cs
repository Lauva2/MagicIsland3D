using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class CookingController : IUIController
    {
        private readonly CookingView view;
        private readonly CookingManager cooking;
        private readonly UnityAction[] cook;
        private readonly UnityAction close, save;
        public CookingModel Model { get; } = new CookingModel();
        public CookingController(CookingView view, CookingManager cooking, Action<int> cookRecipe, Action close, Action save)
        {
            this.view = view; this.cooking = cooking; this.close = () => close(); this.save = () => save(); int count = cooking.State.Definitions.Count;
            if (view.CookButtons.Length < count) throw new InvalidOperationException("CookingPanel needs enough recipe cards");
            Model.Titles = new string[count]; Model.Ingredients = new string[count]; Model.Results = new string[count]; Model.Labels = new string[count]; Model.CanCook = new bool[count]; cook = new UnityAction[count];
            for (int i = 0; i < count; i++) { int index = i; cook[i] = () => cookRecipe(index); view.CookButtons[i].onClick.AddListener(cook[i]); }
            view.CloseButton.onClick.AddListener(this.close); view.SaveButton.onClick.AddListener(this.save); Tick();
            UnityEngine.Canvas.ForceUpdateCanvases(); view.RecipeScroll.verticalNormalizedPosition = 1;
        }
        public void Tick()
        {
            if (view == null || cooking.State == null) return;
            for (int i = 0; i < cook.Length; i++)
            {
                var recipe = cooking.Catalog.Recipes[i]; var rule = cooking.State.Definitions[i];
                Model.Titles[i] = recipe.DisplayName;
                Model.Ingredients[i] = string.Join("\n", rule.Ingredients.Select(c => cooking.Inventory.Catalog.Find(c.Id).DisplayName + " × " + c.Quantity + " · 持有 " + cooking.Inventory.GetCount(c.Id)));
                Model.Results[i] = "制作 " + recipe.OutputQuantity + " 份 · 恢复 " + recipe.StaminaRecovery + " 体力";
                var check = cooking.Check(i); Model.CanCook[i] = check == CookingResult.Success;
                if (check == CookingResult.Locked) Model.Results[i] += "\n" + (cooking.Stories?.UnlockHint(rule.RequiredEventId) ?? "食谱未解锁");
                Model.Labels[i] = check == CookingResult.Locked ? "尚未解锁" : Model.CanCook[i] ? "制作一份" : check == CookingResult.InsufficientIngredients ? "食材不足" : "背包空间不足";
            }
            Model.Feedback = cooking.Feedback ?? "使用背包中的作物和鱼类制作 · 料理可食用或送礼"; view.Render(Model);
        }
        public void Dispose()
        {
            if (view == null) return;
            for (int i = 0; i < cook.Length; i++) view.CookButtons[i].onClick.RemoveListener(cook[i]);
            view.CloseButton.onClick.RemoveListener(close); view.SaveButton.onClick.RemoveListener(save);
        }
    }
}
