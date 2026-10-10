using System;
using System.Collections.Generic;

namespace MagicIsland.Inventory
{
    public enum ItemType { Seed, Crop, Material, Tool }

    public sealed class ItemDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public ItemType Type { get; }
        public int MaxStack { get; }
        public ItemDefinition(string id, string name, ItemType type, int maxStack)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || maxStack < 1)
                throw new ArgumentException("Invalid item definition");
            Id = id; Name = name; Type = type; MaxStack = maxStack;
        }
    }

    public readonly struct InventorySlot
    {
        public string ItemId { get; }
        public int Quantity { get; }
        public bool Empty => Quantity == 0;
        public InventorySlot(string itemId, int quantity) { ItemId = itemId; Quantity = quantity; }
    }

    // Pure state. Add/remove are all-or-nothing, including additions across multiple stacks.
    public sealed class InventoryState
    {
        private readonly Dictionary<string, ItemDefinition> definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private readonly InventorySlot[] slots;
        private bool transferring;
        public IReadOnlyList<InventorySlot> Slots { get; }
        public int Capacity => slots.Length;
        public event Action Changed;
        public InventoryState CreateEmpty(int capacity) => new InventoryState(capacity, definitions.Values);

        public InventoryState(int capacity, IEnumerable<ItemDefinition> items)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (ItemDefinition item in items)
            {
                if (item == null || definitions.ContainsKey(item.Id)) throw new ArgumentException("Duplicate or null item definition");
                definitions.Add(item.Id, item);
            }
            slots = new InventorySlot[capacity];
            Slots = Array.AsReadOnly(slots);
        }

        public long GetCount(string id)
        {
            long count = 0;
            foreach (InventorySlot slot in slots) if (slot.ItemId == id && !slot.Empty) count += slot.Quantity;
            return count;
        }

        public bool CanAdd(string id, int quantity)
        {
            if (id == null || quantity < 1 || !definitions.TryGetValue(id, out ItemDefinition item)) return false;
            long space = 0;
            foreach (InventorySlot slot in slots)
            {
                if (slot.Empty) space += item.MaxStack;
                else if (slot.ItemId == id) space += item.MaxStack - slot.Quantity;
            }
            return space >= quantity;
        }

        public bool TryAdd(string id, int quantity, bool notify = true)
        {
            if (!CanAdd(id, quantity)) return false;
            int remaining = quantity, maxStack = definitions[id].MaxStack;
            // Fill existing stacks before occupying empty slots.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < slots.Length && remaining > 0; i++)
                {
                    InventorySlot slot = slots[i];
                    if (pass == 0 ? slot.Empty || slot.ItemId != id : !slot.Empty) continue;
                    int added = Math.Min(remaining, maxStack - slot.Quantity);
                    slots[i] = new InventorySlot(id, slot.Quantity + added);
                    remaining -= added;
                }
            if (notify) NotifyChanged();
            return true;
        }

        public bool CanTransferSlotTo(int index, int quantity, InventoryState target) => !transferring && target != null && target != this
            && !target.transferring && index >= 0 && index < Capacity && quantity > 0 && quantity <= slots[index].Quantity
            && target.CanAdd(slots[index].ItemId, quantity);

        public bool TryTransferSlotTo(int index, int quantity, InventoryState target)
        {
            if (!CanTransferSlotTo(index, quantity, target)) return false;
            transferring = target.transferring = true;
            try
            {
                var slot = slots[index];
                target.TryAdd(slot.ItemId, quantity, false);
                slots[index] = quantity == slot.Quantity ? default : new InventorySlot(slot.ItemId, slot.Quantity - quantity);
                // Both inventories commit before either observer runs.
                NotifyChanged(); target.NotifyChanged(); return true;
            }
            finally { transferring = target.transferring = false; }
        }

        public bool TryRemove(string id, int quantity, bool notify = true)
        {
            if (id == null || quantity < 1 || !definitions.ContainsKey(id) || GetCount(id) < quantity) return false;
            int remaining = quantity;
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].Empty || slots[i].ItemId != id) continue;
                int removed = Math.Min(remaining, slots[i].Quantity);
                int left = slots[i].Quantity - removed;
                slots[i] = left == 0 ? default : new InventorySlot(id, left);
                remaining -= removed;
            }
            if (notify) NotifyChanged();
            return true;
        }

        public void NotifyChanged() => Changed?.Invoke();

        public void Restore(IReadOnlyList<InventorySlot> saved)
        {
            if (saved == null || saved.Count != Capacity) throw new ArgumentException("Invalid saved slot count");
            for (int i = 0; i < saved.Count; i++)
            {
                InventorySlot slot = saved[i];
                if (slot.Quantity < 0 || (slot.Empty && !string.IsNullOrEmpty(slot.ItemId))
                    || (!slot.Empty && (slot.ItemId == null || !definitions.TryGetValue(slot.ItemId, out ItemDefinition item)
                        || slot.Quantity > item.MaxStack)))
                    throw new ArgumentException("Invalid saved inventory slot");
            }
            for (int i = 0; i < saved.Count; i++) slots[i] = saved[i];
            Changed?.Invoke();
        }
    }
}
