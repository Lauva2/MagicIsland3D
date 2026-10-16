using UnityEngine;

namespace MagicIsland.Ocean
{
    public enum CoastalBirdActivity { Circling, Skimming, Perching }

    // Driven by OceanManager's shared clock, including pause, interiors and AA lifetime.
    public sealed class CoastalBird : MonoBehaviour
    {
        public CoastalBirdActivity Activity;
        public Transform LeftWing,RightWing;
        public Vector3 Center;
        public float Radius=4,Speed=.23f,Phase;
        [Min(1)] public float ActivityPeriod=48;
        [Range(.1f,.9f)] public float RestFraction=.6f;
        [Min(.1f)] public float FlapSpeed=5.2f;
        [Header("Perched habits")]
        public CoastalBirdHabits Habits=new CoastalBirdHabits();
        readonly CoastalBirdIdle idle=new CoastalBirdIdle();
        readonly CoastalActivityClock activity=new CoastalActivityClock();
        public float FlightActivity=>activity.Motion;
        public float ForagingActivity=>activity.Foraging;
        public void SetEnvironment(CoastalEcologyRhythm settings,float light,bool rain,float twilight)
            =>activity.Set(settings.Bird(light,rain,twilight),settings.TransitionSeconds,Phase);
        Renderer body;
        MaterialPropertyBlock poseProperties;
        Vector2 presentedHead;
        bool poseApplied;
        static readonly int poseId=Shader.PropertyToID("_GullPose");
        public CoastalBirdGesture Gesture=>idle.Gesture;
        public Vector2 HeadPose=>idle.Head;
        [Header("Disturbance response")]
        [Min(0)] public float ReactionDistance=3.2f,EscapeDistance=4,EscapeHeight=3;
        [Min(.01f)] public float QuietSeconds=4,TakeoffSeconds=.65f,RecoverySeconds=4;
        public float Alarm => alarm.Level;
        public int TakeoffCount { get; private set; }
        private readonly WildlifeAlarm alarm=new WildlifeAlarm();
        private Vector3 escapeDirection;
        private Vector3 takeoffOrigin;
        private float restUntil;
        private float escapeStarted;
        private float lastRenderTime=float.NaN,yaw,bank;
        [Header("Safe landing")]
        [Min(.1f)] public float LandingSearchDistance=3,FlightSpeed=6;
        [Min(0)] public float ReturnStagger=.65f;
        CoastalWildlifeNavigation navigation;
        readonly CoastalAirRoute airRoute=new CoastalAirRoute();
        Vector3 landingPoint;
        int landingOwner=-1;
        bool hasLanding;
        float landingRetry;
        public bool WaitingForLanding {get;private set;}
        public Vector3 LandingPoint=>hasLanding?landingPoint:Center;
        public int LandingChanges {get;private set;}
        public void BindNavigation(CoastalWildlifeNavigation value,int owner)
        {
            navigation?.Release(landingOwner);navigation=value;landingOwner=owner;
            hasLanding=false;WaitingForLanding=false;landingRetry=0;
            airRoute.Reset();
            idle.Reset();activity.Reset();ApplyHeadPose();
        }

        public void React(float seconds,Vector3? player,float time,float daylight,bool raining)
        {
            if(seconds<=0)return;
            var home=Activity==CoastalBirdActivity.Perching?LandingPoint:transform.localPosition;
            bool nearby=player.HasValue&&daylight>.12f&&(!raining||Activity==CoastalBirdActivity.Perching)&&Mathf.Abs(home.y-player.Value.y)<1.6f&&Vector3.ProjectOnPlane(home-player.Value,Vector3.up).sqrMagnitude<ReactionDistance*ReactionDistance;
            if(nearby&&alarm.Level<.02f)
            {
                var outward=new Vector3(home.x,0,home.z).normalized;
                var away=Vector3.ProjectOnPlane(home-player.Value,Vector3.up).normalized;
                escapeDirection=(outward+away*1.2f).normalized;
                takeoffOrigin=transform.localPosition-LandingPoint;
            }
            float stagger=navigation==null?0:ReturnStagger*(.5f+.5f*Mathf.Sin(Phase*2.3f));
            float previous=alarm.Level;alarm.Advance(seconds,nearby,QuietSeconds+stagger,TakeoffSeconds,RecoverySeconds);
            if(previous==0&&alarm.Level>0){TakeoffCount++;escapeStarted=time;}
            if(previous>0&&alarm.Level==0)restUntil=time+8;
        }

