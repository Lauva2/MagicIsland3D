using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.FishingSystem;
using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [Serializable] public sealed class FishConfig { public string ItemId; public int Weight = 1; }
    [CreateAssetMenu(menuName = "Magic Island/Fishing Catalog")]
    public sealed class FishingCatalog : ScriptableObject
    {
        public const string Address = "FishingCatalog";
        public string RodAddress = "FishingRod", SpotAddress = "CoastalFishingSpot";
        public Vector3 SpotPosition = new Vector3((float)MagicIsland.Interaction.IslandLayout.PierX, 0, (float)MagicIsland.Interaction.IslandLayout.PierZ);
        public float WaitMin = 2, WaitMax = 5, BiteWindow = 2;
        public int StaminaCost = 5;
        public List<FishConfig> Fish = new List<FishConfig>();
        public IEnumerable<FishDefinition> Definitions() => Fish.Select(f => new FishDefinition(f.ItemId, f.Weight));
        public bool Validate(ItemCatalog items, GameConfig game)
        {
            if (Fish == null || string.IsNullOrWhiteSpace(RodAddress) || string.IsNullOrWhiteSpace(SpotAddress)
                || !Finite(WaitMin) || !Finite(WaitMax) || !Finite(BiteWindow) || WaitMin < .2f || WaitMax < WaitMin || WaitMax > 60 || BiteWindow < .2f || BiteWindow > 10
                || StaminaCost > game.MaxStamina || !Finite(SpotPosition.x) || !Finite(SpotPosition.y) || !Finite(SpotPosition.z)
                || !MagicIsland.Interaction.IslandLayout.Contains(SpotPosition.x,SpotPosition.z) || SpotPosition.y != 0
                || Fish.Any(f => f == null || items.Find(f.ItemId)?.Type != ItemType.Fish || items.Find(f.ItemId).SellPrice < 1)) return false;
            try { new FishingState(Definitions(), BiteWindow, StaminaCost); return true; } catch (ArgumentException) { return false; }
        }
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
