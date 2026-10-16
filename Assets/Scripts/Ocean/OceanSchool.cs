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
        [Header("Boids flocking")]
        [Min(.01f)] public float NeighbourRadius=1.8f;
        [Min(.01f)] public float SeparationRadius=.7f;
        [Min(0)] public float SeparationWeight=1.8f,AlignmentWeight=.85f,CohesionWeight=.65f;
        [Min(0)] public float CruiseWeight=.55f;
        [Min(.01f)] public float Acceleration=1.5f;
        [Header("Three dimensional swimming")]
        [Min(0)] public float VerticalRange=.22f;
        [Min(0)] public float VerticalSpeed=.12f;
        [Range(0,25)] public float MaximumPitch=18;
        [Header("Disturbance response")]
        public const float MaxFleeDistance=.85f,MaxScatterDistance=.38f;
        [Min(0)] public float ReactionDistance=7;
        [Min(0)] public float PlayerReactionDistance=6.5f;
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
        [System.NonSerialized] public OceanFishAnimation Animation;
        [System.NonSerialized] public OceanFishAnatomy[] Anatomy;
        internal bool HasEnvironment,EnvironmentRain;
        internal float EnvironmentLight=1,EnvironmentTwilight;
        public void SetEnvironment(float light,bool rain,float twilight)
        {HasEnvironment=true;EnvironmentLight=Mathf.Clamp01(light);EnvironmentRain=rain;EnvironmentTwilight=Mathf.Clamp01(twilight);}
        public float WaterFloor { get; private set; }=float.NegativeInfinity;
        public float WaterCeiling { get; private set; }=float.PositiveInfinity;
        public OceanSwimObstacle[] WaterObstacles { get; private set; }
        // OceanSurface owns these local-space AA bounds. Configure before ResetSteering / Render;
        // this only prepares runtime navigation and never edits baked members or shared resources.
        public void ConfigureWater(float floor,float ceiling,OceanSwimObstacle[] obstacles)
        {
            WaterFloor=floor;WaterCeiling=ceiling;
            if(obstacles==null){WaterObstacles=null;return;}
            var nearby=new System.Collections.Generic.List<OceanSwimObstacle>();
            foreach(var obstacle in obstacles)
                if(Vector2.Distance(obstacle.Center,new Vector2(Center.x,Center.z))<HomeRadius+obstacle.Radius+1)nearby.Add(obstacle);
            WaterObstacles=nearby.ToArray();
        }
        public Vector3 EscapeDirection=>escapeDirection;
        public Vector3 ActivityPosition=>Steering==null?transform.position:transform.parent.TransformPoint(Steering.States[0].Position);
        public Vector3 DeformedVertex(Vector3 vertex,Vector4 pivot,Vector4 shape)
        {
            if(Steering==null)return transform.localPosition+OceanFishMotion.Deform(vertex,pivot,shape,Guide,MotionTime,Mathf.Clamp(ScatterDistance,0,MaxScatterDistance)*Alarm);
            int index=Mathf.RoundToInt((pivot.w-1)/2.399963f);var state=Steering.RenderState(index);
            if(Steering.Spines!=null)
            {
                var p=OceanFishMotion.Local(vertex,pivot,shape);var spine=Steering.Spines[index];var flat=spine.Deform(new Vector2(p.x,p.z));p.x=flat.x;p.z=flat.y;
                if(shape.y>2.5f)p.y+=Mathf.Sin(spine.RenderPhase)*.012f*Mathf.Clamp01(Mathf.Abs(p.x)/.45f);
                return state.Position+OceanFishMotion.Orient(p,state.Yaw,state.Pitch);
            }
            return state.Position+OceanFishMotion.Body(vertex,pivot,shape,MotionTime,state.Yaw,state.Pitch);
        }
        public void ResetSteering(){Steering=null;MotionTime=0;presentedTime=float.NaN;}
        public int PresentationCount { get; private set; }
        float presentedTime=float.NaN;
        private readonly WildlifeAlarm alarm=new WildlifeAlarm();
        private Vector3 escapeDirection;
        private float extraAngle;
        private bool fishingNearby;
        private bool playerNearby;
        private Vector3 playerThreatAnchor;
        private Renderer body;
        private MaterialPropertyBlock properties;
        public bool Startle(Vector3 localPoint)
        {
            var away=Center-localPoint;away.y=0;
            if(away.sqrMagnitude>ReactionDistance*ReactionDistance)return false;
            if(alarm.Level<.02f)escapeDirection=away.sqrMagnitude>.001f?away.normalized:Vector3.right;
            alarm.Trigger(QuietSeconds);return true;
        }
        public void React(float seconds,Vector3? fishingPoint,Vector3? playerPoint=null)
        {
            if(seconds<=0)return;
            bool nearby=fishingPoint.HasValue&&Vector3.ProjectOnPlane(fishingPoint.Value-Center,Vector3.up).sqrMagnitude<ReactionDistance*ReactionDistance;
            if(nearby&&!fishingNearby)Startle(fishingPoint.Value);
            fishingNearby=nearby;
            bool threat=false;
            if(playerPoint.HasValue&&PlayerReactionDistance>0)
            {
                var point=playerPoint.Value;
                float distance=PlayerReactionDistance;
                if(playerNearby)
                {
                    distance+=.8f;
                    var gap=point-playerThreatAnchor;
                    threat=Mathf.Abs(gap.y)<1.65f&&gap.x*gap.x+gap.z*gap.z<distance*distance;
                }
                if(!threat&&Steering!=null)
                    foreach(var member in Steering.States)
                    {
                        var gap=point-member.Position;
                        if(Mathf.Abs(gap.y)<1.65f&&gap.x*gap.x+gap.z*gap.z<PlayerReactionDistance*PlayerReactionDistance)
                        {threat=true;playerThreatAnchor=member.Position;break;}
                    }
                if(threat&&!playerNearby)
                {
                    var away=Center-point;away.y=0;
                    escapeDirection=away.sqrMagnitude>.001f?away.normalized:Vector3.right;
                }
            }
            playerNearby=threat;
            alarm.Advance(seconds,threat,QuietSeconds,.22f,RecoverySeconds);
            extraAngle+=seconds*Speed*2.2f*alarm.Level;
        }
        public void Render(float time,bool submit=true)
        {
            float seconds=Mathf.Max(0,time-MotionTime);
            if(Swimmers.Length==Members&&Members>0&&Members<=32)
            {
                if(Steering==null||time<MotionTime)
                {
                    Steering=new OceanFishSteering(this);seconds=Mathf.Max(0,time);
                }
                Steering.Advance(seconds);
            }
            float angle=Phase+time*Speed+extraAngle;
            MotionTime=time;Guide=new Vector4(Radius,OrbitAspect,angle,Speed);
            if(submit)Present();
        }
        public Bounds PresentationBounds
        {
            get
            {
                if(body==null)body=GetComponent<Renderer>();
                return body!=null?body.bounds:new Bounds(transform.position,Vector3.one*HomeRadius*2);
            }
        }
        // Called for the owned gameplay camera immediately before culling, after camera following.
        // Simulation always continues; only interpolation and GPU submission are omitted offscreen.
        public bool PresentLatest()
        {
            if(presentedTime==MotionTime)return false;
            Present();return true;
        }
        void Present()
        {
            float time=MotionTime,angle=Guide.z;
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
                if(Steering!=null)
                {
                    Steering.WritePoses(transform.localPosition);properties.SetVectorArray("_FishPoses",Steering.Poses);
                    properties.SetVectorArray("_FishAttitudes",Steering.Attitudes);
                    properties.SetFloat("_FishBodyEnabled",Steering.Spines==null?0:1);
                    if(Steering.Spines!=null)properties.SetVectorArray("_FishSpines",Steering.SpinePoses);
                    // Per-renderer bounds include depth excursions without touching the shared AA mesh.
                    float extent=HomeRadius+Radius+1;
                    float height=Mathf.Abs(transform.localPosition.y-Center.y)+VerticalRange+1;
                    Vector3 boundsCenter=Vector3.zero;
                    if(!float.IsInfinity(WaterFloor)&&!float.IsInfinity(WaterCeiling))
                    {
                        boundsCenter=Center-transform.localPosition;
                        boundsCenter.y=(WaterFloor+WaterCeiling)*.5f-transform.localPosition.y;
                        height=(WaterCeiling-WaterFloor)*.5f+.05f;extent=HomeRadius+.1f;
                    }
                    body.localBounds=new Bounds(boundsCenter,new Vector3(extent*2,height*2,extent*2));
                }
                properties.SetFloat("_OceanTime",time);
                properties.SetFloat("_FishScatter",Mathf.Clamp(ScatterDistance,0,MaxScatterDistance)*alarm.Level);body.SetPropertyBlock(properties);
                presentedTime=time;PresentationCount++;
            }
        }
    }
}
