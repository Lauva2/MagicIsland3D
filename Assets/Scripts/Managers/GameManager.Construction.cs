using System.Collections;
using MagicIsland.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        public CraftingManager Crafting { get; private set; }
        public PlacementManager Placements { get; private set; }
        public FurnitureManager Furniture { get; private set; }
        public PlacementManager ActivePlacement => Housing.Inside ? Furniture : Placements;
        public bool CanPlaceItem(string id)
        { var recipe = Crafting?.Catalog?.FindItem(id); return recipe != null && recipe.IsPlaceable && recipe.IsFurniture == Housing.Inside; }
        private bool craftingOpening, craftingCommitting, placementOpening, placementCommitting;
        private int constructionGeneration;
        private bool ConstructionBusy => MagicBusy || MapBusy || advancingDay || DailyReportBusy || InventoryUpgradeBusy || AnimalBusy || HouseUpgradeBusy || craftingOpening || craftingCommitting || UI.HasCrafting || placementOpening || placementCommitting || Placements.Active || Furniture.Active;
        public void OpenCrafting()
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !ToolUpgrades.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            craftingOpening = true; SetInputEnabled(false); StartCoroutine(LoadCrafting(++constructionGeneration));
        }
        private IEnumerator LoadCrafting(int generation)
        {
            yield return UI.ShowCrafting(Crafting, CraftRecipe, CloseCrafting, SaveCrafting, SwitchToUpgrades);
            if (generation != constructionGeneration) yield break;
            craftingOpening = false;
            if (State != GameFlowState.Playing || UI.LastError != null || !UI.HasCrafting)
            { CloseCrafting(); Interactions.SetFeedback("暂时无法打开制作面板，请重试"); }
        }
        public void CloseCrafting()
        {
            if (craftingCommitting) return;
            constructionGeneration++; craftingOpening = false; UI.CloseCrafting();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void CraftRecipe(int index)
        {
            if (State != GameFlowState.Playing || !UI.HasCrafting || craftingOpening || craftingCommitting || Tools.Busy
                || !ToolUpgrades.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            craftingCommitting = true;
            try { if (Crafting.Craft(index)) Interactions.SetFeedback(Crafting.Feedback); UI.RefreshViews(); }
            finally { craftingCommitting = false; }
        }
        public void SaveCrafting()
        {
            Interactions.SetFeedback("进度只在每天睡觉结算后保存");
        }
        private void SwitchToUpgrades() { if (craftingOpening || craftingCommitting) return; CloseCrafting(); OpenToolUpgrade(); }
        private void SwitchToCrafting() { if (upgradeOpening || upgradeCommitting) return; CloseToolUpgrade(); OpenCrafting(); }
        public void BeginPlacementFromBag(int index)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || consumableCommitting || Inventory.IsDragging || !UI.IsInventoryVisible || Tools.Busy || index < 0 || index >= Inventory.State.Capacity) return;
            var slot = Inventory.State.Slots[index]; if (slot.Empty || !CanPlaceItem(slot.ItemId)) return;
            UI.SetInventoryVisible(false); placementOpening = true; SetInputEnabled(false); StartCoroutine(LoadPlacement(slot.ItemId, ++constructionGeneration));
        }
        private IEnumerator LoadPlacement(string item, int generation)
        {
            yield return ActivePlacement.Begin(item);
            if (generation != constructionGeneration) yield break;
            if (ActivePlacement.LastError == null) yield return UI.ShowPlacement(ActivePlacement, ConfirmPlacement, RotatePlacement, CancelPlacement, SaveConstruction);
            if (generation != constructionGeneration) yield break;
            placementOpening = false;
            if (State != GameFlowState.Playing || ActivePlacement.LastError != null || UI.LastError != null || !UI.HasPlacement)
            { CancelPlacement(); Interactions.SetFeedback("暂时无法加载摆放预览，请重试"); yield break; }
            Players.SetInputEnabled(true); Cameras.SetInputEnabled(true); Tools.SetInputEnabled(false); Interactions.SetInputEnabled(false);
        }
        public void RotatePlacement() { if (State == GameFlowState.Playing && !placementOpening && !placementCommitting) ActivePlacement.Rotate(); }
        public void CancelPlacement()
        {
            if (placementCommitting) return;
            constructionGeneration++; placementOpening = false; Placements.Cancel(); Furniture.Cancel(); UI.ClosePlacement();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void ConfirmPlacement()
        {
            if (State != GameFlowState.Playing || placementOpening || placementCommitting || !ActivePlacement.Ready || !UI.HasPlacement) return;
            bool placed = false; placementCommitting = true;
            try
            {
                placed = ActivePlacement.Confirm();
                if (placed) Interactions.SetFeedback(ActivePlacement.Feedback);
                UI.RefreshViews();
            }
            finally { placementCommitting = false; }
            if (placed) CancelPlacement();
        }
        public void RecoverDecoration(PlacedDecoration actor)
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !ActivePlacement.Owns(actor)) return;
            placementCommitting = true;
            try { ActivePlacement.Recover(actor); Interactions.SetFeedback(ActivePlacement.Feedback); UI.RefreshViews(); }
            finally { placementCommitting = false; }
        }
        public void SaveConstruction()
        { Interactions.SetFeedback("进度只在每天睡觉结算后保存"); }
        private void UpdateConstruction()
        {
            if (placementOpening || !ActivePlacement.Ready) return;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            ActivePlacement.UpdatePreview(Players.Player.ViewCamera.ScreenPointToRay(Input.mousePosition), overUI);
            if (Input.GetKeyDown(KeyCode.R)) RotatePlacement();
            if (!overUI && Input.GetMouseButtonDown(0)) ConfirmPlacement();
        }
        private void CloseConstructionForExit()
        { constructionGeneration++; craftingOpening = placementOpening = false; UI.CloseCrafting(); UI.ClosePlacement(); Placements.Cancel(); Furniture.Cancel(); }
    }
}
