using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public class PlacementManager : MonoBehaviour
    {
        public PlacementState State { get; private set; }
        public CraftingCatalog Catalog { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public IReadOnlyDictionary<int, PlacedDecoration> Objects => objects;
        public string LastError { get; protected set; }
        public string Feedback { get; private set; }
        public string ItemId { get; private set; }
        public event System.Action<SavedPlacedObject> Placed;
        public event System.Action<int> Recovered;
        public bool Active => previewHandle.IsValid();
        public bool Ready => Active && preview != null;
        public bool HasCandidate { get; private set; }
        public int GridX { get; private set; }
        public int GridZ { get; private set; }
        public int QuarterTurns { get; private set; }
        public PlacementResult Result { get; private set; } = PlacementResult.InvalidPosition;
        protected Transform world;
        public bool Indoor => State?.Indoor ?? false;
        private PlayerManager players;
        private float reach;
        private int generation;
        private readonly Dictionary<int, PlacedDecoration> objects = new Dictionary<int, PlacedDecoration>();
        private readonly Dictionary<int, AsyncOperationHandle<GameObject>> handles = new Dictionary<int, AsyncOperationHandle<GameObject>>();
        private readonly List<AsyncOperationHandle<GameObject>> assets = new List<AsyncOperationHandle<GameObject>>();
        private AsyncOperationHandle<GameObject> previewHandle;
        private GameObject preview;
        private Renderer[] renderers;
        private ShadowCastingMode[] shadows;
        private MaterialPropertyBlock tint;
        protected virtual void Awake() => tint = new MaterialPropertyBlock();
        public void Initialize(CraftingCatalog catalog, InventoryManager inventory, GameConfig game, PlayerManager players, Transform world,
            IEnumerable<NPCScheduleDefinition> routes, SavedPlacedObject[] saved = null, bool indoor = false, int houseLevel = 0)
        {
            Clear(); Catalog = catalog; Inventory = inventory; this.players = players; this.world = world; reach = game.InteractionReach;
            State = new PlacementState(catalog.Placeables(indoor), game.FarmSize, routes, indoor, houseLevel);
            if (saved != null) State.Restore(saved);
        }
        public IEnumerator LoadAssets()
        {
            LastError = null; int current = generation;
            foreach (var recipe in Catalog.PlacementEntries.Where(r => r.IsPlaceable && r.IsFurniture == Indoor))
            {
                var h = Addressables.LoadAssetAsync<GameObject>(recipe.PrefabAddress); assets.Add(h); yield return h;
                if (current != generation) yield break;
                if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null || h.Result.GetComponent<PlacedDecoration>() == null
                    || h.Result.GetComponent<BoxCollider>() == null || h.Result.GetComponentsInChildren<Renderer>(true).Length == 0)
                { LastError = "Decoration prefab requires marker, box collider and renderers: " + recipe.PrefabAddress; yield break; }
                var c = h.Result.GetComponent<BoxCollider>();
                if (GameManager.Instance?.Configuration?.Orchards?.Find(recipe.OutputItemId) != null && (h.Result.GetComponent<OrchardActor>() == null || !h.Result.GetComponent<OrchardActor>().IsValid))
                { LastError = "果树预制体缺少成长和果实节点: " + recipe.PrefabAddress; yield break; }
                if (c.isTrigger || !c.enabled || c.size.x <= 0 || c.size.z <= 0 || c.size.y <= 0 || c.size.y > 1.8f
                    || c.size.x > .95f || c.size.z > .95f || Mathf.Abs(c.center.x) > .02f || Mathf.Abs(c.center.z) > .02f
                    || c.center.y - c.size.y * .5f < -.01f || c.center.y + c.size.y * .5f > 1.8f
                    || h.Result.transform.localScale != Vector3.one || h.Result.transform.localRotation != Quaternion.identity
                    || h.Result.GetComponentsInChildren<Collider>(true).Length != 1)
                { LastError = "Decoration collider must fit a 1m cell: " + recipe.PrefabAddress; yield break; }
            }
            if (world != null) yield return RestoreObjects();
        }
        protected IEnumerator RestoreObjects()
        {
            LastError = null; int current = generation;
            ClearObjects();
            foreach (var record in State.Capture())
            {
                var h = Addressables.InstantiateAsync(Catalog.FindItem(record.ItemId).PrefabAddress, new Vector3(0, -100, 0), Quaternion.identity, world);
                handles.Add(record.Id, h); yield return h;
                if (current != generation) yield break;
                if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null) { LastError = "Saved decoration AA load failed"; yield break; }
                var actor = h.Result.GetComponent<PlacedDecoration>(); h.Result.SetActive(false);
                actor.Initialize(record.Id); SetTransform(h.Result, record.GridX, record.GridZ, record.QuarterTurns); Present(h.Result); objects.Add(record.Id, actor);
                if (!PhysicalClear(record.GridX, record.GridZ, true) || (Indoor && Reserved(record.GridX, record.GridZ)))
                { LastError = "已保存的" + Inventory.Catalog.Find(record.ItemId).DisplayName + "在位置 (" + record.GridX + ", " + record.GridZ + ") 与设施冲突"; yield break; }
                h.Result.SetActive(true);
            }
        }
        public IEnumerator Begin(string id)
        {
            Cancel(); LastError = null; Feedback = null; var config = Catalog?.FindItem(id);
            if (config == null || !config.IsPlaceable || config.IsFurniture != Indoor || world == null || !AreaAvailable || Inventory.State.GetCount(id) < 1) { LastError = "No placeable item in this area"; yield break; }
            ItemId = id; int current = generation;
            previewHandle = Addressables.InstantiateAsync(config.PrefabAddress, new Vector3(0, -100, 0), Quaternion.identity, world);
            yield return previewHandle;
            if (current != generation) yield break;
            if (previewHandle.Status != AsyncOperationStatus.Succeeded || previewHandle.Result == null) { LastError = "Placement preview AA load failed"; yield break; }
            preview = previewHandle.Result;
            Present(preview);
            foreach (var collider in preview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var light in preview.GetComponentsInChildren<Light>(true)) light.enabled = false;
            renderers = preview.GetComponentsInChildren<Renderer>(true); shadows = new ShadowCastingMode[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) { shadows[i] = renderers[i].shadowCastingMode; renderers[i].shadowCastingMode = ShadowCastingMode.Off; }
            preview.SetActive(false);
        }
        public void UpdatePreview(Ray ray, bool overUI)
        {
            if (!Ready) return;
            if (overUI) { if (HasCandidate) SetCandidate(GridX, GridZ); return; }
            var plane = new Plane(world.up, world.position);
            if (!plane.Raycast(ray, out float distance) || distance > 100) { HasCandidate = false; preview.SetActive(false); Result = PlacementResult.InvalidPosition; return; }
            var point = world.InverseTransformPoint(ray.GetPoint(distance));
            SetCandidate(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z));
        }
        public void SetCandidate(int x, int z)
        {
            if (!Ready) return;
            bool changed = !HasCandidate || GridX != x || GridZ != z;
            var previous = Result;
            HasCandidate = true; GridX = x; GridZ = z;
            SetTransform(preview, x, z, QuarterTurns); preview.SetActive(true);
            Result = CheckCandidate();
            if (changed || previous != Result) Feedback = null;
            tint.SetColor("_Color", Result == PlacementResult.Success ? new Color(.45f, .9f, .48f) : new Color(1, .28f, .25f));
            foreach (var renderer in renderers) renderer.SetPropertyBlock(tint);
        }
        public void Rotate() { if (!Ready) return; QuarterTurns = (QuarterTurns + 1) % 4; if (HasCandidate) SetCandidate(GridX, GridZ); }
        public PlacementResult CheckCandidate()
        {
            if (!Ready || !HasCandidate || !AreaAvailable) return PlacementResult.InvalidPosition;
            var result = State.CheckPlace(ItemId, GridX, GridZ, QuarterTurns, Inventory.State);
            if (result != PlacementResult.Success) return result;
            if (!Indoor && (GameManager.Instance?.Animals?.ReservesTile(GridX, GridZ) ?? false)) return PlacementResult.Blocked;
            if (Reserved(GridX, GridZ)) return PlacementResult.RouteReserved;
            var point = world.TransformPoint(new Vector3(GridX, 0, GridZ));
            if (Vector3.ProjectOnPlane(point - players.Player.transform.position, Vector3.up).sqrMagnitude > reach * reach) return PlacementResult.TooFar;
            return PhysicalClear(GridX, GridZ, false) ? PlacementResult.Success : PlacementResult.Blocked;
        }
        protected virtual bool AreaAvailable => !(GameManager.Instance?.Housing?.Inside ?? false);
        protected virtual bool Reserved(int x, int z) => (GameManager.Instance?.Housing?.ReservesTile(x, z) ?? false) || (GameManager.Instance?.Terrain?.ReservesTile(x,z) ?? false);
        protected virtual void Present(GameObject go) { }
        protected bool PhysicalClear(int x, int z, bool restoring)
        {
            Physics.SyncTransforms(); var point = world.TransformPoint(new Vector3(x, .9f, z));
            foreach (var c in Physics.OverlapBox(point, new Vector3(.46f, .89f, .46f), world.rotation, ~0, QueryTriggerInteraction.Collide))
            {
                if (preview != null && c.transform.IsChildOf(preview.transform)) continue;
                if (c.isTrigger) { if (c.GetComponentInParent<FishingSpot>() != null || (Indoor && c.GetComponentInParent<HomePortal>() != null)) return false; continue; }
                if (restoring && (c is CharacterController || c.GetComponentInParent<NPCActor>() != null || c.GetComponentInParent<PlacedDecoration>() != null)) continue;
                if (c.bounds.max.y <= world.position.y + .05f) continue;
                return false;
            }
            return true;
        }
        public bool Confirm()
        {
            Result = CheckCandidate(); if (Result != PlacementResult.Success) { Feedback = Describe(Result); return false; }
            var result = State.Place(ItemId, GridX, GridZ, QuarterTurns, Inventory.State, out var record, false);
            if (result != PlacementResult.Success) { Feedback = Explain(result); return false; }
            var actor = preview.GetComponent<PlacedDecoration>(); actor.Initialize(record.Id);
            foreach (var c in preview.GetComponentsInChildren<Collider>(true)) c.enabled = true;
            foreach (var light in preview.GetComponentsInChildren<Light>(true)) light.enabled = true;
            for (int i = 0; i < renderers.Length; i++) { renderers[i].SetPropertyBlock(null); renderers[i].shadowCastingMode = shadows[i]; }
            objects.Add(record.Id, actor); handles.Add(record.Id, previewHandle); previewHandle = default; preview = null;
            HasCandidate = false; ItemId = null; Feedback = "摆放完成 · 右键已摆放物品可回收"; Placed?.Invoke(record); Inventory.State.NotifyChanged(); return true;
        }
        private void SetTransform(GameObject go, int x, int z, int turns)
        { go.transform.localPosition = new Vector3(x, Indoor ? 0 : (GameManager.Instance?.Terrain?.GroundHeight(x,z) ?? 0), z); go.transform.localRotation = Quaternion.Euler(0, turns * 90, 0); }
        public bool Owns(PlacedDecoration actor) => actor != null && objects.TryGetValue(actor.Id, out var own) && own == actor;
        public bool InRange(PlacedDecoration actor) => AreaAvailable && Owns(actor) && Vector3.ProjectOnPlane(actor.transform.position - players.Player.transform.position, Vector3.up).sqrMagnitude <= reach * reach;
        public bool Recover(PlacedDecoration actor)
        {
            if (!InRange(actor)) { Feedback = "距离太远，请靠近"; return false; }
            var result = State.Recover(actor.Id, Inventory.State, false);
            if (result != PlacementResult.Success) { Feedback = Explain(result); return false; }
            int id = actor.Id; actor.gameObject.SetActive(false); ReleaseInstance(handles[id]); handles.Remove(id); objects.Remove(id);
            Recovered?.Invoke(id); Inventory.State.NotifyChanged(); Feedback = "已回收至背包"; return true;
        }
        public static string Explain(PlacementResult result) => result == PlacementResult.Success ? "位置可用 · 点击确认摆放"
            : result == PlacementResult.FarmReserved ? "这里是农田，不能摆放" : result == PlacementResult.RouteReserved ? "请留出林舟的行走通道"
            : result == PlacementResult.Occupied ? "该位置已有装饰" : result == PlacementResult.MissingItem ? "背包中没有此物品"
            : result == PlacementResult.BagFull ? "背包已满，暂时不能回收" : result == PlacementResult.LimitReached ? "已达到 128 件摆放上限"
            : result == PlacementResult.Blocked ? "设施或角色挡住了位置" : result == PlacementResult.TooFar ? "距离太远，请靠近" : "请在岛内选择位置";
        public string Describe(PlacementResult result) => Indoor && result == PlacementResult.RouteReserved ? "请留出床边、出生点和出口通道"
            : Indoor && result == PlacementResult.InvalidPosition ? "请在小屋地板内选择位置"
            : !Indoor && result == PlacementResult.RouteReserved && (GameManager.Instance?.Housing?.ReservesTile(GridX, GridZ) ?? false) ? "请留出小屋门前通道"
            : !Indoor && result == PlacementResult.RouteReserved && IslandLayout.PierReserved(GridX,GridZ) ? "请留出码头通道" : Explain(result);
        public void SetSaveFailed() => Feedback = "操作已完成，保存失败，请重试保存进度";
        public void SetSaved() => Feedback = "建设进度已保存";
        public void Cancel()
        {
            generation++; if (previewHandle.IsValid()) ReleaseInstance(previewHandle); previewHandle = default; preview = null;
            ItemId = null; HasCandidate = false; QuarterTurns = 0; renderers = null; shadows = null; Result = PlacementResult.InvalidPosition;
        }
        protected void ClearObjects()
        { foreach (var h in handles.Values) ReleaseInstance(h); handles.Clear(); objects.Clear(); }
        public virtual void Clear()
        {
            Cancel(); ClearObjects();
            foreach (var h in assets) if (h.IsValid()) { if (h.IsDone) Addressables.Release(h); else h.Completed += value => { if (value.IsValid()) Addressables.Release(value); }; }
            assets.Clear(); State = null; Catalog = null; Inventory = null; world = null; players = null; LastError = null; Feedback = null;
        }
        private static void ReleaseInstance(AsyncOperationHandle<GameObject> h)
        {
            if (!h.IsValid()) return;
            if (!h.IsDone) { h.Completed += ReleaseInstance; return; }
            if (h.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(h); else Addressables.Release(h);
        }
        protected virtual void OnDestroy() => Clear();
    }
}
