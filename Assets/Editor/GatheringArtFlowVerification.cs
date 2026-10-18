using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Farming;
using MagicIsland.Gathering;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.Tools;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class GatheringArtFlowVerification
    {
        const string Key = "MagicIsland.GatheringArtVerification";
        const int Tree = 0, Rock = 3;
        static Quaternion pausedRotation;
        static Vector3[] pausedParticles;
        static double pauseStart;
        static string colliderPose;
        static GatheringArtFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            var paths = new[] { "Assets/Art/Prefabs/Gathering/GatheringTree.prefab", "Assets/Art/Prefabs/Gathering/GatheringRock.prefab", FarmArtSetup.EffectPath, "Assets/Config/GatheringCatalog.asset" }
                .Concat(Directory.GetFiles(GatheringArtSetup.MaterialFolder, "*.mat")).Concat(Directory.GetFiles(GatheringArtSetup.MeshFolder, "*.asset")).ToArray();
            var bytes = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Repeated setup preserves resource art/configuration " + paths[i]);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup); Phase(0);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key + ".Phase", p); Debug.Log("Gathering art phase " + p); }
        static T View<T>() where T : Component => Object.FindObjectOfType<T>();
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir, g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting, g.Configuration.Animals);
        static void SameSave(GameManager g)
        { string saved = SessionState.GetString(Key + ".Saved", ""); Require(saved.Length == 0 ? !File.Exists(g.Saves.SavePath) : File.ReadAllText(g.Saves.SavePath) == saved, "Daytime collection cannot write sleep checkpoint"); }
        static void Move(GameManager g, Vector3 point)
        { g.Players.RestorePosition(point); g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget(); Physics.SyncTransforms(); }
        static string Colliders(GatheringNode node) => string.Join(";", node.GetComponentsInChildren<Collider>(true).Select(c => c.name + c.transform.position.ToString("F5") + c.transform.rotation.ToString("F5") + c.transform.lossyScale.ToString("F5")));
        static void Hit(GameManager g, int index, FarmTool tool)
        {
            var node = g.Gathering.Nodes[index]; Move(g, node.transform.position + new Vector3(0, .1f, -1.15f)); Require(g.Tools.SelectTool(tool), "Select gathering tool");
            var target = g.Interactions.RefreshPointer(new Ray(node.transform.position + Vector3.up * 7, Vector3.down), false);
            Require(target.Kind == InteractionKind.Gathering && target.Index == index && target.InRange && g.Interactions.TryInteract(), "Real click dispatch to gathering resource");
            typeof(ToolManager).GetMethod("TickAction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(g.Tools, new object[] { g.Configuration.Config.ToolActionDuration * .52f });
            Require(g.Tools.Presentation.ActiveEffectCount > 0 && node.Art.IsReacting, "Successful contact plays resource reaction and AA fragments"); SameSave(g);
        }
        static void Capture(GameManager g, int index, string name, float size)
        {
            var node = g.Gathering.Nodes[index]; var camera = g.Players.Player.ViewCamera; float oldSize = camera.orthographicSize;
            camera.orthographicSize = size; camera.GetComponent<CameraController>().SnapToTarget();
            camera.transform.position += node.transform.position + Vector3.up * (index == Tree && node.WholeVisual.activeSelf ? 1.65f : .45f) - (g.Players.Player.transform.position + Vector3.up * .6f);
            var canvases = Object.FindObjectsOfType<Canvas>(); var enabled = canvases.Select(c => c.enabled).ToArray(); g.Cameras.SetOcclusionEnabled(false);
            var playerRenderers = g.Players.Player.GetComponentsInChildren<Renderer>(true); var visible = playerRenderers.Select(r => r.enabled).ToArray();
            var ring = g.Interactions.Highlight.Ring; bool ringEnabled = ring.enabled;
            try { foreach (var c in canvases) c.enabled = false; foreach (var r in playerRenderers) r.enabled = false; ring.enabled = false; GameUiPreview.CaptureInventory(camera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
            finally { for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = enabled[i]; for (int i = 0; i < playerRenderers.Length; i++) playerRenderers[i].enabled = visible[i]; ring.enabled = ringEnabled; camera.orthographicSize = oldSize; g.Cameras.SetOcclusionEnabled(true); }
        }
        static void CheckModels(GameManager g)
        {
            Require(g.Gathering.Nodes.Count == 5 && Object.FindObjectsOfType<GatheringNode>().Length == 5, "Exactly five original AA nodes");
            for (int i = 0; i < g.Gathering.Nodes.Count; i++)
            {
                var node = g.Gathering.Nodes[i]; Require(node.transform.localPosition == g.Configuration.Gathering.Nodes[i].Position && node.transform.localScale == Vector3.one, "Original resource position and scale");
                Require(node.Art != null && node.Art.ArtVersion == GatheringArtSetup.Version && node.Art.WholeMotion.GetComponentsInChildren<Collider>(true).Length == 0 && node.Art.DepletedMotion.GetComponentsInChildren<Collider>(true).Length == 0, "Independent collision-free resource art");
                Require(node.WholeVisual.GetComponentsInChildren<Collider>(true).Length == (i < 3 ? 4 : 2) && node.DepletedVisual.GetComponentsInChildren<Collider>(true).Length == 2, "Original collider count retained");
                Require(node.Art.WholeMotion.GetComponentsInChildren<MeshFilter>().All(m => m.sharedMesh != null && m.sharedMesh.normals.Length == m.sharedMesh.vertexCount), "Valid AA resource meshes");
                Require(node.WholeVisual.GetComponent<CameraOccluder>().Renderers.All(r => r.enabled && r.transform.IsChildOf(node.Art.WholeMotion)), "Occlusion binds visible art");
            }
            var tree = g.Gathering.Nodes[Tree]; var trunk = tree.WholeVisual.transform.Find("Trunk");
            Require(trunk.localPosition == new Vector3(0, 1.05f, 0) && trunk.localScale == new Vector3(.42f, 1.05f, .42f), "Original tree trunk collision footprint");
            Require(tree.WholeVisual.GetComponentsInChildren<Collider>().Count(c => c.isTrigger) == 3, "Original clickable trigger crowns retained");
            var effects = g.Tools.Presentation.EffectRoot.GetComponent<FarmActionEffects>(); Require(effects.IsValid && effects.WoodChip != effects.StoneChip, "Distinct AA wood and stone fragments");
            Move(g, tree.transform.position + new Vector3(2, .1f, -1.5f)); Capture(g, Tree, "GatheringOakArtPreview", 3.2f);
            Move(g, g.Gathering.Nodes[Rock].transform.position + new Vector3(2, .1f, -1.5f)); Capture(g, Rock, "GatheringBoulderArtPreview", 2.5f);
        }
        static void Home(GameManager g) { Move(g, g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); g.EnterHome(); }
        static void Rest(GameManager g) { Move(g, g.Configuration.Housing.InteriorOrigin + new Vector3(-1.5f, .1f, 2)); g.RequestRest(); }
        static void ExitHome(GameManager g) { Move(g, g.Configuration.Housing.InteriorOrigin + new Vector3(0, .1f, -3)); g.ExitHome(); }
        static void Sleep(GameManager g)
        {
            View<SleepView>().ConfirmButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var save) && save.Day == g.Farm.State.Day, "Actual sleep saves gathering state");
            SessionState.SetString(Key + ".Saved", File.ReadAllText(g.Saves.SavePath)); g.StartNewDayFromReport();
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 30 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Gathering art timeout " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Gathering art startup");
                if (p == 1 && g.State == GameFlowState.MainMenu) throw new Exception("Gathering art startup returned to menu");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                { string dir = Path.GetFullPath("Logs/GatheringArtSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SaveDirectory(g, dir); SessionState.SetString(Key + ".Saved", ""); g.EnterGame(); Phase(1); }
                else if (p == 1 && g.State == GameFlowState.Playing)
                {
                    CheckModels(g); var tree = g.Gathering.Nodes[Tree]; g.Tools.SelectTool(FarmTool.Axe); Move(g, Vector3.zero);
                    Require(!g.TryGather(Tree, out _) && g.Stamina.State.Current == 100 && g.Gathering.State.HitsRemaining(Tree) == 3, "Distant hit free");
                    Move(g, tree.transform.position + new Vector3(0, .1f, -1.15f)); g.Tools.SelectTool(FarmTool.Pickaxe);
                    Require(!g.TryGather(Tree, out _) && !tree.Art.IsReacting && g.Tools.Presentation.ActiveEffectCount == 0, "Wrong tool has no reaction/fragments");
                    colliderPose = Colliders(tree); Hit(g, Tree, FarmTool.Axe); Require(g.Gathering.State.HitsRemaining(Tree) == 2 && g.Stamina.State.Current == 96 && g.Inventory.GetCount("wood") == 0, "First tree hit commits only partial progress");
                    var art = g.Gathering.Nodes[Tree].Art; Require(art.IsReacting && Quaternion.Angle(art.WholeMotion.localRotation, Quaternion.identity) > .1f && Colliders(g.Gathering.Nodes[Tree]) == colliderPose, "Tree sways while every collider stays fixed");
                    Capture(g, Tree, "GatheringTreeHitPreview", 3.2f); g.ToggleInventory(); pausedRotation = art.WholeMotion.localRotation; pausedParticles = g.Tools.Presentation.EffectRoot.GetComponentsInChildren<Transform>().Select(t => t.position).ToArray(); pauseStart = EditorApplication.timeSinceStartup; Phase(3);
                }
                else if (p == 3 && EditorApplication.timeSinceStartup - pauseStart > .25)
                { Require(g.UI.IsInventoryVisible && g.Gathering.Nodes[Tree].Art.WholeMotion.localRotation == pausedRotation && pausedParticles.SequenceEqual(g.Tools.Presentation.EffectRoot.GetComponentsInChildren<Transform>().Select(t => t.position)), "Inventory freezes sway and flying fragments"); g.CloseInventory(); Phase(4); }
                else if (p == 4 && !g.Tools.Busy)
                {
                    Require(!g.Gathering.Nodes[Tree].Art.IsReacting && g.Gathering.Nodes[Tree].Art.WholeMotion.localRotation == Quaternion.identity, "Hit restores exact tree pose");
                    colliderPose = Colliders(g.Gathering.Nodes[Rock]); Hit(g, Rock, FarmTool.Pickaxe);
                    Require(g.Gathering.Nodes[Rock].Art.IsReacting && Colliders(g.Gathering.Nodes[Rock]) == colliderPose && g.Gathering.State.HitsRemaining(Rock) == 1 && g.Inventory.GetCount("stone") == 0, "Boulder reaction keeps collision and partial progress"); Capture(g, Rock, "GatheringRockHitPreview", 2.5f); Phase(6);
                }
                else if (p == 6 && !g.Tools.Busy) { Home(g); Phase(7); }
                else if (p == 7 && g.State == GameFlowState.Playing && g.Housing.Inside) { Rest(g); Phase(8); }
                else if (p == 8 && View<SleepView>() != null)
                { Sleep(g); Require(g.Farm.State.Day == 2 && g.Gathering.State.HitsRemaining(Tree) == 2 && g.Gathering.State.HitsRemaining(Rock) == 1, "Sleep keeps partial progress"); g.ReturnToMainMenu(); Phase(9); }
                else if (p == 9 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(10); }
                else if (p == 10 && g.State == GameFlowState.MainMenu) { SameSave(g); Require(g.Gathering.Nodes.Count == 0 && Object.FindObjectsOfType<GatheringNode>().Length == 0, "Unload releases AA node instances"); g.ContinueGame(); Phase(11); }
                else if (p == 11 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Gathering.State.HitsRemaining(Tree) == 2 && g.Gathering.State.HitsRemaining(Rock) == 1 && g.Gathering.Nodes[Tree].WholeVisual.activeSelf && !g.Gathering.Nodes[Tree].Art.IsReacting, "Partial checkpoint restores resource art without replaying hit"); ExitHome(g); Phase(12); }
                else if (p == 12 && g.State == GameFlowState.Playing && !g.Housing.Inside) { Hit(g, Tree, FarmTool.Axe); Phase(13); }
                else if (p == 13 && !g.Tools.Busy)
                { Hit(g, Tree, FarmTool.Axe); Require(g.Gathering.State.IsDepleted(Tree) && g.Inventory.GetCount("wood") == 3 && g.Gathering.Nodes[Tree].DepletedVisual.activeSelf, "Final hit grants original wood yield and detailed stump"); Capture(g, Tree, "GatheringStumpArtPreview", 2.0f); Phase(14); }
                else if (p == 14 && !g.Tools.Busy)
                { g.Tools.Presentation.TickEffects(2); int stamina = g.Stamina.State.Current; Require(!g.TryGather(Tree, out _) && g.Stamina.State.Current == stamina && g.Inventory.GetCount("wood") == 3 && g.Tools.Presentation.ActiveEffectCount == 0, "Depleted hit cannot pay or emit more fragments"); Hit(g, Rock, FarmTool.Pickaxe); Require(g.Gathering.State.IsDepleted(Rock) && g.Inventory.GetCount("stone") == 2, "Final rock hit gives original stone yield"); Capture(g, Rock, "GatheringFragmentsArtPreview", 2.0f); Phase(15); }
                else if (p == 15 && !g.Tools.Busy) { Home(g); Phase(16); }
                else if (p == 16 && g.State == GameFlowState.Playing && g.Housing.Inside) { Rest(g); Phase(17); }
                else if (p == 17 && View<SleepView>() != null)
                { Sleep(g); Require(g.Farm.State.Day == 3 && g.Gathering.State.RespawnDay(Tree) == 5 && g.Gathering.State.RespawnDay(Rock) == 4, "Original regeneration dates preserved"); g.ReturnToMainMenu(); Phase(18); }
                else if (p == 18 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(19); }
                else if (p == 19 && g.State == GameFlowState.MainMenu) { SameSave(g); g.ContinueGame(); Phase(20); }
                else if (p == 20 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Gathering.Nodes[Tree].DepletedVisual.activeSelf && g.Gathering.Nodes[Rock].DepletedVisual.activeSelf && g.Inventory.GetCount("wood") == 3 && g.Inventory.GetCount("stone") == 2, "Cooldown checkpoint restores stump and fragment art"); Rest(g); Phase(21); }
                else if (p == 21 && View<SleepView>() != null)
                {
                    Sleep(g); int day = g.Farm.State.Day; Require(g.Inventory.GetCount("wood") == 3 && g.Inventory.GetCount("stone") == 2, "Regeneration grants no free materials");
                    if (day == 4) { Require(g.Gathering.State.IsDepleted(Tree) && !g.Gathering.State.IsDepleted(Rock) && g.Gathering.Nodes[Rock].WholeVisual.activeSelf, "Stone regenerates on original day"); Rest(g); }
                    else { Require(day == 5 && !g.Gathering.State.IsDepleted(Tree) && g.Gathering.Nodes[Tree].WholeVisual.activeSelf && g.Gathering.Nodes[Tree].Art.WholeMotion.localRotation == Quaternion.identity && g.Gathering.Nodes[Tree].Art.DepletedMotion.localScale == Vector3.one, "Tree regenerates with exact resting pose"); ExitHome(g); Phase(22); }
                }
                else if (p == 22 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                { Move(g, g.Gathering.Nodes[Tree].transform.position + new Vector3(2, .1f, -1.5f)); Capture(g, Tree, "GatheringRegeneratedPreview", 3.2f); Hit(g, Tree, FarmTool.Axe); Require(g.Gathering.State.HitsRemaining(Tree) == 2, "Regenerated tree starts a new collection cycle"); Phase(23); }
                else if (p == 23 && !g.Tools.Busy) { SameSave(g); g.ReturnToMainMenu(); Phase(24); }
                else if (p == 24 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(25); }
                else if (p == 25 && g.State == GameFlowState.MainMenu)
                {
                    SameSave(g); Require(g.Gathering.State == null && g.Gathering.Nodes.Count == 0 && !g.Tools.OwnsEffects && Object.FindObjectsOfType<GatheringArtVisual>().Length == 0
                        && Resources.FindObjectsOfTypeAll<Material>().All(m => !m.name.EndsWith(" (Camera Fade)")), "Exit clears resource art, FX and fade material copies");
                    File.WriteAllText("Logs/GatheringArtVerification.txt", "PASS: packed AA editable oak/boulder art preservation, original positions/colliders, mouse/range/wrong-tool guards, partial and final collection, independent paused sway and wood/stone fragments, exact pose restore, sleep-only checkpoint plus partial/cooldown reload, original regeneration dates/yields and fresh collection, resource/FX/fade cleanup.\n");
                    Debug.Log("Gathering art flow verification PASSED."); Phase(30); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.isPlaying = false; EditorApplication.Exit(1); }
        }
    }
}
