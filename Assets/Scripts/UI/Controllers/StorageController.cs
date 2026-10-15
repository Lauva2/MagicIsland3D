using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace MagicIsland.UI.Controllers
{
    public sealed class StorageController : IUIController
    {
        private readonly StorageView view;
        private readonly StorageManager storage;
        private readonly InventoryState bag, chest;
        private readonly UnityAction close, one, stack, save, matching, previous, next;
        private readonly UnityAction[] bagSelect, chestSelect;
        private readonly Func<bool, int, bool> beginDrag;
        private readonly Action<bool, int> drop, quick;
        private readonly Action cancelDrag;
        private int selected = -1, page, pointerId;
        private bool deposit = true;
        private InventorySlotView dragView;
        private uint bagRevision, chestRevision;
        public StorageModel Model { get; } = new StorageModel();
        public StorageController(StorageView view, StorageManager storage, Action<bool, int, int> transfer, Action close, Action save,
            Func<bool, int, bool> beginDrag = null, Action<bool, int> drop = null, Action cancelDrag = null, Action<bool, int> quick = null, Action matching = null)
        {
            this.view = view; this.storage = storage; this.beginDrag = beginDrag; this.drop = drop; this.cancelDrag = cancelDrag; this.quick = quick;
            bag = storage.Inventory.State; chest = storage.State.Contents; bagRevision = bag.ReorderVersion; chestRevision = chest.ReorderVersion;
            if (view.BagSlots.Length != InventoryView.DisplaySlots || chest.Capacity > view.ChestSlots.Length) throw new InvalidOperationException("StoragePanel requires thirty bag cells and enough chest cells");
            Model.BagSlots = CreateModels(bag.Capacity); Model.ChestSlots = CreateModels(chest.Capacity);
            bagSelect = Bind(view.BagSlots, true); chestSelect = Bind(view.ChestSlots, false);
            this.close = () => close(); this.save = () => save(); this.matching = () => matching?.Invoke();
            one = () => { if (dragView == null && selected >= 0) transfer(deposit, selected, 1); };
            stack = () => { if (dragView == null && selected >= 0) transfer(deposit, selected, Source.Slots[selected].Quantity); };
            previous = () => ChangePage(-1); next = () => ChangePage(1);
            view.CloseButton.onClick.AddListener(this.close); view.MoveOneButton.onClick.AddListener(one); view.MoveStackButton.onClick.AddListener(stack);
            view.SaveButton.onClick.AddListener(this.save); view.DepositMatchingButton.onClick.AddListener(this.matching);
            view.PreviousPageButton.onClick.AddListener(previous); view.NextPageButton.onClick.AddListener(next);
            bag.Changed += Tick; chest.Changed += Tick; Tick();
        }
        private InventoryState Source => deposit ? bag : chest;
        private int Index(bool fromBag, int visible) => fromBag ? page * InventoryView.DisplaySlots + visible : visible;
        private bool Unlocked(bool fromBag, int visible) => visible >= 0 && Index(fromBag, visible) < (fromBag ? bag : chest).Capacity;
        private static InventorySlotModel[] CreateModels(int count) => Enumerable.Range(0, count).Select(_ => new InventorySlotModel()).ToArray();
        private UnityAction[] Bind(InventorySlotView[] views, bool fromBag)
        {
            var actions = new UnityAction[views.Length];
            for (int i = 0; i < views.Length; i++)
            {
                int visible = i; var slot = views[i]; var input = slot.GetComponent<InventorySlotInput>();
                if (input == null) throw new InvalidOperationException("StoragePanel slot needs InventorySlotInput");
                actions[i] = () => { if (dragView == null && !storage.IsDragging && !input.QuickModifier && Unlocked(fromBag, visible)) { selected = Index(fromBag, visible); deposit = fromBag; Tick(); } };
                slot.SelectButton.onClick.AddListener(actions[i]);
                input.Quick = () => { if (dragView == null && !storage.IsDragging && Unlocked(fromBag, visible)) this.quick?.Invoke(fromBag, Index(fromBag, visible)); };
                input.Begin = data =>
                {
                    if (dragView != null || !Unlocked(fromBag, visible) || !(beginDrag?.Invoke(fromBag, Index(fromBag, visible)) ?? false)) return false;
                    dragView = slot; pointerId = data.pointerId; Tick(); view.MoveDragPreview(data); return true;
                };
                input.Drag = data => { if (dragView == slot && data.pointerId == pointerId) view.MoveDragPreview(data); };
                input.End = data => { if (dragView == slot && data.pointerId == pointerId) CancelDrag(); };
                input.Cancel = () => { if (dragView == slot) CancelDrag(); };
                input.Drop = data =>
                {
                    if (dragView == null || data.pointerId != pointerId || data.pointerDrag != dragView.gameObject || fromBag == storage.DragDeposit || !Unlocked(fromBag, visible)) return;
                    dragView = null;
                    try { drop?.Invoke(fromBag, Index(fromBag, visible)); }
                    finally { cancelDrag?.Invoke(); Tick(); }
                };
            }
            return actions;
        }
        private void CancelDrag(bool refresh = true) { dragView = null; cancelDrag?.Invoke(); if (refresh && view != null) Tick(); }
        private void ChangePage(int delta)
        {
            if (dragView != null || storage.IsDragging) return;
            int target = page + delta, count = (bag.Capacity + InventoryView.DisplaySlots - 1) / InventoryView.DisplaySlots;
            if (target < 0 || target >= count) return; page = target; selected = -1; Tick();
        }
        public void Tick()
        {
            if (view == null || storage.State == null) return;
            if (dragView != null && !storage.DragValid) CancelDrag(false);
            if (bagRevision != bag.ReorderVersion || chestRevision != chest.ReorderVersion)
            { bagRevision = bag.ReorderVersion; chestRevision = chest.ReorderVersion; selected = -1; }
            if (Model.BagSlots.Length != bag.Capacity) Model.BagSlots = CreateModels(bag.Capacity);
            Model.PageCount = (bag.Capacity + InventoryView.DisplaySlots - 1) / InventoryView.DisplaySlots; page = Math.Min(page, Model.PageCount - 1); Model.Page = page;
            Model.Dragging = dragView != null && storage.IsDragging;
            if (Model.Dragging)
            { var slot = (storage.DragDeposit ? bag : chest).Slots[storage.DragSource]; var item = storage.Inventory.Catalog.Find(slot.ItemId); Model.DragName = item.DisplayName; Model.DragQuantity = slot.Quantity; Model.DragIcon = item.Icon; Model.DragColor = item.IconColor; }
            Fill(Model.BagSlots, bag, true); Fill(Model.ChestSlots, chest, false);
            Model.BagCapacity = "随身背包 · " + Model.BagSlots.Count(s => s.Quantity > 0) + " / " + bag.Capacity + " 格 · 本页解锁 " + Math.Min(30, bag.Capacity - page * 30) + " / 30";
            Model.ChestCapacity = "储物箱 · " + Model.ChestSlots.Count(s => s.Quantity > 0) + " / " + chest.Capacity + " 格";
            var value = selected < 0 ? default : Source.Slots[selected];
            Model.Details = value.Empty ? "选择上方物品存入，或选择下方物品取出" : (deposit ? "背包 · " : "箱内 · ") + storage.Inventory.Catalog.Find(value.ItemId).DisplayName + " × " + value.Quantity;
            Model.OneLabel = deposit ? "存入一个 ↓" : "↑ 取出一个"; Model.StackLabel = deposit ? "存入整组 ↓" : "↑ 取出整组";
            Model.CanMoveOne = !Model.Dragging && selected >= 0 && storage.State.Check(deposit, selected, 1) == StorageResult.Success;
            Model.CanMoveStack = !Model.Dragging && selected >= 0 && storage.State.Check(deposit, selected, value.Quantity) == StorageResult.Success;
            Model.CanDepositMatching = !Model.Dragging && storage.State.CanDepositMatching;
            Model.Feedback = storage.Feedback ?? "物品需要取回背包后才能使用 · 进度仅在睡觉时保存"; view.Render(Model);
        }
        private void Fill(InventorySlotModel[] models, InventoryState state, bool fromBag)
        {
            for (int i = 0; i < models.Length; i++)
            {
                var slot = state.Slots[i]; var item = slot.Empty ? null : storage.Inventory.Catalog.Find(slot.ItemId); var model = models[i];
                model.Name = item?.DisplayName; model.Quantity = slot.Quantity; model.Icon = item?.Icon; model.IconColor = item?.IconColor ?? UnityEngine.Color.white;
                model.Selected = i == selected && deposit == fromBag; model.DragSource = Model.Dragging && storage.DragDeposit == fromBag && storage.DragSource == i;
            }
        }
        public void Dispose()
        {
            CancelDrag(false); bag.Changed -= Tick; chest.Changed -= Tick; if (view == null) return;
            for (int i = 0; i < bagSelect.Length; i++) { view.BagSlots[i].SelectButton.onClick.RemoveListener(bagSelect[i]); view.BagSlots[i].GetComponent<InventorySlotInput>().Unbind(); }
            for (int i = 0; i < chestSelect.Length; i++) { view.ChestSlots[i].SelectButton.onClick.RemoveListener(chestSelect[i]); view.ChestSlots[i].GetComponent<InventorySlotInput>().Unbind(); }
            view.CloseButton.onClick.RemoveListener(close); view.MoveOneButton.onClick.RemoveListener(one); view.MoveStackButton.onClick.RemoveListener(stack); view.SaveButton.onClick.RemoveListener(save);
            view.DepositMatchingButton.onClick.RemoveListener(matching); view.PreviousPageButton.onClick.RemoveListener(previous); view.NextPageButton.onClick.RemoveListener(next);
        }
    }
}