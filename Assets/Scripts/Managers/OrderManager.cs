using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.Orders;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagicIsland.Managers
{
    public sealed class OrderManager : MonoBehaviour
    {
        public OrderState State { get; private set; }
        public OrderBoard Board { get; private set; }
        public GameObject Hall { get; private set; }
        public string LastError { get; private set; }
        public string Feedback { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public EconomyManager Economy { get; private set; }
        public CalendarManager Calendar { get; private set; }
        private FarmManager farm;
        private OrderCatalog catalog;
        private AsyncOperationHandle<GameObject> hallHandle;
        private int loadGeneration;
        public void Initialize(OrderCatalog catalog, FarmManager farm, InventoryManager inventory, EconomyManager economy, CalendarManager calendar, SavedOrder saved = null, AnimalConfig animals = null)
        {
            Clear();
            this.catalog = catalog; this.farm = farm; Inventory = inventory; Economy = economy; Calendar = calendar;
            State = new OrderState(catalog.Definitions(farm.Crops), calendar.Config.DaysPerSeason,
                saved?.Seed ?? Random.Range(1, int.MaxValue), farm.State.Day, ingredients: farm.Crops.Ingredients(animals));
            if (saved != null) State.Restore(saved, farm.State.Day);
        }
        public IEnumerator LoadHall(Transform world, int farmSize)
        {
            int generation = loadGeneration; LastError = null;
            hallHandle = Addressables.InstantiateAsync(catalog.BuildingAddress, world);
            yield return hallHandle;
            if (generation != loadGeneration) yield break;
            if (hallHandle.Status != AsyncOperationStatus.Succeeded || hallHandle.Result == null)
            { LastError = "Cannot load order hall: " + catalog.BuildingAddress; yield break; }
            Hall = hallHandle.Result;
            var boards = Hall.GetComponentsInChildren<OrderBoard>(true);
            if (boards.Length != 1 || boards[0].GetComponentsInChildren<Collider>().Length == 0)
            { LastError = "Order hall needs one interactive noticeboard"; yield break; }
            Board = boards[0]; Hall.transform.localPosition = new Vector3(Mathf.Min(9.3f, farmSize * 0.6f + 2.7f), 0, 4.5f);
        }
        public bool InRange(Vector3 position, float reach) => Board != null
            && Vector3.ProjectOnPlane(Board.transform.position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public void NextDay(bool coopBuilt = false) { State.NextDay(farm.State.Day, coopBuilt); Feedback = null; }
        public DeliveryResult Check() => State.Check(Inventory.State, Economy.State);
        public bool Deliver()
        {
            DeliveryResult result = State.Deliver(Inventory.State, Economy.State);
            Feedback = Explain(result);
            return result == DeliveryResult.Success;
        }
        public static string Explain(DeliveryResult result) => result == DeliveryResult.Success ? "交付成功，奖励已到账"
            : result == DeliveryResult.InsufficientItems ? "物品数量不足，请先准备好全部物品"
            : result == DeliveryResult.AlreadyFulfilled ? "今日订单已交付，明天再来看看"
            : result == DeliveryResult.GoldLimit ? "金币已达上限，暂时无法交付" : "正在交付，请稍候";
        public void SetSaveFailed() => Feedback = "订单已交付，保存失败，请重试保存";
        public void Clear()
        {
            loadGeneration++;
            if (hallHandle.IsValid())
            {
                if (hallHandle.IsDone) ReleaseHall(hallHandle);
                else hallHandle.Completed += ReleaseHall;
            }
            hallHandle = default; Board = null; Hall = null; State = null; farm = null; catalog = null;
            Inventory = null; Economy = null; Calendar = null; Feedback = null;
        }
        private static void ReleaseHall(AsyncOperationHandle<GameObject> handle)
        { if (!handle.IsValid()) return; if (handle.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(handle); else Addressables.Release(handle); }
        private void OnDestroy() => Clear();
    }
}
