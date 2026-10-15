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
    public static class FeedCraftingFlowVerification
    {
        const string Key = "MagicIsland.FeedCraftingVerification", Feed = "animal_feed";
        static FeedCraftingFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            var paths = new[] { "Assets/Config/CraftingCatalog.asset", "Assets/Art/Prefabs/UI/CraftingPanel.prefab", "Assets/Art/Prefabs/UI/ToolUpgradePanel.prefab" };
            var snapshots = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(snapshots[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Complete recipes and layout survive repeated setup");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static CraftingView Bench => UnityEngine.Object.FindObjectOfType<CraftingView>();
        static CoopView Coop => UnityEngine.Object.FindObjectOfType<CoopView>();
        static int Slot(GameManager g, string id) => Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == id);
        static void AtBench(GameManager g) => g.Players.RestorePosition(g.ToolUpgrades.Workbench.transform.position + new Vector3(0, .1f, -1.7f));
        static void AtChest(GameManager g) => g.Players.RestorePosition(g.Storage.Chest.transform.position + new Vector3(0, .1f, -1.4f));
        static void AtCoop(GameManager g) => g.Players.RestorePosition(g.Animals.Coop.transform.position + new Vector3(0, .1f, -1.8f));
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static string Tasks(GameManager g) => JsonUtility.ToJson(new SaveData { Tasks = g.Tasks.State.Capture() });
        static void TextCheck(Component view)
        { foreach (var t in view.GetComponentsInChildren<TMPro.TMP_Text>()) { t.ForceMeshUpdate(); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Feed UI glyphs/layout: " + t.name + "=" + t.text); } }
        static void Capture(GameManager g, string name)
        { g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Write(GameManager g, string json) { File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void GrowCorn(GameManager g)
        {
            // A one-day season is a test fixture; plant in summer, then verify existing cross-season growth.
            g.AdvanceDay(); g.StartNewDayFromReport(); g.Inventory.TryAdd("corn_seed", 1); Require(g.Farm.SelectSeed("corn_seed"), "Select corn seed");
            Require(g.Farm.TryAct(0, FarmTool.Hoe, out _) && g.Farm.TryAct(0, FarmTool.Seeds, out _), "Actually sow corn in summer");
            for (int i = 0; i < g.Configuration.Crops.Find("corn").GrowthDays; i++)
            { if (!g.Farm.State.Cells[0].Watered) Require(g.Farm.TryAct(0, FarmTool.WateringCan, out _), "Water corn before growth"); g.AdvanceDay(); g.StartNewDayFromReport(); }
            Require(g.Farm.State.Cells[0].Mature && g.Farm.TryAct(0, FarmTool.Hoe, out _) && g.Inventory.GetCount("corn") == 1, "Real harvested corn enters bag");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 19 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Feed crafting timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Supply must not require a world model");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    var catalog = g.Configuration.Crafting; var recipe = catalog.FindItem(Feed);
                    Require(catalog.Recipes.Count == 7 && catalog.Validate(g.Configuration.Items) && recipe.IsSupply && !recipe.IsPlaceable && recipe.Yield == 10 && recipe.Ingredients[0].ItemId == "corn", "Seven AA recipes with exact feed recipe");
                    Require(catalog.Placeables().Count() == 3 && catalog.Placeables(true).Count() == 3 && catalog.Placeables().All(p => p.ItemId != Feed), "Supplies excluded from both placement catalogs");
                    recipe.IsFurniture = true; Require(!catalog.Validate(g.Configuration.Items), "Ambiguous furniture and supply rejected"); recipe.IsFurniture = false;
                    foreach (string invalid in new[] { "corn_seed", "sardine", "fried_egg", "egg", "missing" })
                    { recipe.Ingredients[0].ItemId = invalid; Require(!catalog.Validate(g.Configuration.Items), "Invalid supply ingredient type rejected"); } recipe.Ingredients[0].ItemId = "corn";
                    string output = recipe.OutputItemId; recipe.OutputItemId = "wood_fence"; Require(!catalog.Validate(g.Configuration.Items), "Supply cannot emit placeable items"); recipe.OutputItemId = output;
                    var outdoor = catalog.Recipes[0]; string old = outdoor.Ingredients[0].ItemId; outdoor.Ingredients[0].ItemId = "corn"; Require(!catalog.Validate(g.Configuration.Items), "Decorations retain material-only rules"); outdoor.Ingredients[0].ItemId = old;
                    recipe.PrefabAddress = "MissingFeedModel"; catalog.Recipes.Remove(recipe); catalog.Recipes.Insert(0, recipe);
                    Require(catalog.Validate(g.Configuration.Items), "Supply address is ignored, reordered recipe remains valid");
                    g.Configuration.Seasons.DaysPerSeason = 1; g.Configuration.Seasons.SpringRainChance = g.Configuration.Seasons.SummerRainChance = g.Configuration.Seasons.AutumnRainChance = g.Configuration.Seasons.WinterRainChance = 0;
                    string dir = Path.GetFullPath("Logs/FeedCraftingSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Placements.LastError == null && g.Furniture.LastError == null && g.Inventory.GetCount(Feed) == 0, "Startup does not load missing supply prefab or give free feed"); GrowCorn(g);
                    SessionState.SetString(Key + ".Tasks", Tasks(g)); SessionState.SetInt(Key + ".Stamina", g.Stamina.State.Current); AtBench(g); g.OpenCrafting(); Phase(2);
                }
                else if (phase == 2 && Bench != null)
                {
                    Require(Bench.PageText.text.Contains("农场装饰") && Bench.NameTexts[0].text == "木栅栏", "Reordered catalog still opens original decor category"); Bench.NextButton.onClick.Invoke(); Require(Bench.PageText.text.Contains("室内家具") && Bench.NameTexts[0].text == "木桌", "Original furniture category preserved"); Bench.NextButton.onClick.Invoke();
                    Require(Bench.Layer == UILayer.PopUp && Bench.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp) && Bench.PageText.text.Contains("养殖用品") && !Bench.NextButton.interactable
                        && Bench.PreviousButton.interactable && Bench.CraftButtons[0].interactable && Bench.NameTexts[0].text == "玉米饲料" && Bench.CostTexts[0].text.Contains("玉米") && Bench.ResultTexts[0].text.Contains("10")
                        && !Bench.CraftButtons[1].transform.parent.gameObject.activeSelf && !Bench.CraftButtons[2].transform.parent.gameObject.activeSelf, "Supply page shows exact material/output, sparse cards and bounds");
                    TextCheck(Bench); Capture(g, "FeedCraftingPanelPreview"); int day = g.Farm.State.Day; double minute = g.Time.Clock.Minute; g.AdvanceDay(); g.StartNewDayFromReport(); g.Time.Tick(10, true); Require(g.Farm.State.Day == day && g.Time.Clock.Minute == minute && !g.Tools.InputEnabled, "Workbench modal pauses farm/time");
                    var point = g.Players.Player.transform.position; g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.CraftRecipe(0); Require(g.Inventory.GetCount("corn") == 1 && g.Inventory.GetCount(Feed) == 0, "Craft reach guard"); g.Players.RestorePosition(point);
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () => { Require(g.Inventory.GetCount("corn") == 0 && g.Inventory.GetCount(Feed) == 10, "Ten feed committed before observers"); g.CraftRecipe(0); g.CloseCrafting(); g.ReturnToMainMenu(); g.AdvanceDay(); g.StartNewDayFromReport(); };
                    g.Inventory.State.Changed += observer; Bench.CraftButtons[0].onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.UI.HasCrafting && g.State == GameFlowState.Playing && g.Crafting.Feedback.Contains("保存失败") && g.Inventory.GetCount(Feed) == 10 && g.Economy.Gold == 100 && g.Stamina.State.Current == SessionState.GetInt(Key + ".Stamina", 0) && Tasks(g) == SessionState.GetString(Key + ".Tasks", ""), "Save failure retains craft and blocks nested actions without gold/stamina/tasks");
                    SaveDirectory(g, dir); Bench.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.Version == SaveData.CurrentVersion && saved.Slots.Sum(s => s.ItemId == Feed ? s.Quantity : 0) == 10 && saved.Slots.All(s => s.ItemId != "corn"), "Save retry does not craft again");
                    var good = g.Inventory.State.Slots.ToArray(); var full = Enumerable.Repeat(new InventorySlot("wood", 99), g.Inventory.State.Capacity).ToArray(); full[0] = new InventorySlot("corn", 99); full[1] = new InventorySlot(Feed, 90);
                    g.Inventory.State.Restore(full); g.CraftRecipe(0); Require(g.Inventory.GetCount("corn") == 99 && g.Inventory.GetCount(Feed) == 90 && g.Crafting.Feedback.Contains("未消耗"), "Nine stack spaces cannot fit ten output");
                    full[1] = new InventorySlot(Feed, 89); g.Inventory.State.Restore(full); g.CraftRecipe(0); Require(g.Inventory.GetCount("corn") == 98 && g.Inventory.GetCount(Feed) == 99, "Exactly ten stack spaces succeeds");
                    full[0] = new InventorySlot("corn", 1); full[1] = new InventorySlot(Feed, 99); g.Inventory.State.Restore(full); g.CraftRecipe(0); Require(g.Inventory.GetCount("corn") == 0 && g.Inventory.GetCount(Feed) == 109, "Last corn frees a slot for full feed stack plus ten");
                    g.Inventory.State.Restore(good); g.CraftRecipe(0); Require(g.Inventory.GetCount(Feed) == 10 && g.Crafting.Feedback.Contains("材料不足"), "Missing corn preserves original ten feed"); Bench.SaveButton.onClick.Invoke();
                    g.CloseCrafting(); g.CraftRecipe(0); Require(g.Inventory.GetCount(Feed) == 10, "Closed panel cannot craft"); g.OpenCrafting(); Phase(3);
                }
                else if (phase == 3 && Bench != null)
                { Require(Bench.PageText.text.Contains("农场装饰"), "Reopen resets first category"); Bench.NextButton.onClick.Invoke(); Bench.NextButton.onClick.Invoke(); Require(!Bench.CraftButtons[0].interactable && Bench.CraftButtons[0].GetComponentInChildren<TMPro.TMP_Text>().text == "材料不足", "Supply live stock and shortage state"); TextCheck(Bench); Bench.UpgradeButton.onClick.Invoke(); Phase(4); }
                else if (phase == 4 && UnityEngine.Object.FindObjectOfType<ToolUpgradeView>() != null)
                { var upgrade = UnityEngine.Object.FindObjectOfType<ToolUpgradeView>(); Require(upgrade.CraftingButton.GetComponentInChildren<TMPro.TMP_Text>().text == "工作台制作", "Generic workbench navigation"); upgrade.CraftingButton.onClick.Invoke(); Phase(5); }
                else if (phase == 5 && Bench != null) { Require(!g.UI.HasToolUpgrade, "Workbench views remain exclusive"); g.CloseCrafting(); g.ToggleInventory(); Phase(6); }
                else if (phase == 6 && g.UI.IsInventoryVisible)
                {
                    var bag = UnityEngine.Object.FindObjectOfType<InventoryView>(); int slot = Slot(g, Feed); bag.Slots[slot].SelectButton.onClick.Invoke();
                    Require(!bag.PlaceButton.gameObject.activeSelf && !bag.UseSeedButton.gameObject.activeSelf && !bag.UseFoodButton.gameObject.activeSelf && !g.CanPlaceItem(Feed), "Supply exposes no placement/seed/food bag action"); TextCheck(bag); Capture(g, "CraftedFeedInventoryPreview");
                    g.BeginPlacementFromBag(slot); Require(g.UI.IsInventoryVisible && !g.Placements.Active && !g.Furniture.Active && g.Inventory.GetCount(Feed) == 10, "Direct bag placement rejects supply before AA");
                    var outside = g.Placements.Begin(Feed); Require(!outside.MoveNext() && !g.Placements.Active && g.Placements.LastError != null, "Outdoor manager directly rejects supplies");
                    var inside = g.Furniture.Begin(Feed); Require(!inside.MoveNext() && !g.Furniture.Active, "Indoor manager directly rejects supplies");
                    Require(g.Saves.TryLoad(out var saved), "Read valid feed save"); string json = JsonUtility.ToJson(saved); saved.PlacedObjects = new[] { new SavedPlacedObject { Id = 1, ItemId = Feed, GridX = -6, GridZ = -7 } }; Write(g, JsonUtility.ToJson(saved)); Require(!g.Saves.TryLoad(out _), "Feed cannot be restored as a world decoration"); Write(g, json);
                    g.CloseInventory(); AtChest(g); g.OpenStorage(); Phase(7);
                }
                else if (phase == 7 && UnityEngine.Object.FindObjectOfType<StorageView>() != null)
                { g.TransferStorage(true, Slot(g, Feed), 10); Require(g.Storage.State.Contents.GetCount(Feed) == 10 && g.Inventory.GetCount(Feed) == 0 && g.Saves.TryLoad(out var saved) && saved.Storage.Any(s => s.ItemId == Feed && s.Quantity == 10), "Crafted feed storage and autosave"); g.CloseStorage(); g.ReturnToMainMenu(); Phase(8); }
                else if (phase == 8 && g.State == GameFlowState.MainMenu)
                { Require(g.Crafting.State == null && g.Placements.State == null && Bench == null, "MVC and placement lifecycle clear"); g.ContinueGame(); Phase(9); }
                else if (phase == 9 && g.State == GameFlowState.Playing)
                { Require(g.Storage.State.Contents.GetCount(Feed) == 10 && g.Inventory.GetCount(Feed) == 0 && g.Economy.Gold == 100 && g.Stamina.State.Current == SessionState.GetInt(Key + ".Stamina", 0), "v21 reload preserves supplies, inventory and resources"); AtChest(g); g.OpenStorage(); Phase(10); }
                else if (phase == 10 && UnityEngine.Object.FindObjectOfType<StorageView>() != null)
                {
                    int slot = Enumerable.Range(0, g.Storage.State.Contents.Capacity).First(i => g.Storage.State.Contents.Slots[i].ItemId == Feed); g.TransferStorage(false, slot, 10); g.CloseStorage();
                    g.Inventory.TryAdd("wood", 15); g.Inventory.TryAdd("stone", 5); g.Economy.Restore(1000); Require(g.Animals.Build() && g.Animals.BuyChicken() && g.Animals.BuyChicken() && g.SaveProgress(), "Coop and two birds for homemade supplies"); AtCoop(g); g.OpenCoop(); Phase(11);
                }
                else if (phase == 11 && Coop != null)
                { Coop.FeedButton.onClick.Invoke(); g.FeedChickens(); Require(g.Inventory.GetCount(Feed) == 8 && g.Animals.State.Unfed == 0 && g.Animals.State.Eggs == 0, "Homemade feed works once per bird through actual coop button"); g.CloseAnimalPanel(); g.ReturnToMainMenu(); Phase(12); }
                else if (phase == 12 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(13); }
                else if (phase == 13 && g.State == GameFlowState.Playing)
                { Require(g.Inventory.GetCount(Feed) == 8 && g.Animals.State.Unfed == 0 && g.Animals.State.Count == 2, "Reload keeps consumed feed and fed-day records"); g.AdvanceDay(); g.StartNewDayFromReport(); Require(g.Animals.State.Eggs == 2, "Homemade feed produces eggs next day"); AtCoop(g); g.OpenCoop(); Phase(14); }
                else if (phase == 14 && Coop != null)
                { Coop.CollectButton.onClick.Invoke(); Coop.FeedButton.onClick.Invoke(); Require(g.Inventory.GetCount("egg") == 2 && g.Inventory.GetCount(Feed) == 6 && g.Animals.State.Unfed == 0 && g.Saves.TryLoad(out var saved) && saved.Animals.Chickens.All(c => c.FedDay == saved.Day), "Collect and next-day homemade feeding save together"); g.CloseAnimalPanel(); g.ReturnToMainMenu(); Phase(15); }
                else if (phase == 15 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(16); }
                else if (phase == 16 && g.State == GameFlowState.Playing)
                { Require(g.Inventory.GetCount(Feed) == 6 && g.Inventory.GetCount("corn") == 0 && g.Inventory.GetCount("egg") == 2 && g.Storage.State.Contents.GetCount(Feed) == 0 && g.Animals.State.Unfed == 0 && g.Placements.State.Count == 0 && g.Furniture.State.Count == 0, "Complete farm/craft/storage/husbandry reload without duplicates"); g.ReturnToMainMenu(); Phase(17); }
                else if (phase == 17 && g.State == GameFlowState.MainMenu) { g.Configuration.Crafting.FindItem(Feed).PrefabAddress = null; g.EnterGame(); Phase(18); }
                else if (phase == 18 && g.State == GameFlowState.Playing)
                {
                    Require(g.Inventory.GetCount(Feed) == 0 && !g.Animals.State.Built && g.Farm.State.Day == 1, "New game supplies no free feed or coop");
                    File.WriteAllText("Logs/FeedCraftingVerification.txt", "PASS: packed AA seven recipes, supply typing and placement exclusion, no supply model required, reordered catalog/category actions, real grown corn -> ten feed, atomic capacity/stack/last-material/shortage/reentry/range/modal/save retry, Chinese MVC/navigation/reset, bag actions, invalid placement save rejection, storage/v21 reload, real two-bird feeding/eggs/readback, lifecycle and new game.");
                    Debug.Log("Feed crafting flow verification PASSED."); Phase(19); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
