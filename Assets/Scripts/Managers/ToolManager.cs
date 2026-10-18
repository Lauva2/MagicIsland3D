using System;
using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Player;
using MagicIsland.Tools;
using MagicIsland.Interaction;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MagicIsland.Managers
{
    public sealed class ToolManager : MonoBehaviour
    {
        public FarmTool SelectedTool { get; private set; }
        public bool Busy => gate.Busy;
        public float ActionProgress => Busy ? 1 - (float)gate.Remaining / duration : 0;
        public bool InputEnabled { get; private set; }
        public ToolPresentation Presentation { get; private set; }
        public string LastError { get; private set; }
        public bool OwnsEffects => effectsHandle.IsValid();
        private AsyncOperationHandle<GameObject> effectsHandle;
        private int effectGeneration;
        public IEnumerator LoadEffects(string address)
        {
            LastError = null; int generation = effectGeneration;
            effectsHandle = Addressables.LoadAssetAsync<GameObject>(address); yield return effectsHandle;
            if (generation != effectGeneration) yield break;
            if (effectsHandle.Status != AsyncOperationStatus.Succeeded || effectsHandle.Result == null
                || effectsHandle.Result.GetComponent<FarmActionEffects>() == null || !effectsHandle.Result.GetComponent<FarmActionEffects>().IsValid
                || effectsHandle.Result.GetComponentsInChildren<Collider>(true).Length != 0 || Presentation == null)
            { LastError = "Cannot load tool action effects: " + address; ReleaseEffects(); yield break; }
            Presentation.BindEffects(effectsHandle.Result);
        }
        private void ReleaseEffects()
        {
            effectGeneration++;
            if (effectsHandle.IsValid())
            {
                if (effectsHandle.IsDone) Addressables.Release(effectsHandle);
                else effectsHandle.Completed += value => { if (value.IsValid()) Addressables.Release(value); };
            }
            effectsHandle = default;
        }
        public event Action<ToolFeedback> Feedback;
        public event Action<ToolActionKind> ActionSucceeded;
        private readonly ToolActionGate gate = new ToolActionGate();
        private FarmManager farm;
        private StaminaManager stamina;
        private GatheringManager gathering;
        private PlayerMovement player;
        private float duration, reach;
        private ToolActionKind actionKind;
        private Quaternion facing, initialFacing;
        private Vector3 effectPosition, hitDirection;
        private bool impactPending;
        private MagicManager pendingMagic;
        private int hitIndex = -1;
        private string lastFailure, lastFailureDetail;
        private float nextFailure;
        public void Initialize(GameConfig config, FarmManager farm, StaminaManager stamina, PlayerMovement player, Transform world, GatheringManager gathering = null)
        {
            Clear(); this.farm = farm; this.stamina = stamina; this.player = player; this.gathering = gathering;
            duration = config.ToolActionDuration; reach = config.InteractionReach;
            SelectedTool = FarmTool.Hoe; Presentation = new ToolPresentation(player.Visual, world, player.Animation.ToolSocket);
        }
        public bool SelectTool(FarmTool tool)
        {
            if (Busy || !InputEnabled || tool < FarmTool.Hoe || tool > FarmTool.Fertilizer) return false;
            SelectedTool = tool; Presentation.Show(ActionKind(tool)); return true;
        }
        private static ToolActionKind ActionKind(FarmTool tool) => tool == FarmTool.FishingRod ? ToolActionKind.FishingRod : tool == FarmTool.Fertilizer ? ToolActionKind.Fertilizer : (ToolActionKind)tool;
        public void SetInputEnabled(bool enabled) => InputEnabled = enabled;
        public void ReportFailure(string message, string detail = "")
        {
            if (lastFailure == message && lastFailureDetail == detail && UnityEngine.Time.unscaledTime < nextFailure) return;
            lastFailure = message; lastFailureDetail = detail; nextFailure = UnityEngine.Time.unscaledTime + 1f;
            Feedback?.Invoke(new ToolFeedback(false, message, detail));
        }
        public bool TryAct(int index, out string message)
        {
            message = "正在操作，请稍候";
            if (!InputEnabled || Busy || farm == null || index < 0 || index >= farm.State.Cells.Length) return false;
            if (SelectedTool == FarmTool.FishingRod) { message = "鱼竿用于海岸垂钓，请切换农具"; ReportFailure(message); return false; }
            Vector3 target = farm.Grid.CellCenter(index);
            if (Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).sqrMagnitude > reach * reach)
            { message = "距离太远，请靠近"; ReportFailure(message); return false; }
            // Claim the action before farm/inventory events can trigger another request.
            if (!gate.TryBegin(duration)) return false;
            bool fertilizing = SelectedTool == FarmTool.Fertilizer;
            bool harvest = !fertilizing && farm.State.Cells[index].Mature;
            string harvestedId = harvest ? farm.State.HarvestId(index) : null;
            int before = stamina.State.Current;
            bool acted = fertilizing ? farm.Fertilizer.Apply(index, out message) : farm.TryAct(index, SelectedTool, out message);
            if (!acted)
            { gate.Reset(); ReportFailure(message); return false; }
            actionKind = harvest ? ToolActionKind.Harvest : ActionKind(SelectedTool);
            BeginPresentation(target, target);
            string detail = before > stamina.State.Current ? $"体力 -{before - stamina.State.Current}" : "";
            if (harvest) detail = farm.Inventory.Catalog.Find(harvestedId).DisplayName + " +1" + (detail.Length > 0 ? " · " + detail : "");
            lastFailure = null;
            ActionSucceeded?.Invoke(actionKind);
            Feedback?.Invoke(new ToolFeedback(true, message + "完成", detail));
            return true;
        }
        private void Update()
        {
            TickAction(UnityEngine.Time.deltaTime);
        }
        internal void TickAction(float seconds)
        {
            if (Presentation == null || !InputEnabled) return;
            bool wasBusy = Busy;
            float before = ActionProgress;
            gate.Tick(seconds, false);
            float progress = wasBusy ? (Busy ? ActionProgress : 1) : 0;
            float contact = ContactProgress(actionKind);
            // Advance old effects up to contact, emit once, then advance only the remaining frame time.
            // This also handles a low frame rate crossing contact and the end in one update.
            float lead = wasBusy && impactPending && progress >= contact
                ? Mathf.Clamp((contact - before) * duration, 0, seconds) : seconds;
            Presentation.TickEffects(lead); gathering?.TickVisuals(lead);
            if (wasBusy)
            {
                player.Visual.rotation = Quaternion.Slerp(initialFacing, facing, Mathf.SmoothStep(0, 1, progress / .2f));
                Presentation.Animate(actionKind, progress);
                player.Animation.SampleAction(progress);
                if (impactPending && progress >= contact)
                {
                    impactPending = false;
                    if (pendingMagic != null) { pendingMagic.PlayRain(effectPosition); pendingMagic = null; }
                    else Presentation.Burst(actionKind, effectPosition);
                    if (hitIndex >= 0) gathering?.PlayHit(hitIndex, hitDirection, duration * (1 - contact));
                    Presentation.TickEffects(seconds - lead); gathering?.TickVisuals(seconds - lead);
                }
            }
            if (wasBusy && !Busy)
            { player.ToolActionLocked = false; player.Visual.rotation = facing; player.Animation.EndAction(); Presentation.Show(ActionKind(SelectedTool)); }
        }
        private static float ContactProgress(ToolActionKind kind) => kind == ToolActionKind.Water ? .35f : .5f;
        private void BeginPresentation(Vector3 target, Vector3 effect, int gatheringIndex = -1)
        {
            initialFacing = player.Visual.rotation;
            Vector3 direction = Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up);
            facing = direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : initialFacing;
            effectPosition = effect; hitIndex = gatheringIndex; hitDirection = direction; impactPending = true;
            player.ToolActionLocked = true; player.Animation.BeginAction(actionKind);
            Presentation.Show(actionKind); Presentation.Animate(actionKind, 0);
        }
        public bool TryCastRain(MagicManager magic, int index, out string message)
        {
            message = "正在操作，请稍候";
            if (!InputEnabled || Busy || magic?.State == null || farm == null || index < 0 || index >= farm.State.Cells.Length) return false;
            Vector3 target = farm.Grid.CellCenter(index);
            if (Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).sqrMagnitude > reach * reach)
            { message = "距离太远，请靠近"; ReportFailure(message); return false; }
            if (!gate.TryBegin(duration)) return false;
            var result = magic.State.CastRain(farm.State, stamina.State, index, out int watered);
            if (result != MagicIsland.Magic.MagicResult.Success) { gate.Reset(); message = MagicManager.Explain(result); ReportFailure(message); return false; }
            farm.Grid.Refresh(); actionKind = ToolActionKind.RainSpell; BeginPresentation(target, target); pendingMagic = magic;
            message = $"唤雨成功 · 浇灌 {watered} 格"; lastFailure = null;
            ActionSucceeded?.Invoke(actionKind); Feedback?.Invoke(new ToolFeedback(true, message, $"体力 -{magic.Config.RainStamina}")); return true;
        }
        public bool TryGather(int index, out string message)
        {
            message = "正在操作，请稍候";
            if (!InputEnabled || Busy || gathering == null || index < 0 || index >= gathering.Nodes.Count) return false;
            Vector3 target = gathering.Nodes[index].transform.position;
            if (!gathering.InRange(index, player.transform.position, reach))
            { message = "距离太远，请靠近"; ReportFailure(message); return false; }
            if (!gate.TryBegin(duration)) return false;
            int before = stamina.State.Current;
            if (!gathering.Hit(index, SelectedTool, out message)) { gate.Reset(); ReportFailure(message); return false; }
            actionKind = (ToolActionKind)SelectedTool;
            BeginPresentation(target, target + Vector3.up * .5f, index);
            string detail = "体力 -" + (before - stamina.State.Current);
            if (gathering.State.IsDepleted(index))
            {
                var definition = gathering.State.Definitions[index];
                detail = farm.Inventory.Catalog.Find(definition.ItemId).DisplayName + " +" + definition.Yield + " · " + detail;
            }
            lastFailure = null; ActionSucceeded?.Invoke(actionKind); Feedback?.Invoke(new ToolFeedback(true, message, detail)); return true;
        }
        public bool TryHarvestFruit(OrchardManager orchard, PlacedDecoration tree, out string message)
        {
            message = "正在操作，请稍候";
            if (!InputEnabled || Busy || orchard == null || !orchard.Owns(tree)) return false;
            if (!orchard.InRange(tree)) { message = "距离太远，请靠近"; ReportFailure(message); return false; }
            if (!gate.TryBegin(duration)) return false;
            if (!orchard.Harvest(tree, out message)) { gate.Reset(); ReportFailure(message); return false; }
            actionKind = ToolActionKind.Harvest; BeginPresentation(tree.transform.position, tree.transform.position + Vector3.up * .1f);
            lastFailure = null;
            ActionSucceeded?.Invoke(actionKind); Feedback?.Invoke(new ToolFeedback(true, "采收完成", message)); return true;
        }
        public bool TryBeachPickup(BeachPickup pickup,out string message)
        {
            message="正在操作，请稍候";
            if(!InputEnabled||Busy||gathering==null||!gathering.Owns(pickup))return false;
            if(!gathering.BeachInRange(pickup,player.transform.position,reach)){message="距离太远，请靠近";ReportFailure(message);return false;}
            if(!gate.TryBegin(duration))return false;
            var target=pickup.transform.position;
            if(!gathering.Collect(pickup,out message)){gate.Reset();ReportFailure(message);return false;}
            actionKind=ToolActionKind.Harvest;BeginPresentation(target,target+Vector3.up*.05f);
            lastFailure=null;ActionSucceeded?.Invoke(actionKind);Feedback?.Invoke(new ToolFeedback(true,"拾取完成",message));return true;
        }
        public void Clear()
        {
            if (player != null) { player.ToolActionLocked = false; player.Animation?.EndAction(); }
            impactPending = false; hitIndex = -1; pendingMagic = null;
            Presentation?.Clear(); Presentation = null; gate.Reset(); InputEnabled = false;
            ReleaseEffects(); LastError = null;
            farm = null; stamina = null; player = null; gathering = null; lastFailure = lastFailureDetail = null; nextFailure = 0;
        }
        private void OnDestroy() => Clear();
    }
}
