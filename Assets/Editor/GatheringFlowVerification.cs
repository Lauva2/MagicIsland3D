using System;
using System.IO;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Gathering;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.Saving;
using MagicIsland.Tasks;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class GatheringFlowVerification
    {
        private const string Running = "MagicIsland.GatheringVerification";
        static GatheringFlowVerification() { if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(builder => builder is BuildScriptPackedPlayMode);
            SessionState.SetBool(Running, true); Phase(0); SessionState.SetFloat(Running + ".Started", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        private static void Phase(int value) => SessionState.SetInt(Running + ".Phase", value);
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        private static PrototypeHud Hud => UnityEngine.Object.FindObjectOfType<PrototypeHud>();
        private static void SaveDirectory(GameManager game, string directory) => game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items,
            game.Configuration.Crops, directory, game.Configuration.Tasks, game.Configuration.Orders, game.Configuration.Seasons, game.Configuration.Gathering, game.Configuration.ToolUpgrades, game.Configuration.Storage);
        private static void Capture(GameManager game, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true);
        }
        private static void CheckText(TMPro.TMP_Text text)
        {
            text.ForceMeshUpdate(); Require(!text.isTextOverflowing, "Gathering text fits: " + text.name);
            Require(text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Gathering glyphs baked: " + text.text);
        }
        private static void Place(GameManager game, int index) => game.Players.RestorePosition(game.Gathering.Nodes[index].transform.position + new Vector3(1.5f, 0.1f, -0.3f));
        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            int phase = SessionState.GetInt(Running + ".Phase", 0);
            if (phase == 15 && !EditorApplication.isPlaying) { SessionState.SetBool(Running, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Running + ".Started", 0) > 160) throw new Exception("Gathering timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var game = GameManager.Instance; if (game == null) return;
                Require(game.State != GameFlowState.Failed, "Game configuration failed");
                if (phase == 0 && game.State == GameFlowState.MainMenu)
                {
                    var catalog = game.Configuration.Gathering;
                    Require(catalog != null && catalog.Validate(game.Configuration.Items, game.Configuration.Config) && catalog.Nodes.Count == 5 && catalog.Types.Count == 2,
                        "GatheringCatalog packed AA before menu");
                    int size = game.Configuration.Config.FarmSize; game.Configuration.Config.FarmSize = 12;
                    Require(catalog.Validate(game.Configuration.Items, game.Configuration.Config), "Default resources clear largest farm"); game.Configuration.Config.FarmSize = size;
                    int yield = catalog.Types[0].Yield; catalog.Types[0].Yield = 0; Require(!catalog.Validate(game.Configuration.Items, game.Configuration.Config), "Invalid gathering quantity rejected"); catalog.Types[0].Yield = yield;
                    var position = catalog.Nodes[0].Position; catalog.Nodes[0].Position = Vector3.zero;
                    Require(!catalog.Validate(game.Configuration.Items, game.Configuration.Config), "Resource inside farm rejected"); catalog.Nodes[0].Position = position;
                    Require(game.Configuration.Items.Find("wood").Type == ItemType.Material && game.Configuration.Items.Find("stone").Type == ItemType.Material, "Material catalog definitions");
                    game.Configuration.Seasons.SpringRainChance = 0;
                    string directory = Path.GetFullPath("Logs/GatheringSaves/" + Guid.NewGuid().ToString("N"));
                    SessionState.SetString(Running + ".Directory", directory); SaveDirectory(game, directory); game.EnterGame(); Phase(1);
                }
                else if (phase == 1 && game.State == GameFlowState.Playing)
                {
                    Require(game.Gathering.Nodes.Count == 5 && UnityEngine.Object.FindObjectsOfType<GatheringNode>().Length == 5
                        && game.Gathering.Nodes.All(node => node.WholeVisual.activeSelf && !node.DepletedVisual.activeSelf), "Five AA resource instances initialized once");
                    Require(Hud.ToolButtons.Length == 5 && game.Tools.Presentation.Grip.Find("Axe") != null && game.Tools.Presentation.Grip.Find("Pickaxe") != null, "HUD and held gathering tools");
                    var point = game.Gathering.Nodes[0].transform.position; var ray = new Ray(point + Vector3.up * 6, Vector3.down); Physics.SyncTransforms();
                    Require(game.Interactions.Controller.RefreshGatheringHover(ray, false) && game.Interactions.Controller.GatheringHoverIndex == 0
                        && game.Interactions.Controller.HoverIndex == -1 && game.Interactions.Controller.Hint.Contains("太远"), "Tree ray captures hover without farm input");
                    Hud.ToolButtons[3].onClick.Invoke(); Require(game.Tools.SelectedTool == FarmTool.Axe && !game.TryGather(0, out _) && game.Stamina.State.Current == 100, "Distant gathering free");
                    Place(game, 0); Require(game.Interactions.Controller.RefreshGatheringHover(ray, false) && game.Interactions.Controller.Hint.Contains("还需 3")
                        && !game.Interactions.Controller.RefreshGatheringHover(ray, true), "Near resource hint and UI interception");
                    Hud.ToolButtons[4].onClick.Invoke(); Require(!game.TryGather(0, out string wrong) && wrong.Contains("斧头") && !game.Tools.Busy && game.Stamina.State.Current == 100, "Wrong tool free and releases action lock");
                    Hud.ToolButtons[3].onClick.Invoke();
                    game.Interactions.Controller.RefreshGatheringHover(ray, false); game.UI.RefreshViews();
                    Camera camera = game.Players.Player.ViewCamera; var cp = camera.transform.position; var cr = camera.transform.rotation;
                    camera.transform.position = point + new Vector3(6, 6, -9); camera.transform.LookAt(point + Vector3.up * 1.4f);
                    var feedback = UnityEngine.Object.FindObjectOfType<FeedbackView>(); float alpha = feedback.Group.alpha; feedback.Group.alpha = 0;
                    Capture(game, "GatheringTreePreview"); feedback.Group.alpha = alpha; camera.transform.SetPositionAndRotation(cp, cr);
                    Require(game.TryGather(0, out _) && game.Tools.Busy && game.Gathering.State.HitsRemaining(0) == 2 && game.Stamina.State.Current == 96
                        && game.Inventory.GetCount("wood") == 0, "First chop commits partial hit and stamina");
                    Require(!game.TryGather(0, out _) && !game.Tools.TryAct(0, out _), "Action gate shared with farm/gathering");
                    Hud.ToolButtons[4].onClick.Invoke(); Require(game.Tools.SelectedTool == FarmTool.Axe, "Busy tool cannot switch"); Phase(2);
                }
                else if (phase == 2 && game.Tools.Busy && game.Tools.ActionProgress >= 0.15f)
                {
                    Require(Quaternion.Angle(game.Tools.Presentation.Grip.localRotation, Quaternion.identity) > 1 && game.Tools.Presentation.ActiveEffectCount > 0,
                        "Axe animation and effects");
                    game.ToggleInventory(); SessionState.SetFloat(Running + ".Progress", game.Tools.ActionProgress);
                    SessionState.SetFloat(Running + ".PauseStarted", (float)EditorApplication.timeSinceStartup); Phase(3);
                }
                else if (phase == 3 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Running + ".PauseStarted", 0) > 0.15f)
                {
                    Require(game.UI.IsInventoryVisible && !game.TryGather(0, out _) && game.Tools.ActionProgress == SessionState.GetFloat(Running + ".Progress", 0), "Modal pauses gathering animation and blocks hits");
                    game.CloseInventory(); Phase(4);
                }
                else if (phase == 4 && !game.Tools.Busy)
                { game.ReturnToMainMenu(); Phase(5); }
                else if (phase == 5 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Gathering.State == null && game.Gathering.Nodes.Count == 0 && UnityEngine.Object.FindObjectsOfType<GatheringNode>().Length == 0, "Resource state/instances cleared on return");
                    Require(game.Saves.TryLoad(out SaveData data) && data.Gathering[0].HitsRemaining == 2 && data.Stamina == 96, "Partial chop persists");
                    string original = JsonUtility.ToJson(data); data.Gathering[0].RespawnDay = 2; string invalid = JsonUtility.ToJson(data);
                    File.WriteAllText(game.Saves.SavePath, invalid); File.WriteAllText(game.Saves.SavePath + ".bak", invalid);
                    game.ContinueGame(); Require(game.State == GameFlowState.MainMenu && !game.Saves.HasSave, "Active-node cooldown corruption rejected before world restore");
                    File.WriteAllText(game.Saves.SavePath, original); File.WriteAllText(game.Saves.SavePath + ".bak", original); game.ContinueGame(); Phase(6);
                }
                else if (phase == 6 && game.State == GameFlowState.Playing)
                {
                    Require(game.Gathering.State.HitsRemaining(0) == 2 && game.Stamina.State.Current == 96 && game.Gathering.Nodes.Count == 5, "Partial chop resumes with no duplicate instances/loot");
                    Place(game, 0); Hud.ToolButtons[3].onClick.Invoke(); Require(game.TryGather(0, out _) && game.Gathering.State.HitsRemaining(0) == 1, "Second chop after reload"); Phase(7);
                }
                else if (phase == 7 && !game.Tools.Busy)
                {
                    string directory = SessionState.GetString(Running + ".Directory", ""); string blocked = Path.Combine(directory, "blocked"); File.WriteAllText(blocked, "not a directory");
                    SaveDirectory(game, blocked); Require(game.TryGather(0, out string message) && message.Contains("保存失败") && game.Inventory.GetCount("wood") == 3
                        && game.Gathering.State.IsDepleted(0) && game.Stamina.State.Current == 88, "Completed chop autosave failure preserves material/stamina/depletion");
                    SaveDirectory(game, directory); Phase(8);
                }
                else if (phase == 8 && !game.Tools.Busy)
                {
                    Require(!game.Gathering.Nodes[0].WholeVisual.activeSelf && game.Gathering.Nodes[0].DepletedVisual.activeSelf
                        && !game.TryGather(0, out _) && game.Inventory.GetCount("wood") == 3 && game.Stamina.State.Current == 88, "Stump visible and depleted node cannot pay twice");
                    Require(game.SaveProgress() && game.Saves.TryLoad(out SaveData data) && data.Gathering[0].RespawnDay == 4 && data.Gathering[0].HitsRemaining == 0, "Save retry records fixed respawn day");
                    game.UI.RefreshViews(); CheckText(Hud.InventoryText); CheckText(Hud.HintText);
                    foreach (var button in Hud.ToolButtons) CheckText(button.GetComponentInChildren<TMPro.TMP_Text>());
                    var bag = UnityEngine.Object.FindObjectOfType<InventoryView>(true); game.ToggleInventory();
                    int slot = Enumerable.Range(0, game.Inventory.State.Capacity).First(i => game.Inventory.State.Slots[i].ItemId == "wood");
                    bag.Slots[slot].SelectButton.onClick.Invoke(); Require(bag.DetailTitleText.text == "木材" && !bag.UseSeedButton.interactable, "Materials display in existing MVC bag and cannot plant");
                    CheckText(bag.DetailText); Capture(game, "GatheringBagPreview"); game.CloseInventory();
                    Place(game, 3); Hud.ToolButtons[4].onClick.Invoke(); Require(game.TryGather(3, out _) && game.Stamina.State.Current == 83 && game.Gathering.State.HitsRemaining(3) == 1, "First pickaxe hit"); Phase(9);
                }
                else if (phase == 9 && !game.Tools.Busy)
                {
                    var slots = game.Inventory.State.Slots.ToArray(); var full = Enumerable.Repeat(new InventorySlot("wood", 99), slots.Length).ToArray();
                    game.Inventory.State.Restore(full); Require(!game.TryGather(3, out string message) && message.Contains("背包已满")
                        && game.Gathering.State.HitsRemaining(3) == 1 && game.Stamina.State.Current == 83, "Full bag final hit leaves rock and stamina intact"); game.Inventory.State.Restore(slots);
                    game.Stamina.Restore(4); Require(!game.TryGather(3, out message) && message.Contains("体力不足") && game.Gathering.State.HitsRemaining(3) == 1, "Insufficient gathering stamina free"); game.Stamina.Restore(83);
                    Require(game.TryGather(3, out _) && game.Inventory.GetCount("stone") == 2 && game.Stamina.State.Current == 78
                        && game.Saves.TryLoad(out SaveData data) && data.Gathering[3].RespawnDay == 3 && data.Stamina == 78 && data.Version == SaveData.CurrentVersion, "Completed rock autosaves loot/cooldown/stamina"); Phase(10);
                }
                else if (phase == 10 && !game.Tools.Busy)
                {
                    Require(game.Gathering.Nodes[3].DepletedVisual.activeSelf && game.Tasks.State.GetProgress(4) == 0 && game.Tasks.State.GetProgress(5) == 0, "Gathering not crop harvest/shop sale");
                    game.UI.RefreshViews(); Capture(game, "GatheringDepletedPreview"); game.ReturnToMainMenu(); Phase(11);
                }
                else if (phase == 11 && game.State == GameFlowState.MainMenu) { game.ContinueGame(); Phase(12); }
                else if (phase == 12 && game.State == GameFlowState.Playing)
                {
                    Require(game.Gathering.Nodes[0].DepletedVisual.activeSelf && game.Gathering.Nodes[3].DepletedVisual.activeSelf
                        && game.Inventory.GetCount("wood") == 3 && game.Inventory.GetCount("stone") == 2 && game.Stamina.State.Current == 78, "Depleted resources and materials resume unchanged");
                    var tree = game.Gathering.Nodes[0]; var rock = game.Gathering.Nodes[3]; game.AdvanceDay();
                    Require(game.Gathering.State.IsDepleted(0) && game.Gathering.State.IsDepleted(3) && game.Stamina.State.Current == 100, "Day 2 waiting with stamina refill");
                    game.AdvanceDay(); Require(game.Gathering.State.IsDepleted(0) && !game.Gathering.State.IsDepleted(3) && rock.WholeVisual.activeSelf, "Day 3 rock respawn");
                    game.AdvanceDay(); Require(!game.Gathering.State.IsDepleted(0) && tree.WholeVisual.activeSelf && game.Gathering.Nodes[0] == tree && game.Gathering.Nodes[3] == rock
                        && UnityEngine.Object.FindObjectsOfType<GatheringNode>().Length == 5 && game.Inventory.GetCount("wood") == 3, "Day 4 tree respawn reuses instances and no free material");
                    game.Tasks.State.Record(TaskAction.Hoe); game.ClaimTaskReward(); Require(game.Economy.Gold == 105, "Tutorial still functional");
                    game.ReturnToMainMenu(); Phase(13);
                }
                else if (phase == 13 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Saves.TryLoad(out SaveData data), "Read final gathering save"); string order = JsonUtility.ToJson(data.Order); data.Version = 8; data.Gathering = null;
                    File.WriteAllText(game.Saves.SavePath, JsonUtility.ToJson(data)); File.WriteAllText(game.Saves.SavePath + ".bak", JsonUtility.ToJson(data));
                    Require(game.Saves.TryLoad(out SaveData upgraded) && upgraded.Version == SaveData.CurrentVersion && upgraded.Gathering.All(value => value.HitsRemaining > 0 && value.RespawnDay == 0)
                        && upgraded.Gold == 105 && upgraded.Tasks[0].Claimed && JsonUtility.ToJson(upgraded.Order) == order && upgraded.Slots.Any(value => value.ItemId == "wood" && value.Quantity == 3),
                        "v8 migration adds untouched resources and preserves inventory/gold/tasks/order");
                    game.ContinueGame(); Phase(14);
                }
                else if (phase == 14 && game.State == GameFlowState.Playing)
                {
                    Require(game.Gathering.Nodes.Count == 5 && game.Gathering.State.Day == 4 && game.Inventory.GetCount("wood") == 3 && game.Economy.Gold == 105, "Legacy resources playable without duplicating materials");
                    File.WriteAllText("Logs/GatheringFlowVerification.txt", "PASS: packed AA GatheringCatalog, tree/rock prefabs and five resource nodes; catalog validation, range/ray/UI interception.\n"
                        + "MVC HUD five tools and material bag, Chinese/layout, shared action lock, axe/pickaxe animation/effects and modal pause.\n"
                        + "Wrong tool/full bag/stamina/depleted failure atomicity, partial/depleted save/resume, completion autosave and failure retry.\n"
                        + "Day 3 rock/day 4 tree respawn, stable instances, no duplicated output, corruption rejection, v8 migration retaining inventory/tasks/orders/gold, lifecycle cleanup.\n");
                    Debug.Log("Gathering flow verification PASSED."); Phase(15); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Running, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
