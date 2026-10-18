using System.Collections;
using MagicIsland.Configuration;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagicIsland.Managers
{
    public sealed class ConfigurationManager : MonoBehaviour
    {
        public GameConfig Config { get; private set; }
        public ItemCatalog Items { get; private set; }
        public CropCatalog Crops { get; private set; }
        public WeatherConfig Weather { get; private set; }
        public SeasonConfig Seasons { get; private set; }
        public TaskCatalog Tasks { get; private set; }
        public OrderCatalog Orders { get; private set; }
        public GatheringCatalog Gathering { get; private set; }
        public ToolUpgradeCatalog ToolUpgrades { get; private set; }
        public StorageConfig Storage { get; private set; }
        public CookingCatalog Cooking { get; private set; }
        public NPCCatalog NPCs { get; private set; }
        public DialogueCatalog Dialogues { get; private set; }
        public RelationshipCatalog Relationships { get; private set; }
        public StoryEventCatalog Stories { get; private set; }
        public FishingCatalog Fishing { get; private set; }
        public NPCScheduleCatalog NPCSchedules { get; private set; }
        public CraftingCatalog Crafting { get; private set; }
        public HousingConfig Housing { get; private set; }
        public AnimalConfig Animals { get; private set; }
        public InventoryUpgradeConfig InventoryUpgrades { get; private set; }
        public ShippingConfig Shipping { get; private set; }
        public FertilizerConfig Fertilizer { get; private set; }
        public AudioCatalog Audio { get; private set; }
        public OrchardCatalog Orchards { get; private set; }
        public MagicConfig Magic { get; private set; }
        private AsyncOperationHandle<MagicConfig> magicHandle;
        private AsyncOperationHandle<OrchardCatalog> orchardHandle;
        private AsyncOperationHandle<AudioCatalog> audioHandle;
        private AsyncOperationHandle<FertilizerConfig> fertilizerHandle;
        private AsyncOperationHandle<ShippingConfig> shippingHandle;
        public string LastError { get; private set; }
        private AsyncOperationHandle<GameConfig> handle;
        private AsyncOperationHandle<ItemCatalog> itemHandle;
        private AsyncOperationHandle<CropCatalog> cropHandle;
        private AsyncOperationHandle<WeatherConfig> weatherHandle;
        private AsyncOperationHandle<SeasonConfig> seasonHandle;
        private AsyncOperationHandle<TaskCatalog> taskHandle;
        private AsyncOperationHandle<OrderCatalog> orderHandle;
        private AsyncOperationHandle<GatheringCatalog> gatheringHandle;
        private AsyncOperationHandle<ToolUpgradeCatalog> upgradeHandle;
        private AsyncOperationHandle<StorageConfig> storageHandle;
        private AsyncOperationHandle<CookingCatalog> cookingHandle;
        private AsyncOperationHandle<NPCCatalog> npcHandle;
        private AsyncOperationHandle<DialogueCatalog> dialogueHandle;
        private AsyncOperationHandle<RelationshipCatalog> relationshipHandle;
        private AsyncOperationHandle<StoryEventCatalog> storyHandle;
        private AsyncOperationHandle<FishingCatalog> fishingHandle;
        private AsyncOperationHandle<NPCScheduleCatalog> scheduleHandle;
        private AsyncOperationHandle<CraftingCatalog> craftingHandle;
        private AsyncOperationHandle<HousingConfig> housingHandle;
        private AsyncOperationHandle<AnimalConfig> animalHandle;
        private AsyncOperationHandle<InventoryUpgradeConfig> inventoryUpgradeHandle;

        public IEnumerator LoadConfiguration()
        {
            LastError = null;
            if (Config != null && Items != null && Crops != null && Weather != null && Seasons != null && Tasks != null && Orders != null && Gathering != null && ToolUpgrades != null && Storage != null && Cooking != null && NPCs != null && Dialogues != null && Relationships != null && Stories != null && Fishing != null && NPCSchedules != null && Crafting != null && Housing != null && Animals != null && InventoryUpgrades != null && Shipping != null && Fertilizer != null && Audio != null && Orchards != null) yield break;
            if (orchardHandle.IsValid()) Addressables.Release(orchardHandle); Orchards = null;
            if (audioHandle.IsValid()) Addressables.Release(audioHandle); Audio = null;
            if (fertilizerHandle.IsValid()) Addressables.Release(fertilizerHandle); Fertilizer = null;
            if (shippingHandle.IsValid()) Addressables.Release(shippingHandle); Shipping = null;
            if (inventoryUpgradeHandle.IsValid()) Addressables.Release(inventoryUpgradeHandle);
            if (housingHandle.IsValid()) Addressables.Release(housingHandle);
            if (craftingHandle.IsValid()) Addressables.Release(craftingHandle);
            if (scheduleHandle.IsValid()) Addressables.Release(scheduleHandle);
            if (fishingHandle.IsValid()) Addressables.Release(fishingHandle);
            if (storyHandle.IsValid()) Addressables.Release(storyHandle);
            if (relationshipHandle.IsValid()) Addressables.Release(relationshipHandle);
            if (dialogueHandle.IsValid()) Addressables.Release(dialogueHandle);
            if (npcHandle.IsValid()) Addressables.Release(npcHandle);
            if (cookingHandle.IsValid()) Addressables.Release(cookingHandle);
            if (storageHandle.IsValid()) Addressables.Release(storageHandle);
            if (upgradeHandle.IsValid()) Addressables.Release(upgradeHandle);
            if (gatheringHandle.IsValid()) Addressables.Release(gatheringHandle);
            if (orderHandle.IsValid()) Addressables.Release(orderHandle);
            if (animalHandle.IsValid()) Addressables.Release(animalHandle);
            if (inventoryUpgradeHandle.IsValid()) Addressables.Release(inventoryUpgradeHandle);
            if (housingHandle.IsValid()) Addressables.Release(housingHandle);
            if (handle.IsValid()) Addressables.Release(handle);
            if (itemHandle.IsValid()) Addressables.Release(itemHandle);
            if (cropHandle.IsValid()) Addressables.Release(cropHandle);
            if (weatherHandle.IsValid()) Addressables.Release(weatherHandle);
            if (seasonHandle.IsValid()) Addressables.Release(seasonHandle);
            if (taskHandle.IsValid()) Addressables.Release(taskHandle);
            Config = null;
            Items = null;
            Crops = null;
            Weather = null;
            Seasons = null;
            Tasks = null;
            Orders = null; Gathering = null; ToolUpgrades = null; Storage = null; Cooking = null; NPCs = null; Dialogues = null; Relationships = null; Stories = null; Fishing = null; NPCSchedules = null; Crafting = null; Housing = null; Animals = null; InventoryUpgrades = null;
            var initialize = Addressables.InitializeAsync(false);
            yield return initialize;
            bool ready = initialize.Status == AsyncOperationStatus.Succeeded;
            if (!ready) LastError = initialize.OperationException?.Message ?? "AA initialization failed";
            Addressables.Release(initialize);
            if (!ready) yield break;
            handle = Addressables.LoadAssetAsync<GameConfig>(GameConfig.Address);
            yield return handle;
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null || !handle.Result.IsValid)
            {
                LastError = handle.OperationException?.Message ?? "GameConfig has invalid values";
                if (handle.IsValid()) Addressables.Release(handle);
                yield break;
            }
            itemHandle = Addressables.LoadAssetAsync<ItemCatalog>(ItemCatalog.Address);
            yield return itemHandle;
            if (itemHandle.Status != AsyncOperationStatus.Succeeded || itemHandle.Result == null || !itemHandle.Result.Validate(handle.Result))
            {
                LastError = itemHandle.OperationException?.Message ?? "ItemCatalog has invalid definitions or farming item IDs";
                if (itemHandle.IsValid()) Addressables.Release(itemHandle);
                Addressables.Release(handle);
                yield break;
            }
            cropHandle = Addressables.LoadAssetAsync<CropCatalog>(CropCatalog.Address);
            yield return cropHandle;
            if (cropHandle.Status != AsyncOperationStatus.Succeeded || cropHandle.Result == null || !cropHandle.Result.Validate(handle.Result, itemHandle.Result))
            {
                LastError = cropHandle.OperationException?.Message ?? "CropCatalog has invalid crop or item mappings";
                if (cropHandle.IsValid()) Addressables.Release(cropHandle);
                Addressables.Release(itemHandle); Addressables.Release(handle);
                yield break;
            }
            weatherHandle = Addressables.LoadAssetAsync<WeatherConfig>(WeatherConfig.Address);
            yield return weatherHandle;
            if (weatherHandle.Status != AsyncOperationStatus.Succeeded || weatherHandle.Result == null || !weatherHandle.Result.IsValid)
            {
                LastError = weatherHandle.OperationException?.Message ?? "WeatherConfig has invalid values";
                if (weatherHandle.IsValid()) Addressables.Release(weatherHandle);
                Addressables.Release(cropHandle); Addressables.Release(itemHandle); Addressables.Release(handle);
                yield break;
            }
            seasonHandle = Addressables.LoadAssetAsync<SeasonConfig>(SeasonConfig.Address);
            yield return seasonHandle;
            if (seasonHandle.Status != AsyncOperationStatus.Succeeded || seasonHandle.Result == null || !seasonHandle.Result.IsValid)
            {
                LastError = seasonHandle.OperationException?.Message ?? "SeasonConfig has invalid values";
                if (seasonHandle.IsValid()) Addressables.Release(seasonHandle);
                Addressables.Release(weatherHandle); Addressables.Release(cropHandle); Addressables.Release(itemHandle); Addressables.Release(handle);
                yield break;
            }
            taskHandle = Addressables.LoadAssetAsync<TaskCatalog>(TaskCatalog.Address);
            yield return taskHandle;
            if (taskHandle.Status != AsyncOperationStatus.Succeeded || taskHandle.Result == null || !taskHandle.Result.IsValid)
            {
                LastError = taskHandle.OperationException?.Message ?? "TaskCatalog has invalid values";
                if (taskHandle.IsValid()) Addressables.Release(taskHandle);
                Addressables.Release(seasonHandle); Addressables.Release(weatherHandle); Addressables.Release(cropHandle); Addressables.Release(itemHandle); Addressables.Release(handle);
                yield break;
            }
            orderHandle = Addressables.LoadAssetAsync<OrderCatalog>(OrderCatalog.Address);
            yield return orderHandle;
            if (orderHandle.Status != AsyncOperationStatus.Succeeded || orderHandle.Result == null || !orderHandle.Result.Validate(cropHandle.Result, itemHandle.Result))
            {
                LastError = orderHandle.OperationException?.Message ?? "OrderCatalog has invalid values";
                if (orderHandle.IsValid()) Addressables.Release(orderHandle);
                Addressables.Release(taskHandle); Addressables.Release(seasonHandle); Addressables.Release(weatherHandle);
                Addressables.Release(cropHandle); Addressables.Release(itemHandle); Addressables.Release(handle);
                yield break;
            }
            gatheringHandle = Addressables.LoadAssetAsync<GatheringCatalog>(GatheringCatalog.Address);
            yield return gatheringHandle;
            if (gatheringHandle.Status != AsyncOperationStatus.Succeeded || gatheringHandle.Result == null || !gatheringHandle.Result.Validate(itemHandle.Result, handle.Result))
            {
                LastError = gatheringHandle.OperationException?.Message ?? "GatheringCatalog has invalid values";
                if (gatheringHandle.IsValid()) Addressables.Release(gatheringHandle);
                Addressables.Release(orderHandle); Addressables.Release(taskHandle); Addressables.Release(seasonHandle); Addressables.Release(weatherHandle);
                Addressables.Release(cropHandle); Addressables.Release(itemHandle); Addressables.Release(handle); yield break;
            }
            upgradeHandle = Addressables.LoadAssetAsync<ToolUpgradeCatalog>(ToolUpgradeCatalog.Address);
            yield return upgradeHandle;
            if (upgradeHandle.Status != AsyncOperationStatus.Succeeded || upgradeHandle.Result == null || !upgradeHandle.Result.Validate(itemHandle.Result))
            {
                LastError = upgradeHandle.OperationException?.Message ?? "ToolUpgradeCatalog has invalid values";
                if (upgradeHandle.IsValid()) Addressables.Release(upgradeHandle);
                Addressables.Release(gatheringHandle); Addressables.Release(orderHandle); Addressables.Release(taskHandle);
                Addressables.Release(seasonHandle); Addressables.Release(weatherHandle); Addressables.Release(cropHandle);
                Addressables.Release(itemHandle); Addressables.Release(handle); yield break;
            }
            storageHandle = Addressables.LoadAssetAsync<StorageConfig>(StorageConfig.Address);
            yield return storageHandle;
            if (storageHandle.Status != AsyncOperationStatus.Succeeded || storageHandle.Result == null || !storageHandle.Result.IsValid)
            {
                LastError = storageHandle.OperationException?.Message ?? "StorageConfig has invalid values";
                if (storageHandle.IsValid()) Addressables.Release(storageHandle);
                Addressables.Release(upgradeHandle); Addressables.Release(gatheringHandle); Addressables.Release(orderHandle); Addressables.Release(taskHandle);
                Addressables.Release(seasonHandle); Addressables.Release(weatherHandle); Addressables.Release(cropHandle);
                Addressables.Release(itemHandle); Addressables.Release(handle); yield break;
            }
            cookingHandle = Addressables.LoadAssetAsync<CookingCatalog>(CookingCatalog.Address);
            yield return cookingHandle;
            if (cookingHandle.Status != AsyncOperationStatus.Succeeded || cookingHandle.Result == null || !cookingHandle.Result.Validate(itemHandle.Result))
            {
                LastError = cookingHandle.OperationException?.Message ?? "CookingCatalog has invalid values";
                if (cookingHandle.IsValid()) Addressables.Release(cookingHandle);
                Addressables.Release(storageHandle); Addressables.Release(upgradeHandle); Addressables.Release(gatheringHandle); Addressables.Release(orderHandle); Addressables.Release(taskHandle);
                Addressables.Release(seasonHandle); Addressables.Release(weatherHandle); Addressables.Release(cropHandle); Addressables.Release(itemHandle); Addressables.Release(handle); yield break;
            }
            npcHandle = Addressables.LoadAssetAsync<NPCCatalog>(NPCCatalog.Address);
            yield return npcHandle;
            if (npcHandle.Status != AsyncOperationStatus.Succeeded || npcHandle.Result == null || !npcHandle.Result.Validate(handle.Result))
            {
                LastError = npcHandle.OperationException?.Message ?? "NPCCatalog has invalid values";
                ReleaseNPCConfiguration(); yield break;
            }
            dialogueHandle = Addressables.LoadAssetAsync<DialogueCatalog>(DialogueCatalog.Address);
            yield return dialogueHandle;
            if (dialogueHandle.Status != AsyncOperationStatus.Succeeded || dialogueHandle.Result == null || !dialogueHandle.Result.Validate(npcHandle.Result))
            {
                LastError = dialogueHandle.OperationException?.Message ?? "DialogueCatalog has invalid values";
                ReleaseNPCConfiguration(); yield break;
            }
            relationshipHandle = Addressables.LoadAssetAsync<RelationshipCatalog>(RelationshipCatalog.Address);
            yield return relationshipHandle;
            if (relationshipHandle.Status != AsyncOperationStatus.Succeeded || relationshipHandle.Result == null || !relationshipHandle.Result.Validate(npcHandle.Result, itemHandle.Result))
            {
                LastError = relationshipHandle.OperationException?.Message ?? "RelationshipCatalog has invalid values";
                if (relationshipHandle.IsValid()) Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            storyHandle = Addressables.LoadAssetAsync<StoryEventCatalog>(StoryEventCatalog.Address);
            yield return storyHandle;
            if (storyHandle.Status != AsyncOperationStatus.Succeeded || storyHandle.Result == null || !storyHandle.Result.Validate(npcHandle.Result, relationshipHandle.Result, cookingHandle.Result))
            {
                LastError = storyHandle.OperationException?.Message ?? "StoryEventCatalog has invalid events or recipe unlocks";
                if (storyHandle.IsValid()) Addressables.Release(storyHandle);
                if (relationshipHandle.IsValid()) Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            fishingHandle = Addressables.LoadAssetAsync<FishingCatalog>(FishingCatalog.Address);
            yield return fishingHandle;
            if (fishingHandle.Status != AsyncOperationStatus.Succeeded || fishingHandle.Result == null || !fishingHandle.Result.Validate(itemHandle.Result, handle.Result))
            {
                LastError = fishingHandle.OperationException?.Message ?? "FishingCatalog has invalid fish or fishing values";
                if (fishingHandle.IsValid()) Addressables.Release(fishingHandle);
                Addressables.Release(storyHandle); Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            scheduleHandle = Addressables.LoadAssetAsync<NPCScheduleCatalog>(NPCScheduleCatalog.Address);
            yield return scheduleHandle;
            if (scheduleHandle.Status != AsyncOperationStatus.Succeeded || scheduleHandle.Result == null || !scheduleHandle.Result.Validate(handle.Result, npcHandle.Result, fishingHandle.Result))
            {
                LastError = scheduleHandle.OperationException?.Message ?? "NPCScheduleCatalog has invalid routes or timing";
                if (scheduleHandle.IsValid()) Addressables.Release(scheduleHandle);
                Addressables.Release(fishingHandle); Addressables.Release(storyHandle); Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            craftingHandle = Addressables.LoadAssetAsync<CraftingCatalog>(CraftingCatalog.Address);
            yield return craftingHandle;
            if (craftingHandle.Status != AsyncOperationStatus.Succeeded || craftingHandle.Result == null || !craftingHandle.Result.Validate(itemHandle.Result))
            {
                LastError = craftingHandle.OperationException?.Message ?? "CraftingCatalog has invalid recipes or placeables";
                if (craftingHandle.IsValid()) Addressables.Release(craftingHandle);
                Addressables.Release(scheduleHandle); Addressables.Release(fishingHandle); Addressables.Release(storyHandle); Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            housingHandle = Addressables.LoadAssetAsync<HousingConfig>(HousingConfig.Address);
            yield return housingHandle;
            if (housingHandle.Status != AsyncOperationStatus.Succeeded || housingHandle.Result == null || !housingHandle.Result.Validate(itemHandle.Result))
            {
                LastError = housingHandle.OperationException?.Message ?? "HousingConfig has invalid area or AA definitions";
                if (inventoryUpgradeHandle.IsValid()) Addressables.Release(inventoryUpgradeHandle);
            if (housingHandle.IsValid()) Addressables.Release(housingHandle);
                Addressables.Release(craftingHandle); Addressables.Release(scheduleHandle); Addressables.Release(fishingHandle); Addressables.Release(storyHandle); Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            animalHandle = Addressables.LoadAssetAsync<AnimalConfig>(AnimalConfig.Address);
            yield return animalHandle;
            if (animalHandle.Status != AsyncOperationStatus.Succeeded || animalHandle.Result == null || !animalHandle.Result.Validate(itemHandle.Result, handle.Result))
            {
                LastError = animalHandle.OperationException?.Message ?? "AnimalConfig has invalid items, prices or location";
                if (animalHandle.IsValid()) Addressables.Release(animalHandle);
                Addressables.Release(housingHandle); Addressables.Release(craftingHandle); Addressables.Release(scheduleHandle); Addressables.Release(fishingHandle); Addressables.Release(storyHandle); Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            inventoryUpgradeHandle = Addressables.LoadAssetAsync<InventoryUpgradeConfig>(InventoryUpgradeConfig.Address);
            yield return inventoryUpgradeHandle;
            if (inventoryUpgradeHandle.Status != AsyncOperationStatus.Succeeded || inventoryUpgradeHandle.Result == null || !inventoryUpgradeHandle.Result.IsValid)
            {
                LastError = inventoryUpgradeHandle.OperationException?.Message ?? "InventoryUpgradeConfig has invalid prices";
                if (inventoryUpgradeHandle.IsValid()) Addressables.Release(inventoryUpgradeHandle);
                Addressables.Release(animalHandle); Addressables.Release(housingHandle); Addressables.Release(craftingHandle); Addressables.Release(scheduleHandle); Addressables.Release(fishingHandle); Addressables.Release(storyHandle); Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            shippingHandle = Addressables.LoadAssetAsync<ShippingConfig>(ShippingConfig.Address); yield return shippingHandle;
            if (shippingHandle.Status != AsyncOperationStatus.Succeeded || shippingHandle.Result == null || !shippingHandle.Result.IsValid)
            {
                LastError = shippingHandle.OperationException?.Message ?? "ShippingConfig has invalid values";
                if (shippingHandle.IsValid()) Addressables.Release(shippingHandle);
                Addressables.Release(inventoryUpgradeHandle); Addressables.Release(animalHandle); Addressables.Release(housingHandle); Addressables.Release(craftingHandle);
                Addressables.Release(scheduleHandle); Addressables.Release(fishingHandle); Addressables.Release(storyHandle); Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            fertilizerHandle = Addressables.LoadAssetAsync<FertilizerConfig>(FertilizerConfig.Address); yield return fertilizerHandle;
            if (fertilizerHandle.Status != AsyncOperationStatus.Succeeded || fertilizerHandle.Result == null || !fertilizerHandle.Result.Validate(itemHandle.Result))
            {
                LastError = fertilizerHandle.OperationException?.Message ?? "FertilizerConfig has invalid rules";
                if (fertilizerHandle.IsValid()) Addressables.Release(fertilizerHandle); Addressables.Release(shippingHandle);
                Addressables.Release(inventoryUpgradeHandle); Addressables.Release(animalHandle); Addressables.Release(housingHandle); Addressables.Release(craftingHandle); Addressables.Release(scheduleHandle); Addressables.Release(fishingHandle); Addressables.Release(storyHandle); Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            audioHandle = Addressables.LoadAssetAsync<AudioCatalog>(AudioCatalog.Address); yield return audioHandle;
            if (audioHandle.Status != AsyncOperationStatus.Succeeded || audioHandle.Result == null || !audioHandle.Result.IsValid)
            {
                LastError = audioHandle.OperationException?.Message ?? "AudioCatalog has invalid cues or volumes";
                if (audioHandle.IsValid()) Addressables.Release(audioHandle); Addressables.Release(fertilizerHandle); Addressables.Release(shippingHandle);
                Addressables.Release(inventoryUpgradeHandle); Addressables.Release(animalHandle); Addressables.Release(housingHandle); Addressables.Release(craftingHandle); Addressables.Release(scheduleHandle); Addressables.Release(fishingHandle); Addressables.Release(storyHandle); Addressables.Release(relationshipHandle); ReleaseNPCConfiguration(); yield break;
            }
            orchardHandle = Addressables.LoadAssetAsync<OrchardCatalog>(OrchardCatalog.Address); yield return orchardHandle;
            if (orchardHandle.Status != AsyncOperationStatus.Succeeded || orchardHandle.Result == null || !orchardHandle.Result.Validate(itemHandle.Result, craftingHandle.Result))
            { LastError = "OrchardCatalog has invalid fruit trees"; ReleaseConfiguration(); yield break; }
            magicHandle = Addressables.LoadAssetAsync<MagicConfig>(MagicConfig.Address); yield return magicHandle;
            if (magicHandle.Status != AsyncOperationStatus.Succeeded || magicHandle.Result == null || !magicHandle.Result.Validate(itemHandle.Result, cropHandle.Result))
            { LastError = "MagicConfig has invalid crop or spell rules"; ReleaseConfiguration(); yield break; }
            Magic = magicHandle.Result;
            Orchards = orchardHandle.Result;
            Audio = audioHandle.Result;
            Fertilizer = fertilizerHandle.Result;
            Shipping = shippingHandle.Result;
            InventoryUpgrades = inventoryUpgradeHandle.Result;
            Animals = animalHandle.Result;
            Housing = housingHandle.Result;
            Crafting = craftingHandle.Result;
            NPCSchedules = scheduleHandle.Result;
            Fishing = fishingHandle.Result;
            Stories = storyHandle.Result;
            Relationships = relationshipHandle.Result;
            NPCs = npcHandle.Result; Dialogues = dialogueHandle.Result;
            Cooking = cookingHandle.Result;
            Storage = storageHandle.Result;
            ToolUpgrades = upgradeHandle.Result;
            Gathering = gatheringHandle.Result;
            Orders = orderHandle.Result;
            Tasks = taskHandle.Result;
            Seasons = seasonHandle.Result;
            Weather = weatherHandle.Result;
            Crops = cropHandle.Result;
            Config = handle.Result;
            Items = itemHandle.Result;
        }

        private void ReleaseNPCConfiguration()
        {
            if (orchardHandle.IsValid()) { if (orchardHandle.IsDone) Addressables.Release(orchardHandle); else orchardHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); }; } orchardHandle = default; Orchards = null;
            if (dialogueHandle.IsValid()) Addressables.Release(dialogueHandle);
            if (npcHandle.IsValid()) Addressables.Release(npcHandle);
            Addressables.Release(cookingHandle); Addressables.Release(storageHandle); Addressables.Release(upgradeHandle);
            Addressables.Release(gatheringHandle); Addressables.Release(orderHandle); Addressables.Release(taskHandle);
            Addressables.Release(seasonHandle); Addressables.Release(weatherHandle); Addressables.Release(cropHandle); Addressables.Release(itemHandle); Addressables.Release(handle);
        }
        private void OnDestroy() => ReleaseConfiguration();
        private void ReleaseConfiguration()
        {
            if (magicHandle.IsValid()) { if (magicHandle.IsDone) Addressables.Release(magicHandle); else magicHandle.Completed += h => { if (h.IsValid()) Addressables.Release(h); }; }
            magicHandle = default; Magic = null;
            if (orchardHandle.IsValid()) { if (orchardHandle.IsDone) Addressables.Release(orchardHandle); else orchardHandle.Completed += h => { if (h.IsValid()) Addressables.Release(h); }; }
            if (audioHandle.IsValid()) { if (audioHandle.IsDone) Addressables.Release(audioHandle); else audioHandle.Completed += h => { if (h.IsValid()) Addressables.Release(h); }; }
            if (fertilizerHandle.IsValid()) { if (fertilizerHandle.IsDone) Addressables.Release(fertilizerHandle); else fertilizerHandle.Completed += h => { if (h.IsValid()) Addressables.Release(h); }; }
            if (shippingHandle.IsValid()) { if (shippingHandle.IsDone) Addressables.Release(shippingHandle); else shippingHandle.Completed += h => { if (h.IsValid()) Addressables.Release(h); }; }
            if (inventoryUpgradeHandle.IsValid()) { if (inventoryUpgradeHandle.IsDone) Addressables.Release(inventoryUpgradeHandle); else inventoryUpgradeHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); }; }
            if (animalHandle.IsValid()) { if (animalHandle.IsDone) Addressables.Release(animalHandle); else animalHandle.Completed += h => { if (h.IsValid()) Addressables.Release(h); }; }
            if (housingHandle.IsValid())
            {
                if (housingHandle.IsDone) Addressables.Release(housingHandle);
                else housingHandle.Completed += h => { if (h.IsValid()) Addressables.Release(h); };
            }
            if (craftingHandle.IsValid())
            {
                if (craftingHandle.IsDone) Addressables.Release(craftingHandle);
                else craftingHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (scheduleHandle.IsValid())
            {
                if (scheduleHandle.IsDone) Addressables.Release(scheduleHandle);
                else scheduleHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (fishingHandle.IsValid())
            {
                if (fishingHandle.IsDone) Addressables.Release(fishingHandle);
                else fishingHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (storyHandle.IsValid())
            {
                if (storyHandle.IsDone) Addressables.Release(storyHandle);
                else storyHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (relationshipHandle.IsValid())
            {
                if (relationshipHandle.IsDone) Addressables.Release(relationshipHandle);
                else relationshipHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (dialogueHandle.IsValid())
            {
                if (dialogueHandle.IsDone) Addressables.Release(dialogueHandle);
                else dialogueHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (npcHandle.IsValid())
            {
                if (npcHandle.IsDone) Addressables.Release(npcHandle);
                else npcHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (cookingHandle.IsValid())
            {
                if (cookingHandle.IsDone) Addressables.Release(cookingHandle);
                else cookingHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (storageHandle.IsValid())
            {
                if (storageHandle.IsDone) Addressables.Release(storageHandle);
                else storageHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (upgradeHandle.IsValid())
            {
                if (upgradeHandle.IsDone) Addressables.Release(upgradeHandle);
                else upgradeHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (gatheringHandle.IsValid())
            {
                if (gatheringHandle.IsDone) Addressables.Release(gatheringHandle);
                else gatheringHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            if (orderHandle.IsValid())
            {
                if (orderHandle.IsDone) Addressables.Release(orderHandle);
                else orderHandle.Completed += operation => { if (operation.IsValid()) Addressables.Release(operation); };
            }
            if (taskHandle.IsValid())
            {
                if (taskHandle.IsDone) Addressables.Release(taskHandle);
                else taskHandle.Completed += operation => { if (operation.IsValid()) Addressables.Release(operation); };
            }
            if (seasonHandle.IsValid())
            {
                if (seasonHandle.IsDone) Addressables.Release(seasonHandle);
                else seasonHandle.Completed += operation => { if (operation.IsValid()) Addressables.Release(operation); };
            }
            if (weatherHandle.IsValid())
            {
                if (weatherHandle.IsDone) Addressables.Release(weatherHandle);
                else weatherHandle.Completed += operation => { if (operation.IsValid()) Addressables.Release(operation); };
            }
            if (cropHandle.IsValid())
            {
                if (cropHandle.IsDone) Addressables.Release(cropHandle);
                else cropHandle.Completed += operation => { if (operation.IsValid()) Addressables.Release(operation); };
            }
            if (handle.IsValid())
            {
                if (handle.IsDone) Addressables.Release(handle);
                else handle.Completed += operation => { if (operation.IsValid()) Addressables.Release(operation); };
            }
            if (itemHandle.IsValid())
            {
                if (itemHandle.IsDone) Addressables.Release(itemHandle);
                else itemHandle.Completed += operation => { if (operation.IsValid()) Addressables.Release(operation); };
            }
        }
    }
}
