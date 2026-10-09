using System;
using System.IO;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Saving;

internal static class VerifySave
{
    private static int checks;
    private static readonly ItemDefinition[] Items = { new ItemDefinition("seed", "Seed", ItemType.Seed, 99), new ItemDefinition("crop", "Crop", ItemType.Crop, 99) };
    private static InventoryState Bag() => new InventoryState(4, Items);
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }

    public static int Main()
    {
        var farm = new FarmState(3, 3);
        var bag = Bag();
        bag.Restore(new[] { new InventorySlot("crop", 7), default, new InventorySlot("seed", 12), default });
        farm.TryAct(4, FarmTool.Hoe, out _);
        farm.TryAct(4, FarmTool.Seeds, out _);
        farm.TryAct(4, FarmTool.WateringCan, out _);
        farm.AdvanceDay();
        farm.TryAct(4, FarmTool.WateringCan, out _);
        SaveData data = SaveData.Capture(farm, bag, 2f, 0.1f, -4f);
        farm.AdvanceDay();
        bag.TryRemove("seed", 2);
        Check(data.Day == 2 && data.Cells[4].Growth == 1 && data.Cells[4].Watered && data.Slots[2].Quantity == 12, "Snapshot is independent of later changes");
        data.Validate(3, 3, Bag());
        var resumed = new FarmState(3, 3);
        resumed.Restore(data.Day, data.Cells);
        var restored = Bag();
        var slots = new InventorySlot[4];
        for (int i = 0; i < slots.Length; i++) slots[i] = new InventorySlot(data.Slots[i].ItemId, data.Slots[i].Quantity);
        restored.Restore(slots);
        Check(restored.Slots[0].ItemId == "crop" && restored.Slots[1].Empty && restored.Slots[2].Quantity == 12, "Slot order and holes preserved");
        Check(resumed.Day == 2 && resumed.Cells[4].Watered && resumed.Cells[4].Growth == 1, "Farm flags restored");
        resumed.AdvanceDay();
        Check(resumed.Cells[4].Growth == 2 && !resumed.Cells[4].Watered, "Restored crop continues normal growth");
        data.Version = 99;
        Reject(() => data.Validate(3, 3, Bag()), "Future versions rejected");
        data.Version = SaveData.CurrentVersion;
        Reject(() => data.Validate(4, 3, Bag()), "Changed farm shape rejected");
        Reject(() => data.Validate(3, 4, Bag()), "Changed growth rules rejected");
        data.PlayerX = float.NaN;
        Reject(() => data.Validate(3, 3, Bag()), "Nonfinite position rejected");
        data.PlayerX = 2;
        data.PlayerX = 11.6f;
        data.Validate(3, 3, Bag());
        Check(true, "Player beside island boundary can save");
        data.PlayerX = 100;
        Reject(() => data.Validate(3, 3, Bag()), "Position outside island rejected");
        data.PlayerX = 2;
        data.Slots[2].Quantity = 100;
        Reject(() => data.Validate(3, 3, Bag()), "Oversized stacks rejected");
        data.Slots[2].Quantity = 12;
        data.Slots[0].ItemId = "unknown";
        Reject(() => data.Validate(3, 3, Bag()), "Unknown item rejected");
        data.Slots[0].ItemId = "crop";
        data.Cells[4].Tilled = false;
        Reject(() => data.Validate(3, 3, Bag()), "Invalid crop state rejected");
        data.Cells[4].Tilled = true;
        data.Day = 0;
        Reject(() => data.Validate(3, 3, Bag()), "Invalid day rejected");
        data.Day = 2;
        int notifications = 0;
        restored.Changed += () => notifications++;
        slots[3] = new InventorySlot("unknown", 1);
        Reject(() => restored.Restore(slots), "Atomic restoration rejects bad final slot");
        Check(restored.GetCount("seed") == 12 && restored.GetCount("crop") == 7 && notifications == 0, "Rejected restoration changes no slots or events");
        data.Cells[8].Growth = -1;
        Reject(() => resumed.Restore(9, data.Cells), "Invalid final cell rejected");
        Check(resumed.Day == 3 && resumed.Cells[4].Growth == 2, "Rejected farm restoration leaves old state intact");

        string directory = Path.GetFullPath("Logs/SaveStoreTest/" + Guid.NewGuid().ToString("N"));
        var store = new SaveFileStore(directory);
        Check(!store.TryRead(int.Parse, out int _, out _, out _), "Missing save is normal");
        store.Write("1");
        store.Write("2");
        Check(File.ReadAllText(store.FilePath) == "2" && File.ReadAllText(store.BackupPath) == "1", "Atomic overwrite retains previous backup");
        Check(!File.Exists(store.FilePath + ".tmp"), "No temporary file remains after commit");
        File.WriteAllText(store.FilePath, "broken");
        Check(store.TryRead(int.Parse, out int recovered, out _, out bool backup) && recovered == 1 && backup, "Corrupt primary recovers backup");
        File.WriteAllText(store.BackupPath, "broken");
        Check(!store.TryRead(int.Parse, out _, out string error, out _) && error != null, "Two corrupt files report failure");
        Console.WriteLine("PASS: " + checks + " save assertions");
        return 0;
    }
}
