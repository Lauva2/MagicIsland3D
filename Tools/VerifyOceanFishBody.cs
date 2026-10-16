using System;
using System.Linq;
using MagicIsland.Ocean;
using UnityEngine;

class VerifyOceanFishBody
{
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid body geometry rejected");}
    public static int Main()
    {
        var settings=new OceanFishAnimation();Check(settings.IsValid,"Default animation settings");
        var samples=new[]{new Vector2(0,.25f),new Vector2(-.07f,.1f),new Vector2(.07f,.1f),new Vector2(-.09f,-.12f),new Vector2(.09f,-.12f),new Vector2(-.12f,-.45f),new Vector2(.12f,-.45f)};
        var shape=new OceanFishAnatomy(.25f,-.45f,.51f,samples);var spine=new OceanFishSpine(shape,settings,Vector3.zero,0,1,false);
        Reject(()=>new OceanFishAnatomy(float.NaN,-.45f,.51f,samples));Reject(()=>new OceanFishAnatomy(.25f,.25f,.51f,samples));Reject(()=>new OceanFishAnatomy(.25f,-.45f,.1f,samples));
        float bent=0;var center=Vector3.zero;
        for(int step=0;step<1800;step++)
        {
            float yaw=Mathf.Sin(step*.017f)*1.5f;center+=new Vector3(Mathf.Sin(yaw),0,Mathf.Cos(yaw))*.015f;
            spine.Step(center,yaw,1f/30,step%300<200?.45f:.03f);
            for(int sub=0;sub<4;sub++)
            {
                spine.Interpolate(sub/4f);Check(Vector2.Distance(spine.Node(0),new Vector2(0,.25f))<.000001f,"Head remains anchored");
                for(int j=1;j<OceanFishSpine.Nodes;j++)Check(Mathf.Abs(Vector2.Distance(spine.Node(j),spine.Node(j-1))-.175f)<.00001f,"Interpolated segment rest distances never stretch");
                foreach(var p in samples)Check(spine.Deform(p).magnitude<=.51001f,"Bent body stays in original safety envelope");
                bent=Mathf.Max(bent,Mathf.Abs(spine.Node(4).x));
            }
        }
        Check(bent>.025f,"Tail and torso bend independently of rigid root pose");
        var before=Enumerable.Range(0,5).Select(spine.Node).ToArray();spine.Interpolate(.75f);Check(before.SequenceEqual(Enumerable.Range(0,5).Select(spine.Node)),"Repeated paused render is identical");
        var school=VerifyOceanFishSteering.School();school.Animation=settings;school.Members=1;
        school.Swimmers=new[]{new OceanFishMember{Radius=.52f,Bottom=-.06f,Top=.06f,Pivot=new Vector4(0,0,0,1)}};
        school.Anatomy=new[]{shape};school.HomeRadius=10;school.SetEnvironment(1,false,0);
        var simulation=new OceanFishSteering(school);simulation.Advance(2);float day=simulation.States[0].ActivityLevel;
        school.SetEnvironment(0,false,0);simulation.Advance(15);float night=simulation.States[0].ActivityLevel;
        Check(Mathf.Abs(simulation.States[0].ExplorationLevel-settings.NightExploration)<.001f,"Night exploration rate settles");
        Check(Mathf.Abs(simulation.States[0].ShelterBlend-settings.NightShelter)<.001f,"Night shelter weight settles inside existing body bounds");
        Check(Mathf.Abs(day-1)<.001f&&Mathf.Abs(night-settings.NightSpeed)<.001f,"Night changes activity without stopping swimming");
        school.SetEnvironment(1,true,0);simulation.Advance(15);Check(Mathf.Abs(simulation.States[0].ActivityLevel-settings.RainSpeed)<.001f,"Rain activity");
        school.SetEnvironment(.7f,false,1);simulation.Advance(15);Check(simulation.States[0].ActivityLevel>day,"Evening activity increases");
        Check(simulation.States[0].ShelterBlend>=settings.TwilightShelter-.001f,"Existing dusk activity boost now approaches safe shelter");
        float activity=simulation.States[0].ActivityLevel;simulation.Advance(0);Check(simulation.States[0].ActivityLevel==activity,"Paused rhythm freezes");
        var hitch=new OceanFishSteering(school);var stepped=new OceanFishSteering(school);hitch.Advance(2);
        for(int i=0;i<60;i++)stepped.Advance(1f/30);hitch.WritePoses(Vector3.zero);stepped.WritePoses(Vector3.zero);
        for(int i=0;i<OceanFishSpine.Nodes;i++)Check(Vector4.Distance(hitch.SpinePoses[i],stepped.SpinePoses[i])<.0001f,"Frame hitch retains the same body constraint solution");
        Console.WriteLine("PASS: "+checks+" distance-constrained fish body / rhythm assertions; tail bend="+bent);return 0;
    }
}
