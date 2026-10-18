using UnityEngine;

namespace MagicIsland.TerrainSystem
{
    // Four baked meshes share one renderer/material. No physics or gameplay state.
    public sealed class SeasonalGroundPatch : MonoBehaviour
    {
        public MeshFilter Filter;
        public MeshRenderer Surface;
        public Mesh[] Seasons = new Mesh[4];
        public string TreeId;
        public IslandBiome Biome;
        [Range(.1f,.49f)] public float FootprintRadius=.48f;
        public bool IsValid => Filter!=null&&Surface!=null&&Surface.sharedMaterial!=null
            &&Seasons!=null&&Seasons.Length==4&&System.Array.TrueForAll(Seasons,m=>m!=null)
            &&FootprintRadius>=.1f&&FootprintRadius<=.49f&&!float.IsNaN(FootprintRadius);
    }
}
