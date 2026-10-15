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
        public OceanSwimNavigation(OceanSwimObstacle[] obstacles,Vector3 home,float radius)
        { Obstacles=obstacles??Array.Empty<OceanSwimObstacle>();Home=new Vector2(home.x,home.z);HomeRadius=radius; }

        public bool CanMove(Vector3 from,Vector3 to,OceanFishMember member)
        {
            Vector2 a=new Vector2(from.x,from.z),b=new Vector2(to.x,to.z);
            float allowed=Mathf.Max(.1f,HomeRadius-member.Radius);
            if((b-Home).sqrMagnitude>allowed*allowed)return false;
            foreach(var obstacle in Obstacles)
            {
                if(to.y+member.Top<obstacle.Bottom-.025f||to.y+member.Bottom>obstacle.Top+.025f)continue;
                float radius=obstacle.Radius+member.Radius+.04f;
                // Explicit endpoints keep a just-accepted position valid when the next step
                // is stationary (the segment projection has slightly different rounding).
                float squared=radius*radius;
                if((a-obstacle.Center).sqrMagnitude<squared||(b-obstacle.Center).sqrMagnitude<squared||SegmentDistanceSquared(a,b,obstacle.Center)<squared)return false;
            }
            return true;
        }
        public static float SegmentDistanceSquared(Vector2 a,Vector2 b,Vector2 point)
        {
            var delta=b-a;float t=delta.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude):0;
            return (point-a-delta*t).sqrMagnitude;
        }
        public Vector2 Avoid(Vector3 position,Vector2 heading,OceanFishMember member,float lookAhead,float side)
        {
            Vector2 p=new Vector2(position.x,position.z),end=p+heading*lookAhead,steer=Vector2.zero;
            foreach(var obstacle in Obstacles)
            {
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
}
