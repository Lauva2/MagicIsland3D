using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Tools;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class ToolUpgradeManager : MonoBehaviour
    {
        public ToolUpgradeState State { get; private set; }
        public ToolUpgradeCatalog Catalog { get; private set; }
        public ToolWorkbench Workbench { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public EconomyManager Economy { get; private set; }
        public string LastError { get; private set; }
        public string Feedback { get; private set; }
        private AsyncOperationHandle<GameObject> handle;
        private int generation;
        public void Initialize(ToolUpgradeCatalog catalog, InventoryManager inventory, EconomyManager economy, SavedToolUpgrade[] saved = null)
        {
            Clear(); Catalog = catalog; Inventory = inventory; Economy = economy; State = new ToolUpgradeState(catalog.Definitions());
            if (saved != null) State.Restore(saved);
        }
        public IEnumerator LoadWorkbench(Transform world, Vector3 hallPosition)
        {
            LastError = null; int current = generation;
            handle = Addressables.InstantiateAsync(Catalog.WorkbenchAddress, world); yield return handle;
            if (current != generation) yield break;
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null || (Workbench = handle.Result.GetComponent<ToolWorkbench>()) == null
                || Workbench.GetComponentsInChildren<Collider>().Length == 0)
            { LastError = "Tool workbench needs marker and colliders: " + Catalog.WorkbenchAddress; yield break; }
            Workbench.transform.localPosition = hallPosition + new Vector3(0.2f, 0, -4.4f);
        }
        public bool InRange(Vector3 position, float reach) => Workbench != null && Vector3.ProjectOnPlane(Workbench.transform.position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public UpgradeResult Check(FarmTool tool) => State.Check(tool, Inventory.State, Economy.State);
        public bool Upgrade(FarmTool tool)
        {
            var result = State.Upgrade(tool, Inventory.State, Economy.State); Feedback = Explain(result); return result == UpgradeResult.Success;
        }
        public static string Explain(UpgradeResult result) => result == UpgradeResult.Success ? "升级完成，每次采集抵两次操作"
            : result == UpgradeResult.AlreadyUpgraded ? "已达到当前最高等级" : result == UpgradeResult.InsufficientMaterials ? "木材或石料不足"
            : result == UpgradeResult.InsufficientGold ? "金币不足" : result == UpgradeResult.Busy ? "正在升级，请稍候" : "此工具暂不支持升级";
        public void SetSaveFailed() => Feedback = "工具已升级，保存失败，请重试保存";
        public void SetSaved() => Feedback = "工具升级进度已保存";
        public void Clear()
        {
            generation++; if (handle.IsValid()) { if (handle.IsDone) Release(handle); else handle.Completed += Release; }
            handle = default; State = null; Catalog = null; Workbench = null; Inventory = null; Economy = null; Feedback = null; LastError = null;
        }
        private static void Release(AsyncOperationHandle<GameObject> value)
        { if (!value.IsValid()) return; if (value.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(value); else Addressables.Release(value); }
        private void OnDestroy() => Clear();
    }
}
