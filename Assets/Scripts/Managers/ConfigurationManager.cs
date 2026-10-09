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
        public string LastError { get; private set; }
        private AsyncOperationHandle<GameConfig> handle;
        private AsyncOperationHandle<ItemCatalog> itemHandle;

        public IEnumerator LoadConfiguration()
        {
            LastError = null;
            if (Config != null && Items != null) yield break;
            if (handle.IsValid()) Addressables.Release(handle);
            if (itemHandle.IsValid()) Addressables.Release(itemHandle);
            Config = null;
            Items = null;
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
            Config = handle.Result;
            Items = itemHandle.Result;
        }

        private void OnDestroy()
        {
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
