using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.TerrainSystem
{
    public sealed class IslandTerrain : MonoBehaviour
    {
        public MeshCollider Ground, Boundary;
        [HideInInspector] public int CoastArtVersion;
        public Transform CoastalScenery;
        public VillagePaths Paths;
        public bool IsValid => Ground!=null&&Boundary!=null&&Ground.sharedMesh!=null&&Boundary.sharedMesh!=null
            && !Ground.convex&&!Boundary.convex&&!Ground.isTrigger&&!Boundary.isTrigger&&Ground.enabled&&Boundary.enabled
            && (Paths==null||Paths.IsValid);
        public bool Contains(Vector3 world,float margin=0)
        {var p=transform.InverseTransformPoint(world);return IslandLayout.ContainsLand(p.x,p.z,margin);}
        public bool TryGround(Vector3 world,out Vector3 point)
        {
            point=world;if(!Contains(world,.30f))return false;
            if(!Ground.Raycast(new Ray(world+Vector3.up*20,Vector3.down),out var hit,50))return false;
            point=hit.point+Vector3.up*.05f;return true;
        }
    }
}
