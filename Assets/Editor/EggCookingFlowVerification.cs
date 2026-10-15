using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Inventory;
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
    public static class EggCookingFlowVerification
    {
        const string Key = "MagicIsland.EggCookingVerification", Npc = "island_steward", Grilled = "fried_egg", Soup = "egg_vegetable_soup";
        static EggCookingFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            var paths = new[] { "Assets/Config/CookingCatalog.asset", "Assets/Config/ItemCatalog.asset", "Assets/Art/Prefabs/UI/CookingPanel.prefab" };
            var snapshots = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(snapshots[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Complete recipes and panel survive repeated setup: " + paths[i]);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static CookingView Kitchen => UnityEngine.Object.FindObjectOfType<CookingView>();
        static GiftView Gift => UnityEngine.Object.FindObjectOfType<GiftView>();
        static void Place(GameManager g) => g.Players.RestorePosition(g.Cooking.Station.transform.position + new Vector3(0, .1f, -1.5f));
        static void AtNpc(GameManager g) => g.Players.RestorePosition(g.NPCs.Actors[Npc].transform.position + new Vector3(1.2f, .1f, -.8f));
        static int Slot(GameManager g, string id) => Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == id);
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void TextCheck(Component view) { foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Egg food text/glyphs: " + text.name + "=" + text.text); } }
        static void Capture(GameManager g, string name) { g.UI.RefreshViews(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void ProduceEggs(GameManager g)
        {
            Require(!g.Animals.State.Built && g.Inventory.GetCount("egg") == 0, "New game supplies no eggs or animals");
            g.Inventory.TryAdd("wood", 15); g.Inventory.TryAdd("stone", 5); g.Economy.Restore(1000);
            Require(g.Animals.Build() && g.Animals.BuyChicken() && g.Animals.BuyChicken(), "Real AA coop and two birds for cooking");
            for (int day = 0; day < 3; day++)
            { g.Inventory.TryAdd("animal_feed", 2); Require(g.Animals.Feed(), "Feed before egg production"); g.AdvanceDay(); g.StartNewDayFromReport(); }
            Require(g.Animals.State.Eggs == 6 && g.Animals.Collect() && g.Inventory.GetCount("egg") == 6 && g.Animals.State.Eggs == 0, "Six husbandry eggs collected before cooking");
            g.Economy.Restore(100);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 14 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Egg cooking timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Egg cooking configuration failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    var cooking = g.Configuration.Cooking; Require(cooking.Validate(g.Configuration.Items) && cooking.Recipes.Count == 7 && cooking.Recipes[5].Id == Grilled && cooking.Recipes[6].Id == Soup && cooking.Recipes[2].RequiredEventId == "steward_recipe", "Seven AA recipes retain legacy ordering and event lock");
                    string original = cooking.Recipes[5].IngredientId;
                    foreach (string invalid in new[] { "turnip_seed", "wood", "animal_feed", "roasted_corn" }) { cooking.Recipes[5].IngredientId = invalid; Require(!cooking.Validate(g.Configuration.Items), "Seeds/materials/food rejected as ingredients"); } cooking.Recipes[5].IngredientId = original;
                    var additional = cooking.Recipes[6].AdditionalIngredients[0]; string old = additional.ItemId;
                    cooking.Recipes[6].IngredientId = "turnip"; additional.ItemId = "egg"; Require(cooking.Validate(g.Configuration.Items), "Additional animal ingredient supported");
                    additional.ItemId = "animal_feed"; Require(!cooking.Validate(g.Configuration.Items), "Feed rejected as additional ingredient");
                    cooking.Recipes[6].IngredientId = "egg"; additional.ItemId = old;
                    string dir = Path.GetFullPath("Logs/EggCookingSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                { ProduceEggs(g); g.Inventory.TryAdd("turnip", 3); SessionState.SetString(Key + ".Tasks", JsonUtility.ToJson(new SaveData { Tasks = g.Tasks.State.Capture() })); Place(g); g.OpenCooking(); Phase(2); }
                else if (phase == 2 && Kitchen != null)
                {
                    Require(Kitchen.CookButtons.Length == 7 && Kitchen.RecipeScroll != null && Kitchen.RecipeScroll.vertical && !Kitchen.RecipeScroll.horizontal && Kitchen.RecipeScroll.verticalNormalizedPosition > .99f, "Scrollable seven-recipe MVC opens at top");
                    Require(!Kitchen.CookButtons[2].interactable && Kitchen.ResultTexts[2].text.Contains("朋友的食谱"), "Vegetable soup keeps event lock"); TextCheck(Kitchen); Capture(g, "SevenRecipePanelPreview");
                    Kitchen.RecipeScroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases(); g.UI.RefreshViews(); Require(Kitchen.CookButtons[5].interactable && Kitchen.CookButtons[6].interactable && Kitchen.IngredientTexts[6].text.Contains(g.Inventory.Catalog.Find("egg").DisplayName) && Kitchen.IngredientTexts[6].text.Contains(g.Inventory.Catalog.Find("turnip").DisplayName), "Egg cards show all ingredients and stocks");
                    TextCheck(Kitchen); Capture(g, "EggCookingPanelPreview");
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () => { Require(g.Inventory.GetCount("egg") == 5 && g.Inventory.GetCount(Grilled) == 1 && g.Stamina.State.Current == 100, "Atomic egg cooking before observers"); g.CookRecipe(5); g.ReturnToMainMenu(); g.CloseCooking(); };
                    g.Inventory.State.Changed += observer; Kitchen.CookButtons[5].onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.State == GameFlowState.Playing && g.UI.HasCooking && g.Cooking.Feedback.Contains("保存失败") && g.Inventory.GetCount(Grilled) == 1, "Save failure retains craft and blocks duplicate/close/return");
                    SaveDirectory(g, dir); Kitchen.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.Slots.Sum(s => s.ItemId == Grilled ? s.Quantity : 0) == 1, "Craft save retry once");
                    Kitchen.CookButtons[5].onClick.Invoke(); Kitchen.CookButtons[6].onClick.Invoke(); Kitchen.CookButtons[6].onClick.Invoke();
                    Require(g.Inventory.GetCount(Grilled) == 2 && g.Inventory.GetCount(Soup) == 2 && g.Inventory.GetCount("egg") == 2 && g.Inventory.GetCount("turnip") == 1, "Both egg recipes consume exact ingredients");
                    var bag = g.Inventory.State.Slots.ToArray(); var full = Enumerable.Repeat(new InventorySlot("wood", 99), g.Inventory.State.Capacity).ToArray(); full[0] = new InventorySlot("egg", 99); full[1] = new InventorySlot("turnip", 99);
                    g.Inventory.State.Restore(full); g.CookRecipe(6); Require(g.Inventory.GetCount("egg") == 99 && g.Inventory.GetCount("turnip") == 99 && g.Inventory.GetCount(Soup) == 0 && g.Cooking.Feedback.Contains("放不下"), "Full bag consumes no eggs or crops");
                    full[1] = new InventorySlot("wood", 99); g.Inventory.State.Restore(full); g.CookRecipe(6); Require(g.Inventory.GetCount("egg") == 99 && g.Cooking.Feedback.Contains("不足"), "Missing crop consumes no eggs");
                    g.Inventory.State.Restore(bag); Require(g.Stamina.State.Current == 100 && g.Economy.Gold == 100 && JsonUtility.ToJson(new SaveData { Tasks = g.Tasks.State.Capture() }) == SessionState.GetString(Key + ".Tasks", ""), "Cooking no stamina/gold/crop task side effects");
                    g.CloseCooking(); g.CookRecipe(5); Require(g.Inventory.GetCount(Grilled) == 2, "Closed panel cannot craft"); g.ToggleInventory(); Phase(3);
                }
                else if (phase == 3 && g.UI.IsInventoryVisible)
                {
                    var bag = UnityEngine.Object.FindObjectOfType<InventoryView>(); int grill = Slot(g, Grilled); bag.Slots[grill].SelectButton.onClick.Invoke();
                    Require(bag.UseFoodButton.gameObject.activeSelf && !bag.UseFoodButton.interactable && !bag.UseSeedButton.gameObject.activeSelf && bag.DetailText.text.Contains("25"), "New food uses existing MVC with full-stamina protection"); g.UseFood(grill); Require(g.Inventory.GetCount(Grilled) == 2, "Full stamina does not consume egg food");
                    g.Stamina.Restore(20); g.UI.RefreshViews(); bag.UseFoodButton.onClick.Invoke(); Require(g.Inventory.GetCount(Grilled) == 1 && g.Stamina.State.Current == 45, "Fried egg restores 25");
                    int soup = Slot(g, Soup); bag.Slots[soup].SelectButton.onClick.Invoke(); Require(bag.DetailText.text.Contains("45"), "Soup recovery preview"); TextCheck(bag); Capture(g, "EggFoodInventoryPreview"); g.Stamina.Restore(90); bag.UseFoodButton.onClick.Invoke();
                    Require(g.Inventory.GetCount(Soup) == 1 && g.Stamina.State.Current == 100 && g.Food.State.LastRecovery == 10 && g.Saves.TryLoad(out var saved) && saved.Stamina == 100, "Soup restores 45 clamped and autosaves");
                    g.CloseInventory(); AtNpc(g); g.OpenDialogue(Npc); Phase(4);
                }
                else if (phase == 4 && g.UI.HasDialogue)
                {
                    var talk = UnityEngine.Object.FindObjectOfType<DialogueView>(); if (talk == null) return;
                    int remaining = 32; while (g.UI.HasDialogue && remaining-- > 0) talk.NextButton.onClick.Invoke(); Require(g.NPCs.State.HasMet(Npc), "Complete meeting before gift"); g.OpenGift(Npc); Phase(5);
                }
                else if (phase == 5 && Gift != null)
                {
                    int grilled = Slot(g, Grilled); Gift.Slots[grilled].SelectButton.onClick.Invoke(); Require(Gift.PreviewText.text.Contains("+10") && !Gift.Slots[Slot(g, "egg")].SelectButton.interactable, "Prepared egg normal gift, raw egg not gift"); TextCheck(Gift); Capture(g, "EggFoodGiftPreview"); Gift.GiveButton.onClick.Invoke();
                    Require(g.Relationships.State.Points(Npc) == 10 && g.Inventory.GetCount(Grilled) == 0 && g.Stamina.State.Current == 100, "Egg food gift consumes one and adds 10");
                    g.GiveGift(Slot(g, Soup)); Require(g.Inventory.GetCount(Soup) == 1 && g.Relationships.State.Points(Npc) == 10, "Daily limit shared with new foods"); g.CloseGift(); g.AdvanceDay(); g.StartNewDayFromReport(); g.OpenGift(Npc); Phase(6);
                }
                else if (phase == 6 && Gift != null)
                {
                    Gift.Slots[Slot(g, Soup)].SelectButton.onClick.Invoke(); Gift.GiveButton.onClick.Invoke(); Require(g.Relationships.State.Points(Npc) == 20 && g.Relationships.State.LastGiftDay(Npc) == 5 && g.Inventory.GetCount(Soup) == 0, "Egg soup gift +10 next day");
                    g.CloseGift(); Place(g); g.OpenCooking(); Phase(7);
                }
                else if (phase == 7 && Kitchen != null)
                { Require(Kitchen.RecipeScroll.verticalNormalizedPosition > .99f, "Reopen resets scroll to first page"); Kitchen.CookButtons[5].onClick.Invoke(); Kitchen.CookButtons[6].onClick.Invoke(); Require(g.Inventory.GetCount(Grilled) == 1 && g.Inventory.GetCount(Soup) == 1, "Cook last ingredients for storage"); g.CloseCooking(); g.Players.RestorePosition(g.Storage.Chest.transform.position + Vector3.back * 1.2f); g.OpenStorage(); Phase(8); }
                else if (phase == 8 && g.UI.HasStorage && UnityEngine.Object.FindObjectOfType<StorageView>() != null)
                {
                    g.TransferStorage(true, Slot(g, Grilled), 1); g.TransferStorage(true, Slot(g, Soup), 1);
                    Require(g.Storage.State.Contents.GetCount(Grilled) == 1 && g.Storage.State.Contents.GetCount(Soup) == 1 && g.Saves.TryLoad(out var saved) && saved.Storage.Any(s => s.ItemId == Soup), "Egg foods stored and saved"); g.CloseStorage(); g.ReturnToMainMenu(); Phase(9);
                }
                else if (phase == 9 && g.State == GameFlowState.MainMenu) { Require(g.Cooking.State == null && g.Food.State == null && !g.UI.HasCooking, "Cooking MVC cleanup"); g.ContinueGame(); Phase(10); }
                else if (phase == 10 && g.State == GameFlowState.Playing)
                { Require(g.Storage.State.Contents.GetCount(Grilled) == 1 && g.Storage.State.Contents.GetCount(Soup) == 1 && g.Relationships.State.Points(Npc) == 20 && g.Relationships.State.LastGiftDay(Npc) == 5 && g.Stamina.State.Current == 100, "Reload preserves egg food, stamina and gift records"); Place(g); g.OpenCooking(); Phase(11); }
                else if (phase == 11 && Kitchen != null)
                {
                    Require(g.Food.State.Recovery(Grilled) == 25 && g.Food.State.Recovery(Soup) == 45 && g.Cooking.Check(5) == CookingResult.InsufficientIngredients && g.Cooking.Check(6) == CookingResult.InsufficientIngredients && g.Cooking.Check(2) == CookingResult.Locked, "Reload restores default egg recipes and legacy lock"); TextCheck(Kitchen); g.CloseCooking(); g.ReturnToMainMenu(); Phase(12);
                }
                else if (phase == 12 && g.State == GameFlowState.MainMenu) { g.EnterGame(); Phase(13); }
                else if (phase == 13 && g.State == GameFlowState.Playing)
                {
                    Require(g.Inventory.GetCount(Grilled) == 0 && g.Storage.State.Contents.GetCount(Soup) == 0 && g.Relationships.State.Points(Npc) == 0, "New game resets old egg food and gifts");
                    File.WriteAllText("Logs/EggCookingFlowVerification.txt", "PASS: seven AA recipes and animal/crop validation, scrollable MVC/Chinese layout, exact atomic cooking and reentry/save failure/retry, no loss on missing ingredients/full bag, 25/45 food and clamp, normal +10 gifts/daily limit/raw exclusion, storage/reload/legacy event lock/new game/cleanup.\n");
                    Debug.Log("Egg cooking flow verification PASSED."); Phase(14); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
