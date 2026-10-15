using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class InteractionPromptFlowVerification
    {
        const string Key = "MagicIsland.InteractionPromptVerification";
        static InteractionManager fixture;
        static InteractionPromptFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            var paths = new[] { InteractionPromptSetup.HighlightPath, "Assets/Art/Prefabs/UI/InteractionPromptPanel.prefab", "Assets/Art/Materials/Effects/InteractionRing.mat", "Assets/Config/GameConfig.asset" };
            var bytes = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Repeat setup preserves art and configuration");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool condition, string why) { if (!condition) throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key + ".Phase", p); Debug.Log("Interaction prompt phase " + p); }
        static InteractionPromptView Prompt => Object.FindObjectOfType<InteractionPromptView>();
        static Ray Down(Vector3 point) => new Ray(point + Vector3.up * 8, Vector3.down);
        static void Move(GameManager g, Vector3 point)
        { g.Players.RestorePosition(point); g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget(); Physics.SyncTransforms(); }
        static InteractionTarget Point(GameManager g, Component actor, InteractionKind kind, bool approach = true)
        {
            if (approach) Move(g, actor is FishingSpot spot ? spot.Position + new Vector3(-.5f, .1f, 0) : actor.transform.position + new Vector3(0, .1f, -1.6f));
            foreach (var collider in actor.GetComponentsInChildren<Collider>().Where(c => c.enabled && c.gameObject.activeInHierarchy))
            {
                var target = g.Interactions.RefreshPointer(Down(collider.bounds.center), false);
                if (target.Kind == kind && target.Root == actor.transform) return target;
            }
            throw new Exception("Cannot target owned " + kind + " / " + actor.name + ": " + g.Interactions.Current.Kind);
        }
        static void Capture(GameManager g, string name)
        { g.UI.RefreshViews(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void OutdoorTargets(GameManager g)
        {
            var panel = Prompt;
            Require(panel != null && panel.Layer == UILayer.Common && panel.transform.parent == g.UI.GetLayerRoot(UILayer.Common), "Prompt uses AA MVC and Common layer");
            Require(!panel.Group.interactable && !panel.Group.blocksRaycasts && panel.GetComponentsInChildren<Graphic>(true).All(x => !x.raycastTarget), "Prompt cannot intercept mouse");
            Require(panel.GetComponentsInChildren<TMPro.TMP_Text>().All(x => x.font != null && x.font.HasCharacter('岛')), "Prompt supports Chinese TMP");
            Require(g.Interactions.OwnsHighlight && g.Interactions.Highlight.Ring.sharedMaterial != null && g.Interactions.Highlight.GetComponentsInChildren<Collider>(true).Length == 0, "Manager owns AA visual without colliders");
            var node = g.Gathering.Nodes[0]; g.Tools.SelectTool(FarmTool.Hoe);
            var target = Point(g, node, InteractionKind.Gathering);
            Require(target.InRange && !target.Ready && panel.DetailsText.text.Contains("斧头") && panel.Group.alpha == 1, "Wrong tool tells player which tool to use");
            int stamina = g.Stamina.State.Current, sounds = g.Audio.PlayedSoundCount;
            Require(!g.Interactions.TryInteract() && g.Stamina.State.Current == stamina && g.Audio.PlayedSoundCount == sounds, "Invalid click does not spend stamina or play success audio");
            g.Tools.SelectTool(FarmTool.Axe); target = Point(g, node, InteractionKind.Gathering);
            Require(target.Ready && panel.OperationText.text == "左键砍树" && g.Interactions.Highlight.Target == node.transform, "Ready tree shares prompt and highlight target");
            Capture(g, "InteractionTreePreview");
            Move(g, node.transform.position + new Vector3(0, .1f, 6)); target = Point(g, node, InteractionKind.Gathering, false);
            Require(!target.InRange && panel.StatusText.text == "请靠近" && !g.Interactions.TryInteract(), "Far target cannot be clicked");
            target = Point(g, node, InteractionKind.Gathering); var surface = node.WholeVisual.GetComponentsInChildren<Renderer>().First();
            g.Cameras.SetOcclusionEnabled(false); var original = surface.sharedMaterials;
            Point(g, node, InteractionKind.Gathering); g.Interactions.ClearTarget();
            Require(original.SequenceEqual(surface.sharedMaterials), "Highlight leaves model materials unchanged"); g.Cameras.SetOcclusionEnabled(true);
            g.Interactions.RefreshPointer(Down(node.transform.position), true);
            Require(!g.Interactions.Current.Visible && panel.Group.alpha == 0 && !g.Interactions.Highlight.gameObject.activeSelf, "Pointer over UI hides prompt and ring");
            foreach (var npc in g.NPCs.Actors.Values) Require(Point(g, npc, InteractionKind.NPC).Name == g.NPCs.Catalog.Find(npc.Id).DisplayName, "NPC display name comes from catalog");
            Point(g, g.ToolUpgrades.Workbench, InteractionKind.Workbench);
            Point(g, g.Cooking.Station, InteractionKind.Cooking);
            Point(g, g.Shipping.Box, InteractionKind.Shipping);
            Point(g, g.Storage.Chest, InteractionKind.Storage);
            Point(g, g.Shop.Counter.GetComponent<ShopCounter>(), InteractionKind.Shop);
            Point(g, g.Orders.Board, InteractionKind.Orders); Capture(g, "InteractionBoardPreview");
            Require(g.Inventory.TryAdd("wood", g.Configuration.Animals.BuildWood) && g.Inventory.TryAdd("stone", g.Configuration.Animals.BuildStone), "Coop materials fixture");
            g.Economy.TryEarn(g.Configuration.Animals.BuildGold); Require(g.Animals.Build(), "Actual AA coop fixture");
            Point(g, g.Animals.Coop, InteractionKind.Coop);
            var fishing = Point(g, g.Fishing.Spot, InteractionKind.Fishing);
            Require(!fishing.Ready && fishing.Details.Contains("鱼竿"), "Fishing needs a rod"); g.Tools.SelectTool(FarmTool.FishingRod);
            Require(Point(g, g.Fishing.Spot, InteractionKind.Fishing).Ready, "Selecting rod enables fishing");
            g.Tools.SelectTool(FarmTool.Hoe); Vector3 plot = g.Farm.Grid.CellCenter(0); Move(g, plot + new Vector3(0, .1f, -1));
            target = g.Interactions.RefreshPointer(Down(plot), false);
            Require(target.Kind == InteractionKind.Farm && target.Index == 0 && target.Ready && panel.Group.alpha == 1 && !g.Interactions.Highlight.gameObject.activeSelf, "Empty farm retains its existing grid outline");
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                blocker.transform.position = plot + Vector3.up; Physics.SyncTransforms();
                Require(!g.Interactions.RefreshPointer(Down(plot), false).Visible, "Unmarked solid obstacle blocks farm behind it");
            }
            finally { Object.DestroyImmediate(blocker); }
            int mask = g.Players.Player.ViewCamera.cullingMask;
            g.Players.Player.ViewCamera.cullingMask &= ~(1 << g.Farm.Grid.gameObject.layer);
            Require(!g.Interactions.RefreshPointer(Down(plot), false).Visible, "Farm respects camera layer mask"); g.Players.Player.ViewCamera.cullingMask = mask;
            g.Interactions.RefreshPointer(Down(plot), false); Require(g.Interactions.TryInteract(), "Unified click tills the selected farm plot");
            Require(!g.Interactions.TryInteract(), "Action gate blocks repeat click");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 17 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Interaction timeout " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Game initialization");
                if (p == 0 && g.State == GameFlowState.MainMenu) { g.EnterGame(); Phase(1); }
                else if (p == 1 && g.State == GameFlowState.Playing) { OutdoorTargets(g); Phase(2); }
                else if (p == 2 && !g.Tools.Busy)
                {
                    g.Tools.SelectTool(FarmTool.Seeds); Move(g, g.Farm.Grid.CellCenter(0) + new Vector3(0, .1f, -1));
                    g.Interactions.RefreshPointer(Down(g.Farm.Grid.CellCenter(0)), false); Require(g.Interactions.TryInteract(), "Unified farm planting"); Phase(3);
                }
                else if (p == 3 && !g.Tools.Busy)
                {
                    g.Interactions.RefreshPointer(Down(g.Farm.Grid.CellCenter(0)), false); g.UI.RefreshViews();
                    Require(Prompt.Group.alpha == 0 && g.Interactions.Controller.HoverIndex == 0, "Planted farm uses CropInfo without duplicate prompt");
                    Point(g, g.Storage.Chest, InteractionKind.Storage); Require(g.Interactions.TryInteract(), "Unified storage click dispatch");
                    Require(!g.Interactions.Current.Visible && Prompt.Group.alpha == 0, "Modal opening clears target before AA panel finishes loading"); Phase(4);
                }
                else if (p == 4 && Object.FindObjectOfType<StorageView>() != null)
                {
                    Require(g.UI.AnyPopupVisible && !g.Interactions.RefreshPointer(Down(g.Storage.Chest.transform.position), false).Visible && !g.Interactions.TryInteract(), "Open popup blocks world ray and click");
                    g.CloseStorage(); g.ToggleInventory(); Phase(5);
                }
                else if (p == 5 && g.UI.IsInventoryVisible)
                {
                    Require(!g.Interactions.RefreshPointer(Down(g.Gathering.Nodes[0].transform.position), false).Visible, "Inventory also suppresses world interactions"); g.CloseInventory();
                    Point(g, g.Gathering.Nodes[0], InteractionKind.Gathering); g.Tools.SelectTool(FarmTool.Axe); Point(g, g.Gathering.Nodes[0], InteractionKind.Gathering);
                    int sounds = g.Audio.PlayedSoundCount;
                    Require(g.Interactions.TryInteract() && g.Tools.Busy && g.Audio.PlayedSoundCount == sounds + 1, "One gathering click commits and plays one sound"); Phase(6);
                }
                else if (p == 6 && !g.Tools.Busy)
                {
                    Point(g, g.Housing.Entrance, InteractionKind.Home); Move(g, g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f));
                    Point(g, g.Housing.Entrance, InteractionKind.Home, false); Require(g.Interactions.TryInteract(), "Home entrance dispatch"); Phase(7);
                }
                else if (p == 7 && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    var bed = g.Housing.Interior.Bed; Point(g, bed, InteractionKind.Bed);
                    Require(Prompt.DetailsText.text.Contains("保存") && g.Interactions.Highlight.gameObject.layer == g.Configuration.Housing.InteriorLayer, "Indoor bed explains sleep save and uses interior layer");
                    Capture(g, "InteractionBedPreview"); var exit = g.Housing.Interior.Exit;
                    Move(g, exit.Position + new Vector3(0, .1f, 1.2f)); Point(g, exit, InteractionKind.Home, false);
                    Require(g.Interactions.TryInteract(), "Interior exit dispatch"); Phase(8);
                }
                else if (p == 8 && g.State == GameFlowState.Playing && !g.Housing.Inside) { g.ReturnToMainMenu(); Phase(9); }
                else if (p == 9 && Object.FindObjectOfType<PauseView>() != null) { Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(10); }
                else if (p == 10 && g.State == GameFlowState.MainMenu)
                {
                    Require(!g.Interactions.OwnsHighlight && g.Interactions.Highlight == null && Prompt == null && Object.FindObjectsOfType<InteractionHighlight>(true).Length == 0, "Menu return releases AA prompt and ring");
                    fixture = new GameObject("Highlight lifecycle fixture").AddComponent<InteractionManager>(); fixture.StartCoroutine(fixture.LoadHighlight("Interaction_Verification_Missing")); Phase(11);
                }
                else if (p == 11 && fixture.LastError != null)
                {
                    Require(!fixture.OwnsHighlight && fixture.Highlight == null, "Failed AA highlight load releases handle");
                    fixture.StartCoroutine(fixture.LoadHighlight(g.Configuration.Config.InteractionHighlightAddress)); fixture.Clear(); Phase(12);
                }
                else if (p == 12)
                { Require(!fixture.OwnsHighlight && fixture.Highlight == null, "Cancelled load cannot bind late highlight"); Phase(13); }
                else if (p == 13)
                { Require(fixture.GetComponentsInChildren<InteractionHighlight>(true).Length == 0, "Cancelled instance released after completion"); Object.Destroy(fixture.gameObject); g.EnterGame(); Phase(14); }
                else if (p == 14 && g.State == GameFlowState.Playing)
                {
                    Require(g.Interactions.OwnsHighlight && Object.FindObjectsOfType<InteractionHighlight>(true).Length == 1 && Prompt != null, "Re-entry owns exactly one fresh ring and panel");
                    Point(g, g.Orders.Board, InteractionKind.Orders); Capture(g, "InteractionBoardPreview");
                    g.Tools.SelectTool(FarmTool.Axe); Point(g, g.Gathering.Nodes[0], InteractionKind.Gathering); Capture(g, "InteractionTreePreview");
                    string item = g.Configuration.Crafting.Recipes.First(r => r.IsPlaceable && !r.IsFurniture).OutputItemId;
                    Require(g.Inventory.TryAdd(item, 1), "Prepare one real decoration"); g.Placements.StartCoroutine(g.Placements.Begin(item)); Phase(15);
                }
                else if (p == 15 && g.Placements.Ready)
                {
                    bool placed = false;
                    for (int x = -8; x <= 8 && !placed; x++) for (int z = -8; z <= 8 && !placed; z++)
                    {
                        Move(g, new Vector3(x, .1f, z - 1.6f)); g.Placements.SetCandidate(x, z);
                        if (g.Placements.Result == PlacementResult.Success) placed = g.Placements.Confirm();
                    }
                    Require(placed, "Place actual AA decoration fixture"); var decoration = g.Placements.Objects.Values.Single();
                    var target = Point(g, decoration, InteractionKind.Decoration);
                    Require(target.Operation == "右键回收" && !g.Interactions.TryInteract(), "Furniture only responds to its displayed right click");
                    string item = g.Placements.State.Find(decoration.Id).ItemId; long count = g.Inventory.GetCount(item);
                    Require(g.Interactions.TryInteract(true) && g.Placements.Objects.Count == 0, "Right click returns decoration to inventory");
                    Require(g.Inventory.GetCount(item) == count + 1, "Recovery returns exactly one item");
                    Require(!g.Interactions.Current.Visible && Prompt.Group.alpha == 0, "Recovered target no longer displays prompt"); Phase(16);
                }
                else if (p == 16)
                {
                    Require(!g.Interactions.Highlight.gameObject.activeSelf, "Recovered object clears ring in next visual update");
                    File.WriteAllText("Logs/InteractionPromptVerification.txt", "PASS: packed AA, MVC/layers/TMP, read-only readiness, unified targets/clicks, range/tools, UI/modal blocking, farm occlusion/layers, outdoor targets, home/bed/exit, audio gate, artist preservation, unload/re-entry and failed/cancelled loads.\n");
                    Debug.Log("Interaction prompt flow verification PASSED."); Phase(17); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.isPlaying = false; EditorApplication.Exit(1); }
        }
    }
}
