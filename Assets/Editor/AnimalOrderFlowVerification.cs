using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MagicIsland.Managers;
using MagicIsland.Orders;
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
    public static class AnimalOrderFlowVerification
    {
        const string Key = "MagicIsland.AnimalOrderVerification";
        static AnimalOrderFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            var paths = new[] { "Assets/Config/OrderCatalog.asset", "Assets/Art/Prefabs/UI/OrderPanel.prefab" };
            var snapshots = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(snapshots[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Repeated setup preserves complete catalog and panel");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static OrderView Board => UnityEngine.Object.FindObjectOfType<OrderView>();
        static void AtBoard(GameManager g) => g.Players.RestorePosition(g.Orders.Board.transform.position + new Vector3(-1, .1f, -1.2f));
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void Write(GameManager g, string json) { File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static string Order(GameManager g) => JsonUtility.ToJson(g.Orders.State.Capture());
        static string Tasks(GameManager g) => JsonUtility.ToJson(new SaveData { Tasks = g.Tasks.State.Capture() });
        static void TextCheck(Component view)
        { foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Order text and Chinese glyphs: " + text.name + "=" + text.text); } }
        static void Capture(GameManager g, string name)
        { g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Product(GameManager g, string item, int quantity, long reward)
        {
            Require(g.Orders.State.ItemId == item && g.Orders.State.Quantity == quantity && g.Orders.State.Reward == reward && g.Orders.State.CoopOffersUnlocked && !g.Orders.State.Fulfilled, "Today's exact product order");
            Require(Board.Layer == UILayer.PopUp && Board.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp)
                && Board.RequestText.text.Contains(g.Inventory.Catalog.Find(item).DisplayName) && Board.DeliverButton.interactable
                && Board.DeliverButton.GetComponentInChildren<TMPro.TMP_Text>().text == "交付全部物品", "Generic item MVC, icon and button");
            TextCheck(Board);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 17 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Animal order timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Animal order configuration failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    var catalog = g.Configuration.Orders; Require(catalog.Offers.Count == 6 && catalog.Validate(g.Configuration.Crops, g.Configuration.Items), "Six AA offers validate before new game");
                    var egg = catalog.Offers.Single(o => o.Id == "egg_daily"); string original = egg.ItemId;
                    foreach (string invalid in new[] { "turnip_seed", "animal_feed", "sardine", "missing" })
                    { egg.ItemId = invalid; Require(!catalog.Validate(g.Configuration.Crops, g.Configuration.Items), "Non-delivery item type rejected"); } egg.ItemId = original;
                    egg.CropId = "turnip"; Require(!catalog.Validate(g.Configuration.Crops, g.Configuration.Items), "Ambiguous crop and item rejected"); egg.CropId = null;
                    foreach (var crop in catalog.Offers.Take(3)) crop.RequiresCoop = true;
                    Require(!catalog.Validate(g.Configuration.Crops, g.Configuration.Items), "Each season needs a locked-pool offer"); foreach (var crop in catalog.Offers.Take(3)) crop.RequiresCoop = false;
                    var definitions = catalog.Definitions(g.Configuration.Crops).ToArray();
                    int seed = Enumerable.Range(1, 10000).First(s => new OrderState(definitions, 28, s, 4, true).ItemId == "egg"
                        && new OrderState(definitions, 28, s, 5, true).ItemId == "fried_egg" && new OrderState(definitions, 28, s, 6, true).ItemId == "egg_vegetable_soup");
                    SessionState.SetInt(Key + ".Seed", seed);
                    string dir = Path.GetFullPath("Logs/AnimalOrderSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    var definitions = g.Configuration.Orders.Definitions(g.Configuration.Crops).ToArray();
                    g.Orders.State.Restore(new OrderState(definitions, 28, SessionState.GetInt(Key + ".Seed", 1), 1).Capture(), 1);
                    Require(!g.Orders.State.CoopOffersUnlocked && !definitions.Single(d => d.Id == g.Orders.State.Capture().DefinitionId).RequiresCoop, "No animal order before building");
                    SessionState.SetString(Key + ".Original", Order(g)); g.Inventory.TryAdd("wood", 15); g.Inventory.TryAdd("stone", 5); g.Inventory.TryAdd("animal_feed", 2); g.Economy.Restore(1000); AtBoard(g); g.OpenOrder(); Phase(2);
                }
                else if (phase == 2 && Board != null) { Board.CoopButton.onClick.Invoke(); Phase(3); }
                else if (phase == 3 && UnityEngine.Object.FindObjectOfType<CoopView>() != null)
                {
                    var coop = UnityEngine.Object.FindObjectOfType<CoopView>(); coop.BuildButton.onClick.Invoke();
                    Require(g.Animals.State.Built && Order(g) == SessionState.GetString(Key + ".Original", ""), "Building through board preserves today's original order");
                    g.CloseAnimalPanel(); Require(g.Animals.BuyChicken() && g.Animals.BuyChicken() && g.Animals.Feed() && g.SaveProgress(), "Two fed birds saved with unchanged order"); g.ReturnToMainMenu(); Phase(4);
                }
                else if (phase == 4 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var saved), "Read pre-upgrade snapshot"); saved.Version = 20;
                    string legacy = Regex.Replace(JsonUtility.ToJson(saved, true), ",\\s*\"CoopOffersUnlocked\":\\s*(true|false)", "");
                    Require(!legacy.Contains("CoopOffersUnlocked"), "Actual v20 fixture omits new field"); Write(g, legacy);
                    Require(g.Saves.TryLoad(out var migrated) && migrated.Version == SaveData.CurrentVersion && !migrated.Order.CoopOffersUnlocked && migrated.Animals.Built
                        && migrated.Animals.Chickens.All(c => c.FedDay == 1) && JsonUtility.ToJson(migrated.Order) == SessionState.GetString(Key + ".Original", ""), "v20 migration preserves exact order and fed coop");
                    g.ContinueGame(); Phase(5);
                }
                else if (phase == 5 && g.State == GameFlowState.Playing)
                {
                    Require(Order(g) == SessionState.GetString(Key + ".Original", "") && g.Economy.Gold == 700 && g.Animals.State.Unfed == 0, "Reload built coop retains today's locked pool and resources");
                    g.AdvanceDay(); g.StartNewDayFromReport(); var expected = new OrderState(g.Configuration.Orders.Definitions(g.Configuration.Crops), 28, SessionState.GetInt(Key + ".Seed", 1), 2, true);
                    Require(g.Orders.State.CoopOffersUnlocked && Order(g) == JsonUtility.ToJson(expected.Capture()) && g.Animals.State.Eggs == 2 && g.Animals.Collect(), "Next day unlock and real egg production");
                    g.Inventory.TryAdd("animal_feed", 2); Require(g.Animals.Feed(), "Feed again for food order ingredients"); g.AdvanceDay(); g.StartNewDayFromReport(); Require(g.Animals.Collect() && g.Inventory.GetCount("egg") == 4, "Four real eggs collected");
                    g.Inventory.TryAdd("turnip", 1); g.Players.RestorePosition(g.Cooking.Station.transform.position + new Vector3(0, .1f, -1.5f)); g.OpenCooking(); Phase(6);
                }
                else if (phase == 6 && UnityEngine.Object.FindObjectOfType<CookingView>() != null)
                {
                    var kitchen = UnityEngine.Object.FindObjectOfType<CookingView>(); kitchen.CookButtons[5].onClick.Invoke(); kitchen.CookButtons[6].onClick.Invoke();
                    Require(g.Inventory.GetCount("fried_egg") == 1 && g.Inventory.GetCount("egg_vegetable_soup") == 1 && g.Inventory.GetCount("egg") == 2, "Actual cooking supplies exact three product orders");
                    g.CloseCooking(); g.AdvanceDay(); g.StartNewDayFromReport(); AtBoard(g); g.OpenOrder(); Phase(7);
                }
                else if (phase == 7 && Board != null)
                {
                    Product(g, "egg", 2, 70); Capture(g, "EggOrderPanelPreview"); var stock = g.Inventory.State.Slots.ToArray(); string tasks = Tasks(g);
                    var point = g.Players.Player.transform.position; g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.DeliverOrder(); Require(g.Inventory.GetCount("egg") == 2 && !g.Orders.State.Fulfilled, "Delivery reach guard"); g.Players.RestorePosition(point);
                    g.Inventory.State.TryRemove("egg", 1); g.DeliverOrder(); Require(g.Inventory.GetCount("egg") == 1 && g.Inventory.GetCount("fried_egg") == 1 && g.Economy.Gold == 700 && !g.Orders.State.Fulfilled, "Insufficient eggs retain all products and gold"); g.Inventory.State.Restore(stock);
                    g.Economy.Restore(long.MaxValue); g.DeliverOrder(); Require(g.Inventory.GetCount("egg") == 2 && !g.Orders.State.Fulfilled, "Overflow does not consume eggs"); g.Economy.Restore(700);
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () => { Require(g.Orders.State.Fulfilled && g.Economy.Gold == 770 && g.Inventory.GetCount("egg") == 0, "Observers see complete delivery"); g.DeliverOrder(); g.CloseOrder(); g.ReturnToMainMenu(); g.AdvanceDay(); g.StartNewDayFromReport(); };
                    g.Inventory.State.Changed += observer; Board.DeliverButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.State == GameFlowState.Playing && g.UI.HasOrder && g.Farm.State.Day == 4 && g.Orders.Feedback.Contains("保存失败") && Tasks(g) == tasks, "Save failure retains result, blocks reentry and does not count crop tasks");
                    g.DeliverOrder(); Require(g.Economy.Gold == 770, "Failed save cannot pay twice"); SaveDirectory(g, dir);
                    Require(g.SaveProgress() && g.Saves.TryLoad(out var saved) && saved.Order.CoopOffersUnlocked && saved.Order.Fulfilled && saved.Gold == 770 && saved.Version == SaveData.CurrentVersion, "Retry saves completed egg order with exact pool");
                    g.UI.RefreshViews(); Require(!Board.DeliverButton.interactable, "Fulfilled MVC cannot deliver again"); SessionState.SetString(Key + ".Delivered", Order(g)); g.CloseOrder(); g.ReturnToMainMenu(); Phase(8);
                }
                else if (phase == 8 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Orders.State == null && Board == null && g.Animals.State == null, "Managers and AA panel clear on return"); Require(g.Saves.TryLoad(out var saved), "Read completed product order"); string good = JsonUtility.ToJson(saved);
                    saved.Order.CoopOffersUnlocked = false; Write(g, JsonUtility.ToJson(saved)); g.ContinueGame(); Require(g.State == GameFlowState.MainMenu && !g.Saves.HasSave, "Animal order with false pool rejected before loading world");
                    Write(g, good); g.ContinueGame(); Phase(9);
                }
                else if (phase == 9 && g.State == GameFlowState.Playing)
                { Require(Order(g) == SessionState.GetString(Key + ".Delivered", "") && g.Economy.Gold == 770 && g.Animals.State.Built, "Paid egg order reloads exactly"); AtBoard(g); g.OpenOrder(); Phase(10); }
                else if (phase == 10 && Board != null)
                {
                    g.DeliverOrder(); g.AdvanceDay(); g.StartNewDayFromReport(); Require(g.Economy.Gold == 770 && g.Farm.State.Day == 4, "Paid reload cannot deliver or advance while modal");
                    g.CloseOrder(); g.AdvanceDay(); g.StartNewDayFromReport(); AtBoard(g); g.OpenOrder(); Phase(11);
                }
                else if (phase == 11 && Board != null)
                {
                    Product(g, "fried_egg", 1, 50); Capture(g, "FriedEggOrderPanelPreview"); string tasks = Tasks(g); g.Stamina.Restore(80); Board.DeliverButton.onClick.Invoke();
                    Require(g.Orders.State.Fulfilled && g.Economy.Gold == 820 && g.Inventory.GetCount("fried_egg") == 0 && g.Stamina.State.Current == 80 && Tasks(g) == tasks, "Prepared food delivery pays exactly without eating or crop tasks");
                    g.CloseOrder(); g.AdvanceDay(); g.StartNewDayFromReport(); AtBoard(g); g.OpenOrder(); Phase(12);
                }
                else if (phase == 12 && Board != null)
                {
                    Product(g, "egg_vegetable_soup", 1, 90); Capture(g, "EggSoupOrderPanelPreview"); string tasks = Tasks(g); Board.DeliverButton.onClick.Invoke(); g.DeliverOrder();
                    Require(g.Orders.State.Fulfilled && g.Economy.Gold == 910 && g.Inventory.GetCount("egg_vegetable_soup") == 0 && g.Stamina.State.Current == 100 && Tasks(g) == tasks
                        && g.Saves.TryLoad(out var saved) && saved.Order.Fulfilled && saved.Gold == 910, "Soup delivers once and saves full state");
                    g.CloseOrder(); g.ReturnToMainMenu(); Phase(13);
                }
                else if (phase == 13 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var saved), "Read final snapshot"); string good = JsonUtility.ToJson(saved); saved.Animals = new MagicIsland.Animals.SavedAnimals { Day = saved.Day };
                    Write(g, JsonUtility.ToJson(saved)); g.ContinueGame(); Require(g.State == GameFlowState.MainMenu && !g.Saves.HasSave, "Unlocked order pool without coop rejected"); Write(g, good); g.ContinueGame(); Phase(14);
                }
                else if (phase == 14 && g.State == GameFlowState.Playing)
                { Require(g.Economy.Gold == 910 && g.Farm.State.Day == 6 && g.Orders.State.Fulfilled && g.Orders.State.CoopOffersUnlocked && g.Animals.State.Count == 2 && g.Animals.State.Eggs == 0 && g.Inventory.GetCount("egg") == 0 && g.Inventory.GetCount("fried_egg") == 0 && g.Inventory.GetCount("egg_vegetable_soup") == 0, "Reload retains complete husbandry/cooking/delivery chain without duplicates"); g.ReturnToMainMenu(); Phase(15); }
                else if (phase == 15 && g.State == GameFlowState.MainMenu) { g.EnterGame(); Phase(16); }
                else if (phase == 16 && g.State == GameFlowState.Playing)
                {
                    Require(!g.Animals.State.Built && !g.Orders.State.CoopOffersUnlocked && g.Economy.Gold == 100 && g.Inventory.GetCount("egg") == 0 && g.Inventory.GetCount("fried_egg") == 0 && g.Farm.State.Day == 1, "New game resets unlock and supplies no products");
                    File.WriteAllText("Logs/AnimalOrderVerification.txt", "PASS: six packed AA offers, generic MVC/Chinese layout, real coop construction/feed/eggs/cooking, unlock only next day, exact v20 migration, shortage/overflow/range/modal/atomic/reentry/save retry, 70/50/90 rewards, deterministic unpaid and paid reload, invalid pool/coop records rejected, no crop-task or eating side effects, new game and lifecycle cleanup.");
                    Debug.Log("Animal order flow verification PASSED."); Phase(17); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
