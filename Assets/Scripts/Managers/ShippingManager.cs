using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
using MagicIsland.Interaction;
using MagicIsland.Shipping;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class ShippingManager : MonoBehaviour
    {
        public ShippingState State { get; private set; }
        public ShippingBox Box { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public ShippingConfig Config { get; private set; }
        public string LastError { get; private set; }
        public string Feedback { get; private set; }
        public bool SavePending { get; private set; }
        private AsyncOperationHandle<GameObject> handle;
        private int generation;
        public void Initialize(ShippingConfig config, InventoryManager inventory, AnimalConfig animals, int day, SavedShipping saved = null)
        {
            Clear(); Config = config; Inventory = inventory; State = new ShippingState(inventory.State, inventory.Catalog.Definitions(), ShippingConfig.Prices(inventory.Catalog, animals));
            if (saved != null) State.Restore(saved, day);
        }
        public IEnumerator LoadBox(Transform world, Vector3 housePosition)
        {
            int current = generation; handle = Addressables.InstantiateAsync(Config.BoxAddress, world); yield return handle;
            if (current != generation) yield break;
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null || (Box = handle.Result.GetComponent<ShippingBox>()) == null || Box.GetComponentsInChildren<Collider>().Length == 0)
            { LastError = "出货箱模型缺少标记或碰撞配置"; yield break; }
            Box.transform.localPosition = housePosition + Config.HouseOffset;
        }
        public bool InRange(Vector3 position, float reach) => Box != null && Vector3.ProjectOnPlane(Box.transform.position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public bool Transfer(bool deposit, int index, int quantity)
        {
            var result = State.Transfer(deposit, index, quantity);
            Feedback = result == StorageResult.Success ? (deposit ? "已放入出货箱 " : "已取回 ") + quantity + " 个物品"
                : result == StorageResult.NoMatchingItems ? "出货箱只收购有售价的农作物、鱼和鸡蛋" : StorageManager.Explain(result);
            if (SavePending) Feedback += " · 保存失败，请重试";
            return result == StorageResult.Success;
        }
        public void SetSaveFailed() { SavePending = true; Feedback = "物品已保留，保存失败，请点击保存进度重试"; }
        public void SetSaved() { SavePending = false; Feedback = "出货进度已保存"; }
        public void Clear()
        {
            generation++; if (handle.IsValid()) { if (handle.IsDone) Release(handle); else handle.Completed += Release; }
            handle = default; State = null; Box = null; Inventory = null; Config = null; LastError = null; Feedback = null; SavePending = false;
        }
        private static void Release(AsyncOperationHandle<GameObject> h) { if (!h.IsValid()) return; if (h.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(h); else Addressables.Release(h); }
        private void OnDestroy() => Clear();
    }
}
