using System;
using System.IO;
using System.Linq;
using MagicIsland.Animals;
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
    public static class AnimalFlowVerification
    {
        const string Key = "MagicIsland.AnimalVerification";
        static AnimalFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static CoopView Coop => UnityEngine.Object.FindObjectOfType<CoopView>();
        static AnimalShopView Supply => UnityEngine.Object.FindObjectOfType<AnimalShopView>();
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir, g.Configuration.Tasks,
            g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships,
            g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void Office(GameManager g) => g.Players.RestorePosition(g.Orders.Board.transform.position + new Vector3(0, .1f, -1.5f));
        static void Shop(GameManager g) => g.Players.RestorePosition(g.Shop.Counter.position + new Vector3(0, .1f, -1.5f));
        static void AtCoop(GameManager g) => g.Players.RestorePosition(g.Animals.Config.Position + new Vector3(0, .1f, -1.7f));
        static void Door(GameManager g) => g.Players.RestorePosition(g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f));
        static void Home(GameManager g, float x, float z) => g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin + new Vector3(x, .1f, z));
        static void TextCheck(Component view)
        { foreach (var t in view.GetComponentsInChildren<TMPro.TMP_Text>()) { t.ForceMeshUpdate(); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Animal Chinese/layout: " + t.name + "=" + t.text); } }
        static void Capture(GameManager g, string name)
        { g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); g.UI.RefreshViews(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Write(GameManager g, SaveData data) { string json = JsonUtility.ToJson(data); File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 24 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Animal timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Animal configuration failed");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Animals.Validate(g.Configuration.Items, g.Configuration.Config) && (int)g.Configuration.Items.Find("egg").Type == 8, "20th AA config and appended egg type");
                    string dir = Path.GetFullPath("Logs/AnimalSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(!g.Animals.State.Built && g.Animals.Coop == null && g.Animals.Chickens.Count == 0 && g.Animals.State.Day == g.Farm.State.Day, "New game grants no coop/birds/products");
                    g.Inventory.TryAdd("wood", 30); g.Inventory.TryAdd("stone", 10); g.Economy.Restore(1000); g.Farm.TryAct(0, MagicIsland.Farming.FarmTool.Hoe, out _);
                    g.OpenCoop(true); g.OpenAnimalShop(); Require(!g.UI.HasCoop && !g.UI.HasAnimalShop, "Distant office/shop guards"); Shop(g); g.OpenShop(); Phase(2);
                }
                else if (phase == 2 && UnityEngine.Object.FindObjectOfType<ShopView>() != null)
                { var view = UnityEngine.Object.FindObjectOfType<ShopView>(); TextCheck(view); view.AnimalShopButton.onClick.Invoke(); Phase(3); }
                else if (phase == 3 && Supply != null)
                {
                    Require(!g.UI.HasShop && Supply.Layer == UILayer.PopUp && !Supply.ChickenButton.interactable && !g.Players.Player.enabled, "Shop switches to AA MVC animal supplies");
                    g.BuyChicken(); Require(g.Animals.State.Count == 0 && g.Economy.Gold == 1000, "Cannot buy chicken before coop"); Supply.FeedButton.onClick.Invoke(); Supply.FeedButton.onClick.Invoke(); Require(g.Inventory.GetCount("animal_feed") == 2 && g.Economy.Gold == 980, "Two feed purchases exact price");
                    var slots = g.Inventory.State.Slots.ToArray(); g.Inventory.State.Restore(Enumerable.Repeat(new MagicIsland.Inventory.InventorySlot("wood", 99), g.Inventory.State.Capacity).ToArray()); g.BuyAnimalFeed(); Require(g.Economy.Gold == 980, "Full bag feed purchase costs nothing"); g.Inventory.State.Restore(slots);
                    TextCheck(Supply); g.CloseAnimalPanel(); Office(g); g.OpenOrder(); Phase(4);
                }
                else if (phase == 4 && UnityEngine.Object.FindObjectOfType<OrderView>() != null)
                { var view = UnityEngine.Object.FindObjectOfType<OrderView>(); TextCheck(view); view.CoopButton.onClick.Invoke(); Phase(5); }
                else if (phase == 5 && Coop != null)
                {
                    Require(!g.UI.HasOrder && Coop.Layer == UILayer.PopUp && Coop.BuildButton.interactable && !Coop.FeedButton.interactable, "Office construction mode with range checks");
                    var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.transform.position = g.Animals.Config.Position + Vector3.up * .5f; Physics.SyncTransforms(); g.BuildCoop(); Require(!g.Animals.State.Built && g.Economy.Gold == 980 && g.Inventory.GetCount("wood") == 30, "Blocked site consumes nothing"); UnityEngine.Object.DestroyImmediate(blocker);
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () => { Require(g.Animals.State.Built && g.Animals.Coop != null && g.Inventory.GetCount("wood") == 15 && g.Inventory.GetCount("stone") == 5 && g.Economy.Gold == 780, "Coop transaction/visuals complete before observers"); g.BuildCoop(); g.ReturnToMainMenu(); g.CloseAnimalPanel(); };
                    g.Inventory.State.Changed += observer; Coop.BuildButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.UI.HasCoop && g.Animals.Feedback.Contains("保存失败") && g.Animals.State.Built, "Build failure to save preserves result and blocks reentry"); g.BuildCoop(); Require(g.Economy.Gold == 780, "Duplicate construction cannot pay again");
                    SaveDirectory(g, dir); Coop.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var save) && save.Version == SaveData.CurrentVersion && save.Animals.Built && save.Animals.Chickens.Length == 0, "Build save retry"); TextCheck(Coop); Capture(g, "CoopConstructionPreview"); g.CloseAnimalPanel(); Shop(g); g.OpenAnimalShop(); Phase(6);
                }
                else if (phase == 6 && Supply != null)
                {
                    Supply.ChickenButton.onClick.Invoke(); Supply.ChickenButton.onClick.Invoke(); g.BuyChicken(); Require(g.Animals.State.Count == 2 && g.Animals.Chickens.Count == 2 && g.Economy.Gold == 680, "Two AA chicken instances and capacity limit");
                    Require(g.Animals.Chickens.Select(c => c.Id).Distinct().Count() == 2 && g.Animals.Chickens.All(c => c.transform.parent == g.Animals.Coop.ChickenSpots[c.Id - 1]), "Chicken IDs and spots"); TextCheck(Supply); Capture(g, "AnimalShopPreview"); g.CloseAnimalPanel(); AtCoop(g); Physics.SyncTransforms();
                    var interaction = UnityEngine.Object.FindObjectOfType<InteractionController>(); Require(interaction.RefreshCoopHover(new Ray(g.Animals.Coop.transform.position + Vector3.up * 5, Vector3.down), false) && interaction.Hint.Contains("喂食"), "Owned chicken coop world hover"); g.OpenCoop(); Phase(7);
                }
                else if (phase == 7 && Coop != null)
                {
                    Require(Coop.FeedButton.interactable && !Coop.CollectButton.interactable, "Farm care mode"); var p = g.Players.Player.transform.position; g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.FeedChickens(); Require(g.Inventory.GetCount("animal_feed") == 2 && g.Animals.State.Unfed == 2, "Feed reach guard"); g.Players.RestorePosition(p);
                    Coop.FeedButton.onClick.Invoke(); g.FeedChickens(); Require(g.Inventory.GetCount("animal_feed") == 0 && g.Animals.State.Unfed == 0 && g.Animals.State.Eggs == 0, "Batch feed once per bird and no instant eggs"); TextCheck(Coop); g.CloseAnimalPanel(); g.ReturnToMainMenu(); Phase(8);
                }
                else if (phase == 8 && g.State == GameFlowState.MainMenu)
                { Require(g.Animals.State == null && g.Animals.Coop == null && g.Animals.Chickens.Count == 0, "Animal menu cleanup"); g.ContinueGame(); Phase(9); }
                else if (phase == 9 && g.State == GameFlowState.Playing)
                { Require(g.Animals.State.Built && g.Animals.Chickens.Count == 2 && g.Animals.State.Unfed == 0 && g.Animals.State.Eggs == 0 && g.Economy.Gold == 680, "Fed-day reload prevents duplicate feed/purchase"); Door(g); g.EnterHome(); Phase(10); }
                else if (phase == 10 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Home(g, -1.5f, 2); g.RequestRest(); Phase(11); }
                else if (phase == 11 && UnityEngine.Object.FindObjectOfType<SleepView>() != null)
                { UnityEngine.Object.FindObjectOfType<SleepView>().ConfirmButton.onClick.Invoke(); g.StartNewDayFromReport(); Require(g.Animals.State.Day == 2 && g.Animals.State.Eggs == 2 && g.Animals.Coop.EggsMarker.activeSelf && g.Animals.State.Unfed == 2 && g.Saves.TryLoad(out var save) && save.Animals.Eggs == 2, "Indoor sleep produces and saves eggs once"); Home(g, 0, -3); g.ExitHome(); Phase(12); }
                else if (phase == 12 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                { g.AdvanceDay(); g.StartNewDayFromReport(); Require(g.Animals.State.Day == 3 && g.Animals.State.Eggs == 2, "Unfed day produces nothing extra"); g.Players.RestorePosition(g.Animals.Config.Position + new Vector3(1.5f, .1f, -1.5f)); g.Time.Clock.Restore(720); Capture(g, "ChickenCoopPreview"); AtCoop(g); g.OpenCoop(); Phase(13); }
                else if (phase == 13 && Coop != null)
                {
                    Require(Coop.CollectButton.interactable && !Coop.FeedButton.interactable, "Ready egg status"); TextCheck(Coop); Capture(g, "CoopPanelPreview"); var slots = g.Inventory.State.Slots.ToArray(); g.Inventory.State.Restore(Enumerable.Repeat(new MagicIsland.Inventory.InventorySlot("wood", 99), g.Inventory.State.Capacity).ToArray()); g.CollectEggs(); Require(g.Animals.State.Eggs == 2 && g.Animals.Coop.EggsMarker.activeSelf, "Full bag retains ready eggs"); g.Inventory.State.Restore(slots);
                    Coop.CollectButton.onClick.Invoke(); g.CollectEggs(); Require(g.Inventory.GetCount("egg") == 2 && g.Animals.State.Eggs == 0 && !g.Animals.Coop.EggsMarker.activeSelf, "Collect complete and repeat cannot duplicate");
                    g.CloseAnimalPanel(); g.ToggleInventory(); var bag = UnityEngine.Object.FindObjectOfType<InventoryView>(); int egg = Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == "egg"); bag.Slots[egg].SelectButton.onClick.Invoke(); Require(!bag.UseSeedButton.gameObject.activeSelf && !bag.UseFoodButton.gameObject.activeSelf && !bag.PlaceButton.gameObject.activeSelf, "Egg inventory does not offer unrelated seed/food/place actions"); TextCheck(bag); g.CloseInventory(); Shop(g); g.OpenAnimalShop(); Phase(14);
                }
                else if (phase == 14 && Supply != null)
                {
                    string tasks = JsonUtility.ToJson(new SaveData { Tasks = g.Tasks.State.Capture() }); Supply.SellButton.onClick.Invoke(); Supply.SellButton.onClick.Invoke(); g.SellEgg(); Require(g.Economy.Gold == 730 && g.Inventory.GetCount("egg") == 0 && JsonUtility.ToJson(new SaveData { Tasks = g.Tasks.State.Capture() }) == tasks, "Egg sale exact revenue and no crop-task progress"); Supply.FeedButton.onClick.Invoke(); g.CloseAnimalPanel(); AtCoop(g); g.OpenCoop(); Phase(15);
                }
                else if (phase == 15 && Coop != null)
                { g.FeedChickens(); Require(g.Inventory.GetCount("animal_feed") == 1 && g.Animals.State.Unfed == 2, "Insufficient group feed charges nothing"); g.CloseAnimalPanel(); Shop(g); g.OpenAnimalShop(); Phase(16); }
                else if (phase == 16 && Supply != null)
                { Supply.FeedButton.onClick.Invoke(); g.CloseAnimalPanel(); AtCoop(g); g.OpenCoop(); Phase(17); }
                else if (phase == 17 && Coop != null)
                { Coop.FeedButton.onClick.Invoke(); Require(g.Animals.State.Unfed == 0 && g.Economy.Gold == 710 && g.Inventory.GetCount("animal_feed") == 0, "Next-day feeding all birds once"); g.ReturnToMainMenu(); Phase(18); }
                else if (phase == 18 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var save), "Animal snapshot available"); SessionState.SetString(Key + ".Good", JsonUtility.ToJson(save)); save.Animals.Chickens[1].Id = save.Animals.Chickens[0].Id; Write(g, save); Require(!g.Saves.TryLoad(out _), "Duplicate chicken IDs reject both files");
                    save = JsonUtility.FromJson<SaveData>(SessionState.GetString(Key + ".Good", "")); save.Animals.Day++; Write(g, save); Require(!g.Saves.TryLoad(out _), "Animal/farm day mismatch rejected");
                    save = JsonUtility.FromJson<SaveData>(SessionState.GetString(Key + ".Good", "")); save.Version = 19; save.Animals = null;
                    // A real v19 save could only have a crop order from the original pool.
                    save.Order = new MagicIsland.Orders.OrderState(g.Configuration.Orders.Definitions(g.Configuration.Crops), g.Configuration.Seasons.DaysPerSeason, save.Order.Seed, save.Day).Capture();
                    Write(g, save); g.ContinueGame(); Phase(19);
                }
                else if (phase == 19 && g.State == GameFlowState.Playing)
                { Require(!g.Animals.State.Built && g.Animals.State.Count == 0 && g.Animals.State.Day == 3 && g.Animals.Coop == null && g.Economy.Gold == 710 && g.Farm.State.Cells[0].Tilled && g.Housing.Upgrade.Level == 0, "v19 migration grants no animals and preserves world/resources"); g.ReturnToMainMenu(); Phase(20); }
                else if (phase == 20 && g.State == GameFlowState.MainMenu)
                { g.Configuration.Config.FarmSize = 12; SaveDirectory(g, Path.Combine(SessionState.GetString(Key + ".Dir", ""), "MaxFarm")); g.EnterGame(); Phase(21); }
                else if (phase == 21 && g.State == GameFlowState.Playing)
                { Require(g.Animals.Config.Validate(g.Configuration.Items, g.Configuration.Config) && g.Animals.SiteClear(), "Coop site clears max farm/trees/NPC/cottage"); g.Inventory.TryAdd("wood", 15); g.Inventory.TryAdd("stone", 5); g.Economy.Restore(500); Office(g); g.OpenCoop(true); Phase(22); }
                else if (phase == 22 && Coop != null)
                { Coop.BuildButton.onClick.Invoke(); Require(g.Animals.State.Built && g.Animals.Coop != null && g.Animals.ReservesTile(-10, -2) && g.Farm.State.Size == 12 && g.NPCSchedules.Bound, "Maximum farm build and reserved construction site"); g.CloseAnimalPanel(); g.OpenCoop(); g.CloseAnimalPanel(); g.ReturnToMainMenu(); Phase(23); }
                else if (phase == 23 && g.State == GameFlowState.MainMenu)
                { g.Configuration.Animals.ChickenAddress = "MissingAnimalModel"; g.EnterGame(); Phase(25); }
                else if (phase == 25 && g.State == GameFlowState.MainMenu)
                { Require(g.Animals.State == null && g.Animals.Coop == null && g.Animals.Chickens.Count == 0, "AA failure clears partial world and returns to menu"); g.Configuration.Animals.ChickenAddress = "Chicken"; g.EnterGame(); Phase(26); }
                else if (phase == 26 && g.State == GameFlowState.Playing)
                { Require(!g.Animals.State.Built && g.Animals.State.Day == 1, "AA model retry starts a clean world"); g.ReturnToMainMenu(); Phase(27); }
                else if (phase == 27 && g.State == GameFlowState.MainMenu)
                { File.WriteAllText("Logs/AnimalVerification.txt", "PASS: packed AA animal config/coop/chickens/MVC, office and shop navigation, range/modal/reentry, build/purchase/feed/collect/sale atomic costs, saved feed days, indoor sleep production, no-feed days, bag capacity, invalid saves, v19 migration, model cleanup, maximum farm and AA failure/retry."); Debug.Log("Animal flow verification PASSED."); Phase(24); EditorApplication.isPlaying = false; }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogError("Animal phase " + phase + ": " + error); EditorApplication.Exit(1); }
        }
    }
}
