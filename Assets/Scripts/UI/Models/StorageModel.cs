namespace MagicIsland.UI.Models
{
    public sealed class StorageModel
    {
        public InventorySlotModel[] BagSlots, ChestSlots;
        public string BagCapacity, ChestCapacity, Details, Feedback, OneLabel, StackLabel;
        public bool CanMoveOne, CanMoveStack;
    }
}
