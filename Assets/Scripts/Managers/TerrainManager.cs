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
        public void ConfigurePaths(int farmSize,MagicIsland.Configuration.HousingConfig housing)
        {
            if(Terrain?.Paths!=null&&Terrain.Paths.IsValid)Terrain.Paths.Configure(farmSize,housing);
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
            var ray=new Ray(requested+Vector3.up*20,Vector3.down);bool found=false;float height=float.MinValue;
            if(Terrain.Ground.Raycast(ray,out var land,50)){point=land.point;height=point.y;found=true;}
            if(pier!=null&&pier.Deck!=null&&pier.Deck.Raycast(ray,out var deck,50)&&deck.point.y>height){point=deck.point;found=true;}
            if(found)point+=Vector3.up*.05f;return found;
        }
        public float GroundHeight(int x,int z)=>Terrain!=null?(float)IslandLayout.Height(x,z):0;
        public bool ReservesTile(int x,int z)=>IslandLayout.PierReserved(x,z);
        public Vector3 Constrain(Vector3 before,Vector3 after,float radius)
        {
            if(Contains(after,radius))return after;
            var result=before;result.y=after.y;return result;
        }
        public void Clear()
        {
            generation++;pier=null;Terrain=null;
            if(handle.IsValid()){if(handle.IsDone)Release(handle);else handle.Completed+=Release;}handle=default;
        }
        private static void Release(AsyncOperationHandle<GameObject> value)
        {if(!value.IsValid())return;if(value.Status==AsyncOperationStatus.Succeeded)Addressables.ReleaseInstance(value);else Addressables.Release(value);}
        private void OnDestroy()=>Clear();
    }
}
