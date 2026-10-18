using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MagicIsland.CameraSystem;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.Tools;
using MagicIsland.UI.Controllers;
using MagicIsland.UI.Views;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Runs inside InteractionPromptFlowVerification's packed-AA game and isolated save directory.
    public static class InteractionExperienceVerification
    {
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static Ray Down(Vector3 point) => new Ray(point + Vector3.up * 8, Vector3.down);
        public static void Advance(ToolManager tools, float seconds) => typeof(ToolManager)
            .GetMethod("TickAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tools, new object[] { seconds });
        static void Move(GameManager g, Vector3 point)
        { g.Players.RestorePosition(point); g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget(); Physics.SyncTransforms(); }
        static void Finish(GameManager g) { Advance(g.Tools, g.Configuration.Config.ToolActionDuration * 2); g.Tools.Presentation.TickEffects(2); g.Gathering.TickVisuals(2); }
        static T Controller<T>(GameManager g, string address)
        {
            var entries = (IDictionary)typeof(UIManager).GetField("instances", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(g.UI);
            var entry = entries[address];
            return (T)entry.GetType().GetField("Controller").GetValue(entry);
        }
        static void Capture(GameManager g, string name)
        {
            g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget(); g.UI.RefreshViews();
            GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true);
        }
        public static void Verify(GameManager g)
        {
            Require(!File.Exists(g.Saves.SavePath), "Experience fixture starts without a sleep checkpoint");
            g.Time.Clock.Restore(720); g.Weather.Tick(720, true);
            VerifyPointer(g);
            VerifyRefreshes(g);
            var plot = g.Farm.Grid.CellCenter(21);
            Move(g, plot + new Vector3(0, .1f, -1)); g.Tools.SelectTool(FarmTool.Hoe);
            Motion(g, ToolActionKind.Hoe, plot, () => g.Tools.TryAct(21, out _), true);
            g.Tools.SelectTool(FarmTool.Seeds); Motion(g, ToolActionKind.Seed, plot, () => g.Tools.TryAct(21, out _));
            g.Tools.SelectTool(FarmTool.WateringCan); Motion(g, ToolActionKind.Water, plot, () => g.Tools.TryAct(21, out _));
            g.Farm.State.Cells[21].Growth = g.Farm.State.Cells[21].RequiredGrowthDays; g.Farm.Grid.Refresh();
            Motion(g, ToolActionKind.Harvest, plot, () => g.Tools.TryAct(21, out _));
            Require(g.Inventory.TryAdd("basic_fertilizer", 1), "Fertilizer fixture"); g.Tools.SelectTool(FarmTool.Fertilizer);
            Motion(g, ToolActionKind.Fertilizer, plot, () => g.Tools.TryAct(21, out _));
            foreach (var pair in new[] { (1, FarmTool.Axe, ToolActionKind.Axe), (g.Gathering.Nodes.Count - 1, FarmTool.Pickaxe, ToolActionKind.Pickaxe) })
            {
                var node = g.Gathering.Nodes[pair.Item1]; Move(g, node.transform.position + new Vector3(0, .1f, -1.6f)); g.Tools.SelectTool(pair.Item2);
                Motion(g, pair.Item3, node.transform.position, () => g.Tools.TryGather(pair.Item1, out _), node: node);
            }
            var pickup = g.Gathering.BeachNodes[0];
            Move(g, Vector3.zero);
            ToolFeedback failure = null; Action<ToolFeedback> observe = value => failure = value; g.Tools.Feedback += observe;
            try { Require(!g.Tools.TryBeachPickup(pickup, out _) && failure != null && !failure.Success && failure.Message == "距离太远，请靠近" && !g.Tools.Busy, "Distant beach pickup reports failure without action"); }
            finally { g.Tools.Feedback -= observe; }
            var position = pickup.transform.position; Move(g, position + new Vector3(0, .1f, -1));
            Motion(g, ToolActionKind.Harvest, position, () => g.Tools.TryBeachPickup(pickup, out _));
            Require(!g.Gathering.Owns(pickup), "Pickup commits once before visual contact");
            plot = g.Farm.Grid.CellCenter(20); Move(g, plot + new Vector3(0, .1f, -1)); g.Tools.SelectTool(FarmTool.Hoe);
            Require(g.Tools.TryAct(20, out _), "Low-frame-rate action fixture");
            Advance(g.Tools, g.Configuration.Config.ToolActionDuration * 1.01f);
            Require(!g.Tools.Busy && g.Tools.Presentation.ActiveEffectCount == 10 && !g.Players.Player.ToolActionLocked && !g.Players.Player.Animation.Action.HasValue,
                "Crossing contact and completion in one frame emits once and releases pose/movement");
            Advance(g.Tools, 0); Require(g.Tools.Presentation.ActiveEffectCount == 10, "Completed action cannot emit again"); Finish(g);
            g.Tools.SelectTool(FarmTool.Hoe); g.Interactions.ClearTarget();
            Require(!File.Exists(g.Saves.SavePath), "Interaction polish cannot create daytime saves");
            File.WriteAllText("Logs/InteractionExperienceVerification.txt", "PASS: packed AA; 120 identical hover/UI refreshes retain target, crop text and outline without Changed notifications; live tool/range/growth/water/fertilizer/quality updates; feedback hold/fade/hide/replacement; fresh click checks; ray depth ordering; reentrant dispatch guard; target-specific failure deduplication; focus/UI clearing; shared crop hint; seven tool timelines plus beach pickup; smooth facing and fixed root; contact effects/resource reaction once; pause before contact; low-frame-rate contact/end; no daytime save writes.\n");
        }
        static void VerifyPointer(GameManager g)
        {
            var plot = g.Farm.Grid.CellCenter(24); Move(g, plot + new Vector3(0, .1f, -1));
            g.Tools.SelectTool(FarmTool.Seeds);
            Require(!g.Interactions.RefreshPointer(Down(plot), false).Ready, "Unprepared plot is initially unavailable");
            g.Tools.SelectTool(FarmTool.Hoe);
            int stamina = g.Stamina.State.Current;
            Require(g.Interactions.TryInteract() && g.Farm.State.Cells[24].Tilled && g.Stamina.State.Current == stamina - g.Configuration.Config.HoeStaminaCost,
                "Click rechecks changed tool even when cached readiness was false");
            Require(!g.Interactions.Current.Ready && g.Interactions.Current.Details == "正在操作，请稍候", "Successful click immediately publishes busy state"); Finish(g);

            plot = g.Farm.Grid.CellCenter(23); Move(g, plot + new Vector3(0, .1f, -1));
            g.Interactions.RefreshPointer(Down(plot), false);
            int attempts = 0; bool reentered = false;
            Action callback = () => { attempts++; reentered |= g.Interactions.TryInteract(); };
            g.Interactions.Changed += callback;
            try
            {
                g.Tools.SelectTool(FarmTool.Seeds);
                g.Interactions.RefreshPointer(Down(plot), false);
                Require(attempts > 0 && !reentered && !g.Tools.Busy && !g.Farm.State.Cells[23].Tilled, "Hover notifications cannot initiate a transaction");
                g.Tools.SelectTool(FarmTool.Hoe);
                Require(g.Interactions.TryInteract() && !reentered, "Target notifications cannot reenter dispatch");
            }
            finally { g.Interactions.Changed -= callback; }
            Finish(g);

            plot = g.Farm.Grid.CellCenter(22); Move(g, plot + new Vector3(0, .1f, -1));
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                blocker.transform.position = plot + Vector3.up; Physics.SyncTransforms();
                Require(!g.Interactions.RefreshPointer(Down(plot), false).Visible, "Foreground obstacle blocks plot");
                var behind = g.Interactions.RefreshPointer(new Ray(plot + Vector3.up * .01f, Vector3.up), false);
                Require(behind.Kind == InteractionKind.Farm && behind.Index == 22, "Obstacle behind the grid plane cannot steal plot selection");
            }
            finally { Object.DestroyImmediate(blocker); Physics.SyncTransforms(); }
            g.Interactions.RefreshPointer(Down(plot), false); Move(g, plot + Vector3.forward * 6);
            int failures = 0; ToolFeedback feedback = null;
            Action<ToolFeedback> observe = value => { if (!value.Success) { failures++; feedback = value; } };
            g.Tools.Feedback += observe;
            try
            {
                Require(!g.Interactions.TryInteract() && !g.Interactions.Current.InRange && feedback != null && feedback.Message == "距离太远，请靠近" && feedback.Detail == "农田地块", "Click refreshes range and identifies failed target");
                Require(!g.Interactions.TryInteract() && failures == 1, "Repeated identical failure is throttled");
                Capture(g, "InteractionDistanceFeedbackPreview");
            }
            finally { g.Tools.Feedback -= observe; }
            g.Interactions.RefreshPointer(Down(plot), true); Require(!g.Interactions.TryInteract(), "UI pointer cannot reuse the previous world ray");
            Move(g, plot + new Vector3(0, .1f, -1)); g.Interactions.RefreshPointer(Down(plot), false);
            g.Interactions.Controller.SendMessage("OnApplicationFocus", false);
            Require(!g.Interactions.Current.Visible && !g.Interactions.TryInteract(), "Losing focus invalidates pointer and click");
            Require(!g.Interactions.RefreshPointer(Down(plot), false).Visible, "Unfocused updates cannot republish a target");
            g.Interactions.Controller.SendMessage("OnApplicationFocus", true);

            g.Tools.SelectTool(FarmTool.FishingRod); plot = g.Farm.Grid.CellCenter(19);
            Require(g.Farm.TryAct(19, FarmTool.Hoe, out _) && g.Farm.TryAct(19, FarmTool.Seeds, out _), "Planted crop hint fixture");
            Move(g, plot + new Vector3(0, .1f, -1)); g.Interactions.RefreshPointer(Down(plot), false); g.UI.RefreshViews();
            var crop = Object.FindObjectOfType<CropInfoView>();
            Require(crop != null && crop.HintText.text == g.Interactions.Current.Hint && crop.HintText.text.Contains("鱼竿"), "Crop card uses the same failure hint as click");
            g.Tools.SelectTool(FarmTool.WateringCan); g.Interactions.RefreshPointer(Down(plot), false); g.UI.RefreshViews(); crop.HintText.ForceMeshUpdate(true);
            Require(g.Interactions.Current.Ready && crop.HintText.text == g.Interactions.Current.Operation && !crop.HintText.isTextOverflowing && !crop.HintText.text.Contains("\n"), "Ready crop shows one fitting operation line");
            Capture(g, "InteractionCropHintPreview");
        }
        static void VerifyRefreshes(GameManager g)
        {
            var crop = Controller<CropInfoController>(g, "CropInfoPanel");
            var view = Object.FindObjectOfType<CropInfoView>();
            var plot = g.Farm.Grid.CellCenter(19);
            var target = g.Interactions.RefreshPointer(Down(plot), false); g.UI.RefreshViews();
            string details = crop.Model.Details;
            var outline = g.Farm.Grid.transform.Find("Target outline"); outline.hasChanged = false;
            int notifications = 0; Action changed = () => notifications++;
            g.Interactions.Changed += changed;
            try
            {
                for (int i = 0; i < 120; i++)
                {
                    Require(ReferenceEquals(target, g.Interactions.RefreshPointer(Down(plot), false)), "Identical hover reuses the immutable live-checked target");
                    g.UI.RefreshViews();
                }
                Require(notifications == 0 && ReferenceEquals(details, crop.Model.Details) && outline.gameObject.activeSelf && !outline.hasChanged,
                    "Stable hover avoids target notifications, crop string rebuilding and outline transforms");
                g.Tools.SelectTool(FarmTool.FishingRod); g.Interactions.RefreshPointer(Down(plot), false); g.UI.RefreshViews();
                Require(notifications == 1 && crop.Model.Hint.Contains("鱼竿"), "Tool changes publish exactly once and refresh crop hint");
                // The rod's target text is unchanged by these crop mutations: the crop card must still poll its own data.
                var cell = g.Farm.State.Cells[19];
                int growth = cell.Growth; bool watered = cell.Watered, fertilized = cell.Fertilized; var quality = cell.Quality;
                cell.Growth = cell.RequiredGrowthDays; cell.Watered = true; cell.Fertilized = true; cell.Quality = CropQuality.Quality;
                g.UI.RefreshViews();
                Require(crop.Model.Details.Contains("已成熟") && crop.Model.Details.Contains("今天已浇水") && crop.Model.Details.Contains("本轮已施肥")
                    && crop.Model.Name == g.Inventory.Catalog.Find(g.Farm.State.HarvestId(19)).DisplayName && view.NameText.text == crop.Model.Name,
                    "In-place growth, water, fertilizer and quality changes update without a new pointer target");
                cell.Growth = growth; cell.Watered = watered; cell.Fertilized = fertilized; cell.Quality = quality; g.UI.RefreshViews();
                g.Interactions.ClearTarget(); int cleared = notifications; g.Interactions.ClearTarget();
                Require(notifications == cleared, "Repeated clear does not repeat notifications");
                g.UI.RefreshViews(); Require(view.Group.alpha == 0, "Cleared crop hides on the UI tick");
                g.Interactions.RefreshPointer(Down(plot), false); g.UI.RefreshViews();
                Require(view.Group.alpha == 1 && crop.Model.Details == details, "Reacquiring the crop restores current details");
            }
            finally { g.Interactions.Changed -= changed; }

            var feedback = Controller<FeedbackController>(g, "FeedbackPanel");
            var feedbackView = Object.FindObjectOfType<FeedbackView>();
            var show = typeof(FeedbackController).GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic);
            var remaining = typeof(FeedbackController).GetField("remaining", BindingFlags.Instance | BindingFlags.NonPublic);
            show.Invoke(feedback, new object[] { new ToolFeedback(false, "距离太远，请靠近", "农田地块") });
            remaining.SetValue(feedback, Time.unscaledDeltaTime + 1f);
            feedback.Tick(); Require(feedback.Model.Alpha == 1 && feedbackView.Group.alpha == 1, "Feedback hold stays fully visible");
            remaining.SetValue(feedback, Time.unscaledDeltaTime + .175f); feedback.Tick();
            Require(Mathf.Abs(feedbackView.Group.alpha - .5f) < .001f && feedbackView.DetailText.text == "农田地块", "Only opacity changes during fade");
            show.Invoke(feedback, new object[] { new ToolFeedback(true, "收获", "农田地块") });
            Require(feedbackView.Group.alpha == 1 && feedbackView.MessageText.text == "收获" && feedback.Model.Success, "New feedback replaces an active fade immediately");
            remaining.SetValue(feedback, Time.unscaledDeltaTime); feedback.Tick();
            for (int i = 0; i < 120; i++) feedback.Tick();
            Require(feedback.Model.Alpha == 0 && feedbackView.Group.alpha == 0, "Feedback reaches zero and remains hidden");
            feedback.Dismiss(); g.Tools.SelectTool(FarmTool.Hoe); g.Interactions.ClearTarget();
        }
        static void Motion(GameManager g, ToolActionKind kind, Vector3 target, Func<bool> act, bool pause = false, GatheringNode node = null)
        {
            g.Tools.Presentation.TickEffects(2); g.Gathering.TickVisuals(2);
            var player = g.Players.Player; Vector3 root = player.transform.position;
            g.Interactions.RefreshPointer(Down(target), false);
            Quaternion facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(target - root, Vector3.up));
            Quaternion initial = facing * Quaternion.Euler(0, 90, 0); player.Visual.rotation = initial;
            int sounds = g.Audio.PlayedSoundCount;
            Require(act() && g.Tools.Busy && player.ToolActionLocked && player.Animation.Action == kind, "Successful commit locks matching action " + kind);
            Require(g.Audio.PlayedSoundCount == sounds + 1 && g.Tools.Presentation.ActiveEffectCount == 0 && Quaternion.Angle(initial, player.Visual.rotation) < .01f, "Success acknowledgement is immediate; facing and particles wait for action progress");
            if (node != null) Require(!node.Art.IsReacting, "Resource reaction waits for contact");
            float duration = g.Configuration.Config.ToolActionDuration;
            Advance(g.Tools, duration * .1f);
            Require(Quaternion.Angle(player.Visual.rotation, facing) > 1 && Quaternion.Angle(player.Visual.rotation, facing) < 89 && g.Tools.Presentation.ActiveEffectCount == 0, "Facing blends during windup without early particles");
            if (pause) Capture(g, "InteractionWindupPreview");
            if (pause)
            {
                g.ToggleInventory(); float progress = g.Tools.ActionProgress; Quaternion rotation = player.Visual.rotation;
                Advance(g.Tools, duration * 2);
                Require(g.UI.IsInventoryVisible && Mathf.Approximately(progress, g.Tools.ActionProgress) && Quaternion.Angle(rotation, player.Visual.rotation) < .01f && g.Tools.Presentation.ActiveEffectCount == 0, "Modal pause freezes turn and pending contact");
                g.CloseInventory();
            }
            Advance(g.Tools, duration * .1f);
            Require(Quaternion.Angle(player.Visual.rotation, facing) < .01f && g.Tools.Presentation.ActiveEffectCount == 0, "Turn finishes before contact");
            Advance(g.Tools, duration * .31f);
            Require(g.Tools.Presentation.ActiveEffectCount > 0 && Mathf.Abs(player.Animation.ActionProgress - g.Tools.ActionProgress) < .001f && player.transform.position == root, "Contact emits effects with matching tool/rig progress and stationary root");
            if (node != null) Require(node.Art.IsReacting, "Resource reaction starts with contact particles");
            if (pause) Capture(g, "InteractionContactPreview");
            int particles = g.Tools.Presentation.ActiveEffectCount; Advance(g.Tools, 0);
            Require(g.Tools.Presentation.ActiveEffectCount == particles, "Contact cannot emit twice");
            Finish(g); Require(!g.Tools.Busy && !player.ToolActionLocked && !player.Animation.Action.HasValue, "Recovery releases action and movement");
        }
    }
}
