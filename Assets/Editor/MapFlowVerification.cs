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
    [InitializeOnLoad] public static class MapFlowVerification
    {
        const string Key = "MagicIsland.MapVerification";
        static double waitStart;
        static Vector3 npcPosition;
        static MapFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            var paths = new[] { MapPanelSetup.Path, "Assets/Art/Prefabs/UI/FarmHudPanel.prefab", "Assets/Config/GameConfig.asset" }; var bytes = paths.Select(File.ReadAllBytes).ToArray();
            PrototypeAddressablesSetup.EnsureSetup(); for (int i = 0; i < paths.Length; i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Repeated setup preserves map, HUD and configuration");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup); Phase(0);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key + ".Phase", p); Debug.Log("Map phase " + p); }
        static T View<T>() where T : Component => Object.FindObjectOfType<T>();
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir, g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting, g.Configuration.Animals);
        static void SameSave(GameManager g) { string saved = SessionState.GetString(Key + ".Saved", ""); Require(saved.Length == 0 ? !File.Exists(g.Saves.SavePath) : File.ReadAllText(g.Saves.SavePath) == saved, "Map selection/open/close cannot save daytime progress"); }
        static void Move(GameManager g, Vector3 point) { g.Players.RestorePosition(point); g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget(); }
        static void TextCheck(MapView view)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>(true))
            { text.ForceMeshUpdate(true); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Map Chinese TMP/fit " + text.name + ": " + text.text); }
        }
        static void CheckView(GameManager g)
        {
            var view = View<MapView>(); Require(view.IsValid && view.Layer == UILayer.PopUp && view.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp) && Object.FindObjectsOfType<MapView>().Length == 1, "One correctly layered packed-AA MVC map");
            Require(!g.Players.Player.enabled && !g.Tools.InputEnabled && !g.Interactions.Controller.enabled && g.Interactions.Current.Kind == InteractionKind.None, "Map pauses all world input and clears target");
            Require(g.Map.Model.Locations.Count == 12 && g.Map.Model.Roads.Length == 8, "All current locations and configured village roads");
            Require(g.Map.Model.Locations.Single(p => p.Id == "shop").WorldPosition == g.Shop.Counter.position && g.Map.Model.Locations.Single(p => p.Id == "home").WorldPosition == g.Housing.Entrance.Position
                && g.Map.Model.Locations.Single(p => p.Id == "orders").WorldPosition == g.Orders.Board.transform.position && g.Map.Model.Locations.Single(p => p.Id == "fishing").WorldPosition == g.Fishing.Spot.transform.position, "Map reads actual scene positions");
            for (int i = 0; i < view.MarkerButtons.Length; i++)
            {
                var point = view.MarkerButtons[i].GetComponent<RectTransform>().anchoredPosition; var entry = g.Map.Model.Locations[i];
                Require(Vector2.Distance(point, new Vector2(entry.Position.x * view.MapArea.rect.width, (entry.Position.y - 1) * view.MapArea.rect.height)) < .01f, "Marker projects world location exactly");
            }
            TextCheck(view); Require(view.MapGraphic.canvasRenderer != null && view.MapGraphic.canvasRenderer.materialCount > 0, "Map background has an active CanvasRenderer and UI material"); SameSave(g);
        }
        static void Capture(GameManager g, string name)
        { g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 30 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 300) throw new Exception("Map timeout " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Map startup");
                if (p == 1 && g.State == GameFlowState.MainMenu) throw new Exception("Map world startup returned to menu");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                { Require(!g.Map.Ready, "Map unavailable on menu"); g.ToggleMap(); Require(!g.UI.HasMap, "Menu cannot open world map"); string dir = Path.GetFullPath("Logs/MapSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SaveDirectory(g, dir); SessionState.SetString(Key + ".Saved", ""); g.EnterGame(); Phase(1); }
                else if (p == 1 && g.State == GameFlowState.Playing)
                { Require(g.Map.Ready && View<PrototypeHud>().MapButton != null, "Map manager initialized after world AA and HUD entry wired"); View<PrototypeHud>().MapButton.onClick.Invoke(); Phase(2); }
                else if (p == 2 && View<MapView>() != null)
                {
                    CheckView(g); var view = View<MapView>(); Vector3 player = g.Players.Player.transform.position;
                    for (int i = 0; i < view.LocationButtons.Length; i++) { view.LocationButtons[i].onClick.Invoke(); Require(g.Map.Model.Selected == i && view.DetailTitle.text == g.Map.Model.Locations[i].Name && player == g.Players.Player.transform.position, "Location click shows detail without moving player"); TextCheck(view); }
                    view.MarkerButtons[10].onClick.Invoke(); Require(g.Map.Model.Selected == 10, "Marker click shares MVC selection"); Capture(g, "MapIslandPreview");
                    SessionState.SetFloat(Key + ".Clock", (float)g.Time.Clock.Minute); npcPosition = g.NPCs.Actors.Values.First().transform.position; waitStart = EditorApplication.timeSinceStartup;
                    g.ToggleInventory(); g.OpenShop(); g.CloseInventory(); Require(!g.UI.IsInventoryVisible && !g.UI.HasShop && !g.Players.Player.enabled, "Map blocks other panels and accidental input resume"); Phase(3);
                }
                else if (p == 3 && EditorApplication.timeSinceStartup - waitStart > .25)
                {
                    Require((float)g.Time.Clock.Minute == SessionState.GetFloat(Key + ".Clock", -1) && g.NPCs.Actors.Values.First().transform.position == npcPosition, "Map freezes clock and NPC schedule");
                    var old = View<MapView>(); int selected = g.Map.Model.Selected; g.ToggleMap(); old.MarkerButtons[0].onClick.Invoke(); Require(g.Map.Model.Selected == selected, "Closed controller removes selection events");
                    g.ToggleMap(); g.HandleEscape(); waitStart = EditorApplication.timeSinceStartup; Phase(4);
                }
                else if (p == 4 && EditorApplication.timeSinceStartup - waitStart > .15)
                { Require(!g.UI.HasMap && View<MapView>() == null && g.Players.Player.enabled && g.Tools.InputEnabled && !g.UI.HasPause, "Early cancellation releases map and resumes input without pause"); View<PrototypeHud>().MapButton.onClick.Invoke(); Phase(5); }
                else if (p == 5 && View<MapView>() != null) { CheckView(g); g.HandleEscape(); Require(!g.UI.HasMap && !g.UI.HasPause, "Esc closes map first"); Phase(6); }
                else if (p == 6 && View<MapView>() == null) { g.OpenPause(); Phase(7); }
                else if (p == 7 && View<PauseView>() != null)
                { g.ToggleMap(); Require(!g.UI.HasMap, "Pause cannot stack map"); View<PauseView>().ContinueButton.onClick.Invoke(); g.ToggleInventory(); g.ToggleMap(); Require(g.UI.IsInventoryVisible && !g.UI.HasMap, "Inventory cannot stack map"); g.CloseInventory(); Move(g, g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); g.EnterHome(); Phase(8); }
                else if (p == 8 && g.State == GameFlowState.Playing && g.Housing.Inside) { View<PrototypeHud>().MapButton.onClick.Invoke(); Phase(9); }
                else if (p == 9 && View<MapView>() != null)
                {
                    CheckView(g); Require(View<MapView>().RegionText.text.Contains("室内") && g.Map.Model.PlayerPosition == g.Map.Model.Locations.Single(l => l.Id == "home").Position, "Interior uses home marker instead of remote interior coordinates");
                    View<MapView>().LocationButtons[1].onClick.Invoke(); TextCheck(View<MapView>()); Capture(g, "MapInteriorPreview"); View<MapView>().CloseButton.onClick.Invoke(); Require(g.Players.Player.enabled && !g.Tools.InputEnabled, "Map close restores correct interior controls");
                    Move(g, g.Configuration.Housing.InteriorOrigin + new Vector3(-1.5f, .1f, 2)); g.RequestRest(); Phase(10);
                }
                else if (p == 10 && View<SleepView>() != null)
                { View<SleepView>().ConfirmButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var save) && save.Day == 2, "Actual sleep still saves world"); SessionState.SetString(Key + ".Saved", File.ReadAllText(g.Saves.SavePath)); g.StartNewDayFromReport(); g.ReturnToMainMenu(); Phase(11); }
                else if (p == 11 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(12); }
                else if (p == 12 && g.State == GameFlowState.MainMenu)
                { Require(!g.Map.Ready && g.Map.Model.Locations.Count == 0 && !g.UI.HasMap && View<MapView>() == null, "Menu clears map world references and AA UI"); SameSave(g); g.ContinueGame(); Phase(13); }
                else if (p == 13 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Map.Ready && g.Map.Model.Selected == 0, "Reload reconstructs locations without persisting UI selection"); Move(g, g.Configuration.Housing.InteriorOrigin + new Vector3(0, .1f, -3)); g.ExitHome(); Phase(14); }
                else if (p == 14 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                { Require(g.Inventory.TryAdd("wood", g.Configuration.Animals.BuildWood) && g.Inventory.TryAdd("stone", g.Configuration.Animals.BuildStone), "Prepare changing facility fixture"); g.Economy.Restore(g.Configuration.Animals.BuildGold); Require(g.Animals.Build(), "Build real AA coop"); SameSave(g); g.ToggleMap(); Phase(15); }
                else if (p == 15 && View<MapView>() != null)
                { CheckView(g); Require(g.Map.Model.Locations.Last().Name == "鸡舍" && g.Map.Model.Locations.Last().WorldPosition == g.Animals.Coop.transform.position, "New facility updates actual map location and description"); View<MapView>().MarkerButtons[11].onClick.Invoke(); TextCheck(View<MapView>()); Capture(g, "MapCoopPreview"); g.CloseMap(); g.ReturnToMainMenu(); Phase(16); }
                else if (p == 16 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(17); }
                else if (p == 17 && g.State == GameFlowState.MainMenu) { SameSave(g); g.EnterGame(); Phase(18); }
                else if (p == 18 && g.State == GameFlowState.Playing) { g.ToggleMap(); Phase(19); }
                else if (p == 19 && View<MapView>() != null)
                { CheckView(g); Require(g.Map.Model.Locations.Last().Name == "鸡舍预留地", "New game resets facility map state"); g.CloseMap(); g.ReturnToMainMenu(); Phase(20); }
                else if (p == 20 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(21); }
                else if (p == 21 && g.State == GameFlowState.MainMenu)
                {
                    SameSave(g); Require(!g.Map.Ready && !g.UI.HasMap && View<MapView>() == null, "Final unload leaves no map instance or references");
                    File.WriteAllText("Logs/MapVerification.txt", "PASS: preserved prefab/HUD setup, packed-AA MVC PopUp map/HUD toggle, all actual locations/roads/marker coordinates, Chinese layout, readonly selection, interior position/controls, modal clock/NPC/input pause, panel guards, M/Esc/close and early cancellation, listener release, sleep checkpoint/reload, changing facility/new-game refresh and unload without daytime writes.\n"); Debug.Log("Map flow verification PASSED."); Phase(30); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.isPlaying = false; EditorApplication.Exit(1); }
        }
    }
}
