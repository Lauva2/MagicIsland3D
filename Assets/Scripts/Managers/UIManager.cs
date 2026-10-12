using System;
using System.Collections;
using System.Collections.Generic;
using MagicIsland.Interaction;
using MagicIsland.Loading;
using MagicIsland.UI.Controllers;
using MagicIsland.UI.Views;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagicIsland.Managers
{
    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        public string LastError { get; private set; }
        public bool HasFarmHud => instances.ContainsKey(RuntimeUi.HudAddress);
        public bool HasInventory => instances.ContainsKey(RuntimeUi.InventoryAddress);
        public bool HasSleep => instances.ContainsKey("SleepPanel");
        public bool HasShop => instances.ContainsKey("ShopPanel");
        public bool HasToolUpgrade => instances.ContainsKey("ToolUpgradePanel");
        public bool HasGift => instances.ContainsKey("GiftPanel");
        public bool HasFishing => instances.ContainsKey("FishingPanel");
        public bool HasDialogue => instances.ContainsKey("DialoguePanel");
        public bool HasCrafting => instances.ContainsKey("CraftingPanel");
        public bool HasPlacement => instances.ContainsKey("PlacementPanel");
        public bool HasCooking => instances.ContainsKey("CookingPanel");
        public bool HasStorage => instances.ContainsKey("StoragePanel");
        public bool HasHouseUpgrade => instances.ContainsKey("HouseUpgradePanel");
        public bool HasOrder => instances.ContainsKey("OrderPanel");
        public bool IsInventoryVisible => instances.TryGetValue(RuntimeUi.InventoryAddress, out Entry entry)
            && entry.Handle.IsValid() && entry.Handle.IsDone && entry.Handle.Status == AsyncOperationStatus.Succeeded
            && entry.Handle.Result.activeSelf;
        private sealed class Entry
        {
            public AsyncOperationHandle<GameObject> Handle;
            public IUIController Controller;
            public bool Released;
        }
        private readonly Dictionary<string, Entry> instances = new Dictionary<string, Entry>();
        private MainMenuController menuController;
        private readonly Dictionary<UILayer, RectTransform> layers = new Dictionary<UILayer, RectTransform>();

        public RectTransform GetLayerRoot(UILayer layer) => layers[layer];

        private void CreateLayers()
        {
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                RectTransform root = new GameObject(layer.ToString(), typeof(RectTransform)).GetComponent<RectTransform>();
                root.SetParent(transform, false);
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.sizeDelta = Vector2.zero;
                layers.Add(layer, root);
            }
        }

        private void PlacePanel(GameObject panel)
        {
            foreach (var child in panel.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            UIView view = panel.GetComponent<UIView>();
            Canvas canvas = panel.GetComponent<Canvas>();
            if (view == null || canvas == null) throw new InvalidOperationException("UI panel requires UIView and Canvas: " + panel.name);
            if (!layers.TryGetValue(view.Layer, out RectTransform parent))
                throw new InvalidOperationException("Unknown UI layer: " + view.Layer);
            if (parent.childCount >= 1000) throw new InvalidOperationException("UI layer panel limit reached");
            panel.transform.SetParent(parent, false);
            panel.transform.SetAsLastSibling();
            // Prefab canvases stay independent, retaining their own scaler and reference resolution.
            for (int i = 0; i < parent.childCount; i++)
            {
                Canvas child = parent.GetChild(i).GetComponent<Canvas>();
                child.overrideSorting = true;
                child.sortingOrder = (int)view.Layer * 1000 + i;
            }
        }

        private void Awake()
        {
            Instance = this;
            CreateLayers();
            RuntimeUi.EnsureEventSystem(transform);
        }

        public IEnumerator ShowMainMenu(Action enterGame, Action continueGame, Action openSettings, Action quitGame, bool hasSave)
        {
            yield return Load(RuntimeUi.MenuAddress, root =>
            {
                MainMenuView view = root.GetComponent<MainMenuView>();
                if (view == null) throw new InvalidOperationException("MainMenuPanel has no MainMenuView");
                menuController = new MainMenuController(view, enterGame, continueGame, openSettings, quitGame, hasSave);
                return menuController;
            });
            SetMainMenuVisible(true);
        }

        public IEnumerator ShowFarmHud(FarmManager farm, InteractionManager interaction, EconomyManager economy, TimeManager time, StaminaManager stamina, WeatherManager weather, Action nextDay, Action returnMenu, Action toggleInventory)
        {
            yield return Load(RuntimeUi.HudAddress, root =>
            {
                PrototypeHud view = root.GetComponent<PrototypeHud>();
                if (view == null) throw new InvalidOperationException("FarmHudPanel has no PrototypeHud view");
                return new FarmHudController(view, farm, interaction, economy, time, stamina, weather, nextDay, returnMenu, toggleInventory);
            });
        }

        public IEnumerator PrepareInventory(InventoryManager inventory, FarmManager farm, Func<string, bool> chooseSeed, Action closeInventory, FoodManager food, Action<int> eatFood, Action saveFood, PlacementManager placement = null, Action<int> startPlacement = null)
        {
            yield return Load(RuntimeUi.InventoryAddress, root =>
            {
                InventoryView view = root.GetComponent<InventoryView>();
                if (view == null) throw new InvalidOperationException("InventoryPanel has no InventoryView");
                return new InventoryController(view, inventory, farm, chooseSeed, closeInventory, food, eatFood, saveFood, placement, startPlacement);
            });
            SetInventoryVisible(false);
        }

        public IEnumerator ShowFeedback(ToolManager tools)
        {
            yield return Load("FeedbackPanel", root => new FeedbackController(root.GetComponent<FeedbackView>(), tools));
        }
        public IEnumerator ShowCropInfo(FarmManager farm, InteractionManager interaction)
        {
            yield return Load("CropInfoPanel", root => new CropInfoController(root.GetComponent<CropInfoView>(), farm, interaction));
        }
        public IEnumerator ShowTasks(TaskManager tasks, Action claim)
        {
            yield return Load("TaskPanel", root => new TaskController(root.GetComponent<TaskView>(), tasks, claim));
        }
        public IEnumerator ShowSettings(SettingsManager settings, Action close)
        {
            yield return Load("SettingsPanel", root =>
            {
                SettingsView view = root.GetComponent<SettingsView>();
                if (view == null) throw new InvalidOperationException("SettingsPanel has no SettingsView");
                return new SettingsController(view, settings, close);
            });
        }

        public void CloseSettings() => Close("SettingsPanel");
        public IEnumerator ShowShop(ShopManager shop, Action close, Func<bool, MagicIsland.Economy.TradeResult> trade, Action save)
        {
            yield return Load("ShopPanel", root =>
            {
                ShopView view = root.GetComponent<ShopView>();
                if (view == null) throw new InvalidOperationException("ShopPanel has no ShopView");
                return new ShopController(view, shop, close, trade, save);
            });
        }
        public void CloseShop() => Close("ShopPanel");
        public IEnumerator ShowOrder(OrderManager orders, Action deliver, Action close, Action houseUpgrade = null)
        {
            yield return Load("OrderPanel", root => new OrderController(root.GetComponent<OrderView>(), orders, deliver, close, houseUpgrade));
        }
        public IEnumerator ShowHouseUpgrade(HousingManager housing, FarmManager farm, InventoryManager inventory, EconomyManager economy, Action apply, Action close, Action save)
        { yield return Load("HouseUpgradePanel", root => new HouseUpgradeController(root.GetComponent<HouseUpgradeView>(), housing, farm, inventory, economy, apply, close, save)); }
        public void CloseHouseUpgrade() => Close("HouseUpgradePanel");
        public void CloseOrder() => Close("OrderPanel");
        public IEnumerator ShowToolUpgrade(ToolUpgradeManager upgrades, Action<MagicIsland.Farming.FarmTool> upgrade, Action close, Action save, Action crafting = null)
        {
            yield return Load("ToolUpgradePanel", root => new ToolUpgradeController(root.GetComponent<ToolUpgradeView>(), upgrades, upgrade, close, save, crafting));
        }
        public void CloseToolUpgrade() => Close("ToolUpgradePanel");
        public IEnumerator ShowStorage(StorageManager storage, Action<bool, int, int> transfer, Action close, Action save)
        {
            yield return Load("StoragePanel", root => new StorageController(root.GetComponent<StorageView>(), storage, transfer, close, save));
        }
        public void CloseStorage() => Close("StoragePanel");
        public IEnumerator ShowCooking(CookingManager cooking, Action<int> cook, Action close, Action save)
        { yield return Load("CookingPanel", root => new CookingController(root.GetComponent<CookingView>(), cooking, cook, close, save)); }
        public void CloseCooking() => Close("CookingPanel");
        public IEnumerator ShowDialogue(DialogueManager dialogue, Action next, Action close, Action gift, Action story)
        { yield return Load("DialoguePanel", root => new DialogueController(root.GetComponent<DialogueView>(), dialogue, next, close, gift, story)); }
        public void CloseDialogue() => Close("DialoguePanel");
        public IEnumerator ShowGift(RelationshipManager relationships, Action<int> give, Action close, Action save)
        { yield return Load("GiftPanel", root => new GiftController(root.GetComponent<GiftView>(), relationships, give, close, save)); }
        public void CloseGift() => Close("GiftPanel");
        public IEnumerator ShowFishing(FishingManager fishing, Action cast, Action reel, Action save, Action close)
        { yield return Load("FishingPanel", root => new FishingController(root.GetComponent<FishingView>(), fishing, cast, reel, save, close)); }
        public void CloseFishing() => Close("FishingPanel");
        public IEnumerator ShowCrafting(CraftingManager crafting, Action<int> craft, Action close, Action save, Action upgrade)
        { yield return Load("CraftingPanel", root => new CraftingController(root.GetComponent<CraftingView>(), crafting, craft, close, save, upgrade)); }
        public void CloseCrafting() => Close("CraftingPanel");
        public IEnumerator ShowPlacement(PlacementManager placement, Action confirm, Action rotate, Action cancel, Action save)
        { yield return Load("PlacementPanel", root => new PlacementController(root.GetComponent<PlacementView>(), placement, confirm, rotate, cancel, save)); }
        public void ClosePlacement() => Close("PlacementPanel");
        public IEnumerator ShowSceneTransition(string text)
        { yield return Load("SceneTransitionPanel", root => new SceneTransitionController(root.GetComponent<SceneTransitionView>(), text)); }
        public void CloseSceneTransition() => Close("SceneTransitionPanel");
        public IEnumerator ShowSleep(Action confirm, Action cancel)
        {
            yield return Load("SleepPanel", root => new SleepController(root.GetComponent<SleepView>(), confirm, cancel));
        }
        public void CloseSleep() => Close("SleepPanel");

        public void SetInventoryVisible(bool visible)
        {
            if (visible) DismissFeedback();
            if (instances.TryGetValue(RuntimeUi.InventoryAddress, out Entry entry) && entry.Handle.IsValid()
                && entry.Handle.IsDone && entry.Handle.Status == AsyncOperationStatus.Succeeded)
                entry.Handle.Result.SetActive(visible);
        }

        private void DismissFeedback()
        {
            if (instances.TryGetValue("FeedbackPanel", out Entry entry))
                (entry.Controller as FeedbackController)?.Dismiss();
        }

        private void PreparePanel(GameObject panel)
        {
            if (panel.GetComponent<UIView>().Layer == UILayer.PopUp) DismissFeedback();
        }

        private IEnumerator Load(string address, Func<GameObject, IUIController> createController)
        {
            LastError = null;
            if (instances.TryGetValue(address, out Entry loaded))
            {
                if (loaded.Handle.IsDone && loaded.Handle.Status == AsyncOperationStatus.Succeeded)
                {
                    PreparePanel(loaded.Handle.Result);
                    loaded.Handle.Result.SetActive(true);
                }
                yield break;
            }
            Entry entry = new Entry { Handle = Addressables.InstantiateAsync(address, transform) };
            instances.Add(address, entry);
            yield return entry.Handle;
            if (entry.Released) yield break;
            if (entry.Handle.Status != AsyncOperationStatus.Succeeded)
            {
                LastError = entry.Handle.OperationException?.Message ?? "UI loading failed: " + address;
                Close(address);
                yield break;
            }
            try
            {
                PlacePanel(entry.Handle.Result);
                PreparePanel(entry.Handle.Result);
                entry.Controller = createController(entry.Handle.Result);
            }
            catch (Exception error)
            {
                LastError = error.Message;
                Debug.LogException(error);
                Close(address);
            }
        }

        public void SetMainMenuVisible(bool visible)
        {
            if (instances.TryGetValue(RuntimeUi.MenuAddress, out Entry entry) && entry.Handle.IsValid()
                && entry.Handle.IsDone && entry.Handle.Status == AsyncOperationStatus.Succeeded)
                entry.Handle.Result.SetActive(visible);
        }

        public void SetMenuStatus(string message, bool busy) => menuController?.SetStatus(message, busy);
        public void SetSaveAvailable(bool available) => menuController?.SetSaveAvailable(available);

        public void Close(string address)
        {
            if (!instances.TryGetValue(address, out Entry entry)) return;
            entry.Controller?.Dispose();
            if (address == RuntimeUi.MenuAddress) menuController = null;
            Release(entry);
            instances.Remove(address);
        }

        private static void Release(Entry entry)
        {
            if (entry.Released || !entry.Handle.IsValid()) return;
            entry.Released = true;
            if (entry.Handle.IsDone)
            {
                if (entry.Handle.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(entry.Handle);
                else Addressables.Release(entry.Handle);
            }
            else entry.Handle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(handle);
                else if (handle.IsValid()) Addressables.Release(handle);
            };
        }

        public void RefreshViews() { foreach (Entry entry in instances.Values) entry.Controller?.Tick(); }
        private void Update() => RefreshViews();

        private void OnDestroy()
        {
            foreach (Entry entry in instances.Values) { entry.Controller?.Dispose(); Release(entry); }
            instances.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
