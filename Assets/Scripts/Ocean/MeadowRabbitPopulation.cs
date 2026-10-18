using UnityEngine;
using MagicIsland.TerrainSystem;

namespace MagicIsland.Ocean
{
    // One batch, no per-animal objects/Update. WorldWaterEcology owns Bind/Tick/Release.
    sealed class MeadowRabbitPopulation
    {
        const float Step=1f/30;
        static readonly int PoseId=Shader.PropertyToID("_RabbitPose"),ActionId=Shader.PropertyToID("_RabbitActions"),VisibleId=Shader.PropertyToID("_RabbitVisible"),EarsId=Shader.PropertyToID("_RabbitEars");
        sealed class Rabbit
        {
            public MeadowRabbitHabitat Habitat;
            public float[] Distances;
            public float At,From,To,HopAge,HopDuration,Yaw,YawRate,Wait,RestAge,Fear,Age,Forage,Look,Alert,Visible;
            public float PreviousYaw,PreviousHop,PreviousForage,PreviousLook,PreviousAlert,PreviousVisible;
            public Vector3 Position,Previous;
            public Vector2 Ears,PreviousEars;
            public int Visits;
            public bool Hopping,Blocked,Threat,Hidden,Turning;
        }
        readonly Rabbit[] rabbits;
        readonly Vector4[] poses=new Vector4[8],actions=new Vector4[8];
        readonly Vector4[] ears=new Vector4[8];
        readonly float[] visible=new float[8];
        readonly Renderer renderer;
        readonly Transform space;
        readonly MaterialPropertyBlock original=new MaterialPropertyBlock(),block=new MaterialPropertyBlock();
        readonly MeadowRabbitNavigation navigation;
        float accumulator;
        public MeadowRabbitPopulation(Renderer renderer,MeadowRabbitHabitat[] habitats,Transform space,IslandTerrain terrain)
        {
            this.renderer=renderer;this.space=space;renderer.GetPropertyBlock(original);
            navigation=new MeadowRabbitNavigation(terrain,space);rabbits=new Rabbit[habitats.Length];
            for(int i=0;i<habitats.Length;i++)
            {
                var h=habitats[i];var d=new float[h.Track.Length];for(int j=1;j<d.Length;j++)d[j]=d[j-1]+Vector3.Distance(h.Track[j],h.Track[j-1]);
                var r=new Rabbit{Habitat=h,Distances=d,At=d[d.Length-1]*.5f,Yaw=h.Phase*57.29578f,Visits=i,Age=h.Phase};
                r.Wait=RestDuration(r)*.7f;
                r.Position=Point(r,r.At);r.Hidden=!navigation.Clear(r.Position,r.Position);
                if(r.Hidden){FindVisible(r);if(r.Hidden)r.Wait=.7f;}
                r.Previous=r.Position;r.PreviousYaw=r.Yaw;r.Visible=r.Hidden?0:1;r.PreviousVisible=r.Visible;
                rabbits[i]=r;
            }
            Render();
        }
        static Vector3 Point(Rabbit r,float distance)
        {
            var d=r.Distances;for(int i=1;i<d.Length;i++)if(distance<=d[i])return Vector3.Lerp(r.Habitat.Track[i-1],r.Habitat.Track[i],Mathf.InverseLerp(d[i-1],d[i],distance));
            return r.Habitat.Track[d.Length-1];
        }
        void FindVisible(Rabbit r)
        {
            for(int i=0;i<r.Distances.Length;i++)if(navigation.Clear(r.Habitat.Track[i],r.Habitat.Track[i]))
            {r.At=r.Distances[i];r.Position=Point(r,r.At);r.Hidden=false;return;}
        }
        static float Hop(Rabbit r)=>r.Hopping?Mathf.Clamp01(r.HopAge/r.HopDuration):-1;
        static float Variation(Rabbit r,float salt)=>.5f+.5f*Mathf.Sin(r.Visits*2.399963f+r.Habitat.Phase*1.37f+salt);
        static float RestDuration(Rabbit r)=>r.Habitat.WaitSeconds*Mathf.Lerp(.7f,1.6f,Variation(r,1.3f));
        // Unequal, short ear flicks, then a quiet interval; no independent shader clock.
        static float Ear(float age,float phase,float period)
        {float t=Mathf.Repeat(age+phase,period);return t<.46f?Mathf.Sin(t/.46f*Mathf.PI)*.04f:0;}
        public void Tick(float seconds,float light,bool rain,Vector3? player)
        {
            if(seconds<=0)return;
            navigation.SetPlayer(player.HasValue?(Vector3?)space.InverseTransformPoint(player.Value):null);
            var p=player.HasValue?(Vector3?)space.InverseTransformPoint(player.Value):null;
            accumulator+=seconds;int steps=0;
            while(accumulator>=Step&&steps++<256)
            {
                accumulator-=Step;
                foreach(var r in rabbits)
                {
                    r.Previous=r.Position;r.PreviousYaw=r.Yaw;r.PreviousHop=Hop(r);r.PreviousForage=r.Forage;r.PreviousLook=r.Look;r.PreviousAlert=r.Alert;r.PreviousVisible=r.Visible;r.PreviousEars=r.Ears;
                    Advance(r,p,light,rain);
                }
            }
            Render();
        }
        void Advance(Rabbit r,Vector3? player,float light,bool rain)
        {
            r.Age+=Step;
            if(r.Hidden)
            {
                r.Wait-=Step;if(r.Wait<=0){FindVisible(r);r.Previous=r.Position;r.Wait=.7f;}
                r.Visible=Mathf.MoveTowards(r.Visible,r.Hidden?0:1,Step*2);return;
            }
            r.Visible=Mathf.MoveTowards(r.Visible,1,Step*2);
            float distance=player.HasValue?Vector2.Distance(new Vector2(r.Position.x,r.Position.z),new Vector2(player.Value.x,player.Value.z)):float.PositiveInfinity;
            bool nearby=player.HasValue&&Mathf.Abs(player.Value.y-r.Position.y)<2&&distance<r.Habitat.StartleDistance+(r.Threat?1:0);
            r.Threat=nearby;r.Fear=nearby?3.5f:Mathf.Max(0,r.Fear-Step);
            bool calm=!r.Hopping&&!r.Turning&&r.Fear==0;
            // Some pauses are for grazing, others for watching or simply resting.
            float graze=calm&&!rain&&Variation(r,2.1f)>.30f&&r.RestAge>.25f?.45f+.23f*Mathf.Sin(r.RestAge*3.6f+r.Habitat.Phase):0;
            r.Forage=Mathf.MoveTowards(r.Forage,graze,Step*1.8f);
            r.Look=Mathf.MoveTowards(r.Look,calm&&r.Forage<.1f?.12f*Mathf.Sin(r.RestAge*.85f+r.Habitat.Phase):0,Step*.3f);
            r.Alert=Mathf.MoveTowards(r.Alert,r.Fear>0?1:.15f+.1f*Mathf.Sin(r.Age*.9f),Step*2);
            var earTarget=!r.Hopping?new Vector2(Ear(r.Age,r.Habitat.Phase,3.7f),Ear(r.Age,r.Habitat.Phase+1.1f,5.3f)):Vector2.zero;
            r.Ears=Vector2.MoveTowards(r.Ears,earTarget,Step*.35f);
            if(r.Hopping)
            {
                r.HopAge=Mathf.Min(r.HopDuration,r.HopAge+Step);float u=r.HopAge/r.HopDuration;
                float at=Mathf.Lerp(r.From,r.To,u*u*(3-2*u));var next=Point(r,at);
                if(!r.Blocked&&!navigation.Clear(r.Position,next))r.Blocked=true;
                if(!r.Blocked){r.At=at;r.Position=next;}
                if(u>=1){r.Hopping=false;r.Turning=false;r.Visits++;r.Wait=r.Blocked?.7f:r.Fear>0?.15f:RestDuration(r);r.RestAge=0;}
                return;
            }
            r.RestAge+=Step;r.Wait-=Step*(.35f+.65f*Mathf.Clamp01(light))*(rain?.3f:1);
            if(r.Fear>0)r.Wait=Mathf.Min(r.Wait,.15f);
            if(r.Wait>0)return;
            r.Turning=true;
            float end=r.Distances[r.Distances.Length-1],direction;
            if(r.Fear>0&&player.HasValue)
            {
                float first=(r.Habitat.Track[0]-player.Value).sqrMagnitude,last=(r.Habitat.Track[r.Distances.Length-1]-player.Value).sqrMagnitude;
                direction=last>first?1:-1;
            }
            else
            {
                float destination=end*.5f+Mathf.Sin(r.Visits*2.399963f+r.Habitat.Phase)*Mathf.Min(.75f,end*.22f);
                direction=destination-r.At;
                if(Mathf.Abs(direction)<.20f)direction=-Mathf.Sign(direction==0?1:direction)*.45f;
            }
            float length=r.Habitat.HopDistance*(r.Fear>0?1:Mathf.Lerp(.6f,1,Variation(r,.4f)));
            // Calm wandering stays near the center; fleeing may use the entire checked strip.
            float limit=Mathf.Min(.85f,end*.25f);
            float target=Mathf.Clamp(r.At+Mathf.Sign(direction)*Mathf.Min(length,r.Fear>0?length:Mathf.Abs(direction)),0,end);
            if(r.Fear==0&&Mathf.Abs(r.At-end*.5f)<=limit)target=Mathf.Clamp(target,end*.5f-limit,end*.5f+limit);
            var goal=Point(r,target);var delta=goal-r.Position;delta.y=0;
            if(delta.sqrMagnitude<.01f||!navigation.Clear(r.Position,goal)){r.Wait=.6f;r.Turning=false;return;}
            if(r.Fear>0&&player.HasValue&&(goal-player.Value).sqrMagnitude+.01f<(r.Position-player.Value).sqrMagnitude){r.Wait=.6f;r.Turning=false;return;}
            float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            r.Yaw=Mathf.SmoothDampAngle(r.Yaw,yaw,ref r.YawRate,.16f,240,Step);
            if(Mathf.Abs(Mathf.DeltaAngle(r.Yaw,yaw))>10||r.Forage>.10f)return;
            float speed=r.Fear>0?r.Habitat.EscapeSpeed:r.Habitat.Speed*Mathf.Lerp(.82f,1,Variation(r,3.7f));
            r.From=r.At;r.To=target;r.HopAge=0;r.HopDuration=Mathf.Max(.28f,Vector3.Distance(r.Position,goal)/speed);r.Hopping=true;r.Blocked=false;
        }
        void Render()
        {
            float alpha=Mathf.Clamp01(accumulator/Step);
            for(int i=0;i<rabbits.Length;i++)
            {
                var r=rabbits[i];var p=space.TransformPoint(Vector3.Lerp(r.Previous,r.Position,alpha));float hop=Hop(r);
                // Fold the end/start boundaries through zero elevation, rather than interpolating -1.
                float a=r.PreviousHop<0?0:Mathf.Sin(r.PreviousHop*Mathf.PI),b=hop<0?0:Mathf.Sin(hop*Mathf.PI);
                poses[i]=new Vector4(p.x,p.y,p.z,Mathf.LerpAngle(r.PreviousYaw,r.Yaw,alpha)*Mathf.Deg2Rad);
                actions[i]=new Vector4(Mathf.Lerp(a,b,alpha),Mathf.Lerp(r.PreviousForage,r.Forage,alpha),Mathf.Lerp(r.PreviousLook,r.Look,alpha),Mathf.Lerp(r.PreviousAlert,r.Alert,alpha));
                visible[i]=Mathf.Lerp(r.PreviousVisible,r.Visible,alpha);
                var ear=Vector2.Lerp(r.PreviousEars,r.Ears,alpha);ears[i]=new Vector4(ear.x,ear.y,0,0);
            }
            renderer.GetPropertyBlock(block);block.SetFloat("_RabbitBound",1);block.SetVectorArray(PoseId,poses);block.SetVectorArray(ActionId,actions);block.SetVectorArray(EarsId,ears);block.SetFloatArray(VisibleId,visible);renderer.SetPropertyBlock(block);
        }
        public void Release(){navigation.Release();if(renderer!=null)renderer.SetPropertyBlock(original);}
    }
}
