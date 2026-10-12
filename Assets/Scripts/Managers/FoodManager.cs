using MagicIsland.Configuration;
using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class FoodManager : MonoBehaviour
    {
        public FoodState State { get; private set; }
        public string Feedback { get; private set; }
        public StaminaManager Stamina { get; private set; }
        private InventoryManager inventory;
        public void Initialize(CookingCatalog catalog, InventoryManager inventory, StaminaManager stamina)
        { Clear(); State = new FoodState(catalog.Definitions()); this.inventory = inventory; Stamina = stamina; }
        public FoodResult Check(int index) => State.Check(index, inventory.State, Stamina.State);
        public bool Use(int index)
        {
            var result = State.Use(index, inventory.State, Stamina.State);
            Feedback = result == FoodResult.Success ? "已食用，恢复 " + State.LastRecovery + " 体力" : result == FoodResult.StaminaFull ? "体力已满，未消耗食物" : "暂时无法食用";
            return result == FoodResult.Success;
        }
        public void SetSaveFailed() => Feedback = "已食用，保存失败，请点击保存进度重试";
        public void SetSaved() => Feedback = "进度已保存";
        public void Clear() { State = null; Stamina = null; inventory = null; Feedback = null; }
    }
}
