using System;
using System.Collections.Generic;
namespace MagicIsland.Inventory
{
    public enum StorageResult { Success, InvalidSlot, EmptySlot, InvalidQuantity, DestinationFull, Busy }
    public sealed class StorageState
    {
        public InventoryState Contents { get; }
        private readonly InventoryState bag;
        private bool moving;
        public event Action Changed;
        public StorageState(int capacity, IEnumerable<ItemDefinition> items, InventoryState bag)
        {
            if (capacity < 1 || capacity > 36) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.bag = bag ?? throw new ArgumentNullException(nameof(bag)); Contents = new InventoryState(capacity, items);
        }
        public StorageResult Check(bool deposit, int index, int quantity)
        {
            if (moving) return StorageResult.Busy;
            var source = deposit ? bag : Contents; var target = deposit ? Contents : bag;
            if (index < 0 || index >= source.Capacity) return StorageResult.InvalidSlot;
            if (source.Slots[index].Empty) return StorageResult.EmptySlot;
            if (quantity < 1 || quantity > source.Slots[index].Quantity) return StorageResult.InvalidQuantity;
            return source.CanTransferSlotTo(index, quantity, target) ? StorageResult.Success : StorageResult.DestinationFull;
        }
        public StorageResult Transfer(bool deposit, int index, int quantity)
        {
            var result = Check(deposit, index, quantity); if (result != StorageResult.Success) return result;
            moving = true;
            try
            {
                var source = deposit ? bag : Contents; var target = deposit ? Contents : bag;
                if (!source.TryTransferSlotTo(index, quantity, target)) return StorageResult.Busy;
                Changed?.Invoke(); return StorageResult.Success;
            }
            finally { moving = false; }
        }
        public InventorySlot[] Capture()
        {
            var slots = new InventorySlot[Contents.Capacity]; for (int i = 0; i < slots.Length; i++) slots[i] = Contents.Slots[i]; return slots;
        }
        public void Restore(IReadOnlyList<InventorySlot> slots)
        {
            if (moving) throw new InvalidOperationException("Storage transfer in progress");
            Contents.Restore(slots); Changed?.Invoke();
        }
    }
}
