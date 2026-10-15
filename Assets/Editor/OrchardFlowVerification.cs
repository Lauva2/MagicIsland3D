using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class OrchardFlowVerification
    {
        const string Key = "MagicIsland.OrchardVerification";
        static OrchardFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            var paths = Directory.GetFiles(OrchardSetup.Folder, "*.prefab").Concat(Directory.GetFiles(OrchardSetup.Materials, "*.mat"))
                .Concat(new[] { OrchardSetup.ConfigPath, "Assets/Config/ItemCatalog.asset", "Assets/Config/CraftingCatalog.asset", "Assets/Art/Prefabs/UI/ShopPanel.prefab" }).ToArray();
            var bytes = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Setup preserves orchard art/config/shop " + paths[i]);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup); Phase(0); EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key + ".Phase", p); SessionState.SetFloat(Key + ".PhaseTime", (float)EditorApplication.timeSinceStartup); Debug.Log("Orchard phase " + p); }
        static T View<T>() where T : Component => Object.FindObjectOfType<T>();
        static void Move(GameManager g, Vector3 point) { g.Players.RestorePosition(point); g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget(); Physics.SyncTransforms(); }
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir, g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting, g.Configuration.Animals, g.Configuration.Orchards);
        static void SameSave(GameManager g) { string saved = SessionState.GetString(Key + ".Saved", ""); Require(saved.Length == 0 ? !File.Exists(g.Saves.SavePath) : File.ReadAllText(g.Saves.SavePath) == saved, "Only sleep can write orchard progress"); }
        static int Bag(GameManager g, string id) => Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == id);
        static int TreeId(GameManager g, string sapling) => g.Placements.State.Capture().Single(p => p.ItemId == sapling).Id;
        static PlacedDecoration Tree(GameManager g, string sapling) => g.Placements.Objects[TreeId(g, sapling)];
        static void Begin(GameManager g, string sapling)
        { g.ToggleInventory(); var view = View<InventoryView>(); view.Slots[Bag(g, sapling)].SelectButton.onClick.Invoke(); Require(view.PlaceButton.interactable, "Sapling uses inventory MVC placement"); view.PlaceButton.onClick.Invoke(); }
        static void Place(GameManager g, int x, int z)
        { Move(g, new Vector3(x, .1f, z - 1.4f)); g.Placements.SetCandidate(x, z); Require(g.Placements.Result == PlacementResult.Success, "Sapling placement candidate " + g.Placements.Describe(g.Placements.Result)); g.ConfirmPlacement(); SameSave(g); }
        static void Home(GameManager g) { Move(g, g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); g.EnterHome(); }
        static void Rest(GameManager g) { Move(g, g.Configuration.Housing.InteriorOrigin + new Vector3(-1.5f, .1f, 2)); g.RequestRest(); }
        static void ExitHome(GameManager g) { Move(g, g.Configuration.Housing.InteriorOrigin + new Vector3(0, .1f, -3)); g.ExitHome(); }
        static void Sleep(GameManager g)
        {
            View<SleepView>().ConfirmButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var save) && save.Version == MagicIsland.Saving.SaveData.CurrentVersion && save.Day == g.Farm.State.Day && save.Orchard.Length == 2, "Actual sleep saves current orchard with world");
            SessionState.SetString(Key + ".Saved", File.ReadAllText(g.Saves.SavePath)); g.StartNewDayFromReport();
        }
        static void Harvest(GameManager g, string sapling)
        {
            var tree = Tree(g, sapling); Move(g, tree.transform.position + new Vector3(0, .1f, -1.4f));
            var target = g.Interactions.RefreshPointer(new Ray(tree.transform.position + Vector3.up * 6, Vector3.down), false);
            Require(target.Kind == InteractionKind.Orchard && target.Ready && target.InRange && g.Interactions.TryInteract(), "Actual fruit-tree mouse harvest"); SameSave(g);
        }
        static void Capture(GameManager g, string name)
        {
            var camera = g.Players.Player.ViewCamera; float before = camera.orthographicSize; int mask = camera.cullingMask; camera.cullingMask = ~ (1 << g.Configuration.Housing.InteriorLayer); camera.orthographicSize = 3.3f; camera.GetComponent<CameraController>().SnapToTarget();
            camera.transform.position += new Vector3(-3.5f, 1.0f, 6) - (g.Players.Player.transform.position + Vector3.up * .6f);
            var canvases = Object.FindObjectsOfType<Canvas>(); var enabled = canvases.Select(c => c.enabled).ToArray(); var renderers = g.Players.Player.GetComponentsInChildren<Renderer>(true); var visible = renderers.Select(r => r.enabled).ToArray();
            g.Cameras.SetOcclusionEnabled(false); bool ring = g.Interactions.Highlight.Ring.enabled;
            try { foreach (var c in canvases) c.enabled = false; foreach (var r in renderers) r.enabled = false; g.Interactions.Highlight.Ring.enabled = false; GameUiPreview.CaptureInventory(camera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
            finally { for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = enabled[i]; for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible[i]; g.Interactions.Highlight.Ring.enabled = ring; camera.orthographicSize = before; camera.cullingMask = mask; g.Cameras.SetOcclusionEnabled(true); }
        }
        static void TextCheck(Component root)
        { foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) { t.ForceMeshUpdate(true); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Orchard TMP layout " + t.name + " " + t.text); } }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 40 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".PhaseTime", 0) < .2) return;
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 300) throw new Exception("Orchard timeout " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Orchard startup");
                if (p == 1 && g.State == GameFlowState.MainMenu) throw new Exception("Orchard world initialization returned to menu");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                { string dir = Path.GetFullPath("Logs/OrchardSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SaveDirectory(g, dir); SessionState.SetString(Key + ".Saved", ""); g.EnterGame(); Phase(1); }
                else if (p == 1 && g.State == GameFlowState.Playing)
                { Require(g.Configuration.Orchards.Types.Count == 2 && g.Orchard.State.Count == 0, "Orchard AA configuration and empty new game"); g.Economy.Restore(1000); Move(g, g.Shop.Counter.position + new Vector3(0, .1f, -1.4f)); g.OpenShop(); Phase(2); }
                else if (p == 2 && View<ShopView>() != null)
                {
                    for (int i = 8; i < 10; i++) { View<ShopView>().CropButtons[i].onClick.Invoke(); View<ShopView>().BuyButton.onClick.Invoke(); TextCheck(View<ShopView>()); }
                    Require(g.Inventory.GetCount("apple_sapling") == 1 && g.Inventory.GetCount("orange_sapling") == 1 && g.Economy.Gold == 670, "Shop MVC buys two sapling types at configured prices");
                    GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/OrchardShopPreview.png", true); SameSave(g); g.CloseShop(); Begin(g, "apple_sapling"); Phase(3);
                }
                else if (p == 3 && g.Placements.Ready && g.UI.HasPlacement)
                { g.Placements.SetCandidate(0, 0); Require(g.Placements.Result == PlacementResult.FarmReserved, "Saplings cannot occupy crops"); Place(g, -5, 6); Require(g.Orchard.State.Count == 1 && g.Inventory.GetCount("apple_sapling") == 0, "Placement records growth and consumes sapling once"); Begin(g, "orange_sapling"); Phase(4); }
                else if (p == 4 && g.Placements.Ready && g.UI.HasPlacement)
                {
                    g.Placements.SetCandidate(-4, 6); Require(g.Placements.Result == PlacementResult.Occupied, "Fruit trees reserve growth clearance"); Place(g, -2, 6);
                    Require(g.Orchard.State.Count == 2 && Object.FindObjectsOfType<OrchardActor>().Length == 2 && g.Placements.Objects.Values.All(a => a.GetComponent<OrchardActor>().Sapling.activeSelf && a.GetComponentsInChildren<Collider>(true).Length == 1), "Two packed-AA saplings with original placement collision rule"); Capture(g, "OrchardSaplingPreview"); Home(g); Phase(5);
                }
                else if (p == 5 && g.State == GameFlowState.Playing && g.Housing.Inside) { Rest(g); Phase(6); }
                else if (p == 6 && View<SleepView>() != null)
                { Sleep(g); Require(g.Orchard.State.Capture().All(t => g.Orchard.State.Stage(t.PlacementId) == 1), "Sleep grows young trees"); Capture(g, "OrchardYoungPreview"); g.ReturnToMainMenu(); Phase(7); }
                else if (p == 7 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(8); }
                else if (p == 8 && g.State == GameFlowState.MainMenu) { Require(g.Orchard.State == null && Object.FindObjectsOfType<OrchardActor>().Length == 0, "Menu releases orchard actors/state"); SameSave(g); g.ContinueGame(); Phase(9); }
                else if (p == 9 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Orchard.State.Count == 2 && g.Placements.Objects.Values.All(a => a.GetComponent<OrchardActor>().Young.activeSelf), "Partial growth checkpoint reconstructs AA stage"); Rest(g); Phase(10); }
                else if (p == 10 && View<SleepView>() != null)
                { Sleep(g); if (g.Farm.State.Day == 3) Rest(g); else { Require(g.Farm.State.Day == 4 && g.Placements.Objects.Values.All(a => a.GetComponent<OrchardActor>().Fruit.activeSelf), "Third sleep makes mature fruit ready"); g.ReturnToMainMenu(); Phase(11); } }
                else if (p == 11 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(12); }
                else if (p == 12 && g.State == GameFlowState.MainMenu) { SameSave(g); g.ContinueGame(); Phase(13); }
                else if (p == 13 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Placements.Objects.Values.All(a => a.GetComponent<OrchardActor>().Fruit.activeSelf), "Ready fruit survives checkpoint reload"); ExitHome(g); Phase(14); }
                else if (p == 14 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                {
                    Capture(g, "OrchardRipePreview"); var tree = Tree(g, "apple_sapling"); Move(g, Vector3.zero); Require(!g.HarvestTree(tree), "Distant harvest rejected");
                    Move(g, tree.transform.position + new Vector3(0, .1f, -1.4f)); int fill = (g.Inventory.State.Capacity - 1) * 99; Require(g.Inventory.TryAdd("wood", fill), "Fill bag fixture");
                    Require(!g.HarvestTree(tree) && tree.GetComponent<OrchardActor>().Fruit.activeSelf && g.Orchard.State.Find(tree.Id).NextFruitDay == 4, "Full bag retains ripe fruit"); Require(g.Inventory.TryRemove("wood", fill), "Remove fill fixture");
                    Harvest(g, "apple_sapling"); Require(g.Inventory.GetCount("apple") == 2 && !tree.GetComponent<OrchardActor>().Fruit.activeSelf && !g.HarvestTree(tree), "One harvest gives two apples and blocks repeat"); Phase(15);
                }
                else if (p == 15 && !g.Tools.Busy) { Harvest(g, "orange_sapling"); Require(g.Inventory.GetCount("orange") == 2, "Orange harvest uses its own item"); Phase(16); }
                else if (p == 16 && !g.Tools.Busy) { Capture(g, "OrchardHarvestedPreview"); Move(g, g.Shop.Counter.position + new Vector3(0, .1f, -1.4f)); g.OpenShop(); Phase(17); }
                else if (p == 17 && View<ShopView>() != null)
                { View<ShopView>().CropButtons[8].onClick.Invoke(); View<ShopView>().SellButton.onClick.Invoke(); Require(g.Inventory.GetCount("apple") == 1 && g.Economy.Gold == 710, "Fruit sale uses configured price"); SameSave(g); g.CloseShop(); Move(g, g.Storage.Chest.transform.position + new Vector3(0, .1f, -1.4f)); g.OpenStorage(); Phase(18); }
                else if (p == 18 && View<StorageView>() != null)
                { g.TransferStorage(true, Bag(g, "apple"), 1); Require(g.Storage.State.Contents.GetCount("apple") == 1, "Fruit can be stored"); int slot = Enumerable.Range(0, g.Storage.State.Contents.Capacity).First(i => g.Storage.State.Contents.Slots[i].ItemId == "apple"); g.TransferStorage(false, slot, 1); Require(g.Inventory.GetCount("apple") == 1, "Fruit can be withdrawn"); SameSave(g); g.CloseStorage(); Home(g); Phase(19); }
                else if (p == 19 && g.State == GameFlowState.Playing && g.Housing.Inside) { Rest(g); Phase(20); }
                else if (p == 20 && View<SleepView>() != null)
                { Sleep(g); Require(g.Farm.State.Day == 5 && g.Orchard.State.Capture().All(t => t.NextFruitDay == 6) && g.Placements.Objects.Values.All(a => !a.GetComponent<OrchardActor>().Fruit.activeSelf), "Harvest cooldown is saved without premature fruit"); g.ReturnToMainMenu(); Phase(21); }
                else if (p == 21 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(22); }
                else if (p == 22 && g.State == GameFlowState.MainMenu) { SameSave(g); g.ContinueGame(); Phase(23); }
                else if (p == 23 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Inventory.GetCount("apple") == 1 && g.Inventory.GetCount("orange") == 2 && g.Placements.Objects.Values.All(a => !a.GetComponent<OrchardActor>().Fruit.activeSelf), "Cooldown reload cannot duplicate harvest"); Rest(g); Phase(24); }
                else if (p == 24 && View<SleepView>() != null)
                { Sleep(g); Require(g.Farm.State.Day == 6 && g.Placements.Objects.Values.All(a => a.GetComponent<OrchardActor>().Fruit.activeSelf) && g.Inventory.GetCount("apple") == 1, "Fruit regrows after two sleeps without automatic items"); ExitHome(g); Phase(25); }
                else if (p == 25 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                {
                    var tree = Tree(g, "apple_sapling"); Move(g, tree.transform.position + new Vector3(0, .1f, -1.4f)); var target = g.Interactions.RefreshPointer(new Ray(tree.transform.position + Vector3.up * 6, Vector3.down), false);
                    Require(target.Kind == InteractionKind.Orchard && g.Interactions.TryInteract(true) && g.Orchard.State.Count == 1 && g.Inventory.GetCount("apple_sapling") == 1 && g.Inventory.GetCount("apple") == 1, "Right-click recovery clears growth and discards unpicked fruit"); SameSave(g); Begin(g, "apple_sapling"); Phase(26);
                }
                else if (p == 26 && g.Placements.Ready && g.UI.HasPlacement)
                { Place(g, -5, 6); Require(g.Orchard.State.Count == 2 && g.Orchard.State.Find(TreeId(g, "apple_sapling")).PlantedDay == 6 && Tree(g, "apple_sapling").GetComponent<OrchardActor>().Sapling.activeSelf, "Replant restarts tree growth"); Capture(g, "OrchardReplantPreview"); g.ReturnToMainMenu(); Phase(27); }
                else if (p == 27 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(28); }
                else if (p == 28 && g.State == GameFlowState.MainMenu) { SameSave(g); g.EnterGame(); Phase(29); }
                else if (p == 29 && g.State == GameFlowState.Playing)
                { Require(g.Orchard.State.Count == 0 && g.Placements.State.Count == 0 && Object.FindObjectsOfType<OrchardActor>().Length == 0, "Fresh game resets orchard without replacing sleep save"); SameSave(g); g.ReturnToMainMenu(); Phase(30); }
                else if (p == 30 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(31); }
                else if (p == 31 && g.State == GameFlowState.MainMenu)
                { SameSave(g); Require(g.Orchard.State == null && Object.FindObjectsOfType<OrchardActor>().Length == 0, "Orchard cleanup complete"); File.WriteAllText("Logs/OrchardVerification.txt", "PASS: preserved config/shop/art, packed-AA orchard setup, actual shop sapling purchase, bag MVC placement/spacing/farm guards, sleep growth and stage restore, ripe/cooldown reload, mouse harvest/range/full bag/repeat guards, fruit sale/storage, regrowth, recovery/replant reset, sleep-only current-version checkpoints and fresh-game/resource cleanup.\n"); Debug.Log("Orchard flow verification PASSED."); Phase(40); EditorApplication.isPlaying = false; }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.isPlaying = false; EditorApplication.Exit(1); }
        }
    }
}
