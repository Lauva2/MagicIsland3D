using MagicIsland.Configuration;
using MagicIsland.Economy;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using MagicIsland.Interaction;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class ShopManager : MonoBehaviour
    {
        public ItemConfig Seed { get; private set; }
        public ItemConfig Crop { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public EconomyManager Economy { get; private set; }
        public Transform Counter { get; private set; }
        public ShopCounter Actor { get; private set; }
        public string LastError { get; private set; }
        public bool OwnsCounter => counterHandle.IsValid();
        private AsyncOperationHandle<GameObject> counterHandle;
        private int counterGeneration;
        public IEnumerator LoadCounter(string address,Transform world,int farmSize)
        {
            ClearCounter();LastError=null;int current=counterGeneration;
            counterHandle=Addressables.InstantiateAsync(address,world);yield return counterHandle;
            if(current!=counterGeneration)yield break;
            Actor=counterHandle.Status==AsyncOperationStatus.Succeeded&&counterHandle.Result!=null?counterHandle.Result.GetComponent<ShopCounter>():null;
            if(Actor==null||world==null||Actor.transform.localScale!=Vector3.one||!System.Array.Exists(Actor.GetComponentsInChildren<Collider>(),c=>c.enabled&&!c.isTrigger))
            {LastError="商店 AA 预制体缺少柜台或实体碰撞";Release(counterHandle);counterHandle=default;Actor=null;yield break;}
            Counter=Actor.transform;Counter.localPosition=new Vector3(farmSize*.6f+1.5f,.6f,-4.6f);Physics.SyncTransforms();
        }
        private void ClearCounter()
        {
            counterGeneration++;Actor=null;Counter=null;
            if(counterHandle.IsValid()){if(counterHandle.IsDone)Release(counterHandle);else counterHandle.Completed+=Release;}counterHandle=default;
        }
        private static void Release(AsyncOperationHandle<GameObject> value)
        {if(!value.IsValid())return;if(value.Status==AsyncOperationStatus.Succeeded)Addressables.ReleaseInstance(value);else Addressables.Release(value);}
        private void OnDestroy()=>Clear();
        private ShopTransactions transactions;
        public IReadOnlyList<CropConfig> Crops => crops.Crops;
        public int SelectedIndex { get; private set; }
        public bool SelectedIsFish => Crop?.Type == MagicIsland.Inventory.ItemType.Fish;
        public bool SelectedOnlySells => Seed == null;
        public IReadOnlyList<ItemConfig> SaleItems => saleItems;
        public string Feedback { get; private set; }
        private readonly List<ItemConfig> saleItems = new List<ItemConfig>();
        private readonly List<ShopTransactions> fishOffers = new List<ShopTransactions>();
        public CalendarManager Calendar { get; private set; }
        private CropCatalog crops;
        private ItemCatalog items;
        private readonly Dictionary<string, ShopTransactions> offers = new Dictionary<string, ShopTransactions>();
        private bool trading;
        private readonly Dictionary<int, ItemConfig> extraSeeds = new Dictionary<int, ItemConfig>();
        public bool SelectedIsCrop => crops != null && SelectedIndex < crops.Crops.Count;
        public bool SelectedIsTree => extraSeeds.ContainsKey(SelectedIndex);
        public event System.Action<int> CropSold;
        public event System.Action TransactionSucceeded;
        public void Initialize(GameConfig config, ItemCatalog items, InventoryManager inventory, EconomyManager economy, Transform counter, CropCatalog crops, CalendarManager calendar = null, FishingCatalog fishing = null, OrchardCatalog orchards = null)
        {
            Clear();
            this.items = items; this.crops = crops;
            Calendar = calendar;
            Inventory = inventory; Economy = economy; Counter = counter;
            foreach (CropConfig crop in crops.Crops)
            {
                ItemConfig seed = items.Find(crop.SeedItemId), harvest = items.Find(crop.HarvestItemId);
                offers.Add(crop.Id, new ShopTransactions(inventory.State, economy.State, seed.Id, harvest.Id, seed.BuyPrice, harvest.SellPrice));
                saleItems.Add(harvest);
            }
            if (fishing != null) foreach (var fish in fishing.Fish) { var item = items.Find(fish.ItemId); saleItems.Add(item); fishOffers.Add(new ShopTransactions(inventory.State, economy.State, item.Id, item.SellPrice)); }
            foreach (var crop in crops.Crops) { var item = items.Find(crop.QualityHarvestItemId); if (item == null) continue; saleItems.Add(item); fishOffers.Add(new ShopTransactions(inventory.State, economy.State, item.Id, item.SellPrice)); }
            if (orchards != null) foreach (var tree in orchards.Types)
            {
                var seed = items.Find(tree.SaplingId); var fruit = items.Find(tree.FruitId); extraSeeds.Add(saleItems.Count, seed); saleItems.Add(fruit);
                fishOffers.Add(new ShopTransactions(inventory.State, economy.State, seed.Id, fruit.Id, seed.BuyPrice, fruit.SellPrice));
            }
            SelectCrop(crops.Crops.FindIndex(crop => crop.SeedItemId == config.SeedItemId));
            foreach(string id in BeachPickup.Items)
            {
                var item=items.Find(id);if(item==null||item.SellPrice<=0)continue;
                saleItems.Add(item);fishOffers.Add(new ShopTransactions(inventory.State,economy.State,item.Id,item.SellPrice));
            }
        }
        public bool InRange(Vector3 position, float reach) => Counter != null
            && Vector3.ProjectOnPlane(Counter.position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public bool SelectCrop(int index)
        {
            if (trading || crops == null || index < 0 || index >= crops.Crops.Count) return false;
            SelectedIndex = index; CropConfig crop = crops.Crops[index];
            Seed = items.Find(crop.SeedItemId); Crop = items.Find(crop.HarvestItemId); transactions = offers[crop.Id]; return true;
        }
        public TradeResult Check(bool buy, int quantity) => trading ? TradeResult.Busy : transactions?.Check(buy, quantity) ?? TradeResult.Busy;
        public bool SelectOffer(int index)
        {
            if (trading || crops == null || index < 0 || index >= saleItems.Count) return false;
            Feedback = null;
            if (index < crops.Crops.Count) return SelectCrop(index);
            SelectedIndex = index; Seed = extraSeeds.TryGetValue(index, out var seed) ? seed : null; Crop = saleItems[index]; transactions = fishOffers[index - crops.Crops.Count]; return true;
        }
        public TradeResult Trade(bool buy, int quantity)
        {
            if (trading || transactions == null) return TradeResult.Busy;
            trading = true;
            try
            {
                TradeResult result = transactions.Trade(buy, quantity);
                if (result == TradeResult.Success) TransactionSucceeded?.Invoke();
                if (!buy && Crop.Type == MagicIsland.Inventory.ItemType.Crop && result == TradeResult.Success) CropSold?.Invoke(quantity);
                return result;
            }
            finally { trading = false; }
        }
        public void SetSaveFailed() => Feedback = "交易已完成，保存失败，请点击保存进度重试";
        public void SetSaved() => Feedback = "交易进度已保存";
        public void Clear() { ClearCounter();LastError=null;offers.Clear(); extraSeeds.Clear(); saleItems.Clear(); fishOffers.Clear(); Feedback = null; trading = false; transactions = null; crops = null; items = null; Calendar = null; Seed = Crop = null; Inventory = null; Economy = null; }
    }
}
