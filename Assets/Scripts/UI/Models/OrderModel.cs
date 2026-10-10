using UnityEngine;
namespace MagicIsland.UI.Models
{
    public sealed class OrderModel
    {
        public string Date, Request, Stock, Reward, Status, ButtonLabel, Feedback;
        public bool CanDeliver;
        public Sprite Icon;
        public Color IconColor;
    }
}
