using UnityEngine;

namespace MagicIsland.TerrainSystem
{
    // Baked into IslandTerrain, owned/released with its AA instance. No runtime generation or Update.
    public sealed class StarterIslandWorld : MonoBehaviour
    {
        public int ArtVersion;
        [HideInInspector] public int GrasslandCoastArtVersion;
        public MeshCollider[] Grounds = System.Array.Empty<MeshCollider>();
        public Renderer[] Waters = System.Array.Empty<Renderer>();
        public Transform Scenery;
        public bool IsValid => (ArtVersion==1||ArtVersion==2||ArtVersion==3)&&Grounds!=null&&Grounds.Length>0&&System.Array.TrueForAll(Grounds,c=>c!=null&&c.sharedMesh!=null&&!c.convex&&!c.isTrigger&&c.enabled)
            &&Waters!=null&&(ArtVersion==3||Waters.Length>0)&&System.Array.TrueForAll(Waters,r=>r!=null&&r.sharedMaterial!=null)&&Scenery!=null;
        public bool Raycast(Ray ray,out RaycastHit hit,float distance)
        {
            hit=default;bool found=false;
            foreach(var c in Grounds)if(c.Raycast(ray,out var candidate,distance)&&(!found||candidate.distance<hit.distance)){hit=candidate;found=true;}
            return found;
        }
        public bool OwnsGround(Collider c)=>System.Array.IndexOf(Grounds,c)>=0;
    }
}
