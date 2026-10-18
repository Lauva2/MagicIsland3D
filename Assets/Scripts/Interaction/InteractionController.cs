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
        public InteractionManager Manager;
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
        public bool HasFocus { get; private set; } = true;
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
            if (!HasFocus) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(FarmTool.Hoe);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(FarmTool.Seeds);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(FarmTool.WateringCan);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectTool(FarmTool.Axe);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectTool(FarmTool.Pickaxe);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SelectTool(FarmTool.FishingRod);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SelectTool(FarmTool.Fertilizer);
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
            Manager.RefreshPointer(ViewCamera.ScreenPointToRay(Input.mousePosition), overUI);
            if (!overUI && Input.GetKeyDown(KeyCode.R)) { Manager.TryCastRain(); return; }
            var target = Manager.Current;
            if (overUI || gestureBlocked || !target.Visible) return;
            if (Input.GetMouseButtonDown(1)) { Manager.TryInteract(true); return; }
            if (target.Kind == InteractionKind.Farm)
            {
                if (!Input.GetMouseButton(0) || Tools.Busy || visited.Contains(target.Index)) return;
                if (!target.InRange) { if (Input.GetMouseButtonDown(0)) Manager.TryInteract(); return; }
                visited.Add(target.Index); Manager.TryInteract();
            }
            else if (Input.GetMouseButtonDown(0)) Manager.TryInteract();
        }
        internal void ApplyTarget(InteractionTarget target)
        {
            Hint = target.Visible ? target.Hint : GameManager.Instance?.Housing?.Inside == true
                ? "WASD 移动·点击床休息或出口出门" : "WASD 移动 · 鼠标指向可交互目标";
            bool visible = target.Visible;
            HoverIndex = visible && target.Kind == InteractionKind.Farm ? target.Index : -1;
            HoverInRange = HoverIndex >= 0 && target.InRange;
            if (Farm != null && Farm.State != null) Farm.SetHover(HoverIndex, HoverInRange);
            GatheringHoverIndex = visible && target.Kind == InteractionKind.Gathering ? target.Index : -1;
            NPCHoverId = visible && target.Kind == InteractionKind.NPC ? target.Id : null;
            DecorationHover = visible && target.Kind == InteractionKind.Decoration ? target.Root.GetComponent<PlacedDecoration>() : null;
        }
        // Retain the public hover queries used by earlier editor tools; all resolve the same target.
        public bool RefreshHomeHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Home;
        public bool RefreshCoopHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Coop;
        public bool RefreshIndoorHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Bed;
        public void RefreshFarmHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI);
        public bool RefreshGatheringHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Gathering;
        public PlacedDecoration DecorationHover { get; private set; }
        public bool RefreshDecorationHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Decoration;
        public bool RefreshNPCHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.NPC;
        public bool RefreshCookingHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Cooking;
        public bool RefreshFishingHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Fishing;
        public bool RefreshShippingHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Shipping;
        public bool RefreshStorageHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Storage;
        public bool RefreshWorkbenchHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Workbench;
        public bool RefreshOrderBoardHover(Ray ray, bool overUI) => Manager.RefreshPointer(ray, overUI).Kind == InteractionKind.Orders;
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
            ClearHover(); Manager?.ClearTarget();
        }
        private void OnApplicationFocus(bool focused)
        {
            HasFocus = focused;
            if (focused) return;
            visited.Clear(); gestureBlocked = true;
            ClearHover(); Manager?.ClearTarget();
        }
    }
}
