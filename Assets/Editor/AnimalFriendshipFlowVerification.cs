using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MagicIsland.Animals;
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
    public static class AnimalFriendshipFlowVerification
    {
        const string Key = "MagicIsland.AnimalFriendshipVerification";
        static AnimalFriendshipFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            var paths = new[] { "Assets/Config/AnimalConfig.asset", "Assets/Art/Prefabs/Animals/Chicken.prefab", "Assets/Art/Prefabs/UI/CoopPanel.prefab" };
            var data = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(data[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Complete friendship art/config survives repeated setup");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static CoopView View => UnityEngine.Object.FindObjectOfType<CoopView>();
        static AnimalShopView ShopView => UnityEngine.Object.FindObjectOfType<AnimalShopView>();
        static void Office(GameManager g) => g.Players.RestorePosition(g.Orders.Board.transform.position + new Vector3(0, .1f, -1.5f));
        static void Shop(GameManager g) => g.Players.RestorePosition(g.Shop.Counter.position + new Vector3(0, .1f, -1.5f));
        static void Care(GameManager g) => g.Players.RestorePosition(g.Animals.Coop.transform.position + new Vector3(0, .1f, -1.7f));
        static SavedChicken Bird(GameManager g, int id) => g.Animals.State.Chicken(id);
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void Write(GameManager g, string json) { File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static string Legacy(SaveData saved)
        { saved.Version = 21; return Regex.Replace(JsonUtility.ToJson(saved, true), ",\\s*\"(Affinity|LastPetDay)\":\\s*-?\\d+", ""); }
        static void TextCheck(Component root)
        { foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) { t.ForceMeshUpdate(true); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Friendship TMP: " + t.name + "=" + t.text); } }
        static void Capture(GameManager g, string name)
        { g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 19 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Friendship timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Friendship boot failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    var config = g.Configuration.Animals; Require(config.Validate(g.Configuration.Items, g.Configuration.Config) && config.ChickenNames.SequenceEqual(new[] { "小白", "团团" }) && config.PetAffinity == 5 && config.FeedAffinity == 2, "AA names and 5/2 gains before menu");
                    int gain = config.PetAffinity; config.PetAffinity = 0; Require(!config.Validate(g.Configuration.Items, g.Configuration.Config), "Invalid friendship gain rejected"); config.PetAffinity = gain;
                    string name = config.ChickenNames[1]; config.ChickenNames[1] = config.ChickenNames[0]; Require(!config.Validate(g.Configuration.Items, g.Configuration.Config), "Duplicate chicken names rejected"); config.ChickenNames[1] = name;
                    string dir = Path.GetFullPath("Logs/AnimalFriendshipSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                { Require(!g.Animals.State.Built && g.Animals.State.Count == 0, "New game has no free companions"); g.Inventory.TryAdd("wood", 15); g.Inventory.TryAdd("stone", 5); g.Inventory.TryAdd("animal_feed", 3); g.Economy.Restore(1000); Office(g); g.OpenCoop(true); Phase(2); }
                else if (phase == 2 && View != null)
                {
                    Require(View.PetButtons.All(b => !b.interactable) && View.AffinityBars.All(b => b.value == 0) && View.ChickenNameTexts.All(t => t.text.Contains("等待")), "Empty office shows independent waiting cards"); TextCheck(View);
                    View.BuildButton.onClick.Invoke(); Require(g.Animals.State.Built, "Build from actual office button"); g.CloseAnimalPanel(); Shop(g); g.OpenAnimalShop(); Phase(3);
                }
                else if (phase == 3 && ShopView != null)
                { ShopView.ChickenButton.onClick.Invoke(); Require(g.Animals.State.Count == 1 && Bird(g, 1).Affinity == 0 && g.Animals.Chickens[0].HeartRoot != null, "AA chicken includes hidden heart and zero friendship"); TextCheck(g.Animals.Chickens[0]); g.CloseAnimalPanel(); Care(g); g.OpenCoop(); Phase(4); }
                else if (phase == 4 && View != null)
                {
                    Require(View.Layer == UILayer.PopUp && View.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp) && View.ChickenNameTexts[0].text == "小白" && View.PetButtons[0].interactable && !View.PetButtons[1].interactable, "Per-ID care cards and MVC layer");
                    var point = g.Players.Player.transform.position; g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.PetChicken(1); Require(Bird(g, 1).Affinity == 0 && Bird(g, 1).LastPetDay == 0, "Pet range guard"); g.Players.RestorePosition(point);
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () => { Require(Bird(g, 1).Affinity == 5 && Bird(g, 1).LastPetDay == 1 && g.Animals.Chickens[0].HeartVisible, "Complete friendship and world feedback before observers"); g.PetChicken(1); g.FeedChickens(); g.CloseAnimalPanel(); g.ReturnToMainMenu(); g.AdvanceDay(); g.StartNewDayFromReport(); };
                    g.Animals.State.Changed += observer; View.PetButtons[0].onClick.Invoke(); g.Animals.State.Changed -= observer;
                    Require(g.State == GameFlowState.Playing && g.UI.HasCoop && g.Farm.State.Day == 1 && g.Animals.Feedback.Contains("保存失败") && g.Inventory.GetCount("animal_feed") == 3 && Bird(g, 1).Affinity == 5, "Save failure retains pet and blocks mutation/exit/day reentry");
                    SaveDirectory(g, dir); View.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.Version == SaveData.CurrentVersion && saved.Animals.Chickens[0].Affinity == 5 && saved.Animals.Chickens[0].LastPetDay == 1, "Pet retry saves once");
                    Require(!View.PetButtons[0].interactable && View.AffinityBars[0].value == 5 && View.HeartGraphics[0].gameObject.activeSelf, "Pet cooldown/bar/UI heart visible while modal"); TextCheck(View); Capture(g, "ChickenFriendshipPanelPreview");
                    var heartMesh = View.HeartGraphics[0].canvasRenderer.GetMesh(); Require(heartMesh != null && heartMesh.vertexCount == MagicIsland.UI.HeartGraphic.Segments + 1, "Actual canvas heart geometry renders");
                    g.PetChicken(1); g.PetChicken(2); Require(Bird(g, 1).Affinity == 5 && g.Animals.State.Count == 1, "Repeat and absent chicken cannot gain points");
                    View.FeedButton.onClick.Invoke(); g.FeedChickens(); Require(Bird(g, 1).Affinity == 7 && Bird(g, 1).FedDay == 1 && g.Inventory.GetCount("animal_feed") == 2, "One successful feeding grants exactly two");
                    var actor = g.Animals.Chickens[0]; var position = actor.transform.localPosition; g.Animals.Tick(.25f, true);
                    Require(actor.HeartVisible && actor.HeartRoot.localPosition.y > .85f && actor.transform.localPosition == position, "Heart animates while modal keeps idle paused");
                    g.CloseAnimalPanel(); g.Players.RestorePosition(g.Animals.Config.Position + new Vector3(1.5f, .1f, -1.5f)); Capture(g, "ChickenHeartWorldPreview"); Shop(g); g.OpenAnimalShop(); Phase(5);
                }
                else if (phase == 5 && ShopView != null)
                { ShopView.ChickenButton.onClick.Invoke(); Require(g.Animals.State.Count == 2 && Bird(g, 2).Affinity == 0 && Bird(g, 2).LastPetDay == 0, "Newly bought second bird has independent friendship"); g.CloseAnimalPanel(); Care(g); g.OpenCoop(); Phase(6); }
                else if (phase == 6 && View != null)
                {
                    Require(View.ChickenNameTexts[1].text == "团团" && !View.PetButtons[0].interactable && View.PetButtons[1].interactable, "Stable names and separate pet flags");
                    View.FeedButton.onClick.Invoke(); Require(Bird(g, 1).Affinity == 7 && Bird(g, 2).Affinity == 2 && g.Inventory.GetCount("animal_feed") == 1, "Batch feed rewards only new unfed chicken"); View.PetButtons[1].onClick.Invoke();
                    Require(Bird(g, 1).Affinity == 7 && Bird(g, 2).Affinity == 7 && View.PetButtons.All(b => !b.interactable) && View.AffinityBars.All(b => b.value == 7), "Each bird pet once independently"); TextCheck(View); Capture(g, "TwoChickenFriendshipPreview");
                    g.Animals.Tick(2, true); g.UI.RefreshViews(); Require(g.Animals.Chickens.All(c => !c.HeartVisible && !c.HeartRoot.gameObject.activeSelf) && View.HeartGraphics.All(t => !t.gameObject.activeSelf), "Transient hearts expire while modal stays open");
                    g.CloseAnimalPanel(); g.ReturnToMainMenu(); Phase(7);
                }
                else if (phase == 7 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var saved), "Read current chicken snapshot"); string legacy = Legacy(saved); Require(!legacy.Contains("\"Affinity\"") && !legacy.Contains("\"LastPetDay\""), "Real v21 fixture has no new fields"); Write(g, legacy);
                    Require(g.Saves.TryLoad(out var migrated) && migrated.Version == SaveData.CurrentVersion && migrated.Animals.Chickens.All(c => c.Affinity == 0 && c.LastPetDay == 0 && c.FedDay == 1) && migrated.Gold == 700, "Migration awards no retroactive friendship and preserves feeding/gold"); g.ContinueGame(); Phase(8);
                }
                else if (phase == 8 && g.State == GameFlowState.Playing)
                { Require(g.Animals.State.Unfed == 0 && Bird(g, 1).Affinity == 0 && Bird(g, 2).Affinity == 0 && g.Animals.Chickens.All(c => !c.HeartVisible), "Migrated fed birds have fresh pet eligibility without replayed effects"); Care(g); g.OpenCoop(); Phase(9); }
                else if (phase == 9 && View != null)
                { Require(View.PetButtons.All(b => b.interactable) && !View.FeedButton.interactable, "Migration keeps feed restriction but enables new interaction"); View.PetButtons[0].onClick.Invoke(); View.PetButtons[1].onClick.Invoke(); g.FeedChickens(); Require(Bird(g, 1).Affinity == 5 && Bird(g, 2).Affinity == 5 && g.Inventory.GetCount("animal_feed") == 1, "Old feeding never earns points again"); g.CloseAnimalPanel(); g.AdvanceDay(); g.StartNewDayFromReport(); g.Inventory.TryAdd("animal_feed", 1); Office(g); g.OpenCoop(true); Phase(10); }
                else if (phase == 10 && View != null)
                {
                    Require(g.Animals.State.Eggs == 2 && g.Animals.State.CheckPet(1) == AnimalResult.Success && View.PetButtons.All(b => !b.interactable), "Next-day eligibility shown read-only at office");
                    Care(g); g.PetChicken(1); g.FeedChickens(); g.CollectEggs(); Require(Bird(g, 1).Affinity == 5 && Bird(g, 1).LastPetDay == 1 && g.Inventory.GetCount("animal_feed") == 2 && g.Animals.State.Eggs == 2, "Office mode cannot remotely care even after teleport to coop"); g.CloseAnimalPanel(); g.OpenCoop(); Phase(11);
                }
                else if (phase == 11 && View != null)
                {
                    Require(View.PetButtons.All(b => b.interactable), "Care panel refreshes next-day buttons"); View.FeedButton.onClick.Invoke(); Require(Bird(g, 1).Affinity == 7 && Bird(g, 2).Affinity == 7 && g.Inventory.GetCount("animal_feed") == 0, "Both birds earn today's feed points once");
                    var boundaries = g.Animals.State.Capture(); boundaries.Chickens[0].Affinity = 29; boundaries.Chickens[1].Affinity = 69; g.Animals.State.Restore(boundaries); g.UI.RefreshViews();
                    View.PetButtons[0].onClick.Invoke(); View.PetButtons[1].onClick.Invoke(); Require(Bird(g, 1).Affinity == 34 && Bird(g, 2).Affinity == 74 && View.AffinityTexts[0].text.Contains("熟悉") && View.AffinityTexts[1].text.Contains("亲近"), "Independent 30/70 stage transitions and bars"); TextCheck(View); Capture(g, "ChickenFriendshipStagesPreview");
                    var capped = g.Animals.State.Capture(); capped.Chickens[0].Affinity = 99; capped.Chickens[0].LastPetDay = 1; g.Animals.State.Restore(capped); g.UI.RefreshViews(); View.PetButtons[0].onClick.Invoke(); Require(Bird(g, 1).Affinity == 100 && View.AffinityBars[0].value == 100 && g.Animals.Feedback.Contains("+1"), "Near-full pet clamps displayed gain");
                    g.CloseAnimalPanel(); g.AdvanceDay(); g.StartNewDayFromReport(); Care(g); g.OpenCoop(); Phase(20);
                }
                else if (phase == 20 && View != null)
                { Require(View.PetButtons[0].interactable && Bird(g, 1).Affinity == 100 && g.Animals.State.TotalEggs == 4, "Full-affinity bird remains interactable next day, total eggs follow original feed rule"); View.PetButtons[0].onClick.Invoke(); Require(Bird(g, 1).Affinity == 100 && Bird(g, 1).LastPetDay == 3 && g.Animals.Feedback.Contains("已满"), "Full-affinity pet records daily interaction and still reacts"); g.Animals.Tick(2, true); g.CloseAnimalPanel(); g.ReturnToMainMenu(); Phase(12); }
                else if (phase == 12 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var saved) && saved.Animals.Chickens[0].Affinity == 100 && saved.Animals.Chickens[0].LastPetDay == 3, "Current full affinity persisted"); string good = JsonUtility.ToJson(saved);
                    saved.Animals.Chickens[0].Affinity = 101; Write(g, JsonUtility.ToJson(saved)); g.ContinueGame(); Require(g.State == GameFlowState.MainMenu && !g.Saves.HasSave, "Overflow affinity rejected before world load");
                    saved = JsonUtility.FromJson<SaveData>(good); saved.Animals.Chickens[0].LastPetDay = 4; Write(g, JsonUtility.ToJson(saved)); Require(!g.Saves.TryLoad(out _), "Future pet day rejects both files");
                    saved = JsonUtility.FromJson<SaveData>(good); saved.Animals.Eggs += saved.Animals.QualityEggs; saved.Animals.QualityEggs = 0; saved.Order.Fulfilled = true; string order = JsonUtility.ToJson(saved.Order); Write(g, Legacy(saved));
                    Require(g.Saves.TryLoad(out var migrated) && migrated.Order.CoopOffersUnlocked && migrated.Order.Fulfilled && JsonUtility.ToJson(migrated.Order) == order && migrated.Animals.Eggs == 4 && migrated.Animals.Chickens.All(c => c.FedDay == 2 && c.Affinity == 0 && c.LastPetDay == 0) && migrated.Gold == 700, "v21 migration retains paid unlocked order, animals, dates, products and money"); g.ContinueGame(); Phase(13);
                }
                else if (phase == 13 && g.State == GameFlowState.Playing)
                { Require(g.Animals.State.Eggs == 4 && g.Orders.State.Fulfilled && Bird(g, 1).Affinity == 0 && g.Animals.State.CheckPet(1) == AnimalResult.Success, "Second legacy read remains playable without retroactive gifts"); Care(g); g.OpenCoop(); Phase(14); }
                else if (phase == 14 && View != null) { View.PetButtons[0].onClick.Invoke(); Require(Bird(g, 1).Affinity == 5 && Bird(g, 1).LastPetDay == 3, "First new interaction after old paid-order migration"); g.CloseAnimalPanel(); g.ReturnToMainMenu(); Phase(15); }
                else if (phase == 15 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(16); }
                else if (phase == 16 && g.State == GameFlowState.Playing)
                { Require(Bird(g, 1).Affinity == 5 && Bird(g, 1).LastPetDay == 3 && g.Animals.State.CheckPet(1) == AnimalResult.AlreadyPetted && Bird(g, 2).Affinity == 0 && g.Animals.State.CheckPet(2) == AnimalResult.Success && g.Animals.Chickens.All(c => !c.HeartVisible), "Current read retains independent daily limit and never replays transient hearts"); g.PetChicken(2); Require(Bird(g, 2).Affinity == 0, "Closed panel cannot pet"); g.ReturnToMainMenu(); Phase(17); }
                else if (phase == 17 && g.State == GameFlowState.MainMenu) { g.EnterGame(); Phase(18); }
                else if (phase == 18 && g.State == GameFlowState.Playing)
                { Require(!g.Animals.State.Built && g.Animals.State.Count == 0 && g.Animals.Chickens.Count == 0 && g.Farm.State.Day == 1, "New game resets animal friendship and effects"); File.WriteAllText("Logs/AnimalFriendshipVerification.txt", "PASS: packed AA names/gains/chicken heart/Coop MVC cards, default/empty/independent stable IDs, slider/TMP/heart layout, range/office/modal/atomic/reentry/save retry, daily pet/feed/clamp/stages, actual v21 missing-field migration and paid unlocked order preservation, corruption rejection, exact current read, transient effect expiry/cleanup and new game."); Debug.Log("Animal friendship flow verification PASSED."); Phase(19); EditorApplication.isPlaying = false; }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
