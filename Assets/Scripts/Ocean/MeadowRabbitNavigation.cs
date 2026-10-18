using System;
using MagicIsland.TerrainSystem;
using UnityEngine;

namespace MagicIsland.Ocean
{
    // Instance-scoped nonallocating sweep; original terrain, player and other worlds are filtered.
    sealed class MeadowRabbitNavigation
    {
        public const float Radius=.46f,Height=1.15f;
        IslandTerrain terrain;
        Transform space,world;
        readonly Collider[] starts=new Collider[64],ends=new Collider[64];
        readonly RaycastHit[] hits=new RaycastHit[64];
        readonly CoastalWildlifeNavigation volumes;
        public MeadowRabbitNavigation(IslandTerrain terrain,Transform space)
        {
            this.terrain=terrain;this.space=space;world=terrain.transform.parent;
            volumes=new CoastalWildlifeNavigation(0){SolidClear=SolidClear};
        }
        public void SetPlayer(Vector3? point)=>volumes.SetPlayer(point);
        public bool Clear(Vector3 from,Vector3 to)=>volumes.BodyClear(from,to,Radius,Height);
        bool Blocks(Collider c)=>c!=null&&c.enabled&&!c.isTrigger&&c!=terrain.Boundary&&!terrain.IsGround(c)
            &&c.transform.IsChildOf(world)&&c.GetComponentInParent<MagicIsland.Player.PlayerMovement>()==null;
        bool SolidClear(Vector3 from,Vector3 to,float radius,float height)
        {
            if(space==null||world==null||terrain==null)return false;
            float scale=Mathf.Max(Mathf.Abs(space.lossyScale.x),Mathf.Abs(space.lossyScale.z));
            var ext=new Vector3(radius*scale,height*Mathf.Abs(space.lossyScale.y)*.5f,radius*scale);
            var a=space.TransformPoint(from)+Vector3.up*(ext.y+.025f);var b=space.TransformPoint(to)+Vector3.up*(ext.y+.025f);
            int initial=Physics.OverlapBoxNonAlloc(a,ext,starts,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            int ending=Physics.OverlapBoxNonAlloc(b,ext,ends,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            if(initial==starts.Length||ending==ends.Length)return false;
            for(int i=0;i<initial;i++)if(Blocks(starts[i])&&!Exiting(starts[i],a,b,ext))return false;
            for(int i=0;i<ending;i++)if(Blocks(ends[i])&&!InitialExit(ends[i],initial,a,b,ext))return false;
            var delta=b-a;float distance=delta.magnitude;if(distance<.000001f)return true;
            int count=Physics.BoxCastNonAlloc(a,ext,delta/distance,hits,Quaternion.identity,distance,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(Blocks(hits[i].collider)&&!InitialExit(hits[i].collider,initial,a,b,ext))return false;
            return true;
        }
        bool InitialExit(Collider c,int count,Vector3 a,Vector3 b,Vector3 ext)
        {for(int i=0;i<count;i++)if(starts[i]==c)return Exiting(c,a,b,ext);return false;}
        static bool Exiting(Collider c,Vector3 a,Vector3 b,Vector3 ext)
        {
            var bounds=c.bounds;bounds.Expand(ext*2);if(!bounds.Contains(a))return false;
            a-=bounds.center;b-=bounds.center;
            return Progress(a.x,b.x)||Progress(a.y,b.y)||Progress(a.z,b.z);
        }
        static bool Progress(float a,float b)=>Mathf.Abs(b)>Mathf.Abs(a)+.000001f&&a*(b-a)>=0;
        public void Release()
        {volumes.Clear();terrain=null;space=world=null;Array.Clear(starts,0,starts.Length);Array.Clear(ends,0,ends.Length);Array.Clear(hits,0,hits.Length);}
    }
}
