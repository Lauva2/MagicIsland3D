using System;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace MagicIsland.UI.Controllers
{
    public sealed class InventoryController : IUIController
    {
        public InventoryModel Model { get; } = new InventoryModel();
        private readonly InventoryView view;
        private readonly InventoryManager manager;
        private readonly InventoryState state;
        private readonly UnityAction close, useSeed;
        private readonly FarmManager farm;
        private readonly Func<string, bool> chooseSeed;
        private readonly UnityAction[] select;
        private int selected = -1;
        private int displayedDay;
        private readonly FoodManager food;
        private readonly UnityAction eat, save, sort;
        private uint reorderVersion;
        private readonly UnityAction place;
        private readonly PlacementManager placement;
        private readonly Func<int, bool> beginDrag;
        private readonly Action<int> drop, split;
        private readonly Action cancelDrag;
        private int dragging = -1, dragPointer;
        private readonly UnityAction previousPage, nextPage;
        private int page;
        private int StateIndex(int visible) => page * InventoryView.DisplaySlots + visible;
        private bool IsUnlocked(int visible) => visible >= 0 && visible < InventoryView.DisplaySlots && StateIndex(visible) < state.Capacity;

        public InventoryController(InventoryView view, InventoryManager inventory, FarmManager farm, Func<string, bool> chooseSeed, Action closeInventory, FoodManager food = null, Action<int> eatFood = null, Action saveProgress = null, PlacementManager placement = null, Action<int> startPlacement = null, Action sortInventory = null, Func<int, bool> beginDrag = null, Action<int> drop = null, Action<int> split = null, Action cancelDrag = null)
        {
            this.view = view;
            manager = inventory; this.farm = farm; this.chooseSeed = chooseSeed;
            this.beginDrag = beginDrag; this.drop = drop; this.split = split; this.cancelDrag = cancelDrag;
            state = inventory.State;
            previousPage = () => ChangePage(-1); nextPage = () => ChangePage(1);
            view.PreviousPageButton.onClick.AddListener(previousPage); view.NextPageButton.onClick.AddListener(nextPage);
            reorderVersion = state.ReorderVersion;
            sort = () => sortInventory?.Invoke(); view.SortButton.onClick.AddListener(sort);
            this.food = food;
            this.placement = placement; place = () => { if (selected >= 0 && Model.CanPlace) startPlacement?.Invoke(selected); };
            view.PlaceButton.onClick.AddListener(place);
            eat = () => { if (selected >= 0 && Model.CanUseFood) eatFood?.Invoke(selected); };
            save = () => saveProgress?.Invoke();
            view.UseFoodButton.onClick.AddListener(eat); view.SaveButton.onClick.AddListener(save);
            if (view.Slots.Length != InventoryView.DisplaySlots) throw new InvalidOperationException("InventoryPanel requires three rows of ten slots");
            Model.Slots = new InventorySlotModel[state.Capacity];
            for (int i = 0; i < Model.Slots.Length; i++) Model.Slots[i] = new InventorySlotModel();
            close = () => closeInventory();
            view.CloseButton.onClick.AddListener(close);
            useSeed = () =>
            {
                if (selected < 0 || !Model.CanUseSeed) return;
                if (!this.chooseSeed(state.Slots[selected].ItemId))
                { Model.DetailText = "动作结束后再选择种子。"; view.Render(Model); }
            };
            view.UseSeedButton.onClick.AddListener(useSeed);
            select = new UnityAction[view.Slots.Length];
            for (int i = 0; i < select.Length; i++)
            {
                int index = i;
                select[i] = () => { if (dragging < 0 && !manager.IsDragging && IsUnlocked(index)) { selected = StateIndex(index); manager.ClearSortFeedback(); manager.ClearEditFeedback(); Refresh(); } };
                view.Slots[i].SelectButton.onClick.AddListener(select[i]);
                var input = view.Slots[i].GetComponent<InventorySlotInput>();
                if (input == null) throw new InvalidOperationException("InventoryPanel slot needs InventorySlotInput");
                input.Begin = data => StartDrag(index, data);
                input.Drag = data => { if (dragging == index && data.pointerId == dragPointer) view.MoveDragPreview(data); };
                input.End = data => { if (dragging == index && data.pointerId == dragPointer) CancelDrag(); };
                input.Drop = data => DropOn(index, data);
                input.Split = () => { if (dragging < 0 && !manager.IsDragging && IsUnlocked(index)) { this.split?.Invoke(StateIndex(index)); Refresh(); } };
                input.Cancel = () => { if (dragging == index) CancelDrag(); };
            }
            state.Changed += Refresh;
            Refresh();
        }

        private bool StartDrag(int index, PointerEventData data)
        {
            if (dragging >= 0 || !IsUnlocked(index) || !(beginDrag?.Invoke(StateIndex(index)) ?? false)) return false;
            dragging = index; dragPointer = data.pointerId;
            Refresh(); view.MoveDragPreview(data); return true;
        }
        private void DropOn(int index, PointerEventData data)
        {
            if (dragging < 0 || data.pointerId != dragPointer || data.pointerDrag != view.Slots[dragging].gameObject || !IsUnlocked(index)) return;
            dragging = -1;
            try { drop?.Invoke(StateIndex(index)); }
            finally { cancelDrag?.Invoke(); Refresh(); }
        }
        private void CancelDrag(bool refresh = true)
        {
            dragging = -1; cancelDrag?.Invoke();
            if (refresh && view != null) Refresh();
        }
        private void ChangePage(int delta)
        {
            if (dragging >= 0 || manager.IsDragging) return;
            int target = page + delta, count = (state.Capacity + InventoryView.DisplaySlots - 1) / InventoryView.DisplaySlots;
            if (target < 0 || target >= count) return;
            page = target; selected = -1; Refresh();
        }

        private void Refresh()
        {
            if (dragging >= 0 && !manager.DragValid) CancelDrag(false);
            if (reorderVersion != state.ReorderVersion) { reorderVersion = state.ReorderVersion; selected = -1; }
            Model.PageCount = (state.Capacity + InventoryView.DisplaySlots - 1) / InventoryView.DisplaySlots;
            page = Math.Min(page, Model.PageCount - 1); Model.Page = page;
            Model.Dragging = dragging >= 0 && manager.IsDragging;
            Model.CanSort = state.CanSort && !Model.Dragging;
            if (Model.Dragging)
            {
                var slot = state.Slots[manager.DragSource]; var item = manager.Catalog.Find(slot.ItemId);
                Model.DragName = item.DisplayName; Model.DragIcon = item.Icon; Model.DragColor = item.IconColor; Model.DragQuantity = slot.Quantity;
            }
            if (Model.Slots.Length != state.Capacity)
            { Model.Slots = new InventorySlotModel[state.Capacity]; for (int i = 0; i < Model.Slots.Length; i++) Model.Slots[i] = new InventorySlotModel(); }
            displayedDay = farm.State.Day;
            Model.Occupied = 0; Model.CanUseSeed = false;
            Model.IsFood = Model.IsFish = Model.CanUseFood = Model.IsPlaceable = false; Model.Feedback = manager.EditFeedback ?? manager.SortFeedback ?? food?.Feedback;
            Model.CanPlace = false; Model.IsAnimalProduct = Model.IsSupply = false;
            Model.DetailTitle = "选择物品";
            Model.DetailText = "点击一个格子，查看物品详情。";
            for (int i = 0; i < state.Capacity; i++)
            {
                InventorySlot slot = state.Slots[i];
                var item = slot.Empty ? null : manager.Catalog.Find(slot.ItemId);
                InventorySlotModel model = Model.Slots[i];
                model.Name = item?.DisplayName;
                model.Quantity = slot.Quantity;
                model.Icon = item?.Icon;
                model.IconColor = item?.IconColor ?? UnityEngine.Color.white;
                model.Selected = i == selected;
                model.DragSource = Model.Dragging && i == manager.DragSource;
                if (slot.Empty) continue;
                Model.Occupied++;
                if (i == selected)
                {
                    Model.DetailTitle = item.DisplayName;
                    Model.CanUseSeed = farm.Crops.FindBySeed(slot.ItemId) != null && slot.Quantity > 0;
                    Model.IsFood = food != null && food.State.Recovery(slot.ItemId) > 0;
                    Model.IsFish = item.Type == ItemType.Fish;
                    Model.IsAnimalProduct = item.Type == ItemType.AnimalProduct;
                    var recipe = placement?.Catalog?.FindItem(slot.ItemId);
                    Model.IsPlaceable = recipe?.IsPlaceable ?? false;
                    Model.IsSupply = recipe?.IsSupply ?? false;
                    Model.CanPlace = Model.IsPlaceable && (GameManager.Instance?.CanPlaceItem(slot.ItemId) ?? false);
                    if (Model.IsPlaceable && manager.EditFeedback == null && manager.SortFeedback == null) Model.Feedback = GameManager.Instance?.ActivePlacement.Feedback;
                    Model.CanUseFood = Model.IsFood && food.Check(i) == FoodResult.Success;
                    Model.UseFoodLabel = Model.CanUseFood ? "食用一个" : "体力已满";
                    Model.DetailText = $"{item.Description}\n数量 {slot.Quantity} · 每格最多 {item.MaxStack}";
                    var crop = farm.Crops.FindBySeed(slot.ItemId);
                    if (crop != null && farm.Calendar != null) Model.DetailText += "\n" + farm.Calendar.PlantingDescription(crop);
                    if (Model.IsFood) Model.DetailText += "\n恢复 " + food.State.Recovery(slot.ItemId) + " 体力 · 当前 " + food.Stamina.State.Current + " / " + food.Stamina.State.Maximum;
                    if (Model.IsFish) Model.DetailText += "\n商店收购 " + item.SellPrice + " 金币 / 条";
                    if (Model.IsPlaceable && !Model.CanPlace) Model.DetailText += item.Type == ItemType.Furniture ? "\n请带到小屋内摆放" : "\n请带到农场后摆放";
                }
            }
            if (Model.Dragging) Model.CanUseSeed = Model.CanUseFood = Model.CanPlace = false;
            view.Render(Model);
        }
        public void Tick() { if (farm.State != null && (food != null || displayedDay != farm.State.Day)) Refresh(); }
        public void Dispose()
        {
            CancelDrag(false);
            state.Changed -= Refresh;
            if (view == null) return;
            view.CloseButton.onClick.RemoveListener(close);
            view.UseSeedButton.onClick.RemoveListener(useSeed);
            view.UseFoodButton.onClick.RemoveListener(eat); view.SaveButton.onClick.RemoveListener(save);
            view.SortButton.onClick.RemoveListener(sort);
            view.PreviousPageButton.onClick.RemoveListener(previousPage); view.NextPageButton.onClick.RemoveListener(nextPage);
            view.PlaceButton.onClick.RemoveListener(place);
            for (int i = 0; i < select.Length; i++) { view.Slots[i].SelectButton.onClick.RemoveListener(select[i]); view.Slots[i].GetComponent<InventorySlotInput>().Unbind(); }
        }
    }
}
