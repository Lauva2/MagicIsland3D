using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    public sealed class OceanInteractionEffects : MonoBehaviour
    {
        public OceanEffectParticle[] Particles=Array.Empty<OceanEffectParticle>();
        [Min(0)] public float VisibleDistance=26;
        [Range(0,1)] public float SplashStrength=.65f,WakeStrength=.16f,SandStrength=.22f;
        [Min(.1f)] public float FishInterval=.48f,RayInterval=.65f,CrabInterval=.23f;
        public int ActiveCount { get { int count=0;foreach(var p in Particles)if(p!=null&&p.Active)count++;return count; } }
        public int EmissionCount { get; private set; }
        public bool IsValid
        {
            get
            {
                if(Particles.Length!=84)return false;
                int flags=0;foreach(var p in Particles){if(p==null||p.View==null||p.View.sharedMaterial==null||p.View.sharedMaterial.shader==null||p.GetComponent<MeshFilter>()?.sharedMesh==null)return false;flags|=1<<(int)p.Kind;}
                return flags==63&&GetComponentsInChildren<Collider>(true).Length==0;
            }
        }
        private OceanSurface owner;
        private Vector3[] fishPrevious,crabPrevious;
        private float[] fishTimer,crabTimer;
        private Vector3[] floorVertices;
        private const int Sectors=256;
        private int rings,sequence;
        private void Initialize(OceanSurface ocean)
        {
            if(owner==ocean)return;owner=ocean;
            fishPrevious=new Vector3[ocean.Schools.Length];crabPrevious=new Vector3[ocean.Crabs.Length];fishTimer=new float[fishPrevious.Length];crabTimer=new float[crabPrevious.Length];
            for(int i=0;i<fishPrevious.Length;i++)fishPrevious[i]=ocean.Schools[i].ActivityPosition;
            for(int i=0;i<crabPrevious.Length;i++)crabPrevious[i]=ocean.Crabs[i].transform.position;
            floorVertices=ocean.Shore.GetComponent<MeshFilter>().sharedMesh.vertices;rings=floorVertices.Length/Sectors;
        }
        private OceanEffectParticle Acquire(OceanEffectKind kind)
        {
            OceanEffectParticle oldest=null;
            foreach(var p in Particles)
            {
                if(p==null||p.Kind!=kind)continue;
                if(!p.Active)return p;
                if(oldest==null||p.Age/Mathf.Max(.05f,p.Lifetime)>oldest.Age/Mathf.Max(.05f,oldest.Lifetime))oldest=p;
            }
            return oldest;
        }
        private void Emit(OceanEffectKind kind,Vector3 position,Vector3 velocity,float size,float lifetime,float strength,float delay=0)
        {
            var slot=Acquire(kind);if(slot==null||strength<=0)return;
            slot.Begin(position,velocity,size,lifetime,strength,delay,++sequence*2.399963f,owner);EmissionCount++;
        }
        public void Splash(Vector3 point,OceanSurface ocean)
        {
            Initialize(ocean);point.y=ocean.HeightAt(point)+.025f;
            Emit(OceanEffectKind.Ripple,point,Vector3.zero,.80f,1.25f,SplashStrength);
            Emit(OceanEffectKind.Ripple,point,Vector3.zero,.62f,1.4f,SplashStrength*.65f,.13f);
            for(int i=0;i<8;i++)
            {
                float angle=i*2.399963f;var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Emit(OceanEffectKind.Droplet,point+radial*.035f,radial*(.35f+(i%3)*.12f)+Vector3.up*(.85f+(i%4)*.12f),.055f+(i%3)*.015f,.32f+(i%3)*.04f,SplashStrength*.8f);
            }
        }
        public void Tick(float seconds,OceanSurface ocean,Vector3? player)
        {
            Initialize(ocean);if(seconds<=0)return;
            foreach(var particle in Particles)if(particle!=null)particle.Step(seconds,ocean);
            for(int i=0;i<fishPrevious.Length;i++)
            {
                var school=ocean.Schools[i];var position=school.ActivityPosition;var motion=position-fishPrevious[i];fishPrevious[i]=position;
                fishTimer[i]=Mathf.Max(0,fishTimer[i]-seconds);
                if(fishTimer[i]>0||motion.sqrMagnitude<.000005f||!Nearby(position,player))continue;
                bool ray=school.Species==ShallowFishSpecies.SpottedRay;
                if(ray)
                {
                    var sand=position-Vector3.ProjectOnPlane(motion,Vector3.up).normalized*.28f;
                    if(TryBed(sand,out float floor))
                    {
                        sand.y=floor+.035f;
                        Emit(OceanEffectKind.ReefDust,sand,new Vector3(-motion.x,0,-motion.z).normalized*.045f+Vector3.up*.025f,.25f,1.25f,SandStrength*.65f);
                    }
                }
                if(school.Alarm>.2f)
                {
                    position-=Vector3.ProjectOnPlane(motion,Vector3.up).normalized*.32f;
                    Emit(OceanEffectKind.FishWake,position,Vector3.ProjectOnPlane(-motion,Vector3.up).normalized*.08f,.32f,.72f,WakeStrength*school.Alarm*(1-ocean.Rain*.3f));
                }
                fishTimer[i]=ray?Mathf.Max(.1f,RayInterval):Mathf.Max(.1f,FishInterval);
            }
            for(int i=0;i<crabPrevious.Length;i++)
            {
                var crab=ocean.Crabs[i];var position=crab.transform.position;var motion=position-crabPrevious[i];crabPrevious[i]=position;
                crabTimer[i]=Mathf.Max(0,crabTimer[i]-seconds);
                if(crabTimer[i]>0||crab.Alarm<.2f||motion.sqrMagnitude<.000005f||!Nearby(position,player))continue;
                var point=position-Vector3.ProjectOnPlane(motion,Vector3.up).normalized*.10f+Vector3.up*.025f;
                if(ocean.Rain<.5f)Emit(OceanEffectKind.ShorePuff,point,Vector3.up*.035f,.12f,.55f,SandStrength*.55f);
                for(int j=0;j<3;j++)
                {
                    float angle=j*2.399963f+sequence;var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    Emit(OceanEffectKind.ShoreGrain,point,radial*.14f+Vector3.up*(.25f+j*.05f),.027f+j*.005f,.23f+j*.025f,SandStrength*1.4f);
                }
                crabTimer[i]=Mathf.Max(.1f,CrabInterval);
            }
        }
        private bool Nearby(Vector3 point,Vector3? player)=>!player.HasValue||(point-player.Value).sqrMagnitude<=VisibleDistance*VisibleDistance;
        public bool TryBed(Vector3 world,out float height)
        {
            height=0;if(owner==null||rings<2||floorVertices.Length!=Sectors*rings)return false;
            var p=owner.Shore.transform.InverseTransformPoint(world);double angle=Math.Atan2(p.z,p.x);if(angle<0)angle+=Math.PI*2;
            int sector=Math.Min(Sectors-1,(int)(angle/(Math.PI*2)*Sectors)),next=(sector+1)%Sectors;
            for(int j=0;j<rings-1;j++)
            {
                var a=floorVertices[sector*rings+j];var b=floorVertices[next*rings+j];var c=floorVertices[sector*rings+j+1];var d=floorVertices[next*rings+j+1];
                if(Triangle(p,a,b,c,out float y)||Triangle(p,c,b,d,out y)){height=owner.Shore.transform.TransformPoint(new Vector3(p.x,y,p.z)).y;return true;}
            }
            return false;
        }
        private static bool Triangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c,out float y)
        {
            float determinant=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            if(Mathf.Abs(determinant)<.000001f){y=0;return false;}
            float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/determinant,v=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/determinant,w=1-u-v;
            y=u*a.y+v*b.y+w*c.y;return u>=-.0001f&&v>=-.0001f&&w>=-.0001f;
        }
    }
}
