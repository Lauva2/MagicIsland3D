using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    // Immutable neighbourhood snapshots at 30 Hz; interpolated poses at the render rate.
    public sealed class OceanFishSteering
    {
        public const float StepSeconds=1f/30;
        const double ClockStep=1.0/30;
        public struct MemberState
        {
            public Vector3 Position,Velocity,Target,LastDirection;
            public float Yaw,Pitch,Until,FeedBlend,StoppedSeconds,RecoveryYaw,RecoveryPitch,RecoveryUntil;
            public OceanFishActivity Activity;
        }
        public readonly MemberState[] States;
        public readonly Vector4[] Poses=new Vector4[32],Attitudes=new Vector4[32];
        readonly MemberState[] previous;
        readonly OceanSchool school;
        public readonly OceanSwimNavigation Navigation;
        public int AvoidanceCount { get; private set; }
        public int ForagingCount { get; private set; }
        public int ExplorationCount { get; private set; }
        public OceanFishMember BodyBounds(int index)=>bodies[index];
        readonly Vector3[] positions,velocities,proposed,corrections;
        readonly bool[] halted;
        readonly float[] radii,minY,maxY,pitchLimit;
        readonly OceanFishMember[] bodies;
        double elapsed;
        float simulated;
        public OceanFishSteering(OceanSchool school)
        {
            this.school=school;
            Navigation=new OceanSwimNavigation(school.WaterObstacles??school.Obstacles,school.Center,school.HomeRadius)
                {Floor=school.WaterFloor,Ceiling=school.WaterCeiling};
            int count=school.Swimmers.Length;
            States=new MemberState[count];previous=new MemberState[count];
            positions=new Vector3[count];velocities=new Vector3[count];proposed=new Vector3[count];
            corrections=new Vector3[count];halted=new bool[count];
            radii=new float[count];minY=new float[count];maxY=new float[count];pitchLimit=new float[count];
            bodies=new OceanFishMember[count];
            for(int i=0;i<count;i++)
            {
                var m=school.Swimmers[i];
                // A three dimensional enclosing radius also protects the nose/tail during pitch.
                m.Radius=Mathf.Sqrt(m.Radius*m.Radius+Mathf.Max(m.Bottom*m.Bottom,m.Top*m.Top));radii[i]=m.Radius;
                bool water=!float.IsInfinity(Navigation.Floor)&&!float.IsInfinity(Navigation.Ceiling);
                float room=water?Mathf.Max(0,Navigation.Ceiling-Navigation.Floor-(m.Top-m.Bottom)):0;
                float pad=Mathf.Min(m.Radius*Mathf.Sin(school.MaximumPitch*Mathf.Deg2Rad),room*.18f);
                pitchLimit[i]=Mathf.Asin(Mathf.Clamp01(pad/Mathf.Max(.01f,m.Radius)));
                m.Bottom-=pad;m.Top+=pad;bodies[i]=m;
                minY[i]=water?Navigation.Floor-m.Bottom:m.Spawn.y;
                maxY[i]=water?Navigation.Ceiling-m.Top:m.Spawn.y;
                if(minY[i]>maxY[i])throw new InvalidOperationException("Fish body does not fit the baked water column");
                var spawn=m.Spawn;spawn.y=Mathf.Clamp(spawn.y,minY[i],maxY[i]);
                Navigation.FindClear(spawn,m,m.Pivot.w,out spawn);
                // Older baked schools can contain coincident or intersecting spawn poses.
                if(!Navigation.CanMove(spawn,spawn,m)||!SpawnClear(spawn,i))
                {
                    bool found=false;
                    for(int attempt=0;attempt<2048;attempt++)
                    {
                        float angle=m.Pivot.w+attempt*2.399963f;
                        float radius=(Navigation.HomeRadius-m.Radius)*Mathf.Sqrt(((attempt%512)+.5f)/512);
                        float height=Mathf.Lerp(minY[i],maxY[i],(attempt%7+.5f)/7);
                        var candidate=new Vector3(Navigation.Home.x+Mathf.Cos(angle)*radius,height,Navigation.Home.y+Mathf.Sin(angle)*radius);
                        if(!Navigation.CanMove(candidate,candidate,m)||!SpawnClear(candidate,i))continue;
                        spawn=candidate;found=true;break;
                    }
                    if(!found)throw new InvalidOperationException("No separated fish spawn inside the baked habitat: "+school.Species+" member="+i+" radius="+m.Radius+" water="+minY[i]+".."+maxY[i]+" home="+school.Center);
                }
                States[i]=new MemberState{Position=spawn,Yaw=school.Phase,Until=4+i*.53f,Activity=OceanFishActivity.Cruising};
                previous[i]=States[i];
            }
        }
        bool SpawnClear(Vector3 p,int count)
        {
            for(int j=0;j<count;j++)if(Vector3.Distance(p,States[j].Position)<radii[count]+radii[j]+.12f)return false;
            return true;
        }
        public void Advance(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0)return;
            elapsed+=seconds;
            while(elapsed+1e-7>=ClockStep)
            {simulated+=StepSeconds;Step(StepSeconds,simulated);elapsed-=ClockStep;}
        }
        float Depth(int i,float time,float phase)
        {
            float amplitude=school.VerticalRange*(school.Species==ShallowFishSpecies.SpottedRay?.3f:1);
            float high=school.Species==ShallowFishSpecies.SpottedRay?Mathf.Min(maxY[i],minY[i]+.07f):maxY[i];
            amplitude=Mathf.Min(amplitude,(high-minY[i])*.45f);
            float middle=Mathf.Clamp(school.Swimmers[i].Spawn.y,minY[i]+amplitude,high-amplitude);
            return middle+amplitude*Mathf.Sin(time*.47f+phase);
        }
        void Step(float dt,float time)
        {
            for(int i=0;i<States.Length;i++)
            {previous[i]=States[i];positions[i]=States[i].Position;velocities[i]=States[i].Velocity;}
            for(int i=0;i<States.Length;i++)
            {
                var state=previous[i];var member=bodies[i];float phase=member.Pivot.w;
                state.StoppedSeconds=new Vector2(state.Velocity.x,state.Velocity.z).sqrMagnitude<.000025f?state.StoppedSeconds+dt:0;
                if(state.StoppedSeconds>.3f&&time>=state.RecoveryUntil)
                {
                    state.RecoveryYaw=EscapeYaw(i,state.Yaw,out state.RecoveryPitch);state.RecoveryUntil=time+2;
                    state.StoppedSeconds=0;
                }
                if(school.Alarm>.02f){state.Activity=OceanFishActivity.Fleeing;state.Until=time+3+(i%5);}
                else if(state.Activity==OceanFishActivity.Fleeing)state.Activity=OceanFishActivity.Returning;
                else if(time>=state.Until)
                {
                    if(state.Activity==OceanFishActivity.Cruising)
                    {state.Activity=OceanFishActivity.Foraging;state.Until=time+10+i%3;ForagingCount++;state.Target=ForageTarget(i,phase);}
                    else if(state.Activity==OceanFishActivity.Foraging)
                    {
                        state.Activity=OceanFishActivity.Exploring;state.Until=time+7+i%4;ExplorationCount++;
                        var p=school.Center+new Vector3(Mathf.Sin(phase)*school.HomeRadius*.7f,0,Mathf.Cos(phase)*school.HomeRadius*.7f);
                        p.y=Depth(i,time,phase);Navigation.FindClear(p,member,phase,out state.Target);
                    }
                    else if(state.Activity==OceanFishActivity.Exploring){state.Activity=OceanFishActivity.Returning;state.Until=time+4;}
                    else {state.Activity=OceanFishActivity.Cruising;state.Until=time+10+i%7;}
                }
                float angle=school.Phase+time*school.Speed;
                var goal=school.Center+new Vector3(Mathf.Cos(angle)*school.Radius,0,Mathf.Sin(angle)*school.Radius*school.OrbitAspect);
                goal.y=Depth(i,time,phase);
                bool individual=state.Activity==OceanFishActivity.Foraging||state.Activity==OceanFishActivity.Exploring;
                if(individual)
                {
                    goal=state.Target;
                    // Follow a small moving feeding circuit instead of braking to zero at a shared point.
                    if(Vector3.Distance(state.Position,goal)<.8f)
                        goal+=new Vector3(Mathf.Cos(time*.65f+phase)*.45f,0,Mathf.Sin(time*.65f+phase)*.45f);
                    goal.y=Depth(i,time*.8f,phase);
                }
                if(state.Activity==OceanFishActivity.Fleeing)
                {
                    var escape=OceanFishMotion.Rotate(new Vector2(school.EscapeDirection.x,school.EscapeDirection.z),Mathf.Sin(phase)*school.ScatterDistance);
                    goal=state.Position+new Vector3(escape.x,0,escape.y)*(1+school.FleeDistance);goal.y=Depth(i,time,phase);
                }
                Vector3 toward=goal-state.Position;
                float speed=(school.Species==ShallowFishSpecies.SpottedRay?.27f:school.Species==ShallowFishSpecies.SilverMinnow?.70f:.38f)*(1+.15f*Mathf.Sin(phase));
                float feeding=state.Activity==OceanFishActivity.Foraging?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,1.1f,Vector3.Distance(state.Position,state.Target))):0;
                state.FeedBlend=Mathf.MoveTowards(state.FeedBlend,feeding,dt);
                speed*=Mathf.Lerp(1,.24f,state.FeedBlend)*(1+school.Alarm*1.8f);
                speed=Mathf.MoveTowards(state.Velocity.magnitude,speed,Mathf.Max(.01f,school.Acceleration)*dt);
                var heading=new Vector3(Mathf.Sin(state.Yaw),0,Mathf.Cos(state.Yaw));
                var flock=OceanFishBoids.Evaluate(positions,velocities,radii,i,school.NeighbourRadius,school.SeparationRadius);
                float social=state.Activity==OceanFishActivity.Fleeing?.12f:individual?.3f:1;
                float goalWeight=state.Activity==OceanFishActivity.Fleeing?1.8f:individual?1.4f:Mathf.Max(0,school.CruiseWeight);
                var direction=toward.normalized*goalWeight+flock.Separation*Mathf.Max(0,school.SeparationWeight)
                    +(flock.Alignment*Mathf.Max(0,school.AlignmentWeight)+flock.Cohesion*Mathf.Max(0,school.CohesionWeight))*social;
                direction=direction.sqrMagnitude>.0001f?direction.normalized:heading;
                bool recovering=time<state.RecoveryUntil;
                // Use the current heading to choose the obstacle tangent consistently across adjacent steps.
                var avoidance=Navigation.Avoid(state.Position,new Vector2(heading.x,heading.z),member,.65f+speed,i%2==0?1:-1);
                if(avoidance.sqrMagnitude>.1f)AvoidanceCount++;
                direction+=new Vector3(avoidance.x,0,avoidance.y);
                if(recovering)direction=new Vector3(Mathf.Sin(state.RecoveryYaw),0,Mathf.Cos(state.RecoveryYaw));
                float targetYaw=Mathf.Atan2(direction.x,direction.z);
                float turn=(school.Species==ShallowFishSpecies.SpottedRay?75:135)*dt;
                float yaw=Mathf.MoveTowardsAngle(state.Yaw*Mathf.Rad2Deg,targetYaw*Mathf.Rad2Deg,turn)*Mathf.Deg2Rad;
                float vertical=recovering?0:Mathf.Clamp(toward.y*.7f+flock.Separation.y*.15f
                    +(flock.Alignment.y*school.AlignmentWeight*.04f+flock.Cohesion.y*school.CohesionWeight*.06f)*social,-school.VerticalSpeed,school.VerticalSpeed);
                float pitch=Mathf.Atan2(vertical,Mathf.Max(.02f,speed));
                if(recovering)pitch=state.RecoveryPitch;
                pitch=Mathf.Clamp(pitch,-pitchLimit[i],pitchLimit[i]);
                state.Pitch=Mathf.MoveTowards(state.Pitch,pitch,35*Mathf.Deg2Rad*dt);
                Vector3 velocity=new Vector3(Mathf.Sin(yaw)*Mathf.Cos(state.Pitch),Mathf.Sin(state.Pitch),Mathf.Cos(yaw)*Mathf.Cos(state.Pitch))*speed;
                velocity.y=Mathf.Clamp(velocity.y,-school.VerticalSpeed,school.VerticalSpeed);
                velocity.y=Mathf.Clamp(velocity.y,(minY[i]-state.Position.y)/dt,(maxY[i]-state.Position.y)/dt);
                if(!Navigation.CanMove(state.Position,state.Position+velocity*dt,member))
                {
                    // Rotate gradually towards a clear escape; never snap to a new sideways velocity.
                    if(!recovering)
                    {
                        state.RecoveryYaw=EscapeYaw(i,state.Yaw,out state.RecoveryPitch);
                        state.RecoveryUntil=time+1.5f;
                    }
                    float escapeYaw=state.RecoveryYaw;
                    yaw=Mathf.MoveTowardsAngle(state.Yaw*Mathf.Rad2Deg,escapeYaw*Mathf.Rad2Deg,turn)*Mathf.Deg2Rad;
                    state.Pitch=Mathf.MoveTowards(previous[i].Pitch,state.RecoveryPitch,35*Mathf.Deg2Rad*dt);
                    velocity=new Vector3(Mathf.Sin(yaw)*Mathf.Cos(state.Pitch),Mathf.Sin(state.Pitch),Mathf.Cos(yaw)*Mathf.Cos(state.Pitch))*speed;
                    velocity.y=Mathf.Clamp(velocity.y,-school.VerticalSpeed,school.VerticalSpeed);
                    if(!Navigation.CanMove(state.Position,state.Position+velocity*dt,member))velocity=Vector3.zero;
                }
                state.Yaw=yaw;proposed[i]=velocity;States[i]=state;
            }
            // A separate simultaneous contact pass protects actual body size, even with social weights disabled.
            for(int pass=0;pass<8;pass++)
            {
                Array.Clear(corrections,0,corrections.Length);
                bool contact=false;
                for(int i=0;i<States.Length;i++)for(int j=i+1;j<States.Length;j++)
                {
                    var delta=positions[i]-positions[j];var step=(proposed[i]-proposed[j])*dt;
                    float t=step.sqrMagnitude>.00000001f?Mathf.Clamp01(-Vector3.Dot(delta,step)/step.sqrMagnitude):0;
                    float clearance=radii[i]+radii[j]+.10f;
                    if((delta+step*t).sqrMagnitude>=clearance*clearance-.000001f)continue;
                    var normal=delta.normalized;
                    float closing=Vector3.Dot(proposed[i]-proposed[j],normal);
                    if(closing>=0)continue;
                    // Remove only inward motion. Pushing a neighbour sideways into a reef can deadlock both fish.
                    corrections[i]-=normal*Mathf.Min(0,Vector3.Dot(proposed[i],normal));
                    corrections[j]-=normal*Mathf.Max(0,Vector3.Dot(proposed[j],normal));contact=true;
                }
                for(int i=0;i<States.Length;i++)
                {
                    proposed[i]=Vector3.ClampMagnitude(proposed[i]+corrections[i],proposed[i].magnitude);
                    if(!Navigation.CanMove(positions[i],positions[i]+proposed[i]*dt,bodies[i]))proposed[i]=Vector3.zero;
                }
                if(!contact)break;
            }
            for(int i=0;i<States.Length;i++)
                if(!Navigation.CanMove(positions[i],positions[i]+proposed[i]*dt,bodies[i]))proposed[i]=Vector3.zero;
            // Recheck after obstacle clipping: a stopped fish must not be penetrated by its neighbour.
            for(int pass=0;pass<States.Length;pass++)
            {
                Array.Clear(halted,0,halted.Length);
                bool changed=false;
                for(int i=0;i<States.Length;i++)for(int j=i+1;j<States.Length;j++)
                {
                    var d=positions[i]-positions[j];var step=(proposed[i]-proposed[j])*dt;
                    float t=step.sqrMagnitude>.00000001f?Mathf.Clamp01(-Vector3.Dot(d,step)/step.sqrMagnitude):0;
                    float safe=radii[i]+radii[j]+.025f;
                    if((d+step*t).sqrMagnitude>=safe*safe-.000001f)continue;
                    if(proposed[i].sqrMagnitude+proposed[j].sqrMagnitude<.00000001f)continue;
                    halted[i]=halted[j]=true;changed=true;
                }
                for(int i=0;i<States.Length;i++)if(halted[i])proposed[i]=Vector3.zero;
                if(!changed)break;
            }
            for(int i=0;i<States.Length;i++)
            {
                corrections[i]=proposed[i];
                // A yielding fish still balances its buoyancy with its fins while waiting to turn.
                if(proposed[i].sqrMagnitude<.000025f&&maxY[i]>minY[i])
                {
                    float lift=Mathf.Sin(time*1.7f+bodies[i].Pivot.w)*.025f;
                    if(Mathf.Abs(lift)<.008f)lift=lift<0?-.008f:.008f;
                    lift=Mathf.Clamp(lift,-school.VerticalSpeed,school.VerticalSpeed);
                    var hover=new Vector3(0,Mathf.Clamp(lift,(minY[i]-positions[i].y)/dt,(maxY[i]-positions[i].y)/dt),0);
                    bool clear=Navigation.CanMove(positions[i],positions[i]+hover*dt,bodies[i]);
                    for(int j=0;j<States.Length&&clear;j++)if(i!=j)
                    {
                        var d=positions[i]-positions[j];var travel=(hover-proposed[j])*dt;
                        float t=travel.sqrMagnitude>.00000001f?Mathf.Clamp01(-Vector3.Dot(d,travel)/travel.sqrMagnitude):0;
                        float safe=radii[i]+radii[j]+.04f;
                        clear=(d+travel*t).sqrMagnitude>=safe*safe-.000001f;
                    }
                    if(clear)corrections[i]=hover;
                }
            }
            for(int i=0;i<States.Length;i++)
            {
                var s=States[i];s.Velocity=corrections[i];s.Position=positions[i]+s.Velocity*dt;
                float horizontal=new Vector2(s.Velocity.x,s.Velocity.z).magnitude;
                if(horizontal>.005f)
                {
                    s.LastDirection=s.Velocity.normalized;
                    float turn=(school.Species==ShallowFishSpecies.SpottedRay?75:135)*dt;
                    s.Yaw=Mathf.MoveTowardsAngle(previous[i].Yaw*Mathf.Rad2Deg,Mathf.Atan2(s.Velocity.x,s.Velocity.z)*Mathf.Rad2Deg,turn)*Mathf.Deg2Rad;
                    float pitch=Mathf.Clamp(Mathf.Atan2(s.Velocity.y,horizontal),-pitchLimit[i],pitchLimit[i]);
                    s.Pitch=Mathf.MoveTowards(previous[i].Pitch,pitch,35*Mathf.Deg2Rad*dt);
                }
                States[i]=s;
            }
        }
        float EscapeYaw(int index,float yaw,out float pitch)
        {
            var position=positions[index];var forward=new Vector3(Mathf.Sin(yaw),0,Mathf.Cos(yaw));
            var reverse=-previous[index].LastDirection;
            float best=float.NegativeInfinity,result=reverse.sqrMagnitude>.1f?Mathf.Atan2(reverse.x,reverse.z):yaw+Mathf.PI;
            pitch=reverse.sqrMagnitude>.1f?Mathf.Asin(Mathf.Clamp(reverse.y,-1,1)):0;
            for(int reach=0;reach<3;reach++)
            {
                for(int sample=0;sample<=32;sample++)for(int level=-1;level<=1;level++)
                {
                    float angle=yaw+sample*Mathf.PI/16,tilt=level*pitchLimit[index];
                    var direction=new Vector3(Mathf.Sin(angle)*Mathf.Cos(tilt),Mathf.Sin(tilt),Mathf.Cos(angle)*Mathf.Cos(tilt));
                    if(sample==32)
                    {
                        direction=-previous[index].LastDirection;
                        if(direction.sqrMagnitude<.1f)continue;
                        angle=Mathf.Atan2(direction.x,direction.z);tilt=Mathf.Asin(Mathf.Clamp(direction.y,-1,1));
                    }
                    var endpoint=position+direction*(reach==0?.12f:reach==1?.025f:.003f);
                    if(!Navigation.CanMove(position,endpoint,bodies[index]))continue;
                    float clearance=0;bool crowded=false;
                    for(int j=0;j<States.Length;j++)if(j!=index)
                    {
                        float safe=radii[index]+radii[j]+.101f;
                        if(Vector3.Distance(endpoint,positions[j])<safe){crowded=true;break;}
                        var delta=position-positions[j];var travel=endpoint-position;
                        float nearest=travel.sqrMagnitude>0?Mathf.Clamp01(-Vector3.Dot(delta,travel)/travel.sqrMagnitude):0;
                        float body=radii[index]+radii[j]+.10f;
                        if(nearest>0&&(delta+travel*nearest).sqrMagnitude<body*body-.000001f){crowded=true;break;}
                        clearance+=Vector3.Dot(direction,(position-positions[j]).normalized)/Mathf.Max(.1f,Vector3.Distance(position,positions[j]));
                    }
                    if(crowded)continue;
                    float safety=0;
                    var horizontal=new Vector2(position.x,position.z);
                    foreach(var obstacle in Navigation.Obstacles)
                    {
                        if(position.y+bodies[index].Top<obstacle.Bottom-.025f||position.y+bodies[index].Bottom>obstacle.Top+.025f)continue;
                        var away=horizontal-obstacle.Center;
                        float gap=away.magnitude-obstacle.Radius-bodies[index].Radius-.04f;
                        if(gap<.4f)safety+=Vector2.Dot(new Vector2(direction.x,direction.z),away.normalized)*(1-Mathf.Clamp01(gap/.4f))*3;
                    }
                    var home=Navigation.Home-horizontal;
                    if(home.magnitude>Navigation.HomeRadius-bodies[index].Radius-.4f)
                        safety+=Vector2.Dot(new Vector2(direction.x,direction.z),home.normalized)*4;
                    float score=Vector3.Dot(direction,forward)*.1f+clearance+safety-Mathf.Abs(tilt)*.2f;
                    if(score>best){best=score;result=angle;pitch=tilt;}
                }
                if(!float.IsNegativeInfinity(best))break;
            }
            return result;
        }
        Vector3 ForageTarget(int index,float phase)
        {
            var member=bodies[index];
            Vector3 preferred=school.Center+new Vector3(Mathf.Sin(phase),0,Mathf.Cos(phase));preferred.y=Depth(index,simulated,phase);
            if(school.Species==ShallowFishSpecies.Clownfish||school.Species==ShallowFishSpecies.Butterflyfish)
            {
                float nearest=float.PositiveInfinity;
                foreach(var obstacle in Navigation.Obstacles)if(obstacle.Coral)
                {
                    var offset=OceanFishMotion.Rotate((Navigation.Home-obstacle.Center).normalized,Mathf.Sin(phase)*1.1f);
                    var p=obstacle.Center+offset*(obstacle.Radius+member.Radius+.5f);
                    float distance=(p-Navigation.Home).sqrMagnitude;
                    if(distance>=nearest||distance>school.HomeRadius*school.HomeRadius)continue;
                    var point=new Vector3(p.x,preferred.y,p.y);
                    if(!Navigation.CanMove(point,point,member))continue;
                    preferred=point;nearest=distance;
                }
            }
            Navigation.FindClear(preferred,member,phase,out var result);return result;
        }
        public MemberState RenderState(int index)
        {
            var a=previous[index];var b=States[index];float alpha=Mathf.Clamp01((float)(elapsed/ClockStep));
            b.Position=Vector3.LerpUnclamped(a.Position,b.Position,alpha);
            b.Yaw=(a.Yaw*Mathf.Rad2Deg+Mathf.DeltaAngle(a.Yaw*Mathf.Rad2Deg,b.Yaw*Mathf.Rad2Deg)*alpha)*Mathf.Deg2Rad;
            b.Pitch=Mathf.LerpUnclamped(a.Pitch,b.Pitch,alpha);return b;
        }
        public void WritePoses(Vector3 root)
        {
            for(int i=0;i<States.Length;i++)
            {var s=RenderState(i);var p=s.Position-root;Poses[i]=new Vector4(p.x,p.y,p.z,s.Yaw);Attitudes[i]=new Vector4(s.Pitch,0,0,0);}
        }
    }
}
