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
            Saves.Initialize(Configuration.Config, Configuration.Items);
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
            if (!TryInitializeWorld(out string error))
            {
                yield return Scenes.UnloadGameScene();
                ClearSystems();
                ReturnAfterFailure(error);
                yield break;
            }
            yield return UI.ShowFarmHud(Farm, Interactions, AdvanceDay, ReturnToMainMenu, ToggleInventory);
            if (UI.LastError == null) yield return UI.PrepareInventory(Inventory, CloseInventory);
            if (UI.LastError != null)
            {
                error = UI.LastError;
                UI.Close(RuntimeUi.InventoryAddress);
                UI.Close(RuntimeUi.HudAddress);
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
                Inventory.Initialize(config, Configuration.Items);
                world.CreateWorld();
                Farm.Initialize(world.Grid, config, Inventory);
                Players.Initialize(world.Player, config);
                if (pendingSave != null)
                {
                    var slots = new InventorySlot[pendingSave.Slots.Length];
                    for (int i = 0; i < slots.Length; i++)
                        slots[i] = new InventorySlot(pendingSave.Slots[i].ItemId, pendingSave.Slots[i].Quantity);
                    Inventory.State.Restore(slots);
                    Farm.State.Restore(pendingSave.Day, pendingSave.Cells);
                    Farm.Grid.Refresh();
                    Players.RestorePosition(new Vector3(pendingSave.PlayerX, pendingSave.PlayerY, pendingSave.PlayerZ));
                    pendingSave = null;
                }
                Cameras.Initialize(world.CameraController, config);
                Interactions.Initialize(world.Interaction, config, Farm);
                error = null;
                return true;
            }
            catch (Exception exception) { error = exception.ToString(); return false; }
        }

        public void AdvanceDay()
        {
            if (State != GameFlowState.Playing) return;
            Farm.AdvanceDay();
            SaveProgress();
        }

        public bool SaveProgress()
        {
            if (State != GameFlowState.Playing) return false;
            bool saved = Saves.TrySave(Farm, Inventory, Players);
            Interactions.SetFeedback(saved ? "进度已保存" : "保存失败，请重试返回主菜单");
            if (!saved) Debug.LogWarning("Save failed: " + Saves.LastError);
            return saved;
        }

        public void ToggleInventory()
        {
            if (State != GameFlowState.Playing || !UI.HasInventory) return;
            bool visible = !UI.IsInventoryVisible;
            UI.SetInventoryVisible(visible);
            SetInputEnabled(!visible);
        }
        public void CloseInventory()
        {
            if (State != GameFlowState.Playing) return;
            UI.SetInventoryVisible(false);
            SetInputEnabled(true);
        }
        private void Update()
        {
            if (State == GameFlowState.MainMenu && !settingsOpening && Input.GetKeyDown(KeyCode.Escape)) UI.CloseSettings();
            if (State != GameFlowState.Playing) return;
            if (Input.GetKeyDown(KeyCode.B)) ToggleInventory();
            else if (Input.GetKeyDown(KeyCode.Escape) && UI.IsInventoryVisible) CloseInventory();
        }

        public void ReturnToMainMenu()
        {
            if (State != GameFlowState.Playing) return;
            if (!SaveProgress()) return;
            SetState(GameFlowState.ReturningToMenu);
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
            Players.SetInputEnabled(enabled);
            Cameras.SetInputEnabled(enabled);
            Interactions.SetInputEnabled(enabled);
        }

        private void ClearSystems() { Farm.Clear(); Inventory.Clear(); Players.Clear(); Cameras.Clear(); Interactions.Clear(); }
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
