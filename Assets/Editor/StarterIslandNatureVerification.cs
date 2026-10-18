using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class StarterIslandNatureVerification
    {
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Starter nature: "+why);}
        public static void Run()
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);
            var ecology=root.GetComponentInChildren<WorldWaterEcology>(true);
            Check(ecology!=null&&ecology.IsValid,"baked ecology references");
            Check(ecology.Habitats.Length==10&&ecology.Habitats.Count(h=>h.Freshwater)==4,"lake and coastal habitats");
            Check(ecology.Habitats.Where(h=>h.Freshwater).All(h=>h.School.Species==ShallowFishSpecies.SilverMinnow&&h.WaterLevel==12),"freshwater fish distinct from marine species");
            Check(ecology.Habitats.Where(h=>!h.Freshwater).Select(h=>h.School.Species).Distinct().Count()==4,"four original marine models reused");
            Check(ecology.Habitats.All(h=>h.School.Members<=32&&h.School.GetComponentsInChildren<Transform>().Length==1),"combined schools without individual fish objects");
            Check(ecology.GetComponentsInChildren<Renderer>(true).All(r=>r.gameObject.layer==2&&r.sharedMaterial!=null&&AssetDatabase.Contains(r.GetComponent<MeshFilter>().sharedMesh)),"AA meshes, shared materials and Ignore Raycast");
            var tree=ecology.AncientTree;
            Check(tree.sharedMaterial.shader.name=="MagicIsland/Ancient Tree"&&tree.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3==7500,"Blender ancient tree source");
            Check(tree.GetComponent<CapsuleCollider>().radius==2.5f&&tree.GetComponent<CapsuleCollider>().height==13,"original landmark collision retained");
            var ocean=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath).GetComponent<OceanSurface>();
            Check(ocean.Schools.Length==30&&ocean.EcosystemRenderers.Length>84,"original ocean ecology retained");
            Debug.Log("Starter island nature asset verification PASSED.");
        }
        public static void VerifyRuntime(GameManager g)
        {
            var surface=g.Ocean.Surface;var e=surface.WorldEcology;
            Check(e!=null&&e.Bound&&e.IsValid,"Packed AA ecology bound before input");
            int checks=0;var starts=e.Habitats.Select(h=>h.School.Steering.States.Select(s=>s.Position).ToArray()).ToArray();
            for(int frame=0;frame<900;frame++)
            {
                surface.Tick(1f/30,1,false,g.Weather.Sun);
                foreach(var h in e.Habitats)
                {
                    var s=h.School;
                    for(int i=0;i<s.Members;i++)
                    {
                        var state=s.Steering.States[i];var body=s.Steering.BodyBounds(i);
                        Check(s.Steering.Navigation.CanMove(state.Position,state.Position,body),"whole fish stays in bounded habitat");checks++;
                        for(int j=0;j<i;j++)Check(Vector3.Distance(state.Position,s.Steering.States[j].Position)>=body.Radius+s.Steering.BodyBounds(j).Radius-.025f,"fish bodies separated");
                    }
                    if(frame%150==0)
                    {
                        var mesh=s.GetComponent<MeshFilter>().sharedMesh;var pivots=new System.Collections.Generic.List<Vector4>();var shapes=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(1,pivots);mesh.GetUVs(2,shapes);var v=mesh.vertices;
                        for(int vertex=0;vertex<v.Length;vertex+=7)
                        {
                            var p=s.DeformedVertex(v[vertex],pivots[vertex],shapes[vertex]);
                            Check(p.y>=s.WaterFloor-.002f&&p.y<=s.WaterCeiling+.002f,"animated body remains underwater and above floor");
                        }
                    }
                }
            }
            Check(e.Habitats.Select((h,index)=>h.School.Steering.States.Where((s,i)=>Vector3.Distance(s.Position,starts[index][i])>.1f).Any()).All(v=>v),"every school swims");
            var school=e.Habitats[0].School;var before=school.Steering.States.Select(s=>s.Position).ToArray();float time=surface.AnimationTime;
            for(int frame=0;frame<30;frame++)surface.Tick(0,1,false,g.Weather.Sun);
            Check(surface.AnimationTime==time&&before.SequenceEqual(school.Steering.States.Select(s=>s.Position)),"paused clock freezes lake fish");
            var target=e.transform.TransformPoint(school.Center);surface.DisturbWater(target);
            surface.Tick(.1f,1,false,g.Weather.Sun);
            Check(school.Alarm>0,"lake fish share existing disturbance response");
            Check(Mathf.Abs(surface.HeightAt(target)-12)<.04f,"lake disturbance uses elevated lake surface");
            Capture(g);
            Debug.Log("Starter island nature runtime verification PASSED: "+checks+" bounded-fish checks, body separation/deformation, shared pause, disturbance and day/night renders.");
        }
        static void Capture(GameManager g)
        {
            Directory.CreateDirectory("Logs/LakeEcology");var e=g.Ocean.Surface.WorldEcology;
            LightingFlowVerification.SetTime(g,750);
            LightingFlowVerification.Capture(g,"LakeEcology/AncientTreeDay",new Vector3(-8,22,700),23,65);
            var h=e.Habitats[0];LightingFlowVerification.Capture(g,"LakeEcology/LakeFish",h.School.Center,8,65);
            foreach(int index in new[]{4,5,6})LightingFlowVerification.Capture(g,"LakeEcology/CoastalReef"+(index-4),e.Habitats[index].School.Center,17,80);
            LightingFlowVerification.SetTime(g,1320);
            LightingFlowVerification.Capture(g,"LakeEcology/AncientTreeNight",new Vector3(-8,22,700),23,65);
            LightingFlowVerification.SetTime(g,750);
        }
    }
}