        public void Render(float time,float daylight,bool raining)
        {
            bool visible=daylight>.12f&&(!raining||Activity==CoastalBirdActivity.Perching);
            if(gameObject.activeSelf!=visible)gameObject.SetActive(visible);
            if(!visible){navigation?.Release(landingOwner);idle.Reset();ApplyHeadPose();lastRenderTime=time;return;}
            float seconds=float.IsNaN(lastRenderTime)?0:Mathf.Max(0,time-lastRenderTime);
            activity.Advance(seconds);
            float flightTime=activity.Configured?activity.Time:time;
            float angle=Phase+flightTime*Speed+.18f*Mathf.Sin(flightTime*.21f+Phase);
            float radius=Radius*(.83f+.17f*Mathf.Sin(time*.17f+Phase*2));
            float flap=Mathf.Sin(time*FlapSpeed+Phase)*28;
            float flight=1;
            Vector3 offset;
            if(Activity==CoastalBirdActivity.Perching)
            {
                float naturalTime=activity.Configured?activity.Time:time;
                float rest=activity.Configured?Mathf.Lerp(.96f,RestFraction,Mathf.Clamp01(activity.Motion)):RestFraction;
                float cycle=Mathf.Repeat(naturalTime+Phase*5,ActivityPeriod)/ActivityPeriod;
                flight=cycle<rest||(!activity.Configured&&raining)?0:Mathf.Pow(Mathf.Sin((cycle-rest)/(1-rest)*Mathf.PI),2)*(activity.Configured?activity.Motion:1);
                if(alarm.Level>0||time<restUntil)flight=0;
                offset=new Vector3(Mathf.Cos(angle)*radius,2.5f,Mathf.Sin(angle)*radius*.6f)*flight;
            }
            else
            {
                float height=Activity==CoastalBirdActivity.Skimming?.10f: .32f;
                offset=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle*1.7f+Phase)*height,Mathf.Sin(angle)*radius*.65f);
                flap*=(.25f+.75f*Mathf.SmoothStep(0,1,Mathf.Sin(time*.53f+Phase)))*Mathf.Lerp(.55f,1,Mathf.Clamp01(activity.Motion));
            }
            float frightened=alarm.Level;
            offset+=(escapeDirection*EscapeDistance+Vector3.up*EscapeHeight)*Mathf.SmoothStep(0,1,frightened);
            if(Activity==CoastalBirdActivity.Perching&&frightened>0)offset+=takeoffOrigin*(1-Mathf.SmoothStep(0,1,frightened));
            if(frightened>0)
            {
                float orbit=(time-escapeStarted)*(.7f+Speed)+Phase;
                float blend=Mathf.SmoothStep(0,1,frightened);
                offset+=new Vector3(Mathf.Cos(orbit)*1.1f,Mathf.Sin(orbit*1.3f)*.12f,Mathf.Sin(orbit)*.75f)*blend*blend;
            }
            flight=Mathf.Max(flight,frightened);
            var position=LandingPoint+offset;
            if(navigation!=null&&seconds>0)
            {
                float scale=Mathf.Max(transform.localScale.x,transform.localScale.z);
                if(Activity==CoastalBirdActivity.Perching)
                {
                    landingRetry=Mathf.Max(0,landingRetry-seconds);
                    if(flight<.55f&&alarm.Level<.55f)
                    {
                        bool clear=navigation.LandingClear(LandingPoint,landingOwner,.9f*scale,.6f*scale);
                        if(!clear&&landingRetry==0)
                        {
                            bool found=navigation.FindLanding(Center,landingOwner,.9f*scale,.6f*scale,LandingSearchDistance,Phase,out var point);
                            if(found){landingPoint=point;hasLanding=true;LandingChanges++;clear=true;}
                            landingRetry=.55f+.15f*(.5f+.5f*Mathf.Sin(Phase));
                        }
                        bool wasWaiting=WaitingForLanding;WaitingForLanding=!clear;
                        if(WaitingForLanding&&!wasWaiting&&transform.localPosition.y<Center.y+.15f)TakeoffCount++;
                    }
                    if(WaitingForLanding)
                    {
                        var outward=new Vector3(Center.x,0,Center.z).normalized;
                        float loiter=Phase+time*Speed+.18f*Mathf.Sin(time*.21f+Phase);
                        position=Center+outward*EscapeDistance+Vector3.up*Mathf.Max(2.2f,EscapeHeight)
                            +new Vector3(Mathf.Cos(loiter)*.9f,Mathf.Sin(loiter)*.1f,Mathf.Sin(loiter)*.65f);flight=1;
                        navigation.Release(landingOwner);
                    }
                    else{position=LandingPoint+offset;navigation.Reserve(landingOwner,LandingPoint,.9f*scale);}
                }
                float airborne=Mathf.Clamp01((transform.localPosition.y-(float)MagicIsland.Interaction.IslandLayout.Height(transform.localPosition.x,transform.localPosition.z))/.7f);
                float spread=Mathf.Max(flight,airborne);
                position=airRoute.Move(navigation,transform.localPosition,position,Center,Mathf.Lerp(.9f,1,spread)*scale,
                    Mathf.Lerp(.6f,1.4f,spread)*scale,FlightSpeed,seconds,-.35f*spread*scale);
                flight=Mathf.Max(flight,Mathf.Clamp01((position.y-(float)MagicIsland.Interaction.IslandLayout.Height(position.x,position.z))/.7f));
            }
            else if(navigation!=null&&!float.IsNaN(lastRenderTime))position=transform.localPosition;
            var delta=position-transform.localPosition;
            float target=flight>.02f&&delta.sqrMagnitude>.000001f?Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg:Phase*Mathf.Rad2Deg+Mathf.Sin(time*.8f+Phase)*8;
            if(float.IsNaN(lastRenderTime))yaw=target;
            else if(seconds>0)
            {
                float turn=Mathf.DeltaAngle(yaw,target);
                yaw=Mathf.MoveTowardsAngle(yaw,target,seconds*140);
                bank=Mathf.MoveTowards(bank,Mathf.Clamp(-turn*.35f,-12,12)*flight,seconds*28);
            }
            float pitch=flight>.02f&&delta.sqrMagnitude>.000001f&&seconds>0?Mathf.Clamp(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,-18,18):0;
            // Same-time render calls preserve attitude as well as position and wing phase.
            if(float.IsNaN(lastRenderTime)||seconds>0)transform.localRotation=Quaternion.Euler(pitch,yaw,bank);
            transform.localPosition=position;lastRenderTime=time;
            bool settled=Activity==CoastalBirdActivity.Perching&&flight<.01f&&alarm.Level==0&&!WaitingForLanding
                &&Vector3.Distance(position,LandingPoint)<.025f&&delta.sqrMagnitude<.000025f;
            idle.Advance(seconds,settled,Phase,Habits,activity.Foraging);ApplyHeadPose();
            if(LeftWing!=null)LeftWing.localRotation=Quaternion.Euler(0,Mathf.Lerp(-68,0,flight),Mathf.Lerp(-10-idle.LeftWing,flap,flight));
            if(RightWing!=null)RightWing.localRotation=Quaternion.Euler(0,Mathf.Lerp(68,0,flight),Mathf.Lerp(10+idle.RightWing,-flap,flight));
        }
        void ApplyHeadPose()
        {
            if(body==null)
            {
                body=GetComponent<Renderer>();
                // Per-instance culling includes the sideways preen; the AA mesh is untouched.
                if(body!=null){var bounds=body.localBounds;bounds.Encapsulate(new Bounds(new Vector3(0,.3f,0),new Vector3(1,.6f,1)));body.localBounds=bounds;}
            }
            if(body==null)return;
            if(poseApplied&&presentedHead==idle.Head)return;
            if(poseProperties==null)poseProperties=new MaterialPropertyBlock();
            body.GetPropertyBlock(poseProperties);
            poseProperties.SetVector(poseId,new Vector4(idle.Head.x*Mathf.Deg2Rad,idle.Head.y*Mathf.Deg2Rad,0,1));
            body.SetPropertyBlock(poseProperties);
            presentedHead=idle.Head;poseApplied=true;
        }
    }
}
