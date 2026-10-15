namespace MagicIsland.UI.Models
{
    public sealed class StorageModel
    {
        public InventorySlotModel[] BagSlots, ChestSlots;
        public string BagCapacity, ChestCapacity, Details, Feedback, OneLabel, StackLabel;
        public bool CanMoveOne, CanMoveStack;
        public bool CanDepositMatching, Dragging;
        public int Page, PageCount;
        public string DragName;
        public int DragQuantity;
        public UnityEngine.Sprite DragIcon;
        public UnityEngine.Color DragColor;
    }
}
