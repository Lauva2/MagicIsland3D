using System;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using System.Collections.Generic;
using MagicIsland.WeatherSystem;
using MagicIsland.Tasks;
using MagicIsland.Orders;
using MagicIsland.Gathering;
using MagicIsland.Tools;
using System.Linq;
using MagicIsland.Interaction;

namespace MagicIsland.Saving
{
    [Serializable]
    public sealed class SavedSlot
    {
        public string ItemId;
        public int Quantity;
    }

    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 30;
        public MagicIsland.Magic.SavedMagic Magic = new MagicIsland.Magic.SavedMagic();
        public SavedBeachForage BeachForage;
        public SavedOrchardTree[] Orchard = Array.Empty<SavedOrchardTree>();
        public int CropQualitySeed = 1;
        public MagicIsland.Shipping.SavedShipping Shipping = new MagicIsland.Shipping.SavedShipping();
        public SavedInventoryUpgrade InventoryUpgrade = new SavedInventoryUpgrade();
        public MagicIsland.Reports.SavedDailyReport DailyReport = new MagicIsland.Reports.SavedDailyReport();
        public MagicIsland.Animals.SavedAnimals Animals = new MagicIsland.Animals.SavedAnimals();
        public SavedHouseUpgrade HouseUpgrade = new SavedHouseUpgrade();
        public SavedPlacedObject[] Furniture = Array.Empty<SavedPlacedObject>();
        public string PlayerArea = HousingState.Island;
        public SavedPlacedObject[] PlacedObjects = Array.Empty<SavedPlacedObject>();
        public SavedNPCSchedule[] NPCSchedules = Array.Empty<SavedNPCSchedule>();
        public SavedStoryEvent[] StoryEvents = Array.Empty<SavedStoryEvent>();
        public SavedRelationship[] Relationships = Array.Empty<SavedRelationship>();
        public SavedNPC[] NPCs = Array.Empty<SavedNPC>();
        public SavedSlot[] Storage = Array.Empty<SavedSlot>();
        public SavedToolUpgrade[] ToolUpgrades = Array.Empty<SavedToolUpgrade>();
        public SavedGatheringNode[] Gathering = Array.Empty<SavedGatheringNode>();
        public SavedOrder Order;
        public TaskProgress[] Tasks = Array.Empty<TaskProgress>();
        public int WeatherSeed = 1;
        public WeatherKind Weather;
        public string SelectedSeedItemId;
        public long Gold;
        public int Stamina = 100;
        public double ClockMinute = 360;
        public void Upgrade(int initialGold, int maxStamina = 100, string defaultCropId = "turnip", string defaultSeedId = "turnip_seed", IEnumerable<TaskDefinition> tasks = null, IEnumerable<OrderDefinition> orders = null, int daysPerSeason = 28, IEnumerable<GatheringDefinition> gathering = null, IEnumerable<ToolUpgradeDefinition> toolUpgrades = null, int storageSlots = 0, IEnumerable<string> npcIds = null, IEnumerable<RelationshipDefinition> relationships = null, IEnumerable<StoryEventDefinition> stories = null, IEnumerable<NPCScheduleDefinition> schedules = null, IEnumerable<PlaceableDefinition> placeables = null, BeachForageDefinition beachForage = null)
        {
            if (Version == 1) { Gold = initialGold; Version = 2; }
            if (Version == 2) { ClockMinute = 360; Version = 3; }
            if (Version == 3) { Stamina = maxStamina; Version = 4; }
            if (Version == 4)
            {
                SelectedSeedItemId = defaultSeedId;
                if (Cells != null) foreach (FarmCell cell in Cells)
                    if (cell != null) cell.CropId = cell.Planted ? defaultCropId : null;
                Version = 5;
            }
            if (Version == 5) { WeatherSeed = 1; Weather = WeatherKind.Sunny; Version = 6; }
            if (Version == 6) { Tasks = tasks == null ? Array.Empty<TaskProgress>() : new TaskState(tasks).Capture(); Version = 7; }
            if (Version == 7) { Order = orders == null ? null : new OrderState(orders, daysPerSeason, 1, Day).Capture(); Version = 8; }
            if (Version == 8) { Gathering = gathering == null ? Array.Empty<SavedGatheringNode>() : new GatheringState(gathering, Day).Capture(); Version = 9; }
            if (Version == 9) { ToolUpgrades = toolUpgrades == null ? Array.Empty<SavedToolUpgrade>() : new ToolUpgradeState(toolUpgrades).Capture(); Version = 10; }
            if (Version == 10) { Storage = Enumerable.Range(0, storageSlots).Select(i => new SavedSlot()).ToArray(); Version = 11; }
            if (Version == 11) { NPCs = npcIds == null ? Array.Empty<SavedNPC>() : new NPCState(npcIds).Capture(); Version = 12; }
            if (Version == 12)
            {
                Relationships = relationships == null ? Array.Empty<SavedRelationship>() : new RelationshipState(relationships,
                    new NPCState(relationships.Select(r => r.NpcId)), Day).Capture(); Version = 13;
            }
            if (Version == 13) { StoryEvents = stories?.Select(r => new SavedStoryEvent { Id = r.Id }).ToArray() ?? Array.Empty<SavedStoryEvent>(); Version = 14; }
            if (Version == 14) { NPCSchedules = schedules == null ? Array.Empty<SavedNPCSchedule>() : new NPCScheduleState(schedules, ClockMinute).Capture(); Version = 15; }
            if (Version == 15) { PlacedObjects = Array.Empty<SavedPlacedObject>(); Version = 16; }
            if (Version == 16) { PlayerArea = HousingState.Island; Version = 17; }
            if (Version == 17) { Furniture = Array.Empty<SavedPlacedObject>(); Version = 18; }
            if (Version == 18) { HouseUpgrade = new SavedHouseUpgrade(); Version = 19; }
            if (Version == 19) { Animals = new MagicIsland.Animals.SavedAnimals { Day = Day }; Version = 20; }
            if (Version == 20) { if (Order != null) Order.CoopOffersUnlocked = false; Version = 21; }
            if (Version == 21)
            {
                if (Animals?.Chickens != null) foreach (var chicken in Animals.Chickens)
                    if (chicken != null) { chicken.Affinity = 0; chicken.LastPetDay = 0; }
                Version = 22;
            }
            if (Version == 22)
            {
                if (Animals != null) { Animals.QualityEggs = 0; Animals.QualitySeed = 1; }
                Version = 23;
            }
            if (Version == 23)
            {
                DailyReport = new MagicIsland.Reports.SavedDailyReport { Day = Day };
                Version = 24;
            }
            if (Version == 24)
            {
                InventoryUpgrade = new SavedInventoryUpgrade { BaseSlots = Slots?.Length ?? 0 };
                Version = 25;
            }
            if (Version == 25)
            {
                Shipping = new MagicIsland.Shipping.SavedShipping();
                if (DailyReport?.Last != null) DailyReport.Last.Shipping = Array.Empty<MagicIsland.Shipping.ShippingSale>();
                Version = 26;
            }
            if (Version == 26)
            {
                CropQualitySeed = 1;
                if (Cells != null) foreach (var cell in Cells) if (cell != null) { cell.Fertilized = false; cell.Quality = CropQuality.Ordinary; cell.PlantingNumber = cell.Planted ? 1 : 0; }
                Version = 27;
            }
            if (Version == 27) { Orchard = Array.Empty<SavedOrchardTree>(); Version = 28; }
            if (Version == 28)
            {
                BeachForage = new BeachForageState(Math.Max(1, Day), Math.Max(1, CropQualitySeed), beachForage).Capture();
                Version = 29;
            }
            if (Version == 29) { Magic = new MagicIsland.Magic.SavedMagic(); Version = 30; }
        }
        public int Version;
        public string SavedUtc;
        public int FarmSize;
        public int Day;
        public FarmCell[] Cells;
        public SavedSlot[] Slots;
        public float PlayerX, PlayerY, PlayerZ;

