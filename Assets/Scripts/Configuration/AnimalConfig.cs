using MagicIsland.Animals;
using MagicIsland.Inventory;
using System.Linq;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Animal Config")]
    public sealed class AnimalConfig : ScriptableObject
    {
        public const string Address = "AnimalConfig";
        public string CoopAddress = "ChickenCoop", ChickenAddress = "Chicken", FeedItemId = "animal_feed", EggItemId = "egg";
        public Vector3 Position = new Vector3(-10, 0, -1.8f);
        public int BuildWood = 15, BuildStone = 5, BuildGold = 200, ChickenPrice = 50, FeedPrice = 10, EggPrice = 25;
        public int PetAffinity = 5, FeedAffinity = 2;
        public string QualityEggItemId = "quality_egg";
        public int QualityEggPrice = 40, QualityAffinity = 70, QualityChance = 25;
        public string[] ChickenNames = { "小白", "团团" };
        public string ChickenName(int id) => ChickenNames[id - 1];
        public AnimalDefinition Definition() => new AnimalDefinition(BuildWood, BuildStone, BuildGold, ChickenPrice, FeedPrice, EggPrice, FeedItemId, EggItemId, PetAffinity, FeedAffinity, QualityEggItemId, QualityEggPrice, QualityAffinity, QualityChance);
        public bool Validate(ItemCatalog items, GameConfig game)
        {
            if (items == null || game == null || string.IsNullOrWhiteSpace(CoopAddress) || string.IsNullOrWhiteSpace(ChickenAddress)
                || ChickenNames == null || ChickenNames.Length != AnimalState.MaximumChickens || ChickenNames.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 12 || n.Any(char.IsControl)) || ChickenNames.Distinct().Count() != AnimalState.MaximumChickens
                || !Finite(Position.x) || !Finite(Position.z) || Position.y != 0 || Mathf.Abs(Position.x) > 10.8f || Mathf.Abs(Position.z) > 10.5f
                || (Mathf.Abs(Position.x) < game.FarmSize * .6f + 1.5f && Mathf.Abs(Position.z) < game.FarmSize * .6f + 1.7f)
                || items.Find("wood")?.Type != ItemType.Material || items.Find("stone")?.Type != ItemType.Material
                || items.Find(FeedItemId)?.Type != ItemType.Material || items.Find(EggItemId)?.Type != ItemType.AnimalProduct || items.Find(QualityEggItemId)?.Type != ItemType.AnimalProduct) return false;
            try { Definition(); return true; } catch (System.ArgumentException) { return false; }
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
