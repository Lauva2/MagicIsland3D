using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class BiomePolishVerification
    {
        [Serializable] public sealed class Stage { public string name;public string[] editedMeshes,newMeshes;public int vertices,sites,batches; }
        [Serializable] public sealed class Report { public Stage[] stages; }
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Biome polish verification: "+why);}
        static Vector2[] UV(Mesh m,int c){var v=new List<Vector2>();m.GetUVs(c,v);return v.ToArray();}
        static Dictionary<string,string> Hierarchy(GameObject root)
        {
            string Key(Transform t)=>t.parent==null?t.name:Key(t.parent)+"/"+t.name+":"+t.GetSiblingIndex();
            return root.GetComponentsInChildren<Transform>(true).ToDictionary(Key,t=>t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.layer+"/"+t.gameObject.activeSelf+"/"+string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().FullName)));
        }
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project only");var reports=new List<Stage>();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);var hierarchy=Hierarchy(prefab);var terrain=prefab.GetComponent<IslandTerrain>();
            var colliders=terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+EditorJsonUtility.ToJson(c)).ToArray();
            var protectedPaths=Directory.GetFiles(StarterIslandWorldSetup.Folder,"Solid trunks*.asset")
                .Concat(new[]{OceanSetup.PrefabPath,StarterIslandWorldSetup.Folder+"/Exploration paths.asset",LakeSnowTrailSetup.MeshPath,GrassDesertTrailSetup.MeshPath,
                    StarterIslandWorldSetup.Folder+"/Moonflower grove.asset",StarterIslandWorldSetup.Folder+"/Luminous ancient tree.asset",
                    IslandBiomeSetup.MaterialFolder+"/Biome Details.mat",LakeSnowTrailSetup.MaterialPath}).ToArray();
            var protectedBytes=protectedPaths.Select(File.ReadAllBytes).ToArray();
            for(int stage=0;stage<5;stage++)
            {
                prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);terrain=prefab.GetComponent<IslandTerrain>();
                var plan=BiomePolishSetup.Plan(terrain,stage);Require(plan.Length>0,"unrefined "+BiomePolishSetup.Names[stage]);
                var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();var vertices=plan.Select(e=>e.Mesh.vertices).ToArray();var normals=plan.Select(e=>e.Mesh.normals).ToArray();
                var colors=plan.Select(e=>e.Mesh.colors).ToArray();var triangles=plan.Select(e=>e.Mesh.triangles).ToArray();
                var channels=plan.Select(e=>Enumerable.Range(0,5).Select(c=>UV(e.Mesh,c)).ToArray()).ToArray();
                var originalStamp=UV(plan[0].Mesh,5);
                try
                {
                    plan[0].Mesh.SetUVs(5,Enumerable.Repeat(Vector2.one,plan[0].Mesh.vertexCount).ToArray());bool rejected=false;
                    try{BiomePolishSetup.Plan(terrain,stage);}catch(InvalidOperationException){rejected=true;}Require(rejected,"foreign UV5 guard");
                }
                finally{plan[0].Mesh.SetUVs(5,originalStamp);}
                BiomePolishSetup.Rebuild(stage);int count=0;
                for(int i=0;i<plan.Length;i++)
                {
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(paths[i]);var v=mesh.vertices;var n=mesh.normals;var c=mesh.colors;
                    Require(triangles[i].SequenceEqual(mesh.triangles),"original topology");
                    for(int channel=0;channel<5;channel++)Require(channels[i][channel].SequenceEqual(UV(mesh,channel)),"original UV channels and prior art markers");
                    if(plan[i].Ground)Require(vertices[i].SequenceEqual(v)&&normals[i].SequenceEqual(n),"physical ground geometry retained");
                    for(int j=0;j<v.Length;j++)
                    {
                        Require(new[]{v[j].x,v[j].y,v[j].z,n[j].x,n[j].y,n[j].z}.All(f=>!float.IsNaN(f)&&!float.IsInfinity(f)),"finite geometry");
                        if(c.Length>0)Require(c[j].a==colors[i][j].a,"authored alpha retained");
                        if(!plan[i].Changed[j])Require(v[j]==vertices[i][j]&&n[j]==normals[i][j]&&(c.Length==0||c[j]==colors[i][j]),"unselected trunk and scenery vertices retained");
                        else
                        {
                            count++;
                            if(stage>0)
                            {
                                if(plan[i].Ground)Require(BiomePolishSetup.Mask(stage,v[j].x,v[j].z)>0,"bounded ground colours");
                                else Require(colors[i][j].g-colors[i][j].r>.065f||stage==2&&colors[i][j].r-colors[i][j].g>.10f,"only leaf/cactus/sandstone geometry changes");
                                Require(DesertFrontSetup.Mask(vertices[i][j].x,vertices[i][j].z)==0&&SnowFrontSetup.Mask(vertices[i][j].x,vertices[i][j].z)==0
                                    &&ForestEdgeSetup.Mask(vertices[i][j].x,vertices[i][j].z)==0,"all prior refinements retained");
                            }
                        }
                    }
                    if(stage>0&&!plan[i].Ground)foreach(var component in BiomePolishSetup.Components(mesh).Where(cp=>cp.Any(j=>plan[i].Changed[j])))
                    {
                        var a=new Bounds(vertices[i][component[0]],Vector3.zero);var b=new Bounds(v[component[0]],Vector3.zero);
                        foreach(int j in component){a.Encapsulate(vertices[i][j]);b.Encapsulate(v[j]);}
                        float minor=Mathf.Min(a.size.x,a.size.z);
                        Require(b.size.x>=minor*.70f&&b.size.z>=minor*.70f&&b.size.y>=a.size.y*.85f&&b.size.y<=a.size.y*1.15f,"rounded component does not collapse: "+mesh.name+" "+a.size+" -> "+b.size);
                        var center=a.center;float radius=component.Max(j=>new Vector2(vertices[i][j].x-center.x,vertices[i][j].z-center.z).magnitude);
                        Require(component.All(j=>new Vector2(v[j].x-center.x,v[j].z-center.z).magnitude<=radius+.001f),"original horizontal envelope retained");
                    }
                    if(stage==0&&mesh.name=="Palm Fronds")
                    {
                        for(int blade=0;blade<8;blade++)for(int j=0;j<39;j++)Require(v[blade*78+j]==v[blade*78+j+39],"front/back ribbon positions coincide");
                        float radius=vertices[i].Max(p=>new Vector2(p.x,p.z).magnitude);Require(v.All(p=>new Vector2(p.x,p.z).magnitude<=radius+.001f),"old palm radius retained");
                    }
                }
                prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);terrain=prefab.GetComponent<IslandTerrain>();
                var group=terrain.World.Scenery.Find(BiomePolishSetup.Group(stage));var filters=group==null?Array.Empty<MeshFilter>():group.GetComponentsInChildren<MeshFilter>(true);
                if(stage>0)
                {
                    Require(stage==3||group!=null&&filters.Length>0,"AA dressing dependency");
                    if(group!=null)Require(group.GetComponentsInChildren<Collider>(true).Length==0&&group.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"no runtime scripts/collision");
                    foreach(var site in BiomePolishSetup.Sites(stage))Require(BiomePolishSetup.Clear(site.Point,1.05f,stage!=3),"dense full-footprint sampling");
                    foreach(var f in filters)
                    {
                        Require(f.gameObject.layer==2&&f.GetComponent<Renderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(IslandBiomeSetup.MaterialFolder+"/Biome Details.mat"),"shared material");
                        Require(f.sharedMesh.normals.All(n=>n.sqrMagnitude>.9f),"valid decoration normals");
                        foreach(var p in f.sharedMesh.vertices)Require(StarterIslandLayout.RouteDistance(p.x,p.z)>3&&!StarterIslandLayout.CanPlace(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z)),"all geometry avoids routes/buildable cells");
                    }
                }
                var current=Hierarchy(prefab);foreach(var entry in hierarchy)Require(current.TryGetValue(entry.Key,out var value)&&value==entry.Value,"original hierarchy remains");
                Require(colliders.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+EditorJsonUtility.ToJson(c))),"original collider components remain");
                var newMeshes=filters.Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).ToArray();reports.Add(new Stage{name=BiomePolishSetup.Names[stage],editedMeshes=paths,newMeshes=newMeshes,vertices=count,sites=BiomePolishSetup.Sites(stage).Length,batches=filters.Length});
                var all=paths.Concat(newMeshes).Concat(new[]{TerrainSetup.PrefabPath}).ToArray();var bytes=all.Select(File.ReadAllBytes).ToArray();BiomePolishSetup.Rebuild(stage);
                for(int i=0;i<all.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(all[i])),"repeat stage preserves result");
                Debug.Log("Ordered biome stage "+stage+" "+reports.Last().name+" verification PASSED.");
            }
            var allPaths=reports.SelectMany(s=>s.editedMeshes.Concat(s.newMeshes)).Distinct().Concat(new[]{TerrainSetup.PrefabPath}).ToArray();var allBytes=allPaths.Select(File.ReadAllBytes).ToArray();
            BiomePolishSetup.RebuildAll();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<allPaths.Length;i++)Require(allBytes[i].SequenceEqual(File.ReadAllBytes(allPaths[i])),"repeat ordered pass and normal Setup preserve art");
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(reports[1].editedMeshes.First(p=>p.Contains("/Landscape ")));var v=mesh.vertices;v[0]+=Vector3.up*.01f;mesh.vertices=v;
                var c=mesh.colors;c[0]=Color.magenta;mesh.colors=c;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
                var manual=allPaths.Select(File.ReadAllBytes).ToArray();BiomePolishSetup.RebuildAll();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<allPaths.Length;i++)Require(manual[i].SequenceEqual(File.ReadAllBytes(allPaths[i])),"later manual shape/paint preserved");
            }
            finally{for(int i=0;i<allPaths.Length;i++){File.WriteAllBytes(allPaths[i],allBytes[i]);AssetDatabase.ImportAsset(allPaths[i],ImportAssetOptions.ForceSynchronousImport);}}
            for(int i=0;i<protectedPaths.Length;i++)Require(protectedBytes[i].SequenceEqual(File.ReadAllBytes(protectedPaths[i])),"protected resource: "+protectedPaths[i]);
            Directory.CreateDirectory("Logs/BiomePolish");File.WriteAllText("Logs/BiomePolish/Bake.json",JsonUtility.ToJson(new Report{stages=reports.ToArray()},true));
            Debug.Log("Ordered biome bake verification PASSED: Beach, Grassland, Desert, Forest, Snow; original geometry/markers/collision/hand edits preserved.");
        }
        public static void VerifyRuntime(GameManager g)
        {
            for(int stage=1;stage<5;stage++)
            {
                // Unused editor UV channels may be stripped by the player build. Verify the actual baked art instead.
                string name=new[]{"","Landscape 5 1","Landscape 2 4","Landscape 10 5","Landscape 4 6"}[stage];
                var filter=g.Terrain.Terrain.World.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f=>f.name==name);
                var expected=AssetDatabase.LoadAssetAtPath<Mesh>(StarterIslandWorldSetup.Folder+"/"+name+".asset");
                Require(filter!=null&&expected!=null&&filter.sharedMesh.vertexCount==expected.vertexCount,"Packed biome model "+stage);
                Func<Mesh,Vector3[]> points=m=>m.vertices.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).ToArray();
                Require(points(filter.sharedMesh).SequenceEqual(points(expected)),"Packed model matches baked geometry "+stage);
                var actualColors=filter.sharedMesh.colors;var expectedColors=expected.colors;
                Require(actualColors.Length==expectedColors.Length,"Packed vertex colours "+stage);
                for(int channel=0;channel<3;channel++)
                    Require(Math.Abs(actualColors.Average(c=>(double)c[channel])-expectedColors.Average(c=>(double)c[channel]))<.005,"Packed colour palette "+stage);
                if(stage!=3)Require(g.Terrain.Terrain.World.Scenery.Find(BiomePolishSetup.Group(stage))!=null,"Packed dressing "+stage);
            }
            var player=g.Players.Player;var move=player.GetType().GetMethod("TickMovement",BindingFlags.NonPublic|BindingFlags.Instance);var camera=player.ViewCamera;
            var position=player.transform.position;var rotation=camera.transform.rotation;
            try
            {
                camera.transform.rotation=Quaternion.Euler(45,0,0);
                foreach(var route in new[]{new[]{new Vector2(0,240),new Vector2(-150,370),new Vector2(-100,520)},new[]{new Vector2(-390,565),new Vector2(-420,760),new Vector2(-260,910),new Vector2(-40,980)},new[]{new Vector2(350,820),new Vector2(470,720),new Vector2(480,650)}})
                {
                    Require(g.Terrain.TryResolve(new Vector3(route[0].x,0,route[0].y),out var start),"actual biome floor");g.Players.RestorePosition(start);
                    foreach(var goal in route.Skip(1))
                    {
                        int steps=0;
                        while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),goal)>.6f&&steps++<2200)
                        {var direction=goal-new Vector2(player.transform.position.x,player.transform.position.z);move.Invoke(player,new object[]{direction.normalized,.05f});}
                        Require(steps<2200,"actual character movement to "+goal);
                    }
                }
            }
            finally{g.Players.RestorePosition(position);camera.transform.rotation=rotation;}
            Debug.Log("Ordered biome runtime walking verification PASSED.");
        }
    }
}
