using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    [Serializable]
    public sealed class OceanFishAnimation
    {
        [Range(5,35)] public float JointAngle=26;
        [Range(.1f,20)] public float MuscleResponse=6;
        [Range(0,.95f)] public float Inertia=.65f;
        [Range(0,1)] public float TailStrength=.65f;
        [Range(.1f,1)] public float NightSpeed=.55f,RainSpeed=.82f;
        [Range(1,1.5f)] public float TwilightSpeed=1.15f;
        [Range(.1f,1)] public float NightForaging=.5f,RainForaging=.75f;
        [Range(.05f,1)] public float NightExploration=.25f,RainExploration=.55f;
        [Range(0,1)] public float TwilightShelter=.75f,NightShelter=.85f,RainShelter=.4f;
        [Range(.5f,20)] public float TransitionSeconds=5;
        [Range(.5f,3)] public float PassageLookAhead=1.6f;
        [Range(0,1.5f)] public float PassageStagger=.6f;
        [Range(1,8)] public float RegroupSeconds=3;
        static bool Range(float v,float a,float b)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&v>=a&&v<=b;
        public bool IsValid=>Range(JointAngle,5,35)&&Range(MuscleResponse,.1f,20)&&Range(Inertia,0,.95f)&&Range(TailStrength,0,1)
            &&Range(NightSpeed,.1f,1)&&Range(RainSpeed,.1f,1)&&Range(TwilightSpeed,1,1.5f)
            &&Range(NightForaging,.1f,1)&&Range(RainForaging,.1f,1)&&Range(TransitionSeconds,.5f,20)
            &&Range(NightExploration,.05f,1)&&Range(RainExploration,.05f,1)&&Range(TwilightShelter,0,1)&&Range(NightShelter,0,1)&&Range(RainShelter,0,1)
            &&Range(PassageLookAhead,.5f,3)&&Range(PassageStagger,0,1.5f)&&Range(RegroupSeconds,1,8);
    }

    // Canonical cross-section endpoints exactly enclose the authored mesh in X/Z.
    // Construct once from the loaded AA mesh; no mesh copies or per-frame collections.
    public sealed class OceanFishAnatomy
    {
        public readonly float Head,Tail,Radius;
        public readonly Vector2[] Samples;
        public OceanFishAnatomy(float head,float tail,float radius,Vector2[] samples)
        {
            if(!Finite(head)||!Finite(tail)||!Finite(radius)||head-tail<.001f||radius<=0||samples==null||samples.Length==0)
                throw new ArgumentException("Invalid fish body geometry");
            foreach(var p in samples)if(!Finite(p.x)||!Finite(p.y)||p.sqrMagnitude>radius*radius+.00001f)
                throw new ArgumentException("Fish mesh exceeds its baked body bounds; rebake fish schools");
            Head=head;Tail=tail;Radius=radius;Samples=samples;
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public static OceanFishAnatomy[] Read(Mesh mesh,OceanFishMember[] members)
        {
            var vertices=mesh.vertices;var pivots=new System.Collections.Generic.List<Vector4>();var shapes=new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(1,pivots);mesh.GetUVs(2,shapes);
            var sections=new System.Collections.Generic.Dictionary<float,Vector2>[members.Length];
            for(int i=0;i<sections.Length;i++)sections[i]=new System.Collections.Generic.Dictionary<float,Vector2>();
            for(int v=0;v<vertices.Length;v++)
            {
                int i=Mathf.RoundToInt((pivots[v].w-1)/2.399963f);
                var p=OceanFishMotion.Local(vertices[v],pivots[v],shapes[v]);var table=sections[i];
                if(table.TryGetValue(p.z,out var width))table[p.z]=new Vector2(Mathf.Min(width.x,p.x),Mathf.Max(width.y,p.x));
                else table.Add(p.z,new Vector2(p.x,p.x));
            }
            var result=new OceanFishAnatomy[members.Length];
            for(int i=0;i<result.Length;i++)
            {
                float head=float.NegativeInfinity,tail=float.PositiveInfinity;var samples=new Vector2[sections[i].Count*2];int n=0;
                foreach(var entry in sections[i]){head=Mathf.Max(head,entry.Key);tail=Mathf.Min(tail,entry.Key);samples[n++]=new Vector2(entry.Value.x,entry.Key);samples[n++]=new Vector2(entry.Value.y,entry.Key);}
                var hull=new System.Collections.Generic.List<Vector2>();
                for(int segment=0;segment<OceanFishSpine.Nodes-1;segment++)
                {
                    var group=new System.Collections.Generic.List<Vector2>();
                    foreach(var sample in samples)
                    {
                        int index=Mathf.Min(OceanFishSpine.Nodes-2,(int)((head-sample.y)/Mathf.Max(.0001f,head-tail)*(OceanFishSpine.Nodes-1)));
                        if(index==segment)group.Add(sample);
                    }
                    hull.AddRange(Hull(group));
                }
                result[i]=new OceanFishAnatomy(head,tail,members[i].Radius-.005f,hull.ToArray());
            }
            return result;
        }
        static System.Collections.Generic.List<Vector2> Hull(System.Collections.Generic.List<Vector2> points)
        {
            points.Sort((a,b)=>a.x!=b.x?a.x.CompareTo(b.x):a.y.CompareTo(b.y));
            var hull=new System.Collections.Generic.List<Vector2>();
            for(int pass=0;pass<2;pass++)
            {
                int start=hull.Count;
                for(int n=0;n<points.Count;n++)
                {
                    var p=points[pass==0?n:points.Count-1-n];
                    while(hull.Count>=start+2)
                    {
                        var a=hull[hull.Count-1]-hull[hull.Count-2];var b=p-hull[hull.Count-1];
                        if(a.x*b.y-a.y*b.x>0)break;hull.RemoveAt(hull.Count-1);
                    }
                    hull.Add(p);
                }
                if(hull.Count>start)hull.RemoveAt(hull.Count-1);
            }
            return hull;
        }
    }

    // A head-led position based chain. Every link is projected to its rest distance;
    // muscles steer links while prior world positions provide turn/translation lag.
    // Curvature is reduced (without shortening links) until ALL authored cross-sections
    // fit the existing navigation envelope. No larger collision radius or rebake required.
    public sealed class OceanFishSpine
    {
        public const int Nodes=5;
        readonly OceanFishAnatomy shape;
        readonly OceanFishAnimation settings;
        readonly Vector2[] current=new Vector2[Nodes],previous=new Vector2[Nodes],olderWorld=new Vector2[Nodes],rendered=new Vector2[Nodes],predicted=new Vector2[Nodes];
        readonly float[] angles=new float[Nodes-1];
        readonly float rest;
        readonly bool ray;
        Vector2 lastCenter;
        float lastYaw;
        float previousPhase;
        public float Phase {get;private set;}
        public float RenderPhase {get;private set;}
        public OceanFishAnatomy Anatomy=>shape;
        public OceanFishSpine(OceanFishAnatomy shape,OceanFishAnimation settings,Vector3 center,float yaw,float phase,bool ray)
        {
            this.shape=shape;this.settings=settings;this.ray=ray;rest=(shape.Head-shape.Tail)/(Nodes-1);Phase=previousPhase=RenderPhase=phase;
            lastCenter=new Vector2(center.x,center.z);lastYaw=yaw;
            for(int j=0;j<Nodes;j++){current[j]=previous[j]=rendered[j]=new Vector2(0,shape.Head-j*rest);olderWorld[j]=lastCenter+OceanFishMotion.Rotate(current[j],yaw);}
        }
        public void Step(Vector3 position,float yaw,float seconds,float speed)
        {
            var center=new Vector2(position.x,position.z);
            previousPhase=Phase;
            Phase+=seconds*(ray?Mathf.Lerp(.9f,2.1f,Mathf.Clamp01(speed/.27f)):Mathf.Lerp(2.0f,8.0f,Mathf.Clamp01(speed/.55f)));
            for(int j=0;j<Nodes;j++)
            {
                previous[j]=current[j];var world=lastCenter+OceanFishMotion.Rotate(current[j],lastYaw);
                var prediction=world+(world-olderWorld[j])*settings.Inertia;olderWorld[j]=world;
                predicted[j]=OceanFishMotion.Rotate(prediction-center,-yaw);
            }
            current[0]=new Vector2(0,shape.Head);float parent=0;
            for(int j=1;j<Nodes;j++)
            {
                var direction=predicted[j]-current[j-1];float lag=Mathf.Atan2(-direction.x,-direction.y);
                float wave=Mathf.Sin(Phase-j*.9f)*settings.JointAngle*Mathf.Deg2Rad*settings.TailStrength*j/(Nodes-1)*(ray?.13f:1);
                float angle=Mathf.LerpAngle(lag*Mathf.Rad2Deg,wave*Mathf.Rad2Deg,1-Mathf.Exp(-settings.MuscleResponse*seconds))*Mathf.Deg2Rad;
                float limit=settings.JointAngle*Mathf.Deg2Rad*(ray?.25f:1);
                angle=parent+Mathf.Clamp(Mathf.DeltaAngle(parent*Mathf.Rad2Deg,angle*Mathf.Rad2Deg)*Mathf.Deg2Rad,-limit,limit);
                angles[j-1]=angle;current[j]=current[j-1]+new Vector2(-Mathf.Sin(angle),-Mathf.Cos(angle))*rest;parent=angle;
            }
            Fit(current);lastCenter=center;lastYaw=yaw;
        }
        void Project(Vector2[] target,float strength)
        {
            target[0]=new Vector2(0,shape.Head);
            for(int j=1;j<Nodes;j++){float a=angles[j-1]*strength;target[j]=target[j-1]+new Vector2(-Mathf.Sin(a),-Mathf.Cos(a))*rest;}
        }
        void Fit(Vector2[] target)
        {
            for(int j=1;j<Nodes;j++){var d=target[j]-target[j-1];angles[j-1]=Mathf.Atan2(-d.x,-d.y);}
            float strength=1;
            for(int attempt=0;attempt<8;attempt++)
            {
                if(Fits(target))return;strength*=.5f;Project(target,strength);
            }
            Project(target,0); // Straight rest geometry is inside its original baked bounds.
        }
        bool Fits(Vector2[] target)
        {
            float radius=shape.Radius*shape.Radius;
            foreach(var p in shape.Samples)if(Deform(target,p).sqrMagnitude>radius)return false;
            return true;
        }
        Vector2 Deform(Vector2[] points,Vector2 p)
        {
            float u=Mathf.Clamp((shape.Head-p.y)/Mathf.Max(.0001f,rest),0,Nodes-1);int j=Mathf.Min(Nodes-2,(int)u);
            var direction=(points[j+1]-points[j])/rest;var right=new Vector2(-direction.y,direction.x);
            return Vector2.LerpUnclamped(points[j],points[j+1],u-j)+right*p.x;
        }
        // Called once per render, then shared by CPU validation and the shader pose upload.
        public void Interpolate(float alpha)
        {
            RenderPhase=Mathf.LerpUnclamped(previousPhase,Phase,alpha);
            rendered[0]=new Vector2(0,shape.Head);
            for(int j=1;j<Nodes;j++)
            {
                var a=previous[j]-previous[j-1];var b=current[j]-current[j-1];
                var d=Vector2.LerpUnclamped(a,b,alpha);if(d.sqrMagnitude<.000001f)d=Vector2.down;
                rendered[j]=rendered[j-1]+d.normalized*rest;
            }
            Fit(rendered);
        }
        public Vector2 Node(int index)=>rendered[index];
        public Vector2 Deform(Vector2 point)=>Deform(rendered,point);
        public void Write(Vector4[] buffer,int member)
        {for(int j=0;j<Nodes;j++)buffer[member*Nodes+j]=new Vector4(rendered[j].x,rendered[j].y,0,0);}
    }
}
