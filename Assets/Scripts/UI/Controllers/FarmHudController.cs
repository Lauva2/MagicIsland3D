using System;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.Farming;
using MagicIsland.UI.Models;
using UnityEngine.Events;

namespace MagicIsland.UI.Controllers
{
    public sealed class FarmHudController : IUIController
    {
        public FarmHudModel Model { get; } = new FarmHudModel();
        private readonly PrototypeHud view;
        private readonly FarmManager farm;
        private readonly InteractionManager interaction;
        private readonly UnityAction nextDay, returnMenu, inventory;
        private readonly UnityAction[] tools;

        public FarmHudController(PrototypeHud view, FarmManager farm, InteractionManager interaction,
            Action advanceDay, Action returnToMenu, Action toggleInventory)
        {
            this.view = view;
            this.farm = farm;
            this.interaction = interaction;
            nextDay = () => advanceDay();
            returnMenu = () => returnToMenu();
            view.NextDayButton.onClick.AddListener(nextDay);
            view.ReturnMenuButton.onClick.AddListener(returnMenu);
            inventory = () => toggleInventory();
            view.InventoryButton.onClick.AddListener(inventory);
            tools = new UnityAction[view.ToolButtons.Length];
            for (int i = 0; i < tools.Length; i++)
            {
                FarmTool tool = (FarmTool)i;
                tools[i] = () => interaction.SelectTool(tool);
                view.ToolButtons[i].onClick.AddListener(tools[i]);
            }
            Tick();
        }
        public void Tick()
        {
            if (view == null || farm.State == null || interaction.Controller == null) return;
            Model.Day = farm.State.Day;
            Model.Seeds = farm.Inventory.GetCount(farm.SeedItemId);
            Model.Harvest = farm.Inventory.GetCount(farm.CropItemId);
            Model.Tool = interaction.Controller.Tool;
            Model.Hint = interaction.Controller.Hint;
            Model.Feedback = interaction.Controller.Feedback;
            view.Render(Model);
        }
        public void Dispose()
        {
            if (view == null) return;
            view.NextDayButton.onClick.RemoveListener(nextDay);
            view.ReturnMenuButton.onClick.RemoveListener(returnMenu);
            view.InventoryButton.onClick.RemoveListener(inventory);
            for (int i = 0; i < tools.Length; i++) view.ToolButtons[i].onClick.RemoveListener(tools[i]);
        }
    }
}
