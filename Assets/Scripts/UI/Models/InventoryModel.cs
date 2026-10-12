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
    }

    public sealed class InventoryModel
    {
        public InventorySlotModel[] Slots;
        public int Occupied;
        public bool CanUseSeed;
        public bool IsFood, IsFish, CanUseFood;
        public bool IsPlaceable;
        public bool CanPlace;
        public string UseFoodLabel, Feedback;
        public string DetailTitle = "选择物品";
        public string DetailText = "点击一个格子，查看物品详情。";
    }
}
