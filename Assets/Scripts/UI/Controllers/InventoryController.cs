using System;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;

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

        public InventoryController(InventoryView view, InventoryManager inventory, FarmManager farm, Func<string, bool> chooseSeed, Action closeInventory)
        {
            this.view = view;
            manager = inventory; this.farm = farm; this.chooseSeed = chooseSeed;
            state = inventory.State;
            if (state.Capacity > view.Slots.Length) throw new InvalidOperationException("InventoryPanel has too few slots");
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
            select = new UnityAction[state.Capacity];
            for (int i = 0; i < select.Length; i++)
            {
                int index = i;
                select[i] = () => { selected = index; Refresh(); };
                view.Slots[i].SelectButton.onClick.AddListener(select[i]);
            }
            state.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            displayedDay = farm.State.Day;
            Model.Occupied = 0; Model.CanUseSeed = false;
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
                if (slot.Empty) continue;
                Model.Occupied++;
                if (i == selected)
                {
                    Model.DetailTitle = item.DisplayName;
                    Model.CanUseSeed = farm.Crops.FindBySeed(slot.ItemId) != null && slot.Quantity > 0;
                    Model.DetailText = $"{item.Description}\n数量 {slot.Quantity} · 每格最多 {item.MaxStack}";
                    var crop = farm.Crops.FindBySeed(slot.ItemId);
                    if (crop != null && farm.Calendar != null) Model.DetailText += "\n" + farm.Calendar.PlantingDescription(crop);
                }
            }
            view.Render(Model);
        }
        public void Tick() { if (farm.State != null && displayedDay != farm.State.Day) Refresh(); }
        public void Dispose()
        {
            state.Changed -= Refresh;
            if (view == null) return;
            view.CloseButton.onClick.RemoveListener(close);
            view.UseSeedButton.onClick.RemoveListener(useSeed);
            for (int i = 0; i < select.Length; i++) view.Slots[i].SelectButton.onClick.RemoveListener(select[i]);
        }
    }
}
