using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class CookingManager : MonoBehaviour
    {
        public CookingState State { get; private set; }
        public CookingCatalog Catalog { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public StoryEventManager Stories { get; private set; }
        public CookingStation Station { get; private set; }
        public string Feedback { get; private set; }
        public string LastError { get; private set; }
        private AsyncOperationHandle<GameObject> handle;
        private int generation;
        public void Initialize(CookingCatalog catalog, InventoryManager inventory, StoryEventManager stories = null, AnimalConfig animals = null, CropCatalog crops = null)
        { Clear(); Catalog = catalog; Inventory = inventory; Stories = stories; State = new CookingState(catalog.Definitions(), id => Stories?.State?.Completed(id) ?? false, crops != null ? crops.Ingredients(animals) : animals == null ? null : new IngredientResolver(animals.EggItemId, animals.QualityEggItemId)); }
        public IEnumerator LoadStation(Transform world, int farmSize)
        {
            LastError = null; int current = generation; handle = Addressables.InstantiateAsync(Catalog.StationAddress, world); yield return handle;
            if (current != generation) yield break;
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null || (Station = handle.Result.GetComponent<CookingStation>()) == null || Station.GetComponentsInChildren<Collider>().Length == 0)
            { LastError = "Cooking station needs marker and colliders: " + Catalog.StationAddress; yield break; }
            Station.transform.localPosition = new Vector3(1.5f, 0, -farmSize * .6f - 2.2f);
        }
        public bool InRange(Vector3 position, float reach) => Station != null && Vector3.ProjectOnPlane(Station.transform.position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public CookingResult Check(int index) => State.Check(index, Inventory.State);
        public bool Cook(int index)
        {
            var result = State.Cook(index, Inventory.State); Feedback = result == CookingResult.Success ? "料理完成，食物已放入背包" : Explain(result); return result == CookingResult.Success;
        }
        public static string Explain(CookingResult result) => result == CookingResult.Locked ? "先完成居民事件解锁食谱" : result == CookingResult.InsufficientIngredients ? "背包食材不足" : result == CookingResult.BagFull ? "背包放不下料理，未扣除食材" : "暂时无法制作";
        public void SetSaveFailed() => Feedback = "料理已完成，保存失败，请点击保存进度重试";
        public void SetSaved() => Feedback = "料理进度已保存";
        public void Clear()
        {
            generation++; if (handle.IsValid()) { if (handle.IsDone) Release(handle); else handle.Completed += Release; }
            handle = default; State = null; Catalog = null; Inventory = null; Stories = null; Station = null; Feedback = null; LastError = null;
        }
        private static void Release(AsyncOperationHandle<GameObject> value)
        { if (!value.IsValid()) return; if (value.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(value); else Addressables.Release(value); }
        private void OnDestroy() => Clear();
    }
}
