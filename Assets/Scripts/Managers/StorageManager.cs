using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class StorageManager : MonoBehaviour
    {
        public StorageState State { get; private set; }
        public StorageChest Chest { get; private set; }
        public StorageConfig Config { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public string LastError { get; private set; }
        public string Feedback { get; private set; }
        public bool SavePending { get; private set; }
        public int DragSource { get; private set; } = -1;
        public bool DragDeposit { get; private set; }
        public bool IsDragging => DragSource >= 0;
        private InventorySlot dragSnapshot;
        private uint bagRevision, chestRevision;
        private int bagCapacity, chestCapacity;
        public bool DragValid => IsDragging && State != null && Inventory.State.Capacity == bagCapacity && State.Contents.Capacity == chestCapacity
            && Inventory.State.ReorderVersion == bagRevision && State.Contents.ReorderVersion == chestRevision
            && (DragDeposit ? Inventory.State : State.Contents).Slots[DragSource].ItemId == dragSnapshot.ItemId
            && (DragDeposit ? Inventory.State : State.Contents).Slots[DragSource].Quantity == dragSnapshot.Quantity;
        public bool BeginDrag(bool deposit, int index)
        {
            if (IsDragging || State == null || !Inventory.State.CanSort || !State.Contents.CanSort) return false;
            var source = deposit ? Inventory.State : State.Contents;
            if (index < 0 || index >= source.Capacity || source.Slots[index].Empty) return false;
            DragDeposit = deposit; DragSource = index; dragSnapshot = source.Slots[index];
            bagRevision = Inventory.State.ReorderVersion; chestRevision = State.Contents.ReorderVersion;
            bagCapacity = Inventory.State.Capacity; chestCapacity = State.Contents.Capacity; return true;
        }
        public void CancelDrag() { DragSource = -1; dragSnapshot = default; }
        public bool Drop(bool targetBag, int index)
        {
            bool valid = DragValid && targetBag != DragDeposit, deposit = DragDeposit;
            int source = DragSource, requested = dragSnapshot.Quantity; CancelDrag();
            if (!valid) return false;
            return FinishAvailable(State.TransferAvailable(deposit, source, out int moved, index), deposit, moved, requested);
        }
        public bool QuickTransfer(bool deposit, int index)
        {
            if (IsDragging || State == null) return false;
            var source = deposit ? Inventory.State : State.Contents;
            int requested = index >= 0 && index < source.Capacity ? source.Slots[index].Quantity : 0;
            return FinishAvailable(State.TransferAvailable(deposit, index, out int moved), deposit, moved, requested);
        }
        private bool FinishAvailable(StorageResult result, bool deposit, int moved, int requested)
        {
            if (result != StorageResult.Success) { Feedback = Explain(result); if (SavePending) Feedback += " · 保存失败，请重试"; return false; }
            SavePending = false; Feedback = (deposit ? "已存入 " : "已取出 ") + moved + " 个物品" + (moved < requested ? " · 剩余物品保留在原格" : ""); return true;
        }
        public bool DepositMatching()
        {
            if (IsDragging || State == null) return false;
            var result = State.DepositMatching(out long moved);
            if (result != StorageResult.Success) { Feedback = Explain(result); if (SavePending) Feedback += " · 保存失败，请重试"; return false; }
            SavePending = false; Feedback = "已存入同类物品 " + moved + " 个 · 未转移的物品保留在背包"; return true;
        }
        private AsyncOperationHandle<GameObject> handle;
        private int generation;
        public void Initialize(StorageConfig config, InventoryManager inventory, InventorySlot[] saved = null)
        {
            Clear(); Config = config; Inventory = inventory; State = new StorageState(config.Slots, inventory.Catalog.Definitions(), inventory.State);
            if (saved != null) State.Restore(saved);
        }
        public IEnumerator LoadChest(Transform world, int farmSize)
        {
            LastError = null; int current = generation;
            handle = Addressables.InstantiateAsync(Config.ChestAddress, world); yield return handle;
            if (current != generation) yield break;
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null || (Chest = handle.Result.GetComponent<StorageChest>()) == null
                || Chest.GetComponentsInChildren<Collider>().Length == 0)
            { LastError = "Storage chest needs marker and colliders: " + Config.ChestAddress; yield break; }
            // Front left of the farm, clear of the bed and all supported farm sizes.
            Chest.transform.localPosition = new Vector3(-1.5f, 0, -farmSize * .6f - 2.2f);
        }
        public bool InRange(Vector3 position, float reach) => Chest != null && Vector3.ProjectOnPlane(Chest.transform.position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public bool Transfer(bool deposit, int index, int quantity)
        {
            if (IsDragging) return false;
            var result = State.Transfer(deposit, index, quantity);
            if (result == StorageResult.Success) SavePending = false;
            Feedback = result == StorageResult.Success ? (deposit ? "已存入 " : "已取出 ") + quantity + " 个物品" : Explain(result);
            return result == StorageResult.Success;
        }
        public static string Explain(StorageResult result) => result == StorageResult.DestinationFull ? "目标空间不足，未转移任何物品"
            : result == StorageResult.TargetOccupied ? "目标格已有不同物品，请选择空格或同类物品"
            : result == StorageResult.NoMatchingItems ? "箱内没有与背包相同的物品"
            : result == StorageResult.InvalidQuantity ? "数量无效，请重新选择" : result == StorageResult.Busy ? "正在存取，请稍候" : "请先选择一个有物品的格子";
        public void SetSaveFailed() { SavePending = true; Feedback = "物品已转移，保存失败，请点击保存进度重试"; }
        public void SetSaved() { SavePending = false; Feedback = "储物进度已保存"; }
        public void Clear()
        {
            generation++; if (handle.IsValid()) { if (handle.IsDone) Release(handle); else handle.Completed += Release; }
            handle = default; State = null; Chest = null; Config = null; Inventory = null; LastError = null; Feedback = null; SavePending = false; CancelDrag();
        }
        private static void Release(AsyncOperationHandle<GameObject> value)
        { if (!value.IsValid()) return; if (value.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(value); else Addressables.Release(value); }
        private void OnDestroy() => Clear();
    }
}
