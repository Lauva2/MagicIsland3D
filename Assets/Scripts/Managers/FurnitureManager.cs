using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class FurnitureManager : PlacementManager
    {
        private HomeInterior room;
        private int layer;
        public bool Bound => room != null;
        public void InitializeFurniture(CraftingCatalog catalog, InventoryManager inventory, GameConfig game, PlayerManager players, int layer, SavedPlacedObject[] saved = null, int houseLevel = 0)
        { Initialize(catalog, inventory, game, players, null, null, saved, true, houseLevel); this.layer = layer; }
        public IEnumerator BindInterior(HomeInterior interior)
        {
            LastError = null;
            if (room == interior && Bound) yield break;
            world = interior.transform; room = interior;
            yield return RestoreObjects();
            if (LastError == null) room = interior;
            else { room = null; ClearObjects(); }
        }
        protected override bool AreaAvailable => Bound && (GameManager.Instance?.Housing?.Inside ?? false);
        protected override void Present(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var light in go.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << layer;
        }
        protected override bool Reserved(int x, int z)
        {
            if (room == null) return true;
            var spawn = world.InverseTransformPoint(room.SpawnPoint.position);
            var exit = world.InverseTransformPoint(room.Exit.Position) + Vector3.forward;
            var bed = world.InverseTransformPoint(room.Bed.transform.position) + Vector3.right * 1.5f;
            return Segment(x, z, spawn, exit) || Segment(x, z, spawn, new Vector3(bed.x, 0, spawn.z))
                || Segment(x, z, new Vector3(bed.x, 0, spawn.z), bed);
        }
        private static bool Segment(int x, int z, Vector3 a, Vector3 b)
            => FurnitureLayout.Overlaps(x, z, Mathf.Min(a.x, b.x) - .4, Mathf.Max(a.x, b.x) + .4, Mathf.Min(a.z, b.z) - .4, Mathf.Max(a.z, b.z) + .4);
        public override void Clear() { room = null; base.Clear(); }
    }
}
