using System;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class InventoryManager : MonoBehaviour
    {
        public InventoryState State { get; private set; }
        public ItemCatalog Catalog { get; private set; }
        public void Initialize(GameConfig game, ItemCatalog catalog)
        {
            Catalog = catalog;
            State = new InventoryState(game.InventorySlots, catalog.Definitions());
            if (game.InitialSeeds > 0 && !State.TryAdd(game.SeedItemId, game.InitialSeeds))
                throw new InvalidOperationException("Initial seeds exceed inventory capacity");
        }
        public bool TryAdd(string id, int quantity) => State != null && State.TryAdd(id, quantity);
        public bool TryRemove(string id, int quantity) => State != null && State.TryRemove(id, quantity);
        public long GetCount(string id) => State?.GetCount(id) ?? 0;
        public void Clear() { State = null; Catalog = null; }
    }
}
