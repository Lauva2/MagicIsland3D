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
                if (view == null) throw new InvalidOperationException("MainMenuUI has no MainMenuView");
                menuController = new MainMenuController(view, enterGame, continueGame, openSettings, quitGame, hasSave);
                return menuController;
            });
            SetMainMenuVisible(true);
        }

        public IEnumerator ShowFarmHud(FarmManager farm, InteractionManager interaction, Action nextDay, Action returnMenu, Action toggleInventory)
        {
            yield return Load(RuntimeUi.HudAddress, root =>
            {
                PrototypeHud view = root.GetComponent<PrototypeHud>();
                if (view == null) throw new InvalidOperationException("FarmHUD has no PrototypeHud view");
                return new FarmHudController(view, farm, interaction, nextDay, returnMenu, toggleInventory);
            });
        }

        public IEnumerator PrepareInventory(InventoryManager inventory, Action closeInventory)
        {
            yield return Load(RuntimeUi.InventoryAddress, root =>
            {
                InventoryView view = root.GetComponent<InventoryView>();
                if (view == null) throw new InvalidOperationException("InventoryUI has no InventoryView");
                return new InventoryController(view, inventory, closeInventory);
            });
            SetInventoryVisible(false);
        }

        public IEnumerator ShowSettings(SettingsManager settings, Action close)
        {
            yield return Load("SettingsUI", root =>
            {
                SettingsView view = root.GetComponent<SettingsView>();
                if (view == null) throw new InvalidOperationException("SettingsUI has no SettingsView");
                return new SettingsController(view, settings, close);
            });
        }

        public void CloseSettings() => Close("SettingsUI");

        public void SetInventoryVisible(bool visible)
        {
            if (instances.TryGetValue(RuntimeUi.InventoryAddress, out Entry entry) && entry.Handle.IsValid()
                && entry.Handle.IsDone && entry.Handle.Status == AsyncOperationStatus.Succeeded)
                entry.Handle.Result.SetActive(visible);
        }

        private IEnumerator Load(string address, Func<GameObject, IUIController> createController)
        {
            LastError = null;
            if (instances.TryGetValue(address, out Entry loaded))
            {
                if (loaded.Handle.IsDone && loaded.Handle.Status == AsyncOperationStatus.Succeeded)
                    loaded.Handle.Result.SetActive(true);
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

        private void Update() { foreach (Entry entry in instances.Values) entry.Controller?.Tick(); }

        private void OnDestroy()
        {
            foreach (Entry entry in instances.Values) { entry.Controller?.Dispose(); Release(entry); }
            instances.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
