using UnityEngine;
namespace MagicIsland.Ocean
{
    public enum ShallowFishSpecies { SilverMinnow, Clownfish, Butterflyfish, SpottedRay }
    public sealed class OceanSchool : MonoBehaviour
    {
        public ShallowFishSpecies Species;
        public int Members;
        public Vector3 Center;
        public float Radius=1,Speed=.24f,Phase;
        public float OrbitAspect=.55f,BobAmplitude=.025f;
        [Header("Disturbance response")]
        public const float MaxFleeDistance=.85f,MaxScatterDistance=.38f;
        [Min(0)] public float ReactionDistance=7;
        [Range(0,MaxFleeDistance)] public float FleeDistance=MaxFleeDistance;
        [Range(0,MaxScatterDistance)] public float ScatterDistance=MaxScatterDistance;
        [Min(.01f)] public float QuietSeconds=1.8f,RecoverySeconds=3.2f;
        public float Alarm => alarm.Level;
        public Vector4 Guide { get; private set; }
        public float MotionTime { get; private set; }
        [Header("Baked habitat navigation")]
        public OceanBiome Habitat;
        public float HomeRadius=3.2f;
        public OceanFishMember[] Swimmers=System.Array.Empty<OceanFishMember>();
        public OceanSwimObstacle[] Obstacles=System.Array.Empty<OceanSwimObstacle>();
        public OceanFishSteering Steering { get; private set; }
        public Vector3 EscapeDirection=>escapeDirection;
        public Vector3 ActivityPosition=>Steering==null?transform.position:transform.parent.TransformPoint(Steering.States[0].Position);
        public Vector3 DeformedVertex(Vector3 vertex,Vector4 pivot,Vector4 shape)
        {
            if(Steering==null)return transform.localPosition+OceanFishMotion.Deform(vertex,pivot,shape,Guide,MotionTime,Mathf.Clamp(ScatterDistance,0,MaxScatterDistance)*Alarm);
            int index=Mathf.RoundToInt((pivot.w-1)/2.399963f);var state=Steering.States[index];
            return state.Position+OceanFishMotion.Body(vertex,pivot,shape,MotionTime,state.Yaw);
        }
        public void ResetSteering(){Steering=null;MotionTime=0;}
        private readonly WildlifeAlarm alarm=new WildlifeAlarm();
        private Vector3 escapeDirection;
        private float extraAngle;
        private bool fishingNearby;
        private Renderer body;
        private MaterialPropertyBlock properties;
        public bool Startle(Vector3 localPoint)
        {
            var away=Center-localPoint;away.y=0;
            if(away.sqrMagnitude>ReactionDistance*ReactionDistance)return false;
            if(alarm.Level<.02f)escapeDirection=away.sqrMagnitude>.001f?away.normalized:Vector3.right;
            alarm.Trigger(QuietSeconds);return true;
        }
        public void React(float seconds,Vector3? fishingPoint)
        {
            if(seconds<=0)return;
            bool nearby=fishingPoint.HasValue&&Vector3.ProjectOnPlane(fishingPoint.Value-Center,Vector3.up).sqrMagnitude<ReactionDistance*ReactionDistance;
            if(nearby&&!fishingNearby)Startle(fishingPoint.Value);
            fishingNearby=nearby;
            alarm.Advance(seconds,false,QuietSeconds,.22f,RecoverySeconds);
            extraAngle+=seconds*Speed*2.2f*alarm.Level;
        }
        public void Render(float time)
        {
            float seconds=Mathf.Max(0,time-MotionTime);
            if(Swimmers.Length==Members&&Members>0&&Members<=32)
            {
                if(Steering==null||time<MotionTime){Steering=new OceanFishSteering(this);seconds=Mathf.Max(0,time);}
                Steering.Advance(seconds);
            }
            float angle=Phase+time*Speed+extraAngle;
            MotionTime=time;Guide=new Vector4(Radius,OrbitAspect,angle,Speed);
            transform.localPosition=Center+new Vector3(Mathf.Cos(angle)*Radius,Mathf.Sin(angle*.7f)*BobAmplitude,Mathf.Sin(angle)*Radius*OrbitAspect)+escapeDirection*(Mathf.Clamp(FleeDistance,0,MaxFleeDistance)*alarm.Level);
            // Members evaluate their own guide delay, heading and tail beat in the shader.
            transform.localRotation=Quaternion.identity;
            if(body==null)body=GetComponent<Renderer>();
            if(body!=null)
            {
                if(properties==null)properties=new MaterialPropertyBlock();
                body.GetPropertyBlock(properties);
                properties.SetFloat("_FishEnabled",1);properties.SetVector("_FishGuide",Guide);
                properties.SetFloat("_FishSteered",Steering==null?0:1);
                if(Steering!=null){Steering.WritePoses(transform.localPosition);properties.SetVectorArray("_FishPoses",Steering.Poses);}
                properties.SetFloat("_OceanTime",time);
                properties.SetFloat("_FishScatter",Mathf.Clamp(ScatterDistance,0,MaxScatterDistance)*alarm.Level);body.SetPropertyBlock(properties);
            }
        }
    }
}
