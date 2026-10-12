using System;
using System.IO;
using System.Linq;
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
    public static class StoryEventFlowVerification
    {
        const string Key = "MagicIsland.StoryVerification", Id = "island_steward", History = "steward_history", Recipe = "steward_recipe";
        static StoryEventFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static DialogueView Talk => UnityEngine.Object.FindObjectOfType<DialogueView>();
        static CookingView Kitchen => UnityEngine.Object.FindObjectOfType<CookingView>();
        static void Place(GameManager g) => g.Players.RestorePosition(g.NPCs.Actors[Id].transform.position + new Vector3(1.2f, .1f, -.8f));
        static void AtKitchen(GameManager g) => g.Players.RestorePosition(g.Cooking.Station.transform.position + new Vector3(0, .1f, -1.3f));
        static void Points(GameManager g, int points) => g.Relationships.State.Restore(new[] { new SavedRelationship { Id = Id, Points = points, LastGiftDay = g.Farm.State.Day } });
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void Finish(GameManager g) { int remaining = 32; while (g.UI.HasDialogue && remaining-- > 0) Talk.NextButton.onClick.Invoke(); Require(!g.UI.HasDialogue, "Dialogue should finish"); }
        static void TextCheck(Component view) { foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Story text/glyphs: " + text.name + "=" + text.text); } }
        static void Capture(GameManager g, string name) { GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Write(GameManager g, SaveData data) { string json = JsonUtility.ToJson(data); File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 20 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Story timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Story configuration failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Stories.Validate(g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Cooking), "StoryCatalog packed AA before menu");
                    g.Configuration.Seasons.SpringRainChance = 0; string dir = Path.GetFullPath("Logs/StorySaves/" + Guid.NewGuid().ToString("N")); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing) { Place(g); g.OpenDialogue(Id); Phase(2); }
                else if (phase == 2 && Talk != null)
                {
                    Require(!Talk.StoryButton.interactable && !g.Stories.State.Completed(History), "Intro story gate"); g.OpenStoryFromDialogue(); Require(g.Dialogue.ActiveEventId == null, "No story before meeting");
                    Finish(g); g.Inventory.TryAdd("turnip", 3); g.Inventory.TryAdd("tomato", 3); g.Inventory.TryAdd("corn", 3); Points(g, 29); AtKitchen(g); g.OpenCooking(); Phase(3);
                }
                else if (phase == 3 && Kitchen != null)
                {
                    Require(Kitchen.CookButtons.Length == 5 && !Kitchen.CookButtons[2].interactable && Kitchen.ResultTexts[2].text.Contains("70") && Kitchen.ResultTexts[2].text.Contains("朋友的食谱"), "Third recipe shows condition");
                    g.CookRecipe(2); Require(g.Inventory.GetCount("turnip") == 3 && g.Inventory.GetCount("vegetable_soup") == 0, "Locked craft consumes nothing"); TextCheck(Kitchen); Capture(g, "LockedRecipePreview");
                    g.CloseCooking(); Place(g); g.OpenDialogue(Id); Phase(4);
                }
                else if (phase == 4 && Talk != null)
                {
                    Require(!Talk.StoryButton.interactable, "29 points locked"); g.OpenStoryFromDialogue(); Require(g.Dialogue.ActiveEventId == null, "Direct event action locked");
                    Points(g, 30); g.UI.RefreshViews(); Require(Talk.StoryButton.interactable, "30 points available");
                    g.Players.RestorePosition(new Vector3(0, .1f, -10)); g.OpenStoryFromDialogue(); Require(g.Dialogue.ActiveEventId == null, "Event distance recheck"); Place(g);
                    Talk.StoryButton.onClick.Invoke(); Require(g.Dialogue.ActiveEventId == History && !Talk.GiftButton.interactable && !Talk.StoryButton.interactable, "Story replaces daily dialogue and disables gift");
                    g.OpenGiftFromDialogue(); Require(g.UI.HasDialogue && !g.UI.HasGift, "Story cannot be skipped by gift action"); TextCheck(Talk); Capture(g, "IslandHistoryPreview");
                    while (!g.Dialogue.State.LastLine) Talk.NextButton.onClick.Invoke(); g.CloseDialogue(); Require(!g.Stories.State.Completed(History), "Cancel final displayed line does not complete");
                    g.ReturnToMainMenu(); Phase(5);
                }
                else if (phase == 5 && g.State == GameFlowState.MainMenu) { Require(g.Stories.State == null && g.Dialogue.ActiveEventId == null, "Story cleanup"); g.ContinueGame(); Phase(6); }
                else if (phase == 6 && g.State == GameFlowState.Playing) { Require(!g.Stories.State.Completed(History) && g.Relationships.State.Points(Id) == 30, "Cancellation survives reload"); Place(g); g.OpenDialogue(Id); Phase(7); }
                else if (phase == 7 && Talk != null)
                {
                    Talk.StoryButton.onClick.Invoke(); Require(g.Dialogue.State.Index == 0, "Event restarts at first line");
                    SessionState.SetFloat(Key + ".Minute", (float)g.Time.Clock.Minute); SessionState.SetFloat(Key + ".Pause", (float)EditorApplication.timeSinceStartup); Phase(8);
                }
                else if (phase == 8 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Pause", 0) > .2)
                {
                    int day = g.Farm.State.Day; g.AdvanceDay(); g.ToggleInventory(); g.OpenCooking(); Require(day == g.Farm.State.Day && !g.UI.IsInventoryVisible && !g.UI.HasCooking
                        && Math.Abs(g.Time.Clock.Minute - SessionState.GetFloat(Key + ".Minute", 0)) < .001, "Story modal pauses time and excludes other panels");
                    Finish(g); Require(g.Stories.State.Completed(History) && !g.Stories.State.RecipeUnlocked("vegetable_soup") && g.Saves.TryLoad(out var saved) && saved.StoryEvents[0].Completed, "History completed and saved without reward");
                    Points(g, 70); g.OpenDialogue(Id); Phase(9);
                }
                else if (phase == 9 && Talk != null)
                {
                    Require(Talk.StoryButton.interactable && Talk.StoryButton.GetComponentInChildren<TMPro.TMP_Text>().text == "朋友的食谱", "Second event offered");
                    AtKitchen(g); g.CloseDialogue(); g.OpenCooking(); Phase(10);
                }
                else if (phase == 10 && Kitchen != null)
                {
                    Require(!Kitchen.CookButtons[2].interactable, "70 points alone does not unlock"); g.CloseCooking(); Place(g); g.OpenDialogue(Id); Phase(11);
                }
                else if (phase == 11 && Talk != null)
                {
                    Talk.StoryButton.onClick.Invoke(); TextCheck(Talk); Capture(g, "FriendRecipeDialoguePreview");
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    int notices = 0; Action observer = () => { notices++; Require(g.Stories.State.Completed(Recipe) && g.Stories.State.RecipeUnlocked("vegetable_soup"), "Completion and recipe grant atomic"); g.AdvanceDialogue(); g.ReturnToMainMenu(); };
                    g.Stories.State.Changed += observer; Finish(g); g.Stories.State.Changed -= observer;
                    Require(notices == 1 && g.State == GameFlowState.Playing && g.Stories.State.Completed(Recipe) && g.Inventory.GetCount("vegetable_soup") == 0 && g.Economy.Gold == 100 && g.Stamina.State.Current == 100, "Save failure retains unlock and blocks reentry; no items/gold/stamina granted");
                    SaveDirectory(g, dir); AtKitchen(g); g.OpenCooking(); Phase(12);
                }
                else if (phase == 12 && Kitchen != null)
                {
                    Kitchen.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.StoryEvents.All(s => s.Completed), "Retry saves event grant once");
                    Require(Kitchen.CookButtons[2].interactable && Kitchen.IngredientTexts[2].text.Contains("番茄"), "Unlock enables multi ingredient MVC"); TextCheck(Kitchen); Capture(g, "UnlockedRecipePreview");
                    Kitchen.CookButtons[2].onClick.Invoke(); Require(g.Inventory.GetCount("turnip") == 2 && g.Inventory.GetCount("tomato") == 2 && g.Inventory.GetCount("corn") == 2 && g.Inventory.GetCount("vegetable_soup") == 1, "Craft consumes exactly all three ingredients");
                    g.CloseCooking(); g.Stamina.State.Restore(30); g.ToggleInventory(); Phase(13);
                }
                else if (phase == 13 && g.UI.IsInventoryVisible)
                {
                    int slot = Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == "vegetable_soup");
                    g.UseFood(slot); Require(g.Stamina.State.Current == 80 && g.Inventory.GetCount("vegetable_soup") == 0, "New soup restores 50 through existing food manager"); g.CloseInventory(); g.ReturnToMainMenu(); Phase(14);
                }
                else if (phase == 14 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(15); }
                else if (phase == 15 && g.State == GameFlowState.Playing)
                { Require(g.Stories.State.Capture().All(s => s.Completed) && g.Stories.State.RecipeUnlocked("vegetable_soup") && g.Stamina.State.Current == 80, "Unlock and food effects reload"); Place(g); g.OpenDialogue(Id); Phase(16); }
                else if (phase == 16 && Talk != null)
                {
                    Require(!Talk.StoryButton.interactable, "Completed event cannot replay reward"); g.OpenStoryFromDialogue(); Require(g.Dialogue.ActiveEventId == null, "Direct duplicate event rejected"); g.CloseDialogue(); g.ReturnToMainMenu(); Phase(17);
                }
                else if (phase == 17 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var saved), "Read save validation fixture"); string original = JsonUtility.ToJson(saved);
                    saved.StoryEvents[0].Completed = false; Write(g, saved); Require(!g.Saves.TryLoad(out _), "Missing prerequisite rejected");
                    saved = JsonUtility.FromJson<SaveData>(original); saved.Relationships[0].Points = 69; Write(g, saved); Require(!g.Saves.TryLoad(out _), "Completed event below threshold rejected");
                    saved = JsonUtility.FromJson<SaveData>(original); saved.StoryEvents[1].Id = "unknown"; Write(g, saved); Require(!g.Saves.TryLoad(out _), "Unknown event ID rejected");
                    saved = JsonUtility.FromJson<SaveData>(original); saved.Version = 13; saved.StoryEvents = null; Write(g, saved);
                    Require(g.Saves.TryLoad(out var migrated) && migrated.Version == SaveData.CurrentVersion && migrated.StoryEvents.All(s => !s.Completed) && migrated.Relationships[0].Points == 70 && migrated.NPCs[0].Met && migrated.Stamina == 80 && migrated.Gold == 100, "v13 migration keeps world and relationships, no automatic reward"); g.ContinueGame(); Phase(18);
                }
                else if (phase == 18 && g.State == GameFlowState.Playing) { Require(g.Stories.State.Available(History) && !g.Stories.State.RecipeUnlocked("vegetable_soup"), "Migration offers history before recipe"); AtKitchen(g); g.OpenCooking(); Phase(19); }
                else if (phase == 19 && Kitchen != null)
                {
                    Require(!Kitchen.CookButtons[2].interactable, "Migrated recipe stays locked until event");
                    File.WriteAllText("Logs/StoryEventFlowVerification.txt", "PASS: packed AA story config, 30/70 gates and prerequisite, dialogue MVC/range/modal/cancel/final completion, atomic unlock/reentry/save failure/retry, three-ingredient craft, 50 stamina food, reload/no duplicate, v13 migration, corrupt records, cleanup and Chinese layout.\n");
                    Debug.Log("Story/event flow verification PASSED."); Phase(20); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
