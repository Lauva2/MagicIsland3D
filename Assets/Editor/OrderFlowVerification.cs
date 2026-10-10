using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Orders;
using MagicIsland.Saving;
using MagicIsland.Tasks;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class OrderFlowVerification
    {
        private const string Running = "MagicIsland.OrderVerification";
        static OrderFlowVerification() { if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            PrototypeAddressablesSetup.BuildContent(); var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(builder => builder is BuildScriptPackedPlayMode);
            SessionState.SetBool(Running, true); SessionState.SetInt(Running + ".Phase", 0);
            SessionState.SetFloat(Running + ".Started", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Phase(int value) => SessionState.SetInt(Running + ".Phase", value);
        private static OrderView View => UnityEngine.Object.FindObjectOfType<OrderView>();
        private static void SaveDirectory(GameManager game, string directory) => game.Saves.Initialize(game.Configuration.Config,
            game.Configuration.Items, game.Configuration.Crops, directory, game.Configuration.Tasks, game.Configuration.Orders, game.Configuration.Seasons, gathering: game.Configuration.Gathering, toolUpgrades: game.Configuration.ToolUpgrades, storage: game.Configuration.Storage);
        private static void Capture(GameManager game, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true);
        }
        private static void CheckText(OrderView view)
        {
            foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                text.ForceMeshUpdate(); Require(!text.isTextOverflowing, "Order text fits: " + text.name);
                Require(text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Order Chinese glyphs baked");
            }
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            int phase = SessionState.GetInt(Running + ".Phase", 0);
            if (phase == 10 && !EditorApplication.isPlaying) { SessionState.SetBool(Running, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Running + ".Started", 0) > 150) throw new Exception("Order flow timeout at " + phase);
                if (!EditorApplication.isPlaying) return;
                var game = GameManager.Instance; if (game == null) return;
                Require(game.State != GameFlowState.Failed, "Game initialization");
                if (phase == 0 && game.State == GameFlowState.MainMenu)
                {
                    var catalog = game.Configuration.Orders;
                    Require(catalog != null && catalog.Validate(game.Configuration.Crops, game.Configuration.Items), "OrderCatalog packed AA before menu");
                    int price = catalog.Offers[0].GoldPerItem; catalog.Offers[0].GoldPerItem = 25;
                    Require(!catalog.Validate(game.Configuration.Crops, game.Configuration.Items), "Order reward must beat shop sale"); catalog.Offers[0].GoldPerItem = price;
                    game.Configuration.Seasons.SpringRainChance = 0;
                    string directory = Path.GetFullPath("Logs/OrderSaves/" + Guid.NewGuid().ToString("N"));
                    SessionState.SetString(Running + ".Directory", directory); SaveDirectory(game, directory); game.EnterGame(); Phase(1);
                }
                else if (phase == 1 && game.State == GameFlowState.Playing)
                {
                    Require(game.Orders.Hall != null && game.Orders.Board != null && game.Orders.Hall.name.StartsWith("OrderHall"), "OrderHall instantiated from AA");
                    Require(game.Orders.Board.transform.localPosition.z < -2 && game.Orders.Board.transform.localPosition.x > 1, "Noticeboard in front, clear of doorway");
                    var board = game.Orders.Board.transform; var point = board.TransformPoint(new Vector3(0, 1.28f, -0.17f));
                    var ray = new Ray(point + Vector3.back * 3, Vector3.forward); Physics.SyncTransforms();
                    Require(game.Interactions.Controller.RefreshOrderBoardHover(ray, false) && game.Interactions.Controller.HoverIndex == -1
                        && game.Interactions.Controller.Hint.Contains("太远"), "Distant board hover consumes ray without farm action");
                    game.OpenOrder(); Require(!game.UI.HasOrder && game.Players.Player.enabled, "Distant board cannot open");
                    game.Players.RestorePosition(board.position + new Vector3(-1, 0.1f, -1.2f));
                    Require(game.Interactions.Controller.RefreshOrderBoardHover(ray, false) && game.Interactions.Controller.Hint.Contains("今日订单")
                        && !game.Interactions.Controller.RefreshOrderBoardHover(ray, true), "Nearby hint and UI interception");
                    game.Tasks.State.Record(TaskAction.Hoe); game.ClaimTaskReward(); Require(game.Economy.Gold == 105, "Existing tutorial reward remains functional");
                    Camera camera = game.Players.Player.ViewCamera; var position = camera.transform.position; var rotation = camera.transform.rotation;
                    camera.transform.position = game.Orders.Hall.transform.position + new Vector3(-5, 6, -9);
                    camera.transform.LookAt(game.Orders.Hall.transform.position + Vector3.up); Capture(game, "OrderHallPreview");
                    camera.transform.SetPositionAndRotation(position, rotation);
                    game.OpenOrder(); game.OpenOrder(); Phase(2);
                }
                else if (phase == 2 && View != null)
                {
                    Require(View.Layer == UILayer.PopUp && View.transform.parent == game.UI.GetLayerRoot(UILayer.PopUp)
                        && UnityEngine.Object.FindObjectsOfType<OrderView>().Length == 1, "OrderPanel AA, layer and duplicate-load guard");
                    Require(!game.Players.Player.enabled && !game.Interactions.Controller.enabled && !View.DeliverButton.interactable, "Order modal locks movement/world and missing stock");
                    CheckText(View); Capture(game, "OrderPanelPreview");
                    double minute = game.Time.Clock.Minute; game.Time.Tick(10, game.UI.HasOrder); game.AdvanceDay(); game.ToggleInventory(); game.OpenShop(); game.OpenSleep(); game.ClaimTaskReward();
                    Require(game.Time.Clock.Minute == minute && game.Farm.State.Day == 1 && !game.UI.IsInventoryVisible && !game.UI.HasShop && !game.UI.HasSleep
                        && game.Economy.Gold == 105, "Order modal freezes time/day and blocks other actions");
                    game.DeliverOrder(); Require(!game.Orders.State.Fulfilled && game.Economy.Gold == 105, "Insufficient-stock delivery unchanged");
                    var order = game.Orders.State; game.Inventory.State.TryAdd(order.ItemId, order.Quantity + 1);
                    game.Economy.Restore(long.MaxValue); game.DeliverOrder(); Require(!order.Fulfilled && game.Inventory.GetCount(order.ItemId) == order.Quantity + 1, "Gold limit preserves order/items");
                    game.Economy.Restore(105); game.UI.RefreshViews(); Require(View.DeliverButton.interactable, "MVC reacts to inventory/gold");
                    string directory = SessionState.GetString(Running + ".Directory", ""); string blocked = Path.Combine(directory, "blocked"); File.WriteAllText(blocked, "not a directory");
                    SaveDirectory(game, blocked); View.DeliverButton.onClick.Invoke(); long gold = 105 + order.Reward;
                    Require(order.Fulfilled && game.Economy.Gold == gold && game.Inventory.GetCount(order.ItemId) == 1 && game.Orders.Feedback.Contains("保存失败"), "Failed save retains committed transaction and warns");
                    game.DeliverOrder(); Require(game.Economy.Gold == gold, "No repeat reward after failed save");
                    SaveDirectory(game, directory); Require(game.SaveProgress() && game.Saves.TryLoad(out SaveData data) && data.Order.Fulfilled && data.Gold == gold
                        && data.Tasks[0].Claimed && data.Order.Quantity == order.Quantity && data.Version == SaveData.CurrentVersion, "Save retry captures order/items/gold/tasks together");
                    game.UI.RefreshViews(); Require(!View.DeliverButton.interactable && View.StatusText.text.Contains("已完成"), "Fulfilled panel"); CheckText(View); Capture(game, "OrderDeliveredPreview");
                    SessionState.SetString(Running + ".Order", JsonUtility.ToJson(order.Capture())); SessionState.SetString(Running + ".Gold", gold.ToString());
                    View.CloseButton.onClick.Invoke(); Require(!game.UI.HasOrder && game.Players.Player.enabled && game.Interactions.Controller.enabled, "Close releases panel and input");
                    game.ReturnToMainMenu(); Phase(3);
                }
                else if (phase == 3 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Orders.State == null && game.Orders.Hall == null && game.Orders.Board == null && View == null, "AA hall/order/UI lifecycle cleanup");
                    Require(game.Saves.TryLoad(out SaveData data), "Read completed order"); string original = JsonUtility.ToJson(data);
                    data.Order.Reward++; string invalid = JsonUtility.ToJson(data); File.WriteAllText(game.Saves.SavePath, invalid); File.WriteAllText(game.Saves.SavePath + ".bak", invalid);
                    game.ContinueGame(); Require(game.State == GameFlowState.MainMenu && !game.Saves.HasSave, "Invalid reward rejected before world initialization");
                    File.WriteAllText(game.Saves.SavePath, original); File.WriteAllText(game.Saves.SavePath + ".bak", original); game.ContinueGame(); Phase(4);
                }
                else if (phase == 4 && game.State == GameFlowState.Playing)
                {
                    Require(JsonUtility.ToJson(game.Orders.State.Capture()) == SessionState.GetString(Running + ".Order", "")
                        && game.Economy.Gold == long.Parse(SessionState.GetString(Running + ".Gold", "0")), "Reload exact order and no repeat reward");
                    game.OpenOrder(); Phase(5);
                }
                else if (phase == 5 && View != null)
                {
                    game.DeliverOrder(); Require(game.Economy.Gold == long.Parse(SessionState.GetString(Running + ".Gold", "0")), "Delivery after reload pays nothing");
                    game.CloseOrder(); var previous = game.Orders.State.Capture(); game.AdvanceDay();
                    var expected = new OrderState(game.Configuration.Orders.Definitions(game.Configuration.Crops), game.Calendar.Config.DaysPerSeason, previous.Seed, 2);
                    Require(!game.Orders.State.Fulfilled && game.Orders.State.Day == 2 && JsonUtility.ToJson(expected.Capture()) == JsonUtility.ToJson(game.Orders.State.Capture())
                        && game.Saves.TryLoad(out SaveData data) && data.Order.Day == 2 && !data.Order.Fulfilled, "Sleep refreshes deterministic order and autosaves");
                    game.ReturnToMainMenu(); Phase(6);
                }
                else if (phase == 6 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Saves.TryLoad(out SaveData data), "Read refreshed order"); data.Version = 7; data.Order = null;
                    File.WriteAllText(game.Saves.SavePath, JsonUtility.ToJson(data)); File.WriteAllText(game.Saves.SavePath + ".bak", JsonUtility.ToJson(data));
                    Require(game.Saves.TryLoad(out SaveData legacy) && legacy.Version == SaveData.CurrentVersion && legacy.Order.Day == 2 && legacy.Tasks[0].Claimed
                        && legacy.Gold == data.Gold && !legacy.Order.Fulfilled, "v7 migration preserves economy/tutorial and adds today's unpaid order");
                    game.ContinueGame(); Phase(7);
                }
                else if (phase == 7 && game.State == GameFlowState.Playing)
                {
                    Require(game.Orders.State.Day == 2 && !game.Orders.State.Fulfilled && game.Tasks.State.IsClaimed(0), "Legacy order playable"); game.ReturnToMainMenu(); Phase(8);
                }
                else if (phase == 8 && game.State == GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/OrderFlowVerification.txt", "PASS: packed AA OrderCatalog, OrderHall and OrderPanel; building-front board, range/ray/UI interception.\n"
                        + "MVC/PopUp/Chinese glyphs/layout; modal pause, delivery stock/gold/duplicate guards, save failure recovery, atomic reward persistence.\n"
                        + "Exact order reload, next-day refresh/autosave, corrupt reward rejection, v7 migration preserving tasks/gold, AA lifecycle cleanup.\n");
                    Debug.Log("Order flow verification PASSED."); Phase(10); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Running, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
