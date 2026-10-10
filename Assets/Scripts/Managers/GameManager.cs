using System;
using System.Collections;
using MagicIsland.Loading;
using MagicIsland.Prototype;
using MagicIsland.Saving;
using MagicIsland.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MagicIsland.Managers
{
    public enum GameFlowState { Booting, LoadingConfiguration, MainMenu, LoadingScene, InitializingGame, Playing, ReturningToMenu, Failed }

    public sealed class GameManager : MonoBehaviour
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
        private bool storageOpening, storageCommitting;
        private bool upgradeOpening, upgradeCommitting;
        private bool orderOpening;
        private bool sleepOpening;
        private bool shopOpening;
        private bool ModalOpen => UI.HasStorage || storageOpening || storageCommitting || UI.HasToolUpgrade || upgradeOpening || upgradeCommitting || UI.IsInventoryVisible || UI.HasShop || shopOpening || UI.HasSleep || sleepOpening || UI.HasOrder || orderOpening;
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
            Saves.Initialize(Configuration.Config, Configuration.Items, Configuration.Crops, tasks: Configuration.Tasks, orders: Configuration.Orders, seasons: Configuration.Seasons, gathering: Configuration.Gathering, toolUpgrades: Configuration.ToolUpgrades, storage: Configuration.Storage);
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
            if (Orders.LastError != null || Gathering.LastError != null || ToolUpgrades.LastError != null || Storage.LastError != null)
            {
                error = Orders.LastError ?? Gathering.LastError ?? ToolUpgrades.LastError ?? Storage.LastError;
                yield return Scenes.UnloadGameScene(); ClearSystems(); ReturnAfterFailure(error); yield break;
            }
            yield return UI.ShowFarmHud(Farm, Interactions, Economy, Time, Stamina, Weather, AdvanceDay, ReturnToMainMenu, ToggleInventory);
            if (UI.LastError == null) yield return UI.PrepareInventory(Inventory, Farm, UseSeed, CloseInventory);
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
                Inventory.Initialize(config, Configuration.Items);
                Economy.Initialize(config.InitialGold);
                Stamina.Initialize(config);
                world.CreateWorld(config.FarmSize);
                Time.Initialize(config.SecondsPerGameMinute, world.Bed);
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
                    Players.RestorePosition(new Vector3(pendingSave.PlayerX, pendingSave.PlayerY, pendingSave.PlayerZ));
                    Economy.Restore(pendingSave.Gold);
                    Time.Clock.Restore(pendingSave.ClockMinute);
                    Stamina.Restore(pendingSave.Stamina);
                    pendingSave = null;
                }
                Calendar.Initialize(Configuration.Seasons, Farm);
                Cameras.Initialize(world.CameraController, config);
                Weather.Initialize(Configuration.Weather, Farm, world.Sun, world.Player.ViewCamera, world.transform, weatherSeed, weatherKind, Calendar);
                Weather.Tick(Time.Clock.Minute, true);
                Storage.Initialize(Configuration.Storage, Inventory, savedStorage);
                ToolUpgrades.Initialize(Configuration.ToolUpgrades, Inventory, Economy, savedUpgrades);
                Gathering.Initialize(Configuration.Gathering, Farm.State.Day, Inventory, Stamina, savedGathering, ToolUpgrades);
                Tools.Initialize(config, Farm, Stamina, world.Player, world.transform, Gathering);
                Interactions.Initialize(world.Interaction, config, Farm, Tools);
                Shop.Initialize(config, Configuration.Items, Inventory, Economy, world.ShopCounter, Configuration.Crops, Calendar);
                Tasks.Initialize(Configuration.Tasks, Farm, Shop, Economy, savedTasks);
                Orders.Initialize(Configuration.Orders, Farm, Inventory, Economy, Calendar, savedOrder);
                error = null;
                return true;
            }
            catch (Exception exception) { error = exception.ToString(); return false; }
        }

        public void AdvanceDay()
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || Farm.State.Day >= int.MaxValue - 1) return;
            Weather.ApplyRain();
            Farm.AdvanceDay();
            Time.Clock.NextMorning();
            Weather.NextDay();
            Weather.Tick(Time.Clock.Minute, false);
            Stamina.Refill();
            Tasks.RecordSleep();
            Orders.NextDay();
            Gathering.NextDay(Farm.State.Day);
            UI.RefreshViews();
            SaveProgress();
        }

        public bool SaveProgress()
        {
            if (State != GameFlowState.Playing) return false;
            Weather.ApplyRain();
            bool saved = Saves.TrySave(Farm, Inventory, Players, Economy, Time, Stamina, Weather, Tasks, Orders, Gathering, ToolUpgrades, Storage);
            Interactions.SetFeedback(saved ? "进度已保存" : "保存失败，请重试返回主菜单");
            if (!saved) Debug.LogWarning("Save failed: " + Saves.LastError);
            return saved;
        }

        public void ClaimTaskReward()
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !Tasks.ClaimReward()) return;
            if (!SaveProgress()) Tasks.SetSaveFailed();
            else Interactions.SetFeedback(Tasks.Feedback);
            UI.RefreshViews();
        }

        public void OpenStorage()
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !Storage.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
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
            if (State != GameFlowState.Playing || !UI.HasStorage || storageOpening || storageCommitting || Tools.Busy
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
            if (State != GameFlowState.Playing || !UI.HasStorage || storageCommitting || storageOpening) return;
            if (!SaveProgress()) Storage.SetSaveFailed(); else Storage.SetSaved(); UI.RefreshViews();
        }

        public void OpenToolUpgrade()
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !ToolUpgrades.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            upgradeOpening = true; SetInputEnabled(false); StartCoroutine(LoadToolUpgrade());
        }
        private IEnumerator LoadToolUpgrade()
        {
            yield return UI.ShowToolUpgrade(ToolUpgrades, UpgradeTool, CloseToolUpgrade, SaveToolUpgrades);
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
            if (State != GameFlowState.Playing || !UI.HasToolUpgrade || upgradeOpening || upgradeCommitting || Tools.Busy
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
            if (State != GameFlowState.Playing || !UI.HasToolUpgrade || upgradeCommitting || upgradeOpening) return;
            if (!SaveProgress()) ToolUpgrades.SetSaveFailed(); else ToolUpgrades.SetSaved(); UI.RefreshViews();
        }

        public void OpenOrder()
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy
                || !Orders.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            orderOpening = true;
            SetInputEnabled(false);
            StartCoroutine(LoadOrder());
        }
        public bool TryGather(int index, out string message)
        {
            message = "当前无法采集";
            if (State != GameFlowState.Playing || ModalOpen || !Tools.TryGather(index, out message)) return false;
            if (Gathering.State.IsDepleted(index) && !SaveProgress()) message = "已采集，保存失败，请重试返回主菜单";
            return true;
        }
        private IEnumerator LoadOrder()
        {
            yield return UI.ShowOrder(Orders, DeliverOrder, CloseOrder);
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
            if (State != GameFlowState.Playing || !UI.HasOrder || orderOpening || Tools.Busy
                || !Orders.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach) || !Orders.Deliver()) return;
            if (!SaveProgress()) Orders.SetSaveFailed();
            else Interactions.SetFeedback(Orders.Feedback);
            UI.RefreshViews();
        }

        public bool UseSeed(string id)
        {
            if (State != GameFlowState.Playing || Tools.Busy || UI.HasStorage || storageOpening || storageCommitting || !Farm.SelectSeed(id)) return false;
            CloseInventory(); Tools.SelectTool(MagicIsland.Farming.FarmTool.Seeds); return true;
        }
        public void ToggleInventory()
        {
            if (State != GameFlowState.Playing || !UI.HasInventory || UI.HasShop || shopOpening || UI.HasSleep || sleepOpening || UI.HasOrder || orderOpening || UI.HasToolUpgrade || upgradeOpening || UI.HasStorage || storageOpening) return;
            bool visible = !UI.IsInventoryVisible;
            UI.SetInventoryVisible(visible);
            SetInputEnabled(!visible);
        }
        public void CloseInventory()
        {
            if (State != GameFlowState.Playing || UI.HasShop || shopOpening || UI.HasSleep || sleepOpening || UI.HasOrder || orderOpening || UI.HasToolUpgrade || upgradeOpening || UI.HasStorage || storageOpening) return;
            UI.SetInventoryVisible(false);
            SetInputEnabled(true);
        }
        public void OpenShop()
        {
            if (State != GameFlowState.Playing || shopOpening || UI.HasShop || UI.HasSleep || sleepOpening || UI.HasOrder || orderOpening || UI.HasToolUpgrade || upgradeOpening || UI.HasStorage || storageOpening || Tools.Busy
                || !Shop.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            UI.SetInventoryVisible(false);
            SetInputEnabled(false);
            StartCoroutine(LoadShop());
        }
        private IEnumerator LoadShop()
        {
            shopOpening = true;
            yield return UI.ShowShop(Shop, CloseShop);
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
            UI.CloseShop();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void OpenSleep()
        {
            if (State != GameFlowState.Playing || ModalOpen
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
            if (State != GameFlowState.Playing || !UI.HasSleep || sleepOpening) return;
            CloseSleep();
            AdvanceDay();
        }
        private void Update()
        {
            if (State == GameFlowState.MainMenu && !settingsOpening && Input.GetKeyDown(KeyCode.Escape)) UI.CloseSettings();
            if (State != GameFlowState.Playing) return;
            Time.Tick(UnityEngine.Time.deltaTime, ModalOpen);
            Weather.Tick(Time.Clock.Minute, ModalOpen);
            if (Time.Clock.Finished && !ModalOpen) Interactions.SetFeedback("已经深夜了，请到床边睡觉");
            if (Input.GetKeyDown(KeyCode.B)) ToggleInventory();
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (UI.HasStorage || storageOpening) CloseStorage();
                else if (UI.HasToolUpgrade || upgradeOpening) CloseToolUpgrade();
                else if (UI.HasOrder || orderOpening) CloseOrder();
                else if (UI.HasSleep || sleepOpening) CloseSleep();
                else if (UI.HasShop || shopOpening) CloseShop();
                else if (UI.IsInventoryVisible) CloseInventory();
            }
        }

        public void ReturnToMainMenu()
        {
            if (storageCommitting || State != GameFlowState.Playing) return;
            if (!SaveProgress()) return;
            SetState(GameFlowState.ReturningToMenu);
            UI.CloseSleep();
            UI.CloseShop();
            UI.CloseOrder(); UI.CloseToolUpgrade(); UI.CloseStorage();
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
            Tools.SetInputEnabled(enabled);
            Players.SetInputEnabled(enabled);
            Cameras.SetInputEnabled(enabled);
            Interactions.SetInputEnabled(enabled);
        }

        private void ClearSystems() { Storage.Clear(); ToolUpgrades.Clear(); Gathering.Clear(); Orders.Clear(); Tasks.Clear(); Tools.Clear(); Weather.Clear(); Calendar.Clear(); Stamina.Clear(); Time.Clear(); Shop.Clear(); Economy.Clear(); Farm.Clear(); Inventory.Clear(); Players.Clear(); Cameras.Clear(); Interactions.Clear(); }
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
