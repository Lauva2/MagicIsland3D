using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.Ocean
{
    public sealed class CoastalCrab : MonoBehaviour
    {
        public Vector3 Center;
        public Vector3 Tangent;
        public float Phase,Travel=.45f;
        [Min(1)] public float CrawlPeriod=8;
        [Range(.1f,.8f)] public float RestFraction=.45f;
        [Header("Resting habits")]
        public CoastalCrabHabits Habits=new CoastalCrabHabits();
        readonly CoastalCrabIdle idle=new CoastalCrabIdle();
        readonly CoastalActivityClock activity=new CoastalActivityClock();
        public float MotionActivity=>activity.Motion;
        public float ForagingActivity=>activity.Foraging;
        public void SetEnvironment(CoastalEcologyRhythm settings,float light,bool rain)
            =>activity.Set(settings.Crab(light,rain),settings.TransitionSeconds,Phase);
        public CoastalCrabGesture Gesture=>idle.Gesture;
        public Vector3 ClawPose=>idle.Pose;
        [Header("Disturbance response")]
        [Min(0)] public float ReactionDistance=2.2f,EscapeDistance=1.7f;
        [Min(.01f)] public float QuietSeconds=2.5f,EscapeSeconds=.35f,RecoverySeconds=3.5f;
        public float Alarm => alarm.Level;
        private readonly WildlifeAlarm alarm=new WildlifeAlarm();
        private float escape,lastRenderTime=float.NaN,stride;
        private Renderer body;
        private MaterialPropertyBlock properties;
        public float Walking { get; private set; }
        public float Stride=>stride;
        [Header("Safe coastal movement")]
        [Min(.01f)] public float WalkSpeed=.4f,FleeSpeed=2.8f;
        [Min(0)] public float ReturnStagger=1.1f;
        CoastalWildlifeNavigation navigation;
        public CoastalWildlifeNavigation Navigation {get=>navigation;set{if(navigation==value)return;navigation=value;route.Reset();idle.Reset();activity.Reset();}}
        readonly CoastalGroundRoute route=new CoastalGroundRoute();
        public bool WaitingForPath=>route.Waiting;
        public int DetourCount=>route.Detours;
        public void React(float seconds,Vector3? player)
        {
            if(seconds<=0)return;
            bool nearby=player.HasValue&&Mathf.Abs(Center.y-player.Value.y)<1.5f&&Vector3.ProjectOnPlane(Center-player.Value,Vector3.up).sqrMagnitude<ReactionDistance*ReactionDistance;
            if(nearby&&alarm.Level<.02f)
            {
                float sign=Vector3.Dot(Center-player.Value,Tangent)>=0?1:-1;
                escape=sign*EscapeDistance;
            }
            float stagger=Navigation==null?0:ReturnStagger*(.5f+.5f*Mathf.Sin(Phase*1.7f));
            alarm.Advance(seconds,nearby,QuietSeconds+stagger,EscapeSeconds,RecoverySeconds);
        }
        public void Render(float time)
        {
            float seconds=float.IsNaN(lastRenderTime)?0:Mathf.Max(0,time-lastRenderTime);
            activity.Advance(seconds);
            float naturalTime=activity.Configured?activity.Time:time;
            float cycle=(naturalTime+Phase*2)/Mathf.Max(1,CrawlPeriod),index=Mathf.Floor(cycle);
            float u=Mathf.Clamp01((cycle-index-RestFraction)/Mathf.Max(.1f,1-RestFraction));
            float wander=Mathf.Lerp(Mathf.Sin(index*1.73f+Phase),Mathf.Sin((index+1)*1.73f+Phase),Mathf.SmoothStep(0,1,u))*Travel;
            // Follow the coastline at the home's sand inset, including rounded corners.
            double homeAngle=System.Math.Atan2(Center.z,Center.x),homeRadius=System.Math.Sqrt(Center.x*Center.x+Center.z*Center.z);
            double inset=System.Math.Max(.65,IslandLayout.CoastRadius(homeAngle)-homeRadius);
            double angle=homeAngle+(wander*(1-alarm.Level*.65f)+escape*Mathf.SmoothStep(0,1,alarm.Level))/System.Math.Max(1,homeRadius);
            double radius=IslandLayout.CoastRadius(angle)-inset;
            var position=new Vector3((float)(System.Math.Cos(angle)*radius),0,(float)(System.Math.Sin(angle)*radius));
            position.y=(float)IslandLayout.Height(position.x,position.z)-.008f;
            if(Navigation!=null&&seconds>0)
            {
                float scale=Mathf.Max(transform.localScale.x,transform.localScale.z);
                position=route.Move(Navigation,transform.localPosition,position,Center,Travel+EscapeDistance,.43f*scale,.30f*scale,
                    alarm.Level>.4f?FleeSpeed:WalkSpeed*activity.Motion,seconds);
            }
            else if(Navigation!=null&&!float.IsNaN(lastRenderTime))position=transform.localPosition;
            float distance=Vector3.Distance(position,transform.localPosition);
            if(seconds>0)
            {
                stride+=distance*28;Walking=Mathf.MoveTowards(Walking,Mathf.Clamp01(distance/seconds*3),seconds*6);
            }
            bool settled=alarm.Level==0&&!WaitingForPath&&u==0&&Walking<.05f&&(seconds<=0||distance/seconds<.02f);
            idle.Advance(seconds,settled,Phase,Habits,activity.Foraging);
            transform.localPosition=position;lastRenderTime=time;
            transform.localRotation=Quaternion.Euler(0,Mathf.Atan2(position.x,position.z)*Mathf.Rad2Deg+Mathf.Sin(time*.7f+Phase)*3*(1-Walking),0);
            if(body==null)
            {
                body=GetComponent<Renderer>();
                if(body!=null){var bounds=body.localBounds;bounds.Encapsulate(new Bounds(new Vector3(0,.15f,0),new Vector3(.86f,.30f,.86f)));body.localBounds=bounds;}
            }
            if(body!=null)
            {
                if(properties==null)properties=new MaterialPropertyBlock();
                body.GetPropertyBlock(properties);properties.SetVector("_CrabMotion",new Vector4(stride,Walking,Phase,1));
                properties.SetVector("_CrabPose",new Vector4(idle.Pose.x*Mathf.Deg2Rad,idle.Pose.y*Mathf.Deg2Rad,idle.Pose.z*Mathf.Deg2Rad,1));body.SetPropertyBlock(properties);
            }
        }
    }
}
