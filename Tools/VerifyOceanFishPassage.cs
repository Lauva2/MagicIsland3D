using System;
using System.Linq;
using MagicIsland.Ocean;
using UnityEngine;

class VerifyOceanFishPassage
{
    static int checks;
    static void Check(bool yes,string message){checks++;if(!yes)throw new Exception(message);}
    static OceanSchool School()
    {
        var school=VerifyOceanFishSteering.School();school.HomeRadius=5;school.Members=4;
        school.Obstacles=new[]{new OceanSwimObstacle{Center=Vector2.zero,Radius=.65f,Bottom=-1,Top=1}};
        school.Swimmers=Enumerable.Range(0,4).Select(i=>new OceanFishMember{Radius=.12f,Bottom=-.04f,Top=.04f,
            Spawn=new Vector3(-1.8f,0,(i-1.5f)*.45f),Pivot=new Vector4(0,0,0,1+i*2.399963f)}).ToArray();
        return school;
    }
    static OceanFishSteering Simulation(OceanSchool school)
    {
        var simulation=new OceanFishSteering(school);
        for(int i=0;i<school.Members;i++)
        {
            var state=simulation.States[i];state.Activity=OceanFishActivity.Exploring;state.Until=100;
            state.Target=new Vector3(2.2f,0,0);state.Yaw=Mathf.PI/2;simulation.States[i]=state;
        }
        return simulation;
    }
    static int Main()
    {
        var school=School();var simulation=Simulation(school);var body=simulation.BodyBounds(0);
        var origin=new Vector3(-1.8f,0,0);var goal=new Vector3(2.2f,0,0);
        var left=new OceanFishPassage();var right=new OceanFishPassage();
        Check(left.TryBegin(simulation.Navigation,origin,goal,body,1,0,2,.1f),"First side is feasible");
        Check(right.TryBegin(simulation.Navigation,origin,goal,body,-1,0,2,.6f),"Other side is feasible");
        Check(left.Side==-right.Side&&left.WaitUntil!=right.WaitUntil,"Members retain independent side and stagger");
        foreach(var plan in new[]{left,right})
        {
            var route=plan;var p=origin;float time=0;
            for(int step=0;step<2000&&route.Active;step++)
            {
                route.Tick(p,time,1f/30,3,.4f);var to=Vector3.MoveTowards(p,route.Guide,.3f/30);
                Check(simulation.Navigation.CanMove(p,to,body),"Every planned arc segment is safe for full body");p=to;time+=1f/30;
            }
            Check(!route.Active&&p.x>.5f,"Each side passes to the far side");
            float blend=route.Blend;route.Tick(p,time,1f/30,3,.4f);
            Check(route.Blend<blend&&route.Blend>0,"Regrouping is gradual");
            for(int i=0;i<200;i++)route.Tick(p,time+i/30f,1f/30,3,.4f);
            Check(route.Blend==0,"Temporary guide releases completely");
        }
        var blocked=new OceanSwimNavigation(new[]{school.Obstacles[0],new OceanSwimObstacle{Center=new Vector2(0,-1.3f),Radius=.7f,Bottom=-1,Top=1}},Vector3.zero,5);
        var fallback=new OceanFishPassage();
        Check(fallback.TryBegin(blocked,origin,goal,body,1,0,2,0)&&fallback.Side==-1,"Blocked preferred side falls back to the clear side");
        var narrow=new OceanSwimNavigation(school.Obstacles,Vector3.zero,1.1f);var rejected=new OceanFishPassage();
        Check(!rejected.TryBegin(narrow,origin,goal,body,1,0,2,0),"No route escapes the home bounds");
        var above=new OceanFishPassage();Check(!above.TryBegin(simulation.Navigation,origin+Vector3.up*2,goal+Vector3.up*2,body,1,0,2,0),"Obstacles below the body do not split a school");
        left.Cancel(1);Check(!left.Active&&left.Blend==0,"Alarm cancels route immediately");
        float minZ=0,maxZ=0,splitWidth=0,regroupWidth=float.PositiveInfinity;
        var passed=new bool[4];var released=new bool[4];var first=new float[4];
        var crossed=new bool[4];bool north=false,south=false;
        for(int frame=0;frame<1800;frame++)
        {
            simulation.Advance(1f/30);
            for(int i=0;i<school.Members;i++)
            {
                var s=simulation.States[i];var before=simulation.PreviousStepState(i);
                Check(simulation.Navigation.CanMove(before.Position,s.Position,simulation.BodyBounds(i)),"Actual split movement stays clear");
                Check(Mathf.Abs(Mathf.DeltaAngle(before.Yaw*Mathf.Rad2Deg,s.Yaw*Mathf.Rad2Deg))<=4.501f,"Routing does not snap heading");
                for(int j=0;j<i;j++)Check(Vector3.Distance(s.Position,simulation.States[j].Position)>=simulation.BodyBounds(i).Radius+simulation.BodyBounds(j).Radius+.024f,"Splitting bodies remain separated");
                minZ=Mathf.Min(minZ,s.Position.z);maxZ=Mathf.Max(maxZ,s.Position.z);
                if(!crossed[i]&&before.Position.x<0&&s.Position.x>=0)
                {crossed[i]=true;north|=s.Position.z>.8f;south|=s.Position.z<-.8f;}
                if(!passed[i]&&s.Position.x>.7f){passed[i]=true;first[i]=frame/30f;}
                if(passed[i]&&!s.Passage.Active&&s.Passage.Blend==0)released[i]=true;
            }
            float width=0;
            for(int i=0;i<4;i++)for(int j=0;j<i;j++)width=Mathf.Max(width,Vector3.Distance(simulation.States[i].Position,simulation.States[j].Position));
            if(frame<150)splitWidth=Mathf.Max(splitWidth,width);
            if(frame>300&&frame<600&&simulation.States.All(s=>!s.Passage.Active))regroupWidth=Mathf.Min(regroupWidth,width);
        }
        Console.WriteLine("Passage observation: routes="+simulation.PassageCount+" z="+minZ+".."+maxZ+" passed="+string.Join(",",passed)+" release="+string.Join(",",released)+" arrival="+string.Join(",",first));
        Check(simulation.PassageCount>=4&&minZ<-.85f&&maxZ>.85f,"Actual school splits onto both sides");
        Check(crossed.All(v=>v)&&north&&south,"Members cross the obstacle plane on both sides during actual passage");
        Check(passed.All(v=>v)&&released.All(v=>v),"Every member passes and returns to ordinary behaviour");
        Check(first.Max()-first.Min()>.2f,"Members pass at different times");
        Check(regroupWidth<splitWidth*.8f,"Actual members gradually close the split after passing: "+splitWidth+" -> "+regroupWidth);
        var a=Simulation(school);var b=Simulation(school);a.Advance(12);
        for(int i=0;i<1440;i++)b.Advance(1f/120);
        Check(a.States.Select(s=>s.Passage).SequenceEqual(b.States.Select(s=>s.Passage)),"Frame hitch and 120Hz preserve route state");
        for(int i=0;i<4;i++)Check(Vector3.Distance(a.States[i].Position,b.States[i].Position)<.00001f,"Frame rate does not change split trajectories");
        var active=Simulation(school);active.Advance(1);
        Check(active.States.Any(s=>s.Passage.Active),"Pause fixture has ongoing passages");
        var frozen=active.States.Select(s=>s.Passage).ToArray();active.Advance(0);
        Check(frozen.SequenceEqual(active.States.Select(s=>s.Passage)),"Pause freezes active passage timers and blend");
        school.Startle(school.Center+Vector3.right);school.React(1f/30,null);active.Advance(1f/30);
        Check(active.States.All(s=>s.Activity==OceanFishActivity.Fleeing&&!s.Passage.Active&&s.Passage.Blend==0),"Alarm overrides an ongoing passage");
        var invalid=new OceanFishAnimation{RegroupSeconds=float.NaN};Check(!invalid.IsValid,"Non-finite passage config rejected");
        Console.WriteLine("PASS: "+checks+" obstacle split / stagger / regroup assertions");return 0;
    }
}
