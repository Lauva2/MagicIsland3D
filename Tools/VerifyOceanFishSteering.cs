using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using MagicIsland.Ocean;
using UnityEngine;

// Managed fallback for the motion rules, not a replacement for Unity / packed-AA rendering checks.
class VerifyOceanFishSteering
{
    static int checks;
    static void Check(bool yes, string message)
    { checks++; if (!yes) throw new Exception(message); }
    static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);
    static string Field(string block, string name)
    {
        var match = Regex.Match(block, @"(?m)^\s*(?:- )?" + name + @": ([^\r\n]+)");
        if (!match.Success) throw new Exception("Missing baked field: " + name);
        return match.Groups[1].Value;
    }
    static Vector4 Vector(string text)
    {
        var numbers = Regex.Matches(text, @"[xyzw]: ([^,}]+)").Cast<Match>().Select(m => Number(m.Groups[1].Value)).ToArray();
        return new Vector4(numbers[0], numbers[1], numbers.Length > 2 ? numbers[2] : 0, numbers.Length > 3 ? numbers[3] : 0);
    }
    internal static OceanSchool School()
    {
        // Skip the native MonoBehaviour constructor; only ordinary managed fields / methods are used.
        var school = (OceanSchool)FormatterServices.GetUninitializedObject(typeof(OceanSchool));
        typeof(OceanSchool).GetField("alarm", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(school, new WildlifeAlarm());
        school.Radius = 1; school.Speed = .24f; school.OrbitAspect = .55f; school.HomeRadius = 3.2f;
        school.NeighbourRadius = 1.8f; school.SeparationRadius = .7f;
        school.SeparationWeight = 1.8f; school.AlignmentWeight = .85f; school.CohesionWeight = .65f;
        school.CruiseWeight = .55f; school.Acceleration = 1.5f;
        school.VerticalRange=.22f;school.VerticalSpeed=.12f;school.MaximumPitch=18;
        school.ConfigureWater(float.NegativeInfinity,float.PositiveInfinity,null);
        school.ReactionDistance = 7; school.FleeDistance = .85f; school.ScatterDistance = .38f;
        school.QuietSeconds = 1.8f; school.RecoverySeconds = 3.2f;
        return school;
    }
    static IEnumerable<OceanSchool> BakedSchools(string root)
    {
        string guid = Regex.Match(File.ReadAllText(Path.Combine(root, "Assets/Scripts/Ocean/OceanSchool.cs.meta")), @"guid: (\w+)").Groups[1].Value;
        string prefab = File.ReadAllText(Path.Combine(root, "Assets/Art/Prefabs/Environment/IslandOcean.prefab"));
        foreach (string block in Regex.Split(prefab, @"(?m)^--- "))
        {
            if (!block.Contains("guid: " + guid)) continue;
            var school = School();
            foreach (var field in typeof(OceanSchool).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var match = Regex.Match(block, @"(?m)^  " + field.Name + @": ([^\r\n]+)");
                if (!match.Success) continue;
                string value = match.Groups[1].Value;
                if (field.FieldType == typeof(float)) field.SetValue(school, Number(value));
                else if (field.FieldType == typeof(int)) field.SetValue(school, int.Parse(value));
                else if (field.FieldType.IsEnum) field.SetValue(school, Enum.ToObject(field.FieldType, int.Parse(value)));
                else if (field.FieldType == typeof(Vector3)) field.SetValue(school, (Vector3)Vector(value));
            }
            string swimmers = Regex.Match(block, @"(?ms)^  Swimmers:\r?\n(.*?)^  Obstacles:").Groups[1].Value;
            school.Swimmers = Regex.Split(swimmers, @"(?m)^  - Pivot: ").Skip(1).Select(entry => {
                string data = "    Pivot: " + entry;
                return new OceanFishMember { Pivot = Vector(Field(data, "Pivot")), Radius = Number(Field(data, "Radius")),
                    Bottom = Number(Field(data, "Bottom")), Top = Number(Field(data, "Top")), Spawn = Vector(Field(data, "Spawn")) };
            }).ToArray();
            string obstacles = block.Substring(block.IndexOf("  Obstacles:", StringComparison.Ordinal));
            school.Obstacles = Regex.Split(obstacles, @"(?m)^  - Center: ").Skip(1).Select(entry => {
                string data = "    Center: " + entry; var center = Vector(Field(data, "Center"));
                return new OceanSwimObstacle { Center = new Vector2(center.x, center.y), Radius = Number(Field(data, "Radius")),
                    Bottom = Number(Field(data, "Bottom")), Top = Number(Field(data, "Top")), Coral = Field(data, "Coral") == "1" };
            }).ToArray();
            yield return school;
        }
    }
    static int Main(string[] args)
    {
        string root=args.Length==0?".":args[0];
        int frames=args.Length>1?int.Parse(args[1],CultureInfo.InvariantCulture)*30:1440;
        Check(frames>=1440,"Simulate at least 48 seconds per school");
        var schools = BakedSchools(root).ToArray();
        var floor=ReadFloor(root);
        string prefab=File.ReadAllText(Path.Combine(root,"Assets/Art/Prefabs/Environment/IslandOcean.prefab"));
        string volumes=Regex.Match(prefab,@"(?ms)^  SwimObstacles:\r?\n(.*?)^  Effects:").Groups[1].Value;
        var obstacles=Regex.Split(volumes,@"(?m)^  - Center: ").Skip(1).Select(entry=>{
            string data="    Center: "+entry;var center=Vector(Field(data,"Center"));
            return new OceanSwimObstacle{Center=new Vector2(center.x,center.y),Radius=Number(Field(data,"Radius")),Bottom=Number(Field(data,"Bottom")),Top=Number(Field(data,"Top")),Coral=Field(data,"Coral")=="1"};
        }).ToArray();
        float sea=Number(Field(prefab,"SeaLevel"));
        float waves=Number(Field(File.ReadAllText(Path.Combine(root,"Assets/Art/Materials/Ocean/Water.mat")),"_WaveHeight"));
        Check(obstacles.Length>1500,"Read full-height reef obstacles");
        string ranges=Regex.Match(prefab,@"(?ms)^  FishPopulations:\r?\n(.*?)(?=^  \w+:|\z)").Groups[1].Value;
        var populations=Regex.Split(ranges,@"(?m)^  - Species: ").Skip(1).Select(entry=>{
            string data="    Species: "+entry;
            return new OceanFishPopulation((ShallowFishSpecies)int.Parse(Field(data,"Species")),int.Parse(Field(data,"Minimum")),int.Parse(Field(data,"Maximum")),int.Parse(Field(data,"MinimumSchools")),int.Parse(Field(data,"MaximumSchools")));
        }).ToArray();
        var plan=OceanFishPopulation.CreatePlan(populations,int.Parse(Field(prefab,"FishSeed")));
        Check(schools.Length==plan.Length,"Read every actual school from seeded count plan");
        foreach(var population in populations)Check(plan.Where(p=>p.Species==population.Species).Select(p=>p.Members).OrderBy(n=>n).SequenceEqual(schools.Where(s=>s.Species==population.Species).Select(s=>s.Members).OrderBy(n=>n)),"Baked members match inclusive seeded ranges");
        int avoidance = 0, feeding = 0, exploring = 0, slow = 0, longestStop=0, passages=0;
        foreach (var school in schools)
        {
            school.ConfigureWater(floor.HighestFloor(school.Center,school.HomeRadius)+.07f,sea-Mathf.Max(.1f,waves*1.5f+.025f),obstacles);
            Check(school.Swimmers.Length == school.Members, "Baked population matches swimmer data");
            var simulation = new OceanFishSteering(school);
            var stationary=new int[school.Members];
            var low=simulation.States.Select(s=>s.Position.y).ToArray();var high=(float[])low.Clone();
            for (int frame = 0; frame < frames; frame++)
            {
                if (frame == frames-240) school.Startle(school.Center + Vector3.right);
                var before = simulation.States.Select(s => s.Position).ToArray();
                school.React(1f / 30, null); simulation.Advance(1f / 30);
                for (int i = 0; i < school.Members; i++)
                {
                    var state = simulation.States[i]; var member = simulation.BodyBounds(i);
                    Check(simulation.Navigation.CanMove(before[i], state.Position, member), "Swept movement stays inside habitat and outside coral");
                    Check(state.Position.y+member.Bottom>=school.WaterFloor-.00001f && state.Position.y+member.Top<=school.WaterCeiling+.00001f, "Three dimensional movement stays in the baked water column");
                    Check(!float.IsNaN(state.Yaw) && !float.IsInfinity(state.Velocity.sqrMagnitude), "Motion remains finite");
                    var prior=simulation.PreviousStepState(i).Passage;
                    if(prior.Active&&state.Passage.Active)Check(prior.Side==state.Passage.Side,"Retain passage side until it ends");
                    stationary[i]=state.Velocity.magnitude<.005f?stationary[i]+1:0;
                    longestStop=Math.Max(longestStop,stationary[i]);
                    Check(stationary[i]<90,"A fish must not remain motionless for three seconds: "+school.Species+" member="+i);
                    low[i]=Mathf.Min(low[i],state.Position.y);high[i]=Mathf.Max(high[i],state.Position.y);
                    Check(Mathf.Abs(Mathf.DeltaAngle(simulation.RenderState(i).Yaw*Mathf.Rad2Deg,state.Yaw*Mathf.Rad2Deg))<=135*OceanFishSteering.StepSeconds+.001f,"Render interpolation limits heading changes");
                    for(int j=0;j<i;j++)Check(Vector3.Distance(state.Position,simulation.States[j].Position)>=member.Radius+simulation.BodyBounds(j).Radius+.024f,"Actual body envelopes do not overlap");
                    if (frame == frames-240) Check(state.Activity == OceanFishActivity.Fleeing, "Splash interrupts normal activities");
                    if (state.FeedBlend > .5f && state.Velocity.magnitude < .12f) slow++;
                }
            }
            for(int i=0;i<school.Members;i++)Check(high[i]-low[i]>.01f,"Each actual fish changes depth: "+school.Species+" member="+i);
            Check(school.Alarm == 0 && simulation.States.All(s => s.Activity != OceanFishActivity.Fleeing), "Fish settle after a splash");
            avoidance += simulation.AvoidanceCount; feeding += simulation.ForagingCount; exploring += simulation.ExplorationCount;
            passages+=simulation.PassageCount;
            var frozen = simulation.States.Select(s => s.Position).ToArray();
            foreach (float seconds in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) simulation.Advance(seconds);
            Check(frozen.SequenceEqual(simulation.States.Select(s => s.Position)), "Pause / invalid time does not mutate positions");
            var reference = new OceanFishSteering(school);
            for (int frame = 0; frame < 600; frame++) reference.Advance(1f / 30);
            foreach (int fps in new[] { 15, 60, 120 })
            {
                var other = new OceanFishSteering(school);
                for (int frame = 0; frame < 20 * fps; frame++) other.Advance(1f / fps);
                for (int i = 0; i < school.Members; i++)
                    Check(Vector3.Distance(reference.States[i].Position, other.States[i].Position) < .0001f, "15 / 30 / 60 / 120 fps preserve the same trajectories");
            }
        }
        Check(avoidance > 100 && feeding > 100 && exploring > 100 && slow > 100, "Flocking preserves reef avoidance and individual feeding / exploration");
        Check(passages>10,"Baked habitats use passage routes");

        CheckBoidControls();
        CheckDetour();
        CheckPresentation();
        CheckTurning();
        Console.WriteLine("PASS: " + checks + " actual steering / baked habitat assertions across "+schools.Length+" schools; avoidance=" + avoidance + ", slow feeding=" + slow+", longest stop="+longestStop+" steps, passages="+passages);
        return 0;
    }
    static OceanFishSteering TurningFixture()
    {
        var school=School();school.Members=1;school.HomeRadius=20;school.Radius=.1f;school.Animation=new OceanFishAnimation();
        school.AlignmentWeight=school.SeparationWeight=school.CohesionWeight=0;
        school.Swimmers=new[]{new OceanFishMember{Spawn=Vector3.zero,Radius=.1f,Bottom=-.03f,Top=.03f,Pivot=new Vector4(0,0,0,1)}};
        var result=new OceanFishSteering(school);var s=result.States[0];s.Activity=OceanFishActivity.Foraging;s.Until=100;s.Target=Vector3.right*4;s.Velocity=Vector3.forward*.3f;result.States[0]=s;return result;
    }
    static void CheckTurning()
    {
        var settings=new OceanFishAnimation();settings.TurnResponseSeconds=0;Check(!settings.IsValid,"Zero turn response rejected");settings.TurnResponseSeconds=.35f;
        settings.TurnAcceleration=float.NaN;Check(!settings.IsValid,"Nonfinite angular acceleration rejected");settings.TurnAcceleration=540;
        settings.GoalBlendSeconds=float.PositiveInfinity;Check(!settings.IsValid,"Nonfinite target blend rejected");
        var simulation=TurningFixture();float peak=0;
        for(int frame=0;frame<300;frame++)
        {
            if(frame>=120){var changed=simulation.States[0];changed.Target=new Vector3(-4,0,(frame%2==0?1:-1)*.03f);simulation.States[0]=changed;}
            var before=simulation.States[0];simulation.Advance(1f/30);var after=simulation.States[0];peak=Mathf.Max(peak,Mathf.Abs(after.YawRate));
            Check(Mathf.Abs(after.YawRate-before.YawRate)<=540/30f+.0001f,"Retargeting has bounded angular acceleration, including heading reversal");
            Check(Mathf.Abs(after.YawRate)<=135.001f,"Turns retain the existing maximum heading rate");
            Check(Vector3.Distance(before.Position,after.Position)>.00001f,"An unobstructed feeding fish continues swimming through a turn");
            if(frame==0)Check(Mathf.Abs(after.Yaw-before.Yaw)*Mathf.Rad2Deg<1,"Turn starts gradually instead of instantly using full angular speed");
        }
        Check(peak>60,"Easing still permits a deliberate turn rather than freezing heading");
        var paused=simulation.States[0];simulation.Advance(0);Check(simulation.States[0].YawRate==paused.YawRate&&simulation.States[0].SteeringGoal==paused.SteeringGoal,"Pause freezes turn momentum and target blending");
        var left=TurningFixture();var right=TurningFixture();
        var a=left.States[0];a.Position=new Vector3(0,0,.799f);a.Target=a.SteeringGoal=Vector3.zero;left.States[0]=a;
        var b=right.States[0];b.Position=new Vector3(0,0,.801f);b.Target=b.SteeringGoal=Vector3.zero;right.States[0]=b;
        left.Advance(1f/30);right.Advance(1f/30);
        Check(Vector3.Distance(left.States[0].SteeringGoal,right.States[0].SteeringGoal)<.005f,"Crossing the former arrival threshold no longer jumps the feeding circuit target");
        var school=School();school.Members=1;school.HomeRadius=4;school.Animation=new OceanFishAnimation();
        school.Swimmers=new[]{new OceanFishMember{Spawn=Vector3.zero,Radius=.1f,Bottom=-.03f,Top=.03f,Pivot=new Vector4(0,0,0,1)}};
        school.ConfigureWater(-.4f,.4f,new[]{new OceanSwimObstacle{Center=new Vector2(0,.33f),Radius=.1f,Bottom=-1,Top=1}});
        var recovery=new OceanFishSteering(school);var blocked=recovery.States[0];blocked.Position=new Vector3(0,0,.085f);blocked.Velocity=Vector3.forward*.5f;
        blocked.RecoveryPitch=1.4f;blocked.RecoveryUntil=100;blocked.Until=100;recovery.States[0]=blocked;
        for(int i=0;i<90;i++)
        {
            var before=recovery.States[0];recovery.Advance(1f/30);var after=recovery.States[0];
            Check(Mathf.Abs(after.Pitch)<=school.MaximumPitch*Mathf.Deg2Rad+.00001f,"A blocked/hovering recovery cannot pitch beyond its reserved body volume");
            Check(recovery.Navigation.CanMove(before.Position,after.Position,recovery.BodyBounds(0)),"Clamped recovery preserves the original full-body swept navigation");
        }
    }
    static byte[] Hex(string text)
    {
        var bytes=new byte[text.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(text.Substring(i*2,2),16);return bytes;
    }
    static OceanFishWaterVolume ReadFloor(string root)
    {
        string text=File.ReadAllText(Path.Combine(root,"Assets/Art/Meshes/Ocean/Island Shore.asset"));
        byte[] data=Hex(Field(text,"_typelessdata")),indices=Hex(Field(text,"m_IndexBuffer"));
        int count=int.Parse(Field(text,"m_VertexCount")),stride=data.Length/count;
        var vertices=new Vector3[count];var triangles=new int[indices.Length/2];
        for(int i=0;i<count;i++)vertices[i]=new Vector3(BitConverter.ToSingle(data,i*stride),BitConverter.ToSingle(data,i*stride+4),BitConverter.ToSingle(data,i*stride+8));
        for(int i=0;i<triangles.Length;i++)triangles[i]=BitConverter.ToUInt16(indices,i*2);
        return new OceanFishWaterVolume(vertices,triangles);
    }
    static OceanFishSteering Controlled(OceanSchool school, float separation, float alignment, float cohesion)
    {
        school.SeparationWeight = separation; school.AlignmentWeight = alignment; school.CohesionWeight = cohesion;
        var simulation = new OceanFishSteering(school);
        for (int i = 0; i < simulation.States.Length; i++)
        {
            var state = simulation.States[i]; state.Until = 100;
            state.Velocity = Vector3.forward * .5f; state.Yaw = 0; simulation.States[i] = state;
        }
        return simulation;
    }
    static void CheckBoidControls()
    {
        var school = School(); school.HomeRadius = 10; school.Members = 2; school.NeighbourRadius = 4; school.CruiseWeight = 0;
        school.Swimmers = new[] {
            new OceanFishMember { Spawn = new Vector3(-.2f,0,0), Radius = .1f, Pivot = new Vector4(0,0,0,1) },
            new OceanFishMember { Spawn = new Vector3(.2f,0,0), Radius = .1f, Pivot = new Vector4(0,0,0,3.399963f) }
        };
        var simulation = Controlled(school, 2, 0, 0); simulation.Advance(1);
        Check(simulation.States[0].Position.x < -.3f && simulation.States[1].Position.x > .3f, "Separation actually spreads close swimmers");
        school.Swimmers[0].Spawn = new Vector3(-1,0,0); school.Swimmers[1].Spawn = new Vector3(1,0,0);
        simulation = Controlled(school, 0, 0, 1); simulation.Advance(1);
        Check(simulation.States[0].Position.x > -.9f && simulation.States[1].Position.x < .9f, "Cohesion actually regroups loose swimmers");
        simulation = Controlled(school, 0, 1, 0);
        float yaw = 3 * Mathf.Deg2Rad;
        var velocity = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * .5f;
        var state = simulation.States[1]; state.Yaw = yaw; state.Velocity = velocity; simulation.States[1] = state;
        simulation.Advance(OceanFishSteering.StepSeconds);
        Check(simulation.States[0].Yaw>0&&simulation.States[0].Yaw<yaw&&simulation.States[1].Yaw>0&&simulation.States[1].Yaw<yaw
            &&Mathf.Abs(simulation.States[0].Yaw+simulation.States[1].Yaw-yaw)<.00001f, "Both swimmers ease symmetrically from previous-step headings, including the later member");
        Vector3 first = simulation.States[0].Velocity.normalized, second = simulation.States[1].Velocity.normalized;
        simulation = Controlled(school, 0, 1, 0);
        state = simulation.States[0]; state.Yaw = yaw; state.Velocity = velocity; simulation.States[0] = state;
        simulation.Advance(OceanFishSteering.StepSeconds);
        Check(Vector3.Distance(first, simulation.States[1].Velocity.normalized) < .00001f && Vector3.Distance(second, simulation.States[0].Velocity.normalized) < .00001f, "Reversing member order preserves the same headings");
        simulation = Controlled(school, 0, 0, 0); simulation.Advance(OceanFishSteering.StepSeconds);
        Check(Mathf.Abs(simulation.States[0].Velocity.x) < .00001f, "Disabling social weights removes their effect");
        float before = simulation.States[0].Velocity.magnitude; simulation.Advance(OceanFishSteering.StepSeconds);
        Check(Mathf.Abs(simulation.States[0].Velocity.magnitude - before) <= school.Acceleration * OceanFishSteering.StepSeconds + .00001f, "Speed changes respect configured acceleration");
    }
    static void CheckDetour()
    {
        var school = School(); school.Members = 1; school.HomeRadius = 4; school.Radius = 1.7f; school.Speed = .4f;
        school.Obstacles = new[] { new OceanSwimObstacle { Center = Vector2.zero, Radius = .7f, Bottom = -1, Top = 1 } };
        school.Swimmers = new[] { new OceanFishMember { Pivot = new Vector4(0, 0, 0, 1), Radius = .15f, Bottom = -.05f, Top = .05f, Spawn = new Vector3(-1.8f, 0, 0) } };
        var simulation = new OceanFishSteering(school); var state = simulation.States[0];
        state.Activity = OceanFishActivity.Exploring; state.Target = new Vector3(1.8f, 0, 0); state.Until = 100; state.Yaw = Mathf.PI / 2; simulation.States[0] = state;
        float sideways = 0, furthestX = -1.8f;
        for (int i = 0; i < 900; i++)
        {
            var before = simulation.States[0].Position; simulation.Advance(1f / 30); var after = simulation.States[0].Position;
            Check(simulation.Navigation.CanMove(before, after, school.Swimmers[0]), "Detour never tunnels through the obstacle");
            sideways = Mathf.Max(sideways, Mathf.Abs(after.z)); furthestX=Mathf.Max(furthestX,after.x);
        }
        Check(sideways > .8f && furthestX > 1, "Fish visibly swims around a blocking reef to reach its target: side="+sideways+" final="+simulation.States[0].Position);
    }
    static void CheckPresentation()
    {
        var school=School();school.HomeRadius=10;school.Members=1;
        school.Swimmers=new[]{new OceanFishMember{Radius=.2f,Bottom=-.05f,Top=.05f,Pivot=new Vector4(0,0,0,1)}};
        school.ConfigureWater(-1,1,Array.Empty<OceanSwimObstacle>());
        var simulation=new OceanFishSteering(school);simulation.Advance(1);
        var previous=simulation.RenderState(0);float moved=0,pitched=0;
        for(int frame=0;frame<240;frame++)
        {
            simulation.Advance(1f/120);var current=simulation.RenderState(0);
            float distance=Vector3.Distance(previous.Position,current.Position);moved+=distance;
            Check(distance>.000001f&&distance<.01f,"120 fps rendering continuously interpolates the 30 Hz states");
            Check(Mathf.Abs(Mathf.DeltaAngle(previous.Yaw*Mathf.Rad2Deg,current.Yaw*Mathf.Rad2Deg))<1.14f,"Rendered yaw stays smooth across a fixed-step boundary");
            pitched=Mathf.Max(pitched,Mathf.Abs(current.Pitch));previous=current;
        }
        Check(moved>.2f&&pitched>.01f,"Depth changes produce visible pitch and forward motion");
        var paused=simulation.RenderState(0);simulation.Advance(0);
        Check(simulation.RenderState(0).Position==paused.Position&&simulation.RenderState(0).Pitch==paused.Pitch,"Pause also freezes the interpolated pose");
        var hitch=new OceanFishSteering(school);var stepped=new OceanFishSteering(school);
        hitch.Advance(2);for(int step=0;step<60;step++)stepped.Advance(1f/30);
        Check(Vector3.Distance(hitch.States[0].Position,stepped.States[0].Position)<.00001f,"A two-second frame hitch executes the same swept steps");
        var navigation=new OceanSwimNavigation(new[]{new OceanSwimObstacle{Center=Vector2.zero,Radius=.1f,Bottom=-.1f,Top=.1f}},Vector3.zero,10);
        Check(!navigation.CanMove(Vector3.down,Vector3.up,school.Swimmers[0]),"Vertical sweeps cannot tunnel through a reef between their endpoints");
        school.Members=2;school.Swimmers=new[]{school.Swimmers[0],school.Swimmers[0]};
        simulation=new OceanFishSteering(school);
        Check(Vector3.Distance(simulation.States[0].Position,simulation.States[1].Position)>.5f,"Coincident old spawns are spread before rendering");
    }
}
