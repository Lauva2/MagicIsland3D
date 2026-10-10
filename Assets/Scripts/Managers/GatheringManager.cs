using System.Collections;
using System.Collections.Generic;
using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Gathering;
using MagicIsland.Interaction;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class GatheringManager : MonoBehaviour
    {
        public GatheringState State { get; private set; }
        public GatheringCatalog Catalog { get; private set; }
        public string LastError { get; private set; }
        public IReadOnlyList<GatheringNode> Nodes => nodes.AsReadOnly();
        private readonly List<GatheringNode> nodes = new List<GatheringNode>();
        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private readonly List<AsyncOperationHandle<GameObject>> handles = new List<AsyncOperationHandle<GameObject>>();
        private InventoryManager inventory;
        private StaminaManager stamina;
        private int generation;
        private ToolUpgradeManager upgrades;
        private int Damage(FarmTool tool) => upgrades?.State?.Damage(tool) ?? 1;
        private int Clicks(int index) => (State.HitsRemaining(index) + Damage(State.Definitions[index].Tool) - 1) / Damage(State.Definitions[index].Tool);
        public void Initialize(GatheringCatalog catalog, int day, InventoryManager inventory, StaminaManager stamina, SavedGatheringNode[] saved = null, ToolUpgradeManager upgrades = null)
        {
            Clear(); Catalog = catalog; this.inventory = inventory; this.stamina = stamina; this.upgrades = upgrades;
            State = new GatheringState(catalog.Definitions(), day); if (saved != null) State.Restore(saved);
        }
        public IEnumerator LoadNodes(Transform world)
        {
            LastError = null; int current = generation;
            foreach (var type in Catalog.Types)
            {
                if (prefabs.ContainsKey(type.PrefabAddress)) continue;
                var handle = Addressables.LoadAssetAsync<GameObject>(type.PrefabAddress); handles.Add(handle); yield return handle;
                if (current != generation) yield break;
                var node = handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null ? handle.Result.GetComponent<GatheringNode>() : null;
                if (node == null || node.WholeVisual == null || node.DepletedVisual == null
                    || node.WholeVisual.GetComponentsInChildren<Collider>().Length == 0 || node.DepletedVisual.GetComponentsInChildren<Collider>(true).Length == 0)
                { LastError = "Gathering prefab needs node marker and whole/depleted colliders: " + type.PrefabAddress; yield break; }
                prefabs.Add(type.PrefabAddress, handle.Result);
            }
            for (int i = 0; i < Catalog.Nodes.Count; i++)
            {
                var config = Catalog.Nodes[i]; var type = Catalog.Find(config.TypeId);
                var node = Instantiate(prefabs[type.PrefabAddress], world).GetComponent<GatheringNode>();
                node.name = config.Id; node.transform.localPosition = config.Position; node.Initialize(i); nodes.Add(node);
            }
            Refresh();
        }
        public bool InRange(int index, Vector3 position, float reach) => index >= 0 && index < nodes.Count
            && Vector3.ProjectOnPlane(nodes[index].transform.position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public bool Owns(GatheringNode node) => node != null && node.Index >= 0 && node.Index < nodes.Count && nodes[node.Index] == node;
        public string Describe(int index, FarmTool tool)
        {
            var type = Catalog.Find(Catalog.Nodes[index].TypeId);
            if (State.IsDepleted(index)) return type.DisplayName + "已采完 · " + (State.RespawnDay(index) - State.Day) + " 天后恢复";
            GatherResult check = State.Check(index, tool, inventory.State, stamina.State);
            return check == GatherResult.Success ? type.DisplayName + " · 还需 " + Clicks(index) + " 次" : Explain(index, check);
        }
        public string Explain(int index, GatherResult result)
        {
            if (result == GatherResult.WrongTool) return State.Definitions[index].Tool == FarmTool.Axe ? "请使用斧头砍树（4）" : "请使用镐子采石（5）";
            if (result == GatherResult.Depleted) return "资源已采完，请等待恢复";
            if (result == GatherResult.BagFull) return "背包已满，先腾出材料空间";
            if (result == GatherResult.InsufficientStamina) return "体力不足，请到床边休息";
            return result == GatherResult.Busy ? "正在采集，请稍候" : "无法采集这个目标";
        }
        public bool Hit(int index, FarmTool tool, out string message)
        {
            GatherResult result = State.Hit(index, tool, inventory.State, stamina.State, Damage(tool));
            if (result != GatherResult.Success) { message = Explain(index, result); return false; }
            Refresh(); message = State.IsDepleted(index) ? "采集完成" : "操作完成，还需 " + Clicks(index) + " 次"; return true;
        }
        public void NextDay(int day) { State.NextDay(day); Refresh(); }
        private void Refresh() { for (int i = 0; i < nodes.Count; i++) nodes[i].Refresh(State.IsDepleted(i)); }
        public void Clear()
        {
            generation++;
            foreach (var node in nodes) if (node != null) Destroy(node.gameObject); nodes.Clear();
            foreach (var handle in handles) if (handle.IsValid())
            {
                if (handle.IsDone) Addressables.Release(handle);
                else handle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            handles.Clear(); prefabs.Clear(); State = null; Catalog = null; inventory = null; stamina = null; upgrades = null;
        }
        private void OnDestroy() => Clear();
    }
}
