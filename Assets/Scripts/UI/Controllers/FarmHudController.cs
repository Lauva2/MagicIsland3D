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
        private readonly EconomyManager economy;
        private readonly TimeManager time;
        private readonly StaminaManager stamina;
        private readonly WeatherManager weather;
        private readonly UnityAction nextDay, returnMenu, inventory;
        private readonly UnityAction[] tools;

        public FarmHudController(PrototypeHud view, FarmManager farm, InteractionManager interaction, EconomyManager economy, TimeManager time, StaminaManager stamina, WeatherManager weather,
            Action advanceDay, Action returnToMenu, Action toggleInventory)
        {
            this.view = view;
            this.farm = farm;
            this.interaction = interaction;
            this.economy = economy;
            this.time = time;
            this.stamina = stamina;
            this.weather = weather;
            stamina.State.Changed += Tick;
            economy.State.Changed += Tick;
            farm.Inventory.State.Changed += Tick;
            farm.SeedChanged += Tick;
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
            Model.Date = farm.Calendar?.Date.DisplayName ?? $"第 {Model.Day} 天";
            Model.Weather = weather.DisplayName;
            int minute = time.Clock.DisplayMinute;
            Model.Time = $"{minute / 60:00}:{minute % 60:00}";
            Model.Seeds = farm.Inventory.GetCount(farm.SeedItemId);
            Model.Harvest = farm.TotalHarvest;
            Model.Wood = farm.Inventory.GetCount("wood"); Model.Stone = farm.Inventory.GetCount("stone");
            Model.SeedName = farm.Inventory.Catalog.Find(farm.SeedItemId).DisplayName;
            Model.Gold = economy.Gold;
            Model.Stamina = stamina.State.Current; Model.MaxStamina = stamina.State.Maximum;
            Model.Tool = interaction.Controller.Tool;
            Model.Hint = interaction.Controller.Hint;
            Model.Feedback = interaction.Controller.Feedback;
            view.Render(Model);
        }
        public void Dispose()
        {
            farm.SeedChanged -= Tick;
            if (view == null) return;
            if (stamina.State != null) stamina.State.Changed -= Tick;
            if (economy.State != null) economy.State.Changed -= Tick;
            if (farm.Inventory != null && farm.Inventory.State != null) farm.Inventory.State.Changed -= Tick;
            view.NextDayButton.onClick.RemoveListener(nextDay);
            view.ReturnMenuButton.onClick.RemoveListener(returnMenu);
            view.InventoryButton.onClick.RemoveListener(inventory);
            for (int i = 0; i < tools.Length; i++) view.ToolButtons[i].onClick.RemoveListener(tools[i]);
        }
    }
}
