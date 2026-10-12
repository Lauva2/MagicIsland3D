using System;
using MagicIsland.Managers;
using MagicIsland.Economy;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class ShopController : IUIController
    {
        private readonly ShopView view;
        private readonly ShopManager shop;
        private readonly ShopModel model = new ShopModel();
        private readonly UnityAction close, buy, sell, save;
        private readonly Func<bool, TradeResult> trade;
        private readonly UnityAction[] select;
        public ShopController(ShopView view, ShopManager shop, Action closeShop, Func<bool, TradeResult> trade, Action save)
        {
            this.view = view; this.shop = shop; this.trade = trade; this.save = () => save(); view.SaveButton.onClick.AddListener(this.save);
            close = () => closeShop(); buy = () => Trade(true); sell = () => Trade(false);
            view.CloseButton.onClick.AddListener(close); view.BuyButton.onClick.AddListener(buy); view.SellButton.onClick.AddListener(sell);
            if (shop.SaleItems.Count > view.CropButtons.Length) throw new InvalidOperationException("ShopPanel needs more item options");
            model.CropNames = new string[shop.SaleItems.Count]; select = new UnityAction[view.CropButtons.Length];
            for (int i = 0; i < select.Length; i++)
            {
                int index = i;
                if (i < shop.SaleItems.Count) model.CropNames[i] = shop.SaleItems[i].DisplayName;
                select[i] = () => { if (shop.SelectOffer(index)) { model.Feedback = "点击按钮，每次交易 1 个物品"; Tick(); } };
                view.CropButtons[i].onClick.AddListener(select[i]);
            }
            model.Feedback = "点击按钮，每次交易 1 个物品";
            Tick();
        }
        private void Trade(bool purchase)
        {
            TradeResult result = trade(purchase);
            model.Feedback = result == TradeResult.Success ? (purchase ? "购买成功" : "出售成功") : Explain(result);
            Tick();
        }
        private static string Explain(TradeResult result)
        {
            switch (result)
            {
                case TradeResult.InsufficientGold: return "金币不足";
                case TradeResult.BagFull: return "背包已满";
                case TradeResult.InsufficientItems: return "没有可出售的物品";
                case TradeResult.NotForSale: return "鱼类只收购，不出售";
                case TradeResult.GoldLimit: return "金币已达上限";
                default: return "暂时无法交易";
            }
        }
        public void Tick()
        {
            model.SelectedIndex = shop.SelectedIndex;
            model.Balance = "金币 " + shop.Economy.Gold;
            model.BuyLabel = shop.SelectedIsFish ? "鱼类只收购，不出售" : shop.Seed.DisplayName + " · " + shop.Seed.BuyPrice + " 金币 / 个";
            model.SellLabel = shop.Crop.DisplayName + " · " + shop.Crop.SellPrice + (shop.SelectedIsFish ? " 金币 / 条" : " 金币 / 个");
            model.Stock = shop.SelectedIsFish ? "持有鱼类 " + shop.Inventory.GetCount(shop.Crop.Id) + " · 每次出售 1 条" : "持有种子 " + shop.Inventory.GetCount(shop.Seed.Id) + "    持有作物 " + shop.Inventory.GetCount(shop.Crop.Id);
            if (shop.Calendar != null && !shop.SelectedIsFish) model.Stock += "\n" + shop.Calendar.PlantingDescription(shop.Crops[shop.SelectedIndex]);
            TradeResult buyCheck = shop.Check(true, 1), sellCheck = shop.Check(false, 1);
            model.CanBuy = buyCheck == TradeResult.Success; model.CanSell = sellCheck == TradeResult.Success;
            if (!model.CanBuy && !model.CanSell) model.Feedback = Explain(buyCheck) + " · " + Explain(sellCheck);
            if (shop.Feedback != null) model.Feedback = shop.Feedback;
            view.Render(model);
        }
        public void Dispose()
        { for (int i = 0; i < select.Length; i++) view.CropButtons[i].onClick.RemoveListener(select[i]);
            view.CloseButton.onClick.RemoveListener(close); view.BuyButton.onClick.RemoveListener(buy); view.SellButton.onClick.RemoveListener(sell); view.SaveButton.onClick.RemoveListener(save); }
    }
}
