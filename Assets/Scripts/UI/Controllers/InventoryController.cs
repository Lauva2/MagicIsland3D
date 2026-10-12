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
        private readonly FoodManager food;
        private readonly UnityAction eat, save;
        private readonly UnityAction place;
        private readonly PlacementManager placement;

        public InventoryController(InventoryView view, InventoryManager inventory, FarmManager farm, Func<string, bool> chooseSeed, Action closeInventory, FoodManager food = null, Action<int> eatFood = null, Action saveProgress = null, PlacementManager placement = null, Action<int> startPlacement = null)
        {
            this.view = view;
            manager = inventory; this.farm = farm; this.chooseSeed = chooseSeed;
            state = inventory.State;
            this.food = food;
            this.placement = placement; place = () => { if (selected >= 0 && Model.CanPlace) startPlacement?.Invoke(selected); };
            view.PlaceButton.onClick.AddListener(place);
            eat = () => { if (selected >= 0 && Model.CanUseFood) eatFood?.Invoke(selected); };
            save = () => saveProgress?.Invoke();
            view.UseFoodButton.onClick.AddListener(eat); view.SaveButton.onClick.AddListener(save);
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
            Model.IsFood = Model.IsFish = Model.CanUseFood = Model.IsPlaceable = false; Model.Feedback = food?.Feedback;
            Model.CanPlace = false;
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
                    Model.IsFood = food != null && food.State.Recovery(slot.ItemId) > 0;
                    Model.IsFish = item.Type == ItemType.Fish;
                    Model.IsPlaceable = placement?.Catalog?.FindItem(slot.ItemId) != null;
                    Model.CanPlace = Model.IsPlaceable && (GameManager.Instance?.CanPlaceItem(slot.ItemId) ?? false);
                    if (Model.IsPlaceable) Model.Feedback = GameManager.Instance?.ActivePlacement.Feedback;
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
            view.Render(Model);
        }
        public void Tick() { if (farm.State != null && (food != null || displayedDay != farm.State.Day)) Refresh(); }
        public void Dispose()
        {
            state.Changed -= Refresh;
            if (view == null) return;
            view.CloseButton.onClick.RemoveListener(close);
            view.UseSeedButton.onClick.RemoveListener(useSeed);
            view.UseFoodButton.onClick.RemoveListener(eat); view.SaveButton.onClick.RemoveListener(save);
            view.PlaceButton.onClick.RemoveListener(place);
            for (int i = 0; i < select.Length; i++) view.Slots[i].SelectButton.onClick.RemoveListener(select[i]);
        }
    }
}
