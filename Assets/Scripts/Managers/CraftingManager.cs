using MagicIsland.Configuration;
using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class CraftingManager : MonoBehaviour
    {
        public CraftingState State { get; private set; }
        public CraftingCatalog Catalog { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public string Feedback { get; private set; }
        public void Initialize(CraftingCatalog catalog, InventoryManager inventory)
        { Clear(); Catalog = catalog; Inventory = inventory; State = new CraftingState(catalog.Definitions()); }
        public CraftingResult Check(int index) => State.Check(index, Inventory.State);
        public bool Craft(int index)
        {
            var result = State.Craft(index, Inventory.State);
            Feedback = result == CraftingResult.Success ? "制作完成，打开背包选择成品进行摆放" : result == CraftingResult.MaterialsMissing ? "木材或石料不足"
                : result == CraftingResult.BagFull ? "背包放不下成品，未消耗材料" : "暂时无法制作";
            return result == CraftingResult.Success;
        }
        public void SetSaveFailed() => Feedback = "制作已完成，保存失败，请重试保存进度";
        public void SetSaved() => Feedback = "制作进度已保存";
        public void Clear() { State = null; Catalog = null; Inventory = null; Feedback = null; }
    }
}
