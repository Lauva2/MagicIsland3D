using System.Collections.Generic;
using MagicIsland.Farming;
using MagicIsland.Player;
using MagicIsland.Managers;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicIsland.Interaction
{
    public sealed class InteractionController : MonoBehaviour
    {
        public Camera ViewCamera;
        public PlayerMovement Player;
        public FarmGrid Farm;
        public FarmManager FarmManager;
        public ToolManager Tools;
        public FarmTool Tool => Tools != null ? Tools.SelectedTool : FarmTool.Hoe;
        public string Hint { get; private set; } = "靠近农田，选择锄头开始耕地";
        public string Feedback { get; private set; } = "每次浇水后进入下一天，作物生长一阶段";
        public void SetFeedback(string message) => Feedback = message;
        public float Reach = 2.25f;
        public int HoverIndex { get; private set; } = -1;
        public bool HoverInRange { get; private set; }
        public int GatheringHoverIndex { get; private set; } = -1;
        public string NPCHoverId { get; private set; }
        private readonly HashSet<int> visited = new HashSet<int>();
        private bool gestureBlocked;

        public void SelectTool(FarmTool tool)
        {
            if (Tools == null || !Tools.SelectTool(tool)) return;
            // A held click must not switch tool and act again on the same plot.
            if (Input.GetMouseButton(0)) gestureBlocked = true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(FarmTool.Hoe);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(FarmTool.Seeds);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(FarmTool.WateringCan);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectTool(FarmTool.Axe);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectTool(FarmTool.Pickaxe);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SelectTool(FarmTool.FishingRod);
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(0))
            {
                visited.Clear();
                gestureBlocked = overUI;
            }
            if (!Input.GetMouseButton(0))
            {
                visited.Clear();
                gestureBlocked = false;
            }
            Ray ray = ViewCamera.ScreenPointToRay(Input.mousePosition);
            if (RefreshHomeHover(ray, overUI))
            {
                if (Input.GetMouseButtonDown(0) && !gestureBlocked) { if (GameManager.Instance.Housing.Inside) GameManager.Instance.ExitHome(); else GameManager.Instance.EnterHome(); }
                return;
            }
            if (GameManager.Instance.Housing.Inside)
            {
                if (RefreshDecorationHover(ray, overUI))
                { if (Input.GetMouseButtonDown(1)) GameManager.Instance.RecoverDecoration(DecorationHover); return; }
                if (RefreshIndoorHover(ray, overUI) && Input.GetMouseButtonDown(0) && !gestureBlocked) GameManager.Instance.OpenSleep();
                return;
            }
            if (RefreshDecorationHover(ray, overUI))
            {
                if (Input.GetMouseButtonDown(1)) GameManager.Instance.RecoverDecoration(DecorationHover);
                return;
            }
            if (RefreshFishingHover(ray, overUI))
            {
                if (Input.GetMouseButtonDown(0) && !gestureBlocked) GameManager.Instance.OpenFishing();
                return;
            }
            if (RefreshGatheringHover(ray, overUI))
            {
                if (Input.GetMouseButtonDown(0) && !gestureBlocked)
                {
                    GameManager.Instance.TryGather(GatheringHoverIndex, out string gatheringMessage); Feedback = gatheringMessage;
                }
                return;
            }
            if (RefreshNPCHover(ray, overUI))
            {
                if (Input.GetMouseButtonDown(0) && !gestureBlocked) GameManager.Instance.OpenDialogue(NPCHoverId);
                return;
            }
            if (RefreshCookingHover(ray, overUI))
            {
                if (Input.GetMouseButtonDown(0) && !gestureBlocked) GameManager.Instance.OpenCooking();
                return;
            }
            if (RefreshStorageHover(ray, overUI))
            {
                if (Input.GetMouseButtonDown(0) && !gestureBlocked) GameManager.Instance.OpenStorage();
                return;
            }
            if (RefreshWorkbenchHover(ray, overUI))
            {
                if (Input.GetMouseButtonDown(0) && !gestureBlocked) GameManager.Instance.OpenCrafting();
                return;
            }
            if (RefreshOrderBoardHover(ray, overUI))
            {
                GameManager game = GameManager.Instance;
                if (Input.GetMouseButtonDown(0) && !gestureBlocked && game.Orders.InRange(Player.transform.position, Reach)) game.OpenOrder();
                return;
            }
            int index = overUI ? -1 : Farm.Pick(ray);
            if (!overUI && Physics.Raycast(ViewCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 100f)
                && hit.collider.GetComponentInParent<ShopCounter>() != null)
            {
                ClearHover();
                GameManager game = GameManager.Instance;
                bool near = game != null && game.Shop.InRange(Player.transform.position, Reach);
                Hint = near ? "点击打开岛屿商店" : "商店距离太远，请靠近";
                if (Input.GetMouseButtonDown(0) && !gestureBlocked && near) game.OpenShop();
                // Clicking the counter must never act on the farm behind it.
                return;
            }
            if (!overUI && Physics.Raycast(ViewCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit bedHit, 100f)
                && bedHit.collider.GetComponentInParent<SleepBed>() != null)
            {
                ClearHover();
                GameManager game = GameManager.Instance;
                bool near = game != null && game.Time.InRange(Player.transform.position, Reach);
                Hint = near ? "点击床休息到明天" : "床距离太远，请靠近";
                if (Input.GetMouseButtonDown(0) && !gestureBlocked && near) game.OpenSleep();
                return;
            }
            RefreshFarmHover(ViewCamera.ScreenPointToRay(Input.mousePosition), overUI);
            bool inRange = HoverInRange;
            if (index < 0 || !Input.GetMouseButton(0) || gestureBlocked || !inRange
                || Tools.Busy || visited.Contains(index))
            {
                if (index >= 0 && !inRange && !overUI && Input.GetMouseButtonDown(0)) Tools.ReportFailure("距离太远，请靠近");
                return;
            }
            if (Tools.TryAct(index, out string message))
            {
                visited.Add(index);
                Feedback = message + "完成";
            }
            else { visited.Add(index); Feedback = message; }
        }
        public bool RefreshHomeHover(Ray ray, bool overUI)
        {
            if (overUI || !Physics.Raycast(ray, out var hit, 100, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return false;
            var portal = hit.collider.GetComponentInParent<HomePortal>(); var game = GameManager.Instance;
            if (game == null || !game.Housing.Owns(portal)) return false;
            ClearHover(); bool near = game.Housing.InRange(portal, Reach);
            Hint = near ? portal.IsExit ? "点击出口返回农场" : "点击小屋进门" : "距离太远，请靠近"; return true;
        }
        public bool RefreshIndoorHover(Ray ray, bool overUI)
        {
            ClearHover(); Hint = "WASD 移动·点击床休息或出口出门";
            var game = GameManager.Instance;
            if (game == null || !game.Housing.Inside || overUI || !Physics.Raycast(ray, out var hit, 100)) return false;
            var bed = hit.collider.GetComponentInParent<SleepBed>(); if (bed == null || bed != game.Housing.Interior.Bed) return false;
            Hint = game.Time.InRange(Player.transform.position, Reach) ? "点击床休息到明天" : "请走近床边休息"; return true;
        }
        public void RefreshFarmHover(Ray ray, bool overUI)
        {
            GatheringHoverIndex = -1;
            HoverIndex = overUI ? -1 : Farm.Pick(ray);
            HoverInRange = HoverIndex >= 0 && Vector3.ProjectOnPlane(Farm.CellCenter(HoverIndex) - Player.transform.position,
                Vector3.up).sqrMagnitude <= Reach * Reach;
            Farm.SetHover(HoverIndex, HoverInRange);
            Hint = HoverIndex < 0 ? "WASD 移动 · 鼠标指向农田" : HoverInRange ? FarmManager.Describe(HoverIndex, Tool) : "距离太远，请靠近";
        }
        public bool RefreshGatheringHover(Ray ray, bool overUI)
        {
            if (overUI || !Physics.Raycast(ray, out RaycastHit hit, 100f)) { GatheringHoverIndex = -1; return false; }
            GatheringNode node = hit.collider.GetComponentInParent<GatheringNode>();
            GameManager game = GameManager.Instance;
            if (game == null || !game.Gathering.Owns(node)) { GatheringHoverIndex = -1; return false; }
            ClearHover(); GatheringHoverIndex = node.Index;
            Hint = game.Gathering.InRange(node.Index, Player.transform.position, Reach) ? game.Gathering.Describe(node.Index, Tool) : "资源距离太远，请靠近";
            return true;
        }
        public PlacedDecoration DecorationHover { get; private set; }
        public bool RefreshDecorationHover(Ray ray, bool overUI)
        {
            DecorationHover = null; if (overUI || !Physics.Raycast(ray, out RaycastHit hit, 100f)) return false;
            var actor = hit.collider.GetComponentInParent<PlacedDecoration>(); var game = GameManager.Instance;
            if (game == null || !game.ActivePlacement.Owns(actor)) return false;
            ClearHover(); DecorationHover = actor;
            Hint = game.ActivePlacement.InRange(actor) ? game.Inventory.Catalog.Find(game.ActivePlacement.State.Find(actor.Id).ItemId).DisplayName + "·右键回收" : "距离太远，请靠近";
            return true;
        }
        public bool RefreshNPCHover(Ray ray, bool overUI)
        {
            NPCHoverId = null; if (overUI || !Physics.Raycast(ray, out RaycastHit hit, 100f)) return false;
            var actor = hit.collider.GetComponentInParent<NPCActor>(); var game = GameManager.Instance;
            if (game == null || !game.NPCs.Owns(actor)) return false;
            ClearHover(); NPCHoverId = actor.Id;
            Hint = game.NPCs.InRange(actor.Id, Player.transform.position, Reach) ? "\u70b9\u51fb\u4e0e" + game.NPCs.Catalog.Find(actor.Id).DisplayName + "\u4ea4\u8c08" : "\u8ddd\u79bb\u592a\u8fdc\uff0c\u8bf7\u9760\u8fd1";
            Hint += "\n" + game.NPCSchedules.Describe(actor.Id); return true;
        }
        public bool RefreshCookingHover(Ray ray, bool overUI)
        {
            if (overUI || !Physics.Raycast(ray, out RaycastHit hit, 100f)) return false;
            var station = hit.collider.GetComponentInParent<CookingStation>(); var game = GameManager.Instance;
            if (station == null || game == null || station != game.Cooking.Station) return false;
            ClearHover(); Hint = game.Cooking.InRange(Player.transform.position, Reach) ? "点击料理台制作食物" : "料理台距离太远，请靠近"; return true;
        }
        public bool RefreshFishingHover(Ray ray, bool overUI)
        {
            if (overUI || !Physics.Raycast(ray, out RaycastHit hit, 100f)) return false;
            var spot = hit.collider.GetComponentInParent<FishingSpot>(); var game = GameManager.Instance;
            if (game == null || spot == null || spot != game.Fishing.Spot) return false;
            ClearHover(); Hint = !game.Fishing.InRange(Player.transform.position, Reach) ? "钓点距离太远，请靠近海岸" : Tool != FarmTool.FishingRod ? "选择鱼竿（6），点击水面抛竿" : "点击海岸水面抛竿"; return true;
        }
        public bool RefreshStorageHover(Ray ray, bool overUI)
        {
            if (overUI || !Physics.Raycast(ray, out RaycastHit hit, 100f)) return false;
            var chest = hit.collider.GetComponentInParent<StorageChest>(); var game = GameManager.Instance;
            if (chest == null || game == null || chest != game.Storage.Chest) return false;
            ClearHover(); Hint = game.Storage.InRange(Player.transform.position, Reach) ? "点击储物箱存取物品" : "储物箱距离太远，请靠近"; return true;
        }
        public bool RefreshWorkbenchHover(Ray ray, bool overUI)
        {
            if (overUI || !Physics.Raycast(ray, out RaycastHit hit, 100f)) return false;
            var bench = hit.collider.GetComponentInParent<ToolWorkbench>(); var game = GameManager.Instance;
            if (bench == null || game == null || bench != game.ToolUpgrades.Workbench) return false;
            ClearHover(); Hint = game.ToolUpgrades.InRange(Player.transform.position, Reach) ? "点击工作台制作与升级" : "工作台距离太远，请靠近"; return true;
        }
        public bool RefreshOrderBoardHover(Ray ray, bool overUI)
        {
            if (overUI || !Physics.Raycast(ray, out RaycastHit hit, 100f)) return false;
            OrderBoard board = hit.collider.GetComponentInParent<OrderBoard>();
            GameManager game = GameManager.Instance;
            if (board == null || game == null || board != game.Orders.Board) return false;
            ClearHover();
            Hint = game.Orders.InRange(Player.transform.position, Reach) ? "点击公告栏查看今日订单" : "公告栏距离太远，请靠近";
            return true;
        }
        private void ClearHover()
        {
            DecorationHover = null; NPCHoverId = null;
            GatheringHoverIndex = -1;
            HoverIndex = -1; HoverInRange = false;
            if (Farm != null && Farm.State != null) Farm.SetHover(-1, false);
        }
        private void OnDisable()
        {
            visited.Clear(); gestureBlocked = true;
            ClearHover();
        }
    }
}
