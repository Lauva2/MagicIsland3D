using UnityEngine;

namespace MagicIsland.Ocean
{
    // A fixed step and fixed arrays keep member steering independent of render frame rate.
    public sealed class OceanFishSteering
    {
        public const float StepSeconds=1f/30;
        public struct MemberState
        {
            public Vector3 Position,Velocity,Target;
            public float Yaw,Until,FeedBlend;
            public OceanFishActivity Activity;
        }
        public readonly MemberState[] States;
        public readonly Vector4[] Poses=new Vector4[32];
        readonly OceanSchool school;
        public readonly OceanSwimNavigation Navigation;
        public int AvoidanceCount { get; private set; }
        public int ForagingCount { get; private set; }
        public int ExplorationCount { get; private set; }
        double elapsed;
        float simulated;
        public OceanFishSteering(OceanSchool school)
        {
            this.school=school;Navigation=new OceanSwimNavigation(school.Obstacles,school.Center,school.HomeRadius);
            States=new MemberState[school.Swimmers.Length];
            for(int i=0;i<States.Length;i++)States[i]=new MemberState{Position=school.Swimmers[i].Spawn,Yaw=school.Phase,Until=4+i*.53f,Activity=OceanFishActivity.Cruising};
        }
        public void Advance(float seconds)
        {
            elapsed+=Mathf.Max(0,seconds);
            while(elapsed+1e-7>=StepSeconds)
            {simulated+=StepSeconds;Step(StepSeconds,simulated);elapsed-=StepSeconds;}
        }
        void Step(float dt,float time)
        {
            for(int i=0;i<States.Length;i++)
            {
                var state=States[i];var member=school.Swimmers[i];float phase=member.Pivot.w;
                if(school.Alarm>.02f){state.Activity=OceanFishActivity.Fleeing;state.Until=time+3+(i%5);}
                else if(state.Activity==OceanFishActivity.Fleeing)state.Activity=OceanFishActivity.Returning;
                else if(time>=state.Until)
                {
                    if(state.Activity==OceanFishActivity.Cruising)
                    {
                        state.Activity=OceanFishActivity.Foraging;state.Until=time+10+(i%3);ForagingCount++;
                        state.Target=ForageTarget(member,phase);
                    }
                    else if(state.Activity==OceanFishActivity.Foraging)
                    {
                        state.Activity=OceanFishActivity.Exploring;state.Until=time+7+(i%4);ExplorationCount++;
                        var p=school.Center+new Vector3(Mathf.Sin(phase)*school.HomeRadius*.8f,member.Pivot.y,Mathf.Cos(phase)*school.HomeRadius*.8f);
                        Navigation.FindClear(p,member,phase,out state.Target);
                    }
                    else if(state.Activity==OceanFishActivity.Exploring){state.Activity=OceanFishActivity.Returning;state.Until=time+4;}
                    else {state.Activity=OceanFishActivity.Cruising;state.Until=time+10+(i%7);}
                }
                float angle=school.Phase+time*school.Speed;
                var guide=new Vector4(school.Radius,school.OrbitAspect,angle,school.Speed);
                OceanFishMotion.Pose(member.Pivot,guide,time,0,out var preferred,out _);
                Vector3 goal=school.Center+preferred;
                if(state.Activity==OceanFishActivity.Foraging||state.Activity==OceanFishActivity.Exploring)goal=state.Target;
                if(state.Activity==OceanFishActivity.Fleeing)
                {
                    var escape=OceanFishMotion.Rotate(new Vector2(school.EscapeDirection.x,school.EscapeDirection.z),Mathf.Sin(phase)*school.ScatterDistance);
                    goal=state.Position+new Vector3(escape.x,0,escape.y)*(1+school.FleeDistance);
                }
                Vector2 toward=new Vector2(goal.x-state.Position.x,goal.z-state.Position.z);
                float speed=(school.Species==ShallowFishSpecies.SpottedRay?.27f:school.Species==ShallowFishSpecies.SilverMinnow?.70f:.38f)*(1+.15f*Mathf.Sin(phase));
                float feeding=state.Activity==OceanFishActivity.Foraging?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.9f,toward.magnitude)):0;
                state.FeedBlend=Mathf.MoveTowards(state.FeedBlend,feeding,dt);
                speed*=Mathf.Lerp(1,.10f,state.FeedBlend)*(1+school.Alarm*1.8f);
                speed*=Mathf.Clamp01(toward.magnitude/.3f);
                Vector2 direction=toward.sqrMagnitude>.0001f?toward.normalized:new Vector2(Mathf.Sin(state.Yaw),Mathf.Cos(state.Yaw));
                var avoidance=Navigation.Avoid(state.Position,direction,member,.5f+speed,i%2==0?1:-1);
                if(avoidance.sqrMagnitude>.1f)AvoidanceCount++;
                Vector2 separation=Vector2.zero;
                for(int j=0;j<States.Length;j++)if(j!=i)
                {
                    var delta=state.Position-States[j].Position;delta.y=0;float d=delta.magnitude;
                    float space=(member.Radius+school.Swimmers[j].Radius)*.65f;
                    if(d>.001f&&d<space)separation+=new Vector2(delta.x,delta.z)/d*((space-d)/space)*.55f;
                }
                direction=(direction+avoidance+separation).normalized;
                float targetYaw=Mathf.Atan2(direction.x,direction.y);
                float yaw=Mathf.MoveTowardsAngle(state.Yaw*Mathf.Rad2Deg,targetYaw*Mathf.Rad2Deg,(school.Species==ShallowFishSpecies.SpottedRay?75:135)*dt)*Mathf.Deg2Rad;
                Vector3 velocity=new Vector3(Mathf.Sin(yaw),0,Mathf.Cos(yaw))*speed;
                Vector3 next=state.Position+velocity*dt;
                if(!Navigation.CanMove(state.Position,next,member))
                {
                    // Sweep the whole step: a thin coral cannot be tunneled through on a slow frame.
                    velocity=Vector3.zero;
                    for(int turn=1;turn<=6;turn++)
                    {
                        float test=yaw+(turn%2==0?-1:1)*((turn+1)/2)*Mathf.PI/6;
                        var candidate=new Vector3(Mathf.Sin(test),0,Mathf.Cos(test))*speed*.45f;
                        if(!Navigation.CanMove(state.Position,state.Position+candidate*dt,member))continue;
                        velocity=candidate;break;
                    }
                    next=state.Position+velocity*dt;
                }
                if(velocity.sqrMagnitude>.00001f)state.Yaw=Mathf.MoveTowardsAngle(state.Yaw*Mathf.Rad2Deg,Mathf.Atan2(velocity.x,velocity.z)*Mathf.Rad2Deg,135*dt)*Mathf.Deg2Rad;
                state.Position=next;state.Velocity=velocity;States[i]=state;
            }
        }
        Vector3 ForageTarget(OceanFishMember member,float phase)
        {
            Vector3 preferred=school.Center+new Vector3(Mathf.Sin(phase),member.Pivot.y,Mathf.Cos(phase));
            if(school.Species==ShallowFishSpecies.Clownfish||school.Species==ShallowFishSpecies.Butterflyfish)
            {
                float nearest=float.PositiveInfinity;
                foreach(var obstacle in Navigation.Obstacles)if(obstacle.Coral)
                {
                    Vector2 direction=new Vector2(school.Center.x,school.Center.z)-obstacle.Center;
                    var p=obstacle.Center+direction.normalized*(obstacle.Radius+member.Radius+.28f);
                    float distance=(p-Navigation.Home).sqrMagnitude;
                    if(distance>=nearest||distance>school.HomeRadius*school.HomeRadius)continue;
                    var point=new Vector3(p.x,school.Center.y+member.Pivot.y,p.y);
                    if(!Navigation.CanMove(point,point,member))continue;
                    preferred=point;nearest=distance;
                }
            }
            Navigation.FindClear(preferred,member,phase,out var result);return result;
        }
        public void WritePoses(Vector3 root)
        {
            for(int i=0;i<States.Length;i++){var s=States[i];var p=s.Position-root;Poses[i]=new Vector4(p.x,p.y,p.z,s.Yaw);}
        }
    }
}
