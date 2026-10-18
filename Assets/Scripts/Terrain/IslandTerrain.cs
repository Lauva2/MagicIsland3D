using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.TerrainSystem
{
    public sealed class IslandTerrain : MonoBehaviour
    {
        public MeshCollider Ground, Boundary;
        [HideInInspector] public int CoastArtVersion;
        public Transform CoastalScenery;
        public CoastalPalmScatter PalmScatter = new CoastalPalmScatter();
        public CoastalDetailScatter DetailScatter = new CoastalDetailScatter();
        public VillagePaths Paths;
        public SeasonalGroundCover SeasonalDetails;
        public IslandBiomeSettings Biomes = new IslandBiomeSettings();
        [HideInInspector] public int BiomeArtVersion;
        public Transform BiomeScenery;
        public StarterIslandWorld World;
        public bool IsValid => Ground!=null&&Boundary!=null&&Ground.sharedMesh!=null&&Boundary.sharedMesh!=null
            && !Ground.convex&&!Boundary.convex&&!Ground.isTrigger&&!Boundary.isTrigger&&Ground.enabled&&Boundary.enabled
            && (Paths==null||Paths.IsValid)&&(SeasonalDetails==null||SeasonalDetails.IsValid)&&(World==null||World.IsValid)
            && (BiomeArtVersion==0 ? Biomes==null||Biomes.IsValid : Biomes!=null&&Biomes.IsValid&&BiomeScenery!=null);
        public bool Contains(Vector3 world,float margin=0)
        {var p=transform.InverseTransformPoint(world);return IslandLayout.ContainsLand(p.x,p.z,margin);}
        public bool TryGround(Vector3 world,out Vector3 point)
        {
            point=world;if(!Contains(world,.30f))return false;
            if(!Raycast(new Ray(new Vector3(world.x,300,world.z),Vector3.down),out var hit,350))return false;
            point=hit.point+Vector3.up*.05f;return true;
        }
        public bool IsGround(Collider c)=>c==Ground||(World!=null&&World.OwnsGround(c));
        public bool Raycast(Ray ray,out RaycastHit hit,float distance)
        {
            bool found=Ground.Raycast(ray,out hit,distance);
            if(World!=null&&World.Raycast(ray,out var extra,distance)&&(!found||extra.distance<hit.distance)){hit=extra;found=true;}
            return found;
        }
    }
}
