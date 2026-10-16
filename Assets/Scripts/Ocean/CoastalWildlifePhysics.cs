using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    // Bounded, nonallocating queries include late-loaded facilities and new placements.
    // Ground and the player are handled by the shared coastline / player-volume rules.
    public sealed class CoastalWildlifePhysics
    {
        Transform world,space,player;
        Collider ground,boundary;
        readonly Collider[] starts=new Collider[64],ends=new Collider[64];
        readonly RaycastHit[] hits=new RaycastHit[64];
        public CoastalWildlifePhysics(Transform world,Transform space,Collider ground,Transform player,Collider boundary=null)
        {this.world=world;this.space=space;this.ground=ground;this.player=player;this.boundary=boundary;}
        public void Clear(){world=space=player=null;ground=boundary=null;Array.Clear(starts,0,starts.Length);Array.Clear(ends,0,ends.Length);Array.Clear(hits,0,hits.Length);}
        public bool ClearVolume(Vector3 from,Vector3 to,float radius,float height)
        {
            if(world==null||space==null)return false;
            float scale=Mathf.Max(Mathf.Max(Mathf.Abs(space.lossyScale.x),Mathf.Abs(space.lossyScale.y)),Mathf.Abs(space.lossyScale.z));
            var ext=new Vector3(radius,height*.5f,radius)*scale;
            var a=space.TransformPoint(from)+Vector3.up*(ext.y+.025f);var b=space.TransformPoint(to)+Vector3.up*(ext.y+.025f);
            int initial=Physics.OverlapBoxNonAlloc(a,ext,starts,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            int ending=Physics.OverlapBoxNonAlloc(b,ext,ends,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            if(initial==starts.Length||ending==ends.Length)return false;
            for(int i=0;i<initial;i++)if(Blocks(starts[i])&&!Exiting(starts[i],a,b,ext))return false;
            for(int i=0;i<ending;i++)if(Blocks(ends[i])&&!InitialExit(ends[i],initial,a,b,ext))return false;
            var delta=b-a;float distance=delta.magnitude;
            if(distance<.000001f)return true;
            int count=Physics.BoxCastNonAlloc(a,ext,delta/distance,hits,Quaternion.identity,distance,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(Blocks(hits[i].collider)&&!InitialExit(hits[i].collider,initial,a,b,ext))return false;
            return true;
        }
        bool Blocks(Collider collider)=>collider!=null&&collider!=ground&&collider!=boundary&&collider.enabled&&!collider.isTrigger
            &&collider.transform.IsChildOf(world)&&(player==null||!collider.transform.IsChildOf(player));
        bool InitialExit(Collider collider,int count,Vector3 from,Vector3 to,Vector3 ext)
        {
            for(int i=0;i<count;i++)if(starts[i]==collider)return Exiting(collider,from,to,ext);
            return false;
        }
        static bool Exiting(Collider collider,Vector3 from,Vector3 to,Vector3 ext)
        {
            var bounds=collider.bounds;bounds.Expand(ext*2);
            if(!bounds.Contains(from))return false;
            var a=from-bounds.center;var b=to-bounds.center;
            return Progress(a.x,b.x)||Progress(a.y,b.y)||Progress(a.z,b.z);
        }
        static bool Progress(float from,float to)=>Mathf.Abs(to)>Mathf.Abs(from)+.000001f&&from*(to-from)>=0;
    }
}
