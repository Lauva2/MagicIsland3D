using System.Collections;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagicIsland.Managers
{
    public sealed class TerrainManager : MonoBehaviour
    {
        public IslandTerrain Terrain {get;private set;}
        public bool OwnsTerrain=>handle.IsValid();
        public string LastError {get;private set;}
        private AsyncOperationHandle<GameObject> handle;
        private int generation;
        private FishingSpot pier;
        private readonly Collider[] approachOverlaps=new Collider[32];
        public IEnumerator Load(string address,Transform world)
        {
            Clear();LastError=null;int current=generation;
            handle=Addressables.InstantiateAsync(address,world);yield return handle;
            if(current!=generation)yield break;
            Terrain=handle.Status==AsyncOperationStatus.Succeeded&&handle.Result!=null?handle.Result.GetComponent<IslandTerrain>():null;
            if(Terrain==null||!Terrain.IsValid||world==null)
            {LastError="岛屿地形 AA 预制体缺少地面或边界碰撞";Release(handle);handle=default;Terrain=null;yield break;}
            Physics.SyncTransforms();
        }
        public void BindPier(FishingSpot value)=>pier=value;
        public void BindSeasonalDetails(PlacementManager placements,GatheringManager gathering,AnimalManager animals)
            =>Terrain?.SeasonalDetails?.Bind(placements,gathering.State,Terrain.Paths,Terrain,animals);
        public void ConfigurePaths(int farmSize,MagicIsland.Configuration.HousingConfig housing)
        {
            if(Terrain?.Paths==null||!Terrain.Paths.IsValid)return;
            Terrain.Paths.Configure(farmSize,housing);
            // Instance-only visual wear joins the authored spawn to the actual configured lane.
            // No placement reservation or mesh changes; the AA instance owns this property block.
            var surface=Terrain.Ground.GetComponent<Renderer>();
            if(surface==null||surface.sharedMaterial==null||!surface.sharedMaterial.HasProperty("_VillageApproach"))return;
            var lane=Terrain.Paths.Segments[1];var block=new MaterialPropertyBlock();surface.GetPropertyBlock(block);
            var guide=Vector4.zero;
            float curve=0;
            if(Terrain.Biomes!=null&&lane.gameObject.activeSelf)
            {
                var start=Terrain.transform.TransformPoint(Terrain.Biomes.NewGameSpawn);
                var a=lane.TransformPoint(Vector3.back*.5f);var b=lane.TransformPoint(Vector3.forward*.5f);var direction=b-a;
                var end=a+direction*Mathf.Clamp01(Vector3.Dot(start-a,direction)/Mathf.Max(direction.sqrMagnitude,.0001f));
                foreach(float bend in new[]{.26f,-1.6f,1.6f,-2.4f,2.4f,-3.1f,3.1f})
                {
                    if(!ApproachClear(start,end,bend))continue;
                    guide=new Vector4(start.x,start.z,end.x,end.z);curve=bend;break;
                }
            }
            block.SetVector("_VillageApproach",guide);block.SetFloat("_VillageApproachCurve",curve);surface.SetPropertyBlock(block);
        }
        private bool ApproachClear(Vector3 start,Vector3 end,float bend)
        {
            var delta=end-start;delta.y=0;var side=new Vector3(-delta.z,0,delta.x).normalized;
            // Check only at configuration time. A blocked/full query disables misleading wear;
            // this is presentation and never changes collision, navigation or construction rules.
            for(int i=0;i<=32;i++)
            {
                float t=i/32f;var p=Vector3.Lerp(start,end,t)+side*(Mathf.Sin(t*Mathf.PI)*bend);
                if(!Terrain.Contains(p,.75f)||!TryResolve(p,out var ground))return false;
                int count=Physics.OverlapCapsuleNonAlloc(ground+Vector3.up*.35f,ground+Vector3.up*1.15f,.3f,approachOverlaps,~0,QueryTriggerInteraction.Ignore);
                if(count==approachOverlaps.Length)return false;
                for(int j=0;j<count;j++)
                {
                    var hit=approachOverlaps[j];
                    if(Terrain.IsGround(hit)||hit==Terrain.Boundary||hit is CharacterController||hit.GetComponentInParent<NPCActor>()!=null)continue;
                    return false;
                }
            }
            return true;
        }
        public void RespectSavedPlacement(SavedPlacedObject[] records)
        {
            if(Terrain==null||records==null)return;
            foreach(var patch in Terrain.GetComponentsInChildren<CoastalSceneryPatch>(true))
            {
                if(patch.Obstacle==null)continue;
                foreach(var record in records)
                {
                    var point=Terrain.transform.TransformPoint(new Vector3(record.GridX,.9f,record.GridZ));
                    if(!patch.Obstacle.bounds.Intersects(new Bounds(point,new Vector3(1.12f,1.8f,1.12f))))continue;
                    patch.gameObject.SetActive(false);break;
                }
            }
            Physics.SyncTransforms();
        }
        public bool Contains(Vector3 point,float margin=.3f)
        {
            if(Terrain==null)return false;
            if(Terrain.Contains(point,margin))return true;
            if(pier==null||pier.Deck==null)return false;
            var p=pier.Deck.transform.InverseTransformPoint(point);var size=pier.Deck.size*.5f;var c=pier.Deck.center;
            // Deck collider has unit transform scale in the AA pier prefab.
            return Mathf.Abs(p.x-c.x)<=size.x-margin&&Mathf.Abs(p.z-c.z)<=size.z-margin;
        }
        public bool TryResolve(Vector3 requested,out Vector3 point)
        {
            point=requested;if(Terrain==null||!Contains(requested))return false;
            var ray=new Ray(new Vector3(requested.x,300,requested.z),Vector3.down);bool found=false;float height=float.MinValue;
            if(Terrain.Raycast(ray,out var land,350)){point=land.point;height=point.y;found=true;}
            if(pier!=null&&pier.Deck!=null&&pier.Deck.Raycast(ray,out var deck,350)&&deck.point.y>height){point=deck.point;found=true;}
            if(found)point+=Vector3.up*.05f;return found;
        }
        public float GroundHeight(int x,int z)=>TryResolve(new Vector3(x,0,z),out var p)?p.y-.05f:0;
        public bool ReservesTile(int x,int z)=>IslandLayout.PierReserved(x,z);
        public Vector3 Constrain(Vector3 before,Vector3 after,float radius)
        {
            if(Contains(after,radius))return after;
            var result=before;result.y=after.y;return result;
        }
        public void Clear()
        {
            generation++;pier=null;System.Array.Clear(approachOverlaps,0,approachOverlaps.Length);Terrain?.SeasonalDetails?.Unbind();Terrain=null;
            if(handle.IsValid()){if(handle.IsDone)Release(handle);else handle.Completed+=Release;}handle=default;
        }
        private static void Release(AsyncOperationHandle<GameObject> value)
        {if(!value.IsValid())return;if(value.Status==AsyncOperationStatus.Succeeded)Addressables.ReleaseInstance(value);else Addressables.Release(value);}
        private void OnDestroy()=>Clear();
    }
}
