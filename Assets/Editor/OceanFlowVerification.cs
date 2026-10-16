using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class OceanFlowVerification
    {
        const string Key="MagicIsland.OceanVerification";
        static OceanManager fixture;
        static OceanFlowVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            CoastalNavigationVerification.Run();
            OceanEcologyRhythmVerification.Run();
            CoastalCrabIdleVerification.Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            CoastalBirdIdleVerification.Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            OceanBiomeSetup.VerifyFishScatter();
            var paths=new[]{OceanSetup.PrefabPath,OceanSetup.MaterialFolder+"/Water.mat",OceanSetup.MeshFolder+"/Ocean Surface.asset",OceanSetup.MeshFolder+"/Island Shore.asset","Assets/Config/GameConfig.asset"};
            var bytes=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Setup preserves artist ocean assets");
            Require(!ShaderUtil.ShaderHasError(Shader.Find("MagicIsland/Island Ocean"))&&!ShaderUtil.ShaderHasError(Shader.Find("MagicIsland/Island Shore"))&&!ShaderUtil.ShaderHasError(Shader.Find("MagicIsland/Ocean Ecology")),"Shaders compile");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath).GetComponent<OceanSurface>().IsValid,"Valid ocean prefab, meshes and no colliders");
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(OceanSetup.MeshFolder+"/Island Shore.asset");
            Require(floor.vertexCount==OceanBiomeSetup.Segments*OceanBiomeSetup.Rings&&floor.bounds.size.x>=359&&floor.bounds.size.z>=359,"Irregular seabed covers expanded ocean");
            foreach(var v in floor.vertices)
            {
                double r=Math.Sqrt(v.x*v.x+v.z*v.z),edge=MagicIsland.Interaction.IslandLayout.CoastRadius(Math.Atan2(v.z,v.x));
                if(r<=edge-.46)Require(Math.Abs(v.y-(MagicIsland.Interaction.IslandLayout.Height(v.x,v.z)-.008))<.0001,"Dry coastline retains gameplay height");
                if(r>=edge+2&&r<=edge+6)Require(OceanGeography.SeaLevel-v.y>.15&&OceanGeography.SeaLevel-v.y<1.4,"Baked near-island floor retains a narrower continuous shallow shelf");
            }
            VerifyWildlife(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            CoastalWildlifeVerification.Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            OceanEcologyPolishVerification.Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            OceanFishBehaviourVerification.Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            OceanFishAcceptanceVerification.Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            OceanEffectsVerification.Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();
            var settings=AddressableAssetSettingsDefaultObject.Settings;settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);Phase(0);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool yes,string why){if(!yes)throw new Exception(why);}
        static void VerifyWildlife(GameObject prefab)
        {
            var root=Object.Instantiate(prefab);root.SetActive(false);
            try
            {
                var surface=root.GetComponent<OceanSurface>();var floor=new OceanBiomeSetup.BakedFloor(surface.Shore.GetComponent<MeshFilter>().sharedMesh);
                Require(surface.Schools.Length>=surface.FishPopulations.Sum(p=>p.MinimumSchools)&&surface.Schools.Length<=surface.FishPopulations.Sum(p=>p.MaximumSchools)&&surface.Schools.Select(s=>s.Species).Distinct().Count()==4,"Four species with ranged random school counts");
                Require(surface.Birds.Length==surface.CoastalPopulation.CreatePlan().Birds.Length&&surface.Birds.Select(b=>b.Activity).Distinct().Count()==3&&surface.Crabs.Length==surface.CoastalPopulation.CreatePlan().Crabs.Length,"Ranged flying, skimming and resting gulls plus shore crabs baked");
                foreach(var school in surface.Schools)
                {
                    surface.ConfigureFishHabitat(school);
                    var mesh=school.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
                    var pivots=new System.Collections.Generic.List<Vector4>();var shapes=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(1,pivots);mesh.GetUVs(2,shapes);
                    for(int step=0;step<24;step++)
                    {
                        school.Render(step*Mathf.PI*2/(24*school.Speed));
                        for(int v=0;v<vertices.Length;v+=23)
                        {
                            var p=school.DeformedVertex(vertices[v],pivots[v],shapes[v]);
                            Require(p.y<surface.Water.transform.localPosition.y-.09f&&p.y>floor.Height(p.x,p.z)+.06f,"Every fish path fits between actual baked floor and wave troughs");
                        }
                    }
                }
                var gull=surface.Birds[0];gull.Render(0,1,false);var position=gull.transform.localPosition;var wing=gull.LeftWing.localRotation;
                gull.Render(1,1,false);Require(Vector3.Distance(position,gull.transform.localPosition)>.1f&&Quaternion.Angle(wing,gull.LeftWing.localRotation)>1,"Gulls fly and flap wings");
                foreach(var bird in surface.Birds){bird.Render(1,0,false);Require(!bird.gameObject.activeSelf,"Gulls roost out of view at night");bird.Render(1,1,true);Require(bird.gameObject.activeSelf==(bird.Activity==CoastalBirdActivity.Perching),"Rain shelters flying birds and retains shore roosts");}
                foreach(var crab in surface.Crabs)
                {
                    crab.Render(0);position=crab.transform.localPosition;float movement=0;
                    for(int step=1;step<=30;step++){crab.Render(step);movement=Mathf.Max(movement,Vector3.Distance(position,crab.transform.localPosition));}
                    Require(movement>.01f,"Shore crabs crawl between resting intervals");
                    for(int step=0;step<24;step++){crab.Render(step);var p=crab.transform.localPosition;Require(MagicIsland.Interaction.IslandLayout.ContainsLand(p.x,p.z)&&Mathf.Abs(p.y-((float)MagicIsland.Interaction.IslandLayout.Height(p.x,p.z)-.008f))<.001f,"Crabs stay on dry coastline and follow ground height");}
                }
                VerifyReactions(surface,floor);
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void VerifyReactions(OceanSurface surface,OceanBiomeSetup.BakedFloor floor)
        {
            var bird=surface.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching);bird.Phase=0;bird.Render(0,1,false);
            var player=bird.Center-Vector3.forward*.5f;var rested=bird.transform.localPosition;
            for(int i=1;i<=20;i++){bird.React(.1f,player,i*.1f,1,false);bird.Render(i*.1f,1,false);}
            Require(bird.Alarm>.99f&&bird.transform.localPosition.y>rested.y+2,"Approaching a resting gull causes smooth takeoff");
            for(int i=21;i<=80;i++){bird.React(.1f,player,i*.1f,1,false);bird.Render(i*.1f,1,false);Require(bird.Alarm>.99f,"Gull stays airborne while player waits at its roost");}
            var paused=bird.transform.localPosition;var wing=bird.LeftWing.localRotation;bird.React(0,null,8,1,false);bird.Render(8,1,false);
            Require(paused==bird.transform.localPosition&&Quaternion.Angle(wing,bird.LeftWing.localRotation)<.01f,"Takeoff and wing animation freeze on pause");
            for(int i=81;i<=165;i++){bird.React(.1f,null,i*.1f,1,false);bird.Render(i*.1f,1,false);}
            Require(bird.Alarm==0&&Vector3.Distance(bird.Center,bird.transform.localPosition)<.01f,"Gull returns and rests after quiet delay and recovery");
            foreach(var crab in surface.Crabs)
            {
                crab.React(.4f,crab.Center-crab.Tangent*.4f);crab.Render(.4f);Require(crab.Alarm>.99f&&Vector3.Distance(crab.Center,crab.transform.localPosition)>.05f,"Nearby player makes crab scurry sideways: "+crab.name+" alarm="+crab.Alarm+" position="+crab.transform.localPosition+" center="+crab.Center+" radius="+crab.ReactionDistance+" escape="+crab.EscapeDistance);
                paused=crab.transform.localPosition;crab.React(0,null);crab.Render(.4f);Require(paused==crab.transform.localPosition,"Fleeing crab freezes on pause");
                for(int i=1;i<=100;i++)
                {
                    crab.React(.1f,i<30?(Vector3?)(crab.Center-crab.Tangent*.4f):null);crab.Render(.4f+i*.1f);var p=crab.transform.localPosition;
                    Require(MagicIsland.Interaction.IslandLayout.ContainsLand(p.x,p.z,.4)&&Mathf.Abs(p.y-((float)MagicIsland.Interaction.IslandLayout.Height(p.x,p.z)-.008f))<.001f,"Fleeing and returning crab stays on dry sand and follows its height");
                }
                Require(crab.Alarm==0,"Crab settles after player leaves");
            }
            foreach(var school in surface.Schools)
            {
                surface.ConfigureFishHabitat(school);
                var mesh=school.GetComponent<MeshFilter>().sharedMesh;var data=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(1,data);
                var shapes=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(2,shapes);
                Require(data.Count==mesh.vertexCount&&data.Select(v=>v.w).Distinct().Count()==school.Members,"Every fish has an independent scatter pivot");
                Require(school.Startle(school.transform.localPosition+Vector3.right),"Nearby splash reaches school");school.React(.3f,null);school.Render(.3f);
                var block=new MaterialPropertyBlock();school.GetComponent<Renderer>().GetPropertyBlock(block);
                Require(school.Alarm>.99f&&block.GetFloat("_FishScatter")>.3f,"Splash scatters individual fish through renderer properties");
                paused=school.transform.localPosition;school.React(0,null);school.Render(.3f);Require(paused==school.transform.localPosition,"Scattering fish freeze on pause");
                var vertices=mesh.vertices;
                for(int step=0;step<12;step++)
                {
                    school.Startle(school.transform.localPosition+Vector3.right);school.React(.1f,null);school.Render(step*Mathf.PI*2/(12*school.Speed));
                    for(int v=0;v<vertices.Length;v+=137)
                    {
                        var p=school.DeformedVertex(vertices[v],data[v],shapes[v]);
                        Require(p.y>floor.Height(p.x,p.z)+.06f&&p.y<surface.SeaLevel-.09f,"Independently swimming and scattering vertices remain above floor and below waves");
                        Require(school.GetComponent<Renderer>().localBounds.Contains(p-school.transform.localPosition),"Animated members stay inside renderer culling bounds");
                    }
                }
                for(int i=0;i<70;i++){school.React(.1f,null);school.Render(20+i*.1f);}
                Require(school.Alarm==0,"Fish regroup after a splash without repeated triggers");
                school.React(.3f,school.transform.localPosition);Require(school.Alarm>.99f,"Starting nearby fishing also startles fish");
                for(int i=0;i<70;i++)school.React(.1f,school.Center);
                Require(school.Alarm==0,"Waiting angler does not permanently scatter the school");
            }
        }
        static void Phase(int n){SessionState.SetInt(Key+".Phase",n);SessionState.SetFloat(Key+".PhaseTime",(float)EditorApplication.timeSinceStartup);Debug.Log("Ocean phase "+n);}
        static void Capture(GameManager g,string name,Vector3? focus=null,float size=10.5f)
        {
            // Render the actual AA coast with the gameplay camera, without UI for material inspection.
            var canvases=Object.FindObjectsOfType<Canvas>();var active=canvases.Select(c=>c.enabled).ToArray();
            var view=g.Players.Player.ViewCamera;var controller=view.GetComponent<CameraController>();bool enabled=controller.enabled;controller.enabled=false;
            var oldSize=view.orthographicSize;var oldPosition=view.transform.position;
            try
            {
                foreach(var c in canvases)c.enabled=false;
                view.orthographicSize=size;view.transform.position=(focus??new Vector3(10,.6f,-5))-view.transform.forward*Mathf.Max(24,size*2.5f);
                GameUiPreview.CaptureInventory(view);File.Copy("Logs/InventoryPreview.png","Logs/"+name+".png",true);
            }
            finally{for(int i=0;i<canvases.Length;i++)canvases[i].enabled=active[i];view.orthographicSize=oldSize;view.transform.position=oldPosition;controller.enabled=enabled;}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==13&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".PhaseTime",0)<.15)return;
            try
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)>240)throw new Exception("Ocean timeout "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"Ocean startup");
                if(p==0&&g.State==GameFlowState.MainMenu){g.EnterGame();Phase(1);}
                else if(p==1&&g.State==GameFlowState.Playing)
                {
                    var surface=g.Ocean.Surface;var view=g.Players.Player.ViewCamera;
                    Require(g.Ocean.OwnsOcean&&surface.IsValid&&surface.Water.sharedMaterial.shader.isSupported,"Packed AA ocean owned with supported shader");
                    Require((view.depthTextureMode&DepthTextureMode.Depth)!=0,"Gameplay camera renders depth for shallow water");
                    var world=Object.FindObjectOfType<MagicIsland.Prototype.FarmPrototype>();
                    Require(g.Terrain.OwnsTerrain&&g.Terrain.Terrain.IsValid,"AA terrain provides land and sea boundary");
                    var habitats=surface.GetComponentsInChildren<OceanHabitatPatch>();
                    Require(surface.BiomeArtVersion==OceanBiomeSetup.Version&&surface.Water.GetComponent<MeshFilter>().sharedMesh.bounds.size.x==240&&habitats.Length>=50&&habitats.Sum(h=>h.Population)>=1000&&surface.Schools.Length>=6,"Packed AA contains enlarged sea, clustered habitats and fish schools");
                    Require(habitats.Sum(h=>h.Grasses)>150&&habitats.Sum(h=>h.Corals)>80&&habitats.Sum(h=>h.Sponges)>25&&habitats.Sum(h=>h.Stars)>20&&habitats.Sum(h=>h.Shells)>20,"Diverse shallow ecosystem");
                    var population=surface.CoastalPopulation.CreatePlan();
                    Require(surface.Schools.Length>=surface.FishPopulations.Sum(p=>p.MinimumSchools)&&surface.Schools.Length<=surface.FishPopulations.Sum(p=>p.MaximumSchools)&&surface.Birds.Length==population.Birds.Length&&surface.Crabs.Length==population.Crabs.Length,"Packed AA includes ranged fish schools, gulls and crabs");
                    Require(surface.EffectsArtVersion==OceanInteractionEffectsSetup.Version&&surface.Effects!=null&&surface.Effects.IsValid,"Packed AA loads the nested interaction effects pool and material dependencies");
                    var material=surface.Water.sharedMaterial;
                    Require(material.GetFloat("_NormalStrength")>0&&material.GetFloat("_FoamStrength")>0&&material.GetFloat("_CausticsStrength")>0,"AA material includes ripple, surf and caustic controls");
                    SessionState.SetString(Key+".Material",EditorJsonUtility.ToJson(material));
                    g.Time.Clock.Restore(750);g.Weather.Tick(750,false);Phase(24);
                }
                else if(p==24)
                {
                    var surface=g.Ocean.Surface;var material=surface.Water.sharedMaterial;
                    var block=new MaterialPropertyBlock();surface.Water.GetPropertyBlock(block);
                    var direction=(Vector3)block.GetVector("_SunDirection");
                    Require(Vector3.Dot(direction,-g.Weather.Sun.transform.forward)>.999f&&Mathf.Abs(block.GetFloat("_SunIntensity")-g.Weather.Sun.intensity)<.01f,"Actual solar direction and intensity drive water reflections");
                    surface.Tick(.8f,1,false);Require(surface.AnimationTime>.7f&&surface.Daylight==1&&surface.Rain==0,"Waves advance and daylight parameters update");
                    Capture(g,"OceanDayPreview");Capture(g,"OceanShallowsPreview",new Vector3(18.3f,-.1f,-5.5f),5.2f);
                    Capture(g,"OceanGeographyPreview",new Vector3(7,-.1f,5),43);
                    Capture(g,"OceanFishPreview",surface.Schools[0].Center,3.5f);
                    OceanFishVisibilityVerification.Verify(g);
                    OceanFishBodyFlowVerification.Verify(g);
                    for(int frame=0;frame<48;frame++){surface.Tick(.16f,1,false,g.Weather.Sun);Capture(g,"OceanFishMotion"+frame.ToString("00"),surface.Schools[0].Center,3.5f);}
                    for(int frame=0;frame<90;frame++){surface.Tick(1f/30,1,false,g.Weather.Sun);Capture(g,"OceanFishSmooth"+frame.ToString("00"),surface.Schools[0].Center,3.5f);}
                    var habitats=surface.GetComponentsInChildren<OceanHabitatPatch>();
                    Capture(g,"OceanSmallCoralsPreview",habitats.First(h=>h.SmallCorals>10).transform.position,4.8f);
                    Capture(g,"OceanCoralTerracePreview",habitats.First(h=>h.LargeCorals>18).transform.position,5.8f);
                    Capture(g,"OceanBirdsPreview",surface.Birds[0].Center,5.5f);
                    foreach(var species in new[]{ShallowFishSpecies.Clownfish,ShallowFishSpecies.Butterflyfish,ShallowFishSpecies.SpottedRay})
                        Capture(g,"Ocean"+species+"Preview",surface.Schools.First(s=>s.Species==species).transform.localPosition,2.5f);
                    var geography=new OceanGeography(surface.GeographySeed);var shoal=geography.Shoals.OrderBy(s=>Math.Abs(s.Z)+Math.Abs(s.X-40)).First();
                    Capture(g,"OceanReefLagoonPreview",new Vector3((float)shoal.X,-.1f,(float)shoal.Z),8);
                    var beforeFish=surface.Schools[0].transform.localPosition;
                    surface.Tick(2.3f,1,false);Capture(g,"OceanWavePreview");
                    Require(Vector3.Distance(beforeFish,surface.Schools[0].transform.localPosition)>.02f,"School swims using ocean animation clock");
                    Require(!File.ReadAllBytes("Logs/OceanDayPreview.png").SequenceEqual(File.ReadAllBytes("Logs/OceanWavePreview.png")),"Wave time changes rendered pixels");
                    var landing=g.Fishing.Spot.Bobber.position;g.Ocean.DisturbWater(landing);surface.Tick(.16f,1,false,g.Weather.Sun);
                    Capture(g,"OceanSplashEffectsPreview",landing,3.2f);surface.Tick(.32f,1,false,g.Weather.Sun);Capture(g,"OceanRippleEffectsPreview",landing,3.2f);
                    var ray=surface.Schools.First(s=>s.Species==ShallowFishSpecies.SpottedRay);surface.Tick(.35f,1,false,g.Weather.Sun);
                    Capture(g,"OceanSandEffectsPreview",ray.transform.position,3.5f);
                    surface.Tick(0,0,true);Require(surface.Rain==1&&surface.Daylight==0,"Night and rain update ocean tint");
                    g.Time.Clock.Restore(1260);g.Weather.Tick(1260,false);
                    Require(SessionState.GetString(Key+".Material","")==EditorJsonUtility.ToJson(material),"Runtime weather and waves do not mutate shared AA material");
                    Phase(20);
                }
                else if(p==20)Phase(21);
                else if(p==21)
                {
                    Require(g.Ocean.Surface.Daylight==0,"Real game clock drives night tint");
                    var block=new MaterialPropertyBlock();g.Ocean.Surface.Water.GetPropertyBlock(block);Require(block.GetFloat("_SunIntensity")==0,"Night cannot retain sunlight glints");Capture(g,"OceanNightPreview");
                    g.Weather.State.NextDay(g.Weather.State.Day+1,1);g.Time.Clock.Restore(600);Phase(22);
                }
                else if(p==22)Phase(23);
                else if(p==23)
                {
                    Require(g.Ocean.Surface.Rain==1&&g.Ocean.Surface.Daylight==1&&g.Weather.Rain.isPlaying,"Actual rainy weather drives water and rain together");
                    Capture(g,"OceanRainPreview");g.Weather.State.NextDay(g.Weather.State.Day+1,0);
                    g.Time.Clock.Restore(750);g.Weather.Tick(750,false);
                    SessionState.SetVector3(Key+".PlayerBeforeReaction",g.Players.Player.transform.position);
                    var bird=g.Ocean.Surface.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching);
                    g.Players.RestorePosition(g.Ocean.Surface.transform.TransformPoint(bird.Center)+new Vector3(.3f,.15f,0));
                    SessionState.SetFloat(Key+".ReactionStart",g.Ocean.Surface.AnimationTime);Phase(30);
                }
                else if(p==30&&g.Ocean.Surface.AnimationTime-SessionState.GetFloat(Key+".ReactionStart",0)>1.8f)
                {
                    var bird=g.Ocean.Surface.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching);
                    Require(bird.Alarm>.9f&&bird.transform.localPosition.y>bird.Center.y+2,"Actual player proximity is routed through OceanManager into safe gull takeoff: alarm="+bird.Alarm+", position="+bird.transform.localPosition+", landing="+bird.LandingPoint);
                    Capture(g,"WildlifeGullReactionPreview",bird.Center,6);
                    var outward=new Vector3(bird.Center.x,0,bird.Center.z).normalized;
                    g.Ocean.DisturbWater(g.Ocean.Surface.transform.TransformPoint(bird.Center+outward*2));
                    g.OpenPause();Phase(31);
                }
                else if(p==31&&Object.FindObjectOfType<PauseView>()!=null)
                {
                    var bird=g.Ocean.Surface.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching);
                    SessionState.SetVector3(Key+".StartledBird",bird.transform.localPosition);SessionState.SetFloat(Key+".Alarm",bird.Alarm);Phase(32);
                    SessionState.SetFloat(Key+".EffectAges",g.Ocean.Surface.Effects.Particles.Sum(e=>e.Age));SessionState.SetInt(Key+".EffectEmissions",g.Ocean.Surface.Effects.EmissionCount);
                }
                else if(p==32)
                {
                    var bird=g.Ocean.Surface.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching);
                    Require(bird.transform.localPosition==SessionState.GetVector3(Key+".StartledBird",Vector3.zero)&&bird.Alarm==SessionState.GetFloat(Key+".Alarm",-1),"Real pause freezes an active startle and its recovery timer");
                    Require(g.Ocean.Surface.Effects.Particles.Sum(e=>e.Age)==SessionState.GetFloat(Key+".EffectAges",-1)&&g.Ocean.Surface.Effects.EmissionCount==SessionState.GetInt(Key+".EffectEmissions",-1),"Real pause freezes splash lifetime and wildlife effect emission");
                    int splashes=g.Ocean.Surface.SplashCount;g.Ocean.DisturbWater(g.Fishing.Spot.Bobber.position);Require(splashes==g.Ocean.Surface.SplashCount,"Paused game rejects new splash stimuli");
                    g.ResumeFromPause();var crab=g.Ocean.Surface.Crabs[0];g.Players.RestorePosition(g.Ocean.Surface.transform.TransformPoint(crab.Center-crab.Tangent*.4f)+Vector3.up*.1f);
                    SessionState.SetFloat(Key+".ReactionStart",g.Ocean.Surface.AnimationTime);Phase(33);
                }
                else if(p==33&&g.Ocean.Surface.AnimationTime-SessionState.GetFloat(Key+".ReactionStart",0)>.6f)
                {
                    var crab=g.Ocean.Surface.Crabs[0];Require(crab.Alarm>.95f,"Actual player proximity makes beach crab scurry");
                    Capture(g,"WildlifeCrabReactionPreview",crab.Center,4.5f);
                    Capture(g,"OceanCrabEffectsPreview",crab.transform.localPosition,2.8f);
                    for(int frame=0;frame<12;frame++)
                    {
                        g.Ocean.Surface.Tick(.06f,1,false,g.Weather.Sun,g.Players.Player.transform.position);
                        Capture(g,"CoastalCrabMotion"+frame.ToString("00"),crab.transform.localPosition+Vector3.up*.1f,1.2f);
                    }
                    g.Players.RestorePosition(SessionState.GetVector3(Key+".PlayerBeforeReaction",Vector3.zero));SessionState.SetFloat(Key+".ReactionStart",g.Ocean.Surface.AnimationTime);Phase(34);
                }
                else if(p==34&&g.Ocean.Surface.AnimationTime-SessionState.GetFloat(Key+".ReactionStart",0)>8.8f)
                {
                    Require(g.Ocean.Surface.Crabs[0].Alarm==0&&g.Ocean.Surface.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching).Alarm==0,"Real manager settles wildlife after player moves away");
                    var surface=g.Ocean.Surface;float nearest=float.PositiveInfinity;Vector3 shore=Vector3.zero;int index=-1;
                    for(int s=0;s<surface.Schools.Length;s++)foreach(var member in surface.Schools[s].Steering.States)
                    {
                        double angle=Math.Atan2(member.Position.z,member.Position.x),radius=MagicIsland.Interaction.IslandLayout.CoastRadius(angle)-1.05;
                        var point=new Vector3((float)(Math.Cos(angle)*radius),.1f,(float)(Math.Sin(angle)*radius));
                        float distance=Vector2.Distance(new Vector2(point.x,point.z),new Vector2(member.Position.x,member.Position.z));
                        if(distance<nearest&&distance<surface.Schools[s].PlayerReactionDistance-.25f&&Mathf.Abs(point.y-member.Position.y)<1.6f)
                        {nearest=distance;shore=point;index=s;}
                    }
                    Require(index>=0,"Actual baked shallow fish can detect a player on dry beach");
                    Debug.Log("Actual beach-to-fish detection distance: "+nearest);
                    SessionState.SetInt(Key+".ShoreSchool",index);g.Players.RestorePosition(surface.transform.TransformPoint(shore));
                    SessionState.SetFloat(Key+".ReactionStart",surface.AnimationTime);Phase(36);
                }
                else if(p==36&&g.Ocean.Surface.AnimationTime-SessionState.GetFloat(Key+".ReactionStart",0)>.8f)
                {
                    var school=g.Ocean.Surface.Schools[SessionState.GetInt(Key+".ShoreSchool",0)];
                    Require(school.Alarm>.95f,"Actual player standing on beach startles nearby shallow fish");
                    Capture(g,"CoastalPlayerFishReaction",school.Center,4);
                    g.Players.RestorePosition(SessionState.GetVector3(Key+".PlayerBeforeReaction",Vector3.zero));
                    SessionState.SetFloat(Key+".ReactionStart",g.Ocean.Surface.AnimationTime);Phase(37);
                }
                else if(p==37&&g.Ocean.Surface.AnimationTime-SessionState.GetFloat(Key+".ReactionStart",0)>6.2f)
                {
                    Require(g.Ocean.Surface.Schools[SessionState.GetInt(Key+".ShoreSchool",0)].Alarm==0,"Actual shore player leaving permits fish recovery");
                    g.OpenPause();Phase(2);
                }
                else if(p==2&&Object.FindObjectOfType<PauseView>()!=null)
                {
                    CoastalNavigationVerification.VerifyRuntime(g);
                    CoastalBirdIdleVerification.VerifyRuntime(g);
                    CoastalCrabIdleVerification.VerifyRuntime(g);
                    OceanEcologyRhythmVerification.VerifyRuntime(g);
                    SessionState.SetFloat(Key+".Time",g.Ocean.Surface.AnimationTime);SessionState.SetVector3(Key+".Fish",g.Ocean.Surface.Schools[0].transform.localPosition);
                    SessionState.SetVector3(Key+".Bird",g.Ocean.Surface.Birds[0].transform.localPosition);SessionState.SetVector3(Key+".Wing",g.Ocean.Surface.Birds[0].LeftWing.localEulerAngles);SessionState.SetVector3(Key+".Crab",g.Ocean.Surface.Crabs[0].transform.localPosition);Phase(3);
                }
                else if(p==3)
                {
                    Require(Mathf.Approximately(g.Ocean.Surface.AnimationTime,SessionState.GetFloat(Key+".Time",-1)),"Pause freezes wave clock");
                    Require(Vector3.Distance(g.Ocean.Surface.Schools[0].transform.localPosition,SessionState.GetVector3(Key+".Fish",Vector3.zero))<.00001f,"Pause freezes fish and plant clock");
                    Require(Vector3.Distance(g.Ocean.Surface.Birds[0].transform.localPosition,SessionState.GetVector3(Key+".Bird",Vector3.zero))<.00001f&&Quaternion.Angle(g.Ocean.Surface.Birds[0].LeftWing.localRotation,Quaternion.Euler(SessionState.GetVector3(Key+".Wing",Vector3.zero)))<.001f&&Vector3.Distance(g.Ocean.Surface.Crabs[0].transform.localPosition,SessionState.GetVector3(Key+".Crab",Vector3.zero))<.00001f,"Pause freezes gull flight, wings and crab movement");
                    g.ResumeFromPause();g.Players.RestorePosition(g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome();Phase(4);
                }
                else if(p==4&&g.State==GameFlowState.Playing&&g.Housing.Inside)Phase(5);
                else if(p==5)
                {
                    Require(!g.Ocean.Surface.gameObject.activeSelf&&g.Ocean.OwnsOcean&&!g.Ocean.Surface.Effects.gameObject.activeInHierarchy,"Interior hides coast and effects while preserving owned instance");
                    g.Players.RestorePosition(g.Housing.Interior.Exit.Position+new Vector3(0,.1f,1.2f));g.ExitHome();Phase(6);
                }
                else if(p==6&&g.State==GameFlowState.Playing&&!g.Housing.Inside)Phase(7);
                else if(p==7){Require(g.Ocean.Surface.gameObject.activeSelf,"Returning island reactivates coast");g.ReturnToMainMenu();Phase(8);}
                else if(p==8&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(9);}
                else if(p==9&&g.State==GameFlowState.MainMenu)
                {
                    Require(!g.Ocean.OwnsCoastalNavigation,"Menu releases coastal physics queries and reservations");
                    Require(!g.Ocean.OwnsOcean&&g.Ocean.Surface==null&&Object.FindObjectsOfType<OceanSurface>(true).Length==0&&Object.FindObjectsOfType<CoastalBird>(true).Length==0&&Object.FindObjectsOfType<CoastalCrab>(true).Length==0&&Object.FindObjectsOfType<OceanEffectParticle>(true).Length==0,"Menu releases ocean, wildlife and pooled effects AA instance");
                    fixture=new GameObject("Ocean AA failure fixture").AddComponent<OceanManager>();fixture.StartCoroutine(fixture.Load("Ocean_Verification_Missing",fixture.transform,null,null));Phase(10);
                }
                else if(p==10&&fixture.LastError!=null)
                {
                    Require(!fixture.OwnsCoastalNavigation,"Failed AA load cannot own navigation");
                    Require(!fixture.OwnsOcean&&fixture.Surface==null,"Failed ocean load releases handle");
                    fixture.StartCoroutine(fixture.Load(g.Configuration.Config.OceanPrefabAddress,fixture.transform,null,null));fixture.Clear();Phase(11);
                }
                else if(p==11){Require(!fixture.OwnsOcean&&fixture.Surface==null&&fixture.GetComponentsInChildren<OceanSurface>(true).Length==0,"Cancelled ocean load cannot survive");Object.Destroy(fixture.gameObject);g.EnterGame();Phase(12);}
                else if(p==12&&g.State==GameFlowState.Playing)
                {
                    Require(g.Ocean.OwnsCoastalNavigation,"Re-entry binds current coastal navigation");
                    Require(g.Ocean.OwnsOcean&&Object.FindObjectsOfType<OceanSurface>(true).Length==1,"Re-entry creates exactly one ocean");
                    File.WriteAllText("Logs/OceanVerification.txt","PASS: packed AA, four fish species / ranged random school and member counts and bounded free swimming, whole swim-cycle floor and wave clearance, seeded sparse gulls with flight / wing / roost / rain behaviour, articulated shore crabs, actual beach player fish alarm / recovery, wildlife pause and unload, shaders, ripple/surf/caustic controls, actual solar reflection inputs, night clears sunlight glints, actual render/wave changes, original material preservation, depth camera, coast boundary preservation, day/night/rain, home culling, failure/cancel and re-entry.\n");
                    Debug.Log("Ocean flow verification PASSED.");Phase(13);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
    }
}
