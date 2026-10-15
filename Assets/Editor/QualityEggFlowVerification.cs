using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MagicIsland.Animals;
using MagicIsland.Inventory;
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
    public static class QualityEggFlowVerification
    {
        const string Key = "MagicIsland.QualityEggVerification";
        static QualityEggFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            string[] paths = { "Assets/Config/AnimalConfig.asset", "Assets/Config/ItemCatalog.asset", "Assets/Art/Prefabs/UI/CoopPanel.prefab", "Assets/Art/Prefabs/UI/AnimalShopPanel.prefab" };
            var bytes = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Complete quality resources preserved on repeat setup");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int p) => SessionState.SetInt(Key + ".Phase", p);
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static CoopView Coop => UnityEngine.Object.FindObjectOfType<CoopView>();
        static AnimalShopView Shop => UnityEngine.Object.FindObjectOfType<AnimalShopView>();
        static CookingView Kitchen => UnityEngine.Object.FindObjectOfType<CookingView>();
        static OrderView Board => UnityEngine.Object.FindObjectOfType<OrderView>();
        static void Office(GameManager g) => g.Players.RestorePosition(g.Orders.Board.transform.position + new Vector3(0, .1f, -1.5f));
        static void Market(GameManager g) => g.Players.RestorePosition(g.Shop.Counter.position + new Vector3(0, .1f, -1.5f));
        static void Care(GameManager g) => g.Players.RestorePosition(g.Animals.Coop.transform.position + new Vector3(0, .1f, -1.7f));
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void TextCheck(Component root)
        { foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) { t.ForceMeshUpdate(true); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Quality TMP: " + t.name + "=" + t.text); } }
        static void Capture(GameManager g, string name)
        { g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Write(GameManager g, string json) { File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 15 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Quality timeout phase " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Quality boot failure");
                string dir = SessionState.GetString(Key + ".Dir", "");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                {
                    var c = g.Configuration.Animals; Require(c.Validate(g.Configuration.Items, g.Configuration.Config) && c.QualityEggPrice == 40 && c.QualityAffinity == 70 && c.QualityChance == 25 && g.Configuration.Items.Find(c.QualityEggItemId).Type == ItemType.AnimalProduct, "AA quality item and rules");
                    int chance = c.QualityChance; c.QualityChance = 101; Require(!c.Validate(g.Configuration.Items, g.Configuration.Config), "Reject invalid AA chance"); c.QualityChance = chance;
                    dir = Path.GetFullPath("Logs/QualityEggSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (p == 1 && g.State == GameFlowState.Playing)
                { Require(g.Animals.State.TotalEggs == 0, "New game no free eggs"); g.Inventory.TryAdd("wood", 15); g.Inventory.TryAdd("stone", 5); g.Inventory.TryAdd("animal_feed", 2); g.Economy.Restore(1000); Office(g); g.OpenCoop(true); Phase(2); }
                else if (p == 2 && Coop != null)
                { Coop.BuildButton.onClick.Invoke(); Require(g.Animals.State.Built, "Build actual AA coop"); g.CloseAnimalPanel(); Market(g); g.OpenAnimalShop(); Phase(3); }
                else if (p == 3 && Shop != null)
                {
                    Shop.ChickenButton.onClick.Invoke(); Shop.ChickenButton.onClick.Invoke(); Require(g.Animals.State.Count == 2 && !Shop.QualitySellButton.interactable, "Separate quality button empty and buy two birds");
                    var saved = g.Animals.State.Capture(); saved.Chickens[0].Affinity = 68;
                    for (int seed = 1; seed < 1000; seed++)
                    {
                        saved.QualitySeed = seed; var probe = new AnimalState(g.Animals.State.Definition); probe.Restore(saved); var bag = new InventoryState(1, g.Configuration.Items.Definitions()); bag.TryAdd("animal_feed", 2); probe.Feed(bag); probe.NextDay(2);
                        if (probe.QualityEggs == 1) break;
                    }
                    g.Animals.State.Restore(saved); g.CloseAnimalPanel(); Care(g); g.OpenCoop(); Phase(4);
                }
                else if (p == 4 && Coop != null)
                {
                    Coop.FeedButton.onClick.Invoke(); Require(g.Animals.State.Chicken(1).Affinity == 70 && g.Animals.State.Chicken(2).Affinity == 2 && g.Animals.State.TotalEggs == 0, "Feed reaches inclusive threshold but produces only next morning"); TextCheck(Coop);
                    g.CloseAnimalPanel(); g.AdvanceDay(); g.StartNewDayFromReport(); Require(g.Farm.State.Day == 2 && g.Animals.State.Eggs == 1 && g.Animals.State.QualityEggs == 1 && g.Animals.Coop.EggsMarker.activeSelf, "One egg each fixed kind at normal 25 percent chance");
                    Require(g.Saves.TryLoad(out var saved) && saved.Version == SaveData.CurrentVersion && saved.Animals.QualityEggs == 1 && saved.Animals.QualitySeed == g.Animals.State.QualitySeed, "Settlement persisted before collect"); g.ReturnToMainMenu(); Phase(5);
                }
                else if (p == 5 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(6); }
                else if (p == 6 && g.State == GameFlowState.Playing)
                { Require(g.Animals.State.Eggs == 1 && g.Animals.State.QualityEggs == 1, "Read exact settled result without reroll"); Care(g); g.OpenCoop(); Phase(7); }
                else if (p == 7 && Coop != null)
                {
                    Require(Coop.StatusText.text.Contains("普通蛋 1") && Coop.StatusText.text.Contains("优质蛋 1"), "Separate pending labels"); TextCheck(Coop); Capture(g, "QualityEggCoopPreview");
                    var original = g.Inventory.State.Slots.ToArray(); var full = Enumerable.Repeat(new InventorySlot("wood", 99), g.Inventory.State.Capacity).ToArray(); full[0] = default;
                    g.Inventory.State.Restore(full); g.UI.RefreshViews(); Require(!Coop.CollectButton.interactable, "Two egg kinds cannot share one remaining empty slot"); g.CollectEggs(); Require(g.Animals.State.TotalEggs == 2 && g.Inventory.GetCount("egg") == 0 && g.Inventory.GetCount("quality_egg") == 0 && g.Animals.Coop.EggsMarker.activeSelf, "Blocked mixed collect preserves both and world marker");
                    g.Inventory.State.Restore(original); string blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () => { Require(g.Animals.State.TotalEggs == 0 && g.Inventory.GetCount("egg") == 1 && g.Inventory.GetCount("quality_egg") == 1 && !g.Animals.Coop.EggsMarker.activeSelf, "World and both inventory kinds commit before observer"); g.CollectEggs(); g.SellQualityEgg(); g.CloseAnimalPanel(); g.ReturnToMainMenu(); g.AdvanceDay(); g.StartNewDayFromReport(); };
                    g.Inventory.State.Changed += observer; Coop.CollectButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.State == GameFlowState.Playing && g.UI.HasCoop && g.Farm.State.Day == 2 && g.Animals.Feedback.Contains("保存失败"), "Collect save failure and reentry retain result");
                    SaveDirectory(g, dir); Coop.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.Animals.QualityEggs == 0 && saved.Slots.Sum(s => s.ItemId == "quality_egg" ? s.Quantity : 0) == 1, "Retry preserves single collect");
                    g.CloseAnimalPanel(); Market(g); g.OpenAnimalShop(); Phase(8);
                }
                else if (p == 8 && Shop != null)
                {
                    Require(Shop.QualitySellButton.interactable && Shop.QualityEggPriceText.text.Contains("40") && Shop.EggPriceText.text.Contains("25") && Shop.StockText.text.Contains("优质蛋 1"), "Independent 25 and 40 offers");
                    foreach (var button in new[] { Shop.ChickenButton, Shop.FeedButton, Shop.SellButton, Shop.QualitySellButton })
                        Require(button.GetComponentInChildren<TMPro.TMP_Text>().rectTransform.rect.width <= button.GetComponent<RectTransform>().rect.width, "Shop button labels remain inside backgrounds");
                    TextCheck(Shop); Capture(g, "QualityEggShopPreview");
                    var position = g.Players.Player.transform.position; g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.SellQualityEgg(); Require(g.Inventory.GetCount("quality_egg") == 1, "Quality sale range guard"); g.Players.RestorePosition(position);
                    long gold = g.Economy.Gold; Action observer = () => { Require(g.Economy.Gold == gold + 40 && g.Inventory.GetCount("quality_egg") == 0 && g.Inventory.GetCount("egg") == 1, "Premium sale atomic"); g.SellQualityEgg(); g.SellEgg(); g.CloseAnimalPanel(); g.ReturnToMainMenu(); };
                    g.Inventory.State.Changed += observer; Shop.QualitySellButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.State == GameFlowState.Playing && g.UI.HasAnimalShop && !Shop.QualitySellButton.interactable, "Sale blocks duplicate and close"); Shop.SellButton.onClick.Invoke(); Require(g.Economy.Gold == gold + 65 && g.Inventory.GetCount("egg") == 0, "Normal sale unchanged");
                    g.CloseAnimalPanel(); g.Inventory.TryAdd("egg", 1); g.Inventory.TryAdd("quality_egg", 3); g.Inventory.TryAdd("turnip", 1); g.Players.RestorePosition(g.Cooking.Station.transform.position + new Vector3(0, .1f, -1.5f)); g.OpenCooking(); Phase(9);
                }
                else if (p == 9 && Kitchen != null)
                {
                    TextCheck(Kitchen); Kitchen.CookButtons[5].onClick.Invoke(); Require(g.Inventory.GetCount("egg") == 0 && g.Inventory.GetCount("quality_egg") == 3 && g.Inventory.GetCount("fried_egg") == 1, "AA recipe ordinary first");
                    Kitchen.CookButtons[5].onClick.Invoke(); Kitchen.CookButtons[6].onClick.Invoke(); Require(g.Inventory.GetCount("quality_egg") == 1 && g.Inventory.GetCount("fried_egg") == 2 && g.Inventory.GetCount("egg_vegetable_soup") == 1 && g.Inventory.GetCount("turnip") == 0, "Both egg foods accept quality with unchanged outputs");
                    Require(g.Food.State.Recovery("quality_egg") == 0, "Raw premium egg cannot be eaten"); g.CloseCooking();
                    for (int seed = 1; seed < 1000; seed++) { var order = new OrderState(g.Configuration.Orders.Definitions(g.Farm.Crops), g.Configuration.Seasons.DaysPerSeason, seed, 2, true); if (order.ItemId == "egg") { g.Orders.State.Restore(order.Capture(), 2); break; } }
                    Require(g.Orders.State.ItemId == "egg" && g.Orders.State.Quantity == 2, "Existing egg offer unchanged"); g.Inventory.TryAdd("egg", 1); g.Inventory.TryAdd("quality_egg", 1); Office(g); g.OpenOrder(); Phase(10);
                }
                else if (p == 10 && Board != null)
                {
                    Require(Board.DeliverButton.interactable && Board.StockText.text.Contains("3 / 2"), "Mixed stock shown by order MVC"); TextCheck(Board); Capture(g, "QualityEggOrderPreview");
                    long gold = g.Economy.Gold; Board.DeliverButton.onClick.Invoke(); Require(g.Orders.State.Fulfilled && g.Economy.Gold == gold + 70 && g.Inventory.GetCount("egg") == 0 && g.Inventory.GetCount("quality_egg") == 1, "Existing order reward and normal-first mixed delivery");
                    g.CloseOrder(); g.ReturnToMainMenu(); Phase(11);
                }
                else if (p == 11 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var saved) && saved.Order.Fulfilled && saved.Animals.Chickens[0].Affinity == 70 && saved.Slots.Sum(s => s.ItemId == "quality_egg" ? s.Quantity : 0) == 1, "Save quality inventory with paid order and affinity");
                    string good = JsonUtility.ToJson(saved); saved.Animals.QualityEggs = -1; Write(g, JsonUtility.ToJson(saved)); Require(!g.Saves.TryLoad(out _), "Reject negative quality count");
                    saved = JsonUtility.FromJson<SaveData>(good); saved.Animals.QualitySeed = 0; Write(g, JsonUtility.ToJson(saved)); Require(!g.Saves.TryLoad(out _), "Reject invalid quality seed");
                    saved = JsonUtility.FromJson<SaveData>(good); saved.Version = 22; saved.Animals.Eggs = 3; saved.Animals.Chickens[0].LastPetDay = 2;
                    foreach (var s in saved.Slots) if (s.ItemId == "quality_egg") { s.ItemId = "egg"; }
                    Write(g, Regex.Replace(JsonUtility.ToJson(saved, true), ",\\s*\"(QualityEggs|QualitySeed)\":\\s*-?\\d+", ""));
                    Require(g.Saves.TryLoad(out var old) && old.Version == SaveData.CurrentVersion && old.Animals.Eggs == 3 && old.Animals.QualityEggs == 0 && old.Animals.QualitySeed == 1 && old.Animals.Chickens[0].Affinity == 70 && old.Animals.Chickens[0].LastPetDay == 2 && old.Order.Fulfilled, "Actual v22 missing-field JSON preserves ordinary eggs, affinity, daily pet and paid order"); g.ContinueGame(); Phase(12);
                }
                else if (p == 12 && g.State == GameFlowState.Playing)
                { Require(g.Animals.State.Eggs == 3 && g.Animals.State.QualityEggs == 0 && g.Animals.State.Chicken(1).LastPetDay == 2 && g.Orders.State.Fulfilled, "Legacy world restores unchanged"); g.ReturnToMainMenu(); Phase(13); }
                else if (p == 13 && g.State == GameFlowState.MainMenu)
                { Require(g.Animals.State == null && !g.UI.HasCoop && !g.UI.HasAnimalShop, "Animal session and quality UI cleanup"); g.EnterGame(); Phase(14); }
                else if (p == 14 && g.State == GameFlowState.Playing)
                { Require(g.Animals.State.TotalEggs == 0 && !g.Animals.State.Built && g.Inventory.GetCount("quality_egg") == 0, "New game resets all quality state"); File.WriteAllText("Logs/QualityEggVerification.txt", "PASS: packed AA config/item/MVC, threshold feeding and deterministic morning quality, exact reload, two-kind atomic collect with capacity competition/world marker/reentry/save retry, independent 25/40 sales/range, Chinese layout, normal-first egg cooking and order delivery, raw item exclusion, real missing-field v22 migration/paid order/affinity preservation, corrupt-count/seed rejection, cleanup and reset."); Debug.Log("Quality egg flow verification PASSED."); Phase(15); EditorApplication.isPlaying = false; }
            }
            catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
    }
}
