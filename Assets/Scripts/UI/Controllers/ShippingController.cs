using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class ShippingController : IUIController
    {
        private readonly ShippingView view;
        private readonly ShippingManager shipping;
        private readonly UnityAction one, stack, close, save, previous, next;
        private readonly UnityAction[] bagSelect, boxSelect;
        private int selected = -1, page;
        private bool deposit = true;
        public ShippingModel Model { get; } = new ShippingModel();
        private InventoryState Bag => shipping.Inventory.State;
        private InventoryState Source => deposit ? Bag : shipping.State.Contents;
        public ShippingController(ShippingView view, ShippingManager shipping, Action<bool, int, int> transfer, Action close, Action save)
        {
            this.view = view; this.shipping = shipping;
            if (view == null || view.BagSlots.Length != 30 || view.BoxSlots.Length != 18) throw new InvalidOperationException("ShippingPanel requires thirty bag cells and eighteen shipping cells");
            Model.BoxSlots = Models(18); bagSelect = Bind(view.BagSlots, true); boxSelect = Bind(view.BoxSlots, false);
            one = () => { if (selected >= 0) transfer(deposit, selected, 1); };
            stack = () => { if (selected >= 0 && selected < Source.Capacity) transfer(deposit, selected, Source.Slots[selected].Quantity); };
            this.close = () => close(); this.save = () => save(); previous = () => ChangePage(-1); next = () => ChangePage(1);
            view.MoveOneButton.onClick.AddListener(one); view.MoveStackButton.onClick.AddListener(stack); view.CloseButton.onClick.AddListener(this.close); view.SaveButton.onClick.AddListener(this.save);
            view.PreviousPageButton.onClick.AddListener(previous); view.NextPageButton.onClick.AddListener(next); Tick();
        }
        private static InventorySlotModel[] Models(int n) => Enumerable.Range(0, n).Select(_ => new InventorySlotModel()).ToArray();
        private UnityAction[] Bind(InventorySlotView[] slots, bool fromBag)
        {
            var bindings = new UnityAction[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                int cell = i; bindings[i] = () => { int index = fromBag ? page * 30 + cell : cell; if (index < (fromBag ? Bag : shipping.State.Contents).Capacity) { selected = index; deposit = fromBag; Tick(); } };
                slots[i].SelectButton.onClick.AddListener(bindings[i]);
            }
            return bindings;
        }
        private void ChangePage(int delta) { int target = page + delta; if (target < 0 || target >= Model.PageCount) return; page = target; selected = -1; Tick(); }
        public void Tick()
        {
            if (view == null || shipping.State == null) return;
            if (Model.BagSlots == null || Model.BagSlots.Length != Bag.Capacity) { Model.BagSlots = Models(Bag.Capacity); selected = -1; }
            if (selected >= 0 && (selected >= Source.Capacity || Source.Slots[selected].Empty)) selected = -1;
            Model.Page = page; Model.PageCount = (Bag.Capacity + 29) / 30;
            Fill(Model.BagSlots, Bag, true); Fill(Model.BoxSlots, shipping.State.Contents, false);
            Model.BagCapacity = $"随身背包 · {Bag.Slots.Count(s => !s.Empty)} / {Bag.Capacity} 格";
            Model.ExpectedIncome = $"待出货 · 预计收入 {shipping.State.ExpectedIncome} 金币";
            var slot = selected >= 0 && selected < Source.Capacity ? Source.Slots[selected] : default;
            int price = shipping.State.Price(slot.ItemId);
            Model.Details = slot.Empty ? "选择上方物品出货，或选择下方物品取回" : shipping.Inventory.Catalog.Find(slot.ItemId).DisplayName + " × " + slot.Quantity + (price > 0 ? $" · 单价 {price} 金币" : " · 此物品不可出货");
            Model.OneLabel = deposit ? "放入一个 ↓" : "↑ 取回一个"; Model.StackLabel = deposit ? "放入整组 ↓" : "↑ 取回整组";
            Model.CanMoveOne = !slot.Empty && shipping.State.Check(deposit, selected, 1) == StorageResult.Success;
            Model.CanMoveStack = !slot.Empty && shipping.State.Check(deposit, selected, slot.Quantity) == StorageResult.Success;
            Model.Feedback = shipping.Feedback ?? "睡觉后统一售卖 · 当天可取回 · 进度仅在睡觉时保存"; view.Render(Model);
        }
        private void Fill(InventorySlotModel[] models, InventoryState state, bool fromBag)
        {
            for (int i = 0; i < models.Length; i++)
            {
                var slot = state.Slots[i]; var item = slot.Empty ? null : shipping.Inventory.Catalog.Find(slot.ItemId);
                models[i].Name = item?.DisplayName; models[i].Quantity = slot.Quantity; models[i].Icon = item?.Icon; models[i].IconColor = item?.IconColor ?? UnityEngine.Color.white;
                models[i].Selected = selected == i && deposit == fromBag;
            }
        }
        public void Dispose()
        {
            if (view == null) return;
            for (int i = 0; i < bagSelect.Length; i++) view.BagSlots[i].SelectButton.onClick.RemoveListener(bagSelect[i]);
            for (int i = 0; i < boxSelect.Length; i++) view.BoxSlots[i].SelectButton.onClick.RemoveListener(boxSelect[i]);
            view.MoveOneButton.onClick.RemoveListener(one); view.MoveStackButton.onClick.RemoveListener(stack); view.CloseButton.onClick.RemoveListener(close); view.SaveButton.onClick.RemoveListener(save);
            view.PreviousPageButton.onClick.RemoveListener(previous); view.NextPageButton.onClick.RemoveListener(next);
        }
    }
}
