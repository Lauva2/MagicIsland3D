using System;
using System.Collections.Generic;
using System.Linq;

namespace MagicIsland.Inventory
{
    public sealed class IngredientCost
    {
        public string Id { get; }
        public int Quantity { get; }
        public IngredientCost(string id, int quantity)
        { if (string.IsNullOrWhiteSpace(id) || quantity < 1) throw new ArgumentException("Invalid ingredient"); Id = id; Quantity = quantity; }
    }
    public enum ItemType { Seed, Crop, Material, Tool, Food, Fish, Placeable, Furniture, AnimalProduct }
    public enum InventoryEditResult { Success, Busy, InvalidSlot, EmptySource, SameSlot, StackFull, CannotSplit, NoEmptySlot }

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
        public IEnumerable<ItemDefinition> ItemDefinitions => definitions.Values;
        private readonly Dictionary<string, ItemDefinition> definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private InventorySlot[] slots;
        private bool transferring;
        public IReadOnlyList<InventorySlot> Slots { get; private set; }
        public int Capacity => slots.Length;
        public uint ReorderVersion { get; private set; }
        public bool CanSort => !transferring;
        public event Action Changed;
        private InventoryEditResult CheckEditSource(int index)
        {
            if (transferring) return InventoryEditResult.Busy;
            if (index < 0 || index >= Capacity) return InventoryEditResult.InvalidSlot;
            return slots[index].Empty ? InventoryEditResult.EmptySource : InventoryEditResult.Success;
        }
        private InventoryEditResult CommitSlotEdit(int source, int target, InventorySlot from, InventorySlot to)
        {
            transferring = true;
            try
            {
                slots[source] = from; slots[target] = to;
                unchecked { ReorderVersion++; }
                NotifyChanged(); return InventoryEditResult.Success;
            }
            finally { transferring = false; }
        }
        public InventoryEditResult MoveSlot(int source, int target)
        {
            var check = CheckEditSource(source); if (check != InventoryEditResult.Success) return check;
            if (target < 0 || target >= Capacity) return InventoryEditResult.InvalidSlot;
            if (source == target) return InventoryEditResult.SameSlot;
            var from = slots[source]; var to = slots[target];
            if (to.Empty) return CommitSlotEdit(source, target, default, from);
            if (from.ItemId != to.ItemId) return CommitSlotEdit(source, target, to, from);
            int moved = Math.Min(from.Quantity, definitions[from.ItemId].MaxStack - to.Quantity);
            if (moved == 0) return InventoryEditResult.StackFull;
            return CommitSlotEdit(source, target, moved == from.Quantity ? default : new InventorySlot(from.ItemId, from.Quantity - moved),
                new InventorySlot(to.ItemId, to.Quantity + moved));
        }
        public InventoryEditResult SplitSlot(int source)
        {
            var check = CheckEditSource(source); if (check != InventoryEditResult.Success) return check;
            var from = slots[source]; if (from.Quantity < 2) return InventoryEditResult.CannotSplit;
            int target = Array.FindIndex(slots, slot => slot.Empty);
            if (target < 0) return InventoryEditResult.NoEmptySlot;
            int moved = from.Quantity / 2;
            return CommitSlotEdit(source, target, new InventorySlot(from.ItemId, from.Quantity - moved), new InventorySlot(from.ItemId, moved));
        }
        public bool TrySort()
        {
            if (!CanSort) return false;
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var slot in slots)
                if (!slot.Empty) totals[slot.ItemId] = (totals.TryGetValue(slot.ItemId, out long count) ? count : 0) + slot.Quantity;
            var sorted = new InventorySlot[Capacity];
            int index = 0;
            foreach (string id in totals.Keys.OrderBy(id => definitions[id].Type).ThenBy(id => id, StringComparer.Ordinal))
            {
                long remaining = totals[id];
                while (remaining > 0)
                {
                    int quantity = (int)Math.Min(remaining, definitions[id].MaxStack);
                    sorted[index++] = new InventorySlot(id, quantity);
                    remaining -= quantity;
                }
            }
            transferring = true;
            try
            {
                Array.Copy(sorted, slots, Capacity);
                // Controllers invalidate slot selection before rendering the new order.
                unchecked { ReorderVersion++; }
                NotifyChanged();
                return true;
            }
            finally { transferring = false; }
        }
        public bool CanExpand(int capacity) => !transferring && capacity > Capacity;
        internal bool TryExpand(int capacity, bool notify = true)
        {
            if (!CanExpand(capacity)) return false;
            var expanded = new InventorySlot[capacity]; Array.Copy(slots, expanded, slots.Length);
            transferring = true;
            try { slots = expanded; Slots = Array.AsReadOnly(slots); if (notify) NotifyChanged(); return true; }
            finally { transferring = false; }
        }
        public InventoryState CreateEmpty(int capacity) => new InventoryState(capacity, definitions.Values);
        public ItemDefinition FindDefinition(string id) => id != null && definitions.TryGetValue(id, out var item) ? item : null;

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

        private long FreeSpace(string id)
        {
            var item = FindDefinition(id); if (item == null) return 0;
            long space = 0;
            foreach (var slot in slots) if (slot.Empty) space += item.MaxStack; else if (slot.ItemId == id) space += item.MaxStack - slot.Quantity;
            return space;
        }
        private bool CommitTransfer(InventoryState target, InventoryState sourceCopy, InventoryState targetCopy)
        {
            transferring = target.transferring = true;
            try
            {
                Array.Copy(sourceCopy.slots, slots, Capacity); Array.Copy(targetCopy.slots, target.slots, target.Capacity);
                unchecked { ReorderVersion++; target.ReorderVersion++; }
                NotifyChanged(); target.NotifyChanged(); return true;
            }
            finally { transferring = target.transferring = false; }
        }
        public bool TryTransferAvailableSlotTo(int index, InventoryState target, out int moved, int targetIndex = -1)
        {
            moved = 0;
            if (transferring || target == null || target == this || target.transferring || index < 0 || index >= Capacity || slots[index].Empty || targetIndex < -1 || targetIndex >= target.Capacity) return false;
            var source = slots[index]; var item = target.FindDefinition(source.ItemId); if (item == null) return false;
            long room = targetIndex < 0 ? target.FreeSpace(source.ItemId)
                : target.slots[targetIndex].Empty ? item.MaxStack : target.slots[targetIndex].ItemId == source.ItemId ? item.MaxStack - target.slots[targetIndex].Quantity : 0;
            int quantity = (int)Math.Min(room, source.Quantity); if (quantity < 1) return false;
            var from = CreateEmpty(Capacity); from.Restore(Slots); var to = target.CreateEmpty(target.Capacity); to.Restore(target.Slots);
            from.TryRemoveSlot(index, quantity, false);
            if (targetIndex < 0) to.TryAdd(source.ItemId, quantity, false);
            else to.slots[targetIndex] = new InventorySlot(source.ItemId, to.slots[targetIndex].Quantity + quantity);
            moved = quantity; return CommitTransfer(target, from, to);
        }
        public bool CanTransferMatchingTo(InventoryState target)
        {
            if (transferring || target == null || target == this || target.transferring) return false;
            var ids = new HashSet<string>(target.slots.Where(s => !s.Empty).Select(s => s.ItemId), StringComparer.Ordinal);
            return slots.Any(slot => !slot.Empty && ids.Contains(slot.ItemId) && target.CanAdd(slot.ItemId, 1));
        }
        public bool TryTransferMatchingTo(InventoryState target, out long moved)
        {
            moved = 0;
            if (transferring || target == null || target == this || target.transferring) return false;
            var ids = new HashSet<string>(target.slots.Where(s => !s.Empty).Select(s => s.ItemId), StringComparer.Ordinal);
            var from = CreateEmpty(Capacity); from.Restore(Slots); var to = target.CreateEmpty(target.Capacity); to.Restore(target.Slots);
            for (int i = 0; i < Capacity; i++)
            {
                var slot = from.slots[i]; if (slot.Empty || !ids.Contains(slot.ItemId)) continue;
                int quantity = (int)Math.Min(slot.Quantity, to.FreeSpace(slot.ItemId)); if (quantity < 1) continue;
                to.TryAdd(slot.ItemId, quantity, false); from.TryRemoveSlot(i, quantity, false); moved += quantity;
            }
            return moved > 0 && CommitTransfer(target, from, to);
        }

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

        private bool PrepareAdd(IEnumerable<IngredientCost> products, out InventoryState result)
        {
            result = null; var values = products?.ToArray();
            if (transferring || values == null || values.Length == 0 || values.Any(v => v == null)) return false;
            var copy = CreateEmpty(Capacity); copy.Restore(Slots);
            foreach (var value in values) if (!copy.TryAdd(value.Id, value.Quantity, false)) return false;
            result = copy; return true;
        }
        public bool CanAddAll(IEnumerable<IngredientCost> products) => PrepareAdd(products, out _);
        public bool TryAddAll(IEnumerable<IngredientCost> products, bool notify = true)
        {
            if (!PrepareAdd(products, out var result)) return false;
            transferring = true;
            try { for (int i = 0; i < Capacity; i++) slots[i] = result.slots[i]; if (notify) NotifyChanged(); return true; }
            finally { transferring = false; }
        }

        public bool TryRemoveSlot(int index, int quantity, bool notify = true)
        {
            if (index < 0 || index >= Capacity || quantity < 1 || slots[index].Quantity < quantity) return false;
            var slot = slots[index]; slots[index] = quantity == slot.Quantity ? default : new InventorySlot(slot.ItemId, slot.Quantity - quantity);
            if (notify) NotifyChanged(); return true;
        }
        private bool PrepareExchange(string ingredient, int cost, string output, int yield, out InventoryState result)
        {
            result = null; return !string.IsNullOrWhiteSpace(ingredient) && cost > 0
                && PrepareExchange(new[] { new IngredientCost(ingredient, cost) }, output, yield, out result);
        }
        private bool PrepareExchange(IEnumerable<IngredientCost> ingredients, string output, int yield, out InventoryState result)
        {
            result = null;
            var costs = ingredients?.ToArray();
            if (transferring || yield < 1 || costs == null || costs.Length == 0 || costs.Any(c => c == null || c.Id == output || GetCount(c.Id) < c.Quantity)
                || costs.Select(c => c.Id).Distinct().Count() != costs.Length) return false;
            var copy = CreateEmpty(Capacity); copy.Restore(Slots);
            foreach (var cost in costs) if (!copy.TryRemove(cost.Id, cost.Quantity, false)) return false;
            if (!copy.TryAdd(output, yield, false)) return false;
            result = copy; return true;
        }
        public bool CanExchange(string ingredient, int cost, string output, int yield) => PrepareExchange(ingredient, cost, output, yield, out _);
        public bool CanExchange(IEnumerable<IngredientCost> ingredients, string output, int yield) => PrepareExchange(ingredients, output, yield, out _);
        public bool TryExchange(string ingredient, int cost, string output, int yield)
        {
            if (!PrepareExchange(ingredient, cost, output, yield, out var result)) return false;
            transferring = true;
            try { Restore(result.Slots); return true; }
            finally { transferring = false; }
        }
        public bool TryExchange(IEnumerable<IngredientCost> ingredients, string output, int yield)
        {
            if (!PrepareExchange(ingredients, output, yield, out var result)) return false;
            transferring = true;
            try { Restore(result.Slots); return true; }
            finally { transferring = false; }
        }

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
