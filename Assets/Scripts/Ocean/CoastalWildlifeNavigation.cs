using System;
using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.Ocean
{
    // Local-space transient rules. The owning OceanManager supplies the actual swept
    // scene-volume query; no scene lookup, random source or resource ownership lives here.
    public sealed class CoastalWildlifeNavigation
    {
        public Func<Vector3,Vector3,float,float,bool> SolidClear;
        public Vector3? Player {get;private set;}
        readonly Vector3[] reservations;
        readonly float[] radii;
        bool released;
        public CoastalWildlifeNavigation(int birds){reservations=new Vector3[birds];radii=new float[birds];}
        public void SetPlayer(Vector3? point){if(!released)Player=point;}
        public void Reserve(int owner,Vector3 point,float radius){if(owner>=0&&owner<radii.Length){reservations[owner]=point;radii[owner]=radius;}}
        public void Release(int owner){if(owner>=0&&owner<radii.Length)radii[owner]=0;}
        public void Clear(){released=true;SolidClear=null;Player=null;Array.Clear(radii,0,radii.Length);}
        public static Vector3 Along(Vector3 home,float distance,float insetDelta=0)
        {
            double radius=Math.Sqrt(home.x*home.x+home.z*home.z),angle=Math.Atan2(home.z,home.x);
            double inset=IslandLayout.CoastRadius(angle)-radius+insetDelta;
            angle+=distance/Math.Max(1,radius);radius=IslandLayout.CoastRadius(angle)-inset;
            var p=new Vector3((float)(Math.Cos(angle)*radius),0,(float)(Math.Sin(angle)*radius));
            p.y=(float)IslandLayout.Height(p.x,p.z)-.008f;return p;
        }
        public static bool Dry(Vector3 p,float radius)
        {
            double inset=IslandLayout.CoastRadius(Math.Atan2(p.z,p.x))-Math.Sqrt(p.x*p.x+p.z*p.z);
            return IslandLayout.ContainsLand(p.x,p.z,radius)&&inset<2.35&&!IslandLayout.PierReserved(p.x,p.z);
        }
        public bool BodyClear(Vector3 from,Vector3 to,float radius,float height)
        {
            if(released)return false;
            if(!PlayerClear(from,to,radius,height))return false;
            return SolidClear==null||SolidClear(from,to,radius,height);
        }
        bool PlayerClear(Vector3 from,Vector3 to,float radius,float height)
        {
            if(!Player.HasValue)return true;
            var player=Player.Value;
            var a=from+Vector3.up*(height*.5f)-player-Vector3.up*.85f;
            var b=to+Vector3.up*(height*.5f)-player-Vector3.up*.85f;
            var ext=new Vector3(radius+.38f,height*.5f+.85f,radius+.38f);
            bool inside=Mathf.Abs(a.x)<ext.x&&Mathf.Abs(a.y)<ext.y&&Mathf.Abs(a.z)<ext.z;
            if(inside)
            {
                // A player can walk onto a non-colliding animal: allow a continuous exit,
                // never a step farther through their body or a teleport to a clear point.
                float old=a.x*a.x+a.z*a.z,next=b.x*b.x+b.z*b.z;
                if(next>old+.0000001f&&a.x*(b.x-a.x)+a.z*(b.z-a.z)>=0)return true;
                if(a.y>=0&&b.y>a.y)return true;
            }
            float lo=0,hi=1;
            return !(Slab(a.x,b.x-a.x,ext.x,ref lo,ref hi)&&Slab(a.y,b.y-a.y,ext.y,ref lo,ref hi)&&Slab(a.z,b.z-a.z,ext.z,ref lo,ref hi));
        }
        static bool Slab(float start,float delta,float extent,ref float lo,ref float hi)
        {
            if(Mathf.Abs(delta)<.0000001f)return Mathf.Abs(start)<=extent;
            float a=(-extent-start)/delta,b=(extent-start)/delta;
            lo=Mathf.Max(lo,Mathf.Min(a,b));hi=Mathf.Min(hi,Mathf.Max(a,b));return lo<=hi;
        }
        public bool GroundClear(Vector3 from,Vector3 to,Vector3 home,float range,float radius,float height)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.2f));
            if(steps>64)return false;
            for(int i=0;i<=steps;i++)
            {
                var point=Vector3.Lerp(from,to,i/(float)steps);
                if(!Dry(point,radius)||new Vector2(point.x-home.x,point.z-home.z).magnitude>range+.05f)return false;
            }
            return BodyClear(from,to,radius,height);
        }
        public bool LandingClear(Vector3 point,int owner,float radius,float height)
        {
            if(!Dry(point,radius)||!BodyClear(point,point,radius,height))return false;
            if(Player.HasValue&&Mathf.Abs(point.y-Player.Value.y)<2&&Vector2.Distance(new Vector2(point.x,point.z),new Vector2(Player.Value.x,Player.Value.z))<radius+.7f)return false;
            for(int i=0;i<radii.Length;i++)if(i!=owner&&radii[i]>0&&Vector3.Distance(point,reservations[i])<radius+radii[i]+.2f)return false;
            return true;
        }
        public bool FindLanding(Vector3 home,int owner,float radius,float height,float search,float phase,out Vector3 point)
        {
            if(LandingClear(home,owner,radius,height)){point=home;return true;}
            float side=Mathf.Sin(phase)>=0?1:-1;
            for(int i=0;i<12;i++)
            {
                float distance=(1+i/4)*(search/3)*(i%2==0?side:-side);
                point=Along(home,distance,i%4<2?0:.4f);
                if(Vector3.Distance(point,home)<=search+.1f&&LandingClear(point,owner,radius,height))return true;
            }
            point=home;return false;
        }
    }

    public sealed class CoastalGroundRoute
    {
        readonly Vector3[] path=new Vector3[64],candidate=new Vector3[64];
        int count,index;
        float retry;
        Vector3 lastGoal;
        public int Detours {get;private set;}
        public bool Waiting {get;private set;}
        public void Reset(){count=index=0;retry=0;Waiting=false;Detours=0;}
        public Vector3 Move(CoastalWildlifeNavigation nav,Vector3 from,Vector3 goal,Vector3 home,float range,float radius,float height,float speed,float seconds)
        {
            if(seconds<=0)return from;
            retry=Mathf.Max(0,retry-seconds);
            if(index>=count||Vector3.Distance(lastGoal,goal)>.2f)
            {
                if(retry==0)
                {lastGoal=goal;count=0;index=0;Plan(nav,from,goal,home,range,radius,height);retry=.3f;}
            }
            float budget=Mathf.Max(0,speed)*seconds;Waiting=index>=count;
            for(int step=0;step<64&&index<count&&budget>0;step++)
            {
                var next=Vector3.MoveTowards(from,path[index],budget);
                next.y=(float)IslandLayout.Height(next.x,next.z)-.008f;
                if(!nav.GroundClear(from,next,home,range,radius,height)){count=index=0;retry=0;Waiting=true;break;}
                float distance=Vector3.Distance(from,next);budget-=distance;from=next;
                if(Vector3.Distance(from,path[index])<.012f)index++;else break;
            }
            if(index>=count)Waiting=Vector3.Distance(from,goal)>.025f||!nav.GroundClear(from,from,home,range,radius,height);
            return from;
        }
        void Plan(CoastalWildlifeNavigation nav,Vector3 from,Vector3 goal,Vector3 home,float range,float radius,float height)
        {
            if(Build(nav,from,goal,home,range,radius,height,0))return;
            if(Build(nav,from,goal,home,range,radius,height,.55f)||Build(nav,from,goal,home,range,radius,height,-.55f)){Detours++;return;}
            // A rock may block the entire narrow beach: select the other safe side rather
            // than force passage, leave sand, or oscillate left/right every rendered frame.
            for(int side=0;side<2;side++)
            {
                var alternative=CoastalWildlifeNavigation.Along(home,(side==0?1:-1)*Mathf.Max(.45f,range-radius-.1f));
                if(nav.Player.HasValue&&Vector3.Distance(from,nav.Player.Value)<4&&Vector3.Distance(alternative,nav.Player.Value)<Vector3.Distance(from,nav.Player.Value))continue;
                if(Build(nav,from,alternative,home,range,radius,height,0)){Detours++;return;}
            }
        }
        bool Build(CoastalWildlifeNavigation nav,Vector3 from,Vector3 goal,Vector3 home,float range,float radius,float height,float lane)
        {
            int length=0;var previous=from;
            if(lane!=0)
            {
                var radialFrom=Inset(from,lane);var radialGoal=Inset(goal,lane);
                if(!Append(nav,ref previous,radialFrom,home,range,radius,height,ref length)||!Append(nav,ref previous,radialGoal,home,range,radius,height,ref length))return false;
            }
            if(!Append(nav,ref previous,goal,home,range,radius,height,ref length))return false;
            Array.Copy(candidate,path,length);count=length;index=0;return true;
        }
        static Vector3 Inset(Vector3 point,float delta)=>CoastalWildlifeNavigation.Along(point,0,delta);
        bool Append(CoastalWildlifeNavigation nav,ref Vector3 from,Vector3 goal,Vector3 home,float range,float radius,float height,ref int length)
        {
            var start=from;int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,goal)/.22f));
            for(int i=1;i<=steps;i++)
            {
                if(length>=candidate.Length)return false;
                var next=Vector3.Lerp(start,goal,i/(float)steps);next.y=(float)IslandLayout.Height(next.x,next.z)-.008f;
                if(!nav.GroundClear(from,next,home,range,radius,height))return false;
                candidate[length++]=next;from=next;
            }
            return true;
        }
    }

    public sealed class CoastalAirRoute
    {
        Vector3 guide;
        float hold,retry;
        public bool Avoiding {get;private set;}
        public void Reset(){hold=retry=0;Avoiding=false;}
        public Vector3 Move(CoastalWildlifeNavigation nav,Vector3 from,Vector3 goal,Vector3 home,float radius,float height,float speed,float seconds,float bottom=0)
        {
            if(seconds<=0)return from;
            hold=Mathf.Max(0,hold-seconds);retry=Mathf.Max(0,retry-seconds);
            float distance=speed*seconds;
            var offsetY=Vector3.up*bottom;
            if(hold>0&&Vector3.Distance(from,guide)>.1f)
            {
                var step=Vector3.MoveTowards(from,guide,distance);
                if(nav.BodyClear(from+offsetY,step+offsetY,radius,height))return step;
                hold=0;
            }
            var direct=Vector3.MoveTowards(from,goal,distance);
            if(nav.BodyClear(from+offsetY,direct+offsetY,radius,height)){Avoiding=false;return direct;}
            Avoiding=true;if(retry>0)return from;retry=.25f;
            var outward=new Vector3(home.x,0,home.z).normalized;var tangent=new Vector3(-outward.z,0,outward.x);
            var away=nav.Player.HasValue?Vector3.ProjectOnPlane(from-nav.Player.Value,Vector3.up).normalized:outward;
            bool nearPlayer=nav.Player.HasValue&&Vector3.ProjectOnPlane(from-nav.Player.Value,Vector3.up).magnitude<radius+.6f&&from.y<nav.Player.Value.y+1.8f;
            for(int i=0;i<5;i++)
            {
                var escape=away*1.5f+Vector3.up*.35f;var rise=Vector3.up*2.2f+outward*.3f;
                var offset=i==0?(nearPlayer?escape:rise):i==1?(nearPlayer?rise:escape):i==2?tangent*1.5f+Vector3.up*.8f:i==3?-tangent*1.5f+Vector3.up*.8f:outward*1.6f+Vector3.up;
                var candidate=from+offset;
                if(candidate.y>home.y+7||Vector2.Distance(new Vector2(candidate.x,candidate.z),new Vector2(home.x,home.z))>12)continue;
                if(!nav.BodyClear(from+offsetY,candidate+offsetY,radius,height))continue;
                guide=candidate;hold=1.2f;retry=0;return Vector3.MoveTowards(from,guide,distance);
            }
            return from;
        }
    }
}
