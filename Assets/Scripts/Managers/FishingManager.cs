using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.FishingSystem;
using MagicIsland.Interaction;
using MagicIsland.Tools;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class FishingManager : MonoBehaviour
    {
        public FishingState State { get; private set; }
        public FishingCatalog Catalog { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public StaminaManager Stamina { get; private set; }
        public FishingSpot Spot { get; private set; }
        public FishingRodVisual Rod { get; private set; }
        public string Feedback { get; private set; }
        public string LastError { get; private set; }
        private AsyncOperationHandle<GameObject> rodHandle, spotHandle;
        private int generation;
        private Transform focusedVisual;
        private Quaternion oldRotation;
        private LineRenderer line;
        private Material lineMaterial;
        public void Initialize(FishingCatalog catalog, InventoryManager inventory, StaminaManager stamina)
        { Clear(); Catalog = catalog; Inventory = inventory; Stamina = stamina; State = new FishingState(catalog.Definitions(), catalog.BiteWindow, catalog.StaminaCost); }
        public IEnumerator LoadAssets(Transform world, Transform grip)
        {
            int current = generation; LastError = null; rodHandle = Addressables.InstantiateAsync(Catalog.RodAddress, grip); yield return rodHandle;
            if (current != generation) yield break;
            if (rodHandle.Status != AsyncOperationStatus.Succeeded || rodHandle.Result == null || (Rod = rodHandle.Result.GetComponent<FishingRodVisual>()) == null || Rod.Tip == null)
            { LastError = "Fishing rod requires tip: " + Catalog.RodAddress; yield break; }
            spotHandle = Addressables.InstantiateAsync(Catalog.SpotAddress, world); yield return spotHandle;
            if (current != generation) yield break;
            if (spotHandle.Status != AsyncOperationStatus.Succeeded || spotHandle.Result == null || (Spot = spotHandle.Result.GetComponent<FishingSpot>()) == null || Spot.Bobber == null || Spot.ReachPoint == null || Spot.Deck == null || Spot.GetComponentsInChildren<Collider>().Length == 0)
            { LastError = "Fishing spot requires bobber and collider: " + Catalog.SpotAddress; yield break; }
            Spot.transform.localPosition = Catalog.SpotPosition; Spot.Bobber.gameObject.SetActive(false);
            line = new GameObject("Fishing Line").AddComponent<LineRenderer>(); line.transform.SetParent(Spot.transform, false); line.useWorldSpace = true; line.positionCount = 3;
            line.startWidth = line.endWidth = .018f; lineMaterial = new Material(Shader.Find("Sprites/Default")); lineMaterial.color = new Color(.95f, .93f, .82f); line.sharedMaterial = lineMaterial; line.enabled = false;
        }
        public bool InRange(Vector3 position, float reach) => Spot != null && Vector3.ProjectOnPlane(Spot.Position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public void Focus(Transform visual)
        { Unfocus(); focusedVisual = visual; oldRotation = visual.rotation; Vector3 direction = Vector3.ProjectOnPlane(Spot.Bobber.position - visual.position, Vector3.up); if (direction.sqrMagnitude > .001f) visual.rotation = Quaternion.LookRotation(direction); }
        public void Unfocus() { if (focusedVisual != null) focusedVisual.rotation = oldRotation; focusedVisual = null; }
        public FishingResult CheckCast() => State.CheckCast(Inventory.State, Stamina.State);
        public bool Cast()
        {
            var result = State.Cast(State.Pick(Random.value * .999999), Random.Range(Catalog.WaitMin, Catalog.WaitMax), Inventory.State, Stamina.State);
            Feedback = result == FishingResult.Success ? "浮漂已入水，等它下沉再收竿" : Explain(result); RefreshVisual();
            if(result==FishingResult.Success&&Spot!=null)GameManager.Instance?.Ocean?.DisturbWater(Spot.Bobber.position);
            return result == FishingResult.Success;
        }
        public void Tick(float seconds)
        {
            var before = State.Phase; State.Tick(seconds);
            if (State.Phase != before) Feedback = State.Phase == FishingPhase.Bite ? "有鱼咬钩！立即点击收竿" : "鱼游走了，可以重新抛竿";
            RefreshVisual();
        }
        public bool Reel()
        {
            var result = State.Reel(Inventory.State, Stamina.State);
            Feedback = result == FishingResult.Success ? "钓到" + Inventory.Catalog.Find(State.CaughtItemId).DisplayName + " × 1 · 体力 -" + Catalog.StaminaCost : Explain(result);
            RefreshVisual(); return result == FishingResult.Success;
        }
        public void Cancel() { State?.Cancel(); Unfocus(); RefreshVisual(); }
        private void RefreshVisual()
        {
            bool active = State != null && State.Active;
            if (Spot != null && Spot.Bobber != null)
            {
                Spot.Bobber.gameObject.SetActive(active); var point = Spot.Bobber.position;
                var ocean=GameManager.Instance?.Ocean?.Surface;
                point.y=(ocean!=null?ocean.HeightAt(point):-.22f)+(State!=null&&State.Phase==FishingPhase.Bite?-.045f:.055f);Spot.Bobber.position=point;
            }
            if (line != null) { line.enabled = active; if (active && Rod != null) { line.SetPosition(0, Rod.Tip.position); line.SetPosition(1, (Rod.Tip.position + Spot.Bobber.position) * .5f + Vector3.down * .18f); line.SetPosition(2, Spot.Bobber.position); } }
        }
        public void SetFeedback(string value) => Feedback = value;
        // Follow OceanManager's completed wave update and the rig's animated rod tip.
        private void LateUpdate() { if(State!=null&&State.Active)RefreshVisual(); }
        public void SetSaveFailed() => Feedback = "鱼已入背包，保存失败，请点击保存进度重试";
        public void SetSaved() => Feedback = "钓鱼进度已保存";
        public static string Explain(FishingResult result) => result == FishingResult.StaminaLow ? "体力不足，成功钓鱼需要体力" : result == FishingResult.BagFull ? "背包需要能放下一条任意鱼，请先整理" : result == FishingResult.TooEarly ? "收竿太早了，未消耗体力" : result == FishingResult.TooLate ? "错过了咬钩，未消耗体力" : "暂时无法钓鱼";
        public void Clear()
        {
            generation++; Cancel(); ReleaseOrWait(rodHandle); ReleaseOrWait(spotHandle); rodHandle = spotHandle = default;
            if (line != null) Destroy(line.gameObject); if (lineMaterial != null) Destroy(lineMaterial); line = null; lineMaterial = null;
            Rod = null; Spot = null; State = null; Catalog = null; Inventory = null; Stamina = null; Feedback = null; LastError = null;
        }
        private static void ReleaseOrWait(AsyncOperationHandle<GameObject> value) { if (!value.IsValid()) return; if (value.IsDone) Release(value); else value.Completed += Release; }
        private static void Release(AsyncOperationHandle<GameObject> value) { if (!value.IsValid()) return; if (value.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(value); else Addressables.Release(value); }
        private void OnDestroy() => Clear();
    }
}
