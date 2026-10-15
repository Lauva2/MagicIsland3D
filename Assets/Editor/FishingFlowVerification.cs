using System;
using System.IO;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.FishingSystem;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.Saving;
using MagicIsland.UI.Views;
using MagicIsland.Tools;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class FishingFlowVerification
    {
        const string Key = "MagicIsland.FishingVerification";
        static FishingFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static FishingView View => UnityEngine.Object.FindObjectOfType<FishingView>();
        static ShopView Shop => UnityEngine.Object.FindObjectOfType<ShopView>();
        static void Place(GameManager g) => g.Players.RestorePosition(g.Fishing.Spot.transform.position + new Vector3(-1.2f, .1f, 0));
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void TextCheck(Component view) { foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Fishing text/glyphs: " + text.name + "=" + text.text); } }
        static void Capture(GameManager g, string name) { GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static long FishCount(GameManager g) => g.Inventory.GetCount("sardine") + g.Inventory.GetCount("sea_bass");
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 16 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Fishing timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Fishing configuration failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Fishing.Validate(g.Configuration.Items, g.Configuration.Config), "FishingCatalog packed AA before menu");
                    string dir = Path.GetFullPath("Logs/FishingSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Fishing.Spot != null && g.Fishing.Rod != null && UnityEngine.Object.FindObjectsOfType<FishingRodVisual>(true).Length == 1 && g.Fishing.Rod.transform.parent == g.Tools.Presentation.FishingRodRoot && !g.Fishing.Rod.gameObject.activeInHierarchy, "AA fishing models and hidden unequipped rod");
                    Physics.SyncTransforms(); var point = g.Fishing.Spot.transform.position + Vector3.right * .8f; var ray = new Ray(point + Vector3.up * 5, Vector3.down);
                    Require(g.Interactions.Controller.RefreshFishingHover(ray, false) && g.Interactions.Controller.HoverIndex == -1 && !g.Interactions.Controller.RefreshFishingHover(ray, true), "Water ray owns click and UI blocks hover");
                    g.OpenFishing(); Require(!g.UI.HasFishing, "Far cast blocked"); Place(g); g.OpenFishing(); Require(!g.UI.HasFishing, "Wrong tool blocked");
                    g.Tools.SelectTool(FarmTool.FishingRod); g.Stamina.Restore(4); g.OpenFishing(); Require(!g.UI.HasFishing, "Low stamina cast blocked"); g.Stamina.Refill();
                    var bag = g.Inventory.State.Slots.ToArray(); g.Inventory.State.Restore(Enumerable.Repeat(new InventorySlot("turnip_seed", 99), g.Inventory.State.Capacity).ToArray()); g.OpenFishing(); Require(!g.UI.HasFishing && g.Stamina.State.Current == 100, "Full bag cast blocked free"); g.Inventory.State.Restore(bag);
                    g.Tools.SelectTool(FarmTool.Axe); var node = g.Gathering.Nodes[0]; g.Players.RestorePosition(node.transform.position + new Vector3(1.5f, .1f, 0)); Require(g.TryGather(0, out _), "Start tool swing"); Place(g); g.OpenFishing(); Require(!g.UI.HasFishing && !g.Tools.SelectTool(FarmTool.FishingRod), "Busy swing prevents rod and casting"); Phase(2);
                }
                else if (phase == 2 && !g.Tools.Busy)
                {
                    g.Stamina.Refill(); g.Interactions.SelectTool(FarmTool.FishingRod); Require(g.Tools.SelectedTool == FarmTool.FishingRod && g.Fishing.Rod.gameObject.activeInHierarchy, "Sixth tool equips AA rod without harvest basket");
                    Require(!g.Tools.TryAct(0, out _) && !g.Farm.State.Cells[0].Tilled, "Rod cannot perform farm operation");
                    var camera = g.Players.Player.ViewCamera; camera.transform.position = g.Fishing.Spot.transform.position + new Vector3(6, 6, -8); camera.transform.LookAt(g.Fishing.Spot.transform.position + Vector3.up * .6f);
                    Capture(g, "CoastalFishingSpotPreview"); g.OpenFishing(); Phase(3);
                }
                else if (phase == 3 && View != null && g.Fishing.State.Phase == FishingPhase.Waiting)
                {
                    Require(View.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp) && !g.Tools.InputEnabled && !g.Players.Player.enabled && !g.Interactions.Controller.enabled && g.Fishing.Spot.Bobber.gameObject.activeSelf, "Fishing MVC popup pauses input and displays bobber");
                    TextCheck(View); Capture(g, "FishingWaitingPreview"); SessionState.SetFloat(Key + ".Minute", (float)g.Time.Clock.Minute); SessionState.SetFloat(Key + ".Pause", (float)EditorApplication.timeSinceStartup); Phase(4);
                }
                else if (phase == 4 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Pause", 0) > .2)
                {
                    int day = g.Farm.State.Day; g.AdvanceDay(); g.StartNewDayFromReport(); g.ToggleInventory(); g.OpenShop(); g.OpenStorage(); g.OpenCooking(); g.OpenSleep(); g.OpenDialogue("island_steward");
                    Require(day == g.Farm.State.Day && Math.Abs(g.Time.Clock.Minute - SessionState.GetFloat(Key + ".Minute", 0)) < .001 && !g.UI.IsInventoryVisible && !g.UI.HasShop && !g.UI.HasStorage && !g.UI.HasCooking && !g.UI.HasSleep && !g.UI.HasDialogue, "Fishing modal excludes systems and pauses world time");
                    View.ReelButton.onClick.Invoke(); Require(g.Fishing.State.Phase == FishingPhase.Missed && g.Stamina.State.Current == 100 && FishCount(g) == 0, "Early reel free");
                    View.CastButton.onClick.Invoke(); g.Fishing.Tick(8); g.UI.RefreshViews(); Require(g.Fishing.State.Phase == FishingPhase.Missed && g.Stamina.State.Current == 100 && FishCount(g) == 0, "Missed bite free");
                    View.CastButton.onClick.Invoke(); g.CloseFishing(); Require(g.Fishing.State.Phase == FishingPhase.Cancelled && !g.Fishing.Spot.Bobber.gameObject.activeSelf && g.Tools.InputEnabled, "Cancel frees session and input"); g.OpenFishing(); Phase(5);
                }
                else if (phase == 5 && View != null)
                {
                    g.Players.RestorePosition(new Vector3(0, .1f, -10)); g.ReelFishing(); Require(!g.UI.HasFishing && FishCount(g) == 0 && g.Stamina.State.Current == 100, "Reel rechecks range"); Place(g); g.OpenFishing(); Phase(6);
                }
                else if (phase == 6 && View != null)
                {
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked); Phase(7);
                }
                else if (phase == 7 && View != null && g.Fishing.State.Phase == FishingPhase.Bite)
                {
                    Require(View.StatusText.text.Contains("咬钩") && View.ReelButton.interactable, "Real time bite cue"); TextCheck(View); Capture(g, "FishingBitePreview");
                    int notices = 0; Action observer = () => { notices++; Require(g.Fishing.State.Phase == FishingPhase.Caught && g.Stamina.State.Current == 95 && FishCount(g) == 1, "Catch observer sees complete transaction"); g.ReelFishing(); g.CastFishing(); g.ReturnToMainMenu(); };
                    g.Inventory.State.Changed += observer; View.ReelButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(notices == 1 && g.State == GameFlowState.Playing && g.Fishing.Feedback.Contains("保存失败") && FishCount(g) == 1 && g.Stamina.State.Current == 95, "Save failure retains fish/stamina and blocks reentry"); TextCheck(View); Capture(g, "FishingCaughtPreview");
                    string first = g.Fishing.State.CaughtItemId; SessionState.SetString(Key + ".First", first); g.ReelFishing(); Require(FishCount(g) == 1 && g.Stamina.State.Current == 95, "Duplicate catch cannot reward");
                    SaveDirectory(g, SessionState.GetString(Key + ".Dir", "")); View.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.Stamina == 95 && saved.Slots.Sum(s => s.ItemId == first ? s.Quantity : 0) == 1, "Retry writes catch once");
                    int other = first == "sardine" ? 1 : 0; g.Fishing.State.Cast(other, .25, g.Inventory.State, g.Stamina.State); g.Fishing.Tick(.25f); g.UI.RefreshViews(); View.ReelButton.onClick.Invoke();
                    Require(g.Inventory.GetCount("sardine") == 1 && g.Inventory.GetCount("sea_bass") == 1 && g.Stamina.State.Current == 90 && g.Economy.Gold == 100 && g.Tasks.State.Capture().All(t => t.Progress == 0), "Both fish catchable, no crop task/economy side effect");
                    g.CloseFishing(); g.ToggleInventory(); Phase(8);
                }
                else if (phase == 8 && g.UI.IsInventoryVisible)
                {
                    var bag = UnityEngine.Object.FindObjectOfType<InventoryView>(); int slot = Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == "sardine"); bag.Slots[slot].SelectButton.onClick.Invoke();
                    Require(!bag.UseFoodButton.gameObject.activeSelf && !bag.UseSeedButton.gameObject.activeSelf && bag.DetailTitleText.text == "沙丁鱼", "Raw fish item details, not food/seed"); TextCheck(bag); g.CloseInventory(); g.Players.RestorePosition(g.Shop.Counter.position + Vector3.back * 1.2f); g.OpenShop(); Phase(9);
                }
                else if (phase == 9 && Shop != null)
                {
                    string first = SessionState.GetString(Key + ".First", ""); int index = first == "sardine" ? 3 : 4; Shop.CropButtons[index].onClick.Invoke();
                    Require(g.Shop.SelectedIsFish && !Shop.BuyButton.interactable && Shop.SellButton.interactable && g.TradeShop(true) == MagicIsland.Economy.TradeResult.NotForSale, "Fish offers are sale-only");
                    TextCheck(Shop); Capture(g, "FishShopPreview"); long price = g.Shop.Crop.SellPrice; SessionState.SetInt(Key + ".Price", (int)price);
                    SaveDirectory(g, Path.Combine(SessionState.GetString(Key + ".Dir", ""), "blocked")); Action observer = () => { Require(g.Inventory.GetCount(first) == 0 && g.Economy.Gold == 100 + price, "Fish sale atomic"); g.TradeShop(false); g.ReturnToMainMenu(); };
                    g.Inventory.State.Changed += observer; Shop.SellButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.State == GameFlowState.Playing && g.Shop.Feedback.Contains("保存失败") && FishCount(g) == 1 && g.Economy.Gold == 100 + price && g.Tasks.State.Capture().All(t => t.Progress == 0), "Fish sale saves atomically, failure retains trade without sell task");
                    g.TradeShop(false); Require(g.Economy.Gold == 100 + price, "No duplicate fish sale"); SaveDirectory(g, SessionState.GetString(Key + ".Dir", "")); Shop.SaveButton.onClick.Invoke();
                    Require(g.Saves.TryLoad(out var saved) && saved.Gold == 100 + price && saved.Slots.Sum(s => s.ItemId == first ? s.Quantity : 0) == 0, "Sale retry saves inventory and gold together");
                    g.CloseShop(); g.ReturnToMainMenu(); Phase(10);
                }
                else if (phase == 10 && g.State == GameFlowState.MainMenu)
                { Require(g.Fishing.State == null && g.Fishing.Spot == null && g.Fishing.Rod == null && !g.UI.HasFishing && UnityEngine.Object.FindObjectsOfType<FishingRodVisual>().Length == 0, "Fishing resources and session cleanup"); g.ContinueGame(); Phase(11); }
                else if (phase == 11 && g.State == GameFlowState.Playing)
                {
                    Require(FishCount(g) == 1 && g.Stamina.State.Current == 90 && g.Economy.Gold == 100 + SessionState.GetInt(Key + ".Price", 0) && g.Fishing.State.Phase == FishingPhase.Idle, "Reload keeps catch/trade/stamina without pending cast");
                    Place(g); g.Tools.SelectTool(FarmTool.FishingRod); g.OpenFishing(); Phase(12);
                }
                else if (phase == 12 && View != null)
                {
                    var bag = g.Inventory.State.Slots.ToArray(); g.Inventory.State.Restore(Enumerable.Repeat(new InventorySlot("turnip_seed", 99), g.Inventory.State.Capacity).ToArray()); g.Fishing.Tick((float)g.Fishing.State.Remaining + .001f); g.ReelFishing();
                    Require(g.Stamina.State.Current == 90 && g.Inventory.GetCount("turnip_seed") == 1188 && g.Fishing.State.Phase == FishingPhase.Missed, "Capacity rechecked at catch, no cost"); g.Inventory.State.Restore(bag); g.CloseFishing();
                    Require(g.Saves.TryLoad(out var saved) && saved.Version == SaveData.CurrentVersion, "Existing save schema remains valid");
                    var stored = g.Inventory.State.CreateEmpty(18); int fishSlot = Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.FindDefinition(g.Inventory.State.Slots[i].ItemId)?.Type == ItemType.Fish);
                    Require(g.Inventory.State.TryTransferSlotTo(fishSlot, 1, stored) && stored.GetCount("sardine") + stored.GetCount("sea_bass") == 1, "Fish transfers using existing storage rules");
                    g.Inventory.State.Restore(bag); g.ReturnToMainMenu(); Phase(13);
                }
                else if (phase == 13 && g.State == GameFlowState.MainMenu) { g.EnterGame(); Phase(14); }
                else if (phase == 14 && g.State == GameFlowState.Playing) { Require(FishCount(g) == 0 && g.Stamina.State.Current == 100 && g.Economy.Gold == 100, "New game resets fish and has no duplicate model"); Place(g); g.Tools.SelectTool(FarmTool.FishingRod); g.OpenFishing(); Phase(15); }
                else if (phase == 15 && View != null)
                {
                    g.ReturnToMainMenu(); Require(!g.UI.HasFishing && !g.Fishing.State.Active, "Return cancels pending session");
                    File.WriteAllText("Logs/FishingFlowVerification.txt", "PASS: packed AA config/rod/shore/FishingPanel, sixth tool and farm protection, range/UI/busy/stamina/capacity/modal gates, early/timeout/cancel/real bite, atomic catch/reentry/save failure/retry, both fish, sale-only shop/gold/no crop tasks, reload/new-game/session cleanup, storage compatibility and Chinese layout.\n");
                    Debug.Log("Fishing flow verification PASSED."); Phase(16); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
