using System;
using MagicIsland.Interaction;
class VerifyIslandTerrain
{
    static int count;
    static void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
    static int Main()
    {
        for(int x=-12;x<=12;x++)for(int z=-12;z<=12;z++)Check(IslandLayout.ContainsLand(x,z,.36),"Every legacy farm position remains supported");
        for(int i=0;i<720;i++)
        {
            double angle=i*Math.PI*2/720;
            double oldRadius=Math.Max(12/Math.Max(Math.Abs(Math.Cos(angle)),Math.Abs(Math.Sin(angle)))+1.8,17.3+.65*Math.Sin(angle*3+.7)+.4*Math.Sin(angle*7));
            double r=oldRadius-.45-.37;
            Check(IslandLayout.ContainsLand(Math.Cos(angle)*r,Math.Sin(angle)*r,.36),"Rounded coast retains previously walkable coast and save positions");
        }
        Check(IslandLayout.ContainsLand(-15,0,.36)&&IslandLayout.Height(-15,0)<=0,"Walkable beach extends beyond original square");
        Check(!IslandLayout.ContainsLand(-30,0)&&!IslandLayout.Contains(0,30),"Open sea is outside walkable footprint");
        Check(IslandLayout.ContainsPier(17,-8.2,.36)&&!IslandLayout.ContainsPier(19,-8.2,.36),"Dock has a bounded supported deck");
        Check(HousingState.ValidPosition(HousingState.Island,17,.1,-8.2)&&!HousingState.ValidPosition(HousingState.Island,25,0,0),"Coast save validation accepts deck but rejects sea");
        Check(!IslandLayout.ContainsLand(double.NaN,0)&&!IslandLayout.ContainsLand(double.PositiveInfinity,0),"Non-finite terrain coordinates rejected");
        var items=new[]{new PlaceableDefinition("fence")};var state=new PlacementState(items,5);
        Check(state.CheckLocation("fence",-14,0,0)==PlacementResult.Success,"Decoration can be placed on expanded land");
        Check(state.CheckLocation("fence",13,-8,0)==PlacementResult.RouteReserved,"Dock approach stays clear");
        Check(state.CheckLocation("fence",30,0,0)==PlacementResult.InvalidPosition,"No decoration in open water");
        var old=new NPCScheduleDefinition("npc",1.4,720,1080,new[]{new SchedulePoint(7.4,.95),new SchedulePoint(11.2,.95),new SchedulePoint(11.2,-6.8),new SchedulePoint(9.15,-6.8)});
        var current=new NPCScheduleDefinition("npc",1.4,720,1080,new[]{new SchedulePoint(7.4,.95),new SchedulePoint(11.2,.95),new SchedulePoint(11.2,-6.2),new SchedulePoint(14.15,-6.2)});
        for(int i=0;i<=100;i++)
        {
            var saved=new[]{new SavedNPCSchedule{Id="npc",Distance=old.Length*i/100}};var schedules=new NPCScheduleState(new[]{current},780);schedules.Restore(saved);
            Check(Math.Abs(schedules.Distance("npc")-saved[0].Distance)<.000001,"Legacy route progress remains loadable without resetting it");
            var p=schedules.Position("npc");Check(IslandLayout.ContainsLand(p.X,p.Z,.4),"Restored route never puts NPC into sea");
        }
        Check(current.AvoidsFarm(5),"Extended coast route avoids farm");
        Console.WriteLine("PASS: "+count+" terrain/legacy route assertions");return 0;
    }
}
