using System;
using System.Linq;
using MagicIsland.Animals;
using MagicIsland.Inventory;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyAnimalFriendship
{
    static int count;
    static void Check(bool ok, string why) { count++; if (!ok) throw new Exception(why); }
    static void Reject(Action action) { bool fail = false; try { action(); } catch (ArgumentException) { fail = true; } catch (InvalidOperationException) { fail = true; } Check(fail, "Invalid or reentrant mutation rejected"); }
    static readonly AnimalDefinition definition = new AnimalDefinition(15, 5, 200, 50, 10, 25);
    static readonly ItemDefinition[] items = { new ItemDefinition("wood", "Wood", ItemType.Material, 99), new ItemDefinition("stone", "Stone", ItemType.Material, 99), new ItemDefinition("animal_feed", "Feed", ItemType.Material, 99), new ItemDefinition("egg", "Egg", ItemType.AnimalProduct, 99) };
    static string Snapshot(AnimalState state) { var s = state.Capture(); return s.Built + ":" + s.Day + ":" + s.Eggs + "|" + string.Join("|", s.Chickens.Select(c => c.Id + ":" + c.FedDay + ":" + c.Affinity + ":" + c.LastPetDay)); }
    static AnimalState Two(int day, int points = 0)
    { var s = new AnimalState(definition, day); s.Restore(new SavedAnimals { Built = true, Day = day, Chickens = new[] { new SavedChicken { Id = 1, Affinity = points }, new SavedChicken { Id = 2 } } }); return s; }
    static void Main()
    {
        var state = new AnimalState(definition); var bag = new InventoryState(5, items); var gold = new EconomyState(500);
        Check(state.Pet(1) == AnimalResult.NeedsCoop, "Cannot pet before construction"); bag.TryAdd("wood", 15); bag.TryAdd("stone", 5);
        Check(state.Build(bag, gold) == AnimalResult.Success && state.Pet(1) == AnimalResult.InvalidChicken, "Empty coop cannot pet an absent bird");
        state.BuyChicken(gold); Check(state.Chicken(1).Affinity == 0 && state.Chicken(1).LastPetDay == 0, "New bird has no free friendship");
        Check(state.Pet(1) == AnimalResult.Success && state.Chicken(1).Affinity == 5 && state.Chicken(1).LastPetDay == 1, "First pet adds five and records current day");
        Check(state.Pet(1) == AnimalResult.AlreadyPetted && state.Chicken(1).Affinity == 5, "Second pet on same day adds nothing");
        bag.TryAdd("animal_feed", 2); Check(state.Feed(bag) == AnimalResult.Success && state.Chicken(1).Affinity == 7, "First feeding adds two");
        state.BuyChicken(gold); Check(state.Chicken(2).Affinity == 0 && state.Feed(bag) == AnimalResult.Success && state.Chicken(1).Affinity == 7 && state.Chicken(2).Affinity == 2, "New bird feeding never rewards already fed older bird");
        Check(state.Pet(2) == AnimalResult.Success && state.Chicken(2).Affinity == 7 && state.Feed(bag) == AnimalResult.AlreadyFed, "Each bird has independent pet and feed limits");
        state.NextDay(2); Check(state.Eggs == 2 && state.CheckPet(1) == AnimalResult.Success && state.CheckPet(2) == AnimalResult.Success && state.Unfed == 2, "Cross-day resets eligibility while preserving friendship and eggs");
        for (int points = 0; points <= 100; points++) foreach (int id in new[] { 1, 2 })
        {
            var s = Two(3); var snapshot = s.Capture(); snapshot.Chickens[id - 1].Affinity = points; s.Restore(snapshot);
            var inventory = new InventoryState(2, items); inventory.TryAdd("animal_feed", 1); int events = 0;
            Action observer = () => { events++; Check(s.Chicken(id).Affinity == Math.Min(100, points + 5) && s.Chicken(id).LastPetDay == 3, "Pet observers see complete bounded record"); Check(s.Pet(id) == AnimalResult.Busy && s.Pet(3 - id) == AnimalResult.Busy && s.Feed(inventory) == AnimalResult.Busy, "All pet/feed reentry blocked"); Reject(() => s.NextDay(4)); };
            s.Changed += observer; Check(s.Pet(id) == AnimalResult.Success && events == 1 && inventory.GetCount("animal_feed") == 1, "One successful pet event at every affinity boundary including full"); s.Changed -= observer;
            string before = Snapshot(s); Check(s.Pet(id) == AnimalResult.AlreadyPetted && Snapshot(s) == before, "Daily repeated pet is unchanged");
            Check(s.Feed(inventory) == AnimalResult.MissingFeed && Snapshot(s) == before && inventory.GetCount("animal_feed") == 1, "Insufficient group feed awards neither bird");
            inventory.TryAdd("animal_feed", 1); Check(s.Feed(inventory) == AnimalResult.Success && s.Chicken(id).Affinity == Math.Min(100, points + 7) && s.Chicken(3 - id).Affinity == 2, "Feed reward clamps independently and applies only after full cost");
            before = Snapshot(s); Check(s.Feed(inventory) == AnimalResult.AlreadyFed && Snapshot(s) == before, "Repeated feed cannot add friendship");
            var copy = s.Chicken(id); copy.Affinity = 0; copy.LastPetDay = 0; Check(s.Chicken(id).LastPetDay == 3, "Read-only bird query is an independent copy");
            var data = s.Capture(); var restored = Two(3); restored.Restore(data); data.Chickens[id - 1].Affinity = 0; data.Chickens[id - 1].LastPetDay = 0;
            Check(restored.Chicken(id).LastPetDay == 3 && restored.Pet(id) == AnimalResult.AlreadyPetted, "Snapshot and restore preserve independent daily records");
        }
        var invalid = Two(5, 20); string good = Snapshot(invalid);
        foreach (Action<SavedChicken> mutate in new Action<SavedChicken>[] { c => c.Affinity = -1, c => c.Affinity = 101, c => c.LastPetDay = -1, c => c.LastPetDay = 6, c => { c.LastPetDay = 1; c.Affinity = 0; } })
        { var bad = invalid.Capture(); mutate(bad.Chickens[0]); Reject(() => invalid.Restore(bad)); Check(Snapshot(invalid) == good, "Invalid affinity/date does not partially restore"); }
        foreach (int id in new[] { -1, 0, 3, int.MaxValue }) Check(invalid.Pet(id) == AnimalResult.InvalidChicken && Snapshot(invalid) == good, "Unknown stable bird ID rejected");
        Check(AnimalState.AffinityStage(0) == "陌生" && AnimalState.AffinityStage(29) == "陌生" && AnimalState.AffinityStage(30) == "熟悉" && AnimalState.AffinityStage(69) == "熟悉" && AnimalState.AffinityStage(70) == "亲近" && AnimalState.AffinityStage(100) == "亲近", "Exact relationship stages");
        foreach (int bad in new[] { 0, -1, 101, int.MaxValue })
        { Reject(() => new AnimalDefinition(1, 1, 1, 1, 1, 1, petAffinity: bad)); Reject(() => new AnimalDefinition(1, 1, 1, 1, 1, 1, feedAffinity: bad)); }
        var fullEggs = Two(3); var nearCapacity = fullEggs.Capture(); nearCapacity.Eggs = 98; fullEggs.Restore(nearCapacity); var feed = new InventoryState(2, items); feed.TryAdd("animal_feed", 2);
        Check(fullEggs.Feed(feed) == AnimalResult.EggsFull && fullEggs.Chicken(1).Affinity == 0 && feed.GetCount("animal_feed") == 2, "Egg capacity rejection grants no feed points");
        Check(fullEggs.Pet(1) == AnimalResult.Success && fullEggs.Eggs == 98, "Pet does not consume or generate eggs");
        foreach (int version in new[] { 20, 21 })
        {
            var farm = new FarmState(2); for (int i = 1; i < 5; i++) farm.AdvanceDay(); var birds = Two(5); var inventory = new InventoryState(3, items); inventory.TryAdd("animal_feed", 2); birds.Feed(inventory); birds.Pet(1); inventory.TryAdd("egg", 3);
            var save = SaveData.Capture(farm, inventory, 0, 0, 0, 123, stamina: 85, animals: birds); save.Version = version; save.Upgrade(999); save.Validate(2, 3, inventory.CreateEmpty(3));
            Check(save.Version == SaveData.CurrentVersion && save.Animals.Built && save.Animals.Chickens.All(c => c.FedDay == 5 && c.Affinity == 0 && c.LastPetDay == 0) && save.Slots.Any(s => s.ItemId == "egg" && s.Quantity == 3) && save.Gold == 123 && save.Stamina == 85, "Legacy migration retains feeding/products/resources, awards no retroactive friendship");
            var restored = new AnimalState(definition, 5); restored.Restore(save.Animals);
            Check(restored.Feed(inventory) == AnimalResult.AlreadyFed && restored.Pet(1) == AnimalResult.Success && restored.Chicken(1).Affinity == 5, "Old fed day cannot reward again, but first new pet is available");
            save.Animals = restored.Capture(); save.Upgrade(999); Check(save.Animals.Chickens[0].Affinity == 5 && save.Animals.Chickens[0].LastPetDay == 5, "Current migration never erases earned friendship");
        }
        var daily = Two(1); var dailyBag = new InventoryState(3, items);
        for (int day = 1; day <= 20; day++)
        { dailyBag.TryAdd("animal_feed", 2); daily.Feed(dailyBag); daily.Pet(1); daily.Pet(2); Check(daily.Chicken(1).Affinity == Math.Min(100, day * 7) && daily.Chicken(2).Affinity == Math.Min(100, day * 7), "Daily pet and feed progression clamps at 100"); daily.NextDay(day + 1); }
        Check(daily.TotalEggs == 40 && daily.Chicken(1).Affinity == 100 && daily.CheckPet(1) == AnimalResult.Success, "Friendship preserves total fed daily egg output and allows affection at full");
        Console.WriteLine("PASS: " + count + " animal friendship/feed/daily/atomic/migration assertions");
    }
}
