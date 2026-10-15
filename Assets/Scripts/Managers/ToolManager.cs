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
        private Quaternion facing;
        private string lastFailure;
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
        public void ReportFailure(string message)
        {
            if (lastFailure == message && UnityEngine.Time.unscaledTime < nextFailure) return;
            lastFailure = message; nextFailure = UnityEngine.Time.unscaledTime + 1f;
            Feedback?.Invoke(new ToolFeedback(false, message));
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
            player.FaceAndAct(target); player.ToolActionLocked = true; facing = player.Visual.rotation;
            player.Animation.BeginAction(actionKind);
            Presentation.Show(actionKind); Presentation.Burst(actionKind, target);
            string detail = before > stamina.State.Current ? $"体力 -{before - stamina.State.Current}" : "";
            if (harvest) detail = farm.Inventory.Catalog.Find(harvestedId).DisplayName + " +1" + (detail.Length > 0 ? " · " + detail : "");
            lastFailure = null;
            ActionSucceeded?.Invoke(actionKind);
            Feedback?.Invoke(new ToolFeedback(true, message + "完成", detail));
            return true;
        }
        private void Update()
        {
            if (Presentation == null || !InputEnabled) return;
            bool wasBusy = Busy;
            gate.Tick(UnityEngine.Time.deltaTime, false); Presentation.TickEffects(UnityEngine.Time.deltaTime);
            gathering?.TickVisuals(UnityEngine.Time.deltaTime);
            if (Busy)
            {
                float progress = 1 - (float)gate.Remaining / duration;
                Presentation.Animate(actionKind, progress);
                player.Animation.SampleAction(progress);
            }
            else if (wasBusy)
            { player.ToolActionLocked = false; player.Visual.rotation = facing; player.Animation.EndAction(); Presentation.Show(ActionKind(SelectedTool)); }
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
            gathering.PlayHit(index, target - player.transform.position, duration);
            actionKind = (ToolActionKind)SelectedTool;
            player.FaceAndAct(target); player.ToolActionLocked = true; facing = player.Visual.rotation;
            player.Animation.BeginAction(actionKind);
            Presentation.Show(actionKind); Presentation.Burst(actionKind, target + Vector3.up * 0.5f);
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
            message = "请走近果树";
            if (!InputEnabled || Busy || !orchard.InRange(tree) || !gate.TryBegin(duration)) return false;
            if (!orchard.Harvest(tree, out message)) { gate.Reset(); ReportFailure(message); return false; }
            actionKind = ToolActionKind.Harvest; player.FaceAndAct(tree.transform.position); player.ToolActionLocked = true; facing = player.Visual.rotation;
            player.Animation.BeginAction(actionKind); Presentation.Show(actionKind); Presentation.Burst(actionKind, tree.transform.position + Vector3.up * .1f);
            ActionSucceeded?.Invoke(actionKind); Feedback?.Invoke(new ToolFeedback(true, "采收完成", message)); return true;
        }
        public bool TryBeachPickup(BeachPickup pickup,out string message)
        {
            message="请走近采集物";
            if(!InputEnabled||Busy||gathering==null||!gathering.BeachInRange(pickup,player.transform.position,reach)||!gate.TryBegin(duration))return false;
            var target=pickup.transform.position;
            if(!gathering.Collect(pickup,out message)){gate.Reset();ReportFailure(message);return false;}
            actionKind=ToolActionKind.Harvest;player.FaceAndAct(target);player.ToolActionLocked=true;facing=player.Visual.rotation;
            player.Animation.BeginAction(actionKind);Presentation.Show(actionKind);Presentation.Burst(actionKind,target+Vector3.up*.05f);
            lastFailure=null;ActionSucceeded?.Invoke(actionKind);Feedback?.Invoke(new ToolFeedback(true,"拾取完成",message));return true;
        }
        public void Clear()
        {
            if (player != null) { player.ToolActionLocked = false; player.Animation?.EndAction(); if (Busy) player.Visual.rotation = facing; }
            Presentation?.Clear(); Presentation = null; gate.Reset(); InputEnabled = false;
            ReleaseEffects(); LastError = null;
            farm = null; stamina = null; player = null; gathering = null; lastFailure = null; nextFailure = 0;
        }
        private void OnDestroy() => Clear();
    }
}
