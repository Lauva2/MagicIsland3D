using System;
using MagicIsland.Ocean;
class VerifyWildlifeResponse
{
    static int count;
    static void Check(bool yes,string why){count++;if(!yes)throw new Exception(why);}
    static void Step(WildlifeAlarm alarm,float time,int fps,bool nearby=false)
    {for(int i=0;i<(int)Math.Round(time*fps);i++){alarm.Advance(1f/fps,nearby,2,.4f,3);Check(alarm.Level>=0&&alarm.Level<=1&&alarm.HoldRemaining>=0,"Alarm stays bounded");}}
    static int Main()
    {
        var alarm=new WildlifeAlarm();Step(alarm,3,60);Check(alarm.Level==0,"Calm wildlife remains calm");
        alarm.Trigger(2);Step(alarm,.5f,60);Check(alarm.Level==1,"Splash raises alarm smoothly");
        float level=alarm.Level,hold=alarm.HoldRemaining;
        alarm.Advance(0,true,9,.4f,3);alarm.Advance(-1,false,9,.4f,3);alarm.Advance(float.NaN,false,9,.4f,3);
        Check(alarm.Level==level&&alarm.HoldRemaining==hold,"Pause and invalid time cannot change response or consume its hold");
        Step(alarm,15,60,true);Check(alarm.Level==1&&alarm.HoldRemaining>1.9f,"Waiting player keeps a frightened bird away without repeated takeoff cycles");
        Step(alarm,1,60);Check(alarm.Level==1,"Quiet delay prevents immediate return");Step(alarm,5,60);Check(alarm.Level==0,"Wildlife fully settles after player leaves");
        alarm.Trigger(2);Step(alarm,1,60);alarm.Trigger(2);Step(alarm,1,60);Check(alarm.Level==1,"A second splash extends the alarm");Step(alarm,6,60);Check(alarm.Level==0,"Repeated disturbances never leave a permanent alarm");
        float reference=-1;
        foreach(int fps in new[]{20,30,60,120})
        {
            var sample=new WildlifeAlarm();sample.Trigger(2);Step(sample,3,fps);
            if(reference<0)reference=sample.Level;else Check(Math.Abs(reference-sample.Level)<.0001f,"Recovery independent of frame rate");
        }
        var large=new WildlifeAlarm();large.Trigger(2);large.Advance(20,false,2,.4f,3);Check(large.Level==0&&large.HoldRemaining==0,"Large elapsed step settles safely");
        Console.WriteLine("PASS: "+count+" wildlife response assertions");return 0;
    }
}
