using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    // Baked link into an existing AA meadow mesh, not a second copy of a flower position.
    [Serializable] public sealed class MeadowFlowerVisit
    {
        public Renderer Source;
        public int Vertex;
        public int Slot;
    }
}
