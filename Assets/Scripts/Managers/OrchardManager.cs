using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class OrchardManager : MonoBehaviour
    {
        public OrchardState State { get; private set; }
        public OrchardCatalog Catalog { get; private set; }
        private PlacementManager placement;
        private InventoryManager inventory;
        public void Initialize(OrchardCatalog catalog, PlacementManager placement, InventoryManager inventory, int day, SavedOrchardTree[] saved)
        {
            Clear(); Catalog = catalog; this.placement = placement; this.inventory = inventory;
            State = new OrchardState(catalog.Definitions(), placement.State, day); State.Restore(saved ?? new SavedOrchardTree[0]);
            placement.Placed += OnPlanted; placement.Recovered += OnRecovered;
        }
        private void OnPlanted(SavedPlacedObject record) { State.Planted(record); Refresh(); }
        private void OnRecovered(int id) => State.Removed(id);
        public void Refresh()
        {
            foreach (var record in State.Capture())
                if (placement.Objects.TryGetValue(record.PlacementId, out var actor))
                    actor.GetComponent<OrchardActor>().Render(State.Stage(record.PlacementId), record.NextFruitDay <= State.Day);
        }
        public void NextDay(int day) { State.NextDay(day); Refresh(); }
        public bool Owns(PlacedDecoration actor) => actor != null && placement != null && placement.Owns(actor) && State.Find(actor.Id) != null;
        public bool InRange(PlacedDecoration actor) => Owns(actor) && placement.InRange(actor);
        public bool Harvest(PlacedDecoration actor, out string message)
        {
            message = "请走近果树"; if (!InRange(actor)) return false;
            var result = State.Harvest(actor.Id, inventory.State); message = Explain(result);
            if (result != OrchardResult.Success) return false;
            var definition = State.Definition(actor.Id); Refresh(); message = inventory.Catalog.Find(definition.FruitId).DisplayName + " +" + definition.Yield; return true;
        }
        public string Describe(int id)
        {
            var tree = State.Find(id); var definition = State.Definition(id);
            if (State.Stage(id) < 2) return "成长中 · " + ((long)tree.PlantedDay + definition.GrowthDays - State.Day) + " 天后成熟";
            return tree.NextFruitDay <= State.Day ? Explain(State.Check(id, inventory.State)) : "等待结果 · " + (tree.NextFruitDay - State.Day) + " 天后可采";
        }
        public static string Explain(OrchardResult result) => result == OrchardResult.Success ? "左键采收 · 右键回收会重置成长"
            : result == OrchardResult.BagFull ? "背包空间不足，请先整理" : result == OrchardResult.NotReady ? "果树还未结果" : "暂时无法采收";
        public void Clear() { if (placement != null) { placement.Placed -= OnPlanted; placement.Recovered -= OnRecovered; } placement = null; inventory = null; Catalog = null; State = null; }
        private void OnDestroy() => Clear();
    }
}
