using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MagicIsland.Inventory;
using MagicIsland.Economy;
namespace MagicIsland.Shipping
{
    [Serializable] public sealed class ShippingSale
    {
        public string ItemId;
        public long Quantity;
        public int UnitPrice;
        public BigInteger Revenue => new BigInteger(Quantity) * UnitPrice;
    }
    [Serializable] public sealed class ShippingSlot { public string ItemId; public int Quantity; }
    [Serializable] public sealed class SavedShipping
    {
        public int LastSettlementDay;
        public ShippingSlot[] Slots = Enumerable.Range(0, ShippingState.Capacity).Select(_ => new ShippingSlot()).ToArray();
    }
    public sealed class ShippingState
    {
        public const int Capacity = 18;
        private readonly StorageState storage;
        private readonly InventoryState bag;
        private readonly Dictionary<string, int> prices;
        private bool committing;
        public int LastSettlementDay { get; private set; }
        public InventoryState Contents => storage.Contents;
        public event Action Changed;
        public ShippingState(InventoryState bag, IEnumerable<ItemDefinition> items, IEnumerable<KeyValuePair<string, int>> prices)
        {
            this.bag = bag;
            this.prices = prices.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            if (this.prices.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value <= 0)) throw new ArgumentException("Invalid shipping prices");
            storage = new StorageState(Capacity, items, bag);
        }
        public int Price(string id) => id != null && prices.TryGetValue(id, out int value) ? value : 0;
        public StorageResult Check(bool deposit, int index, int quantity)
        {
            if (committing) return StorageResult.Busy;
            if (deposit && index >= 0 && index < bag.Capacity && !bag.Slots[index].Empty && Price(bag.Slots[index].ItemId) == 0) return StorageResult.NoMatchingItems;
            return storage.Check(deposit, index, quantity);
        }
        public StorageResult Transfer(bool deposit, int index, int quantity)
        {
            var result = Check(deposit, index, quantity); if (result != StorageResult.Success) return result;
            committing = true;
            try { result = storage.Transfer(deposit, index, quantity); if (result == StorageResult.Success) Changed?.Invoke(); return result; }
            finally { committing = false; }
        }
        public ShippingSale[] Quote() => Contents.Slots.Where(s => !s.Empty).GroupBy(s => s.ItemId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new ShippingSale { ItemId = g.Key, Quantity = g.Sum(s => (long)s.Quantity), UnitPrice = Price(g.Key) }).ToArray();
        public BigInteger ExpectedIncome => Quote().Aggregate(BigInteger.Zero, (n, s) => n + s.Revenue);
        public bool Settle(int day, EconomyState economy, out ShippingSale[] sales)
        {
            sales = Array.Empty<ShippingSale>();
            if (committing || day < 1 || day >= int.MaxValue || day <= LastSettlementDay || !Contents.CanSort) return false;
            var quote = Quote(); var total = quote.Aggregate(BigInteger.Zero, (n, s) => n + s.Revenue);
            if (quote.Any(s => s.UnitPrice <= 0) || total > long.MaxValue || (total > 0 && !economy.CanEarn((long)total))) return false;
            committing = true;
            try
            {
                // Commit both states before observers run; repeated settlement is rejected, including empty days.
                for (int i = 0; i < Contents.Capacity; i++) if (!Contents.Slots[i].Empty) Contents.TryRemoveSlot(i, Contents.Slots[i].Quantity, false);
                if (total > 0) economy.TryEarn((long)total, false);
                LastSettlementDay = day; sales = quote;
                Contents.NotifyChanged(); if (total > 0) economy.NotifyChanged(); Changed?.Invoke(); return true;
            }
            finally { committing = false; }
        }
        public SavedShipping Capture() => new SavedShipping { LastSettlementDay = LastSettlementDay, Slots = Contents.Slots.Select(s => new ShippingSlot { ItemId = s.ItemId, Quantity = s.Quantity }).ToArray() };
        public void Restore(SavedShipping saved, int day)
        {
            if (saved == null || saved.LastSettlementDay < 0 || saved.LastSettlementDay >= day || saved.Slots == null || saved.Slots.Length != Capacity
                || saved.Slots.Any(s => s == null || (s.Quantity != 0 && Price(s.ItemId) == 0))) throw new ArgumentException("Invalid saved shipping box");
            var slots = saved.Slots.Select(s => new InventorySlot(s.ItemId, s.Quantity)).ToArray();
            var test = Contents.CreateEmpty(Capacity); test.Restore(slots);
            LastSettlementDay = saved.LastSettlementDay; Contents.Restore(slots);
        }
    }
}
