using UnityEngine;

namespace MagicIsland.Ocean
{
    // Temporary per-member guide. The caller copies this value from the fixed-step snapshot.
    // Planning is read-only: every arc segment must fit the existing full-body navigation.
    // Requires the loaded habitat/body, a safe position, +/-1 side preference and fixed clock.
    // Mutates only this transient value; owns no resources and emits no gameplay events.
    public struct OceanFishPassage
    {
        public bool Active;
        public int Side,ObstacleIndex;
        public Vector2 Center;
        public Vector3 Guide;
        public float Radius,Angle,Remaining,Until,RetryAt,WaitUntil,Blend;

        public bool TryBegin(OceanSwimNavigation navigation,Vector3 position,Vector3 goal,
            OceanFishMember body,int preferredSide,float time,float lookAhead,float stagger)
        {
            if(Active||time<RetryAt)return false;
            RetryAt=time+.4f;
            var p=new Vector2(position.x,position.z);var target=new Vector2(goal.x,goal.z);
            var forward=(target-p).normalized;
            if(forward.sqrMagnitude<.01f)return false;
            var end=p+forward*Mathf.Min(lookAhead,Vector2.Distance(p,target));
            int nearest=-1;float distance=float.PositiveInfinity;
            for(int i=0;i<navigation.Obstacles.Length;i++)
            {
                var obstacle=navigation.Obstacles[i];
                if(position.y+body.Top<obstacle.Bottom-.025f||position.y+body.Bottom>obstacle.Top+.025f)continue;
                var offset=obstacle.Center-p;
                if(Vector2.Dot(offset,forward)<=0)continue;
                float radius=obstacle.Radius+body.Radius+.18f;
                if(OceanSwimNavigation.SegmentDistanceSquared(p,end,obstacle.Center)>=radius*radius)continue;
                if(offset.sqrMagnitude>=distance)continue;
                nearest=i;distance=offset.sqrMagnitude;
            }
            if(nearest<0)return false;
            var hit=navigation.Obstacles[nearest];
            var normal=(p-hit.Center).normalized;
            float angle=Mathf.Atan2(normal.y,normal.x);
            // Aim beyond the obstacle even if the ordinary target happens to lie inside it.
            var exit=target-hit.Center;
            if(exit.magnitude<hit.Radius+body.Radius+.4f)exit=forward;
            float exitAngle=Mathf.Atan2(exit.y,exit.x);
            float radiusAround=hit.Radius+body.Radius+.32f;
            for(int choice=0;choice<2;choice++)
            {
                int side=choice==0?preferredSide:-preferredSide;
                float arc=Mathf.Repeat((exitAngle-angle)*side,Mathf.PI*2);
                if(arc<.15f||arc>Mathf.PI*1.6f)continue;
                var from=position;bool clear=true;
                int segments=Mathf.CeilToInt(arc/.3f);
                for(int sample=0;sample<=segments;sample++)
                {
                    float a=angle+side*arc*sample/segments;
                    var to=new Vector3(hit.Center.x+Mathf.Cos(a)*radiusAround,position.y,hit.Center.y+Mathf.Sin(a)*radiusAround);
                    if(!navigation.CanMove(from,to,body)){clear=false;break;}
                    from=to;
                }
                if(!clear)continue;
                Active=true;Side=side;ObstacleIndex=nearest;Center=hit.Center;Radius=radiusAround;Angle=angle;Remaining=arc;
                Guide=new Vector3(Center.x+Mathf.Cos(angle)*Radius,position.y,Center.y+Mathf.Sin(angle)*Radius);
                Until=time+5+(Vector3.Distance(position,Guide)+arc*Radius)/.12f;
                WaitUntil=time+stagger;
                return true;
            }
            return false;
        }

        public void Tick(Vector3 position,float time,float dt,float regroupSeconds,float stagger)
        {
            if(Active)
            {
                var offset=new Vector2(position.x,position.z)-Center;
                float progress=Mathf.DeltaAngle(Angle*Mathf.Rad2Deg,Mathf.Atan2(offset.y,offset.x)*Mathf.Rad2Deg)*Side;
                // Local avoidance can carry a swimmer slightly outside an arc waypoint.
                // Passing its radial plane counts too; do not turn back to touch the exact point.
                bool passed=progress>0&&progress<90&&offset.magnitude<Radius+.6f;
                if(time>=Until)Finish(time);
                else if(Vector3.Distance(position,Guide)<.24f||passed)
                {
                    if(Remaining<.001f)Finish(time);
                    else
                    {
                        float step=Mathf.Min(.3f,Remaining);Angle+=Side*step;Remaining-=step;
                        Guide=new Vector3(Center.x+Mathf.Cos(Angle)*Radius,Guide.y,Center.y+Mathf.Sin(Angle)*Radius);
                    }
                }
            }
            Blend=Mathf.MoveTowards(Blend,Active?1:0,dt/(Active?.35f:regroupSeconds*(1+stagger)));
        }
        void Finish(float time){Active=false;RetryAt=time+1.5f;}
        public void Cancel(float time){Active=false;Blend=0;RetryAt=time+1.5f;}
    }
}
