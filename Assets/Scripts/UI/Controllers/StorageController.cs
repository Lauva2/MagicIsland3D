using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class StorageController : IUIController
    {
        private readonly StorageView view;
        private readonly StorageManager storage;
        private readonly UnityAction close, one, stack, save;
        private readonly UnityAction[] bagSelect, chestSelect;
        private int selected = -1;
        private bool deposit = true;
        public StorageModel Model { get; } = new StorageModel();
        public StorageController(StorageView view, StorageManager storage, Action<bool, int, int> transfer, Action close, Action save)
        {
            this.view = view; this.storage = storage;
            if (storage.Inventory.State.Capacity > view.BagSlots.Length || storage.State.Contents.Capacity > view.ChestSlots.Length) throw new InvalidOperationException("StoragePanel needs enough slots");
            Model.BagSlots = CreateModels(storage.Inventory.State.Capacity); Model.ChestSlots = CreateModels(storage.State.Contents.Capacity);
            bagSelect = Bind(view.BagSlots, Model.BagSlots.Length, true); chestSelect = Bind(view.ChestSlots, Model.ChestSlots.Length, false);
            this.close = () => close(); this.save = () => save();
            one = () => { if (selected >= 0) transfer(deposit, selected, 1); };
            stack = () => { if (selected >= 0) transfer(deposit, selected, Source.Slots[selected].Quantity); };
            view.CloseButton.onClick.AddListener(this.close); view.MoveOneButton.onClick.AddListener(one); view.MoveStackButton.onClick.AddListener(stack); view.SaveButton.onClick.AddListener(this.save); Tick();
        }
        private InventoryState Source => deposit ? storage.Inventory.State : storage.State.Contents;
        private static InventorySlotModel[] CreateModels(int count) => Enumerable.Range(0, count).Select(i => new InventorySlotModel()).ToArray();
        private UnityAction[] Bind(InventorySlotView[] views, int count, bool fromBag)
        {
            var actions = new UnityAction[count]; for (int i = 0; i < count; i++) { int index = i; actions[i] = () => { selected = index; deposit = fromBag; Tick(); }; views[i].SelectButton.onClick.AddListener(actions[i]); } return actions;
        }
        public void Tick()
        {
            if (view == null || storage.State == null) return;
            Fill(Model.BagSlots, storage.Inventory.State, true); Fill(Model.ChestSlots, storage.State.Contents, false);
            Model.BagCapacity = "随身背包 · " + Model.BagSlots.Count(s => s.Quantity > 0) + " / " + Model.BagSlots.Length + " 格";
            Model.ChestCapacity = "储物箱 · " + Model.ChestSlots.Count(s => s.Quantity > 0) + " / " + Model.ChestSlots.Length + " 格";
            var slot = selected < 0 ? default : Source.Slots[selected];
            Model.Details = slot.Empty ? "选择左侧物品存入，或选择右侧物品取出" : (deposit ? "背包 · " : "箱内 · ") + storage.Inventory.Catalog.Find(slot.ItemId).DisplayName + " × " + slot.Quantity;
            Model.OneLabel = deposit ? "存入一个 →" : "← 取出一个"; Model.StackLabel = deposit ? "存入整组 →" : "← 取出整组";
            Model.CanMoveOne = selected >= 0 && storage.State.Check(deposit, selected, 1) == StorageResult.Success;
            Model.CanMoveStack = selected >= 0 && storage.State.Check(deposit, selected, slot.Quantity) == StorageResult.Success;
            Model.Feedback = storage.Feedback ?? "物品需要取回背包后才能使用 · 成功存取自动保存"; view.Render(Model);
        }
        private void Fill(InventorySlotModel[] models, InventoryState state, bool fromBag)
        {
            for (int i = 0; i < models.Length; i++)
            {
                var slot = state.Slots[i]; var item = slot.Empty ? null : storage.Inventory.Catalog.Find(slot.ItemId); var model = models[i];
                model.Name = item?.DisplayName; model.Quantity = slot.Quantity; model.Icon = item?.Icon; model.IconColor = item?.IconColor ?? UnityEngine.Color.white;
                model.Selected = i == selected && deposit == fromBag;
            }
        }
        public void Dispose()
        {
            if (view == null) return;
            for (int i = 0; i < bagSelect.Length; i++) view.BagSlots[i].SelectButton.onClick.RemoveListener(bagSelect[i]);
            for (int i = 0; i < chestSelect.Length; i++) view.ChestSlots[i].SelectButton.onClick.RemoveListener(chestSelect[i]);
            view.CloseButton.onClick.RemoveListener(close); view.MoveOneButton.onClick.RemoveListener(one); view.MoveStackButton.onClick.RemoveListener(stack); view.SaveButton.onClick.RemoveListener(save);
        }
    }
}
