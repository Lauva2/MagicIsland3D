using System;
using System.IO;
using System.Linq;
using MagicIsland.Farming;
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
    public static class ToolUpgradeFlowVerification
    {
        const string Key = "MagicIsland.UpgradeVerification";
        static ToolUpgradeFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int p) => SessionState.SetInt(Key + ".Phase", p);
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void DirectoryFor(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void Place(GameManager g, Vector3 target) => g.Players.RestorePosition(target + new Vector3(0, .1f, -1.5f));
        static void Capture(GameManager g, string name)
        {
            GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 12 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Upgrade timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Configuration failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.ToolUpgrades.Validate(g.Configuration.Items), "Catalog loads through packed AA");
                    string dir = Path.GetFullPath("Logs/UpgradeSaves/" + Guid.NewGuid().ToString("N")); SessionState.SetString(Key + ".Dir", dir); DirectoryFor(g, dir);
                    g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.ToolUpgrades.Workbench != null && UnityEngine.Object.FindObjectsOfType<ToolWorkbench>().Length == 1, "Single AA workbench");
                    var target = g.ToolUpgrades.Workbench.transform.position; Physics.SyncTransforms(); var ray = new Ray(target + Vector3.up * 4, Vector3.down);
                    Require(g.Interactions.Controller.RefreshWorkbenchHover(ray, false) && g.Interactions.Controller.HoverIndex == -1, "Workbench captures farm ray");
                    g.OpenToolUpgrade(); Require(!g.UI.HasToolUpgrade, "Distant bench blocked");
                    Place(g, target); Require(!g.Interactions.Controller.RefreshWorkbenchHover(ray, true), "UI ray interception");
                    var camera = g.Players.Player.ViewCamera; camera.transform.position = target + new Vector3(5, 5, -8); camera.transform.LookAt(target + Vector3.up);
                    Capture(g, "ToolWorkbenchPreview"); g.OpenToolUpgrade(); Phase(2);
                }
                else if (phase == 2 && g.UI.HasToolUpgrade && UnityEngine.Object.FindObjectOfType<ToolUpgradeView>() != null)
                {
                    var view = UnityEngine.Object.FindObjectOfType<ToolUpgradeView>();
                    Require(view.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp) && !view.UpgradeButtons[0].interactable && !view.UpgradeButtons[1].interactable, "PopUp MVC insufficient materials");
                    g.UpgradeTool(FarmTool.Axe); Require(g.Economy.Gold == 100 && g.ToolUpgrades.State.Level(FarmTool.Axe) == 0, "Failed purchase free");
                    g.Inventory.State.TryAdd("wood", 9); g.Inventory.State.TryAdd("stone", 6); g.Economy.Restore(125); g.UI.RefreshViews();
                    foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Upgrade text and Chinese font: " + text.name); }
                    Require(view.UpgradeButtons.All(b => b.interactable), "Available upgrade buttons"); Capture(g, "ToolUpgradePanelPreview");
                    SessionState.SetFloat(Key + ".Minute", (float)g.Time.Clock.Minute); SessionState.SetFloat(Key + ".Pause", (float)EditorApplication.timeSinceStartup); Phase(3);
                }
                else if (phase == 3 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Pause", 0) > .2)
                {
                    Require(Math.Abs(g.Time.Clock.Minute - SessionState.GetFloat(Key + ".Minute", 0)) < .001 && !g.TryGather(0, out _), "Modal pauses time and gathering");
                    int day = g.Farm.State.Day; g.AdvanceDay(); g.ToggleInventory(); g.OpenOrder(); g.OpenShop(); g.OpenSleep();
                    Require(g.Farm.State.Day == day && !g.UI.IsInventoryVisible && !g.UI.HasOrder && !g.UI.HasShop && !g.UI.HasSleep, "Modal excludes other actions");
                    UnityEngine.Object.FindObjectOfType<ToolUpgradeView>().UpgradeButtons[0].onClick.Invoke();
                    Require(g.ToolUpgrades.State.Level(FarmTool.Axe) == 1 && g.Inventory.GetCount("wood") == 3 && g.Inventory.GetCount("stone") == 4 && g.Economy.Gold == 75
                        && g.Saves.TryLoad(out var saved) && saved.ToolUpgrades[0].Level == 1 && saved.Gold == 75, "Button purchase autosaves cost and level");
                    g.UpgradeTool(FarmTool.Axe); Require(g.Economy.Gold == 75, "Repeat purchase ignored");
                    string blocked = Path.Combine(SessionState.GetString(Key + ".Dir", ""), "blocked"); File.WriteAllText(blocked, "file"); DirectoryFor(g, blocked);
                    g.UpgradeTool(FarmTool.Pickaxe); Require(g.ToolUpgrades.State.Level(FarmTool.Pickaxe) == 1 && g.Economy.Gold == 0 && g.Inventory.GetCount("wood") == 0
                        && g.Inventory.GetCount("stone") == 0 && g.ToolUpgrades.Feedback.Contains("保存失败"), "Autosave failure preserves one committed purchase");
                    DirectoryFor(g, SessionState.GetString(Key + ".Dir", "")); UnityEngine.Object.FindObjectOfType<ToolUpgradeView>().SaveButton.onClick.Invoke();
                    Require(g.Saves.TryLoad(out saved) && saved.ToolUpgrades.All(s => s.Level == 1) && saved.Gold == 0, "Save retry without repayment");
                    Capture(g, "ToolUpgradeCompletedPreview"); g.CloseToolUpgrade(); Place(g, g.Gathering.Nodes[0].transform.position); g.Tools.SelectTool(FarmTool.Axe);
                    Require(g.TryGather(0, out _) && g.Gathering.State.HitsRemaining(0) == 1 && g.Stamina.State.Current == 96, "Upgraded tree partial hit");
                    Place(g, g.ToolUpgrades.Workbench.transform.position); g.OpenToolUpgrade(); Require(!g.UI.HasToolUpgrade, "Busy swing excludes workbench"); Phase(4);
                }
                else if (phase == 4 && !g.Tools.Busy)
                {
                    Place(g, g.Gathering.Nodes[0].transform.position);
                    Require(g.Gathering.Describe(0, FarmTool.Axe).Contains("还需 1") && g.TryGather(0, out _) && g.Gathering.State.IsDepleted(0)
                        && g.Inventory.GetCount("wood") == 3 && g.Stamina.State.Current == 92, "Second tree hit yields once"); Phase(5);
                }
                else if (phase == 5 && !g.Tools.Busy)
                {
                    Place(g, g.Gathering.Nodes[3].transform.position); g.Tools.SelectTool(FarmTool.Pickaxe);
                    Require(g.Gathering.Describe(3, FarmTool.Pickaxe).Contains("还需 1") && g.TryGather(3, out _) && g.Gathering.State.IsDepleted(3)
                        && g.Inventory.GetCount("stone") == 2 && g.Stamina.State.Current == 87, "Rock one hit with unchanged stamina cost"); Phase(6);
                }
                else if (phase == 6 && !g.Tools.Busy) { g.ReturnToMainMenu(); Phase(7); }
                else if (phase == 7 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.ToolUpgrades.State == null && g.ToolUpgrades.Workbench == null && UnityEngine.Object.FindObjectsOfType<ToolWorkbench>().Length == 0, "Workbench resources cleared");
                    g.ContinueGame(); Phase(8);
                }
                else if (phase == 8 && g.State == GameFlowState.Playing)
                {
                    Require(g.ToolUpgrades.State.Level(FarmTool.Axe) == 1 && g.ToolUpgrades.State.Level(FarmTool.Pickaxe) == 1 && g.Gathering.State.IsDepleted(0)
                        && g.Inventory.GetCount("wood") == 3 && g.Inventory.GetCount("stone") == 2 && g.Economy.Gold == 0, "Reload restores upgrades and loot without duplication");
                    Require(g.Saves.TryLoad(out var data), "Read current save"); string original = JsonUtility.ToJson(data); data.ToolUpgrades[0].Level = 2;
                    File.WriteAllText(g.Saves.SavePath, JsonUtility.ToJson(data)); File.WriteAllText(g.Saves.SavePath + ".bak", JsonUtility.ToJson(data));
                    Require(!g.Saves.TryLoad(out _), "Invalid saved level rejected");
                    File.WriteAllText(g.Saves.SavePath, original); File.WriteAllText(g.Saves.SavePath + ".bak", original);
                    g.ReturnToMainMenu(); Phase(9);
                }
                else if (phase == 9 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var data), "Read migration fixture"); string resources = JsonUtility.ToJson(data.Gathering[0]); string order = JsonUtility.ToJson(data.Order);
                    data.Version = 9; data.ToolUpgrades = null; File.WriteAllText(g.Saves.SavePath, JsonUtility.ToJson(data)); File.WriteAllText(g.Saves.SavePath + ".bak", JsonUtility.ToJson(data));
                    Require(g.Saves.TryLoad(out var migrated) && migrated.Version == SaveData.CurrentVersion && migrated.ToolUpgrades.All(s => s.Level == 0) && JsonUtility.ToJson(migrated.Gathering[0]) == resources
                        && JsonUtility.ToJson(migrated.Order) == order && migrated.Stamina == 87 && migrated.Gold == 0, "v9 migration preserves resources/order/stamina/gold");
                    g.ContinueGame(); Phase(10);
                }
                else if (phase == 10 && g.State == GameFlowState.Playing)
                {
                    Require(g.ToolUpgrades.State.Level(FarmTool.Axe) == 0 && g.Gathering.State.IsDepleted(0), "Migrated save playable"); g.ReturnToMainMenu(); Phase(11);
                }
                else if (phase == 11 && g.State == GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/ToolUpgradeFlowVerification.txt", "PASS: Packed AA config/workbench/Panel MVC, reach/ray/UI/modal/busy guards, atomic costs, autosave failure/retry, upgraded gathering, reload/migration/corruption/lifecycle, TMP layout.\n");
                    Debug.Log("Tool upgrade flow verification PASSED."); Phase(12); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
