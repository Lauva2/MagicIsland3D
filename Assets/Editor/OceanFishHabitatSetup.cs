using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static partial class OceanBiomeSetup
    {
        static void BakeFishHabitats(OceanSurface surface,List<OceanSchool> schools,BakedFloor floor,OceanGeography geography)
        {
            var patches=surface.GetComponentsInChildren<OceanHabitatPatch>();var placed=new List<Vector2>();
            foreach(var school in schools)
            {
                var mesh=school.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
                var pivots=new List<Vector4>();mesh.GetUVs(1,pivots);var members=new OceanFishMember[school.Members];
                for(int i=0;i<members.Length;i++)members[i]=new OceanFishMember{Bottom=100,Top=-100};
                for(int v=0;v<vertices.Length;v++)
                {
                    int i=Mathf.RoundToInt((pivots[v].w-1)/2.399963f);var m=members[i];m.Pivot=pivots[v];
                    var relative=vertices[v]-(Vector3)pivots[v];
                    m.Radius=Mathf.Max(m.Radius,new Vector2(relative.x,relative.z).magnitude+.04f);
                    m.Bottom=Mathf.Min(m.Bottom,relative.y-.015f);m.Top=Mathf.Max(m.Top,relative.y+.015f);members[i]=m;
                }
                school.HomeRadius=school.Species==ShallowFishSpecies.Clownfish?2.5f:school.Species==ShallowFishSpecies.SpottedRay?2.9f:3.2f;
                bool ray=school.Species==ShallowFishSpecies.SpottedRay,reef=school.Species==ShallowFishSpecies.Butterflyfish;
                var pool=patches.Where(p=>reef?p.Biome==OceanBiome.ReefSlope:ray?p.Biome==OceanBiome.SandFlat:school.Species==ShallowFishSpecies.Clownfish?p.Corals>10:p.Biome!=OceanBiome.ReefSlope).ToArray();
                if(pool.Length==0)throw new InvalidOperationException("No habitat for "+school.Species);
                var random=new System.Random(unchecked(surface.FishSeed+6001+(int)school.Species*53+placed.Count*113));
                float maxRadius=members.Max(m=>m.Radius),bottom=members.Min(m=>m.Bottom+m.Pivot.y),top=members.Max(m=>m.Top+m.Pivot.y);
                bool found=false;
                for(int attempt=0;attempt<1600;attempt++)
                {
                    var patch=pool[random.Next(pool.Length)];float a=(float)Range(random,0,Math.PI*2),r=(float)Range(random,1.8,5);
                    Vector3 center=patch.transform.localPosition+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                    var horizontal=new Vector2(center.x,center.z);
                    if(placed.Any(p=>(p-horizontal).sqrMagnitude<17))continue;
                    float depth=(float)geography.Depth(center.x,center.z);
                    if(depth<.45f||ray&&depth>1.4f||reef&&depth<1.55f||school.Species==ShallowFishSpecies.SilverMinnow&&depth>1.4f)continue;
                    float highest=float.NegativeInfinity,lowest=float.PositiveInfinity,footprint=school.HomeRadius+maxRadius+.15f;
                    bool volume=true;
                    for(int ring=0;ring<5&&volume;ring++)for(int s=0;s<40;s++)
                    {
                        float phase=s*Mathf.PI/20,reach=footprint*ring/4;
                        float px=center.x+Mathf.Cos(phase)*reach,pz=center.z+Mathf.Sin(phase)*reach;
                        if(Math.Sqrt(px*px+pz*pz)-MagicIsland.Interaction.IslandLayout.CoastRadius(Math.Atan2(pz,px))<.8){volume=false;break;}
                        float h=floor.Height(px,pz);
                        highest=Mathf.Max(highest,h);lowest=Mathf.Min(lowest,h);
                    }
                    float low=highest-bottom+.10f,high=surface.SeaLevel-top-.13f;
                    if(!volume||low>=high||ray&&highest-lowest>.34f)continue;
                    center.y=Mathf.Lerp(low,high,ray?.06f:.52f);
                    var obstacles=surface.SwimObstacles.Where(o=>(o.Center-horizontal).magnitude<school.HomeRadius+maxRadius+o.Radius+.8f&&o.Top>=center.y+bottom-.04f&&o.Bottom<=center.y+top+.04f).ToArray();
                    if((school.Species==ShallowFishSpecies.Clownfish||reef)&&!obstacles.Any(o=>o.Coral&&(o.Center-horizontal).magnitude<school.HomeRadius+o.Radius))continue;
                    var navigation=new OceanSwimNavigation(obstacles,center,school.HomeRadius);bool clear=true;
                    for(int i=0;i<members.Length;i++)
                    {
                        var m=members[i];OceanFishMotion.Pose(m.Pivot,new Vector4(school.Radius,school.OrbitAspect,school.Phase,school.Speed),0,0,out var offset,out _);
                        var preferred=center+offset;
                        if(!navigation.FindClear(preferred,m,m.Pivot.w,out m.Spawn)){clear=false;break;}
                        members[i]=m;
                    }
                    if(!clear)continue;
                    school.Center=center;school.Swimmers=members;school.Obstacles=obstacles;school.Habitat=ray?OceanBiome.SandFlat:reef?OceanBiome.ReefSlope:patch.Biome;
                    // A point can be clear for each fish separately yet too crowded for the whole
                    // group's pitched bodies. Reject this candidate and keep sampling the same plan.
                    try{surface.ConfigureFishHabitat(school);school.ResetSteering();school.Render(0);}
                    catch(InvalidOperationException){continue;}
                    placed.Add(horizontal);found=true;break;
                }
                if(!found)throw new InvalidOperationException("No safe habitat for "+school.name+" ("+school.Species+")");
                float extent=school.HomeRadius+school.Radius+maxRadius+.15f;
                mesh.bounds=new Bounds(Vector3.zero,new Vector3(extent*2,2*(Mathf.Max(Mathf.Abs(bottom),Mathf.Abs(top))+.06f),extent*2));EditorUtility.SetDirty(mesh);
                // Explicit rebakes write the resolved, separated spawn poses; old AA data is only repaired in memory.
                for(int i=0;i<school.Swimmers.Length;i++)
                {
                    var member=school.Swimmers[i];member.Spawn=school.Steering.States[i].Position;school.Swimmers[i]=member;
                }
            }
        }
    }
}
