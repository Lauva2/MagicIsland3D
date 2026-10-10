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

        public IEnumerator LoadConfiguration()
        {
            LastError = null;
            if (Config != null && Items != null && Crops != null && Weather != null && Seasons != null && Tasks != null && Orders != null && Gathering != null && ToolUpgrades != null && Storage != null) yield break;
            if (storageHandle.IsValid()) Addressables.Release(storageHandle);
            if (upgradeHandle.IsValid()) Addressables.Release(upgradeHandle);
            if (gatheringHandle.IsValid()) Addressables.Release(gatheringHandle);
            if (orderHandle.IsValid()) Addressables.Release(orderHandle);
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
            Orders = null; Gathering = null; ToolUpgrades = null; Storage = null;
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

        private void OnDestroy()
        {
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
