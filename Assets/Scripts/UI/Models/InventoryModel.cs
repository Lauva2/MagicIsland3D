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
        public string DetailTitle = "选择物品";
        public string DetailText = "点击一个格子，查看物品详情。";
    }
}
