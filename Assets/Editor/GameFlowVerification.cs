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
                    Require(game.Configuration.Weather != null && game.Configuration.Weather.IsValid, "Weather configuration loads via AA before menu");
                    // Keep legacy manual-watering assertions deterministic; WeatherFlowVerification covers rain.
                    game.Configuration.Weather.RainChance = 0;
                    game.Configuration.Seasons.SpringRainChance = game.Configuration.Seasons.SummerRainChance = game.Configuration.Seasons.AutumnRainChance = game.Configuration.Seasons.WinterRainChance = 0;
                    // This fixture isolates existing crop behaviors; CalendarFlowVerification covers season restrictions.
                    foreach (var fixtureCrop in game.Configuration.Crops.Crops) fixtureCrop.PlantingSeasons = MagicIsland.CalendarSystem.SeasonMask.All;
                    Require(game.Configuration.Config != null && game.Configuration.Items != null && game.Configuration.Crops != null
                        && game.Configuration.Crops.Crops.Count == 3, "All configurations including three crops load via AA before menu");
                    Require(game.Farm.State == null, "World must not initialize before entry");
                    string directory = Path.GetFullPath("Logs/SaveVerification/" + Guid.NewGuid().ToString("N"));
                    SessionState.SetString(Running + ".SaveDirectory", directory);
                    game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items, game.Configuration.Crops, directory, tasks: game.Configuration.Tasks, orders: game.Configuration.Orders, seasons: game.Configuration.Seasons, gathering: game.Configuration.Gathering, toolUpgrades: game.Configuration.ToolUpgrades, storage: game.Configuration.Storage, npcs: game.Configuration.NPCs, relationships: game.Configuration.Relationships, stories: game.Configuration.Stories, schedules: game.Configuration.NPCSchedules, fishing: game.Configuration.Fishing, crafting: game.Configuration.Crafting);
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
                    Require(game.Stamina.State.Current == game.Configuration.Config.MaxStamina, "New game starts with configured full stamina");
                    Require(Mathf.Approximately(game.Players.Player.Speed, game.Configuration.Config.PlayerSpeed), "Player uses config");
                    PrototypeHud[] huds = UnityEngine.Object.FindObjectsOfType<PrototypeHud>();
                    Require(huds.Length == 1 && game.UI.HasFarmHud, "One AA loaded FarmHudPanel");
                    Require(game.UI.GetLayerRoot(UILayer.Bottom).name == "Bottom"
                        && game.UI.GetLayerRoot(UILayer.Top).GetSiblingIndex() > game.UI.GetLayerRoot(UILayer.PopUp).GetSiblingIndex(), "Four ordered UI layers");
                    Require(huds[0].transform.parent == game.UI.GetLayerRoot(UILayer.Common), "HUD auto-parents to Common");
                    if (phase == 1) VerifyInventoryFarming(game);
                    huds[0].InventoryButton.onClick.Invoke();
                    Require(game.UI.IsInventoryVisible && !game.Players.Player.enabled && !game.Interactions.Controller.enabled,
                        "Inventory MVC open pauses world input");
                    InventoryView bag = UnityEngine.Object.FindObjectOfType<InventoryView>();
                    Require(bag != null && bag.Slots.Count(slot => slot.gameObject.activeSelf) == game.Configuration.Config.InventorySlots,
                        "InventoryPanel uses configured capacity");
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
                    if (phase == 1) game.Players.RestorePosition(new Vector3(0, .1f, -4));
                    int previousDay = game.Farm.State.Day;
                    huds[0].NextDayButton.onClick.Invoke();
                    Require(game.Farm.State.Day == previousDay, "HUD rest requires cottage/bed access");
                    game.AdvanceDay();
                    Require(game.Farm.State.Day == previousDay + 1, "Day settlement fires exactly once through GameManager/FarmManager");
                    Require(game.Saves.HasSave && File.Exists(game.Saves.SavePath), "Next day automatically saves to disk");
                    if (phase == 3) { game.Farm.TryAct(1, FarmTool.WateringCan, out _); game.Economy.TrySpend(7); game.Time.Clock.Restore(900); }
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
                    Require(Math.Abs(game.Time.Clock.Minute - 900) < 5, "Saved clock restored");
                    Require(game.Economy.Gold == game.Configuration.Config.InitialGold - 7, "Saved gold restored");
                    Require(game.Stamina.State.Current == game.Configuration.Config.MaxStamina - game.Configuration.Config.WaterStaminaCost, "Saved stamina restored without refilling");
                    game.OpenShop();
                    Require(!game.UI.HasShop, "Out-of-range shop rejected");
                    Vector3 beside = game.Shop.Counter.position + Vector3.left * 1.5f; beside.y = 0.1f;
                    Camera camera = game.Players.Player.ViewCamera;
                    Vector3 projected = camera.WorldToScreenPoint(game.Shop.Counter.position + Vector3.up);
                    Physics.SyncTransforms();
                    Require(Physics.Raycast(camera.ScreenPointToRay(projected), out RaycastHit counterHit, 100f)
                        && counterHit.collider.GetComponentInParent<ShopCounter>() != null, "Mouse ray picks shop counter or canopy");
                    game.Players.RestorePosition(beside);
                    game.Inventory.TryAdd(game.Shop.Crop.Id, 1);
                    game.OpenShop(); game.OpenShop();
                    SessionState.SetInt(PhaseKey, 9);
                }
                else if (phase == 9 && game.State == GameFlowState.Playing)
                {
                    ShopView view = UnityEngine.Object.FindObjectOfType<ShopView>();
                    if (view == null) return;
                    Require(UnityEngine.Object.FindObjectsOfType<ShopView>().Length == 1 && view.transform.parent == game.UI.GetLayerRoot(UILayer.PopUp), "One AA shop at PopUp");
                    Require(!game.Players.Player.enabled && !game.Interactions.Controller.enabled, "Shop pauses input");
                    game.Stamina.Restore(0);
                    Require(!game.Farm.TryAct(2, FarmTool.Hoe, out string tired) && tired.Contains("体力不足") && !game.Farm.State.Cells[2].Tilled, "Zero stamina blocks work");
                    Require(UnityEngine.Object.FindObjectOfType<PrototypeHud>().StaminaFill.fillAmount == 0, "HUD stamina updates immediately");
                    long originalGold = game.Economy.Gold, seeds = game.Inventory.GetCount(game.Shop.Seed.Id);
                    view.BuyButton.onClick.Invoke();
                    Require(Math.Abs(game.Time.Clock.Minute - 900) < 5, "Saved clock restored");
                    Require(game.Economy.Gold == originalGold - game.Shop.Seed.BuyPrice && game.Inventory.GetCount(game.Shop.Seed.Id) == seeds + 1, "MVC purchase exchanges gold for seed");
                    view.SellButton.onClick.Invoke();
                    Require(Math.Abs(game.Time.Clock.Minute - 900) < 5, "Saved clock restored");
                    Require(game.Economy.Gold == originalGold - game.Shop.Seed.BuyPrice + game.Shop.Crop.SellPrice && game.Inventory.GetCount(game.Shop.Crop.Id) == 0, "MVC sale exchanges crop for gold");
                    Require(!view.SellButton.interactable && view.BalanceText.text.Contains(game.Economy.Gold.ToString()), "Shop balances and buttons refresh");
                    Require(UnityEngine.Object.FindObjectOfType<PrototypeHud>().GoldText.text.Contains(game.Economy.Gold.ToString()), "HUD gold immediately refreshes");
                    Require(view.CropButtons.Length == 5, "Three crop and two fish options wired");
                    view.CropButtons[1].onClick.Invoke();
                    Require(game.Shop.Seed.Id == "tomato_seed" && view.BuyText.text.Contains("番茄"), "Tomato option refreshes offer");
                    Action reentrant = () => Require(game.Shop.Trade(true, 1) == MagicIsland.Economy.TradeResult.Busy
                        && !game.Shop.SelectCrop(2), "Cross-crop trade/selection reentrancy rejected");
                    game.Inventory.State.Changed += reentrant;
                    view.BuyButton.onClick.Invoke(); game.Inventory.State.Changed -= reentrant;
                    view.BuyButton.onClick.Invoke();
                    game.Inventory.TryAdd("tomato", 1); view.SellButton.onClick.Invoke();
                    view.CropButtons[2].onClick.Invoke();
                    Require(game.Shop.Seed.Id == "corn_seed", "Corn option selected");
                    view.BuyButton.onClick.Invoke(); game.Inventory.TryAdd("corn", 1); view.SellButton.onClick.Invoke();
                    Require(game.Inventory.GetCount("tomato_seed") == 2 && game.Inventory.GetCount("corn_seed") == 1
                        && game.Inventory.GetCount("tomato") == 0 && game.Inventory.GetCount("corn") == 0, "Buy/sell each variety without mixing stock");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera);
                        File.Copy("Logs/InventoryPreview.png", "Logs/MultiCropShopPreview.png", true);
                    }
                    view.CropButtons[0].onClick.Invoke();
                    SessionState.SetString(Running + ".TradeGold", game.Economy.Gold.ToString());
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera);
                    view.CloseButton.onClick.Invoke();
                    Require(!game.UI.HasShop && game.Players.Player.enabled, "Shop closes and restores input");
                    game.OpenSleep(); Require(!game.UI.HasSleep, "Out-of-range sleep rejected");
                    game.Players.RestorePosition(game.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); game.EnterHome();
                    SessionState.SetInt(PhaseKey, 19);
                }
                else if (phase == 19 && game.State == GameFlowState.Playing && game.Housing.Inside)
                {
                    Vector3 bedside = game.Time.Bed.position + Vector3.right * 1.5f; bedside.y = 0.1f;
                    game.Players.RestorePosition(bedside);
                    game.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget();
                    Camera camera = game.Players.Player.ViewCamera;
                    Vector3 projected = camera.WorldToScreenPoint(game.Time.Bed.position + Vector3.up * 0.5f);
                    Physics.SyncTransforms();
                    Require(Physics.Raycast(camera.ScreenPointToRay(projected), out RaycastHit bedHit, 100f)
                        && bedHit.collider.GetComponentInParent<SleepBed>() != null, "Mouse ray picks bed");
                    game.Time.Clock.Restore(900);
                    game.OpenSleep(); game.OpenSleep();
                    SessionState.SetFloat(Running + ".SleepStarted", (float)EditorApplication.timeSinceStartup);
                    SessionState.SetInt(PhaseKey, 10);
                }
                else if (phase == 10 && game.State == GameFlowState.Playing)
                {
                    SleepView view = UnityEngine.Object.FindObjectOfType<SleepView>();
                    if (view == null || EditorApplication.timeSinceStartup - SessionState.GetFloat(Running + ".SleepStarted", 0) < 0.2) return;
                    Require(game.Time.Clock.Minute == 900, "Sleep modal pauses real clock updates");
                    Require(!game.Players.Player.enabled && !game.Interactions.Controller.enabled
                        && view.transform.parent == game.UI.GetLayerRoot(UILayer.PopUp), "Sleep modal uses AA PopUp and pauses input");
                    Require(UnityEngine.Object.FindObjectsOfType<SleepView>().Length == 1, "Duplicate sleep requests create one panel");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera);
                    view.CancelButton.onClick.Invoke();
                    Require(game.Farm.State.Day == 2 && !game.UI.HasSleep && game.Players.Player.enabled, "Cancel preserves day and restores input");
                    game.OpenSleep(); SessionState.SetInt(PhaseKey, 11);
                }
                else if (phase == 11 && game.State == GameFlowState.Playing)
                {
                    SleepView view = UnityEngine.Object.FindObjectOfType<SleepView>(); if (view == null) return;
                    view.ConfirmButton.onClick.Invoke(); game.ConfirmSleep();
                    Require(game.Farm.State.Day == 3 && game.Time.Clock.Minute == 360, "Sleep advances exactly one day to morning");
                    Require(game.Stamina.State.Current == game.Configuration.Config.MaxStamina && game.Players.Player.enabled, "Sleep refills stamina and restores movement");
                    Require(game.Farm.State.Cells[1].Growth == 2 && !game.Farm.State.Cells[1].Watered, "Sleep grows watered crop and clears watering");
                    Require(game.Saves.TryLoad(out SaveData morning) && morning.Day == 3 && morning.ClockMinute == 360, "Sleep auto-saves morning");
                    Require(morning.Stamina == game.Configuration.Config.MaxStamina, "Sleep saves refilled stamina");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera);
                    game.Players.RestorePosition(game.Housing.Interior.Exit.Position + new Vector3(0, .1f, 1.5f)); game.ExitHome();
                    SessionState.SetInt(PhaseKey, 20);
                }
                else if (phase == 20 && game.State == GameFlowState.Playing && !game.Housing.Inside)
                {
                    game.Time.Clock.Restore(900);
                    Vector3 nearPlot = game.Farm.Grid.CellCenter(2) + Vector3.back; nearPlot.y = 0.1f;
                    game.Players.RestorePosition(nearPlot); game.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget();
                    FeedbackView feedback = UnityEngine.Object.FindObjectOfType<FeedbackView>();
                    Require(feedback != null && feedback.transform.parent == game.UI.GetLayerRoot(UILayer.Top)
                        && !feedback.Group.blocksRaycasts && feedback.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(graphic => !graphic.raycastTarget), "Feedback AA Top panel never blocks clicks");
                    Require(!game.Tools.TryAct(game.Farm.State.Cells.Length - 1, out _) && !game.Tools.Busy
                        && feedback.MessageText.text.Contains("距离太远"), "Out-of-range tool failure has feedback and no animation");
                    game.ToggleInventory();
                    Require(!game.Tools.TryAct(2, out _) && !game.Farm.State.Cells[2].Tilled, "Modal blocks tool request");
                    game.CloseInventory(); game.Tools.SelectTool(FarmTool.Hoe);
                    Require(game.Tools.TryAct(2, out _) && game.Tools.Busy && game.Players.Player.ToolActionLocked, "Hoe action claims lock and commits work");
                    int staminaAfter = game.Stamina.State.Current;
                    Require(!game.Tools.TryAct(3, out _) && game.Stamina.State.Current == staminaAfter
                        && !game.Farm.State.Cells[3].Tilled && !game.Tools.SelectTool(FarmTool.Seeds), "Busy rejects double action and tool switch without debit");
                    int dayBeforeAction = game.Farm.State.Day;
                    game.AdvanceDay();
                    Require(game.Farm.State.Day == dayBeforeAction, "Busy action cannot cross day");
                    SessionState.SetInt(PhaseKey, 12);
                }
                else if (phase == 12 && game.State == GameFlowState.Playing)
                {
                    if (game.Tools.ActionProgress < 0.3f) return;
                    Require(game.Tools.Busy && Quaternion.Angle(game.Tools.Presentation.Grip.localRotation, Quaternion.identity) > 1, "Tool swing animates");
                    Require(game.Tools.Presentation.ActiveEffectCount > 0, "Successful action spawns effects");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera);
                    SessionState.SetFloat(Running + ".ToolProgress", game.Tools.ActionProgress);
                    SessionState.SetFloat(Running + ".ToolPausedAt", (float)EditorApplication.timeSinceStartup);
                    game.ToggleInventory(); SessionState.SetInt(PhaseKey, 13);
                }
                else if (phase == 13 && game.State == GameFlowState.Playing)
                {
                    if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Running + ".ToolPausedAt", 0) < 0.15) return;
                    Require(Mathf.Approximately(game.Tools.ActionProgress, SessionState.GetFloat(Running + ".ToolProgress", 0)), "Inventory freezes action progress");
                    game.CloseInventory(); SessionState.SetInt(PhaseKey, 14);
                }
                else if (phase == 14 && game.State == GameFlowState.Playing && !game.Tools.Busy)
                {
                    Require(!game.Players.Player.ToolActionLocked, "Finished animation restores movement");
                    int before = game.Stamina.State.Current;
                    Require(!game.Tools.TryAct(2, out _) && game.Stamina.State.Current == before && !game.Tools.Busy, "Invalid repeated hoe has no cost or animation");
                    FeedbackView feedback = UnityEngine.Object.FindObjectOfType<FeedbackView>();
                    Require(feedback.MessageText.text.Contains("土地已耕作"), "Failure reason shown by MVC");
                    Require(game.Tools.SelectTool(FarmTool.Seeds) && game.Tools.TryAct(2, out _) && game.Farm.State.Cells[2].Planted, "Seed action works");
                    SessionState.SetInt(PhaseKey, 15);
                }
                else if (phase == 15 && game.State == GameFlowState.Playing && !game.Tools.Busy)
                {
                    Require(game.Tools.SelectTool(FarmTool.WateringCan) && game.Tools.TryAct(2, out _) && game.Farm.State.Cells[2].Watered, "Water action works");
                    FeedbackView feedback = UnityEngine.Object.FindObjectOfType<FeedbackView>();
                    Require(feedback.DetailText.text.Contains("体力 -"), "Successful work displays stamina cost");
                    SessionState.SetInt(PhaseKey, 16);
                }
                else if (phase == 16 && game.State == GameFlowState.Playing && !game.Tools.Busy)
                {
                    // Test fixture: mature this crop to exercise harvest presentation without changing the date.
                    game.Farm.State.Cells[2].Growth = game.Configuration.Config.CropGrowthDays;
                    long crops = game.Inventory.GetCount(game.Farm.CropItemId);
                    Require(game.Tools.SelectTool(FarmTool.Hoe) && game.Tools.TryAct(2, out _)
                        && game.Inventory.GetCount(game.Farm.CropItemId) == crops + 1, "Harvest adds one crop");
                    Require(UnityEngine.Object.FindObjectOfType<FeedbackView>().DetailText.text.Contains("+1"), "Harvest feedback shows obtained crop");
                    SessionState.SetInt(PhaseKey, 17);
                }
                else if (phase == 17 && game.State == GameFlowState.Playing && !game.Tools.Busy)
                {
                    game.Time.Clock.Restore(900);
                    Require(game.Stamina.State.Current == game.Configuration.Config.MaxStamina - game.Configuration.Config.HoeStaminaCost
                        - game.Configuration.Config.SeedStaminaCost - game.Configuration.Config.WaterStaminaCost - game.Configuration.Config.HarvestStaminaCost, "Each presented action costs exactly once");
                    ChooseSeedFromBag(game, "tomato_seed");
                    Require(game.Farm.SeedItemId == "tomato_seed" && game.Tools.SelectedTool == FarmTool.Seeds
                        && UnityEngine.Object.FindObjectOfType<PrototypeHud>().SeedText.text.Contains("番茄"), "Bag seed action closes bag and updates HUD/tool");
                    Require(game.Farm.TryAct(3, FarmTool.Hoe, out _) && game.Farm.TryAct(3, FarmTool.Seeds, out _)
                        && game.Farm.TryAct(3, FarmTool.WateringCan, out _), "Plant tomato through selected seed");
                    ChooseSeedFromBag(game, "corn_seed");
                    Require(game.Farm.TryAct(4, FarmTool.Hoe, out _) && game.Farm.TryAct(4, FarmTool.Seeds, out _)
                        && game.Farm.TryAct(4, FarmTool.WateringCan, out _), "Plant corn through selected seed");
                    ChooseSeedFromBag(game, "tomato_seed");
                    Require(game.Farm.State.Cells[3].CropId == "tomato" && game.Farm.State.Cells[3].RequiredGrowthDays == 5
                        && game.Farm.State.Cells[4].CropId == "corn" && game.Farm.State.Cells[4].RequiredGrowthDays == 7, "Mixed crops retain separate growth configuration");
                    string directory = SessionState.GetString(Running + ".SaveDirectory", "");
                    string blocked = Path.Combine(directory, "blocked");
                    File.WriteAllText(blocked, "not a directory");
                    game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items, game.Configuration.Crops, blocked, tasks: game.Configuration.Tasks, orders: game.Configuration.Orders, seasons: game.Configuration.Seasons, gathering: game.Configuration.Gathering, toolUpgrades: game.Configuration.ToolUpgrades, storage: game.Configuration.Storage, npcs: game.Configuration.NPCs, relationships: game.Configuration.Relationships, stories: game.Configuration.Stories, schedules: game.Configuration.NPCSchedules, fishing: game.Configuration.Fishing, crafting: game.Configuration.Crafting);
                    game.ReturnToMainMenu();
                    Require(game.State == GameFlowState.Playing && game.Farm.State != null, "Failed save keeps active game");
                    game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items, game.Configuration.Crops, directory, tasks: game.Configuration.Tasks, orders: game.Configuration.Orders, seasons: game.Configuration.Seasons, gathering: game.Configuration.Gathering, toolUpgrades: game.Configuration.ToolUpgrades, storage: game.Configuration.Storage, npcs: game.Configuration.NPCs, relationships: game.Configuration.Relationships, stories: game.Configuration.Stories, schedules: game.Configuration.NPCSchedules, fishing: game.Configuration.Fishing, crafting: game.Configuration.Crafting);
                    game.ReturnToMainMenu();
                    SessionState.SetInt(PhaseKey, 7);
                }
                else if (phase == 7 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Tools.Presentation == null && !game.Tools.Busy && UnityEngine.Object.FindObjectOfType<FeedbackView>() == null,
                        "Tool art, locks and feedback panel released on return");
                    string save = game.Saves.SavePath;
                    Require(game.Saves.TryLoad(out SaveData latest) && latest.Gold == long.Parse(SessionState.GetString(Running + ".TradeGold", "0")), "Traded gold saved to disk");
                    Require(latest.ClockMinute >= 900 && latest.ClockMinute < 905, "Clock saved to disk");
                    Require(latest.SelectedSeedItemId == "tomato_seed" && latest.Cells[3].CropId == "tomato"
                        && latest.Cells[4].CropId == "corn", "Mixed crop IDs and seed selection saved");
                    if (!SessionState.GetBool(Running + ".MixedResumed", false))
                    { game.ContinueGame(); SessionState.SetInt(PhaseKey, 18); return; }
                    File.WriteAllText(save, "corrupt json");
                    Require(game.Saves.TryLoad(out SaveData recovered) && game.Saves.RecoveredBackup && recovered.Day == 3,
                        "Corrupt primary save recovers validated backup");
                    File.WriteAllText(save + ".bak", "{\"Version\":99}");
                    game.ContinueGame();
                    Require(game.State == GameFlowState.MainMenu && !game.Saves.HasSave
                        && !UnityEngine.Object.FindObjectOfType<MainMenuView>().ContinueButton.interactable,
                        "Corrupt or unsupported save is rejected without initializing world");
                    recovered.Version = 1;
                    // Legacy saves contained only the original crop and growth duration.
                    foreach (FarmCell cell in recovered.Cells)
                    { cell.CropId = null; cell.RequiredGrowthDays = game.Configuration.Config.CropGrowthDays; cell.Growth = Math.Min(cell.Growth, cell.RequiredGrowthDays); }
                    File.WriteAllText(save, JsonUtility.ToJson(recovered));
                    Require(game.Saves.TryLoad(out SaveData upgraded) && upgraded.Version == SaveData.CurrentVersion
                        && upgraded.Gold == game.Configuration.Config.InitialGold, "Legacy v1 saves migrate with starting gold");
                    Directory.CreateDirectory("Logs");
                    File.WriteAllText("Logs/GameFlowVerification.txt",
                        "PASS: packed AA, two new games and continued saved game.\n"
                        + "Saved day, crop flags/growth, inventory and player position restored.\n"
                        + "Autosave, save-failure retention, corrupt-file recovery, version rejection verified.\n"
                        + "TMP Chinese glyph coverage/default font, four UI layers and MVC lifecycle verified.\n"
                        + "Time restore, bed ray/range, sleep pause/cancel/confirm, next morning and autosave verified.\n"
                        + "Panel AA names, stamina HUD, exhaustion/trading, saved stamina and sleep refill verified.\n"
                        + "Tool AA feedback, four actions, animation/effects, range/modal checks, duplicate lock, pause/resume and cleanup verified.\n"
                        + "CropCatalog AA, three shop varieties, bag seed MVC, mixed crop save/resume and seed selection persistence verified.\n"
                        + "Shop AA popup, range, input pause, MVC buying/selling and gold save verified.\n");
                    Debug.Log("Game flow verification PASSED (save/resume, packed AA, Chinese TMP).");
                    SessionState.SetInt(PhaseKey, 5);
                    EditorApplication.isPlaying = false;
                }
                else if (phase == 18 && game.State == GameFlowState.Playing)
                {
                    Require(game.Farm.SeedItemId == "tomato_seed" && game.Inventory.GetCount("tomato_seed") == 1
                        && game.Inventory.GetCount("corn_seed") == 0, "Selected seed and variety stock restored");
                    Require(game.Farm.State.Cells[3].CropId == "tomato" && game.Farm.State.Cells[3].Watered && game.Farm.State.Cells[3].RequiredGrowthDays == 5
                        && game.Farm.State.Cells[4].CropId == "corn" && game.Farm.State.Cells[4].Watered && game.Farm.State.Cells[4].RequiredGrowthDays == 7, "Mixed farm restored through GameManager");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        int[] indices = { 1, 3, 4 }; int[] growth = indices.Select(index => game.Farm.State.Cells[index].Growth).ToArray();
                        foreach (int index in indices) game.Farm.State.Cells[index].Growth = game.Farm.State.Cells[index].RequiredGrowthDays;
                        game.Farm.Grid.Refresh();
                        GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/MultiCropFarmPreview.png", true);
                        for (int i = 0; i < indices.Length; i++) game.Farm.State.Cells[indices[i]].Growth = growth[i];
                        game.Farm.Grid.Refresh();
                    }
                    SessionState.SetBool(Running + ".MixedResumed", true);
                    game.ReturnToMainMenu(); SessionState.SetInt(PhaseKey, 7);
                }
            }
            catch (Exception error)
            {
                SessionState.SetBool(Running, false);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void ChooseSeedFromBag(GameManager game, string seed)
        {
            game.ToggleInventory(); InventoryView bag = UnityEngine.Object.FindObjectOfType<InventoryView>();
            Require(UnityEngine.Object.FindObjectOfType<FeedbackView>().Group.alpha == 0, "Opening bag dismisses previous tool feedback");
            int slot = -1;
            for (int i = 0; i < game.Inventory.State.Capacity; i++) if (game.Inventory.State.Slots[i].ItemId == seed) { slot = i; break; }
            Require(slot >= 0, "Seed exists in bag"); bag.Slots[slot].SelectButton.onClick.Invoke();
            Require(bag.UseSeedButton.interactable, "Supported seed enables use action");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            { GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/MultiCropBagPreview.png", true); }
            bag.UseSeedButton.onClick.Invoke(); Require(!game.UI.IsInventoryVisible, "Using seed closes bag");
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
