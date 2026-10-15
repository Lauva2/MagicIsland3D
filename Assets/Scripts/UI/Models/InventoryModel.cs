using UnityEngine;

namespace MagicIsland.UI.Models
{
    public sealed class InventorySlotModel
    {
        public string Name;
        public int Quantity;
        public Sprite Icon;
        public Color IconColor;
        public bool Selected;
        public bool DragSource;
    }

    public sealed class InventoryModel
    {
        public InventorySlotModel[] Slots;
        public int Occupied;
        public bool CanUseSeed;
        public bool CanSort;
        public int Page, PageCount = 1;
        public bool Dragging;
        public string DragName;
        public int DragQuantity;
        public Sprite DragIcon;
        public Color DragColor;
        public bool IsFood, IsFish, CanUseFood;
        public bool IsPlaceable;
        public bool IsAnimalProduct;
        public bool IsSupply;
        public bool CanPlace;
        public string UseFoodLabel, Feedback;
        public string DetailTitle = "选择物品";
        public string DetailText = "点击一个格子，查看物品详情。";
    }
}
