using System;
using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
using MagicIsland.Saving;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class SaveManager : MonoBehaviour
    {
        public bool HasSave { get; private set; }
        public string LastError { get; private set; }
        public bool RecoveredBackup { get; private set; }
        public string SavePath => store?.FilePath;
        private SaveFileStore store;
        private GameConfig config;
        private ItemCatalog catalog;
        private CropCatalog crops;
        private TaskCatalog tasks;
        private OrderCatalog orders;
        private SeasonConfig seasons;
        private GatheringCatalog gathering;
        private ToolUpgradeCatalog toolUpgrades;
        private StorageConfig storage;
        private NPCCatalog npcs;
        private RelationshipCatalog relationships;
        private StoryEventCatalog stories;
        private NPCScheduleCatalog schedules;
        private FishingCatalog fishing;
        private CraftingCatalog crafting;

        public void Initialize(GameConfig gameConfig, ItemCatalog items, CropCatalog crops, string directory = null, TaskCatalog tasks = null, OrderCatalog orders = null, SeasonConfig seasons = null, GatheringCatalog gathering = null, ToolUpgradeCatalog toolUpgrades = null, StorageConfig storage = null, NPCCatalog npcs = null, RelationshipCatalog relationships = null, StoryEventCatalog stories = null, NPCScheduleCatalog schedules = null, FishingCatalog fishing = null, CraftingCatalog crafting = null)
        {
            config = gameConfig;
            catalog = items;
            this.crops = crops;
            this.tasks = tasks;
            this.orders = orders;
            this.seasons = seasons;
            this.gathering = gathering; this.toolUpgrades = toolUpgrades; this.storage = storage; this.npcs = npcs; this.relationships = relationships; this.stories = stories; this.schedules = schedules; this.fishing = fishing; this.crafting = crafting;
            store = new SaveFileStore(directory ?? Path.Combine(Application.persistentDataPath, "Saves"));
            RefreshAvailability();
        }

        public void RefreshAvailability() => HasSave = TryLoad(out _);
        public bool TryLoad(out SaveData data)
        {
            if (store == null) { data = null; return false; }
            HasSave = store.TryRead(json =>
            {
                SaveData parsed = JsonUtility.FromJson<SaveData>(json);
                if (parsed == null) throw new InvalidOperationException("Empty save");
                parsed.Upgrade(config.InitialGold, config.MaxStamina, crops.FindBySeed(config.SeedItemId).Id, config.SeedItemId, tasks?.Definitions(), orders?.Definitions(crops), seasons?.DaysPerSeason ?? 28, gathering?.Definitions(), toolUpgrades?.Definitions(), storage?.Slots ?? 0, npcs?.Ids(), relationships?.Definitions(npcs), stories?.Definitions(), schedules?.Definitions(config, npcs, fishing), crafting?.Placeables());
                parsed.Validate(config.FarmSize, config.CropGrowthDays, new InventoryState(config.InventorySlots, catalog.Definitions()), config.MaxStamina, crops.Definitions(), crops.FindBySeed(config.SeedItemId).Id, tasks?.Definitions(), orders?.Definitions(crops), seasons?.DaysPerSeason ?? 28, gathering?.Definitions(), toolUpgrades?.Definitions(), storage?.Slots ?? 0, npcs?.Ids(), relationships?.Definitions(npcs), stories?.Definitions(), schedules?.Definitions(config, npcs, fishing), crafting?.Placeables(), crafting?.Placeables(true));
                return parsed;
            }, out data, out string error, out bool recovered);
            LastError = error;
            RecoveredBackup = recovered;
            return HasSave;
        }

        public bool TrySave(FarmManager farm, InventoryManager inventory, PlayerManager players, EconomyManager economy, TimeManager time, StaminaManager stamina, WeatherManager weather, TaskManager taskManager = null, OrderManager orderManager = null, GatheringManager gatheringManager = null, ToolUpgradeManager toolUpgradeManager = null, StorageManager storageManager = null, NPCManager npcManager = null, RelationshipManager relationshipManager = null, StoryEventManager storyManager = null, NPCScheduleManager scheduleManager = null, PlacementManager placementManager = null, HousingManager housingManager = null, FurnitureManager furnitureManager = null)
        {
            try
            {
                Vector3 position = housingManager != null ? housingManager.SavedPosition : players.Player.transform.position;
                SaveData data = SaveData.Capture(farm.State, inventory.State, position.x, position.y, position.z, economy.Gold, time.Clock.Minute, stamina.State.Current, farm.SeedItemId,
                    weather.State.Seed, weather.State.Kind, taskManager?.State, orderManager?.State, gatheringManager?.State, toolUpgradeManager?.State, storageManager?.State, npcManager?.State, relationshipManager?.State, storyManager?.State, scheduleManager?.State, placementManager?.State, housingManager?.State?.Area ?? MagicIsland.Interaction.HousingState.Island, furnitureManager?.State, housingManager?.Upgrade);
                data.Validate(config.FarmSize, config.CropGrowthDays, new InventoryState(config.InventorySlots, catalog.Definitions()), config.MaxStamina, crops.Definitions(), crops.FindBySeed(config.SeedItemId).Id, tasks?.Definitions(), orders?.Definitions(crops), seasons?.DaysPerSeason ?? 28, gathering?.Definitions(), toolUpgrades?.Definitions(), storage?.Slots ?? 0, npcs?.Ids(), relationships?.Definitions(npcs), stories?.Definitions(), schedules?.Definitions(config, npcs, fishing), crafting?.Placeables(), crafting?.Placeables(true));
                store.Write(JsonUtility.ToJson(data, true));
                LastError = null;
                HasSave = true;
                RecoveredBackup = false;
                return true;
            }
            catch (Exception error) { LastError = error.Message; return false; }
        }
    }
}