        public void Validate(int farmSize, int growthDays, InventoryState inventory, int maxStamina = 100,
            IEnumerable<CropDefinition> crops = null, string defaultCropId = "turnip", IEnumerable<TaskDefinition> tasks = null, IEnumerable<OrderDefinition> orders = null, int daysPerSeason = 28, IEnumerable<GatheringDefinition> gathering = null, IEnumerable<ToolUpgradeDefinition> toolUpgrades = null, int storageSlots = 0, IEnumerable<string> npcIds = null, IEnumerable<RelationshipDefinition> relationships = null, IEnumerable<StoryEventDefinition> stories = null, IEnumerable<NPCScheduleDefinition> schedules = null, IEnumerable<PlaceableDefinition> placeables = null, IEnumerable<PlaceableDefinition> furniture = null, IEnumerable<KeyValuePair<string, int>> shippingPrices = null, IEnumerable<OrchardDefinition> orchards = null, BeachForageDefinition beachForage = null)
        {
            if (CropQualitySeed < 1) throw new InvalidOperationException("Invalid crop quality seed");
            if (Version != CurrentVersion) throw new InvalidOperationException("Unsupported save version");
            if (!MagicIsland.Magic.MagicState.Valid(Magic)) throw new InvalidOperationException("Invalid saved magic progress");
            if (BeachForage == null) throw new InvalidOperationException("Missing saved beach forage");
            new BeachForageState(Day, BeachForage.Seed, beachForage).Restore(BeachForage);
            var shippingDefinitions = (shippingPrices ?? Array.Empty<KeyValuePair<string, int>>()).ToArray();
            new MagicIsland.Shipping.ShippingState(inventory, inventory.ItemDefinitions, shippingDefinitions).Restore(Shipping, Day);
            if (DailyReport?.Pending == true && Shipping.Slots.Any(s => s.Quantity != 0)) throw new InvalidOperationException("Pending daily report requires an empty shipping box");
            if (DailyReport?.Last?.Shipping != null && DailyReport.Last.Shipping.Any(s => s != null && !shippingDefinitions.Any(p => p.Key == s.ItemId))) throw new InvalidOperationException("Invalid shipping report item");
            if (InventoryUpgradeState.SavedCapacity(InventoryUpgrade) != inventory.Capacity) throw new InvalidOperationException("Invalid saved backpack capacity");
            if (!MagicIsland.Reports.DailyReportState.Valid(DailyReport, Day, farmSize * farmSize)) throw new InvalidOperationException("Invalid saved daily report");
            if (!MagicIsland.Animals.AnimalState.Valid(Animals, Day)) throw new InvalidOperationException("Invalid saved animals");
            if (!HouseUpgradeState.Valid(HouseUpgrade, Day)) throw new InvalidOperationException("Invalid saved house upgrade");
            if (schedules != null) new NPCScheduleState(schedules, ClockMinute).Restore(NPCSchedules);
            PlacementState restoredPlacements = null;
            if (placeables != null) { restoredPlacements = new PlacementState(placeables, farmSize, schedules); restoredPlacements.Restore(PlacedObjects); }
            if (Orchard == null) throw new InvalidOperationException("Missing orchard records");
            if (orchards != null) { if (restoredPlacements == null) throw new InvalidOperationException("Missing orchard placement definitions"); new OrchardState(orchards, restoredPlacements, Day).Restore(Orchard); }
            else if (Orchard.Length != 0) throw new InvalidOperationException("Orchard definitions required");
            if (Furniture == null || Furniture.Length > PlacementState.Maximum) throw new InvalidOperationException("Invalid furniture records");
            if (furniture != null) new PlacementState(furniture, farmSize, indoor: true, houseLevel: HouseUpgrade.Level).Restore(Furniture);
            NPCState restoredNPCs = null;
            if (npcIds != null || relationships != null) { restoredNPCs = new NPCState(npcIds ?? relationships.Select(r => r.NpcId)); restoredNPCs.Restore(NPCs); }
            RelationshipState restoredRelationships = null;
            if (relationships != null) { restoredRelationships = new RelationshipState(relationships, restoredNPCs, Day); restoredRelationships.Restore(Relationships); }
            if (stories != null) new StoryEventState(stories, restoredRelationships).Restore(StoryEvents);
            if (storageSlots > 0) inventory.CreateEmpty(storageSlots).Restore(GetStorageSlots());
            if (tasks != null) new TaskState(tasks).Restore(Tasks);
            if (toolUpgrades != null) new ToolUpgradeState(toolUpgrades).Restore(ToolUpgrades);
            if (gathering != null) new GatheringState(gathering, Day).Restore(Gathering);
            if (orders != null)
            {
                if (Order == null) throw new InvalidOperationException("Missing saved order");
                if (Order.CoopOffersUnlocked && (!Animals.Built || Day == 1)) throw new InvalidOperationException("Order pool requires a previously built coop");
                new OrderState(orders, daysPerSeason, Order.Seed, Day).Restore(Order, Day);
            }
            if (WeatherSeed < 1 || !WeatherState.IsValid(Weather)) throw new InvalidOperationException("Invalid saved weather");
            if (double.IsNaN(ClockMinute) || double.IsInfinity(ClockMinute) || ClockMinute < 0 || ClockMinute > 1440)
                throw new InvalidOperationException("Invalid saved clock");
            if (maxStamina < 1 || Stamina < 0 || Stamina > maxStamina) throw new InvalidOperationException("Invalid saved stamina");
            if (Gold < 0) throw new InvalidOperationException("Invalid saved gold");
            if (FarmSize != farmSize || Day < 1 || Day == int.MaxValue || Cells == null || Cells.Length != farmSize * farmSize)
                throw new InvalidOperationException("Invalid saved farm dimensions or day");
            var definitions = new Dictionary<string, CropDefinition>();
            bool checkSelection = crops != null, selectionFound = false;
            if (crops == null) crops = new[] { new CropDefinition(defaultCropId, "turnip_seed", "turnip", growthDays) };
            foreach (CropDefinition crop in crops)
            { definitions.Add(crop.Id, crop); if (crop.SeedId == SelectedSeedItemId) selectionFound = true; }
            if (checkSelection && !selectionFound) throw new InvalidOperationException("Invalid selected seed");
            foreach (FarmCell cell in Cells)
            {
                string id = cell != null && cell.Planted ? cell.CropId : defaultCropId;
                CropDefinition crop = id != null && definitions.TryGetValue(id, out CropDefinition value) ? value : null;
                if (cell == null || crop == null || !FarmState.ValidQuality(cell, crop) || cell.RequiredGrowthDays != crop.GrowthDays || cell.Growth < 0 || cell.Growth > crop.GrowthDays
                    || (cell.Planted && !cell.Tilled) || (cell.Watered && !cell.Planted)
                    || (!cell.Planted && (cell.Growth != 0 || !string.IsNullOrEmpty(cell.CropId))))
                    throw new InvalidOperationException("Invalid saved farm cell");
            }
            if (!HousingState.ValidPosition(PlayerArea, PlayerX, PlayerY, PlayerZ, HouseUpgrade.Level))
                throw new InvalidOperationException("Invalid saved player area or position");
            if (Slots == null || Slots.Length != inventory.Capacity) throw new InvalidOperationException("Saved inventory capacity changed");
            var restored = new InventorySlot[Slots.Length];
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] == null) throw new InvalidOperationException("Missing saved slot");
                restored[i] = new InventorySlot(Slots[i].ItemId, Slots[i].Quantity);
            }
            // This inventory is temporary during validation; no live world state is modified.
            inventory.Restore(restored);
        }

        public static SaveData Capture(FarmState farm, InventoryState inventory, float x, float y, float z, long gold = 0, double clockMinute = 360, int stamina = 100, string selectedSeed = null,
            int weatherSeed = 1, WeatherKind weather = WeatherKind.Sunny, TaskState tasks = null, OrderState orders = null, GatheringState gathering = null, ToolUpgradeState toolUpgrades = null, StorageState storage = null, NPCState npcs = null, RelationshipState relationships = null, StoryEventState stories = null, NPCScheduleState schedules = null, PlacementState placements = null, string playerArea = HousingState.Island, PlacementState furniture = null, HouseUpgradeState houseUpgrade = null, MagicIsland.Animals.AnimalState animals = null, MagicIsland.Reports.DailyReportState dailyReport = null, InventoryUpgradeState inventoryUpgrade = null, MagicIsland.Shipping.ShippingState shipping = null, BeachForageState beachForage = null)
        {
            var data = new SaveData { CropQualitySeed = farm.QualitySeed, Shipping = shipping?.Capture() ?? new MagicIsland.Shipping.SavedShipping(), Version = CurrentVersion, InventoryUpgrade = inventoryUpgrade?.Capture() ?? new SavedInventoryUpgrade { BaseSlots = inventory.Capacity }, DailyReport = dailyReport?.Capture() ?? new MagicIsland.Reports.SavedDailyReport { Day = farm.Day }, Animals = animals?.Capture() ?? new MagicIsland.Animals.SavedAnimals { Day = farm.Day }, HouseUpgrade = houseUpgrade?.Capture() ?? new SavedHouseUpgrade(), PlayerArea = playerArea, Furniture = furniture?.Capture() ?? Array.Empty<SavedPlacedObject>(), PlacedObjects = placements?.Capture() ?? Array.Empty<SavedPlacedObject>(), NPCSchedules = schedules?.Capture() ?? Array.Empty<SavedNPCSchedule>(), StoryEvents = stories?.Capture() ?? Array.Empty<SavedStoryEvent>(), Relationships = relationships?.Capture() ?? Array.Empty<SavedRelationship>(), NPCs = npcs?.Capture() ?? Array.Empty<SavedNPC>(), Storage = storage?.Capture().Select(s => new SavedSlot { ItemId = s.ItemId, Quantity = s.Quantity }).ToArray() ?? Array.Empty<SavedSlot>(), ToolUpgrades = toolUpgrades?.Capture() ?? Array.Empty<SavedToolUpgrade>(), Gathering = gathering?.Capture() ?? Array.Empty<SavedGatheringNode>(), Order = orders?.Capture(), Tasks = tasks?.Capture() ?? Array.Empty<TaskProgress>(), WeatherSeed = weatherSeed, Weather = weather, Gold = gold, ClockMinute = clockMinute, Stamina = stamina, SelectedSeedItemId = selectedSeed, SavedUtc = DateTime.UtcNow.ToString("O"),
                FarmSize = farm.Size, Day = farm.Day, PlayerX = x, PlayerY = y, PlayerZ = z,
                Cells = new FarmCell[farm.Cells.Length], Slots = new SavedSlot[inventory.Capacity] };
            data.BeachForage = (beachForage ?? new BeachForageState(farm.Day, farm.QualitySeed)).Capture();
            for (int i = 0; i < data.Cells.Length; i++)
            {
                FarmCell cell = farm.Cells[i];
                data.Cells[i] = new FarmCell { Fertilized = cell.Fertilized, Quality = cell.Quality, PlantingNumber = cell.PlantingNumber, CropId = cell.CropId, Tilled = cell.Tilled, Planted = cell.Planted, Watered = cell.Watered,
                    Growth = cell.Growth, RequiredGrowthDays = cell.RequiredGrowthDays };
            }
            for (int i = 0; i < data.Slots.Length; i++)
                data.Slots[i] = new SavedSlot { ItemId = inventory.Slots[i].ItemId, Quantity = inventory.Slots[i].Quantity };
            return data;
        }

        public InventorySlot[] GetStorageSlots()
        {
            if (Storage == null || Storage.Any(s => s == null)) throw new InvalidOperationException("Missing storage slots");
            return Storage.Select(s => new InventorySlot(s.ItemId, s.Quantity)).ToArray();
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
