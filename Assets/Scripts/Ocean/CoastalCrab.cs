using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.Ocean
{
    public sealed class CoastalCrab : MonoBehaviour
    {
        public Vector3 Center;
        public Vector3 Tangent;
        public float Phase,Travel=.45f;
        [Header("Disturbance response")]
        [Min(0)] public float ReactionDistance=2.2f,EscapeDistance=1.7f;
        [Min(.01f)] public float QuietSeconds=2.5f,EscapeSeconds=.35f,RecoverySeconds=3.5f;
        public float Alarm => alarm.Level;
        private readonly WildlifeAlarm alarm=new WildlifeAlarm();
        private float escape,scurryPhase;
        public void React(float seconds,Vector3? player)
        {
            if(seconds<=0)return;
            bool nearby=player.HasValue&&Mathf.Abs(Center.y-player.Value.y)<1.5f&&Vector3.ProjectOnPlane(Center-player.Value,Vector3.up).sqrMagnitude<ReactionDistance*ReactionDistance;
            if(nearby&&alarm.Level<.02f)
            {
                float sign=Vector3.Dot(Center-player.Value,Tangent)>=0?1:-1;
                escape=sign*EscapeDistance;
            }
            alarm.Advance(seconds,nearby,QuietSeconds,EscapeSeconds,RecoverySeconds);
            scurryPhase+=seconds*alarm.Level*3;
        }
        public void Render(float time)
        {
            float t=time*.55f+Phase+scurryPhase;
            // Follow the coastline at the home's sand inset, including rounded corners.
            double homeAngle=System.Math.Atan2(Center.z,Center.x),homeRadius=System.Math.Sqrt(Center.x*Center.x+Center.z*Center.z);
            double inset=System.Math.Max(.65,IslandLayout.CoastRadius(homeAngle)-homeRadius);
            double angle=homeAngle+(Mathf.Sin(t)*Travel*(1-alarm.Level*.65f)+escape*alarm.Level)/System.Math.Max(1,homeRadius);
            double radius=IslandLayout.CoastRadius(angle)-inset;
            var position=new Vector3((float)(System.Math.Cos(angle)*radius),0,(float)(System.Math.Sin(angle)*radius));
            position.y=(float)IslandLayout.Height(position.x,position.z)-.008f;
            transform.localPosition=position;
            transform.localRotation=Quaternion.Euler(0,Mathf.Atan2(Tangent.x,Tangent.z)*Mathf.Rad2Deg+90,Mathf.Sin(t*9)*2);
        }
    }
}
