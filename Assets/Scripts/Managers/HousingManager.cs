using System;
using System.Collections;
using System.Collections.Generic;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class HousingManager : MonoBehaviour
    {
        public HousingState State { get; private set; }
        public HousingConfig Config { get; private set; }
        public HomePortal Entrance { get; private set; }
        public HomeInterior Interior { get; private set; }
        public HouseUpgradeState Upgrade { get; private set; }
        public string UpgradeFeedback { get; private set; }
        public bool Inside => State?.Inside ?? false;
        public string LastError { get; private set; }
        private AsyncOperationHandle<GameObject> exterior;
        private AsyncOperationHandle<GameObject> exteriorUpgradeAsset, interiorUpgradeAsset;
        private GameObject exteriorUpgrade, interiorUpgrade;
        private PlayerManager players;
        private Transform world;
        private Camera view;
        private Light sun;
        private int originalCameraMask, originalSunMask, generation;
        private Color originalBackground;
        private readonly Dictionary<GameObject, int> layers = new Dictionary<GameObject, int>();
        public void Initialize(HousingConfig config, PlayerManager players, Transform world, Light sun, SavedHouseUpgrade saved = null, int day = 1)
        {
            Clear(); Config = config; this.players = players; this.world = world; this.sun = sun;
            view = players.Player.ViewCamera; originalCameraMask = view.cullingMask; originalBackground = view.backgroundColor;
            originalSunMask = sun.cullingMask; sun.cullingMask &= ~(1 << config.InteriorLayer);
            view.cullingMask = originalCameraMask & ~(1 << config.InteriorLayer); State = new HousingState();
            Upgrade = new HouseUpgradeState(config.UpgradeDefinition()); if (saved != null) Upgrade.Restore(saved, day);
        }
        public IEnumerator LoadExterior(int farmSize)
        {
            LastError = null; int current = generation;
            exterior = Addressables.InstantiateAsync(Config.ExteriorAddress, world); yield return exterior;
            if (current != generation) yield break;
            if (exterior.Status != AsyncOperationStatus.Succeeded || exterior.Result == null
                || (Entrance = exterior.Result.GetComponent<HomePortal>()) == null || Entrance.IsExit || Entrance.ReachPoint == null
                || Entrance.GetComponentsInChildren<Collider>(true).Length == 0)
            { LastError = "小屋模型缺少门口和碰撞配置"; yield break; }
            Entrance.transform.localPosition = Config.ExteriorPosition(farmSize); Entrance.gameObject.SetActive(true); Physics.SyncTransforms();
            exteriorUpgradeAsset = Addressables.LoadAssetAsync<GameObject>(Config.ExteriorUpgradeAddress); yield return exteriorUpgradeAsset;
            if (current != generation) yield break;
            interiorUpgradeAsset = Addressables.LoadAssetAsync<GameObject>(Config.InteriorUpgradeAddress); yield return interiorUpgradeAsset;
            if (current != generation) yield break;
            if (!ValidExpansion(exteriorUpgradeAsset) || !ValidExpansion(interiorUpgradeAsset)) { LastError = "小屋扩建模型暂时无法加载，请检查 AA 配置"; yield break; }
            ApplyExpansion();
        }
        private static bool ValidExpansion(AsyncOperationHandle<GameObject> h) => h.Status == AsyncOperationStatus.Succeeded && h.Result != null
            && h.Result.GetComponentsInChildren<Renderer>(true).Length > 0 && h.Result.transform.localScale == Vector3.one;
        public bool NextDay(int day)
        {
            if (!Upgrade.Complete(day)) return false;
            ApplyExpansion(); UpgradeFeedback = "小屋扩建已完工，回家看看新增的空间吧"; return true;
        }
        public HouseUpgradeResult ApplyUpgrade(int day, InventoryManager inventory, EconomyManager economy)
        {
            var result = Upgrade.Request(day, inventory.State, economy.State);
            UpgradeFeedback = result == HouseUpgradeResult.Success ? "已申请扩建，材料和金币已支付" : ExplainUpgrade(result); return result;
        }
        public void SetUpgradeSaveFailed() => UpgradeFeedback = "小屋进度已保留，保存失败，请重试保存进度";
        public void SetUpgradeSaved() => UpgradeFeedback = "小屋进度已保存";
        public static string ExplainUpgrade(HouseUpgradeResult result) => result == HouseUpgradeResult.InsufficientMaterials ? "背包材料不足"
            : result == HouseUpgradeResult.InsufficientGold ? "金币不足" : result == HouseUpgradeResult.Building ? "正在施工，请等待完工"
            : result == HouseUpgradeResult.Completed ? "小屋已完成本次扩建" : result == HouseUpgradeResult.InvalidDay ? "当前日期无法申请施工" : "可以申请扩建";
        private void ApplyExpansion()
        {
            if (Upgrade.Level == 0) return;
            if (Entrance != null && exteriorUpgrade == null)
            {
                exteriorUpgrade = Instantiate(exteriorUpgradeAsset.Result, Entrance.transform, false); exteriorUpgrade.SetActive(true);
                if (Entrance.NameText != null) Entrance.NameText.text = "舒适小屋";
            }
            if (Interior == null || interiorUpgrade != null) return;
            interiorUpgrade = Instantiate(interiorUpgradeAsset.Result, Interior.transform, false); interiorUpgrade.SetActive(true);
            foreach (var wall in Interior.OriginalWalls) wall.SetActive(false);
            foreach (var window in Interior.Windows) window.localPosition += Vector3.forward * 2;
            Interior.Skirting.localPosition += new Vector3(2, 0, 2);
            var size = Interior.Skirting.localScale; size.x += 4; Interior.Skirting.localScale = size;
            foreach (var t in interiorUpgrade.GetComponentsInChildren<Transform>(true)) if (t.gameObject.layer != 2) t.gameObject.layer = Config.InteriorLayer;
            Physics.SyncTransforms();
        }
        public bool BindInterior(UnityEngine.SceneManagement.Scene scene)
        {
            LastError = null;
            foreach (var root in scene.GetRootGameObjects()) { Interior = root.GetComponentInChildren<HomeInterior>(true); if (Interior != null) break; }
            if (Interior == null || Interior.SpawnPoint == null || Interior.Bed == null || Interior.Exit == null || !Interior.Exit.IsExit
                || Interior.OriginalWalls == null || Interior.OriginalWalls.Length != 4 || Interior.OriginalWalls.Any(w => w == null)
                || Interior.Windows == null || Interior.Windows.Length != 4 || Interior.Windows.Any(w => w == null) || Interior.Skirting == null)
            { LastError = "小屋室内缺少床、出口或扩建引用"; return false; }
            Interior.transform.position = Config.InteriorOrigin;
            ApplyExpansion();
            foreach (var child in Interior.GetComponentsInChildren<Transform>(true)) if (child.gameObject.layer != 2) child.gameObject.layer = Config.InteriorLayer;
            foreach (var light in Interior.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << Config.InteriorLayer;
            Physics.SyncTransforms(); return true;
        }
        public bool InRange(HomePortal portal, float reach) => Owns(portal) && Vector3.ProjectOnPlane(portal.Position - players.Player.transform.position, Vector3.up).sqrMagnitude <= reach * reach;
        public bool Owns(HomePortal portal) => portal != null && (Inside ? Interior != null && portal == Interior.Exit : portal == Entrance);
        public bool ApplyArea(string area, Vector3? savedPosition = null)
        {
            LastError = null;
            Vector3 point;
            if (area == HousingState.Home)
            {
                if (Interior == null) { LastError = "小屋室内尚未加载"; return false; }
                point = savedPosition.HasValue ? Config.InteriorOrigin + savedPosition.Value : Interior.SpawnPoint.position;
                var local = point - Config.InteriorOrigin;
                if (!HousingState.ValidPosition(area, local.x, local.y, local.z, Upgrade.Level) || !Free(point, savedPosition.HasValue))
                { LastError = "小屋内的角色位置被家具挡住，请检查室内布局"; return false; }
            }
            else if (area == HousingState.Island)
            {
                if (savedPosition.HasValue && GameManager.Instance.Terrain.TryResolve(savedPosition.Value,out var safe) && Free(safe, true)) point = safe;
                else if (!FindOutsidePoint(out point)) { LastError = "小屋门口没有安全落脚位置"; return false; }
            }
            else { LastError = "未知的玩家区域"; return false; }
            if (State.Area != area) { if (!State.Busy) State.Begin(area); State.Complete(); }
            else State.Abort();
            if (area == HousingState.Home)
            {
                layers.Clear(); foreach (var t in players.Player.GetComponentsInChildren<Transform>(true)) { layers.Add(t.gameObject, t.gameObject.layer); t.gameObject.layer = Config.InteriorLayer; }
                view.cullingMask = (1 << Config.InteriorLayer) | (1 << 5);
            }
            else
            {
                foreach (var pair in layers) if (pair.Key != null) pair.Key.layer = pair.Value; layers.Clear();
                view.cullingMask = originalCameraMask & ~(1 << Config.InteriorLayer);
            }
            players.RestorePosition(point); players.Player.Visual.rotation = Quaternion.Euler(0, area == HousingState.Home ? 0 : 180, 0);
            view.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); Physics.SyncTransforms(); return true;
        }
        private bool FindOutsidePoint(out Vector3 point)
        {
            var door = Entrance.Position;
            foreach (float depth in new[] { 1f, 1.5f, 2f }) foreach (float side in new[] { 0f, -.75f, .75f, -1.2f, 1.2f })
            {
                var p = door + new Vector3(side, .1f - door.y, -depth);
                if (Mathf.Abs(p.x) <= 11.5f && Mathf.Abs(p.z) <= 11.5f && Free(p)) { point = p; return true; }
            }
            // Legacy decorations can surround the old bed. Preserve them and find the closest free island cell.
            float best = float.MaxValue; point = default; bool found = false;
            for (int x = -11; x <= 11; x++) for (int z = -11; z <= 11; z++)
            {
                var p = new Vector3(x, .1f, z); float distance = (p - door).sqrMagnitude;
                if (distance < best && Free(p)) { point = p; best = distance; found = true; }
            }
            return found;
        }
        private bool Free(Vector3 point, bool saved = false)
        {
            Physics.SyncTransforms(); var c = players.Player.GetComponent<CharacterController>();
            float radius = saved ? Mathf.Max(.02f, c.radius - c.skinWidth - .01f) : c.radius + .025f;
            var center = point + c.center; float half = c.height * .5f - radius;
            foreach (var hit in Physics.OverlapCapsule(center - Vector3.up * half, center + Vector3.up * half, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if(GameManager.Instance?.Terrain?.Terrain?.IsGround(hit)??false)continue;
                if (!hit.transform.IsChildOf(players.Player.transform) && hit.bounds.max.y > point.y + .05f) return false;
            }
            return true;
        }
        public Vector3 SavedPosition => Inside ? players.Player.transform.position - Config.InteriorOrigin : players.Player.transform.position;
        public void UpdatePresentation()
        { if (Inside && view != null) view.backgroundColor = new Color(.19f, .23f, .21f); }
        public bool ReservesTile(int x, int z)
        {
            if (Entrance == null) return false; var p = Entrance.Position;
            return x + .5f > p.x - .36f && x - .5f < p.x + .36f && z + .5f > p.z - 2.1f && z - .5f < p.z;
        }
        public void Clear()
        {
            generation++; foreach (var pair in layers) if (pair.Key != null) pair.Key.layer = pair.Value; layers.Clear();
            if (view != null) { view.cullingMask = originalCameraMask; view.backgroundColor = originalBackground; }
            if (sun != null) sun.cullingMask = originalSunMask;
            if (exterior.IsValid()) { if (exterior.IsDone) Release(exterior); else exterior.Completed += Release; }
            if (exteriorUpgrade != null) Destroy(exteriorUpgrade); if (interiorUpgrade != null) Destroy(interiorUpgrade);
            ReleaseAsset(exteriorUpgradeAsset); ReleaseAsset(interiorUpgradeAsset); exteriorUpgradeAsset = interiorUpgradeAsset = default;
            exteriorUpgrade = interiorUpgrade = null; Upgrade = null; UpgradeFeedback = null;
            exterior = default; Entrance = null; Interior = null; State = null; Config = null; players = null; view = null; world = null; sun = null; LastError = null;
        }
        private static void Release(AsyncOperationHandle<GameObject> h)
        { if (!h.IsValid()) return; if (h.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(h); else Addressables.Release(h); }
        private static void ReleaseAsset(AsyncOperationHandle<GameObject> h)
        { if (!h.IsValid()) return; if (h.IsDone) Addressables.Release(h); else h.Completed += value => { if (value.IsValid()) Addressables.Release(value); }; }
        private void OnDestroy() => Clear();
    }
}
