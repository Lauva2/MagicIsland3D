using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Economy;
namespace MagicIsland.Animals
{
    public enum AnimalResult { Success, AlreadyBuilt, NeedsCoop, InsufficientMaterials, InsufficientGold, ChickenLimit, NoChickens, AlreadyFed, MissingFeed, EggsFull, NoEggs, BagFull, GoldLimit, Busy, InvalidDay, InvalidChicken, AlreadyPetted }
    public sealed class AnimalDefinition
    {
        public readonly int Wood, Stone, BuildGold, ChickenPrice, FeedPrice, EggPrice;
        public readonly string FeedId, EggId;
        public readonly int PetAffinity, FeedAffinity, QualityAffinity, QualityChance, QualityEggPrice;
        public readonly string QualityEggId;
        public AnimalDefinition(int wood, int stone, int buildGold, int chickenPrice, int feedPrice, int eggPrice, string feedId = "animal_feed", string eggId = "egg", int petAffinity = 5, int feedAffinity = 2, string qualityEggId = "quality_egg", int qualityEggPrice = 40, int qualityAffinity = 70, int qualityChance = 25)
        { if (qualityEggPrice <= eggPrice || qualityAffinity < 1 || qualityAffinity > 100 || qualityChance < 0 || qualityChance > 100 || string.IsNullOrWhiteSpace(qualityEggId) || qualityEggId == feedId || qualityEggId == eggId || qualityEggId == "wood" || qualityEggId == "stone" || wood < 1 || stone < 1 || buildGold < 1 || chickenPrice < 1 || feedPrice < 1 || eggPrice < 1 || petAffinity < 1 || petAffinity > 100 || feedAffinity < 1 || feedAffinity > 100 || string.IsNullOrWhiteSpace(feedId) || string.IsNullOrWhiteSpace(eggId) || feedId == eggId || feedId == "wood" || feedId == "stone" || eggId == "wood" || eggId == "stone") throw new ArgumentException("Invalid animal definition"); Wood = wood; Stone = stone; BuildGold = buildGold; ChickenPrice = chickenPrice; FeedPrice = feedPrice; EggPrice = eggPrice; FeedId = feedId; EggId = eggId; PetAffinity = petAffinity; FeedAffinity = feedAffinity; QualityEggId = qualityEggId; QualityEggPrice = qualityEggPrice; QualityAffinity = qualityAffinity; QualityChance = qualityChance; }
    }
    [Serializable] public sealed class SavedChicken { public int Id, FedDay, Affinity, LastPetDay; }
    [Serializable] public sealed class SavedAnimals
    { public bool Built; public int Day = 1, Eggs, QualityEggs; public int QualitySeed = 1; public SavedChicken[] Chickens = Array.Empty<SavedChicken>(); }
    public sealed class AnimalState
    {
        public const int MaximumChickens = 2, EggCapacity = 99;
        public const int MaximumAffinity = 100;
        public event Action Changed;
        public AnimalDefinition Definition { get; }
        public bool Built { get; private set; }
        public int Day { get; private set; }
        public int Eggs { get; private set; }
        public int QualityEggs { get; private set; }
        public int TotalEggs => Eggs + QualityEggs;
        public int QualitySeed { get; private set; }
        private SavedChicken[] chickens = Array.Empty<SavedChicken>();
        private bool committing;
        public int Count => chickens.Length;
        public int Unfed => chickens.Count(c => c.FedDay != Day);
        public SavedChicken Chicken(int id) { var value = chickens.FirstOrDefault(c => c.Id == id); return value == null ? null : Copy(value); }
        public static string AffinityStage(int points) => points >= 70 ? "亲近" : points >= 30 ? "熟悉" : "陌生";
        public AnimalState(AnimalDefinition definition, int day = 1, int qualitySeed = 1)
        { Definition = definition ?? throw new ArgumentNullException(nameof(definition)); if (qualitySeed < 1 || day < 1 || day >= int.MaxValue) throw new ArgumentException("Invalid animal day"); Day = day; QualitySeed = qualitySeed; }
        public AnimalResult CheckBuild(InventoryState bag, EconomyState gold) => committing ? AnimalResult.Busy : Built ? AnimalResult.AlreadyBuilt
            : bag.GetCount("wood") < Definition.Wood || bag.GetCount("stone") < Definition.Stone ? AnimalResult.InsufficientMaterials
            : gold.Gold < Definition.BuildGold ? AnimalResult.InsufficientGold : AnimalResult.Success;
        public AnimalResult Build(InventoryState bag, EconomyState gold, bool notify = true)
        {
            var result = CheckBuild(bag, gold); if (result != AnimalResult.Success) return result; committing = true;
            try { bag.TryRemove("wood", Definition.Wood, false); bag.TryRemove("stone", Definition.Stone, false); gold.TrySpend(Definition.BuildGold, false); Built = true; if (notify) { bag.NotifyChanged(); gold.NotifyChanged(); } return AnimalResult.Success; }
            finally { committing = false; }
        }
        public AnimalResult CheckChicken(EconomyState gold) => committing ? AnimalResult.Busy : !Built ? AnimalResult.NeedsCoop : Count >= MaximumChickens ? AnimalResult.ChickenLimit
            : gold.Gold < Definition.ChickenPrice ? AnimalResult.InsufficientGold : AnimalResult.Success;
        public AnimalResult BuyChicken(EconomyState gold, bool notify = true)
        {
            var result = CheckChicken(gold); if (result != AnimalResult.Success) return result; committing = true;
            try { gold.TrySpend(Definition.ChickenPrice, false); int id = Enumerable.Range(1, MaximumChickens).First(i => chickens.All(c => c.Id != i)); chickens = chickens.Concat(new[] { new SavedChicken { Id = id } }).ToArray(); if (notify) gold.NotifyChanged(); return AnimalResult.Success; }
            finally { committing = false; }
        }
        public AnimalResult CheckFeedPurchase(InventoryState bag, EconomyState gold) => committing ? AnimalResult.Busy : gold.Gold < Definition.FeedPrice ? AnimalResult.InsufficientGold : !bag.CanAdd(Definition.FeedId, 1) ? AnimalResult.BagFull : AnimalResult.Success;
        public AnimalResult BuyFeed(InventoryState bag, EconomyState gold, bool notify = true)
        {
            var result = CheckFeedPurchase(bag, gold); if (result != AnimalResult.Success) return result; committing = true;
            try { gold.TrySpend(Definition.FeedPrice, false); bag.TryAdd(Definition.FeedId, 1, false); if (notify) { bag.NotifyChanged(); gold.NotifyChanged(); } return AnimalResult.Success; }
            finally { committing = false; }
        }
        public AnimalResult CheckFeed(InventoryState bag) => committing ? AnimalResult.Busy : !Built ? AnimalResult.NeedsCoop : Count == 0 ? AnimalResult.NoChickens : Unfed == 0 ? AnimalResult.AlreadyFed
            : Day >= int.MaxValue - 1 ? AnimalResult.InvalidDay : TotalEggs + Count > EggCapacity ? AnimalResult.EggsFull : bag.GetCount(Definition.FeedId) < Unfed ? AnimalResult.MissingFeed : AnimalResult.Success;
        public AnimalResult Feed(InventoryState bag, bool notify = true)
        {
            var result = CheckFeed(bag); if (result != AnimalResult.Success) return result; committing = true;
            try
            {
                var unfed = chickens.Where(c => c.FedDay != Day).ToArray(); bag.TryRemove(Definition.FeedId, unfed.Length, false);
                foreach (var c in unfed) { c.FedDay = Day; c.Affinity = Math.Min(MaximumAffinity, c.Affinity + Definition.FeedAffinity); }
                if (notify) { bag.NotifyChanged(); NotifyChanged(); } return AnimalResult.Success;
            }
            finally { committing = false; }
        }
        public AnimalResult CheckPet(int id) => committing ? AnimalResult.Busy : !Built ? AnimalResult.NeedsCoop
            : chickens.All(c => c.Id != id) ? AnimalResult.InvalidChicken : chickens.First(c => c.Id == id).LastPetDay == Day ? AnimalResult.AlreadyPetted : AnimalResult.Success;
        public AnimalResult Pet(int id, bool notify = true)
        {
            var result = CheckPet(id); if (result != AnimalResult.Success) return result; committing = true;
            try
            {
                var chicken = chickens.First(c => c.Id == id); chicken.LastPetDay = Day;
                chicken.Affinity = Math.Min(MaximumAffinity, chicken.Affinity + Definition.PetAffinity);
                if (notify) NotifyChanged(); return AnimalResult.Success;
            }
            finally { committing = false; }
        }
        public void NotifyChanged() => Changed?.Invoke();
        public void NextDay(int day)
        {
            if (committing || day != (long)Day + 1 || day >= int.MaxValue) throw new InvalidOperationException("Invalid animal settlement day");
            int yield = chickens.Count(c => c.FedDay == Day); if (TotalEggs + yield > EggCapacity) throw new InvalidOperationException("Animal egg capacity exceeded");
            foreach (var c in chickens.Where(c => c.FedDay == Day))
            {
                uint roll; unchecked { roll = (uint)QualitySeed ^ (uint)Day * 0x9e3779b9u ^ (uint)c.Id * 0x85ebca6bu;
                    roll ^= roll >> 16; roll *= 0x7feb352du; roll ^= roll >> 15; roll *= 0x846ca68bu; roll ^= roll >> 16; }
                if (c.Affinity >= Definition.QualityAffinity && roll % 100 < Definition.QualityChance) QualityEggs++; else Eggs++;
            }
            Day = day;
        }
        private IngredientCost[] Products()
        {
            var values = new System.Collections.Generic.List<IngredientCost>();
            if (Eggs > 0) values.Add(new IngredientCost(Definition.EggId, Eggs));
            if (QualityEggs > 0) values.Add(new IngredientCost(Definition.QualityEggId, QualityEggs));
            return values.ToArray();
        }
        public AnimalResult CheckCollect(InventoryState bag) => committing ? AnimalResult.Busy : TotalEggs == 0 ? AnimalResult.NoEggs : !bag.CanAddAll(Products()) ? AnimalResult.BagFull : AnimalResult.Success;
        public AnimalResult Collect(InventoryState bag, bool notify = true)
        {
            var result = CheckCollect(bag); if (result != AnimalResult.Success) return result; committing = true;
            try { bag.TryAddAll(Products(), false); Eggs = QualityEggs = 0; if (notify) bag.NotifyChanged(); return AnimalResult.Success; }
            finally { committing = false; }
        }
        public AnimalResult CheckSell(InventoryState bag, EconomyState gold, bool quality = false) => committing ? AnimalResult.Busy : bag.GetCount(quality ? Definition.QualityEggId : Definition.EggId) < 1 ? AnimalResult.NoEggs : !gold.CanEarn(quality ? Definition.QualityEggPrice : Definition.EggPrice) ? AnimalResult.GoldLimit : AnimalResult.Success;
        public AnimalResult Sell(InventoryState bag, EconomyState gold, bool notify = true, bool quality = false)
        {
            var result = CheckSell(bag, gold, quality); if (result != AnimalResult.Success) return result; committing = true;
            try { bag.TryRemove(quality ? Definition.QualityEggId : Definition.EggId, 1, false); gold.TryEarn(quality ? Definition.QualityEggPrice : Definition.EggPrice, false); if (notify) { bag.NotifyChanged(); gold.NotifyChanged(); } return AnimalResult.Success; }
            finally { committing = false; }
        }
        private static SavedChicken Copy(SavedChicken c) => new SavedChicken { Id = c.Id, FedDay = c.FedDay, Affinity = c.Affinity, LastPetDay = c.LastPetDay };
        public SavedAnimals Capture() => new SavedAnimals { Built = Built, Day = Day, Eggs = Eggs, QualityEggs = QualityEggs, QualitySeed = QualitySeed, Chickens = chickens.Select(Copy).ToArray() };
        public static bool Valid(SavedAnimals s, int day) => s != null && s.Day == day && day > 0 && day < int.MaxValue && s.QualitySeed > 0 && s.Eggs >= 0 && s.QualityEggs >= 0 && s.Eggs <= EggCapacity && s.QualityEggs <= EggCapacity && (long)s.Eggs + s.QualityEggs <= EggCapacity
            && s.Chickens != null && s.Chickens.Length <= MaximumChickens && (s.Built || ((s.Eggs == 0 && s.QualityEggs == 0) && s.Chickens.Length == 0))
            && ((s.Eggs == 0 && s.QualityEggs == 0) || s.Chickens.Length > 0)
            && s.Chickens.All(c => c != null && c.Id >= 1 && c.Id <= MaximumChickens && c.FedDay >= 0 && c.FedDay <= day
                && c.Affinity >= 0 && c.Affinity <= MaximumAffinity && c.LastPetDay >= 0 && c.LastPetDay <= day && (c.LastPetDay == 0 || c.Affinity > 0))
            && s.Chickens.Select(c => c.Id).Distinct().Count() == s.Chickens.Length && (long)s.Eggs + s.QualityEggs + s.Chickens.Count(c => c.FedDay == day) <= EggCapacity;
        public void Restore(SavedAnimals saved)
        { if (committing || !Valid(saved, Day)) throw new ArgumentException("Invalid saved animals"); Built = saved.Built; Eggs = saved.Eggs; QualityEggs = saved.QualityEggs; QualitySeed = saved.QualitySeed; chickens = saved.Chickens.Select(Copy).ToArray(); }
    }
}
