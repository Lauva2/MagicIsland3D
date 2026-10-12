using System;
using System.IO;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Interaction;
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
    public static class CookingFlowVerification
    {
        const string Key = "MagicIsland.CookingVerification";
        static CookingFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static CookingView View => UnityEngine.Object.FindObjectOfType<CookingView>();
        static InventoryView Bag => UnityEngine.Object.FindObjectOfType<InventoryView>();
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir, g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void Place(GameManager g) => g.Players.RestorePosition(g.Cooking.Station.transform.position + new Vector3(0, .1f, -1.5f));
        static int FoodSlot(GameManager g, string id) => Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == id);
        static void Capture(GameManager g, string name) { GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void TextCheck(Component view)
        { foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Food Chinese/layout: " + text.name); } }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 10 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Cooking timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Configuration failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Cooking.Validate(g.Configuration.Items) && g.Configuration.Items.Find("turnip_soup").Type == ItemType.Food, "Packed cooking config and food definitions");
                    string dir = Path.GetFullPath("Logs/CookingSaves/" + Guid.NewGuid().ToString("N")); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Cooking.Station != null && UnityEngine.Object.FindObjectsOfType<CookingStation>().Length == 1, "Single AA station");
                    var point = g.Cooking.Station.transform.position; Physics.SyncTransforms(); var ray = new Ray(point + Vector3.up * 5, Vector3.down);
                    g.Players.RestorePosition(new Vector3(0, .1f, 10)); g.OpenCooking(); Require(!g.UI.HasCooking && g.Interactions.Controller.RefreshCookingHover(ray, false)
                        && g.Interactions.Controller.HoverIndex == -1 && !g.Interactions.Controller.RefreshCookingHover(ray, true), "Reach and UI/farm ray protection");
                    var node = g.Gathering.Nodes[0]; g.Players.RestorePosition(node.transform.position + new Vector3(1.5f, .1f, 0)); g.Tools.SelectTool(FarmTool.Axe); Require(g.TryGather(0, out _), "Start swing");
                    Place(g); g.OpenCooking(); Require(!g.UI.HasCooking, "Busy swing blocks cooking"); Phase(2);
                }
                else if (phase == 2 && !g.Tools.Busy)
                {
                    var point = g.Cooking.Station.transform.position; var camera = g.Players.Player.ViewCamera; camera.transform.position = point + new Vector3(6, 6, -8); camera.transform.LookAt(point + Vector3.up * .7f);
                    Capture(g, "CookingStationPreview"); g.OpenCooking(); Phase(3);
                }
                else if (phase == 3 && g.UI.HasCooking && View != null)
                {
                    Require(View.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp) && View.CookButtons.All(b => !b.interactable), "Cooking MVC, layer and ingredient checks");
                    g.CookRecipe(0); Require(g.Inventory.GetCount("turnip_soup") == 0, "No free food");
                    g.Inventory.TryAdd("turnip", 6); g.Inventory.TryAdd("corn", 2); g.UI.RefreshViews(); TextCheck(View);
                    View.CookButtons[0].onClick.Invoke(); View.CookButtons[1].onClick.Invoke();
                    Require(g.Inventory.GetCount("turnip") == 4 && g.Inventory.GetCount("corn") == 1 && g.Inventory.GetCount("turnip_soup") == 1 && g.Inventory.GetCount("roasted_corn") == 1
                        && g.Saves.TryLoad(out var saved) && saved.Slots.Sum(s => s.ItemId == "turnip_soup" ? s.Quantity : 0) == 1, "Recipe buttons and atomic autosave");
                    Capture(g, "CookingPanelPreview");
                    var bag = g.Inventory.State.Slots.ToArray(); g.Inventory.State.Restore(Enumerable.Repeat(new InventorySlot("turnip", 99), 12).ToArray()); g.CookRecipe(0);
                    Require(g.Inventory.GetCount("turnip") == 1188 && g.Inventory.GetCount("turnip_soup") == 0 && g.Cooking.Feedback.Contains("放不下"), "Full bag does not consume ingredients"); g.Inventory.State.Restore(bag);
                    SessionState.SetFloat(Key + ".Minute", (float)g.Time.Clock.Minute); SessionState.SetFloat(Key + ".Pause", (float)EditorApplication.timeSinceStartup); Phase(4);
                }
                else if (phase == 4 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Pause", 0) > .2)
                {
                    int day = g.Farm.State.Day; g.AdvanceDay(); g.OpenStorage(); g.OpenOrder(); g.OpenShop(); g.OpenSleep(); g.OpenToolUpgrade(); g.ToggleInventory();
                    Require(Math.Abs(g.Time.Clock.Minute - SessionState.GetFloat(Key + ".Minute", 0)) < .001 && g.Farm.State.Day == day && !g.UI.HasStorage && !g.UI.HasOrder && !g.UI.HasShop
                        && !g.UI.HasSleep && !g.UI.HasToolUpgrade && !g.UI.IsInventoryVisible && !g.TryGather(0, out _), "Cooking modal pauses time and excludes all other operations");
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    g.CookRecipe(0); Require(g.Inventory.GetCount("turnip") == 2 && g.Inventory.GetCount("turnip_soup") == 2 && g.Cooking.Feedback.Contains("保存失败"), "Cooking save failure retains one complete recipe");
                    SaveDirectory(g, dir); View.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.Slots.Sum(s => s.ItemId == "turnip_soup" ? s.Quantity : 0) == 2, "Cooking save retry");
                    g.CloseCooking(); g.CookRecipe(0); Require(g.Inventory.GetCount("turnip") == 2, "Closed cooking cannot craft"); g.ToggleInventory(); Phase(5);
                }
                else if (phase == 5 && g.UI.IsInventoryVisible && Bag != null)
                {
                    int slot = FoodSlot(g, "turnip_soup"); g.Stamina.Restore(100); Bag.Slots[slot].SelectButton.onClick.Invoke(); g.UI.RefreshViews();
                    Require(Bag.UseFoodButton.gameObject.activeSelf && !Bag.UseFoodButton.interactable && !Bag.UseSeedButton.gameObject.activeSelf, "Full stamina disables food, food cannot plant");
                    g.UseFood(slot); Require(g.Inventory.GetCount("turnip_soup") == 2 && g.Stamina.State.Current == 100, "Full stamina no consumption");
                    g.Stamina.Restore(45); g.UI.RefreshViews(); TextCheck(Bag); Bag.UseFoodButton.onClick.Invoke();
                    Require(g.Inventory.GetCount("turnip_soup") == 1 && g.Stamina.State.Current == 75 && g.Saves.TryLoad(out var saved) && saved.Stamina == 75, "Food use autosaves quantity/stamina");
                    Capture(g, "FoodInventoryPreview");
                    string dir = SessionState.GetString(Key + ".Dir", ""); SaveDirectory(g, Path.Combine(dir, "blocked")); Bag.UseFoodButton.onClick.Invoke();
                    Require(g.Inventory.GetCount("turnip_soup") == 0 && g.Stamina.State.Current == 100 && g.Food.Feedback.Contains("保存失败"), "Food use clamps and retains commit on save failure");
                    SaveDirectory(g, dir); Bag.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out saved) && saved.Stamina == 100 && saved.Slots.All(s => s.ItemId != "turnip_soup"), "Food save retry without consuming again");
                    int corn = FoodSlot(g, "roasted_corn"); g.Stamina.Restore(70); Bag.Slots[corn].SelectButton.onClick.Invoke(); Bag.UseFoodButton.onClick.Invoke();
                    Require(g.Stamina.State.Current == 90 && g.Inventory.GetCount("roasted_corn") == 0 && g.Tasks.State.Capture().All(t => t.Progress == 0) && g.Economy.Gold == 100, "Corn recovery no task/economy side effects");
                    g.CloseInventory(); g.ReturnToMainMenu(); Phase(6);
                }
                else if (phase == 6 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Cooking.State == null && g.Food.State == null && g.Cooking.Station == null && UnityEngine.Object.FindObjectsOfType<CookingStation>().Length == 0, "Cooking/food lifecycle cleanup"); g.ContinueGame(); Phase(7);
                }
                else if (phase == 7 && g.State == GameFlowState.Playing)
                {
                    Require(g.Stamina.State.Current == 90 && g.Inventory.GetCount("turnip") == 2 && g.Inventory.GetCount("corn") == 1 && g.Inventory.GetCount("turnip_soup") == 0, "Reload after cooking/eating retains exact state");
                    Place(g); g.OpenCooking(); Phase(8);
                }
                else if (phase == 8 && g.UI.HasCooking && View != null)
                {
                    View.CookButtons[0].onClick.Invoke(); Require(g.Inventory.GetCount("turnip") == 0 && g.Inventory.GetCount("turnip_soup") == 1, "Last ingredients cook after reload");
                    g.CloseCooking(); g.Players.RestorePosition(g.Storage.Chest.transform.position + new Vector3(0, .1f, -1.4f)); g.OpenStorage(); Phase(9);
                }
                else if (phase == 9 && g.UI.HasStorage && UnityEngine.Object.FindObjectOfType<StorageView>() != null)
                {
                    int slot = FoodSlot(g, "turnip_soup"); g.TransferStorage(true, slot, 1); Require(g.Inventory.GetCount("turnip_soup") == 0 && g.Storage.State.Contents.GetCount("turnip_soup") == 1
                        && g.Saves.TryLoad(out var saved) && saved.Version == SaveData.CurrentVersion && saved.Storage.Any(s => s.ItemId == "turnip_soup"), "Food stores and persists under schema11");
                    g.CloseStorage(); File.WriteAllText("Logs/CookingFlowVerification.txt", "PASS: packed AA recipes/station/MVC, ingredient and post-consumption capacity checks, modal/busy/reach/UI guards, cooking/eating autosave failure/retry, clamped stamina/full-stamina protection, reload, food storage, lifecycle and Chinese layout.\n");
                    Debug.Log("Cooking/food flow verification PASSED."); Phase(10); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
