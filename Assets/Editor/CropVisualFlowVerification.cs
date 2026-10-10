using System;
using System.IO;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Managers;
using MagicIsland.UI.Views;
using MagicIsland.Interaction;
using MagicIsland.Saving;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class CropVisualFlowVerification
    {
        private const string Running = "MagicIsland.CropVisualVerification";
        private static readonly int[] Plots = { 0, 1, 2, 5, 6, 7, 10, 11, 12 };
        static CropVisualFlowVerification() { if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(builder => builder is BuildScriptPackedPlayMode);
            SessionState.SetBool(Running, true); SessionState.SetInt(Running + ".Phase", 0);
            SessionState.SetFloat(Running + ".Started", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Phase(int phase) => SessionState.SetInt(Running + ".Phase", phase);
        private static CropInfoView Info => UnityEngine.Object.FindObjectOfType<CropInfoView>();
        private static void Hover(GameManager game, int index, bool blocked = false)
        {
            Camera view = game.Players.Player.ViewCamera;
            Ray ray = view.ScreenPointToRay(view.WorldToScreenPoint(game.Farm.Grid.CellCenter(index)));
            game.Interactions.Controller.RefreshFarmHover(ray, blocked); game.UI.RefreshViews();
        }
        private static void VerifyModels(GameManager game)
        {
            Require(game.Farm.LoadedVisualCount == 9, "Nine stage prefabs loaded via packed AA");
            foreach (int index in Plots)
            {
                FarmCell cell = game.Farm.State.Cells[index]; Transform model = game.Farm.Grid.GetCropVisual(index);
                string address = game.Farm.Crops.Find(cell.CropId).PrefabAddress(CropStages.FromGrowth(cell.Growth, cell.RequiredGrowthDays));
                Require(model != null && model.name.EndsWith(address) && model.gameObject.activeSelf, "Correct stage model: " + index);
                Require(model.GetComponentsInChildren<Collider>(true).Length == 0 && model.GetComponentsInChildren<MeshRenderer>().Length > 0,
                    "Models render without intercepting interaction rays");
                Require(model.GetComponentsInChildren<MeshFilter>().All(filter => filter.sharedMesh != null
                    && filter.sharedMesh.triangles.Length > 0 && filter.sharedMesh.normals.Length == filter.sharedMesh.vertexCount), "Baked low-poly meshes have normals");
            }
        }
        private static void Capture(GameManager game, string file)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + file + ".png", true);
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            int phase = SessionState.GetInt(Running + ".Phase", 0);
            if (phase == 7 && !EditorApplication.isPlaying)
            { SessionState.SetBool(Running, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Running + ".Started", 0) > 120)
                    throw new Exception("Crop visual verification timed out at phase " + phase);
                if (!EditorApplication.isPlaying) return;
                GameManager game = GameManager.Instance; if (game == null) return;
                Require(game.State != GameFlowState.Failed, "Game initialization failed");
                if (phase == 0 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Configuration.Crops.Validate(game.Configuration.Config, game.Configuration.Items), "All configured stage addresses valid");
                    game.Configuration.Weather.RainChance = 0;
                    game.Configuration.Seasons.SpringRainChance = game.Configuration.Seasons.SummerRainChance = game.Configuration.Seasons.AutumnRainChance = game.Configuration.Seasons.WinterRainChance = 0;
                    // This fixture isolates existing crop behaviors; CalendarFlowVerification covers season restrictions.
                    foreach (var fixtureCrop in game.Configuration.Crops.Crops) fixtureCrop.PlantingSeasons = MagicIsland.CalendarSystem.SeasonMask.All;
                    game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items, game.Configuration.Crops,
                        Path.GetFullPath("Logs/CropVisualSaves/" + Guid.NewGuid().ToString("N")), tasks: game.Configuration.Tasks, orders: game.Configuration.Orders, seasons: game.Configuration.Seasons, gathering: game.Configuration.Gathering, toolUpgrades: game.Configuration.ToolUpgrades, storage: game.Configuration.Storage);
                    var crop = game.Configuration.Crops.Crops[0]; SessionState.SetString(Running + ".Address", crop.SeedlingPrefabAddress);
                    crop.SeedlingPrefabAddress = "MainMenuPanel"; // A valid AA object with no crop renderers exercises failure cleanup.
                    game.EnterGame(); Phase(1);
                }
                else if (phase == 1 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Farm.LastError != null && game.Farm.LoadedVisualCount == 0 && game.Farm.State == null
                        && !game.Scenes.GameScene.IsValid(), "Invalid model unloads scene and releases visual handles before retry");
                    game.Configuration.Crops.Crops[0].SeedlingPrefabAddress = SessionState.GetString(Running + ".Address", "");
                    game.EnterGame(); Phase(2);
                }
                else if (phase == 2 && game.State == GameFlowState.Playing)
                {
                    Require(Info != null && Info.transform.parent == game.UI.GetLayerRoot(UILayer.Common) && !Info.Group.blocksRaycasts
                        && Info.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(graphic => !graphic.raycastTarget), "CropInfoPanel AA/MVC Common layer and mouse passthrough");
                    for (int kind = 0; kind < 3; kind++)
                    {
                        var crop = game.Configuration.Crops.Crops[kind];
                        if (kind > 0) game.Inventory.TryAdd(crop.SeedItemId, 3);
                        Require(game.Farm.SelectSeed(crop.SeedItemId), "Select crop for visual fixture");
                        for (int stage = 0; stage < 3; stage++)
                        {
                            int index = kind * 5 + stage;
                            Require(game.Farm.TryAct(index, FarmTool.Hoe, out _) && game.Farm.TryAct(index, FarmTool.Seeds, out _), "Plant real configured crop");
                            game.Farm.State.Cells[index].Growth = stage == 0 ? 0 : stage == 1 ? 1 : crop.GrowthDays;
                        }
                    }
                    game.Farm.Grid.Refresh(); VerifyModels(game);
                    Transform growing = game.Farm.Grid.GetCropVisual(1);
                    game.Farm.State.Cells[1].Growth = 2; game.Farm.Grid.Refresh();
                    Require(game.Farm.Grid.GetCropVisual(1) == growing, "Progress inside same stage keeps instance");
                    game.Farm.State.Cells[1].Growth = 3; game.Farm.Grid.Refresh();
                    Require(game.Farm.Grid.GetCropVisual(1) != growing && !growing.gameObject.activeSelf, "Stage change replaces model and hides old instance immediately");
                    game.Farm.State.Cells[1].Growth = 1; game.Farm.Grid.Refresh();
                    game.Farm.SelectSeed("turnip_seed"); game.Farm.TryAct(2, FarmTool.Hoe, out _);
                    Require(game.Farm.Grid.GetCropVisual(2) == null && !game.Farm.State.Cells[2].Planted && game.Inventory.GetCount("turnip") == 1,
                        "Harvest removes visual and gives original crop");
                    game.Farm.TryAct(2, FarmTool.Seeds, out _);
                    Require(game.Farm.Grid.GetCropVisual(2).name.EndsWith("TurnipSeedling"), "Replant uses seedling rather than retained mature model");
                    Transform seedling = game.Farm.Grid.GetCropVisual(2);
                    Require(!game.Farm.TryAct(2, FarmTool.Seeds, out _) && game.Farm.Grid.GetCropVisual(2) == seedling, "Failed action does not replace visual");
                    game.Farm.State.Cells[2].Growth = 3; game.Farm.Grid.Refresh();
                    game.Players.RestorePosition(new Vector3(-2.5f, 0.1f, -4)); game.Time.Clock.Restore(720); game.Weather.Tick(720, false); Phase(3);
                }
                else if (phase == 3 && game.State == GameFlowState.Playing)
                {
                    VerifyModels(game); game.Tools.SelectTool(FarmTool.WateringCan); Hover(game, 0);
                    Require(game.Interactions.Controller.HoverIndex == 0 && Info.Group.alpha == 1 && Info.NameText.text == "芜菁"
                        && Info.DetailsText.text.Contains("幼苗") && Info.DetailsText.text.Contains("0 / 3") && Info.DetailsText.text.Contains("未浇水"), "Ray hover exposes name/stage/progress/dry state");
                    Transform seedling = game.Farm.Grid.GetCropVisual(0); game.Farm.TryAct(0, FarmTool.WateringCan, out _); Hover(game, 0);
                    Require(Info.DetailsText.text.Contains("已浇水") && game.Farm.Grid.GetCropVisual(0) == seedling, "Watering updates details without recreating model");
                    Capture(game, "CropStagesPreview");
                    Hover(game, 24); Require(Info.Group.alpha == 0, "Empty plot hides crop details");
                    Hover(game, 12, true); Require(Info.Group.alpha == 0 && game.Interactions.Controller.HoverIndex == -1, "UI hover hides details and clears outline");
                    game.Players.RestorePosition(new Vector3(8, 0.1f, -8)); Hover(game, 12);
                    Require(Info.Group.alpha == 1 && Info.NameText.text == "玉米" && Info.DetailsText.text.Contains("已成熟")
                        && Info.HintText.text.Contains("距离太远"), "Out-of-range crops remain readable and report distance");
                    game.ToggleInventory(); game.UI.RefreshViews(); Require(Info.Group.alpha == 0, "Modal opening hides previous crop details");
                    game.CloseInventory(); game.Players.RestorePosition(new Vector3(0, 0.1f, -4.6f));
                    game.ReturnToMainMenu(); Phase(4);
                }
                else if (phase == 4 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Farm.LoadedVisualCount == 0 && Info == null && game.Farm.State == null, "Return releases models, AA handles and CropInfoPanel");
                    Require(game.Saves.TryLoad(out SaveData data) && data.Version == SaveData.CurrentVersion && data.Cells[0].Growth == 0 && data.Cells[1].Growth == 1
                        && data.Cells[12].Growth == 7, "Save format remains compatible and stores progress only");
                    game.ContinueGame(); Phase(5);
                }
                else if (phase == 5 && game.State == GameFlowState.Playing)
                {
                    VerifyModels(game); Hover(game, 6);
                    Require(Info.NameText.text == "番茄" && Info.DetailsText.text.Contains("生长中") && Info.DetailsText.text.Contains("1 / 5"), "Restored tomato details use saved crop, not selected seed");
                    foreach (var label in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>())
                        Require(label.text.Where(c => !char.IsControl(c)).All(c => label.font.HasCharacter(c)), "All crop information Chinese glyphs baked");
                    Capture(game, "CropStagesResumePreview"); game.ReturnToMainMenu(); Phase(6);
                }
                else if (phase == 6 && game.State == GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/CropVisualFlowVerification.txt", "PASS: nine low-poly crop stage models load via packed AA; invalid model fails cleanly and retries.\n"
                        + "Real planting, stage transitions, same-stage reuse, watering, failed actions, harvest and replant verified.\n"
                        + "CropInfoPanel MVC/Common/Chinese text, ray hover, distance, UI interception and modal hiding verified.\n"
                        + "Saved mixed crop progress restores all nine models without save schema changes; leaving releases instances/UI/AA handles.\n");
                    Debug.Log("Crop visual flow verification PASSED."); Phase(7); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Running, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
