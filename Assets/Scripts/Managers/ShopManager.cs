using MagicIsland.Configuration;
using MagicIsland.Economy;
using UnityEngine;
using System.Collections.Generic;
namespace MagicIsland.Managers
{
    public sealed class ShopManager : MonoBehaviour
    {
        public ItemConfig Seed { get; private set; }
        public ItemConfig Crop { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public EconomyManager Economy { get; private set; }
        public Transform Counter { get; private set; }
        private ShopTransactions transactions;
        public IReadOnlyList<CropConfig> Crops => crops.Crops;
        public int SelectedIndex { get; private set; }
        public bool SelectedIsFish => SelectedIndex >= crops.Crops.Count;
        public IReadOnlyList<ItemConfig> SaleItems => saleItems;
        public string Feedback { get; private set; }
        private readonly List<ItemConfig> saleItems = new List<ItemConfig>();
        private readonly List<ShopTransactions> fishOffers = new List<ShopTransactions>();
        public CalendarManager Calendar { get; private set; }
        private CropCatalog crops;
        private ItemCatalog items;
        private readonly Dictionary<string, ShopTransactions> offers = new Dictionary<string, ShopTransactions>();
        private bool trading;
        public event System.Action<int> CropSold;
        public void Initialize(GameConfig config, ItemCatalog items, InventoryManager inventory, EconomyManager economy, Transform counter, CropCatalog crops, CalendarManager calendar = null, FishingCatalog fishing = null)
        {
            Clear();
            this.items = items; this.crops = crops;
            Calendar = calendar;
            Inventory = inventory; Economy = economy; Counter = counter;
            foreach (CropConfig crop in crops.Crops)
            {
                ItemConfig seed = items.Find(crop.SeedItemId), harvest = items.Find(crop.HarvestItemId);
                offers.Add(crop.Id, new ShopTransactions(inventory.State, economy.State, seed.Id, harvest.Id, seed.BuyPrice, harvest.SellPrice));
                saleItems.Add(harvest);
            }
            if (fishing != null) foreach (var fish in fishing.Fish) { var item = items.Find(fish.ItemId); saleItems.Add(item); fishOffers.Add(new ShopTransactions(inventory.State, economy.State, item.Id, item.SellPrice)); }
            SelectCrop(crops.Crops.FindIndex(crop => crop.SeedItemId == config.SeedItemId));
        }
        public bool InRange(Vector3 position, float reach) => Counter != null
            && Vector3.ProjectOnPlane(Counter.position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public bool SelectCrop(int index)
        {
            if (trading || crops == null || index < 0 || index >= crops.Crops.Count) return false;
            SelectedIndex = index; CropConfig crop = crops.Crops[index];
            Seed = items.Find(crop.SeedItemId); Crop = items.Find(crop.HarvestItemId); transactions = offers[crop.Id]; return true;
        }
        public TradeResult Check(bool buy, int quantity) => trading ? TradeResult.Busy : transactions?.Check(buy, quantity) ?? TradeResult.Busy;
        public bool SelectOffer(int index)
        {
            if (trading || crops == null || index < 0 || index >= saleItems.Count) return false;
            Feedback = null;
            if (index < crops.Crops.Count) return SelectCrop(index);
            SelectedIndex = index; Seed = null; Crop = saleItems[index]; transactions = fishOffers[index - crops.Crops.Count]; return true;
        }
        public TradeResult Trade(bool buy, int quantity)
        {
            if (trading || transactions == null) return TradeResult.Busy;
            trading = true;
            try
            {
                TradeResult result = transactions.Trade(buy, quantity);
                if (!buy && !SelectedIsFish && result == TradeResult.Success) CropSold?.Invoke(quantity);
                return result;
            }
            finally { trading = false; }
        }
        public void SetSaveFailed() => Feedback = "交易已完成，保存失败，请点击保存进度重试";
        public void SetSaved() => Feedback = "交易进度已保存";
        public void Clear() { offers.Clear(); saleItems.Clear(); fishOffers.Clear(); Feedback = null; trading = false; transactions = null; crops = null; items = null; Calendar = null; Seed = Crop = null; Inventory = null; Economy = null; Counter = null; }
    }
}
