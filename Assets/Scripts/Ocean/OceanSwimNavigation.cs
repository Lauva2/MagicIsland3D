using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    [Serializable] public struct OceanSwimObstacle
    {
        public Vector2 Center;
        public float Radius,Bottom,Top;
        public bool Coral;
    }
    [Serializable] public struct OceanFishMember
    {
        public Vector4 Pivot;
        public float Radius,Bottom,Top;
        public Vector3 Spawn;
    }
    public enum OceanFishActivity { Cruising, Foraging, Exploring, Returning, Fleeing }

    // Baked circles conservatively enclose each solid reef object, without physics colliders.
    public sealed class OceanSwimNavigation
    {
        public readonly OceanSwimObstacle[] Obstacles;
        public readonly Vector2 Home;
        public readonly float HomeRadius;
        public float Floor = float.NegativeInfinity, Ceiling = float.PositiveInfinity;
        public OceanSwimNavigation(OceanSwimObstacle[] obstacles,Vector3 home,float radius)
        { Obstacles=obstacles??Array.Empty<OceanSwimObstacle>();Home=new Vector2(home.x,home.z);HomeRadius=radius; }

        public bool CanMove(Vector3 from,Vector3 to,OceanFishMember member)
        {
            if(Mathf.Min(from.y,to.y)+member.Bottom<Floor-.00001f||Mathf.Max(from.y,to.y)+member.Top>Ceiling+.00001f)return false;
            Vector2 a=new Vector2(from.x,from.z),b=new Vector2(to.x,to.z);
            float allowed=Mathf.Max(.1f,HomeRadius-member.Radius);
            if((b-Home).sqrMagnitude>allowed*allowed+.00001f)return false;
            foreach(var obstacle in Obstacles)
            {
                if(Mathf.Max(from.y,to.y)+member.Top<obstacle.Bottom-.025f||Mathf.Min(from.y,to.y)+member.Bottom>obstacle.Top+.025f)continue;
                float radius=obstacle.Radius+member.Radius+.04f;
                // Explicit endpoints keep a just-accepted position valid when the next step
                // is stationary (the segment projection has slightly different rounding).
                float squared=radius*radius-.00001f;
                if((a-obstacle.Center).sqrMagnitude<squared||(b-obstacle.Center).sqrMagnitude<squared||SegmentDistanceSquared(a,b,obstacle.Center)<squared)return false;
            }
            return true;
        }
        public static float SegmentDistanceSquared(Vector2 a,Vector2 b,Vector2 point)
        {
            var delta=b-a;float t=delta.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude):0;
            return (point-a-delta*t).sqrMagnitude;
        }
        // A fully checked passage owns steering around its obstacle; swept CanMove still checks it.
        public Vector2 Avoid(Vector3 position,Vector2 heading,OceanFishMember member,float lookAhead,float side,int guidedObstacle=-1)
        {
            Vector2 p=new Vector2(position.x,position.z),end=p+heading*lookAhead,steer=Vector2.zero;
            for(int index=0;index<Obstacles.Length;index++)
            {
                if(index==guidedObstacle)continue;
                var obstacle=Obstacles[index];
                if(position.y+member.Top<obstacle.Bottom-.025f||position.y+member.Bottom>obstacle.Top+.025f)continue;
                float radius=obstacle.Radius+member.Radius+.22f;
                if(SegmentDistanceSquared(p,end,obstacle.Center)>=radius*radius)continue;
                Vector2 normal=(p-obstacle.Center).normalized;
                if(normal.sqrMagnitude<.01f)normal=-heading;
                var tangent=new Vector2(-normal.y,normal.x);
                float alignment=Vector2.Dot(tangent,heading);
                tangent*=Mathf.Abs(alignment)>.12f?Mathf.Sign(alignment):side;
                steer+=normal*.9f+tangent*1.6f;
            }
            var home=Home-p;
            if(home.magnitude>HomeRadius-member.Radius-.6f)steer+=home.normalized*2;
            return steer;
        }
        public bool FindClear(Vector3 preferred,OceanFishMember member,float phase,out Vector3 point)
        {
            preferred.y=Mathf.Clamp(preferred.y,Floor-member.Bottom,Ceiling-member.Top);
            if(CanMove(preferred,preferred,member)){point=preferred;return true;}
            for(int i=0;i<240;i++)
            {
                float angle=phase+i*2.399963f,r=Mathf.Max(.1f,HomeRadius-member.Radius)*Mathf.Sqrt((i+.5f)/240);
                point=new Vector3(Home.x+Mathf.Cos(angle)*r,preferred.y,Home.y+Mathf.Sin(angle)*r);
                if(CanMove(point,point,member))return true;
            }
            point=preferred;return false;
        }
    }

    // Reads the existing AA floor once. No generated geometry, random source or physics objects.
    public sealed class OceanFishWaterVolume
    {
        readonly Vector3[] vertices;
        readonly int[] triangles;
        public OceanFishWaterVolume(Vector3[] vertices,int[] triangles)
        {this.vertices=vertices;this.triangles=triangles;}
        public float HighestFloor(Vector3 center,float radius)
        {
            float highest=float.NegativeInfinity;
            var p=new Vector2(center.x,center.z);float squared=radius*radius;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                var x=new Vector2(a.x,a.z);var y=new Vector2(b.x,b.z);var z=new Vector2(c.x,c.z);
                if(p.x+radius<Mathf.Min(x.x,Mathf.Min(y.x,z.x))||p.x-radius>Mathf.Max(x.x,Mathf.Max(y.x,z.x))
                    ||p.y+radius<Mathf.Min(x.y,Mathf.Min(y.y,z.y))||p.y-radius>Mathf.Max(x.y,Mathf.Max(y.y,z.y)))continue;
                float d=(y.y-z.y)*(x.x-z.x)+(z.x-y.x)*(x.y-z.y);
                float u=((y.y-z.y)*(p.x-z.x)+(z.x-y.x)*(p.y-z.y))/d;
                float v=((z.y-x.y)*(p.x-z.x)+(x.x-z.x)*(p.y-z.y))/d;
                bool inside=u>=0&&v>=0&&u+v<=1;
                if(!inside&&OceanSwimNavigation.SegmentDistanceSquared(x,y,p)>squared
                    &&OceanSwimNavigation.SegmentDistanceSquared(y,z,p)>squared
                    &&OceanSwimNavigation.SegmentDistanceSquared(z,x,p)>squared)continue;
                highest=Mathf.Max(highest,Mathf.Max(a.y,Mathf.Max(b.y,c.y)));
            }
            return highest;
        }
    }
}
