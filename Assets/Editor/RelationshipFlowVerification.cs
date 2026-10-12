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
    public static class RelationshipFlowVerification
    {
        const string Key = "MagicIsland.RelationshipVerification", Id = "island_steward";
        static RelationshipFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static GiftView Gift => UnityEngine.Object.FindObjectOfType<GiftView>();
        static DialogueView Talk => UnityEngine.Object.FindObjectOfType<DialogueView>();
        static void Place(GameManager g) => g.Players.RestorePosition(g.NPCs.Actors[Id].transform.position + new Vector3(1.2f, .1f, -.8f));
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void FinishTalk(GameManager g) { int remaining = 32; while (g.UI.HasDialogue && remaining-- > 0) Talk.NextButton.onClick.Invoke(); Require(!g.UI.HasDialogue, "Finish introduction"); }
        static int Slot(GameManager g, string id) => Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == id && !g.Inventory.State.Slots[i].Empty);
        static void TextCheck(Component view) { foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Relationship text/glyphs: " + text.name); } }
        static void Capture(GameManager g, string name) { GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Write(GameManager g, SaveData data) { string json = JsonUtility.ToJson(data); File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 17 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Relationship timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Configuration failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Relationships.Validate(g.Configuration.NPCs, g.Configuration.Items), "RelationshipCatalog packed AA before menu");
                    g.Configuration.Seasons.SpringRainChance = 0; string dir = Path.GetFullPath("Logs/RelationshipSaves/" + Guid.NewGuid().ToString("N")); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                { Place(g); g.OpenGift(Id); Require(!g.UI.HasGift && g.Relationships.State.Points(Id) == 0, "Cannot gift before meeting"); g.OpenDialogue(Id); Phase(2); }
                else if (phase == 2 && g.UI.HasDialogue && Talk != null)
                {
                    Require(!Talk.GiftButton.interactable && Talk.RelationshipText.text.Contains("未见面"), "Intro gift gate"); FinishTalk(g); Require(g.NPCs.State.HasMet(Id), "Met after full intro");
                    g.Inventory.TryAdd("wood", 2); g.Inventory.TryAdd("turnip", 3); g.Inventory.TryAdd("roasted_corn", 2); g.Inventory.TryAdd("turnip_soup", 105);
                    var node = g.Gathering.Nodes[0]; g.Players.RestorePosition(node.transform.position + new Vector3(1.5f, .1f, 0)); g.Tools.SelectTool(FarmTool.Axe); Require(g.TryGather(0, out _), "Start tool swing");
                    Place(g); g.OpenGift(Id); Require(!g.UI.HasGift, "Busy tool excludes gift"); Phase(3);
                }
                else if (phase == 3 && !g.Tools.Busy) { g.OpenDialogue(Id); Phase(4); }
                else if (phase == 4 && g.UI.HasDialogue && Talk != null)
                { Require(Talk.GiftButton.interactable && Talk.RelationshipText.text.Contains("初识"), "Dialogue shows relationship and gift entry"); TextCheck(Talk); Talk.GiftButton.onClick.Invoke(); Phase(5); }
                else if (phase == 5 && g.UI.HasGift && Gift != null)
                {
                    Require(!g.UI.HasDialogue && !g.Dialogue.State.Active && Gift.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp) && Gift.Slots.Count(s => s.gameObject.activeSelf) == 12, "Gift replaces dialogue with PopUp MVC");
                    Require(!Gift.Slots[0].SelectButton.interactable && !Gift.Slots[1].SelectButton.interactable && Gift.Slots[2].SelectButton.interactable, "Only crops and food selectable");
                    g.GiveGift(0); g.GiveGift(1); Require(g.Inventory.GetCount("turnip_seed") == 50 && g.Inventory.GetCount("wood") == 2 && g.Relationships.State.Points(Id) == 0, "Invalid gifts free");
                    Gift.Slots[5].SelectButton.onClick.Invoke(); Require(Gift.PreviewText.text.Contains("+20") && Gift.PreviewText.text.Contains("偏好"), "Favorite preview"); TextCheck(Gift); Capture(g, "GiftPanelPreview");
                    SessionState.SetFloat(Key + ".Minute", (float)g.Time.Clock.Minute); SessionState.SetFloat(Key + ".Pause", (float)EditorApplication.timeSinceStartup); Phase(6);
                }
                else if (phase == 6 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Pause", 0) > .2)
                {
                    int day = g.Farm.State.Day; g.AdvanceDay(); g.OpenCooking(); g.OpenStorage(); g.OpenOrder(); g.OpenShop(); g.OpenSleep(); g.OpenToolUpgrade(); g.ToggleInventory(); g.OpenDialogue(Id);
                    Require(Math.Abs(g.Time.Clock.Minute - SessionState.GetFloat(Key + ".Minute", 0)) < .001 && day == g.Farm.State.Day && !g.UI.HasDialogue && !g.UI.HasCooking && !g.UI.HasStorage
                        && !g.UI.HasOrder && !g.UI.HasShop && !g.UI.HasSleep && !g.UI.HasToolUpgrade && !g.UI.IsInventoryVisible && !g.TryGather(0, out _), "Gift modal pauses and excludes other systems");
                    g.Players.RestorePosition(new Vector3(0, .1f, -10)); g.GiveGift(5); Require(g.Relationships.State.Points(Id) == 0, "Distance recheck protects gift"); Place(g);
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () => { Require(g.Relationships.State.Points(Id) == 20 && g.Relationships.State.LastGiftDay(Id) == 1 && g.Inventory.State.Slots[4].Quantity == 99 && g.Inventory.State.Slots[5].Quantity == 5, "Observer sees atomic selected-slot gift"); g.GiveGift(4); g.ReturnToMainMenu(); };
                    g.Inventory.State.Changed += observer; Gift.GiveButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.State == GameFlowState.Playing && g.Inventory.GetCount("turnip_soup") == 104 && g.Relationships.State.Points(Id) == 20 && g.Relationships.Feedback.Contains("保存失败"), "Save failure keeps gift, points and date; no reentry");
                    g.GiveGift(4); Require(g.Inventory.GetCount("turnip_soup") == 104 && g.Relationships.State.Points(Id) == 20, "One gift per total day"); SaveDirectory(g, dir); Gift.SaveButton.onClick.Invoke();
                    Require(g.Saves.TryLoad(out var saved) && saved.Relationships[0].Points == 20 && saved.Relationships[0].LastGiftDay == 1 && saved.Version == SaveData.CurrentVersion, "Retry saves without giving twice");
                    Require(g.Stamina.State.Current == 96 && g.Economy.Gold == 100 && g.Tasks.State.Capture().All(t => t.Progress == 0), "Gifts do not consume stamina/gold or count as harvest/sell");
                    Capture(g, "GiftCompletedPreview"); g.CloseGift(); g.ReturnToMainMenu(); Phase(7);
                }
                else if (phase == 7 && g.State == GameFlowState.MainMenu)
                { Require(g.Relationships.State == null && g.Relationships.TargetId == null && !g.UI.HasGift, "Relationship cleanup"); g.ContinueGame(); Phase(8); }
                else if (phase == 8 && g.State == GameFlowState.Playing)
                {
                    Require(g.Relationships.State.Points(Id) == 20 && g.Relationships.State.LastGiftDay(Id) == 1 && g.Inventory.GetCount("turnip_soup") == 104, "Reload preserves points/date/items");
                    Place(g); g.OpenGift(Id); Phase(9);
                }
                else if (phase == 9 && g.UI.HasGift && Gift != null)
                {
                    Gift.Slots[4].SelectButton.onClick.Invoke(); Require(!Gift.GiveButton.interactable && Gift.StatusText.text.Contains("今日已送礼"), "Reload cannot gift again today"); g.GiveGift(4); Require(g.Inventory.GetCount("turnip_soup") == 104, "Reload duplicate free");
                    g.CloseGift(); g.AdvanceDay(); Require(g.Relationships.State.Day == 2, "Day sync"); g.OpenGift(Id); Phase(10);
                }
                else if (phase == 10 && g.UI.HasGift && Gift != null)
                {
                    Gift.Slots[Slot(g, "roasted_corn")].SelectButton.onClick.Invoke(); Gift.GiveButton.onClick.Invoke(); Require(g.Relationships.State.Points(Id) == 30 && g.Inventory.GetCount("roasted_corn") == 1 && Gift.StatusText.text.Contains("熟悉"), "Standard food gain/familiar threshold");
                    g.CloseGift(); g.OpenDialogue(Id); Phase(11);
                }
                else if (phase == 11 && g.UI.HasDialogue && Talk != null)
                { Require(g.Dialogue.State.Text == g.Configuration.Relationships.Find(Id).FamiliarGreeting, "Familiar greeting"); TextCheck(Talk); Capture(g, "FamiliarDialoguePreview"); g.CloseDialogue(); g.AdvanceDay(); g.OpenGift(Id); Phase(12); }
                else if (phase == 12 && g.UI.HasGift && Gift != null)
                {
                    int expected = g.Farm.State.Day == 3 ? 50 : g.Farm.State.Day == 4 ? 70 : g.Farm.State.Day == 5 ? 90 : 100;
                    Gift.Slots[Slot(g, "turnip_soup")].SelectButton.onClick.Invoke(); Gift.GiveButton.onClick.Invoke(); Require(g.Relationships.State.Points(Id) == expected && g.Relationships.State.LastGiftDay(Id) == g.Farm.State.Day, "Daily favorite gifts reach thresholds");
                    if (g.Farm.State.Day == 6)
                    {
                        Require(g.Relationships.State.LastGain == 10, "Near-cap actual gain clamped"); g.CloseGift(); g.AdvanceDay(); g.OpenGift(Id); Phase(13);
                    }
                    else { g.CloseGift(); g.AdvanceDay(); g.OpenGift(Id); }
                }
                else if (phase == 13 && g.UI.HasGift && Gift != null)
                {
                    long count = g.Inventory.GetCount("turnip_soup"); Gift.Slots[Slot(g, "turnip_soup")].SelectButton.onClick.Invoke(); g.GiveGift(Slot(g, "turnip_soup"));
                    Require(!Gift.GiveButton.interactable && g.Inventory.GetCount("turnip_soup") == count && g.Relationships.State.Points(Id) == 100 && g.Relationships.Feedback.Contains("已满"), "At cap no gift consumed");
                    g.CloseGift(); g.OpenDialogue(Id); Phase(14);
                }
                else if (phase == 14 && g.UI.HasDialogue && Talk != null)
                { Require(g.Dialogue.State.Text == g.Configuration.Relationships.Find(Id).FriendlyGreeting && Talk.RelationshipText.text.Contains("友好"), "Friendly greeting and relationship status"); TextCheck(Talk); Capture(g, "FriendlyDialoguePreview"); g.CloseDialogue(); g.ReturnToMainMenu(); Phase(15); }
                else if (phase == 15 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var data), "Read validation fixture"); string original = JsonUtility.ToJson(data); data.Relationships[0].LastGiftDay = data.Day + 1; Write(g, data); Require(!g.Saves.TryLoad(out _), "Future gift date rejected");
                    data = JsonUtility.FromJson<SaveData>(original); string order = JsonUtility.ToJson(data.Order), resource = JsonUtility.ToJson(data.Gathering[0]); int day = data.Day;
                    data.Version = 12; data.Relationships = null; Write(g, data); Require(g.Saves.TryLoad(out var migrated) && migrated.Version == SaveData.CurrentVersion && migrated.NPCs[0].Met && migrated.Relationships[0].Points == 0
                        && migrated.Relationships[0].LastGiftDay == 0 && migrated.Day == day && JsonUtility.ToJson(migrated.Order) == order && JsonUtility.ToJson(migrated.Gathering[0]) == resource, "v12 migration preserves meeting/world/items and initializes zero relationships"); g.ContinueGame(); Phase(16);
                }
                else if (phase == 16 && g.State == GameFlowState.Playing)
                {
                    Require(g.NPCs.State.HasMet(Id) && g.Relationships.State.Points(Id) == 0 && g.Relationships.State.LastGiftDay(Id) == 0, "Migrated save playable");
                    File.WriteAllText("Logs/RelationshipFlowVerification.txt", "PASS: packed AA rules/GiftPanel and dialogue migration, met/busy/distance/UI/modal guards, selected-slot atomic gifting/reentry, daily/reload limit, preferred/standard gains, rank greetings, cap, save failure/retry, v12 migration, future-date rejection, cleanup and Chinese layout.\n");
                    Debug.Log("Relationship/gift flow verification PASSED."); Phase(17); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
