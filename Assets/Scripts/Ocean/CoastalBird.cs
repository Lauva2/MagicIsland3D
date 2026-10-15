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
        [Header("Disturbance response")]
        [Min(0)] public float ReactionDistance=3.2f,EscapeDistance=4,EscapeHeight=3;
        [Min(.01f)] public float QuietSeconds=4,TakeoffSeconds=.65f,RecoverySeconds=4;
        public float Alarm => alarm.Level;
        public int TakeoffCount { get; private set; }
        private readonly WildlifeAlarm alarm=new WildlifeAlarm();
        private Vector3 escapeDirection;
        private float restUntil;

        public void React(float seconds,Vector3? player,float time,float daylight,bool raining)
        {
            if(seconds<=0)return;
            var home=Activity==CoastalBirdActivity.Perching?Center:transform.localPosition;
            bool nearby=player.HasValue&&daylight>.12f&&!raining&&Mathf.Abs(home.y-player.Value.y)<1.6f&&Vector3.ProjectOnPlane(home-player.Value,Vector3.up).sqrMagnitude<ReactionDistance*ReactionDistance;
            if(nearby&&alarm.Level<.02f)
            {
                var outward=new Vector3(Center.x,0,Center.z).normalized;
                var away=Vector3.ProjectOnPlane(home-player.Value,Vector3.up).normalized;
                escapeDirection=(outward+away*.35f).normalized;
            }
            float previous=alarm.Level;alarm.Advance(seconds,nearby,QuietSeconds,TakeoffSeconds,RecoverySeconds);
            if(previous==0&&alarm.Level>0)TakeoffCount++;
            if(previous>0&&alarm.Level==0)restUntil=time+8;
        }

        public void Render(float time,float daylight,bool raining)
        {
            bool visible=daylight>.12f&&(!raining||Activity==CoastalBirdActivity.Perching);
            if(gameObject.activeSelf!=visible)gameObject.SetActive(visible);
            if(!visible)return;
            float angle=Phase+time*Speed,flap=Mathf.Sin(time*5.2f+Phase)*28;
            float flight=1;
            Vector3 offset;
            if(Activity==CoastalBirdActivity.Perching)
            {
                float cycle=Mathf.Repeat(time+Phase*5,42);
                flight=cycle<18||raining?0:Mathf.Pow(Mathf.Sin((cycle-18)/24*Mathf.PI),2);
                if(alarm.Level>0||time<restUntil)flight=0;
                offset=new Vector3(Mathf.Cos(angle)*Radius,2.5f,Mathf.Sin(angle)*Radius*.6f)*flight;
            }
            else
            {
                float height=Activity==CoastalBirdActivity.Skimming?.10f: .32f;
                offset=new Vector3(Mathf.Cos(angle)*Radius,Mathf.Sin(angle*2)*height,Mathf.Sin(angle)*Radius*.65f);
                if(Activity==CoastalBirdActivity.Circling)flap*=.45f+.55f*Mathf.SmoothStep(0,1,Mathf.Sin(time*.6f+Phase));
            }
            float frightened=raining?0:alarm.Level;
            offset+=(escapeDirection*EscapeDistance+Vector3.up*EscapeHeight)*frightened;
            flight=Mathf.Max(flight,frightened);
            transform.localPosition=Center+offset;
            float yaw=Mathf.Atan2(-Mathf.Sin(angle),Mathf.Cos(angle)*.65f)*Mathf.Rad2Deg;
            transform.localRotation=Quaternion.Euler(0,Activity==CoastalBirdActivity.Perching?Mathf.LerpAngle(Phase*Mathf.Rad2Deg,yaw,flight):yaw,-Mathf.Sin(angle)*7*flight);
            if(LeftWing!=null)LeftWing.localRotation=Quaternion.Euler(0,Mathf.Lerp(-68,0,flight),Mathf.Lerp(-10,flap,flight));
            if(RightWing!=null)RightWing.localRotation=Quaternion.Euler(0,Mathf.Lerp(68,0,flight),Mathf.Lerp(10,-flap,flight));
        }
    }
}
