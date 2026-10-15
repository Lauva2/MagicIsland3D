using System;
using MagicIsland.Animals;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class AnimalShopController : IUIController
    {
        public AnimalShopModel Model { get; } = new AnimalShopModel();
        private readonly AnimalShopView view; private readonly AnimalManager manager; private readonly UnityAction chicken, feed, sell, close, save, qualitySell;
        public AnimalShopController(AnimalShopView view, AnimalManager manager, Action chicken, Action feed, Action sell, Action close, Action save, Action qualitySell = null)
        { this.view = view; this.manager = manager; this.chicken = () => chicken(); this.feed = () => feed(); this.sell = () => sell(); this.close = () => close(); this.save = () => save(); this.qualitySell = () => qualitySell?.Invoke(); view.QualitySellButton.onClick.AddListener(this.qualitySell); view.ChickenButton.onClick.AddListener(this.chicken); view.FeedButton.onClick.AddListener(this.feed); view.SellButton.onClick.AddListener(this.sell); view.CloseButton.onClick.AddListener(this.close); view.SaveButton.onClick.AddListener(this.save); Tick(); }
        public void Tick()
        {
            if (view == null || manager.State == null) return; var state = manager.State; var d = state.Definition;
            Model.Stock = "金币 " + manager.Economy.Gold + " · 鸡 " + state.Count + " / 2\n背包饲料 " + manager.Inventory.GetCount(d.FeedId) + " · 鸡蛋 " + manager.Inventory.GetCount(d.EggId) + " · 优质蛋 " + manager.Inventory.GetCount(d.QualityEggId);
            Model.ChickenPrice = "鸡\n" + d.ChickenPrice + " 金币 / 只"; Model.FeedPrice = "饲料\n" + d.FeedPrice + " 金币 / 份"; Model.EggPrice = "鸡蛋收购\n" + d.EggPrice + " 金币 / 枚";
            Model.QualityEggPrice = "优质鸡蛋收购\n" + d.QualityEggPrice + " 金币 / 枚";
            Model.CanSellQuality = state.CheckSell(manager.Inventory.State, manager.Economy.State, true) == AnimalResult.Success;
            Model.CanChicken = state.CheckChicken(manager.Economy.State) == AnimalResult.Success; Model.CanFeed = state.CheckFeedPurchase(manager.Inventory.State, manager.Economy.State) == AnimalResult.Success;
            Model.CanSell = state.CheckSell(manager.Inventory.State, manager.Economy.State) == AnimalResult.Success;
            Model.Feedback = manager.Feedback ?? (!state.Built ? "请先在办事处建造鸡舍" : "购买的鸡直接送到鸡舍，饲料放入背包"); view.Render(Model);
        }
        public void Dispose() { if (view == null) return; view.QualitySellButton.onClick.RemoveListener(qualitySell); view.ChickenButton.onClick.RemoveListener(chicken); view.FeedButton.onClick.RemoveListener(feed); view.SellButton.onClick.RemoveListener(sell); view.CloseButton.onClick.RemoveListener(close); view.SaveButton.onClick.RemoveListener(save); }
    }
}
