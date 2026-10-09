using System;
using System.IO;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using MagicIsland.Managers;
using MagicIsland.Loading;
using MagicIsland.Saving;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicIsland.EditorTools
{
    // Explicit batch verification, runs only when Run is invoked in the validation copy.
    [InitializeOnLoad]
    public static class GameFlowVerification
    {
        private const string Running = "MagicIsland.FlowVerification";
        private const string PhaseKey = Running + ".Phase";
        private const string Started = Running + ".Started";

        static GameFlowVerification()
        {
            if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            // Generated temporary UI objects can dirty the batch editor's open scene.
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            int packed = settings.DataBuilders.FindIndex(builder => builder is BuildScriptPackedPlayMode);
            if (packed < 0) throw new Exception("Packed Play Mode builder is missing");
            settings.ActivePlayModeDataBuilderIndex = packed;
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            SessionState.SetBool(Running, true);
            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetFloat(Started, (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            int phase = SessionState.GetInt(PhaseKey, 0);
            if (phase == 5 && !EditorApplication.isPlaying)
            {
                SessionState.SetBool(Running, false);
                EditorApplication.Exit(0);
                return;
            }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Started, 0f) > 120f)
                    throw new Exception("Game flow verification timed out in phase " + phase);
                if (!EditorApplication.isPlaying) return;
                GameManager game = GameManager.Instance;
                if (game == null) return;
                Require(game.State != GameFlowState.Failed, "GameManager entered Failed");
                if (phase == 0 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Configuration.Config != null && game.Configuration.Items != null, "All configurations must load before menu");
                    Require(game.Farm.State == null, "World must not initialize before entry");
                    string directory = Path.GetFullPath("Logs/SaveVerification/" + Guid.NewGuid().ToString("N"));
                    SessionState.SetString(Running + ".SaveDirectory", directory);
                    game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items, directory);
                    game.UI.SetSaveAvailable(game.Saves.HasSave);
                    Require(!UnityEngine.Object.FindObjectOfType<MainMenuView>().ContinueButton.interactable, "No save disables Continue");
                    MainMenuView menu = UnityEngine.Object.FindObjectOfType<MainMenuView>();
                    Require(menu.SettingsButton != null && menu.QuitButton != null, "Four menu actions wired");
                    menu.SettingsButton.onClick.Invoke();
                    game.OpenSettings();
                    SessionState.SetInt(PhaseKey, 8);
                }
                else if (phase == 8 && game.State == GameFlowState.MainMenu)
                {
                    SettingsView settings = UnityEngine.Object.FindObjectOfType<SettingsView>();
                    if (settings == null) return;
                    Require(UnityEngine.Object.FindObjectsOfType<SettingsView>().Length == 1
                        && settings.transform.parent == game.UI.GetLayerRoot(UILayer.PopUp), "One AA settings popup, correct layer");
                    float previousVolume = game.Settings.Volume;
                    settings.VolumeSlider.value = 0.33f;
                    Require(Mathf.Approximately(game.Settings.Volume, 0.33f) && settings.VolumeText.text == "33%", "Settings MVC changes volume");
                    settings.VolumeSlider.value = previousVolume;
                    settings.CloseButton.onClick.Invoke();
                    game.EnterGame();
                    game.EnterGame();
                    Require(game.State == GameFlowState.LoadingScene, "Entry locks duplicate requests");
                    SessionState.SetInt(PhaseKey, 1);
                }
                else if ((phase == 1 || phase == 3) && game.State == GameFlowState.Playing)
                {
                    Require(UnityEngine.Object.FindObjectsOfType<GameManager>().Length == 1, "Only one GameManager");
                    Require(UnityEngine.Object.FindObjectsOfType<EventSystem>().Length == 1, "Only one EventSystem");
                    Require(game.Farm.State.Cells.Length == game.Configuration.Config.FarmSize * game.Configuration.Config.FarmSize,
                        "Farm size must come from GameConfig");
                    Require(game.Farm.State.Day == 1 && game.Inventory.GetCount(game.Configuration.Config.SeedItemId) == game.Configuration.Config.InitialSeeds,
                        "New game must initialize clean state");
                    Require(Mathf.Approximately(game.Players.Player.Speed, game.Configuration.Config.PlayerSpeed), "Player uses config");
                    PrototypeHud[] huds = UnityEngine.Object.FindObjectsOfType<PrototypeHud>();
                    Require(huds.Length == 1 && game.UI.HasFarmHud, "One AA loaded FarmHUD");
                    Require(game.UI.GetLayerRoot(UILayer.Bottom).name == "Bottom"
                        && game.UI.GetLayerRoot(UILayer.Top).GetSiblingIndex() > game.UI.GetLayerRoot(UILayer.PopUp).GetSiblingIndex(), "Four ordered UI layers");
                    Require(huds[0].transform.parent == game.UI.GetLayerRoot(UILayer.Common), "HUD auto-parents to Common");
                    if (phase == 1) VerifyInventoryFarming(game);
                    huds[0].InventoryButton.onClick.Invoke();
                    Require(game.UI.IsInventoryVisible && !game.Players.Player.enabled && !game.Interactions.Controller.enabled,
                        "Inventory MVC open pauses world input");
                    InventoryView bag = UnityEngine.Object.FindObjectOfType<InventoryView>();
                    Require(bag != null && bag.Slots.Count(slot => slot.gameObject.activeSelf) == game.Configuration.Config.InventorySlots,
                        "InventoryUI uses configured capacity");
                    Require(bag.transform.parent == game.UI.GetLayerRoot(UILayer.PopUp)
                        && bag.GetComponent<Canvas>().sortingOrder > huds[0].GetComponent<Canvas>().sortingOrder,
                        "Inventory auto-parents above HUD");
                    Require(bag.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length == 0
                        && bag.GetComponentsInChildren<TMPro.TMP_Text>(true).All(text => text.font != null), "TMP texts with serialized fonts");
                    bag.Slots[0].SelectButton.onClick.Invoke();
                    Require(bag.DetailTitleText.text == game.Inventory.Catalog.Find(game.Inventory.State.Slots[0].ItemId).DisplayName,
                        "Selecting a slot displays configured item details");
                    if (phase == 3 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera);
                    bag.CloseButton.onClick.Invoke();
                    Require(!game.UI.IsInventoryVisible && game.Players.Player.enabled && game.Interactions.Controller.enabled,
                        "Closing inventory restores input");
                    huds[0].ToolButtons[1].onClick.Invoke();
                    Require(game.Interactions.Controller.Tool == FarmTool.Seeds, "MVC tool event reaches InteractionManager");
                    if (phase == 3)
                    {
                        Require(game.Farm.TryAct(1, FarmTool.Hoe, out _) && game.Farm.TryAct(1, FarmTool.Seeds, out _), "Prepare crop for resume");
                        game.Farm.TryAct(1, FarmTool.WateringCan, out _);
                        game.Players.RestorePosition(new Vector3(2f, 0.1f, -4f));
                    }
                    int previousDay = game.Farm.State.Day;
                    huds[0].NextDayButton.onClick.Invoke();
                    Require(game.Farm.State.Day == previousDay + 1, "MVC day event fires exactly once through GameManager/FarmManager");
                    Require(game.Saves.HasSave && File.Exists(game.Saves.SavePath), "Next day automatically saves to disk");
                    if (phase == 3) game.Farm.TryAct(1, FarmTool.WateringCan, out _);
                    foreach (TMPro.TMP_Text label in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>())
                        Require(label.text.Where(c => !char.IsControl(c)).All(c => label.font.HasCharacter(c)), "All displayed UI characters have glyphs: " + label.name);
                    Require(TMPro.TMP_Settings.defaultFontAsset.HasCharacter('\u5c9b'), "Global TMP default supports Chinese");
                    huds[0].ReturnMenuButton.onClick.Invoke();
                    game.ReturnToMainMenu();
                    Require(game.State == GameFlowState.ReturningToMenu, "Return locks duplicate requests");
                    SessionState.SetInt(PhaseKey, phase + 1);
                }
                else if ((phase == 2 || phase == 4) && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Farm.State == null && game.Players.Player == null && game.Interactions.Controller == null,
                        "Returning clears scene system references");
                    Require(!game.Scenes.GameScene.IsValid() && !game.UI.HasFarmHud && !game.UI.HasInventory && game.Inventory.State == null,
                        "AA scene, HUD, inventory UI and bag state are released");
                    if (phase == 2)
                    {
                        game.EnterGame();
                        SessionState.SetInt(PhaseKey, 3);
                    }
                    else
                    {
                        MainMenuView menu = UnityEngine.Object.FindObjectOfType<MainMenuView>();
                        Require(menu.ContinueButton.interactable, "Saved game enables Continue");
                        menu.ContinueButton.onClick.Invoke();
                        game.ContinueGame();
                        Require(game.State == GameFlowState.LoadingScene, "Continue locks duplicate requests");
                        SessionState.SetInt(PhaseKey, 6);
                    }
                }
                else if (phase == 6 && game.State == GameFlowState.Playing)
                {
                    Require(game.Farm.State.Day == 2, "Saved day restored");
                    var cell = game.Farm.State.Cells[1];
                    Require(cell.Tilled && cell.Planted && cell.Watered && cell.Growth == 1, "Saved crop growth and watering restored");
                    Require(game.Inventory.GetCount(game.Configuration.Config.SeedItemId) == game.Configuration.Config.InitialSeeds - 1,
                        "Saved inventory restored without granting initial seeds again");
                    Require(Mathf.Approximately(game.Players.Player.transform.position.x, 2f)
                        && Mathf.Approximately(game.Players.Player.transform.position.z, -4f), "Saved player position restored");
                    string directory = SessionState.GetString(Running + ".SaveDirectory", "");
                    string blocked = Path.Combine(directory, "blocked");
                    File.WriteAllText(blocked, "not a directory");
                    game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items, blocked);
                    game.ReturnToMainMenu();
                    Require(game.State == GameFlowState.Playing && game.Farm.State != null, "Failed save keeps active game");
                    game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items, directory);
                    game.ReturnToMainMenu();
                    SessionState.SetInt(PhaseKey, 7);
                }
                else if (phase == 7 && game.State == GameFlowState.MainMenu)
                {
                    string save = game.Saves.SavePath;
                    File.WriteAllText(save, "corrupt json");
                    Require(game.Saves.TryLoad(out SaveData recovered) && game.Saves.RecoveredBackup && recovered.Day == 2,
                        "Corrupt primary save recovers validated backup");
                    File.WriteAllText(save + ".bak", "{\"Version\":99}");
                    game.ContinueGame();
                    Require(game.State == GameFlowState.MainMenu && !game.Saves.HasSave
                        && !UnityEngine.Object.FindObjectOfType<MainMenuView>().ContinueButton.interactable,
                        "Corrupt or unsupported save is rejected without initializing world");
                    Directory.CreateDirectory("Logs");
                    File.WriteAllText("Logs/GameFlowVerification.txt",
                        "PASS: packed AA, two new games and continued saved game.\n"
                        + "Saved day, crop flags/growth, inventory and player position restored.\n"
                        + "Autosave, save-failure retention, corrupt-file recovery, version rejection verified.\n"
                        + "TMP Chinese glyph coverage/default font, four UI layers and MVC lifecycle verified.\n");
                    Debug.Log("Game flow verification PASSED (save/resume, packed AA, Chinese TMP).");
                    SessionState.SetInt(PhaseKey, 5);
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error)
            {
                SessionState.SetBool(Running, false);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void VerifyInventoryFarming(GameManager game)
        {
            string seed = game.Configuration.Config.SeedItemId;
            string crop = game.Configuration.Config.HarvestItemId;
            long originalSeeds = game.Inventory.GetCount(seed);
            Require(game.Farm.TryAct(0, FarmTool.Hoe, out _), "Till farm");
            Require(game.Farm.TryAct(0, FarmTool.Seeds, out _), "Plant using inventory seed");
            Require(game.Inventory.GetCount(seed) == originalSeeds - 1, "Successful planting consumes one seed");
            Require(!game.Farm.TryAct(0, FarmTool.Seeds, out _) && game.Inventory.GetCount(seed) == originalSeeds - 1,
                "Failed duplicate planting must not consume a seed");
            for (int i = 0; i < game.Configuration.Config.CropGrowthDays; i++)
            {
                game.Farm.TryAct(0, FarmTool.WateringCan, out _);
                game.AdvanceDay();
            }
            Require(game.Farm.State.Cells[0].Mature, "Crop matured");
            int space = game.Configuration.Config.InventorySlots * game.Inventory.Catalog.Find(seed).MaxStack
                - (int)game.Inventory.GetCount(seed);
            Require(game.Inventory.TryAdd(seed, space), "Fill bag to capacity");
            Require(!game.Farm.TryAct(0, FarmTool.Hoe, out _) && game.Farm.State.Cells[0].Mature && game.Inventory.GetCount(crop) == 0,
                "Full bag preserves mature crop without adding items");
            game.Inventory.TryRemove(seed, (int)game.Inventory.GetCount(seed));
            Require(game.Farm.TryAct(0, FarmTool.Hoe, out _) && game.Inventory.GetCount(crop) == 1
                && !game.Farm.State.Cells[0].Planted, "Harvest moves crop into bag after space becomes available");
            Require(!game.Farm.TryAct(0, FarmTool.Seeds, out _) && !game.Farm.State.Cells[0].Planted,
                "No planting when inventory has no seeds");
        }
    }
}
