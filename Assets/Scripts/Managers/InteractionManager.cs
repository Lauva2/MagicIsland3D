using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using UnityEngine;
using System;
using System.Collections;
using MagicIsland.Gathering;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagicIsland.Managers
{
    public sealed class InteractionManager : MonoBehaviour
    {
        public InteractionController Controller { get; private set; }
        public InteractionTarget Current { get; private set; } = InteractionTarget.None;
        public event Action Changed;
        public string LastError { get; private set; }
        public InteractionHighlight Highlight { get; private set; }
        public bool OwnsHighlight => highlightHandle.IsValid();
        private AsyncOperationHandle<GameObject> highlightHandle;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private Ray pointerRay;
        private bool hasPointer, dispatching, notifying;
        private int generation;
        private GameManager Game => GameManager.Instance;
        private bool CanInput => Controller != null && Controller.isActiveAndEnabled && Controller.HasFocus && Game != null
            && Game.State == GameFlowState.Playing && !Game.UI.AnyPopupVisible;
        public void Initialize(InteractionController controller, GameConfig config, FarmManager farm, ToolManager tools)
        {
            Clear();
            Controller = controller;
            controller.Manager = this;
            controller.Reach = config.InteractionReach;
            controller.FarmManager = farm;
            controller.Tools = tools;
        }
        public void SelectTool(FarmTool tool) { if (Controller != null) Controller.SelectTool(tool); }
        public IEnumerator LoadHighlight(string address)
        {
            generation++; ClearTarget();
            if (highlightHandle.IsValid()) { if (highlightHandle.IsDone) Release(highlightHandle); else highlightHandle.Completed += Release; }
            highlightHandle = default; Highlight = null;
            LastError = null; int current = generation;
            highlightHandle = Addressables.InstantiateAsync(address, transform); yield return highlightHandle;
            if (current != generation) yield break;
            Highlight = highlightHandle.Status == AsyncOperationStatus.Succeeded && highlightHandle.Result != null ? highlightHandle.Result.GetComponent<InteractionHighlight>() : null;
            if (Highlight == null || Highlight.Ring == null || Highlight.Ring.positionCount < 8 || Highlight.Ring.sharedMaterial == null
                || Highlight.GetComponentsInChildren<Collider>(true).Length != 0)
            { LastError = "交互高亮 AA 预制体缺少轮廓配置"; Release(highlightHandle); highlightHandle = default; Highlight = null; yield break; }
            Highlight.Hide();
        }
        public void SetInputEnabled(bool enabled) { if (Controller != null) Controller.enabled = enabled; if (!enabled) ClearTarget(); }
        public void ClearTarget()
        {
            hasPointer = false;
            bool changed = Current != InteractionTarget.None;
            Current = InteractionTarget.None; Controller?.ApplyTarget(Current);
            if (Highlight != null) Highlight.Hide();
            if (changed) NotifyChanged();
        }
        public void Clear()
        {
            generation++; ClearTarget();
            if (Controller != null) Controller.Manager = null; Controller = null;
            if (highlightHandle.IsValid()) { if (highlightHandle.IsDone) Release(highlightHandle); else highlightHandle.Completed += Release; }
            highlightHandle = default; Highlight = null;
        }
        private static void Release(AsyncOperationHandle<GameObject> operation)
        { if (!operation.IsValid()) return; if (operation.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(operation); else Addressables.Release(operation); }
        public InteractionTarget RefreshPointer(Ray ray, bool overUI)
        {
            pointerRay = ray; hasPointer = CanInput && !overUI;
            var target = hasPointer ? Resolve(ray) : InteractionTarget.None;
            if (ReferenceEquals(Current, target)) return Current;
            Current = target;
            Controller?.ApplyTarget(Current);
            if (Highlight != null) { if (Current.Visible && Current.Kind != InteractionKind.Farm) Highlight.Show(Current); else Highlight.Hide(); }
            NotifyChanged(); return Current;
        }
        private void NotifyChanged()
        {
            bool wasNotifying = notifying; notifying = true;
            try { Changed?.Invoke(); }
            finally { notifying = wasNotifying; }
        }
        private InteractionTarget Make(InteractionKind kind, Transform root, string name, string operation, string details, bool near, bool ready = true, int index = -1, string id = null)
        {
            if (Game.Tools.Busy) { ready = false; details = "正在操作，请稍候"; }
            // Reuse the immutable snapshot only after the live ray and readiness checks above.
            if (Current.Kind == kind && Current.Root == root && Current.Name == name && Current.Operation == operation
                && Current.Details == details && Current.InRange == near && Current.Ready == ready && Current.Index == index && Current.Id == id)
                return Current;
            return new InteractionTarget(kind, root, name, operation, details, near, ready, index, id);
        }
        private InteractionTarget Resolve(Ray ray)
        {
            var game = Game; Vector3 player = Controller.Player.transform.position; float reach = Controller.Reach;
            int mask = Controller.ViewCamera.cullingMask & Physics.DefaultRaycastLayers & ~(1 << 5);
            int count = Physics.RaycastNonAlloc(ray, hits, 100, mask, QueryTriggerInteraction.Collide);
            RaycastHit[] results = hits;
            if (count == hits.Length) { results = Physics.RaycastAll(ray, 100, mask, QueryTriggerInteraction.Collide); count = results.Length; }
            Collider collider = null; float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = results[i];
                if (hit.collider == null || hit.transform.IsChildOf(Controller.Player.transform) || hit.distance >= distance) continue;
                collider = hit.collider; distance = hit.distance;
            }
            float farmDistance = float.MaxValue;
            int farmIndex = !game.Housing.Inside && (Controller.ViewCamera.cullingMask & (1 << Controller.Farm.gameObject.layer)) != 0
                ? Controller.Farm.Pick(ray, out farmDistance) : -1;
            if (farmIndex < 0) farmDistance = float.MaxValue;
            if (collider != null && distance <= farmDistance)
            {
                var portal = collider.GetComponentInParent<HomePortal>();
                if (game.Housing.Owns(portal)) return Make(InteractionKind.Home, portal.transform, portal.IsExit ? "小屋出口" : game.Housing.Upgrade.Level == 1 ? "舒适小屋" : "我的小屋", portal.IsExit ? "左键返回农场" : "左键进门", "", game.Housing.InRange(portal, reach));
                var bed = collider.GetComponentInParent<SleepBed>();
                if (game.Housing.Inside && bed == game.Housing.Interior.Bed) return Make(InteractionKind.Bed, bed.transform, "小屋床铺", "左键休息到明天", "睡觉结算后保存进度", game.Time.InRange(player, reach));
                var decoration = collider.GetComponentInParent<PlacedDecoration>();
                if (!game.Housing.Inside && game.Orchard.Owns(decoration))
                {
                    var record = game.Placements.State.Find(decoration.Id); var type = game.Configuration.Orchards.Find(record.ItemId);
                    return Make(InteractionKind.Orchard, decoration.transform, type.DisplayName, "左键采收 · 右键回收", game.Orchard.Describe(decoration.Id), game.Orchard.InRange(decoration), game.Orchard.State.Check(decoration.Id, game.Inventory.State) == OrchardResult.Success, decoration.Id);
                }
                if (game.ActivePlacement.Owns(decoration))
                {
                    var placed = game.ActivePlacement.State.Find(decoration.Id); bool capacity = game.Inventory.State.CanAdd(placed.ItemId, 1) && game.Inventory.State.CanSort;
                    return Make(InteractionKind.Decoration, decoration.transform, game.Inventory.Catalog.Find(placed.ItemId).DisplayName, "右键回收", capacity ? "回收到背包" : "背包已满，暂时不能回收", game.ActivePlacement.InRange(decoration), capacity);
                }
                if (!game.Housing.Inside)
                {
                    var pickup=collider.GetComponentInParent<BeachPickup>();
                    if(game.Gathering.Owns(pickup))
                    {
                        bool ready=game.Gathering.BeachState.Check(pickup.Id,game.Inventory.State)==GatherResult.Success;
                        return Make(InteractionKind.BeachForage,pickup.transform,game.Inventory.Catalog.Find(pickup.ItemId).DisplayName,"左键拾取",ready?"拾取后可在商店出售":"背包已满，请先腾出位置",game.Gathering.BeachInRange(pickup,player,reach),ready,id:pickup.Id);
                    }
                    var spot = collider.GetComponentInParent<FishingSpot>();
                    if (spot != null && spot == game.Fishing.Spot)
                    {
                        bool rod = Controller.Tool == FarmTool.FishingRod;
                        return Make(InteractionKind.Fishing, spot.transform, "海岸钓点", "左键抛竿", rod ? "等待浮漂下沉后收竿" : "请使用鱼竿（6）", game.Fishing.InRange(player, reach), rod);
                    }
                    var coop = collider.GetComponentInParent<CoopActor>();
                    if (game.Animals.Owns(coop)) return Make(InteractionKind.Coop, coop.transform, "鸡舍", "左键管理鸡舍", "喂食、摸摸或收取鸡蛋", game.Animals.InRange(player, reach));
                    var node = collider.GetComponentInParent<GatheringNode>();
                    if (game.Gathering.Owns(node))
                    {
                        var config = game.Gathering.Catalog.Find(game.Gathering.Catalog.Nodes[node.Index].TypeId);
                        bool ready = game.Gathering.State.Check(node.Index, Controller.Tool, game.Inventory.State, game.Stamina.State) == GatherResult.Success;
                        return Make(InteractionKind.Gathering, node.transform, config.DisplayName, config.Tool == FarmTool.Axe ? "左键砍树" : "左键采石", game.Gathering.Describe(node.Index, Controller.Tool), game.Gathering.InRange(node.Index, player, reach), ready, node.Index);
                    }
                    var npc = collider.GetComponentInParent<NPCActor>();
                    if (game.NPCs.Owns(npc)) return Make(InteractionKind.NPC, npc.transform, game.NPCs.Catalog.Find(npc.Id).DisplayName, "左键交谈", game.NPCSchedules.Describe(npc.Id), game.NPCs.InRange(npc.Id, player, reach), id: npc.Id);
                    var cooking = collider.GetComponentInParent<CookingStation>();
                    if (cooking != null && cooking == game.Cooking.Station) return Make(InteractionKind.Cooking, cooking.transform, "料理台", "左键制作食物", "烹饪料理或查看食谱", game.Cooking.InRange(player, reach));
                    var shipping = collider.GetComponentInParent<ShippingBox>();
                    if (shipping != null && shipping == game.Shipping.Box) return Make(InteractionKind.Shipping, shipping.transform, "出货箱", "左键存取出货", "睡觉后统一售卖", game.Shipping.InRange(player, reach));
                    var storage = collider.GetComponentInParent<StorageChest>();
                    if (storage != null && storage == game.Storage.Chest) return Make(InteractionKind.Storage, storage.transform, "储物箱", "左键存取物品", "整理背包与储物箱", game.Storage.InRange(player, reach));
                    var bench = collider.GetComponentInParent<ToolWorkbench>();
                    if (bench != null && bench == game.ToolUpgrades.Workbench) return Make(InteractionKind.Workbench, bench.transform, "工作台", "左键制作与升级", "制作物品或升级工具", game.ToolUpgrades.InRange(player, reach));
                    var board = collider.GetComponentInParent<OrderBoard>();
                    if (board != null && board == game.Orders.Board) return Make(InteractionKind.Orders, board.transform, "办事处公告栏", "左键查看今日订单", "交付订单、扩建小屋或建造鸡舍", game.Orders.InRange(player, reach));
                    var shop = collider.GetComponentInParent<ShopCounter>();
                    if (shop != null && shop.transform == game.Shop.Counter) return Make(InteractionKind.Shop, shop.transform, "岛屿商店", "左键买卖物品", "种子、收购与背包扩容", game.Shop.InRange(player, reach));
                }
            }
            int index = farmIndex;
            if (index < 0 || collider != null && distance <= farmDistance && collider.bounds.max.y > Controller.Farm.CellCenter(index).y + .04f) return InteractionTarget.None;
            var cell = game.Farm.State.Cells[index]; var tool = Controller.Tool;
            string name = cell.Planted ? game.Inventory.Catalog.Find(game.Farm.Crops.Find(cell.CropId).HarvestItemId).DisplayName : "农田地块";
            string operation = cell.Mature && tool != FarmTool.Fertilizer && tool != FarmTool.FishingRod ? "左键收获"
                : tool == FarmTool.Hoe ? "左键耕地" : tool == FarmTool.Seeds ? "左键播种" : tool == FarmTool.WateringCan ? "左键浇水" : tool == FarmTool.Fertilizer ? "左键施肥" : "请选择农具（1 / 2 / 3 / 7）";
            if (game.Magic.State?.RainLearned == true) operation += " · R 唤雨";
            string details = tool == FarmTool.FishingRod ? "鱼竿用于海岸垂钓" : game.Farm.Describe(index, tool);
            if (!cell.Tilled && tool != FarmTool.Hoe && !cell.Planted) details = "请先使用锄头（1）耕地";
            if (tool == FarmTool.Hoe && cell.Tilled && !cell.Planted) details = "土地已耕好，可选择种子（2）播种";
            if (tool == FarmTool.WateringCan && !cell.Planted) details = "先选择种子（2）播种，再浇水";
            bool near = Vector3.ProjectOnPlane(Controller.Farm.CellCenter(index) - player, Vector3.up).sqrMagnitude <= reach * reach;
            return Make(InteractionKind.Farm, Controller.Farm.transform, name, operation, details, near, game.Farm.CanPerform(index, tool), index);
        }
        public bool TryCastRain()
        {
            if (!CanInput || !hasPointer || dispatching || notifying || Game.Tools.Busy) return false;
            dispatching = true;
            try
            {
                RefreshPointer(pointerRay, false);
                if (Current.Kind != InteractionKind.Farm) { Game.Tools.ReportFailure("请指向附近已耕作的农田"); return false; }
                return Game.Tools.TryCastRain(Game.Magic, Current.Index, out _);
            }
            finally
            {
                try { if (hasPointer) RefreshPointer(pointerRay, false); }
                finally { dispatching = false; }
            }
        }
        public bool TryInteract(bool secondary = false)
        {
            if (!CanInput || !hasPointer || dispatching || notifying || Game.Tools.Busy) return false;
            dispatching = true;
            try
            {
                // Recheck the same ray at click time; state, tools and actors can change after hover.
                RefreshPointer(pointerRay, false);
                return Dispatch(secondary);
            }
            finally
            {
                // Publish the committed state before allowing another dispatch, including event callbacks.
                try { if (hasPointer) RefreshPointer(pointerRay, false); }
                finally { dispatching = false; }
            }
        }
        private bool Dispatch(bool secondary)
        {
            if (!Current.Visible) return false;
            var target = Current;
            if ((secondary && target.Kind != InteractionKind.Decoration && target.Kind != InteractionKind.Orchard) || (!secondary && target.Kind == InteractionKind.Decoration)) return false;
            if (!target.InRange || (!target.Ready && !secondary)) { Controller.SetFeedback(target.Hint); Game.Tools.ReportFailure(target.Hint, target.Name); return false; }
            if (secondary) { Game.RecoverDecoration(target.Root.GetComponent<PlacedDecoration>()); return true; }
            switch (target.Kind)
            {
                case InteractionKind.Farm:
                    bool acted = Game.Tools.TryAct(target.Index, out string message); Controller.SetFeedback(acted ? message + "完成" : message); return acted;
                case InteractionKind.Gathering:
                    bool gathered = Game.TryGather(target.Index, out string feedback); Controller.SetFeedback(feedback); return gathered;
                case InteractionKind.BeachForage:
                    bool picked=Game.Tools.TryBeachPickup(target.Root.GetComponent<BeachPickup>(),out string pickupFeedback);Controller.SetFeedback(pickupFeedback);return picked;
                case InteractionKind.Orchard: return Game.HarvestTree(target.Root.GetComponent<PlacedDecoration>());
                case InteractionKind.Home: if (Game.Housing.Inside) Game.ExitHome(); else Game.EnterHome(); break;
                case InteractionKind.Bed: Game.OpenSleep(); break;
                case InteractionKind.Fishing: Game.OpenFishing(); break;
                case InteractionKind.Coop: Game.OpenCoop(); break;
                case InteractionKind.NPC: Game.OpenDialogue(target.Id); break;
                case InteractionKind.Cooking: Game.OpenCooking(); break;
                case InteractionKind.Shipping: Game.OpenShipping(); break;
                case InteractionKind.Storage: Game.OpenStorage(); break;
                case InteractionKind.Workbench: Game.OpenCrafting(); break;
                case InteractionKind.Orders: Game.OpenOrder(); break;
                case InteractionKind.Shop: Game.OpenShop(); break;
                default: return false;
            }
            return true;
        }
        public void SetFeedback(string message) { if (Controller != null) Controller.SetFeedback(message); }
        private void OnDisable() => ClearTarget();
        private void OnDestroy() => Clear();
    }
}
