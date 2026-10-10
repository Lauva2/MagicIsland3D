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
            var result = State.Transfer(deposit, index, quantity);
            Feedback = result == StorageResult.Success ? (deposit ? "已存入 " : "已取出 ") + quantity + " 个物品" : Explain(result);
            return result == StorageResult.Success;
        }
        public static string Explain(StorageResult result) => result == StorageResult.DestinationFull ? "目标空间不足，未转移任何物品"
            : result == StorageResult.InvalidQuantity ? "数量无效，请重新选择" : result == StorageResult.Busy ? "正在存取，请稍候" : "请先选择一个有物品的格子";
        public void SetSaveFailed() => Feedback = "物品已转移，保存失败，请点击保存进度重试";
        public void SetSaved() => Feedback = "储物进度已保存";
        public void Clear()
        {
            generation++; if (handle.IsValid()) { if (handle.IsDone) Release(handle); else handle.Completed += Release; }
            handle = default; State = null; Chest = null; Config = null; Inventory = null; LastError = null; Feedback = null;
        }
        private static void Release(AsyncOperationHandle<GameObject> value)
        { if (!value.IsValid()) return; if (value.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(value); else Addressables.Release(value); }
        private void OnDestroy() => Clear();
    }
}
