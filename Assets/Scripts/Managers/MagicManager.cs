using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.Magic;
using MagicIsland.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagicIsland.Managers
{
    public sealed class MagicManager : MonoBehaviour
    {
        public MagicState State { get; private set; }
        public MagicConfig Config { get; private set; }
        public MagicRainEffect Effect { get; private set; }
        public string LastError { get; private set; }
        public string Feedback { get; private set; }
        public bool OwnsEffect => effectHandle.IsValid();
        private FarmManager farm;
        private AsyncOperationHandle<GameObject> effectHandle;
        private int generation;
        private bool bound;
        private float previousNight;
        public void Initialize(MagicConfig config, FarmManager farm, SavedMagic saved)
        {
            Clear(); Config = config; this.farm = farm; State = new MagicState(config.Definition());
            if (saved != null) State.Restore(saved);
            farm.SetMagicHarvestRecorder(State.RecordHarvest); farm.ActionSucceeded += FarmAction;
            previousNight = Shader.GetGlobalFloat("_IslandMagicNight"); bound = true;
        }
        private void FarmAction(TaskAction action) { if (action == TaskAction.Harvest) { Feedback = null; State.NotifyChanged(); } }
        public IEnumerator LoadEffect(Transform world)
        {
            ClearEffect(); int current = generation; LastError = null;
            effectHandle = Addressables.InstantiateAsync(Config.RainEffectAddress, world); yield return effectHandle;
            if (current != generation) yield break;
            Effect = effectHandle.Status == AsyncOperationStatus.Succeeded && effectHandle.Result != null ? effectHandle.Result.GetComponent<MagicRainEffect>() : null;
            if (Effect == null || !Effect.IsValid) { LastError = "唤雨术特效资源无效"; ClearEffect(); yield break; }
            Effect.ResetEffect();
        }
        public bool Study(InventoryManager inventory)
        {
            MagicStage before = State.Stage;
            var result = State.Study(inventory.State);
            Feedback = result == MagicResult.Success ? before == MagicStage.Unstudied ? $"获得月光花种子 ×{Config.GiftSeeds}" : "你学会了唤雨术！关闭手札，指向农田按 R 施法。" : Explain(result);
            return result == MagicResult.Success;
        }
        public void ResetPresentation() => Effect?.ResetEffect();
        public void PlayRain(Vector3 center) => Effect?.Begin(center, (Config.RainRadius * 2 + 1) * MagicIsland.Farming.FarmGrid.CellSize);
        public void Tick(float seconds, bool paused, bool inside, float daylight)
        {
            if (!bound) return;
            Shader.SetGlobalFloat("_IslandMagicNight", 1 - Mathf.Clamp01(daylight));
            Effect?.Tick(seconds, paused || inside);
        }
        public static string Explain(MagicResult result)
        {
            switch (result)
            {
                case MagicResult.Locked: return "先在魔法手札中完成启蒙，学会唤雨术";
                case MagicResult.BagFull: return "背包空间不足，请先腾出位置领取种子";
                case MagicResult.MissingFlower: return "背包里需要一朵月光花，普通或优质均可";
                case MagicResult.WrongStage: return "先亲手收获一朵月光花，再回来研读符文";
                case MagicResult.InvalidPlot: return "请指向附近已耕作的农田";
                case MagicResult.NothingToWater: return "这片农田没有需要浇水的幼苗";
                case MagicResult.InsufficientStamina: return "体力不足，请到床边休息";
                default: return "正在操作，请稍候";
            }
        }
        private static void Release(AsyncOperationHandle<GameObject> h)
        { if (!h.IsValid()) return; if (h.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(h); else Addressables.Release(h); }
        private void ClearEffect()
        {
            generation++; if (Effect != null) Effect.ResetEffect(); Effect = null;
            if (effectHandle.IsValid()) { if (effectHandle.IsDone) Release(effectHandle); else effectHandle.Completed += Release; }
            effectHandle = default;
        }
        public void Clear()
        {
            if (farm != null) { farm.SetMagicHarvestRecorder(null); farm.ActionSucceeded -= FarmAction; }
            ClearEffect(); if (bound) Shader.SetGlobalFloat("_IslandMagicNight", previousNight);
            bound = false; State = null; Config = null; farm = null; Feedback = LastError = null;
        }
        private void OnDestroy() => Clear();
    }
}
