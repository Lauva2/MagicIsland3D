using System;
using System.IO;
using System.Linq;
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
    public static class HouseUpgradeFlowVerification
    {
        const string Key = "MagicIsland.HouseUpgradeVerification";
        static HouseUpgradeFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static HouseUpgradeView View => UnityEngine.Object.FindObjectOfType<HouseUpgradeView>();
        static OrderView Order => UnityEngine.Object.FindObjectOfType<OrderView>();
        static InventoryView Bag => UnityEngine.Object.FindObjectOfType<InventoryView>();
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir, g.Configuration.Tasks,
            g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships,
            g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void At(GameManager g, float x, float z) => g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin + new Vector3(x, .1f, z));
        static void Door(GameManager g) => g.Players.RestorePosition(g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f));
        static void Office(GameManager g) => g.Players.RestorePosition(g.Orders.Board.transform.position + new Vector3(0, .1f, -1.5f));
        static void Begin(GameManager g, string item)
        { g.ToggleInventory(); int slot = Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == item); Bag.Slots[slot].SelectButton.onClick.Invoke(); Bag.PlaceButton.onClick.Invoke(); }
        static void TextCheck(Component view)
        { foreach (var t in view.GetComponentsInChildren<TMPro.TMP_Text>()) { t.ForceMeshUpdate(); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "House upgrade Chinese/layout: " + t.name + "=" + t.text); } }
        static void Capture(GameManager g, string name)
        { g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); g.UI.RefreshViews(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Write(GameManager g, string json) { File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 23 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("House upgrade timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "House upgrade configuration failed");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Housing.Validate(g.Configuration.Items) && g.Configuration.Housing.UpgradeGold == 300 && g.Configuration.Housing.BuildDays == 1, "Housing AA configuration upgrade costs");
                    int wood = g.Configuration.Housing.UpgradeWood; g.Configuration.Housing.UpgradeWood = 0; Require(!g.Configuration.Housing.IsValid, "Invalid upgrade costs rejected"); g.Configuration.Housing.UpgradeWood = wood;
                    string dir = Path.GetFullPath("Logs/HouseUpgradeSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Housing.Upgrade.Level == 0 && !g.Housing.Upgrade.Building && g.Furniture.State.HouseLevel == 0, "New game grants no upgrade");
                    g.Inventory.TryAdd("wood", 19); g.Inventory.TryAdd("stone", 10); g.Inventory.TryAdd("wood_table", 1); g.Inventory.TryAdd("wood_chair", 1); g.Economy.Restore(299); g.Farm.TryAct(0, MagicIsland.Farming.FarmTool.Hoe, out _);
                    g.OpenHouseUpgrade(); Require(!g.UI.HasHouseUpgrade, "Office reach guard"); Door(g); g.EnterHome(); Phase(2);
                }
                else if (phase == 2 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Housing.Interior.OriginalWalls.All(w => w.activeSelf), "Original room remains unchanged before upgrade"); At(g, 2, .2f); Begin(g, "wood_table"); Phase(3); }
                else if (phase == 3 && g.Furniture.Ready && UnityEngine.Object.FindObjectOfType<PlacementView>() != null)
                { g.Furniture.SetCandidate(2, 2); g.RotatePlacement(); g.ConfirmPlacement(); Require(g.Furniture.State.Count == 1, "Original furniture fixture"); At(g, 0, -3); g.ExitHome(); Phase(4); }
                else if (phase == 4 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                { Office(g); g.OpenOrder(); Phase(5); }
                else if (phase == 5 && Order != null)
                { TextCheck(Order); Order.HouseUpgradeButton.onClick.Invoke(); Phase(6); }
                else if (phase == 6 && View != null)
                {
                    Require(g.UI.HasHouseUpgrade && !g.UI.HasOrder && View.Layer == UILayer.PopUp && !g.Players.Player.enabled && !View.ApplyButton.interactable, "AA MVC office service switches modal and checks costs");
                    g.ApplyHouseUpgrade(); Require(!g.Housing.Upgrade.Building && g.Inventory.GetCount("wood") == 19 && g.Economy.Gold == 299, "Missing material refusal costs nothing");
                    g.Inventory.TryAdd("wood", 11); g.Inventory.TryAdd("stone", 5); g.ApplyHouseUpgrade(); Require(!g.Housing.Upgrade.Building && g.Inventory.GetCount("wood") == 30, "Missing gold refusal costs nothing");
                    g.Economy.Restore(500); g.UI.RefreshViews(); Require(View.FeedbackText.text.Contains("备齐"), "Live costs clear old insufficient-gold feedback"); TextCheck(View); Capture(g, "HouseUpgradePanelPreview");
                    g.ToggleInventory(); g.AdvanceDay(); Require(!g.UI.IsInventoryVisible && g.Farm.State.Day == 1, "Upgrade modal blocks bag and day advance");
                    var office = g.Players.Player.transform.position; g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.ApplyHouseUpgrade(); Require(!g.Housing.Upgrade.Building, "Distant confirmation cannot pay"); g.Players.RestorePosition(office);
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () => { Require(g.Inventory.GetCount("wood") == 10 && g.Inventory.GetCount("stone") == 5 && g.Economy.Gold == 200 && g.Housing.Upgrade.CompletionDay == 2, "Upgrade publishes atomic costs/construction"); g.ApplyHouseUpgrade(); g.ReturnToMainMenu(); g.CloseHouseUpgrade(); };
                    g.Inventory.State.Changed += observer; View.ApplyButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.UI.HasHouseUpgrade && g.Housing.Upgrade.Level == 0 && g.Housing.Upgrade.Building && g.Housing.UpgradeFeedback.Contains("保存失败"), "Save failure retains pending request and rejects reentry"); g.ApplyHouseUpgrade(); Require(g.Economy.Gold == 200 && g.Inventory.GetCount("wood") == 10, "No repeated payment");
                    SaveDirectory(g, dir); View.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.Version == SaveData.CurrentVersion && saved.HouseUpgrade.Level == 0 && saved.HouseUpgrade.CompletionDay == 2 && saved.Furniture.Length == 1, "Pending request and original furniture saved together"); TextCheck(View);
                    g.CloseHouseUpgrade(); g.ReturnToMainMenu(); Phase(7);
                }
                else if (phase == 7 && g.State == GameFlowState.MainMenu)
                { Require(g.Housing.Upgrade == null && !g.Scenes.HomeScene.IsValid(), "Menu releases housing upgrade models/state"); g.ContinueGame(); Phase(8); }
                else if (phase == 8 && g.State == GameFlowState.Playing)
                { Require(!g.Housing.Inside && !g.Scenes.HomeScene.IsValid() && g.Housing.Upgrade.Building && g.Housing.Upgrade.CompletionDay == 2 && g.Economy.Gold == 200 && g.Furniture.State.Count == 1, "Construction reload without early completion or extra charge"); Office(g); g.OpenHouseUpgrade(); Phase(9); }
                else if (phase == 9 && View != null)
                { Require(!View.ApplyButton.interactable && View.StatusText.text.Contains("施工中"), "Pending service status"); TextCheck(View); SessionState.SetString(Key + ".Clock", g.Time.Clock.Minute.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Phase(10); }
                else if (phase == 10 && View != null)
                { Require(g.Time.Clock.Minute == double.Parse(SessionState.GetString(Key + ".Clock", "0"), System.Globalization.CultureInfo.InvariantCulture), "Office modal pauses clock"); g.CloseHouseUpgrade(); Door(g); g.EnterHome(); Phase(11); }
                else if (phase == 11 && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    SessionState.SetInt(Key + ".Room", g.Housing.Interior.GetInstanceID()); SessionState.SetInt(Key + ".Table", g.Furniture.Objects.Values.Single().GetInstanceID());
                    Require(g.Housing.Interior.OriginalWalls.All(w => w.activeSelf) && g.Furniture.State.HouseLevel == 0, "Construction keeps original room until next day"); At(g, -1.5f, 2); SessionState.SetString(Key + ".Player", JsonUtility.ToJson(g.Housing.SavedPosition)); g.RequestRest(); Phase(12);
                }
                else if (phase == 12 && UnityEngine.Object.FindObjectOfType<SleepView>() != null)
                {
                    SaveDirectory(g, Path.Combine(SessionState.GetString(Key + ".Dir", ""), "blocked"));
                    UnityEngine.Object.FindObjectOfType<SleepView>().ConfirmButton.onClick.Invoke();
                    Require(g.Farm.State.Day == 2 && g.Housing.Upgrade.Level == 1 && !g.Housing.Upgrade.Building && g.Furniture.State.HouseLevel == 1, "Sleep completes next day before save");
                    Require(g.Interactions.Controller.Feedback.Contains("保存失败"), "Completion save failure retains expanded world"); SaveDirectory(g, SessionState.GetString(Key + ".Dir", "")); Require(g.SaveProgress(), "Completion save retry succeeds");
                    Require(g.Housing.Interior.GetInstanceID() == SessionState.GetInt(Key + ".Room", 0) && g.Furniture.Objects.Values.Single().GetInstanceID() == SessionState.GetInt(Key + ".Table", 0), "In-place extension preserves room and furniture instances");
                    var before = JsonUtility.FromJson<Vector3>(SessionState.GetString(Key + ".Player", "{}")); Require(Vector2.Distance(new Vector2(before.x, before.z), new Vector2(g.Housing.SavedPosition.x, g.Housing.SavedPosition.z)) < .001f, "Completion preserves player horizontal position");
                    Require(g.Housing.Interior.OriginalWalls.All(w => !w.activeSelf) && g.Housing.Interior.transform.Find("PlayerHomeExpansion(Clone)") != null && g.Housing.Entrance.NameText.text == "舒适小屋", "AA extension geometry and upgraded exterior");
                    Require(g.Housing.Interior.Bed.transform.localPosition == new Vector3(-3, 0, 2) && g.Housing.Interior.SpawnPoint.localPosition == new Vector3(0, .1f, -2.5f) && g.Housing.Interior.Exit.transform.localPosition == new Vector3(0, 0, -4.5f), "Bed/spawn/exit unchanged");
                    At(g, 5, 0); g.Players.Player.GetComponent<CharacterController>().Move(Vector3.right * 2.4f); Require(g.Housing.SavedPosition.x > 7.3f, "Former east wall no longer blocks movement"); At(g, 8, 3.2f); Begin(g, "wood_chair"); Phase(13);
                }
                else if (phase == 13 && g.Furniture.Ready && UnityEngine.Object.FindObjectOfType<PlacementView>() != null)
                {
                    g.Furniture.SetCandidate(8, 5); Require(g.Furniture.CheckCandidate() == PlacementResult.Success, "New area has floor and is placeable"); g.RotatePlacement(); g.RotatePlacement(); g.ConfirmPlacement();
                    Require(g.Furniture.State.Count == 2 && g.Saves.TryLoad(out var save) && save.HouseUpgrade.Level == 1 && save.Furniture.Any(f => f.GridX == 8 && f.GridZ == 5), "Expanded furniture position autosaves");
                    At(g, 4, -2.5f); Capture(g, "ExpandedHomePreview"); At(g, 7.5f, 4.4f); UnityEngine.Time.timeScale = 0; Require(g.SaveProgress(), "Expanded player position saved"); g.ReturnToMainMenu(); Phase(14);
                }
                else if (phase == 14 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(15); }
                else if (phase == 15 && g.State == GameFlowState.Playing)
                {
                    Require(g.Housing.Inside && g.Housing.Upgrade.Level == 1 && g.Furniture.Objects.Count == 2 && g.Farm.State.Cells[0].Tilled && g.Economy.Gold == 200 && g.Inventory.GetCount("wood") == 10, "Completed indoor reload restores geometry/world/paid costs");
                    Require(Vector3.Distance(g.Housing.SavedPosition, new Vector3(7.5f, .1f, 4.4f)) < .001f && g.Furniture.State.Capture().Single(f => f.ItemId == "wood_chair").QuarterTurns == 2, "Expanded position/rotation exact restore");
                    UnityEngine.Time.timeScale = 1; At(g, 0, -3); g.ExitHome(); Phase(16);
                }
                else if (phase == 16 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                { Office(g); g.OpenHouseUpgrade(); Phase(17); }
                else if (phase == 17 && View != null)
                { Require(!View.ApplyButton.interactable && View.StatusText.text.Contains("已完工"), "Single upgrade completed status"); g.ApplyHouseUpgrade(); Require(g.Economy.Gold == 200, "Completed request cannot charge again"); Capture(g, "HouseUpgradeCompletedPreview"); g.CloseHouseUpgrade(); g.ReturnToMainMenu(); Phase(18); }
                else if (phase == 18 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var save), "Completed save available"); SessionState.SetString(Key + ".Good", JsonUtility.ToJson(save)); save.HouseUpgrade.Level = 2; Write(g, JsonUtility.ToJson(save)); Require(!g.Saves.TryLoad(out _), "Unknown house level rejects primary and backup");
                    save = JsonUtility.FromJson<SaveData>(SessionState.GetString(Key + ".Good", "")); save.HouseUpgrade = new SavedHouseUpgrade { CompletionDay = save.Day }; Write(g, JsonUtility.ToJson(save)); Require(!g.Saves.TryLoad(out _), "Overdue pending construction rejected");
                    save = JsonUtility.FromJson<SaveData>(SessionState.GetString(Key + ".Good", "")); save.Version = 18; save.HouseUpgrade = null; save.Furniture = save.Furniture.Where(f => f.ItemId == "wood_table").ToArray(); save.PlayerArea = HousingState.Home; save.PlayerX = 1; save.PlayerY = .1f; save.PlayerZ = -1;
                    Write(g, JsonUtility.ToJson(save)); g.ContinueGame(); Phase(19);
                }
                else if (phase == 19 && g.State == GameFlowState.Playing)
                {
                    Require(g.Housing.Inside && g.Housing.Upgrade.Level == 0 && !g.Housing.Upgrade.Building && g.Furniture.State.Count == 1 && g.Housing.Interior.OriginalWalls.All(w => w.activeSelf) && g.Economy.Gold == 200 && g.Farm.State.Day == 2, "v18 migration preserves old furniture, indoor position and world; grants no upgrade");
                    At(g, 0, -3); g.ExitHome(); Phase(20);
                }
                else if (phase == 20 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                { Office(g); g.OpenHouseUpgrade(); g.CloseHouseUpgrade(); Phase(21); }
                else if (phase == 21 && g.State == GameFlowState.Playing && !g.UI.HasHouseUpgrade)
                { Require(g.Players.Player.enabled && !g.Housing.Upgrade.Building, "Close during asynchronous panel loading retains state/input"); g.ReturnToMainMenu(); Phase(22); }
                else if (phase == 22 && g.State == GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/HouseUpgradeVerification.txt", "PASS: packed AA housing upgrade UI/exterior/interior, office entry, range/modal/reentry guards, atomic costs, pending save/retry/reload, next-day in-place completion, retained furniture/bed/exit/player, expanded movement and placement, completed reload, invalid saves, v18 migration and Chinese TMP.");
                    Debug.Log("House upgrade flow verification PASSED."); Phase(23); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { UnityEngine.Time.timeScale = 1; SessionState.SetBool(Key, false); Debug.LogError("House upgrade phase " + phase + ": " + error); EditorApplication.Exit(1); }
        }
    }
}
