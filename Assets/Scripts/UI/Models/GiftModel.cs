namespace MagicIsland.UI.Models
{
    public sealed class GiftModel
    {
        public InventorySlotModel[] Slots;
        public bool[] Eligible;
        public string Title, Status, Guide, Detail, Preview, Feedback, ButtonLabel;
        public bool CanGive;
    }
}
