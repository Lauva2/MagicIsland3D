using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.CalendarSystem;
using MagicIsland.Economy;
using MagicIsland.Inventory;

namespace MagicIsland.Orders
{
    public enum DeliveryResult { Success, InsufficientItems, AlreadyFulfilled, GoldLimit, Busy }
    public sealed class OrderDefinition
    {
        public string Id { get; }
        public string ItemId { get; }
        public SeasonMask Seasons { get; }
        public int MinQuantity { get; }
        public int MaxQuantity { get; }
        public int GoldPerItem { get; }
        public OrderDefinition(string id, string itemId, SeasonMask seasons, int minQuantity, int maxQuantity, int goldPerItem)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(itemId) || !CalendarSystem.Seasons.ValidMask(seasons)
                || minQuantity < 1 || maxQuantity < minQuantity || maxQuantity > 99 || goldPerItem < 1)
                throw new ArgumentException("Invalid order definition");
            Id = id; ItemId = itemId; Seasons = seasons; MinQuantity = minQuantity; MaxQuantity = maxQuantity; GoldPerItem = goldPerItem;
        }
    }
    [Serializable]
    public sealed class SavedOrder
    {
        public int Seed, Day, Quantity;
        public string DefinitionId, ItemId;
        public long Reward;
        public bool Fulfilled;
    }
    public sealed class OrderState
    {
        private readonly OrderDefinition[] definitions;
        private readonly int daysPerSeason;
        private SavedOrder current;
        private bool delivering;
        public event Action Changed;
        public int Day => current.Day;
        public string ItemId => current.ItemId;
        public int Quantity => current.Quantity;
        public long Reward => current.Reward;
        public bool Fulfilled => current.Fulfilled;
        public OrderState(IEnumerable<OrderDefinition> definitions, int daysPerSeason, int seed, int day)
        {
            this.definitions = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
            if (this.definitions.Length == 0 || this.definitions.Any(value => value == null)
                || this.definitions.Select(value => value.Id).Distinct().Count() != this.definitions.Length)
                throw new ArgumentException("Missing or duplicate order definitions");
            for (int i = 0; i < 4; i++) if (!this.definitions.Any(value => CalendarSystem.Seasons.Includes(value.Seasons, (Season)i)))
                throw new ArgumentException("Each season needs an order offer");
            this.daysPerSeason = daysPerSeason;
            current = Generate(seed, day);
        }
        private SavedOrder Generate(int seed, int day)
        {
            if (seed < 1) throw new ArgumentException("Invalid order seed");
            Season season = new CalendarDate(day, daysPerSeason).Season;
            var offers = definitions.Where(value => CalendarSystem.Seasons.Includes(value.Seasons, season)).ToArray();
            uint hash = Hash((uint)seed ^ ((uint)day * 0x9e3779b9u));
            OrderDefinition offer = offers[hash % (uint)offers.Length];
            int quantity = offer.MinQuantity + (int)(Hash(hash ^ 0x85ebca6bu) % (uint)(offer.MaxQuantity - offer.MinQuantity + 1));
            return new SavedOrder { Seed = seed, Day = day, DefinitionId = offer.Id, ItemId = offer.ItemId, Quantity = quantity, Reward = (long)quantity * offer.GoldPerItem };
        }
        private static uint Hash(uint value)
        { unchecked { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16); } }
        public void NextDay(int day)
        {
            if (delivering || day != Day + 1) throw new ArgumentException("Orders must advance with the farm");
            current = Generate(current.Seed, day); Changed?.Invoke();
        }
        public DeliveryResult Check(InventoryState inventory, EconomyState economy)
        {
            if (delivering) return DeliveryResult.Busy;
            if (Fulfilled) return DeliveryResult.AlreadyFulfilled;
            if (inventory.GetCount(ItemId) < Quantity) return DeliveryResult.InsufficientItems;
            return economy.CanEarn(Reward) ? DeliveryResult.Success : DeliveryResult.GoldLimit;
        }
        public DeliveryResult Deliver(InventoryState inventory, EconomyState economy)
        {
            DeliveryResult result = Check(inventory, economy);
            if (result != DeliveryResult.Success) return result;
            delivering = true;
            try
            {
                economy.TryEarn(Reward, false); current.Fulfilled = true;
                // Inventory observers see the reward and fulfilled flag already committed.
                inventory.TryRemove(ItemId, Quantity); economy.NotifyChanged(); Changed?.Invoke();
                return DeliveryResult.Success;
            }
            finally { delivering = false; }
        }
        public SavedOrder Capture() => new SavedOrder { Seed = current.Seed, Day = Day, DefinitionId = current.DefinitionId,
            ItemId = ItemId, Quantity = Quantity, Reward = Reward, Fulfilled = Fulfilled };
        public void Restore(SavedOrder saved, int farmDay)
        {
            if (delivering || saved == null || saved.Day != farmDay) throw new ArgumentException("Invalid saved order day");
            SavedOrder expected = Generate(saved.Seed, saved.Day);
            if (saved.DefinitionId != expected.DefinitionId || saved.ItemId != expected.ItemId || saved.Quantity != expected.Quantity || saved.Reward != expected.Reward)
                throw new ArgumentException("Invalid saved order parameters");
            expected.Fulfilled = saved.Fulfilled; current = expected; Changed?.Invoke();
        }
    }
}
