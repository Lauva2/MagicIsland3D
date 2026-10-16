using System;
using MagicIsland.CalendarSystem;
using MagicIsland.Gathering;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using UnityEngine;

namespace MagicIsland.TerrainSystem
{
    public sealed class SeasonalGroundCover : MonoBehaviour
    {
        public const string Address="SeasonalGroundDetails";
        public int ScatterSeed=47831;
        [Range(1,160)] public int MinimumPatches=85,MaximumPatches=110;
        [Range(.5f,1.4f)] public float MinimumScale=.75f,MaximumScale=1.15f;
        public SeasonalGroundPatch[] Patches=Array.Empty<SeasonalGroundPatch>();
        private PlacementManager placements;
        private GatheringState gathering;
        private bool[] available;
        private int[] trees;
        private bool bound,outside;
        private int season=-1;
        private readonly Collider[] overlaps=new Collider[32];
        private MaterialPropertyBlock properties;
        private Transform terrainTransform;
        private string placementItem;
        public bool ScatterValid=>ScatterSeed>0&&MinimumPatches>=1&&MaximumPatches>=MinimumPatches&&MaximumPatches<=160
            &&MinimumScale>=.5f&&MaximumScale>=MinimumScale&&MaximumScale<=1.4f;
        public bool IsValid => ScatterValid&&Patches!=null&&Patches.Length>0&&Patches.Length<=160
            &&Array.TrueForAll(Patches,p=>p!=null&&p.IsValid)&&GetComponentsInChildren<Collider>(true).Length==0;
        public int VisibleCount {get;private set;}

        // TerrainManager calls once all facilities/placement instances are ready. Observes only
        // committed events; the terrain AA instance owns all meshes/materials. Unbind before release.
        public void Bind(PlacementManager placement,GatheringState nodes,VillagePaths paths,IslandTerrain terrain,AnimalManager animals)
        {
            Unbind();placements=placement;gathering=nodes;
            terrainTransform=terrain.transform;if(properties==null)properties=new MaterialPropertyBlock();
            foreach(var entry in placement.Catalog.PlacementEntries)if(entry.IsPlaceable&&!entry.IsFurniture){placementItem=entry.OutputItemId;break;}
            available=new bool[Patches.Length];trees=new int[Patches.Length];
            Physics.SyncTransforms();
            for(int i=0;i<Patches.Length;i++)
            {
                var p=Patches[i];var center=p.transform.position;
                available[i]=ClearPaths(p,paths)&&ClearPhysics(p,terrain);
                int x=Mathf.RoundToInt(terrain.transform.InverseTransformPoint(center).x),z=Mathf.RoundToInt(terrain.transform.InverseTransformPoint(center).z);
                if(animals!=null&&animals.ReservesTile(x,z))available[i]=false;
                trees[i]=-1;
                if(nodes!=null&&!string.IsNullOrEmpty(p.TreeId))
                    for(int n=0;n<nodes.Count;n++)if(nodes.Definitions[n].Id==p.TreeId){trees[i]=n;break;}
            }
            bound=true;placements.Placed+=OnPlaced;placements.Recovered+=OnRecovered;
            if(gathering!=null)gathering.Changed+=RefreshCanopies;
            RefreshCanopies();RefreshPlacement();
        }
        private static bool ClearPaths(SeasonalGroundPatch patch,VillagePaths paths)
        {
            if(paths==null)return true;
            foreach(var segment in paths.Segments)
            {
                if(!segment.gameObject.activeSelf)continue;
                var p=segment.InverseTransformPoint(patch.transform.position);
                float radius=Radius(patch)+.15f;
                if(Mathf.Abs(p.x)<=.5f+radius/segment.lossyScale.x&&Mathf.Abs(p.z)<=.5f+radius/segment.lossyScale.z)return false;
            }
            foreach(var junction in paths.Junctions)
                if(Vector2.Distance(new Vector2(patch.transform.position.x,patch.transform.position.z),new Vector2(junction.position.x,junction.position.z))<paths.Width*.72f+Radius(patch)+.15f)return false;
            return true;
        }
        private bool ClearPhysics(SeasonalGroundPatch patch,IslandTerrain terrain)
        {
            float radius=Radius(patch);
            int count=Physics.OverlapBoxNonAlloc(patch.transform.position+Vector3.up*.35f,new Vector3(radius,.29f,radius),overlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)
            {
                var hit=overlaps[i];if(hit==terrain.Ground||hit==terrain.Boundary||hit is CharacterController||hit.GetComponentInParent<NPCActor>()!=null||hit.GetComponentInParent<PlacedDecoration>()!=null)continue;
                if(hit.bounds.max.y>patch.transform.position.y+.07f)return false;
            }
            return true;
        }
        private void OnPlaced(SavedPlacedObject record)=>RefreshPlacement();
        private void OnRecovered(int id)=>RefreshPlacement();
        private static float Radius(SeasonalGroundPatch patch)=>patch.FootprintRadius*Mathf.Max(Mathf.Abs(patch.transform.lossyScale.x),Mathf.Abs(patch.transform.lossyScale.z));
        private bool ClearPlacement(SeasonalGroundPatch patch)
        {
            var local=terrainTransform.InverseTransformPoint(patch.transform.position);
            float r=Radius(patch)/Mathf.Min(Mathf.Abs(terrainTransform.lossyScale.x),Mathf.Abs(terrainTransform.lossyScale.z));
            if(placementItem==null)return false;
            for(int x=Mathf.FloorToInt(local.x-r+.5f);x<=Mathf.FloorToInt(local.x+r+.5f);x++)
                for(int z=Mathf.FloorToInt(local.z-r+.5f);z<=Mathf.FloorToInt(local.z+r+.5f);z++)
                    if(placements.State.CheckLocation(placementItem,x,z,0)!=PlacementResult.Success)return false;
            return true;
        }
        public void RefreshPlacement()
        {
            VisibleCount=0;if(!bound||season<0)return;
            for(int i=0;i<Patches.Length;i++)
            {
                bool show=available[i]&&ClearPlacement(Patches[i]);
                Patches[i].Surface.enabled=show;
                if(show)VisibleCount++;
            }
        }
        private void RefreshCanopies()
        {
            for(int i=0;i<Patches.Length;i++)
            {
                var patch=Patches[i];patch.Surface.GetPropertyBlock(properties);
                properties.SetFloat("_LeafEnabled",trees[i]>=0&&!gathering.IsDepleted(trees[i])?1:0);patch.Surface.SetPropertyBlock(properties);
            }
        }
        // WeatherManager supplies the date and outdoor state; wind globals supply the paused clock.
        public void Present(Season value,bool isOutside)
        {
            if(!bound)return;
            if(season!=(int)value)
            {
                season=(int)value;
                foreach(var p in Patches)p.Filter.sharedMesh=p.Seasons[season];
                RefreshPlacement();
            }
            if(outside!=isOutside||gameObject.activeSelf!=isOutside){outside=isOutside;gameObject.SetActive(isOutside);}
        }
        public void Unbind()
        {
            if(placements!=null){placements.Placed-=OnPlaced;placements.Recovered-=OnRecovered;}
            if(gathering!=null)gathering.Changed-=RefreshCanopies;
            placements=null;gathering=null;terrainTransform=null;placementItem=null;available=null;trees=null;bound=false;season=-1;VisibleCount=0;
            foreach(var p in Patches)if(p!=null&&p.Surface!=null)p.Surface.enabled=false;
        }
        private void OnDestroy()=>Unbind();
    }
}
