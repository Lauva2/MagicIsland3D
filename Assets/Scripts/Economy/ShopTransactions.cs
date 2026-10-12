using System;
using MagicIsland.Inventory;
namespace MagicIsland.Economy
{
    public enum TradeResult { Success, InvalidQuantity, InsufficientGold, BagFull, InsufficientItems, GoldLimit, Busy, NotForSale }
    public sealed class ShopTransactions
    {
        private readonly InventoryState inventory;
        private readonly EconomyState economy;
        private readonly string seed, crop;
        private readonly int buyPrice, sellPrice;
        private bool trading;
        public ShopTransactions(InventoryState inventory, EconomyState economy, string seed, string crop, int buyPrice, int sellPrice)
        {
            if (inventory == null || economy == null || string.IsNullOrEmpty(seed) || string.IsNullOrEmpty(crop) || buyPrice <= 0 || sellPrice <= 0)
                throw new ArgumentException("Invalid shop configuration");
            this.inventory = inventory; this.economy = economy; this.seed = seed; this.crop = crop;
            this.buyPrice = buyPrice; this.sellPrice = sellPrice;
        }
        public ShopTransactions(InventoryState inventory, EconomyState economy, string fish, int sellPrice)
        {
            if (inventory == null || economy == null || string.IsNullOrWhiteSpace(fish) || sellPrice <= 0) throw new ArgumentException("Invalid fish sale");
            this.inventory = inventory; this.economy = economy; crop = fish; this.sellPrice = sellPrice;
        }
        public TradeResult Check(bool buy, int quantity)
        {
            if (trading) return TradeResult.Busy;
            if (quantity <= 0) return TradeResult.InvalidQuantity;
            if (buy && seed == null) return TradeResult.NotForSale;
            long total = (long)quantity * (buy ? buyPrice : sellPrice);
            if (buy)
            {
                if (economy.Gold < total) return TradeResult.InsufficientGold;
                if (!inventory.CanAdd(seed, quantity)) return TradeResult.BagFull;
            }
            else
            {
                if (inventory.GetCount(crop) < quantity) return TradeResult.InsufficientItems;
                if (!economy.CanEarn(total)) return TradeResult.GoldLimit;
            }
            return TradeResult.Success;
        }
        public TradeResult Trade(bool buy, int quantity)
        {
            TradeResult result = Check(buy, quantity);
            if (result != TradeResult.Success) return result;
            trading = true;
            try
            {
                long total = (long)quantity * (buy ? buyPrice : sellPrice);
                // Update gold silently first: inventory observers see the completed transaction.
                if (buy) { economy.TrySpend(total, false); inventory.TryAdd(seed, quantity); }
                else { economy.TryEarn(total, false); inventory.TryRemove(crop, quantity); }
                economy.NotifyChanged();
                return TradeResult.Success;
            }
            finally { trading = false; }
        }
    }
}
