using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Farming;
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
    [InitializeOnLoad] public static class FarmArtFlowVerification
    {
        const string Key = "MagicIsland.FarmArtVerification";
        const int Plot = 12;
        static Vector3[] pausedPositions;
        static double pausedAt;
        static FarmManager fixture;
        static FarmArtFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            var paths = Directory.GetFiles(CropVisualPrefabBuilder.Folder, "*.prefab").Concat(new[] { FarmArtSetup.EffectPath, "Assets/Config/GameConfig.asset" })
                .Concat(Directory.GetFiles(FarmArtSetup.MaterialFolder, "*.mat")).Concat(Directory.GetFiles("Assets/Art/Materials/Crops", "*.mat")).ToArray();
            var bytes = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Repeated setup preserves farm art " + paths[i]);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key + ".Phase", p); Debug.Log("Farm art phase " + p); }
        static T View<T>() where T : Component => Object.FindObjectOfType<T>();
        static void Move(GameManager g, Vector3 point) { g.Players.RestorePosition(point); g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget(); Physics.SyncTransforms(); }
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir, g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting, g.Configuration.Animals);
        static void SameSave(GameManager g)
        {
            string snapshot = SessionState.GetString(Key + ".Saved", "");
            Require(snapshot.Length == 0 ? !File.Exists(g.Saves.SavePath) : File.ReadAllText(g.Saves.SavePath) == snapshot, "Daytime farm operations cannot write checkpoint");
        }
        static void Act(GameManager g, FarmTool tool)
        {
            Move(g, g.Farm.Grid.CellCenter(Plot) + new Vector3(0, .055f, -1.1f)); Require(g.Tools.SelectTool(tool), "Select farm tool");
            var target = g.Interactions.RefreshPointer(new Ray(g.Farm.Grid.CellCenter(Plot) + Vector3.up * 5, Vector3.down), false);
            Require(target.Index == Plot && target.InRange && g.Interactions.TryInteract(), "Real mouse farm dispatch " + tool);
            typeof(ToolManager).GetMethod("TickAction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(g.Tools, new object[] { g.Configuration.Config.ToolActionDuration * .52f });
            Require(g.Tools.Presentation.ActiveEffectCount > 0, "Committed farm action emits AA feedback at contact"); SameSave(g);
        }
        static void Capture(GameManager g, string name, float size = 4.4f)
        {
            var camera = g.Players.Player.ViewCamera; float previous = camera.orthographicSize; camera.orthographicSize = size;
            camera.GetComponent<CameraController>().SnapToTarget(); camera.transform.position += new Vector3(0, .5f, 0) - (g.Players.Player.transform.position + Vector3.up * .6f);
            var canvases = Object.FindObjectsOfType<Canvas>(); var enabled = canvases.Select(c => c.enabled).ToArray();
            try { foreach (var c in canvases) c.enabled = false; GameUiPreview.CaptureInventory(camera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
            finally { for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = enabled[i]; camera.orthographicSize = previous; }
        }
        static void Fixtures(GameManager g)
        {
            int expectedModels = g.Farm.Crops.Crops.SelectMany(crop => Enum.GetValues(typeof(CropGrowthStage))
                .Cast<CropGrowthStage>().Select(crop.PrefabAddress)).Distinct().Count();
            Require(g.Farm.LoadedVisualCount == expectedModels && g.Farm.PlotPrefab != null && g.Tools.OwnsEffects, "Configured crop models plus soil and effects loaded through packed AA");
            for (int i = 0; i < g.Farm.State.Cells.Length; i++)
            {
                var visual = g.Farm.Grid.GetPlotVisual(i);
                Require(visual.IsValid && visual.GrassCover.activeSelf && !visual.SoilBed.activeSelf && visual.GetComponentsInChildren<Collider>(true).Length == 0, "Untilled grass and collision-free AA plot");
                Require(g.Farm.Grid.Pick(new Ray(g.Farm.Grid.CellCenter(i) + Vector3.up * 6, Vector3.down)) == i, "Original cell coordinates and ray picking");
            }
            int[] indices = { 0, 1, 2, 5, 6, 7, 10, 11, 15 };
            for (int i = 0; i < indices.Length; i++)
            {
                var crop = g.Farm.Crops.Crops[i / 3]; var cell = g.Farm.State.Cells[indices[i]];
                cell.Tilled = cell.Planted = true; cell.CropId = crop.Id; cell.RequiredGrowthDays = crop.GrowthDays;
                cell.Growth = i % 3 == 0 ? 0 : i % 3 == 1 ? 1 : crop.GrowthDays; cell.Watered = i % 2 == 0;
            }
            g.Farm.Grid.Refresh();
            foreach (int index in indices)
            {
                var model = g.Farm.Grid.GetCropVisual(index);
                Require(model.GetComponent<CropArtVisual>().ArtVersion == 1 && model.GetComponentsInChildren<Collider>(true).Length == 0, "Botanical AA stages retain collider-free crop visuals");
                Require(model.GetComponentsInChildren<MeshFilter>().All(m => m.sharedMesh != null && m.sharedMesh.normals.Length == m.sharedMesh.vertexCount), "Valid stage meshes and normals");
            }
            var same = g.Farm.Grid.GetCropVisual(0); g.Farm.Grid.Refresh(); Require(same == g.Farm.Grid.GetCropVisual(0), "Same stage refresh reuses model");
            Move(g, new Vector3(0, .1f, -4)); Capture(g, "FarmArtStagesPreview");
            var effects = g.Tools.Presentation.EffectRoot.GetComponent<FarmActionEffects>();
            Require(effects.IsValid && effects.GetComponentsInChildren<Collider>(true).Length == 0, "AA effect templates cannot intercept picking");
            foreach (ToolActionKind kind in Enum.GetValues(typeof(ToolActionKind))) effects.Burst(kind, new Vector3(8, 0, -5));
            Require(effects.ActiveCount <= FarmActionEffects.MaximumParticles, "Bounded effect particle pool"); effects.Tick(2);
            foreach (ToolActionKind kind in Enum.GetValues(typeof(ToolActionKind))) { effects.Burst(kind, new Vector3(8, 0, -5)); effects.Tick(2); }
            Require(effects.ActiveCount == 0, "Pool reuses inactive particles across all tools without leaks");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 30 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Farm art timeout " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Farm art startup");
                if (p == 1 && g.State == GameFlowState.MainMenu) throw new Exception("Farm startup returned to menu");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                {
                    string dir = Path.GetFullPath("Logs/FarmArtSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SaveDirectory(g, dir);
                    SessionState.SetString(Key + ".Saved", ""); g.Configuration.Weather.RainChance = 0; g.Configuration.Seasons.SpringRainChance = 0;
                    g.EnterGame(); Phase(1);
                }
                else if (p == 1 && g.State == GameFlowState.Playing)
                { Fixtures(g); Require(g.Inventory.TryAdd("basic_fertilizer", 1), "Prepare fertilizer"); Act(g, FarmTool.Hoe); Require(g.Farm.Grid.GetPlotVisual(Plot).SoilBed.activeSelf && !g.Farm.Grid.GetPlotVisual(Plot).GrassCover.activeSelf, "Hoe switches grass to raised soil"); Capture(g, "FarmHoePreview"); Phase(2); }
                else if (p == 2 && !g.Tools.Busy)
                { Act(g, FarmTool.Seeds); Require(g.Farm.Grid.GetCropVisual(Plot).name.EndsWith("TurnipSeedling"), "Plant uses detailed seedling"); Phase(3); }
                else if (p == 3 && !g.Tools.Busy)
                {
                    Act(g, FarmTool.WateringCan); var visual = g.Farm.Grid.GetPlotVisual(Plot);
                    Require(visual.SoilRenderers.All(r => r.sharedMaterial == visual.WetSoil), "Water changes every ridge to wet material"); Capture(g, "FarmWaterPreview");
                    g.ToggleInventory(); Require(g.UI.IsInventoryVisible && !g.Tools.InputEnabled, "Inventory pauses farm action effects"); pausedPositions = g.Tools.Presentation.EffectRoot.GetComponentsInChildren<Transform>().Select(t => t.position).ToArray(); pausedAt = EditorApplication.timeSinceStartup; Phase(4);
                }
                else if (p == 4 && EditorApplication.timeSinceStartup - pausedAt > .25)
                {
                    Require(pausedPositions.SequenceEqual(g.Tools.Presentation.EffectRoot.GetComponentsInChildren<Transform>().Select(t => t.position)), "Paused tool effects keep exact positions");
                    g.ToggleInventory(); Phase(5);
                }
                else if (p == 5 && !g.Tools.Busy)
                { Act(g, FarmTool.Fertilizer); var visual = g.Farm.Grid.GetPlotVisual(Plot); Require(visual.FertilizerGrains.activeSelf && visual.SoilRenderers.All(r => r.sharedMaterial == visual.FertilizedWetSoil), "Fertilizer preserves wetness with grain detail"); Phase(6); }
                else if (p == 6 && !g.Tools.Busy)
                { g.Tools.Presentation.TickEffects(2); Require(g.Tools.Presentation.ActiveEffectCount == 0, "All effects expire"); Move(g, g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); g.EnterHome(); Phase(7); }
                else if (p == 7 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Move(g, g.Configuration.Housing.InteriorOrigin + new Vector3(-1.5f, .1f, 2)); g.RequestRest(); Phase(8); }
                else if (p == 8 && View<SleepView>() != null)
                {
                    View<SleepView>().ConfirmButton.onClick.Invoke(); int day = g.Farm.State.Day;
                    Require(g.Saves.TryLoad(out var save) && save.Day == day, "Actual sleep saves farm state"); SessionState.SetString(Key + ".Saved", File.ReadAllText(g.Saves.SavePath));
                    var plot = g.Farm.Grid.GetPlotVisual(Plot); Require(!g.Farm.State.Cells[Plot].Watered && plot.SoilRenderers.All(r => r.sharedMaterial == plot.FertilizedDrySoil), "Morning soil dries while retaining fertilizer");
                    string expected = day == 4 ? "TurnipMature" : "TurnipGrowing"; Require(g.Farm.Grid.GetCropVisual(Plot).name.EndsWith(expected), "Sleep advances crop model " + expected);
                    g.StartNewDayFromReport(); Move(g, g.Configuration.Housing.InteriorOrigin + new Vector3(0, .1f, -3)); g.ExitHome(); Phase(day == 4 ? 10 : 9);
                }
                else if (p == 9 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                { Act(g, FarmTool.WateringCan); Phase(6); }
                else if (p == 10 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                { SameSave(g); g.ReturnToMainMenu(); Phase(11); }
                else if (p == 11 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(12); }
                else if (p == 12 && g.State == GameFlowState.MainMenu)
                { Require(g.Farm.PlotPrefab == null && g.Farm.LoadedVisualCount == 0 && !g.Tools.OwnsEffects && g.Tools.Presentation == null, "Menu releases plot, crop and FX dependencies"); SameSave(g); g.ContinueGame(); Phase(13); }
                else if (p == 13 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Farm.State.Cells[Plot].Mature && g.Farm.Grid.GetCropVisual(Plot).name.EndsWith("TurnipMature"), "Sleep checkpoint reload reconstructs mature crop"); SameSave(g); Move(g, g.Configuration.Housing.InteriorOrigin + new Vector3(0, .1f, -3)); g.ExitHome(); Phase(14); }
                else if (p == 14 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                {
                    long count = g.Farm.TotalHarvest; Act(g, FarmTool.Hoe); Require(!g.Farm.State.Cells[Plot].Planted && g.Farm.Grid.GetCropVisual(Plot) == null && g.Farm.TotalHarvest == count + 1, "Harvest clears mature model and grants one crop");
                    var visual = g.Farm.Grid.GetPlotVisual(Plot); Require(!visual.FertilizerGrains.activeSelf && visual.SoilRenderers.All(r => r.sharedMaterial == visual.DrySoil), "Harvest restores ordinary dry soil"); Capture(g, "FarmHarvestPreview"); Phase(15);
                }
                else if (p == 15 && !g.Tools.Busy)
                { Act(g, FarmTool.Seeds); Require(g.Farm.Grid.GetCropVisual(Plot).name.EndsWith("TurnipSeedling"), "Replant begins a new visual cycle"); SameSave(g); g.Tools.Presentation.TickEffects(2); g.ReturnToMainMenu(); Phase(16); }
                else if (p == 16 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(17); }
                else if (p == 17 && g.State == GameFlowState.MainMenu)
                { fixture = new GameObject("Farm load fixture").AddComponent<FarmManager>(); fixture.StartCoroutine(fixture.LoadVisuals(g.Configuration.Crops, "FarmPlot_Verification_Missing")); Phase(18); }
                else if (p == 18 && fixture.LastError != null)
                { Require(fixture.PlotPrefab == null && fixture.LoadedVisualCount == 0, "Missing plot load releases dependencies"); fixture.StartCoroutine(fixture.LoadVisuals(g.Configuration.Crops)); fixture.Clear(); Phase(19); }
                else if (p == 19) Phase(20);
                else if (p == 20)
                {
                    Require(fixture.PlotPrefab == null && fixture.LoadedVisualCount == 0, "Cancelled AA plot load cannot bind late asset"); Object.Destroy(fixture.gameObject);
                    File.WriteAllText("Logs/FarmArtVerification.txt", "PASS: editable farm/crop art preservation, all configured crop stages plus plots/FX through packed AA, original ray picking and collider-free visuals, same-stage reuse, real hoe/seed/water/fertilizer/harvest/replant dispatch, bounded reusable paused FX, three actual sleeps/growth/dry soil, checkpoint reload, no daytime save, unload/failure/cancellation.\n");
                    Debug.Log("Farm art flow verification PASSED."); Phase(30); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.isPlaying = false; EditorApplication.Exit(1); }
        }
    }
}
