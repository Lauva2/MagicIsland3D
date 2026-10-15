namespace MagicIsland.UI.Models
{
    public sealed class ShippingModel
    {
        public InventorySlotModel[] BagSlots, BoxSlots;
        public string BagCapacity, ExpectedIncome, Details, Feedback, OneLabel, StackLabel;
        public int Page, PageCount;
        public bool CanMoveOne, CanMoveStack;
    }
}
