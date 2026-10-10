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
        private readonly UnityAction close, buy, sell;
        private readonly UnityAction[] select;
        public ShopController(ShopView view, ShopManager shop, Action closeShop)
        {
            this.view = view; this.shop = shop;
            close = () => closeShop(); buy = () => Trade(true); sell = () => Trade(false);
            view.CloseButton.onClick.AddListener(close); view.BuyButton.onClick.AddListener(buy); view.SellButton.onClick.AddListener(sell);
            if (shop.Crops.Count > view.CropButtons.Length) throw new InvalidOperationException("ShopPanel needs more crop options");
            model.CropNames = new string[shop.Crops.Count]; select = new UnityAction[view.CropButtons.Length];
            for (int i = 0; i < select.Length; i++)
            {
                int index = i;
                if (i < shop.Crops.Count) model.CropNames[i] = shop.Inventory.Catalog.Find(shop.Crops[i].HarvestItemId).DisplayName;
                select[i] = () => { if (shop.SelectCrop(index)) { model.Feedback = "点击按钮，每次交易 1 个物品"; Tick(); } };
                view.CropButtons[i].onClick.AddListener(select[i]);
            }
            model.Feedback = "点击按钮，每次交易 1 个物品";
            Tick();
        }
        private void Trade(bool purchase)
        {
            TradeResult result = shop.Trade(purchase, 1);
            model.Feedback = result == TradeResult.Success ? (purchase ? "购买成功" : "出售成功") : Explain(result);
            Tick();
        }
        private static string Explain(TradeResult result)
        {
            switch (result)
            {
                case TradeResult.InsufficientGold: return "金币不足";
                case TradeResult.BagFull: return "背包已满";
                case TradeResult.InsufficientItems: return "没有可出售的作物";
                case TradeResult.GoldLimit: return "金币已达上限";
                default: return "暂时无法交易";
            }
        }
        public void Tick()
        {
            model.SelectedIndex = shop.SelectedIndex;
            model.Balance = "金币 " + shop.Economy.Gold;
            model.BuyLabel = shop.Seed.DisplayName + " · " + shop.Seed.BuyPrice + " 金币 / 个";
            model.SellLabel = shop.Crop.DisplayName + " · " + shop.Crop.SellPrice + " 金币 / 个";
            model.Stock = "持有种子 " + shop.Inventory.GetCount(shop.Seed.Id) + "    持有作物 " + shop.Inventory.GetCount(shop.Crop.Id);
            if (shop.Calendar != null) model.Stock += "\n" + shop.Calendar.PlantingDescription(shop.Crops[shop.SelectedIndex]);
            TradeResult buyCheck = shop.Check(true, 1), sellCheck = shop.Check(false, 1);
            model.CanBuy = buyCheck == TradeResult.Success; model.CanSell = sellCheck == TradeResult.Success;
            if (!model.CanBuy && !model.CanSell) model.Feedback = Explain(buyCheck) + " · " + Explain(sellCheck);
            view.Render(model);
        }
        public void Dispose()
        { for (int i = 0; i < select.Length; i++) view.CropButtons[i].onClick.RemoveListener(select[i]);
            view.CloseButton.onClick.RemoveListener(close); view.BuyButton.onClick.RemoveListener(buy); view.SellButton.onClick.RemoveListener(sell); }
    }
}
