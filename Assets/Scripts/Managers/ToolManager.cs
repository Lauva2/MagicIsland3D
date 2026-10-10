using System;
using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Player;
using MagicIsland.Tools;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class ToolManager : MonoBehaviour
    {
        public FarmTool SelectedTool { get; private set; }
        public bool Busy => gate.Busy;
        public float ActionProgress => Busy ? 1 - (float)gate.Remaining / duration : 0;
        public bool InputEnabled { get; private set; }
        public ToolPresentation Presentation { get; private set; }
        public event Action<ToolFeedback> Feedback;
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
            SelectedTool = FarmTool.Hoe; Presentation = new ToolPresentation(player.Visual, world);
        }
        public bool SelectTool(FarmTool tool)
        {
            if (Busy || !InputEnabled || tool < FarmTool.Hoe || tool > FarmTool.Pickaxe) return false;
            SelectedTool = tool; Presentation.Show((ToolActionKind)tool); return true;
        }
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
            Vector3 target = farm.Grid.CellCenter(index);
            if (Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).sqrMagnitude > reach * reach)
            { message = "距离太远，请靠近"; ReportFailure(message); return false; }
            // Claim the action before farm/inventory events can trigger another request.
            if (!gate.TryBegin(duration)) return false;
            bool harvest = farm.State.Cells[index].Mature;
            string harvestedId = harvest ? farm.State.FindCrop(farm.State.Cells[index].CropId).HarvestId : null;
            int before = stamina.State.Current;
            if (!farm.TryAct(index, SelectedTool, out message))
            { gate.Reset(); ReportFailure(message); return false; }
            actionKind = harvest ? ToolActionKind.Harvest : (ToolActionKind)SelectedTool;
            player.FaceAndAct(target); player.ToolActionLocked = true; facing = player.Visual.rotation;
            Presentation.Show(actionKind); Presentation.Burst(actionKind, target);
            string detail = before > stamina.State.Current ? $"体力 -{before - stamina.State.Current}" : "";
            if (harvest) detail = farm.Inventory.Catalog.Find(harvestedId).DisplayName + " +1" + (detail.Length > 0 ? " · " + detail : "");
            lastFailure = null;
            Feedback?.Invoke(new ToolFeedback(true, message + "完成", detail));
            return true;
        }
        private void Update()
        {
            if (Presentation == null || !InputEnabled) return;
            bool wasBusy = Busy;
            gate.Tick(UnityEngine.Time.deltaTime, false); Presentation.TickEffects(UnityEngine.Time.deltaTime);
            if (Busy)
            {
                float progress = 1 - (float)gate.Remaining / duration;
                Presentation.Animate(actionKind, progress);
                player.Visual.rotation = facing * Quaternion.Euler(Mathf.Sin(progress * Mathf.PI) * 12f, 0, 0);
            }
            else if (wasBusy)
            { player.ToolActionLocked = false; player.Visual.rotation = facing; Presentation.Show((ToolActionKind)SelectedTool); }
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
            player.FaceAndAct(target); player.ToolActionLocked = true; facing = player.Visual.rotation;
            Presentation.Show(actionKind); Presentation.Burst(actionKind, target + Vector3.up * 0.5f);
            string detail = "体力 -" + (before - stamina.State.Current);
            if (gathering.State.IsDepleted(index))
            {
                var definition = gathering.State.Definitions[index];
                detail = farm.Inventory.Catalog.Find(definition.ItemId).DisplayName + " +" + definition.Yield + " · " + detail;
            }
            lastFailure = null; Feedback?.Invoke(new ToolFeedback(true, message, detail)); return true;
        }
        public void Clear()
        {
            if (player != null) { player.ToolActionLocked = false; if (Busy) player.Visual.rotation = facing; }
            Presentation?.Clear(); Presentation = null; gate.Reset(); InputEnabled = false;
            farm = null; stamina = null; player = null; gathering = null; lastFailure = null; nextFailure = 0;
        }
        private void OnDestroy() => Clear();
    }
}
