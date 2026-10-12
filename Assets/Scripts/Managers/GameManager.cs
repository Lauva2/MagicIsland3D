using System;
using System.Collections;
using MagicIsland.Loading;
using MagicIsland.Prototype;
using MagicIsland.Saving;
using MagicIsland.Inventory;
using MagicIsland.Farming;
using MagicIsland.Economy;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MagicIsland.Managers
{
    public enum GameFlowState { Booting, LoadingConfiguration, MainMenu, LoadingScene, InitializingGame, Playing, ReturningToMenu, Failed, Transitioning }

    public sealed partial class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public GameFlowState State { get; private set; } = GameFlowState.Booting;
        public event Action<GameFlowState> StateChanged;
        public ConfigurationManager Configuration { get; private set; }
        public SceneManager Scenes { get; private set; }
        public UIManager UI { get; private set; }
        public FarmManager Farm { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public PlayerManager Players { get; private set; }
        public CameraManager Cameras { get; private set; }
        public InteractionManager Interactions { get; private set; }
        public SaveManager Saves { get; private set; }
        public SettingsManager Settings { get; private set; }
        public EconomyManager Economy { get; private set; }
        public ShopManager Shop { get; private set; }
        public ToolManager Tools { get; private set; }
        public StaminaManager Stamina { get; private set; }
        public TimeManager Time { get; private set; }
        public WeatherManager Weather { get; private set; }
        public CalendarManager Calendar { get; private set; }
        public TaskManager Tasks { get; private set; }
        public OrderManager Orders { get; private set; }
        public GatheringManager Gathering { get; private set; }
        public ToolUpgradeManager ToolUpgrades { get; private set; }
        public StorageManager Storage { get; private set; }
        public CookingManager Cooking { get; private set; }
        public FoodManager Food { get; private set; }
        public NPCManager NPCs { get; private set; }
        public DialogueManager Dialogue { get; private set; }
        public RelationshipManager Relationships { get; private set; }
        public StoryEventManager Stories { get; private set; }
        public FishingManager Fishing { get; private set; }
        public NPCScheduleManager NPCSchedules { get; private set; }
        private bool fishingOpening, fishingCommitting, shopCommitting;
        private int fishingLoadGeneration;
        private bool giftOpening, giftCommitting;
        private bool dialogueOpening, dialogueCommitting;
        private bool cookingOpening, consumableCommitting;
        private bool storageOpening, storageCommitting;
        private bool upgradeOpening, upgradeCommitting;
        private bool orderOpening;
        private bool sleepOpening;
        private bool shopOpening;
        private bool ModalOpen => ConstructionBusy || UI.HasFishing || fishingOpening || fishingCommitting || shopCommitting || UI.HasGift || giftOpening || giftCommitting || UI.HasDialogue || dialogueOpening || dialogueCommitting || UI.HasCooking || cookingOpening || consumableCommitting || UI.HasStorage || storageOpening || storageCommitting || UI.HasToolUpgrade || upgradeOpening || upgradeCommitting || UI.IsInventoryVisible || UI.HasShop || shopOpening || UI.HasSleep || sleepOpening || UI.HasOrder || orderOpening;
        private bool settingsOpening;
        private SaveData pendingSave;
        private Scene menuScene;
        private Camera menuCamera;
        private bool startupRunning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureManager()
        {
            if (Instance == null && FindObjectOfType<GameManager>() == null)
                new GameObject("GameManager").AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Configuration = GetOrAdd<ConfigurationManager>();
            Scenes = GetOrAdd<SceneManager>();
            UI = GetOrAdd<UIManager>();
            Farm = GetOrAdd<FarmManager>();
            Inventory = GetOrAdd<InventoryManager>();
            Players = GetOrAdd<PlayerManager>();
            Cameras = GetOrAdd<CameraManager>();
            Interactions = GetOrAdd<InteractionManager>();
            Saves = GetOrAdd<SaveManager>();
            Settings = GetOrAdd<SettingsManager>();
            Economy = GetOrAdd<EconomyManager>();
            Shop = GetOrAdd<ShopManager>();
            Time = GetOrAdd<TimeManager>();
            Stamina = GetOrAdd<StaminaManager>();
            Tools = GetOrAdd<ToolManager>();
            Weather = GetOrAdd<WeatherManager>();
            Calendar = GetOrAdd<CalendarManager>();
            Tasks = GetOrAdd<TaskManager>();
            Orders = GetOrAdd<OrderManager>();
            Gathering = GetOrAdd<GatheringManager>();
            ToolUpgrades = GetOrAdd<ToolUpgradeManager>();
            Storage = GetOrAdd<StorageManager>();
            Cooking = GetOrAdd<CookingManager>(); Food = GetOrAdd<FoodManager>();
            NPCs = GetOrAdd<NPCManager>(); Dialogue = GetOrAdd<DialogueManager>();
            Relationships = GetOrAdd<RelationshipManager>();
            Stories = GetOrAdd<StoryEventManager>();
            Fishing = GetOrAdd<FishingManager>(); NPCSchedules = GetOrAdd<NPCScheduleManager>();
            Crafting = GetOrAdd<CraftingManager>(); Placements = GetOrAdd<PlacementManager>(); Housing = GetOrAdd<HousingManager>(); Furniture = GetOrAdd<FurnitureManager>();
        }

        private T GetOrAdd<T>() where T : Component => GetComponent<T>() ?? gameObject.AddComponent<T>();
        private IEnumerator Start() { if (Instance == this) yield return Startup(); }

        public void RetryStartup() { if (State == GameFlowState.Failed && !startupRunning) StartCoroutine(Startup()); }

        private IEnumerator Startup()
        {
            startupRunning = true;
            SetState(GameFlowState.LoadingConfiguration);
            yield return Configuration.LoadConfiguration();
            if (Configuration.Config == null)
            {
                Fail(Configuration.LastError);
                startupRunning = false;
                yield break;
            }
            menuScene = UnitySceneManager.GetActiveScene();
            FarmPrototype directWorld = FindWorld(menuScene);
            menuCamera = directWorld == null ? FindSceneCamera(menuScene) : null;
            Saves.Initialize(Configuration.Config, Configuration.Items, Configuration.Crops, tasks: Configuration.Tasks, orders: Configuration.Orders, seasons: Configuration.Seasons, gathering: Configuration.Gathering, toolUpgrades: Configuration.ToolUpgrades, storage: Configuration.Storage, npcs: Configuration.NPCs, relationships: Configuration.Relationships, stories: Configuration.Stories, schedules: Configuration.NPCSchedules, fishing: Configuration.Fishing, crafting: Configuration.Crafting);
            yield return UI.ShowMainMenu(EnterGame, ContinueGame, OpenSettings, QuitGame, Saves.HasSave);
            if (Saves.LastError != null && !Saves.HasSave)
                UI.SetMenuStatus("存档无法读取，可以开始新游戏", false);
            if (UI.LastError != null)
            {
                Fail(UI.LastError);
                startupRunning = false;
                yield break;
            }
            SetState(GameFlowState.MainMenu);
            startupRunning = false;
            if (directWorld != null)
            {
                // Direct scene Play still goes through the same managers/config/UI initialization.
                Scenes.UseEditorScene(menuScene);
                menuScene = UnitySceneManager.CreateScene("MainMenuBootstrap");
                StartCoroutine(InitializeGame());
            }
        }

        public void EnterGame()
        {
            if (State != GameFlowState.MainMenu || settingsOpening) return;
            pendingSave = null;
            UI.CloseSettings();
            // Set state before scheduling so repeated button presses cannot start duplicate loads.
            SetState(GameFlowState.LoadingScene);
            StartCoroutine(LoadGame());
        }

        public void OpenSettings()
        {
            if (State == GameFlowState.MainMenu && !settingsOpening) StartCoroutine(LoadSettings());
        }

        private IEnumerator LoadSettings()
        {
            settingsOpening = true;
            yield return UI.ShowSettings(Settings, UI.CloseSettings);
            settingsOpening = false;
            if (UI.LastError != null) UI.SetMenuStatus("暂时无法打开设置，请重试", false);
        }

        public void QuitGame()
        {
            if (State != GameFlowState.MainMenu) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ContinueGame()
        {
            if (State != GameFlowState.MainMenu || settingsOpening) return;
            if (!Saves.TryLoad(out pendingSave))
            {
                UI.SetSaveAvailable(false);
                UI.SetMenuStatus("存档无法读取，可以开始新游戏", false);
                return;
            }
            SetState(GameFlowState.LoadingScene);
            UI.CloseSettings();
            StartCoroutine(LoadGame());
        }

        private IEnumerator LoadGame()
        {
            UI.SetMenuStatus("正在加载岛屿…", true);
            SetMenuCameraEnabled(false);
            yield return Scenes.LoadGameScene(Configuration.Config.GameSceneAddress);
            if (!Scenes.GameScene.IsValid())
            {
                ReturnAfterFailure(Scenes.LastError);
                yield break;
            }
            yield return InitializeGame();
        }

        private IEnumerator InitializeGame()
        {
            SetState(GameFlowState.InitializingGame);
            UI.SetMenuStatus("正在准备农场…", true);
            yield return Farm.LoadVisuals(Configuration.Crops);
            if (Farm.LastError != null)
            {
                string loadError = Farm.LastError;
                yield return Scenes.UnloadGameScene(); ClearSystems(); ReturnAfterFailure(loadError); yield break;
            }
            if (!TryInitializeWorld(out string error))
            {
                yield return Scenes.UnloadGameScene();
                ClearSystems();
                ReturnAfterFailure(error);
                yield break;
            }
            yield return Orders.LoadHall(FindWorld(Scenes.GameScene).transform, Configuration.Config.FarmSize);
            if (Orders.LastError == null) yield return Gathering.LoadNodes(FindWorld(Scenes.GameScene).transform);
            if (Orders.LastError == null && Gathering.LastError == null) yield return ToolUpgrades.LoadWorkbench(FindWorld(Scenes.GameScene).transform, Orders.Hall.transform.localPosition);
            if (Orders.LastError == null && Gathering.LastError == null && ToolUpgrades.LastError == null) yield return Storage.LoadChest(FindWorld(Scenes.GameScene).transform, Configuration.Config.FarmSize);
            if (Orders.LastError == null && Gathering.LastError == null && ToolUpgrades.LastError == null && Storage.LastError == null) yield return Cooking.LoadStation(FindWorld(Scenes.GameScene).transform, Configuration.Config.FarmSize);
            if (Orders.LastError == null && Gathering.LastError == null && ToolUpgrades.LastError == null && Storage.LastError == null && Cooking.LastError == null) yield return NPCs.LoadActors(FindWorld(Scenes.GameScene).transform, Orders.Hall.transform.localPosition);
            if (Orders.LastError == null && Gathering.LastError == null && ToolUpgrades.LastError == null && Storage.LastError == null && Cooking.LastError == null && NPCs.LastError == null) yield return Fishing.LoadAssets(FindWorld(Scenes.GameScene).transform, Tools.Presentation.FishingRodRoot);
            if (Orders.LastError == null && Gathering.LastError == null && ToolUpgrades.LastError == null && Storage.LastError == null && Cooking.LastError == null && NPCs.LastError == null && Fishing.LastError == null) yield return Housing.LoadExterior(Configuration.Config.FarmSize);
            if (Housing.LastError == null && Orders.LastError == null && Gathering.LastError == null && ToolUpgrades.LastError == null && Storage.LastError == null && Cooking.LastError == null && NPCs.LastError == null && Fishing.LastError == null) yield return Placements.LoadAssets();
            if (Orders.LastError == null && Gathering.LastError == null && ToolUpgrades.LastError == null && Storage.LastError == null && Cooking.LastError == null && NPCs.LastError == null && Fishing.LastError == null && Placements.LastError == null && Housing.LastError == null) NPCSchedules.Bind();
            if (Housing.LastError != null || Placements.LastError != null || NPCSchedules.LastError != null || Orders.LastError != null || Gathering.LastError != null || ToolUpgrades.LastError != null || Storage.LastError != null || Cooking.LastError != null || NPCs.LastError != null || Fishing.LastError != null)
            {
                error = Housing.LastError ?? Placements.LastError ?? NPCSchedules.LastError ?? Orders.LastError ?? Gathering.LastError ?? ToolUpgrades.LastError ?? Storage.LastError ?? Cooking.LastError ?? NPCs.LastError ?? Fishing.LastError;
                yield return Scenes.UnloadGameScene(); ClearSystems(); ReturnAfterFailure(error); yield break;
            }
            yield return Furniture.LoadAssets();
            if (Furniture.LastError != null)
            { error = Furniture.LastError; yield return Scenes.UnloadGameScene(); ClearSystems(); ReturnAfterFailure(error); yield break; }
            yield return RestoreResidence();
            if (Housing.LastError != null || Scenes.LastError != null || Furniture.LastError != null)
            {
                error = Housing.LastError ?? Scenes.LastError ?? Furniture.LastError; yield return Scenes.UnloadGameScene(); ClearSystems(); ReturnAfterFailure(error); yield break;
            }
            yield return UI.ShowFarmHud(Farm, Interactions, Economy, Time, Stamina, Weather, RequestRest, ReturnToMainMenu, ToggleInventory);
            if (UI.LastError == null) yield return UI.PrepareInventory(Inventory, Farm, UseSeed, CloseInventory, Food, UseFood, SaveFood, Placements, BeginPlacementFromBag);
            if (UI.LastError == null) yield return UI.ShowCropInfo(Farm, Interactions);
            if (UI.LastError == null) yield return UI.ShowFeedback(Tools);
            if (UI.LastError == null) yield return UI.ShowTasks(Tasks, ClaimTaskReward);
            if (UI.LastError != null)
            {
                error = UI.LastError;
                UI.Close(RuntimeUi.InventoryAddress);
                UI.Close(RuntimeUi.HudAddress);
                UI.Close("FeedbackPanel");
                UI.Close("CropInfoPanel");
                UI.Close("TaskPanel");
                yield return Scenes.UnloadGameScene();
                ClearSystems();
                ReturnAfterFailure(error);
                yield break;
            }
            UI.SetMainMenuVisible(false);
            SetInputEnabled(true);
            SetState(GameFlowState.Playing);
        }

        private bool TryInitializeWorld(out string error)
        {
            try
            {
                FarmPrototype world = FindWorld(Scenes.GameScene);
                if (world == null) throw new InvalidOperationException("Game scene has no FarmPrototype world root");
                var config = Configuration.Config;
                var savedTasks = pendingSave?.Tasks;
                var savedOrder = pendingSave?.Order;
                var savedGathering = pendingSave?.Gathering;
                var savedUpgrades = pendingSave?.ToolUpgrades;
                var savedStorage = pendingSave?.GetStorageSlots();
                var savedNPCs = pendingSave?.NPCs;
                var savedRelationships = pendingSave?.Relationships;
                var savedStories = pendingSave?.StoryEvents;
                var savedSchedules = pendingSave?.NPCSchedules;
                var savedPlaced = pendingSave?.PlacedObjects;
                var savedFurniture = pendingSave?.Furniture;
                var savedHouseUpgrade = pendingSave?.HouseUpgrade;
                loadedPlayerArea = pendingSave?.PlayerArea ?? MagicIsland.Interaction.HousingState.Island;
                loadedPlayerPosition = pendingSave == null ? (Vector3?)null : new Vector3(pendingSave.PlayerX, pendingSave.PlayerY, pendingSave.PlayerZ);
                Inventory.Initialize(config, Configuration.Items);
                Economy.Initialize(config.InitialGold);
                Stamina.Initialize(config);
                world.CreateWorld(config.FarmSize);
                Time.Initialize(config.SecondsPerGameMinute, null);
                Farm.Initialize(world.Grid, config, Inventory, Stamina, Configuration.Crops, Calendar);
                Players.Initialize(world.Player, config);
                int weatherSeed = pendingSave != null ? pendingSave.WeatherSeed : UnityEngine.Random.Range(1, int.MaxValue);
                var weatherKind = pendingSave != null ? pendingSave.Weather : MagicIsland.WeatherSystem.WeatherKind.Sunny;
                if (pendingSave != null)
                {
                    var slots = new InventorySlot[pendingSave.Slots.Length];
                    for (int i = 0; i < slots.Length; i++)
                        slots[i] = new InventorySlot(pendingSave.Slots[i].ItemId, pendingSave.Slots[i].Quantity);
                    Inventory.State.Restore(slots);
                    Farm.State.Restore(pendingSave.Day, pendingSave.Cells);
                    Farm.SelectSeed(pendingSave.SelectedSeedItemId, false);
                    Farm.Grid.Refresh();
                    if (pendingSave.PlayerArea == MagicIsland.Interaction.HousingState.Island) Players.RestorePosition(new Vector3(pendingSave.PlayerX, pendingSave.PlayerY, pendingSave.PlayerZ));
                    Economy.Restore(pendingSave.Gold);
                    Time.Clock.Restore(pendingSave.ClockMinute);
                    Stamina.Restore(pendingSave.Stamina);
                    pendingSave = null;
                }
                Calendar.Initialize(Configuration.Seasons, Farm);
                Cameras.Initialize(world.CameraController, config);
                Weather.Initialize(Configuration.Weather, Farm, world.Sun, world.Player.ViewCamera, world.transform, weatherSeed, weatherKind, Calendar);
                Weather.Tick(Time.Clock.Minute, true);
                Housing.Initialize(Configuration.Housing, Players, world.transform, world.Sun, savedHouseUpgrade, Farm.State.Day);
                Food.Initialize(Configuration.Cooking, Inventory, Stamina);
                Storage.Initialize(Configuration.Storage, Inventory, savedStorage);
                ToolUpgrades.Initialize(Configuration.ToolUpgrades, Inventory, Economy, savedUpgrades);
                Gathering.Initialize(Configuration.Gathering, Farm.State.Day, Inventory, Stamina, savedGathering, ToolUpgrades);
                Tools.Initialize(config, Farm, Stamina, world.Player, world.transform, Gathering);
                Fishing.Initialize(Configuration.Fishing, Inventory, Stamina);
                Interactions.Initialize(world.Interaction, config, Farm, Tools);
                Shop.Initialize(config, Configuration.Items, Inventory, Economy, world.ShopCounter, Configuration.Crops, Calendar, Configuration.Fishing);
                Tasks.Initialize(Configuration.Tasks, Farm, Shop, Economy, savedTasks);
                Orders.Initialize(Configuration.Orders, Farm, Inventory, Economy, Calendar, savedOrder);
                NPCs.Initialize(Configuration.NPCs, savedNPCs);
                NPCSchedules.Initialize(Configuration.NPCSchedules, config, NPCs, Configuration.Fishing, Time.Clock.Minute, savedSchedules);
                Crafting.Initialize(Configuration.Crafting, Inventory);
                Placements.Initialize(Configuration.Crafting, Inventory, config, Players, world.transform, Configuration.NPCSchedules.Definitions(config, Configuration.NPCs, Configuration.Fishing), savedPlaced);
                Furniture.InitializeFurniture(Configuration.Crafting, Inventory, config, Players, Configuration.Housing.InteriorLayer, savedFurniture, Housing.Upgrade.Level);
                Relationships.Initialize(Configuration.Relationships, NPCs, Inventory, Farm.State.Day, savedRelationships);
                Stories.Initialize(Configuration.Stories, Relationships, savedStories);
                Cooking.Initialize(Configuration.Cooking, Inventory, Stories);
                Dialogue.Initialize(Configuration.Dialogues, NPCs, Calendar, Weather, Relationships, Stories);
                error = null;
                return true;
            }
            catch (Exception exception) { error = exception.ToString(); return false; }
        }

        private bool advancingDay;
        public void AdvanceDay()
        {
            if (advancingDay || State != GameFlowState.Playing || ConstructionBusy || ModalOpen || Tools.Busy || Farm.State.Day >= int.MaxValue - 1) return;
            advancingDay = true;
            try
            {
                Weather.ApplyRain();
                Farm.AdvanceDay();
                bool houseCompleted = Housing.NextDay(Farm.State.Day);
                if (houseCompleted) Furniture.State.ExpandHome(Housing.Upgrade.Level);
                Time.Clock.NextMorning();
                NPCSchedules.ResetMorning(Time.Clock.Minute);
                Weather.NextDay();
                Weather.Tick(Time.Clock.Minute, false);
                Housing.UpdatePresentation();
                Stamina.Refill();
                Tasks.RecordSleep();
                Orders.NextDay();
                Gathering.NextDay(Farm.State.Day);
                Relationships.State.NextDay(Farm.State.Day);
                UI.RefreshViews();
                if (SaveProgress() && houseCompleted) Interactions.SetFeedback(Housing.UpgradeFeedback);
            } finally { advancingDay = false; }
        }

        public bool SaveProgress()
        {
            if (State != GameFlowState.Playing && !(State == GameFlowState.Transitioning && housingSaveCommitting)) return false;
            Weather.ApplyRain();
            bool saved = Saves.TrySave(Farm, Inventory, Players, Economy, Time, Stamina, Weather, Tasks, Orders, Gathering, ToolUpgrades, Storage, NPCs, Relationships, Stories, NPCSchedules, Placements, Housing, Furniture);
            Interactions.SetFeedback(saved ? "进度已保存" : "保存失败，请重试返回主菜单");
            if (!saved) Debug.LogWarning("Save failed: " + Saves.LastError);
            return saved;
        }

        public void ClaimTaskReward()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || ModalOpen || Tools.Busy || !Tasks.ClaimReward()) return;
            if (!SaveProgress()) Tasks.SetSaveFailed();
            else Interactions.SetFeedback(Tasks.Feedback);
            UI.RefreshViews();
        }

        public void OpenGiftFromDialogue()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasDialogue || dialogueOpening || dialogueCommitting || !Dialogue.State.Active || Dialogue.ActiveEventId != null || !NPCs.State.HasMet(Dialogue.State.NpcId)) return;
            string id = Dialogue.State.NpcId; CloseDialogue(); OpenGift(id);
        }
        public void OpenFishing()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || ModalOpen || Tools.Busy || !Fishing.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            if (Tools.SelectedTool != FarmTool.FishingRod) { Tools.ReportFailure("选择鱼竿（6）再抛竿"); return; }
            var check = Fishing.CheckCast(); if (check != MagicIsland.FishingSystem.FishingResult.Success) { Tools.ReportFailure(FishingManager.Explain(check)); return; }
            fishingOpening = true; SetInputEnabled(false); StartCoroutine(LoadFishing(++fishingLoadGeneration));
        }
        private IEnumerator LoadFishing(int generation)
        {
            yield return UI.ShowFishing(Fishing, CastFishing, ReelFishing, SaveFishing, CloseFishing);
            if (generation != fishingLoadGeneration) yield break;
            fishingOpening = false;
            if (State != GameFlowState.Playing) { CloseFishing(); yield break; }
            if (UI.LastError != null || !UI.HasFishing) { CloseFishing(); Interactions.SetFeedback("暂时无法打开钓鱼面板，请重试"); yield break; }
            Fishing.Focus(Players.Player.Visual); CastFishing();
        }
        public void CastFishing()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasFishing || fishingOpening || fishingCommitting || Tools.Busy || Tools.SelectedTool != FarmTool.FishingRod) return;
            if (!Fishing.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) { CloseFishing(); Interactions.SetFeedback("距离钓点太远，已取消垂钓"); return; }
            Fishing.Cast(); UI.RefreshViews();
        }
        public void ReelFishing()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasFishing || fishingOpening || fishingCommitting) return;
            if (!Fishing.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) { CloseFishing(); Interactions.SetFeedback("距离钓点太远，已取消垂钓"); return; }
            fishingCommitting = true;
            try { if (Fishing.Reel()) { if (!SaveProgress()) Fishing.SetSaveFailed(); else Interactions.SetFeedback(Fishing.Feedback); } UI.RefreshViews(); }
            finally { fishingCommitting = false; }
        }
        public void SaveFishing()
        { if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasFishing || fishingOpening || fishingCommitting) return; if (!SaveProgress()) Fishing.SetSaveFailed(); else Fishing.SetSaved(); UI.RefreshViews(); }
        public void CloseFishing()
        { if (fishingCommitting) return; fishingLoadGeneration++; fishingOpening = false; UI.CloseFishing(); Fishing.Cancel(); if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen); }
        public void OpenGift(string id)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || ModalOpen || Tools.Busy || !NPCs.InRange(id, Players.Player.transform.position, Configuration.Config.InteractionReach) || !Relationships.Prepare(id)) return;
            giftOpening = true; NPCs.Focus(id, Players.Player.transform.position); SetInputEnabled(false); StartCoroutine(LoadGift());
        }
        private IEnumerator LoadGift()
        {
            yield return UI.ShowGift(Relationships, GiveGift, CloseGift, SaveGift); giftOpening = false;
            if (State != GameFlowState.Playing) { CloseGift(); yield break; }
            if (UI.LastError != null || !UI.HasGift) { CloseGift(); Interactions.SetFeedback("\u6682\u65f6\u65e0\u6cd5\u6253\u5f00\u9001\u793c\u9762\u677f\uff0c\u8bf7\u91cd\u8bd5"); }
        }
        public void CloseGift()
        { if (giftCommitting) return; UI.CloseGift(); NPCs.Unfocus(); if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen); }
        public void GiveGift(int slot)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasGift || giftOpening || giftCommitting || Tools.Busy || !NPCs.InRange(Relationships.TargetId, Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            giftCommitting = true;
            try { if (Relationships.Give(slot)) { if (!SaveProgress()) Relationships.SetSaveFailed(); else Interactions.SetFeedback(Relationships.Feedback); } UI.RefreshViews(); }
            finally { giftCommitting = false; }
        }
        public void SaveGift()
        { if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasGift || giftOpening || giftCommitting) return; if (!SaveProgress()) Relationships.SetSaveFailed(); else Relationships.SetSaved(); UI.RefreshViews(); }

        public void OpenDialogue(string id)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || ModalOpen || Tools.Busy || !NPCs.InRange(id, Players.Player.transform.position, Configuration.Config.InteractionReach) || !Dialogue.Begin(id)) return;
            dialogueOpening = true; NPCs.Focus(id, Players.Player.transform.position); SetInputEnabled(false); StartCoroutine(LoadDialogue());
        }
        private IEnumerator LoadDialogue()
        {
            yield return UI.ShowDialogue(Dialogue, AdvanceDialogue, CloseDialogue, OpenGiftFromDialogue, OpenStoryFromDialogue); dialogueOpening = false;
            if (State != GameFlowState.Playing) { CloseDialogue(); yield break; }
            if (UI.LastError != null || !UI.HasDialogue) { CloseDialogue(); Interactions.SetFeedback("\u6682\u65f6\u65e0\u6cd5\u6253\u5f00\u5bf9\u8bdd\uff0c\u8bf7\u91cd\u8bd5"); }
        }
        public void CloseDialogue()
        {
            if (dialogueCommitting) return;
            UI.CloseDialogue(); Dialogue.Cancel(); NPCs.Unfocus(); if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void OpenStoryFromDialogue()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasDialogue || dialogueOpening || dialogueCommitting || !Dialogue.State.Active || Dialogue.ActiveEventId != null || Tools.Busy) return;
            string npcId = Dialogue.State.NpcId; var entry = Stories.Next(npcId);
            if (entry == null || !Stories.State.Available(entry.Id) || !NPCs.InRange(npcId, Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            Dialogue.Cancel(); Dialogue.BeginEvent(entry.Id); UI.RefreshViews();
        }
        public void AdvanceDialogue()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasDialogue || dialogueOpening || dialogueCommitting || !Dialogue.State.Active) return;
            if (!NPCs.InRange(Dialogue.State.NpcId, Players.Player.transform.position, Configuration.Config.InteractionReach)) { CloseDialogue(); return; }
            dialogueCommitting = true;
            try
            {
                string id = Dialogue.State.NpcId;
                if (Dialogue.State.Advance())
                {
                    string eventId = Dialogue.ActiveEventId;
                    bool story = eventId != null && Stories.State.Complete(eventId);
                    bool first = eventId == null && NPCs.State.MarkMet(id); UI.CloseDialogue(); Dialogue.Cancel(); NPCs.Unfocus();
                    if (story)
                    {
                        string result = string.IsNullOrEmpty(Stories.Catalog.Find(eventId).RecipeId) ? "小岛往事已记录" : "新食谱已解锁：蔬菜浓汤";
                        Interactions.SetFeedback(SaveProgress() ? result : result + "，保存失败，可在料理台保存进度或返回菜单重试");
                    }
                    else if (first && !SaveProgress()) Interactions.SetFeedback("\u5df2\u8bb0\u5f55\u89c1\u9762\uff0c\u4fdd\u5b58\u5931\u8d25\uff0c\u8bf7\u91cd\u8bd5\u8fd4\u56de\u4e3b\u83dc\u5355");
                    else Interactions.SetFeedback("\u4ea4\u8c08\u7ed3\u675f\uff0c\u795d\u4f60\u4eca\u5929\u7684\u5c9b\u5c7f\u751f\u6d3b\u6109\u5feb");
                }
                else UI.RefreshViews();
            }
            finally { dialogueCommitting = false; if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen); }
        }

        public void OpenCooking()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || ModalOpen || Tools.Busy || !Cooking.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            cookingOpening = true; SetInputEnabled(false); StartCoroutine(LoadCooking());
        }
        private IEnumerator LoadCooking()
        {
            yield return UI.ShowCooking(Cooking, CookRecipe, CloseCooking, SaveCooking); cookingOpening = false;
            if (State != GameFlowState.Playing) { UI.CloseCooking(); yield break; }
            if (UI.LastError != null || !UI.HasCooking) { UI.CloseCooking(); SetInputEnabled(!ModalOpen); Interactions.SetFeedback("暂时无法打开料理台，请重试"); }
        }
        public void CloseCooking()
        { if (consumableCommitting) return; UI.CloseCooking(); if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen); }
        public void CookRecipe(int index)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasCooking || cookingOpening || consumableCommitting || Tools.Busy
                || !Cooking.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            consumableCommitting = true;
            try { if (Cooking.Cook(index)) { if (!SaveProgress()) Cooking.SetSaveFailed(); else Interactions.SetFeedback(Cooking.Feedback); } UI.RefreshViews(); }
            finally { consumableCommitting = false; }
        }
        public void UseFood(int index)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.IsInventoryVisible || consumableCommitting || Tools.Busy || UI.HasCooking || cookingOpening || UI.HasDialogue || dialogueOpening || dialogueCommitting || UI.HasGift || giftOpening || giftCommitting || UI.HasFishing || fishingOpening || fishingCommitting) return;
            consumableCommitting = true;
            try { if (Food.Use(index)) { if (!SaveProgress()) Food.SetSaveFailed(); else Interactions.SetFeedback(Food.Feedback); } UI.RefreshViews(); }
            finally { consumableCommitting = false; }
        }
        public void SaveCooking()
        { if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasCooking || consumableCommitting || cookingOpening) return; if (!SaveProgress()) Cooking.SetSaveFailed(); else Cooking.SetSaved(); UI.RefreshViews(); }
        public void SaveFood()
        { if (State != GameFlowState.Playing || ConstructionBusy || !UI.IsInventoryVisible || consumableCommitting) return; if (!SaveProgress()) { Food.SetSaveFailed(); ActivePlacement.SetSaveFailed(); } else { Food.SetSaved(); ActivePlacement.SetSaved(); } UI.RefreshViews(); }

        public void OpenStorage()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || ModalOpen || Tools.Busy || !Storage.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            storageOpening = true; SetInputEnabled(false); StartCoroutine(LoadStorage());
        }
        private IEnumerator LoadStorage()
        {
            yield return UI.ShowStorage(Storage, TransferStorage, CloseStorage, SaveStorage);
            storageOpening = false;
            if (State != GameFlowState.Playing) { UI.CloseStorage(); yield break; }
            if (UI.LastError != null || !UI.HasStorage)
            { UI.CloseStorage(); SetInputEnabled(!ModalOpen); Interactions.SetFeedback("暂时无法打开储物箱，请重试"); }
        }
        public void CloseStorage()
        {
            if (storageCommitting) return;
            UI.CloseStorage(); if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void TransferStorage(bool deposit, int index, int quantity)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasStorage || storageOpening || storageCommitting || Tools.Busy
                || !Storage.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            storageCommitting = true;
            try
            {
                if (Storage.Transfer(deposit, index, quantity)) { if (!SaveProgress()) Storage.SetSaveFailed(); else Interactions.SetFeedback(Storage.Feedback); }
                UI.RefreshViews();
            }
            finally { storageCommitting = false; }
        }
        public void SaveStorage()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasStorage || storageCommitting || storageOpening) return;
            if (!SaveProgress()) Storage.SetSaveFailed(); else Storage.SetSaved(); UI.RefreshViews();
        }

        public void OpenToolUpgrade()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || ModalOpen || Tools.Busy || !ToolUpgrades.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            upgradeOpening = true; SetInputEnabled(false); StartCoroutine(LoadToolUpgrade());
        }
        private IEnumerator LoadToolUpgrade()
        {
            yield return UI.ShowToolUpgrade(ToolUpgrades, UpgradeTool, CloseToolUpgrade, SaveToolUpgrades, SwitchToCrafting);
            upgradeOpening = false;
            if (State != GameFlowState.Playing) { UI.CloseToolUpgrade(); yield break; }
            if (UI.LastError != null || !UI.HasToolUpgrade)
            { UI.CloseToolUpgrade(); SetInputEnabled(!ModalOpen); Interactions.SetFeedback("暂时无法打开工作台，请重试"); }
        }
        public void CloseToolUpgrade()
        {
            UI.CloseToolUpgrade(); if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void UpgradeTool(MagicIsland.Farming.FarmTool tool)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasToolUpgrade || upgradeOpening || upgradeCommitting || Tools.Busy
                || !ToolUpgrades.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            upgradeCommitting = true;
            try
            {
                if (ToolUpgrades.Upgrade(tool)) { if (!SaveProgress()) ToolUpgrades.SetSaveFailed(); else Interactions.SetFeedback(ToolUpgrades.Feedback); }
                UI.RefreshViews();
            }
            finally { upgradeCommitting = false; }
        }
        public void SaveToolUpgrades()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasToolUpgrade || upgradeCommitting || upgradeOpening) return;
            if (!SaveProgress()) ToolUpgrades.SetSaveFailed(); else ToolUpgrades.SetSaved(); UI.RefreshViews();
        }

        public void OpenOrder()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || ModalOpen || Tools.Busy
                || !Orders.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            orderOpening = true;
            SetInputEnabled(false);
            StartCoroutine(LoadOrder());
        }
        public bool TryGather(int index, out string message)
        {
            message = "当前无法采集";
            if (State != GameFlowState.Playing || ConstructionBusy || ModalOpen || !Tools.TryGather(index, out message)) return false;
            if (Gathering.State.IsDepleted(index) && !SaveProgress()) message = "已采集，保存失败，请重试返回主菜单";
            return true;
        }
        private IEnumerator LoadOrder()
        {
            yield return UI.ShowOrder(Orders, DeliverOrder, CloseOrder, OpenHouseUpgradeFromOrders);
            orderOpening = false;
            if (State != GameFlowState.Playing) { UI.CloseOrder(); yield break; }
            if (UI.LastError != null || !UI.HasOrder)
            {
                UI.CloseOrder(); SetInputEnabled(!ModalOpen);
                Interactions.SetFeedback("暂时无法打开公告栏，请重试");
            }
        }
        public void CloseOrder()
        {
            UI.CloseOrder();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void DeliverOrder()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasOrder || orderOpening || Tools.Busy
                || !Orders.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach) || !Orders.Deliver()) return;
            if (!SaveProgress()) Orders.SetSaveFailed();
            else Interactions.SetFeedback(Orders.Feedback);
            UI.RefreshViews();
        }

        public bool UseSeed(string id)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || Tools.Busy || UI.HasStorage || storageOpening || storageCommitting || UI.HasCooking || cookingOpening || consumableCommitting || UI.HasDialogue || dialogueOpening || dialogueCommitting || UI.HasGift || giftOpening || giftCommitting || UI.HasFishing || fishingOpening || fishingCommitting || !Farm.SelectSeed(id)) return false;
            CloseInventory(); Tools.SelectTool(MagicIsland.Farming.FarmTool.Seeds); return true;
        }
        public void ToggleInventory()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasInventory || UI.HasShop || shopOpening || UI.HasSleep || sleepOpening || UI.HasOrder || orderOpening || UI.HasToolUpgrade || upgradeOpening || UI.HasStorage || storageOpening || UI.HasCooking || cookingOpening || consumableCommitting || UI.HasDialogue || dialogueOpening || dialogueCommitting || UI.HasGift || giftOpening || giftCommitting || UI.HasFishing || fishingOpening || fishingCommitting) return;
            bool visible = !UI.IsInventoryVisible;
            UI.SetInventoryVisible(visible);
            SetInputEnabled(!visible);
        }
        public void CloseInventory()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || UI.HasShop || shopOpening || UI.HasSleep || sleepOpening || UI.HasOrder || orderOpening || UI.HasToolUpgrade || upgradeOpening || UI.HasStorage || storageOpening || UI.HasCooking || cookingOpening || consumableCommitting || UI.HasDialogue || dialogueOpening || dialogueCommitting || UI.HasGift || giftOpening || giftCommitting || UI.HasFishing || fishingOpening || fishingCommitting) return;
            UI.SetInventoryVisible(false);
            SetInputEnabled(true);
        }
        public void OpenShop()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || shopOpening || UI.HasShop || UI.HasSleep || sleepOpening || UI.HasOrder || orderOpening || UI.HasToolUpgrade || upgradeOpening || UI.HasStorage || storageOpening || UI.HasCooking || cookingOpening || consumableCommitting || UI.HasDialogue || dialogueOpening || dialogueCommitting || UI.HasGift || giftOpening || giftCommitting || UI.HasFishing || fishingOpening || fishingCommitting || Tools.Busy
                || !Shop.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            UI.SetInventoryVisible(false);
            SetInputEnabled(false);
            StartCoroutine(LoadShop());
        }
        private IEnumerator LoadShop()
        {
            shopOpening = true;
            yield return UI.ShowShop(Shop, CloseShop, TradeShop, SaveShop);
            shopOpening = false;
            if (State != GameFlowState.Playing) { UI.CloseShop(); yield break; }
            if (UI.LastError != null || !UI.HasShop)
            {
                UI.CloseShop();
                SetInputEnabled(true);
                Interactions.SetFeedback("暂时无法打开商店，请重试");
            }
        }
        public void CloseShop()
        {
            if (shopCommitting) return;
            UI.CloseShop();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public TradeResult TradeShop(bool buy)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasShop || shopOpening || shopCommitting || Tools.Busy || !Shop.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return TradeResult.Busy;
            shopCommitting = true;
            try { var result = Shop.Trade(buy, 1); if (result == TradeResult.Success && Shop.SelectedIsFish) { if (!SaveProgress()) Shop.SetSaveFailed(); else Shop.SetSaved(); } return result; }
            finally { shopCommitting = false; }
        }
        public void SaveShop()
        { if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasShop || shopOpening || shopCommitting) return; if (!SaveProgress()) Shop.SetSaveFailed(); else Shop.SetSaved(); UI.RefreshViews(); }
        public void OpenSleep()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || ModalOpen
                || !Time.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            if (Tools.Busy) { Tools.ReportFailure("正在操作，请稍候"); return; }
            sleepOpening = true;
            SetInputEnabled(false);
            StartCoroutine(LoadSleep());
        }
        private IEnumerator LoadSleep()
        {
            yield return UI.ShowSleep(ConfirmSleep, CloseSleep);
            sleepOpening = false;
            if (State != GameFlowState.Playing) { UI.CloseSleep(); yield break; }
            if (!UI.HasSleep) SetInputEnabled(!ModalOpen);
            if (UI.LastError != null) Interactions.SetFeedback("暂时无法打开休息面板，请重试");
        }
        public void CloseSleep()
        {
            UI.CloseSleep();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void ConfirmSleep()
        {
            if (State != GameFlowState.Playing || ConstructionBusy || !UI.HasSleep || sleepOpening) return;
            CloseSleep();
            AdvanceDay();
        }
        private void Update()
        {
            if (State == GameFlowState.MainMenu && !settingsOpening && Input.GetKeyDown(KeyCode.Escape)) UI.CloseSettings();
            if (State != GameFlowState.Playing) return;
            if (UI.HasFishing && !fishingOpening && !fishingCommitting)
            {
                if (!Fishing.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) CloseFishing();
                else Fishing.Tick(UnityEngine.Time.deltaTime);
            }
            UpdateConstruction();
            Time.Tick(UnityEngine.Time.deltaTime, ModalOpen);
            NPCSchedules.Tick(Time.Clock.Minute, UnityEngine.Time.deltaTime, ModalOpen);
            Weather.Tick(Time.Clock.Minute, ModalOpen);
            Housing.UpdatePresentation();
            if (Time.Clock.Finished && !ModalOpen) Interactions.SetFeedback("已经深夜了，请到床边睡觉");
            if (Input.GetKeyDown(KeyCode.B)) ToggleInventory();
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (ActivePlacement.Active || placementOpening) CancelPlacement();
                else if (HouseUpgradeBusy) CloseHouseUpgrade();
                else if (UI.HasCrafting || craftingOpening) CloseCrafting();
                else if (UI.HasFishing || fishingOpening) CloseFishing();
                else if (UI.HasGift || giftOpening) CloseGift();
                else if (UI.HasDialogue || dialogueOpening) CloseDialogue();
                else if (UI.HasCooking || cookingOpening) CloseCooking();
                else if (UI.HasStorage || storageOpening) CloseStorage();
                else if (UI.HasToolUpgrade || upgradeOpening) CloseToolUpgrade();
                else if (UI.HasOrder || orderOpening) CloseOrder();
                else if (UI.HasSleep || sleepOpening) CloseSleep();
                else if (UI.HasShop || shopOpening) CloseShop();
                else if (UI.IsInventoryVisible) CloseInventory();
            }
        }

        public void ReturnToMainMenu()
        {
            if (advancingDay || houseUpgradeCommitting || craftingCommitting || placementCommitting || fishingCommitting || shopCommitting || giftCommitting || dialogueCommitting || storageCommitting || consumableCommitting || State != GameFlowState.Playing) return;
            if (!SaveProgress()) return;
            CloseConstructionForExit();
            SetState(GameFlowState.ReturningToMenu);
            UI.CloseSleep();
            UI.CloseShop();
            CloseFishing();
            UI.CloseHouseUpgrade(); houseUpgradeGeneration++; houseUpgradeOpening = false; UI.CloseOrder(); UI.CloseToolUpgrade(); UI.CloseStorage(); UI.CloseCooking(); UI.CloseDialogue(); UI.CloseGift(); Dialogue.Cancel(); NPCs.Unfocus();
            StartCoroutine(ReturnToMenu());
        }

        private IEnumerator ReturnToMenu()
        {
            SetInputEnabled(false);
            // Keep the HUD until unloading succeeds so a failure can restore gameplay.
            if (menuScene.IsValid() && menuScene.isLoaded) UnitySceneManager.SetActiveScene(menuScene);
            yield return Scenes.UnloadGameScene();
            if (Scenes.LastError != null)
            {
                Debug.LogError(Scenes.LastError);
                SetInputEnabled(true);
                SetState(GameFlowState.Playing);
                yield break;
            }
            UI.Close(RuntimeUi.HudAddress);
            UI.Close("FeedbackPanel");
            UI.Close("CropInfoPanel");
            UI.Close("TaskPanel");
            UI.Close(RuntimeUi.InventoryAddress);
            ClearSystems();
            SetMenuCameraEnabled(true);
            UI.SetMainMenuVisible(true);
            UI.SetMenuStatus("种植、浇水、收获，开始你的岛屿生活", false);
            UI.SetSaveAvailable(Saves.HasSave);
            SetState(GameFlowState.MainMenu);
        }

        private void SetInputEnabled(bool enabled)
        {
            Tools.SetInputEnabled(enabled && !Housing.Inside);
            Players.SetInputEnabled(enabled);
            Cameras.SetInputEnabled(enabled);
            Interactions.SetInputEnabled(enabled);
        }

        private void ClearSystems() { UI.CloseHouseUpgrade(); houseUpgradeGeneration++; houseUpgradeOpening = false; UI.CloseSceneTransition(); Furniture.Clear(); Housing.Clear(); loadedPlayerPosition = null; loadedPlayerArea = MagicIsland.Interaction.HousingState.Island; CloseConstructionForExit(); Placements.Clear(); Crafting.Clear(); NPCSchedules.Clear(); Fishing.Clear(); Dialogue.Clear(); Cooking.Clear(); Stories.Clear(); Relationships.Clear(); NPCs.Clear(); Food.Clear(); Storage.Clear(); ToolUpgrades.Clear(); Gathering.Clear(); Orders.Clear(); Tasks.Clear(); Tools.Clear(); Weather.Clear(); Calendar.Clear(); Stamina.Clear(); Time.Clear(); Shop.Clear(); Economy.Clear(); Farm.Clear(); Inventory.Clear(); Players.Clear(); Cameras.Clear(); Interactions.Clear(); }
        private void SetMenuCameraEnabled(bool enabled)
        {
            if (menuCamera == null) return;
            menuCamera.enabled = enabled;
            AudioListener listener = menuCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = enabled;
        }
        private void ReturnAfterFailure(string error)
        {
            Debug.LogError(error);
            SetMenuCameraEnabled(true);
            UI.SetMainMenuVisible(true);
            UI.SetMenuStatus("暂时无法进入岛屿，请重试", false);
            SetState(GameFlowState.MainMenu);
        }
        private void Fail(string error) { Debug.LogError(error); SetState(GameFlowState.Failed); }
        private void SetState(GameFlowState state) { State = state; StateChanged?.Invoke(state); }

        private static FarmPrototype FindWorld(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                FarmPrototype world = root.GetComponentInChildren<FarmPrototype>(true);
                if (world != null) return world;
            }
            return null;
        }
        private static Camera FindSceneCamera(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Camera camera = root.GetComponentInChildren<Camera>(true);
                if (camera != null) return camera;
            }
            return null;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
