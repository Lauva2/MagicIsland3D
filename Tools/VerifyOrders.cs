using System;
using System.Linq;
using MagicIsland.CalendarSystem;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Orders;
using MagicIsland.Saving;
using MagicIsland.Tasks;

internal static class VerifyOrders
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid order rejected"); }
    static readonly OrderDefinition[] definitions = {
        new OrderDefinition("turnip_daily", "turnip", SeasonMask.Spring | SeasonMask.Autumn | SeasonMask.Winter, 1, 3, 40),
        new OrderDefinition("tomato_daily", "tomato", SeasonMask.Summer | SeasonMask.Winter, 1, 3, 75),
        new OrderDefinition("corn_daily", "corn", SeasonMask.Summer | SeasonMask.Autumn | SeasonMask.Winter, 1, 3, 130) };
    static readonly ItemDefinition[] items = {
        new ItemDefinition("turnip_seed", "seed", ItemType.Seed, 99), new ItemDefinition("turnip", "crop", ItemType.Crop, 99),
        new ItemDefinition("tomato", "crop", ItemType.Crop, 99), new ItemDefinition("corn", "crop", ItemType.Crop, 99) };
    static bool Same(SavedOrder a, SavedOrder b) => a.Seed == b.Seed && a.Day == b.Day && a.DefinitionId == b.DefinitionId
        && a.ItemId == b.ItemId && a.Quantity == b.Quantity && a.Reward == b.Reward && a.Fulfilled == b.Fulfilled;
    public static int Main()
    {
        Reject(() => new OrderDefinition("", "x", SeasonMask.All, 1, 3, 40));
        Reject(() => new OrderDefinition("x", "", SeasonMask.All, 1, 3, 40));
        Reject(() => new OrderDefinition("x", "x", SeasonMask.None, 1, 3, 40));
        Reject(() => new OrderDefinition("x", "x", (SeasonMask)16, 1, 3, 40));
        Reject(() => new OrderDefinition("x", "x", SeasonMask.All, 0, 3, 40));
        Reject(() => new OrderDefinition("x", "x", SeasonMask.All, 3, 1, 40));
        Reject(() => new OrderDefinition("x", "x", SeasonMask.All, 1, 100, 40));
        Reject(() => new OrderDefinition("x", "x", SeasonMask.All, 1, 3, 0));
        Reject(() => new OrderState(Array.Empty<OrderDefinition>(), 28, 1, 1));
        Reject(() => new OrderState(new[] { definitions[0] }, 28, 1, 1));
        Reject(() => new OrderState(new[] { definitions[0], definitions[0] }, 28, 1, 1));
        Reject(() => new OrderState(definitions, 28, 0, 1)); Reject(() => new OrderState(definitions, 28, 1, 0));
        Reject(() => new OrderState(definitions, 0, 1, 1));
        for (int seed = 1; seed <= 12; seed++) for (int day = 1; day <= 225; day++)
        {
            var a = new OrderState(definitions, 28, seed, day); var b = new OrderState(definitions, 28, seed, day);
            var definition = definitions.Single(value => value.Id == a.Capture().DefinitionId);
            Check(Same(a.Capture(), b.Capture()) && a.Quantity >= 1 && a.Quantity <= 3 && a.Reward == a.Quantity * definition.GoldPerItem
                && Seasons.Includes(definition.Seasons, new CalendarDate(day, 28).Season), "Deterministic seasonal order and quantity");
        }
        var state = new OrderState(definitions, 28, 1234, 1); var bag = new InventoryState(4, items); var gold = new EconomyState(100);
        int events = 0; state.Changed += () => events++;
        Check(state.Deliver(bag, gold) == DeliveryResult.InsufficientItems && gold.Gold == 100 && !state.Fulfilled && events == 0, "Missing stock changes nothing");
        bag.TryAdd(state.ItemId, state.Quantity + 2);
        var capped = new EconomyState(long.MaxValue);
        Check(state.Deliver(bag, capped) == DeliveryResult.GoldLimit && !state.Fulfilled && bag.GetCount(state.ItemId) == state.Quantity + 2, "Overflow preserves stock and flag");
        DeliveryResult nested = DeliveryResult.Success;
        Action observer = () => { Check(state.Fulfilled && gold.Gold == 100 + state.Reward && bag.GetCount(state.ItemId) == 2, "Observers see committed transaction"); nested = state.Deliver(bag, gold); };
        bag.Changed += observer;
        Check(state.Deliver(bag, gold) == DeliveryResult.Success && nested == DeliveryResult.Busy && events == 1, "Delivery once and guarded reentry");
        bag.Changed -= observer;
        long balance = gold.Gold;
        Check(state.Deliver(bag, gold) == DeliveryResult.AlreadyFulfilled && gold.Gold == balance && bag.GetCount(state.ItemId) == 2, "Duplicate delivery pays nothing");
        var saved = state.Capture(); var restored = new OrderState(definitions, 28, 8, 1); restored.Restore(saved, 1); saved.Fulfilled = false;
        Check(restored.Fulfilled && restored.Deliver(bag, gold) == DeliveryResult.AlreadyFulfilled, "Restored fulfilled flag independent and paid once");
        state.NextDay(2); restored.NextDay(2); Check(Same(state.Capture(), restored.Capture()) && !state.Fulfilled, "Future order preserved through reload");
        Reject(() => state.NextDay(4)); Reject(() => state.NextDay(2));
        var before = state.Capture();
        Action<Action<SavedOrder>> bad = mutate => { var copy = state.Capture(); mutate(copy); Reject(() => state.Restore(copy, 2)); Check(Same(before, state.Capture()), "Invalid restore is atomic"); };
        bad(value => value.Day = 3); bad(value => value.Seed = 0); bad(value => value.DefinitionId = "other");
        bad(value => value.ItemId = "unknown"); bad(value => value.Quantity = 0); bad(value => value.Reward++);
        Reject(() => state.Restore(null, 2));
        var fixedQuantity = new OrderState(new[] { new OrderDefinition("big", "turnip", SeasonMask.All, 99, 99, int.MaxValue) }, 1, 1, 1);
        Check(fixedQuantity.Reward == (long)99 * int.MaxValue, "Reward calculation uses long");
        var farm = new FarmState(2); var tasks = new[] { new TaskDefinition("hoe", TaskAction.Hoe, 1, 5) };
        var taskState = new TaskState(tasks); taskState.Record(TaskAction.Hoe); taskState.TryClaim(gold);
        var order = new OrderState(definitions, 28, 1, 1); var snapshot = SaveData.Capture(farm, bag, 0, 0, 0, gold.Gold, tasks: taskState, orders: order);
        snapshot.Validate(2, 3, new InventoryState(4, items), tasks: tasks, orders: definitions);
        Check(snapshot.Version == SaveData.CurrentVersion && Same(snapshot.Order, order.Capture()), "Save captures order");
        var persisted = new OrderState(definitions, 28, 3, 1); persisted.Restore(snapshot.Order, 1); persisted.NextDay(2);
        order.NextDay(2); Check(Same(persisted.Capture(), order.Capture()), "Saved seed preserves refresh");
        for (int version = 1; version <= 7; version++)
        {
            var legacy = SaveData.Capture(farm, bag, 0, 0, 0, 87, tasks: taskState); legacy.Version = version;
            legacy.Upgrade(100, tasks: tasks, orders: definitions); legacy.Validate(2, 3, new InventoryState(4, items), tasks: tasks, orders: definitions);
            Check(legacy.Version == SaveData.CurrentVersion && legacy.Order.Day == 1 && !legacy.Order.Fulfilled && legacy.Gold == (version == 1 ? 100 : 87)
                && legacy.Tasks[0].Claimed == (version == 7), "Legacy migration preserves economy and existing task rewards");
        }
        snapshot.Upgrade(999, tasks: tasks, orders: definitions); Check(snapshot.Gold == gold.Gold && snapshot.Tasks[0].Claimed, "Current upgrade preserves progress");
        Console.WriteLine("PASS: " + checks + " daily order/delivery/save assertions"); return 0;
    }
}
