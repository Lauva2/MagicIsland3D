using System;
using MagicIsland.Managers;
using MagicIsland.Orders;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class OrderController : IUIController
    {
        private readonly OrderView view;
        private readonly OrderManager orders;
        private readonly UnityAction deliver, close;
        public OrderModel Model { get; } = new OrderModel();
        public OrderController(OrderView view, OrderManager orders, Action deliverOrder, Action closeOrder)
        {
            this.view = view; this.orders = orders;
            deliver = () => deliverOrder(); close = () => closeOrder();
            view.DeliverButton.onClick.AddListener(deliver); view.CloseButton.onClick.AddListener(close); Tick();
        }
        public void Tick()
        {
            if (view == null || orders.State == null) return;
            var state = orders.State; var item = orders.Inventory.Catalog.Find(state.ItemId);
            Model.Date = orders.Calendar.Date.DisplayName + " · 今日收购";
            Model.Request = item.DisplayName + " × " + state.Quantity;
            Model.Stock = "背包持有 " + orders.Inventory.GetCount(state.ItemId) + " / " + state.Quantity;
            Model.Reward = "交付奖励 " + state.Reward + " 金币";
            Model.Status = state.Fulfilled ? "今日订单已完成 · 明天发布新订单" : "有效至今天结束 · 睡觉后刷新";
            Model.ButtonLabel = state.Fulfilled ? "已交付" : "交付全部作物";
            Model.CanDeliver = orders.Check() == DeliveryResult.Success;
            Model.Feedback = orders.Feedback ?? (state.Fulfilled ? "谢谢你为岛屿准备的收成" : OrderManager.Explain(orders.Check()));
            if (!state.Fulfilled && Model.CanDeliver && orders.Feedback == null) Model.Feedback = "作物已备齐，可以交付";
            Model.Icon = item.Icon; Model.IconColor = item.IconColor; view.Render(Model);
        }
        public void Dispose()
        {
            if (view == null) return;
            view.DeliverButton.onClick.RemoveListener(deliver); view.CloseButton.onClick.RemoveListener(close);
        }
    }
}
