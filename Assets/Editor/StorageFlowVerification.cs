using System;
using System.IO;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.Saving;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class StorageFlowVerification
    {
        const string Key = "MagicIsland.StorageVerification";
        static StorageFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void SaveDirectory(GameManager g, string directory) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, directory,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static StorageView View => UnityEngine.Object.FindObjectOfType<StorageView>();
        static void Place(GameManager g) => g.Players.RestorePosition(g.Storage.Chest.transform.position + new Vector3(0, .1f, -1.4f));
        static void Capture(GameManager g, string name)
        { GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 11 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Storage timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Configuration failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Storage.IsValid && g.Configuration.Storage.Slots == 18, "StorageConfig packed AA");
                    string directory = Path.GetFullPath("Logs/StorageSaves/" + Guid.NewGuid().ToString("N")); SessionState.SetString(Key + ".Directory", directory); SaveDirectory(g, directory); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Storage.Chest != null && UnityEngine.Object.FindObjectsOfType<StorageChest>().Length == 1 && g.Storage.State.Contents.Capacity == 18, "Single AA chest and empty contents");
                    var point = g.Storage.Chest.transform.position; Physics.SyncTransforms(); var ray = new Ray(point + Vector3.up * 5, Vector3.down);
                    g.Players.RestorePosition(new Vector3(0, .1f, 10)); g.OpenStorage(); Require(!g.UI.HasStorage && g.Interactions.Controller.RefreshStorageHover(ray, false)
                        && g.Interactions.Controller.HoverIndex == -1 && g.Interactions.Controller.Hint.Contains("太远"), "Chest reach and farm ray interception");
                    Require(!g.Interactions.Controller.RefreshStorageHover(ray, true), "Chest UI ray interception");
                    var node = g.Gathering.Nodes[0]; g.Players.RestorePosition(node.transform.position + new Vector3(1.5f, .1f, 0)); g.Tools.SelectTool(FarmTool.Axe);
                    Require(g.TryGather(0, out _), "Start gathering swing"); Place(g); g.OpenStorage(); Require(!g.UI.HasStorage, "Busy tools cannot open storage"); Phase(2);
                }
                else if (phase == 2 && !g.Tools.Busy)
                {
                    g.Inventory.State.TryAdd("wood", 105); g.Inventory.State.TryAdd("stone", 8); g.Inventory.State.TryAdd("corn", 3);
                    var point = g.Storage.Chest.transform.position; var camera = g.Players.Player.ViewCamera; camera.transform.position = point + new Vector3(6, 6, -8); camera.transform.LookAt(point + Vector3.up * .6f);
                    Capture(g, "StorageChestPreview"); g.OpenStorage(); Phase(3);
                }
                else if (phase == 3 && g.UI.HasStorage && View != null)
                {
                    Require(View.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp) && View.BagSlots.Count(s => s.gameObject.activeSelf) == 30 && View.BagSlots.Count(s => s.SelectButton.interactable) == g.Inventory.State.Capacity && View.ChestSlots.Count(s => s.gameObject.activeSelf) == 18,
                        "PopUp MVC grid sizes"); View.BagSlots[0].SelectButton.onClick.Invoke(); View.MoveOneButton.onClick.Invoke();
                    Require(g.Inventory.GetCount("turnip_seed") == 49 && g.Storage.State.Contents.GetCount("turnip_seed") == 1 && g.Saves.TryLoad(out var data)
                        && data.Storage[0].Quantity == 1 && data.Slots[0].Quantity == 49, "Single deposit button autosaves both sides");
                    View.MoveStackButton.onClick.Invoke(); Require(g.Inventory.GetCount("turnip_seed") == 0 && g.Storage.State.Contents.GetCount("turnip_seed") == 50, "Whole stack merges");
                    View.BagSlots[1].SelectButton.onClick.Invoke(); View.MoveStackButton.onClick.Invoke(); View.BagSlots[2].SelectButton.onClick.Invoke(); View.MoveStackButton.onClick.Invoke();
                    Require(g.Storage.State.Contents.Slots[1].Quantity == 99 && g.Storage.State.Contents.Slots[2].Quantity == 6 && g.Inventory.GetCount("wood") == 0, "Selected stacks deposit exactly, split at 99");
                    View.ChestSlots[2].SelectButton.onClick.Invoke(); View.MoveOneButton.onClick.Invoke();
                    Require(g.Inventory.GetCount("wood") == 1 && g.Storage.State.Contents.GetCount("wood") == 104 && View.MoveOneButton.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("取出"), "Withdraw button direction");
                    foreach (var text in View.GetComponentsInChildren<TMPro.TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Storage Chinese/layout: " + text.name); }
                    Capture(g, "StoragePanelPreview"); SessionState.SetFloat(Key + ".Minute", (float)g.Time.Clock.Minute); SessionState.SetFloat(Key + ".Pause", (float)EditorApplication.timeSinceStartup); Phase(4);
                }
                else if (phase == 4 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Pause", 0) > .2)
                {
                    int day = g.Farm.State.Day; g.AdvanceDay(); g.StartNewDayFromReport(); g.OpenShop(); g.OpenOrder(); g.OpenSleep(); g.OpenToolUpgrade(); g.ToggleInventory();
                    Require(Math.Abs(g.Time.Clock.Minute - SessionState.GetFloat(Key + ".Minute", 0)) < .001 && g.Farm.State.Day == day && !g.UI.HasShop && !g.UI.HasOrder
                        && !g.UI.HasSleep && !g.UI.HasToolUpgrade && !g.UI.IsInventoryVisible && !g.TryGather(0, out _), "Storage modal pauses time and blocks all other actions");
                    var bag = g.Inventory.State.Slots.ToArray(); var box = g.Storage.State.Capture();
                    g.Storage.State.Restore(Enumerable.Repeat(new InventorySlot("stone", 99), 18).ToArray()); g.TransferStorage(true, 0, 1);
                    Require(g.Inventory.GetCount("wood") == 1 && g.Storage.State.Contents.GetCount("wood") == 0 && g.Storage.Feedback.Contains("空间不足"), "Full chest no partial move"); g.Storage.State.Restore(box);
                    g.Inventory.State.Restore(Enumerable.Repeat(new InventorySlot("stone", 99), g.Inventory.State.Capacity).ToArray()); g.TransferStorage(false, 0, 1);
                    Require(g.Storage.State.Contents.GetCount("turnip_seed") == 50 && g.Inventory.GetCount("turnip_seed") == 0, "Full bag withdrawal free"); g.Inventory.State.Restore(bag);
                    string directory = SessionState.GetString(Key + ".Directory", ""); string blocked = Path.Combine(directory, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    View.ChestSlots[0].SelectButton.onClick.Invoke(); View.MoveOneButton.onClick.Invoke();
                    Require(g.Inventory.GetCount("turnip_seed") == 1 && g.Storage.State.Contents.GetCount("turnip_seed") == 49 && g.Storage.Feedback.Contains("保存失败"), "Autosave failure keeps committed transfer");
                    SaveDirectory(g, directory); View.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.Storage[0].Quantity == 49 && saved.Slots.Sum(s => s.ItemId == "turnip_seed" ? s.Quantity : 0) == 1, "Save retry no extra transfer");
                    Require(g.Economy.Gold == 100 && g.Stamina.State.Current == 96 && g.Tasks.State.Capture().All(t => t.Progress == 0), "Storage does not sell, harvest, spend stamina or gold");
                    g.CloseStorage(); g.TransferStorage(false, 0, 1); Require(g.Storage.State.Contents.GetCount("turnip_seed") == 49, "Closed panel cannot transfer"); g.ReturnToMainMenu(); Phase(5);
                }
                else if (phase == 5 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Storage.State == null && g.Storage.Chest == null && UnityEngine.Object.FindObjectsOfType<StorageChest>().Length == 0, "Storage resources cleared on return"); g.ContinueGame(); Phase(6);
                }
                else if (phase == 6 && g.State == GameFlowState.Playing)
                {
                    Require(g.Storage.State.Contents.GetCount("turnip_seed") == 49 && g.Inventory.GetCount("turnip_seed") == 1 && g.Storage.State.Contents.GetCount("wood") == 104 && g.Inventory.GetCount("wood") == 1, "Reload exact distribution and no duplicates");
                    Require(g.Saves.TryLoad(out var saved), "Read fixture"); string original = JsonUtility.ToJson(saved); saved.Storage[0].ItemId = "unknown";
                    File.WriteAllText(g.Saves.SavePath, JsonUtility.ToJson(saved)); File.WriteAllText(g.Saves.SavePath + ".bak", JsonUtility.ToJson(saved)); Require(!g.Saves.TryLoad(out _), "Invalid storage item rejected before world restore");
                    File.WriteAllText(g.Saves.SavePath, original); File.WriteAllText(g.Saves.SavePath + ".bak", original);
                    Place(g); g.OpenStorage(); Phase(7);
                }
                else if (phase == 7 && g.UI.HasStorage && View != null)
                {
                    View.ChestSlots[0].SelectButton.onClick.Invoke(); View.MoveStackButton.onClick.Invoke();
                    Require(g.Inventory.GetCount("turnip_seed") == 50 && g.Storage.State.Contents.GetCount("turnip_seed") == 0 && !View.MoveOneButton.interactable, "Whole withdrawal merges and empty source disables action");
                    g.CloseStorage(); g.ReturnToMainMenu(); Phase(8);
                }
                else if (phase == 8 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var data), "Read v10 migration fixture"); string upgrades = JsonUtility.ToJson(data.ToolUpgrades[0]); string resource = JsonUtility.ToJson(data.Gathering[0]);
                    data.Version = 10; data.Storage = null; File.WriteAllText(g.Saves.SavePath, JsonUtility.ToJson(data)); File.WriteAllText(g.Saves.SavePath + ".bak", JsonUtility.ToJson(data));
                    Require(g.Saves.TryLoad(out var migrated) && migrated.Version == SaveData.CurrentVersion && migrated.Storage.Length == 18 && migrated.Storage.All(s => s.Quantity == 0)
                        && migrated.Slots.Sum(s => s.ItemId == "turnip_seed" ? s.Quantity : 0) == 50 && JsonUtility.ToJson(migrated.ToolUpgrades[0]) == upgrades && JsonUtility.ToJson(migrated.Gathering[0]) == resource,
                        "v10 migration empty chest preserves bag/upgrades/resources"); g.ContinueGame(); Phase(9);
                }
                else if (phase == 9 && g.State == GameFlowState.Playing)
                {
                    Require(g.Storage.State.Contents.Slots.All(s => s.Empty) && g.Inventory.GetCount("turnip_seed") == 50, "Migrated save playable"); g.ReturnToMainMenu(); Phase(10);
                }
                else if (phase == 10 && g.State == GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/StorageFlowVerification.txt", "PASS: packed AA config/chest/PopUp MVC, reach/UI/busy/modal guards, single/whole transfers and stacking, full destination no changes, autosave failure/retry, exact reload, v10 migration, invalid item rejection, lifecycle cleanup and Chinese layout.\n");
                    Debug.Log("Storage flow verification PASSED."); Phase(11); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
