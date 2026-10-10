using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Gathering;
using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [Serializable]
    public sealed class GatheringTypeConfig
    {
        public string Id, DisplayName, ItemId, PrefabAddress;
        public FarmTool Tool = FarmTool.Axe;
        [Range(1, 20)] public int Hits = 3;
        [Range(1, 99)] public int Yield = 3;
        [Min(0)] public int StaminaCost = 4;
        [Range(1, 365)] public int RespawnDays = 3;
    }
    [Serializable]
    public sealed class GatheringNodeConfig
    {
        public string Id, TypeId;
        public Vector3 Position;
    }
    [CreateAssetMenu(menuName = "Magic Island/Gathering Catalog")]
    public sealed class GatheringCatalog : ScriptableObject
    {
        public const string Address = "GatheringCatalog";
        public List<GatheringTypeConfig> Types = new List<GatheringTypeConfig>();
        public List<GatheringNodeConfig> Nodes = new List<GatheringNodeConfig>();
        public GatheringTypeConfig Find(string id) => Types.Find(value => value != null && value.Id == id);
        public IEnumerable<GatheringDefinition> Definitions() => Nodes.Select(node =>
        {
            var type = Find(node.TypeId); return new GatheringDefinition(node.Id, type.ItemId, type.Tool, type.Hits, type.Yield, type.StaminaCost, type.RespawnDays);
        });
        public bool Validate(ItemCatalog items, GameConfig game)
        {
            if (Types == null || Types.Count == 0 || Nodes == null || Nodes.Count == 0 || Nodes.Count > 64) return false;
            var ids = new HashSet<string>();
            foreach (var type in Types)
            {
                if (type == null || string.IsNullOrWhiteSpace(type.Id) || !ids.Add(type.Id) || string.IsNullOrWhiteSpace(type.DisplayName)
                    || string.IsNullOrWhiteSpace(type.PrefabAddress) || items.Find(type.ItemId)?.Type != ItemType.Material) return false;
                try { new GatheringDefinition(type.Id, type.ItemId, type.Tool, type.Hits, type.Yield, type.StaminaCost, type.RespawnDays); }
                catch (ArgumentException) { return false; }
            }
            ids.Clear();
            foreach (var node in Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id) || Find(node.TypeId) == null) return false;
                Vector3 position = node.Position;
                if (float.IsNaN(position.x) || float.IsNaN(position.z) || Mathf.Abs(position.x) > 10.5f || Mathf.Abs(position.z) > 10.5f
                    || position.y != 0 || (Mathf.Abs(position.x) < game.FarmSize * 0.6f + 1.6f && Mathf.Abs(position.z) < game.FarmSize * 0.6f + 1.6f)) return false;
            }
            return true;
        }
    }
}
