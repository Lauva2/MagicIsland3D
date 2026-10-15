using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Animals;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class AnimalManager : MonoBehaviour
    {
        public AnimalState State { get; private set; }
        public AnimalConfig Config { get; private set; }
        public CoopActor Coop { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public EconomyManager Economy { get; private set; }
        public string LastError { get; private set; }
        public string Feedback { get; private set; }
        public IReadOnlyList<ChickenActor> Chickens => chickens;
        private readonly List<ChickenActor> chickens = new List<ChickenActor>();
        private AsyncOperationHandle<GameObject> coopAsset, chickenAsset;
        private Transform world;
        private int generation;
        private float animationTime;
        public void Initialize(AnimalConfig config, InventoryManager inventory, EconomyManager economy, int day, Transform world, SavedAnimals saved = null)
        { Clear(); Config = config; Inventory = inventory; Economy = economy; this.world = world; State = new AnimalState(config.Definition(), day, saved?.QualitySeed ?? Random.Range(1, int.MaxValue)); if (saved != null) State.Restore(saved); }
        public IEnumerator LoadWorld()
        {
            LastError = null; int current = generation;
            coopAsset = Addressables.LoadAssetAsync<GameObject>(Config.CoopAddress); yield return coopAsset; if (current != generation) yield break;
            chickenAsset = Addressables.LoadAssetAsync<GameObject>(Config.ChickenAddress); yield return chickenAsset; if (current != generation) yield break;
            if (!ValidAsset(coopAsset) || !ValidAsset(chickenAsset) || chickenAsset.Result.GetComponent<ChickenActor>() == null || chickenAsset.Result.GetComponent<ChickenActor>().HeartRoot == null
                || chickenAsset.Result.GetComponent<ChickenActor>().HeartRoot.GetComponent<Renderer>() == null)
            { LastError = "鸡舍或鸡模型 AA 加载失败，请检查配置"; yield break; }
            var actor = coopAsset.Result.GetComponent<CoopActor>();
            if (actor == null || actor.ChickenSpots == null || actor.ChickenSpots.Length != 2 || actor.ChickenSpots.Any(t => t == null) || actor.EggsMarker == null)
            { LastError = "鸡舍模型缺少鸡的位置和产物标记"; yield break; }
            if (State.Built && !SiteClear(true)) { LastError = "已保存的鸡舍与设施或装饰冲突，请检查预留地"; yield break; }
            SyncWorld();
        }
        private static bool ValidAsset(AsyncOperationHandle<GameObject> h) => h.Status == AsyncOperationStatus.Succeeded && h.Result != null && h.Result.GetComponentsInChildren<Renderer>(true).Length > 0;
        public bool SiteClear(bool restoring = false)
        {
            Physics.SyncTransforms(); var point = world.TransformPoint(Config.Position + Vector3.up * .8f);
            foreach (var c in Physics.OverlapBox(point, new Vector3(1.05f, .78f, 1.15f), world.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.bounds.max.y <= world.position.y + .05f || (restoring && (c is CharacterController || c.GetComponentInParent<NPCActor>() != null))) continue;
                if (Coop != null && c.transform.IsChildOf(Coop.transform)) continue;
                return false;
            }
            return true;
        }
        public bool ReservesTile(int x, int z) => Config != null && x + .5f > Config.Position.x - 1.05f && x - .5f < Config.Position.x + 1.05f && z + .5f > Config.Position.z - 1.15f && z - .5f < Config.Position.z + 1.15f;
        public bool InRange(Vector3 player, float reach) => Coop != null && Vector3.ProjectOnPlane(Coop.transform.position - player, Vector3.up).sqrMagnitude <= reach * reach;
        public bool Owns(CoopActor actor) => actor != null && actor == Coop;
        public bool Build()
        {
            if (!State.Built && !SiteClear()) { Feedback = "鸡舍预留地被设施、装饰或角色挡住，请先整理"; return false; }
            var result = State.Build(Inventory.State, Economy.State, false); if (result != AnimalResult.Success) { Feedback = Explain(result); return false; }
            SyncWorld(); Inventory.State.NotifyChanged(); Economy.State.NotifyChanged(); Feedback = "鸡舍已建成，可到商店购买鸡和饲料"; return true;
        }
        public bool BuyChicken() { var result = State.BuyChicken(Economy.State, false); if (result != AnimalResult.Success) { Feedback = Explain(result); return false; } SyncWorld(); Economy.State.NotifyChanged(); Feedback = "已购买一只鸡，送到鸡舍了"; return true; }
        public bool BuyFeed() => Finish(State.BuyFeed(Inventory.State, Economy.State), "已购买一份饲料");
        public bool SellEgg() => Finish(State.Sell(Inventory.State, Economy.State), "已出售一枚鸡蛋");
        public bool SellQualityEgg() => Finish(State.Sell(Inventory.State, Economy.State, quality: true), "已出售一枚优质鸡蛋");
        public bool Feed() { bool ok = Finish(State.Feed(Inventory.State, false), "已喂食，明天可以收蛋"); SyncWorld(); if (ok) { Inventory.State.NotifyChanged(); State.NotifyChanged(); } return ok; }
        public bool Pet(int id)
        {
            var before = State.Chicken(id); var result = State.Pet(id, false);
            if (result != AnimalResult.Success) { Feedback = Explain(result); return false; }
            SyncWorld(); chickens.First(c => c.Id == id).ShowHeart();
            int gain = State.Chicken(id).Affinity - before.Affinity;
            Feedback = Config.ChickenName(id) + "很开心 · " + (gain > 0 ? "亲密度 +" + gain : "亲密度已满");
            State.NotifyChanged(); return true;
        }
        public bool Collect() { bool ok = Finish(State.Collect(Inventory.State, false), "鸡蛋已收进背包"); SyncWorld(); if (ok) Inventory.State.NotifyChanged(); return ok; }
        private bool Finish(AnimalResult result, string success) { Feedback = result == AnimalResult.Success ? success : Explain(result); return result == AnimalResult.Success; }
        public void NextDay(int day) { State.NextDay(day); SyncWorld(); }
        public void Tick(float dt, bool paused)
        { if (Coop == null) return; foreach (var c in chickens) c.TickFeedback(dt); if (paused) return; animationTime += dt; foreach (var c in chickens) c.Tick(animationTime); }
        private void SyncWorld()
        {
            if (!State.Built) return;
            if (Coop == null) { var go = Instantiate(coopAsset.Result, world, false); go.transform.localPosition = Config.Position; go.SetActive(true); Coop = go.GetComponent<CoopActor>(); }
            foreach (var record in State.Capture().Chickens) if (chickens.All(c => c.Id != record.Id))
            { var go = Instantiate(chickenAsset.Result, Coop.ChickenSpots[record.Id - 1], false); go.SetActive(true); var chicken = go.GetComponent<ChickenActor>(); chicken.Initialize(record.Id); chickens.Add(chicken); }
            Coop.EggsMarker.SetActive(State.TotalEggs > 0);
        }
        public void SetSaveFailed() => Feedback = "养殖操作已保留，保存失败，请重试保存进度";
        public void SetSaved() => Feedback = "养殖进度已保存";
        public static string Explain(AnimalResult r) => r == AnimalResult.Success ? "可以操作" : r == AnimalResult.AlreadyBuilt ? "鸡舍已建成" : r == AnimalResult.NeedsCoop ? "请先在办事处建造鸡舍"
            : r == AnimalResult.InsufficientMaterials ? "背包木材或石料不足" : r == AnimalResult.InsufficientGold ? "金币不足" : r == AnimalResult.ChickenLimit ? "鸡舍最多饲养两只鸡"
            : r == AnimalResult.NoChickens ? "鸡舍还没有鸡，请到商店购买" : r == AnimalResult.AlreadyFed ? "所有鸡今天都已喂过" : r == AnimalResult.MissingFeed ? "饲料不足，请到商店购买"
            : r == AnimalResult.EggsFull ? "待收鸡蛋已满，请先收取" : r == AnimalResult.NoEggs ? "没有可收取或出售的鸡蛋" : r == AnimalResult.BagFull ? "背包空间不足，鸡蛋和饲料保持原状" : r == AnimalResult.GoldLimit ? "金币已达上限" : r == AnimalResult.InvalidDay ? "当前日期无法继续喂食"
            : r == AnimalResult.AlreadyPetted ? "这只鸡今天已经摸摸过了，明天再来" : r == AnimalResult.InvalidChicken ? "这只鸡还没有入住" : "正在处理，请稍候";
        public void Clear()
        { generation++; if (Coop != null) Destroy(Coop.gameObject); Coop = null; chickens.Clear(); Release(coopAsset); Release(chickenAsset); coopAsset = chickenAsset = default; State = null; Config = null; Inventory = null; Economy = null; world = null; LastError = Feedback = null; animationTime = 0; }
        private static void Release(AsyncOperationHandle<GameObject> h) { if (!h.IsValid()) return; if (h.IsDone) Addressables.Release(h); else h.Completed += value => { if (value.IsValid()) Addressables.Release(value); }; }
        private void OnDestroy() => Clear();
    }
}
