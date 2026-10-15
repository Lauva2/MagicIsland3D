using System;
using System.Collections.Generic;
using System.Linq;
namespace MagicIsland.Inventory
{
    public enum StorageResult { Success, InvalidSlot, EmptySlot, InvalidQuantity, DestinationFull, Busy, TargetOccupied, NoMatchingItems }
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
        private StorageResult CheckSource(bool deposit, int index)
        {
            if (moving || !bag.CanSort || !Contents.CanSort) return StorageResult.Busy;
            var source = deposit ? bag : Contents;
            if (index < 0 || index >= source.Capacity) return StorageResult.InvalidSlot;
            return source.Slots[index].Empty ? StorageResult.EmptySlot : StorageResult.Success;
        }
        public StorageResult TransferAvailable(bool deposit, int index, out int moved, int targetIndex = -1)
        {
            moved = 0; var result = CheckSource(deposit, index); if (result != StorageResult.Success) return result;
            var source = deposit ? bag : Contents; var target = deposit ? Contents : bag;
            if (targetIndex < -1 || targetIndex >= target.Capacity) return StorageResult.InvalidSlot;
            if (targetIndex >= 0 && !target.Slots[targetIndex].Empty && target.Slots[targetIndex].ItemId != source.Slots[index].ItemId) return StorageResult.TargetOccupied;
            moving = true;
            try { if (!source.TryTransferAvailableSlotTo(index, target, out moved, targetIndex)) return StorageResult.DestinationFull; Changed?.Invoke(); return StorageResult.Success; }
            finally { moving = false; }
        }
        public bool CanDepositMatching => !moving && bag.CanTransferMatchingTo(Contents);
        public StorageResult DepositMatching(out long moved)
        {
            moved = 0;
            if (moving || !bag.CanSort || !Contents.CanSort) return StorageResult.Busy;
            if (!bag.Slots.Any(slot => !slot.Empty && Contents.GetCount(slot.ItemId) > 0)) return StorageResult.NoMatchingItems;
            moving = true;
            try { if (!bag.TryTransferMatchingTo(Contents, out moved)) return StorageResult.DestinationFull; Changed?.Invoke(); return StorageResult.Success; }
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
