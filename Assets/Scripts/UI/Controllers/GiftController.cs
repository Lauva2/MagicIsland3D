using System;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Interaction;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class GiftController : IUIController
    {
        private readonly GiftView view;
        private readonly RelationshipManager relationships;
        private readonly UnityAction close, give, save;
        private readonly UnityAction[] select;
        private int selected = -1;
        public GiftModel Model { get; } = new GiftModel();
        public GiftController(GiftView view, RelationshipManager relationships, Action<int> give, Action close, Action save)
        {
            this.view = view; this.relationships = relationships;
            int capacity = relationships.Inventory.State.Capacity; if (capacity > view.Slots.Length) throw new InvalidOperationException("GiftPanel needs enough inventory slots");
            Model.Slots = Enumerable.Range(0, capacity).Select(i => new InventorySlotModel()).ToArray(); Model.Eligible = new bool[capacity]; select = new UnityAction[capacity];
            for (int i = 0; i < capacity; i++) { int index = i; select[i] = () => { selected = index; Tick(); }; view.Slots[i].SelectButton.onClick.AddListener(select[i]); }
            this.give = () => { if (selected >= 0) give(selected); }; this.close = () => close(); this.save = () => save();
            view.GiveButton.onClick.AddListener(this.give); view.CloseButton.onClick.AddListener(this.close); view.SaveButton.onClick.AddListener(this.save); Tick();
        }
        public void Tick()
        {
            if (view == null || relationships.State == null || relationships.TargetId == null) return;
            var id = relationships.TargetId; var rule = relationships.State.Find(id); var bag = relationships.Inventory.State;
            Model.Title = "给" + relationships.NPCs.Catalog.Find(id).DisplayName + "挑选礼物"; Model.Status = relationships.Status(id);
            Model.Guide = "每日一次 · 可送作物和料理 · 偏好 " + relationships.Inventory.Catalog.Find(rule.FavoriteId).DisplayName;
            for (int i = 0; i < Model.Slots.Length; i++)
            {
                var slot = bag.Slots[i]; var item = slot.Empty ? null : relationships.Inventory.Catalog.Find(slot.ItemId); var model = Model.Slots[i];
                model.Name = item?.DisplayName; model.Quantity = slot.Quantity; model.Icon = item?.Icon; model.IconColor = item?.IconColor ?? UnityEngine.Color.white; model.Selected = i == selected;
                Model.Eligible[i] = !slot.Empty && rule.Gain(bag.FindDefinition(slot.ItemId)) > 0;
            }
            var chosen = selected < 0 ? default : bag.Slots[selected]; int gain = rule.Gain(bag.FindDefinition(chosen.ItemId));
            Model.Detail = chosen.Empty ? "选择一个作物或料理" : relationships.Inventory.Catalog.Find(chosen.ItemId).DisplayName + " × " + chosen.Quantity;
            Model.Preview = gain == 0 ? "种子和材料不能作为礼物" : "赠送 1 个 · 好感度 +" + Math.Min(rule.Maximum - relationships.State.Points(id), gain) + (chosen.ItemId == rule.FavoriteId ? " · 偏好礼物" : "");
            var check = relationships.Check(selected); Model.CanGive = check == GiftResult.Success;
            Model.ButtonLabel = check == GiftResult.AlreadyGiven ? "今日已送礼" : check == GiftResult.AtMaximum ? "好感度已满" : "赠送一个";
            Model.Feedback = relationships.Feedback ?? (selected < 0 ? "选择礼物后确认赠送，进度仅在睡觉时保存" : Model.CanGive ? "礼物已准备好" : RelationshipManager.Explain(check)); view.Render(Model);
        }
        public void Dispose()
        {
            if (view == null) return;
            for (int i = 0; i < select.Length; i++) view.Slots[i].SelectButton.onClick.RemoveListener(select[i]);
            view.GiveButton.onClick.RemoveListener(give); view.CloseButton.onClick.RemoveListener(close); view.SaveButton.onClick.RemoveListener(save);
        }
    }
}
