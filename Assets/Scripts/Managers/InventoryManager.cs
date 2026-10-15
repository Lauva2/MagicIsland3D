using System;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class InventoryManager : MonoBehaviour
    {
        public InventoryState State { get; private set; }
        public ItemCatalog Catalog { get; private set; }
        public InventoryUpgradeState UpgradeState { get; private set; }
        public string UpgradeFeedback { get; private set; }
        public string SortFeedback { get; private set; }
        public bool SortSavePending { get; private set; }
        public string EditFeedback { get; private set; }
        public bool EditSavePending { get; private set; }
        public int DragSource { get; private set; } = -1;
        public bool IsDragging => DragSource >= 0;
        private InventorySlot dragSnapshot;
        private uint dragRevision;
        private int dragCapacity;
        public bool DragValid => IsDragging && State != null && State.Capacity == dragCapacity && State.ReorderVersion == dragRevision
            && State.Slots[DragSource].ItemId == dragSnapshot.ItemId && State.Slots[DragSource].Quantity == dragSnapshot.Quantity;
        public bool BeginDrag(int index)
        {
            if (IsDragging || State == null || !State.CanSort || index < 0 || index >= State.Capacity || State.Slots[index].Empty) return false;
            DragSource = index; dragSnapshot = State.Slots[index]; dragRevision = State.ReorderVersion; dragCapacity = State.Capacity;
            ClearEditFeedback(); ClearSortFeedback(); return true;
        }
        public void CancelDrag() { DragSource = -1; dragSnapshot = default; }
        public bool Drop(int target)
        {
            int source = DragSource; bool valid = DragValid; CancelDrag();
            if (!valid) { EditFeedback = "物品已变化，请重新拖拽"; return false; }
            return FinishEdit(State.MoveSlot(source, target));
        }
        public bool Split(int source) => !IsDragging && State != null && FinishEdit(State.SplitSlot(source));
        private bool FinishEdit(InventoryEditResult result)
        {
            if (result == InventoryEditResult.Success)
            { EditFeedback = "背包已调整"; EditSavePending = false; SortFeedback = null; return true; }
            EditFeedback = result == InventoryEditResult.NoEmptySlot ? "没有空格，无法拆分" : result == InventoryEditResult.CannotSplit ? "至少两个物品才能拆分"
                : result == InventoryEditResult.StackFull ? "目标堆叠已满" : result == InventoryEditResult.SameSlot ? "物品留在原格"
                : result == InventoryEditResult.EmptySource ? "空格没有可操作的物品" : "暂时无法调整背包";
            if (EditSavePending) EditFeedback += " · 保存失败，请重试保存进度";
            return false;
        }
        public void SetEditSaveFailed() { EditSavePending = true; EditFeedback = "调整已保留，保存失败，请重试保存进度"; }
        public void SetEditSaved() { EditSavePending = false; EditFeedback = "背包调整已保存"; }
        public void ClearEditFeedback() { if (!EditSavePending) EditFeedback = null; }
        public bool Sort()
        {
            if (IsDragging || State == null || !State.CanSort) return false;
            EditFeedback = null; EditSavePending = false;
            SortFeedback = "背包已整理"; SortSavePending = false;
            return State.TrySort();
        }
        public void SetSortSaveFailed() { SortSavePending = true; SortFeedback = "整理已保留，保存失败，请重试保存进度"; }
        public void SetSortSaved() { SortSavePending = false; SortFeedback = "背包已整理并保存"; }
        public void ClearSortFeedback() { if (!SortSavePending) SortFeedback = null; }
        public void Initialize(GameConfig game, ItemCatalog catalog, InventoryUpgradeConfig upgrades = null, SavedInventoryUpgrade saved = null)
        {
            Catalog = catalog;
            UpgradeState = new InventoryUpgradeState(upgrades?.Definition() ?? new InventoryUpgradeDefinition(300, 800), saved?.BaseSlots ?? game.InventorySlots);
            if (saved != null) UpgradeState.Restore(saved);
            State = new InventoryState(UpgradeState.Capacity, catalog.Definitions()); UpgradeFeedback = SortFeedback = null; SortSavePending = false;
            EditFeedback = null; EditSavePending = false; CancelDrag();
            if (game.InitialSeeds > 0 && !State.TryAdd(game.SeedItemId, game.InitialSeeds))
                throw new InvalidOperationException("Initial seeds exceed inventory capacity");
        }
        public bool TryAdd(string id, int quantity) => State != null && State.TryAdd(id, quantity);
        public bool TryRemove(string id, int quantity) => State != null && State.TryRemove(id, quantity);
        public long GetCount(string id) => State?.GetCount(id) ?? 0;
        public bool Upgrade(EconomyManager economy)
        { var result = UpgradeState.Upgrade(State, economy.State); UpgradeFeedback = result == InventoryUpgradeResult.Success ? "背包已扩容至 " + State.Capacity + " 格" : ExplainUpgrade(result); return result == InventoryUpgradeResult.Success; }
        public static string ExplainUpgrade(InventoryUpgradeResult result) => result == InventoryUpgradeResult.InsufficientGold ? "金币不足，扩容需要全部费用" : result == InventoryUpgradeResult.MaximumLevel ? "已完成全部背包升级" : result == InventoryUpgradeResult.Success ? "可以升级" : "正在处理，请稍候";
        public void SetUpgradeSaveFailed() => UpgradeFeedback = "扩容已保留，保存失败，请重试保存进度";
        public void SetUpgradeSaved() => UpgradeFeedback = "背包扩容进度已保存";
        public void Clear() { State = null; Catalog = null; UpgradeState = null; UpgradeFeedback = SortFeedback = EditFeedback = null; SortSavePending = EditSavePending = false; CancelDrag(); }
    }
}
