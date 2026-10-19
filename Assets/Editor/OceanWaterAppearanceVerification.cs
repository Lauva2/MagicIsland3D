using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Render the real Packed AA coast with the same camera, sun and animation time.
    [InitializeOnLoad] public static class OceanWaterAppearanceVerification
    {
        const string Key="MagicIsland.WaterAppearance";
        static OceanWaterAppearanceVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void RunBaseline()=>Begin("Before");
        public static void Run()=>Begin("After");
        public static void RunWaves()=>Begin("Waves");
        public static void RunTint()=>Begin("Tint");
        public static void RunSeabed()=>Begin("Seabed");
        public static void RunSurfBaseline()=>Begin("SurfBefore");
        public static void RunSurf()=>Begin("Surf");
        public static void RunBeachBaseline()=>Begin("BeachBefore");
        public static void RunBeach()=>Begin("Beach");
        public static void RunArrivalBaseline()=>Begin("ArrivalBefore");
        public static void RunArrival()=>Begin("Arrival");
        public static void RunDesertBaseline()=>Begin("DesertBefore");
        public static void RunDesert()=>Begin("Desert");
        public static void RunSnowBaseline()=>Begin("SnowBefore");
        public static void RunSnow()=>Begin("Snow");
        public static void RunCloudBaseline()=>Begin("CloudBefore");
        public static void RunCloud()=>Begin("Cloud");
        public static void RunTrailBaseline()=>Begin("TrailBefore");
        public static void RunTrail()=>Begin("Trail");
        public static void RunDesertTrailBaseline()=>Begin("DesertTrailBefore");
        public static void RunDesertTrail()=>Begin("DesertTrail");
        public static void RunForestBaseline()=>Begin("ForestBefore");
        public static void RunForest()=>Begin("Forest");
        public static void RunBiomeBaseline()=>Begin("BiomesBefore");
        public static void RunBiomes()=>Begin("Biomes");
        public static void RunCompositionBaseline()=>Begin("CompositionBefore");
        public static void RunComposition()=>Begin("Composition");
        public static void RunGrasslandCompositionBaseline()=>Begin("GrasslandCompositionBefore");
        public static void RunGrasslandComposition()=>Begin("GrasslandComposition");
        public static void RunGrasslandFinishBaseline()=>Begin("GrasslandFinishBefore");
        public static void RunGrasslandFinish()=>Begin("GrasslandFinish");
        public static void RunGrasslandEcologyBaseline()=>Begin("GrasslandEcologyBefore");
        public static void RunGrasslandEcology()=>Begin("GrasslandEcology");
        public static void RunMeadowBehaviour()=>Begin("MeadowBehaviour");
        public static void RunMeadowRabbits()=>Begin("MeadowRabbits");
        public static void RunMeadowPolish()=>Begin("MeadowPolish");
        public static void RunLivingGrasslandBaseline()=>Begin("LivingGrasslandBefore");
        public static void RunLivingGrassland()=>Begin("LivingGrassland");
        public static void RunDesertCompositionBaseline()=>Begin("DesertCompositionBefore");
        public static void RunDesertComposition()=>Begin("DesertComposition");
        static void Begin(string variant)
        {
            var material=File.ReadAllBytes(OceanSetup.MaterialFolder+"/Water.mat");
            PrototypeAddressablesSetup.EnsureSetup();
            Require(material.SequenceEqual(File.ReadAllBytes(OceanSetup.MaterialFolder+"/Water.mat")),"Setup preserves water material");
            Require(!ShaderUtil.ShaderHasError(Shader.Find("MagicIsland/Island Ocean")),"Water shader compiles");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();
            var settings=AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetString(Key+".Variant",variant);SessionState.SetInt(Key+".Phase",0);
            SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);SessionState.SetBool(Key,true);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Water appearance: "+why);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            try
            {
                int phase=SessionState.GetInt(Key+".Phase",0);
                if(phase==2&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
                Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)<180,"Flow timeout");
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;
                Require(g.State!=GameFlowState.Failed,"AA startup");
                if(phase==0&&g.State==GameFlowState.MainMenu){g.EnterGame();SessionState.SetInt(Key+".Phase",1);}
                else if(phase==1&&g.State==GameFlowState.Playing)
                {
                    Require(g.Ocean.OwnsOcean&&g.Ocean.Surface.IsValid&&g.Ocean.Surface.Water.sharedMaterial.shader.isSupported,"Packed water loaded");
                    if(SessionState.GetString(Key+".Variant","")=="Arrival")ArrivalRouteVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="Desert")DesertFrontVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="Snow")SnowFrontVerification.VerifyRuntime(g);
                    Capture(g);
                    if(SessionState.GetString(Key+".Variant","")=="Cloud")CloudShadowVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="Trail")LakeSnowTrailVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="DesertTrail")GrassDesertTrailVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="Forest")ForestEdgeVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="Biomes")BiomePolishVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="Composition")BeachCompositionVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="LivingGrassland")GrasslandRevitalizationVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="DesertComposition")DesertCompositionVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="GrasslandComposition")GrasslandCompositionVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="GrasslandFinish")GrasslandFinishVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="GrasslandEcology")GrasslandEcologyVerification.VerifyRuntime(g);
                    if(SessionState.GetString(Key+".Variant","")=="MeadowBehaviour"){GrasslandEcologyVerification.VerifyRuntime(g);MeadowBehaviourVerification.VerifyRuntime(g);}
                    if(SessionState.GetString(Key+".Variant","")=="MeadowRabbits"){GrasslandEcologyVerification.VerifyRuntime(g);MeadowBehaviourVerification.VerifyRuntime(g);MeadowRabbitVerification.VerifyRuntime(g);}
                    if(SessionState.GetString(Key+".Variant","")=="MeadowPolish"){GrasslandEcologyVerification.VerifyRuntime(g);MeadowBehaviourVerification.VerifyRuntime(g);MeadowRabbitVerification.VerifyRuntime(g);MeadowPolishVerification.VerifyRuntime(g);}
                    if(SessionState.GetString(Key+".Variant","")=="Arrival")
                    {ArrivalRouteVerification.BeginPlacement(g);SessionState.SetInt(Key+".Phase",3);return;}
                    if(SessionState.GetString(Key+".Variant","")=="LivingGrassland"){GrasslandRevitalizationVerification.BeginPlacement(g);SessionState.SetInt(Key+".Phase",4);return;}
                    Debug.Log("Ocean water appearance verification PASSED: Packed AA, morning/day/night/rain and lake renders; paused clock and shared materials preserved.");
                    SessionState.SetInt(Key+".Phase",2);EditorApplication.isPlaying=false;
                }
                else if(phase==4&&g.Placements.Ready)
                {
                    GrasslandRevitalizationVerification.VerifyPlacement(g);
                    Debug.Log("Living grassland appearance and construction verification PASSED.");
                    SessionState.SetInt(Key+".Phase",2);EditorApplication.isPlaying=false;
                }
                else if(phase==3&&g.Placements.Ready)
                {
                    ArrivalRouteVerification.VerifyPlacement(g);
                    Debug.Log("Arrival route Packed appearance and construction verification PASSED.");
                    SessionState.SetInt(Key+".Phase",2);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
        static void Capture(GameManager g)
        {
            var s=g.Ocean.Surface;var camera=g.Players.Player.ViewCamera;var controller=camera.GetComponent<CameraController>();
            var position=camera.transform.position;float size=camera.orthographicSize;bool follow=controller.enabled;
            var canvases=Object.FindObjectsOfType<Canvas>();var enabled=canvases.Select(c=>c.enabled).ToArray();
            var clouds=g.Weather.Clouds.Renderers;var cloudEnabled=clouds.Select(r=>r.enabled).ToArray();
            var material=s.Water.sharedMaterial;string original=EditorJsonUtility.ToJson(material);
            var groundMaterials=g.Terrain.Terrain.GetComponentsInChildren<Renderer>(true).Concat(new[]{s.Shore})
                .SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
            var groundOriginals=groundMaterials.Select(m=>EditorJsonUtility.ToJson(m)).ToArray();
            string variant=SessionState.GetString(Key+".Variant","");
            bool waves=variant=="Waves",tint=variant=="Tint"||variant=="Seabed",surf=variant=="Surf"||variant=="SurfBefore";
            string folder=surf?"Logs/CoastalSurf/"+(variant=="SurfBefore"?"BeforeRender":"AfterRender"):variant=="Seabed"?"Logs/SeabedSlope/AfterRender":waves?"Logs/OceanWaves/AfterRender":tint?"Logs/OceanTint/AfterRender":"Logs/DawnWater/"+variant;Directory.CreateDirectory(folder);
            double minute=g.Time.Clock.Minute;var kind=g.Weather.State.Kind;
            bool beach=variant=="Beach"||variant=="BeachBefore";
            if(beach){folder="Logs/BeachTransition/"+(variant=="BeachBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool arrival=variant=="Arrival"||variant=="ArrivalBefore";
            if(arrival){folder="Logs/ArrivalRoute/"+(variant=="ArrivalBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool desert=variant=="Desert"||variant=="DesertBefore";
            if(desert){folder="Logs/DesertFront/"+(variant=="DesertBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool snow=variant=="Snow"||variant=="SnowBefore";
            if(snow){folder="Logs/SnowFront/"+(variant=="SnowBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool cloud=variant=="Cloud"||variant=="CloudBefore";
            if(cloud){folder="Logs/CloudShadow/"+(variant=="CloudBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool trail=variant=="Trail"||variant=="TrailBefore";
            if(trail){folder="Logs/LakeSnowTrail/"+(variant=="TrailBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool desertTrail=variant=="DesertTrail"||variant=="DesertTrailBefore";
            if(desertTrail){folder="Logs/GrassDesertTrail/"+(variant=="DesertTrailBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool forest=variant=="Forest"||variant=="ForestBefore";
            if(forest){folder="Logs/ForestEdge/"+(variant=="ForestBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool biomes=variant=="Biomes"||variant=="BiomesBefore";
            if(biomes){folder="Logs/BiomePolish/"+(variant=="BiomesBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool composition=variant=="Composition"||variant=="CompositionBefore";
            if(composition){folder="Logs/BeachComposition/"+(variant=="CompositionBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool livingGrassland=variant=="LivingGrassland"||variant=="LivingGrasslandBefore";
            if(livingGrassland){folder="Logs/LivingGrassland/"+(variant=="LivingGrasslandBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool desertComposition=variant=="DesertComposition"||variant=="DesertCompositionBefore";
            if(desertComposition){folder="Logs/DesertComposition/"+(variant=="DesertCompositionBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool grasslandComposition=variant=="GrasslandComposition"||variant=="GrasslandCompositionBefore";
            if(grasslandComposition){folder="Logs/GrasslandComposition/"+(variant=="GrasslandCompositionBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool grasslandFinish=variant=="GrasslandFinish"||variant=="GrasslandFinishBefore";
            if(grasslandFinish){folder="Logs/GrasslandFinish/"+(variant=="GrasslandFinishBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            bool meadow=variant=="GrasslandEcology"||variant=="GrasslandEcologyBefore"||variant=="MeadowBehaviour"||variant=="MeadowRabbits"||variant=="MeadowPolish";
            if(meadow){folder=variant=="MeadowPolish"?"Logs/MeadowPolish/AfterRender":variant=="MeadowRabbits"?"Logs/MeadowRabbits/AfterRender":variant=="MeadowBehaviour"?"Logs/MeadowBehaviour/AfterRender":"Logs/GrasslandEcology/"+(variant=="GrasslandEcologyBefore"?"BeforeRender":"AfterRender");Directory.CreateDirectory(folder);}
            try
            {
                controller.enabled=false;g.Cameras.SetOcclusionEnabled(false);
                foreach(var c in canvases)c.enabled=false;foreach(var r in clouds)r.enabled=false;
                s.Tick(20-s.AnimationTime,1,false,g.Weather.Sun);
                foreach(var light in new[]{new {Name="Morning",Minute=360,Rain=false},new {Name="Day",Minute=750,Rain=false},new {Name="Night",Minute=1260,Rain=false},new {Name="Rain",Minute=600,Rain=true}})
                {
                    typeof(WeatherState).GetProperty("Kind").SetValue(g.Weather.State,light.Rain?WeatherKind.Rainy:WeatherKind.Sunny);
                    AtmospherePolishVerification.Advance(g.Weather,40);LightingFlowVerification.SetTime(g,light.Minute);
                    if(meadow)GrasslandEcologyVerification.StageWeather(s.WorldEcology,light.Rain);
                    s.Tick(0,s.Daylight,light.Rain,g.Weather.Sun,rainAmount:g.Weather.RainAmount);
                    float clock=s.AnimationTime;
                    Shot(camera,folder+"/"+light.Name+"Shallows",new Vector3(18.3f,-.1f,-5.5f),7);
                    Shot(camera,folder+"/"+light.Name+"Reef",s.Schools.First(f=>f.Species==ShallowFishSpecies.Clownfish).Center,5);
                    Shot(camera,folder+"/"+light.Name+"Deep",new Vector3(85,s.SeaLevel,-75),14);
                    Shot(camera,folder+"/"+light.Name+"Lake",new Vector3(-37,12,678),8);
                    if(tint||surf)Shot(camera,folder+"/"+light.Name+"Coast",new Vector3(34,s.SeaLevel,-20),14);
                    if(surf)Shot(camera,folder+"/"+light.Name+"Beach",Beach(s,-.5f),3.8f);
                    if(beach)
                    {
                        Shot(camera,folder+"/"+light.Name+"East",Beach(s,-.5f)-new Vector3(1,0,0),5);
                        Shot(camera,folder+"/"+light.Name+"South",Beach(s,-1.9f)+new Vector3(0,0,1),5);
                        Shot(camera,folder+"/"+light.Name+"West",Beach(s,3.3f)+new Vector3(1,0,0),5);
                        Shot(camera,folder+"/"+light.Name+"Village",Vector3.zero,7);
                        if(variant=="Beach")WetSandControl(camera,g,folder+"/"+light.Name+"EastDry",Beach(s,-.5f)-new Vector3(1,0,0),5);
                    }
                    if(arrival)
                    {
                        Shot(camera,folder+"/"+light.Name+"Spawn",new Vector3(1,0,-14),5.5f);
                        Shot(camera,folder+"/"+light.Name+"Approach",new Vector3(1,0,-10),7);
                        Shot(camera,folder+"/"+light.Name+"Village",new Vector3(1,0,-4),11);
                        Shot(camera,folder+"/"+light.Name+"Home",g.Housing.Entrance.Position+Vector3.back,4.5f);
                        Shot(camera,folder+"/"+light.Name+"Dock",new Vector3(13,0,-8.2f),5.5f);
                    }
                    if(desert)
                    {
                        DesertShot(camera,folder+"/"+light.Name+"Entry",-195,415,22);
                        DesertShot(camera,folder+"/"+light.Name+"Transition",-252,470,26);
                        DesertShot(camera,folder+"/"+light.Name+"Sand",-324,530,26);
                        DesertShot(camera,folder+"/"+light.Name+"Overview",-268,480,65);
                        Shot(camera,folder+"/"+light.Name+"Village",new Vector3(1,0,-10),7);
                    }
                    if(snow)
                    {
                        DesertShot(camera,folder+"/"+light.Name+"Entry",280,846,22);
                        DesertShot(camera,folder+"/"+light.Name+"Transition",214,887,25);
                        DesertShot(camera,folder+"/"+light.Name+"Snow",185,948,25);
                        DesertShot(camera,folder+"/"+light.Name+"Overview",242,875,70);
                        DesertShot(camera,folder+"/"+light.Name+"Desert",-268,480,65);
                    }
                    if(cloud)
                    {
                        DesertShot(camera,folder+"/"+light.Name+"Snow",242,875,70);
                        DesertShot(camera,folder+"/"+light.Name+"Grassland",-110,420,45);
                        DesertShot(camera,folder+"/"+light.Name+"Desert",-268,480,65);
                        Shot(camera,folder+"/"+light.Name+"Beach",new Vector3(1,0,-10),7);
                        if(light.Name=="Day")
                        {
                            var cookie=g.Weather.Sun.cookie;
                            try
                            {
                                g.Weather.Sun.cookie=null;
                                DesertShot(camera,folder+"/DaySnowClear",242,875,70);
                                DesertShot(camera,folder+"/DayGrasslandClear",-110,420,45);
                                DesertShot(camera,folder+"/DayDesertClear",-268,480,65);
                            }
                            finally{g.Weather.Sun.cookie=cookie;}
                        }
                    }
                    if(trail)
                    {
                        DesertShot(camera,folder+"/"+light.Name+"Overview",242,875,70);
                        DesertShot(camera,folder+"/"+light.Name+"LowerTurn",220,870,10);
                        DesertShot(camera,folder+"/"+light.Name+"UpperTurn",200,920,10);
                        DesertShot(camera,folder+"/"+light.Name+"Snow",167,957,12);
                        DesertShot(camera,folder+"/"+light.Name+"Grass",292,842,12);
                        DesertShot(camera,folder+"/"+light.Name+"Desert",-268,480,65);
                    }
                    if(desertTrail)
                    {
                        DesertShot(camera,folder+"/"+light.Name+"Overview",-268,480,65);
                        DesertShot(camera,folder+"/"+light.Name+"Junction",-150,370,12);
                        DesertShot(camera,folder+"/"+light.Name+"ArrivalJunction",0,240,13);
                        DesertShot(camera,folder+"/"+light.Name+"Transition",-252,470,20);
                        DesertShot(camera,folder+"/"+light.Name+"Sand",-324,530,20);
                        DesertShot(camera,folder+"/"+light.Name+"SnowControl",242,875,70);
                    }
                    if(forest)
                    {
                        DesertShot(camera,folder+"/"+light.Name+"Overview",455,643,68);
                        DesertShot(camera,folder+"/"+light.Name+"South",473,563,25);
                        DesertShot(camera,folder+"/"+light.Name+"LakeEdge",408,693,25);
                        DesertShot(camera,folder+"/"+light.Name+"Grove",480,650,28);
                        DesertShot(camera,folder+"/"+light.Name+"SnowControl",242,875,70);
                        DesertShot(camera,folder+"/"+light.Name+"DesertControl",-268,480,65);
                    }
                    if(biomes)
                    {
                        Shot(camera,folder+"/"+light.Name+"Beach",new Vector3(18,0,-8),5);
                        Shot(camera,folder+"/"+light.Name+"BeachWide",new Vector3(0,0,-6),20);
                        DesertShot(camera,folder+"/"+light.Name+"Grassland",-122,440,27);
                        DesertShot(camera,folder+"/"+light.Name+"GrasslandWide",-45,405,65);
                        DesertShot(camera,folder+"/"+light.Name+"Desert",-420,755,28);
                        DesertShot(camera,folder+"/"+light.Name+"DesertWide",-400,745,65);
                        DesertShot(camera,folder+"/"+light.Name+"Forest",485,787,28);
                        DesertShot(camera,folder+"/"+light.Name+"ForestWide",535,780,65);
                        DesertShot(camera,folder+"/"+light.Name+"Snow",-215,940,28);
                        DesertShot(camera,folder+"/"+light.Name+"SnowWide",-190,970,65);
                        DesertShot(camera,folder+"/"+light.Name+"ForestEdgeControl",473,563,25);
                        DesertShot(camera,folder+"/"+light.Name+"DesertEntryControl",-268,480,65);
                    }
                    if(composition)
                    {
                        Shot(camera,folder+"/"+light.Name+"Spawn",new Vector3(1,0,-15),4.5f);
                        Shot(camera,folder+"/"+light.Name+"Shore",new Vector3(9,0,-16),4.5f);
                        Shot(camera,folder+"/"+light.Name+"Dock",new Vector3(15,0,-10),5);
                        Shot(camera,folder+"/"+light.Name+"Overview",new Vector3(6,0,-10),12);
                    }
                    if(livingGrassland)
                    {
                        DesertShot(camera,folder+"/"+light.Name+"Main",-122,440,size);
                        DesertShot(camera,folder+"/"+light.Name+"Interior",-60,450,size);
                        DesertShot(camera,folder+"/"+light.Name+"East",150,315,size);
                        DesertShot(camera,folder+"/"+light.Name+"Tree",-55,318,12);
                        DesertShot(camera,folder+"/"+light.Name+"Wide",-60,450,65);
                        DesertShot(camera,folder+"/"+light.Name+"LakeEdge",-106,625,size);
                        DesertShot(camera,folder+"/"+light.Name+"DesertControl",-410,695,26);
                        DesertShot(camera,folder+"/"+light.Name+"ForestControl",473,563,25);
                    }
                    if(desertComposition)
                    {
                        DesertShot(camera,folder+"/"+light.Name+"Entry",-225,445,22);
                        DesertShot(camera,folder+"/"+light.Name+"Bend",-344,538,22);
                        DesertShot(camera,folder+"/"+light.Name+"Interior",-410,695,26);
                        DesertShot(camera,folder+"/"+light.Name+"North",-420,760,26);
                        DesertShot(camera,folder+"/"+light.Name+"Overview",-347,590,110);
                        DesertShot(camera,folder+"/"+light.Name+"GrassControl",-122,440,18);
                        DesertShot(camera,folder+"/"+light.Name+"ForestControl",473,563,25);
                        var spots=DesertCompositionSetup.Sites(g.Terrain.Terrain);
                        foreach(int index in new[]{0,spots.Length/3,spots.Length*2/3,spots.Length-1})
                        {var point=spots[index].Point;DesertShot(camera,folder+"/"+light.Name+"Play"+index,point.x,point.y,size);}
                    }
                    if(grasslandComposition)
                    {
                        DesertShot(camera,folder+"/"+light.Name+"Arrival",0,222,14);
                        DesertShot(camera,folder+"/"+light.Name+"Junction",-150,370,15);
                        DesertShot(camera,folder+"/"+light.Name+"Meadow",-122,440,18);
                        DesertShot(camera,folder+"/"+light.Name+"LakeApproach",-100,585,20);
                        DesertShot(camera,folder+"/"+light.Name+"Overview",-70,390,75);
                    }
                    if(grasslandFinish)
                    {
                        DesertShot(camera,folder+"/"+light.Name+"South",-60,310,42);
                        DesertShot(camera,folder+"/"+light.Name+"Meadow",-60,450,65);
                        DesertShot(camera,folder+"/"+light.Name+"East",150,315,45);
                        DesertShot(camera,folder+"/"+light.Name+"WestLake",-110,620,40);
                        DesertShot(camera,folder+"/"+light.Name+"North",0,810,42);
                        DesertShot(camera,folder+"/"+light.Name+"Road",-122,440,18);
                        DesertShot(camera,folder+"/"+light.Name+"Overview",20,470,180);
                        DesertShot(camera,folder+"/"+light.Name+"DesertControl",-268,480,65);
                        DesertShot(camera,folder+"/"+light.Name+"ForestControl",473,563,25);
                    }
                    if(meadow)
                    {
                        var sites=GrasslandEcologySetup.Sites();
                        DesertShot(camera,folder+"/"+light.Name+"Flowers",sites[1].x,sites[1].y,6);
                        DesertShot(camera,folder+"/"+light.Name+"Detail",sites[1].x,sites[1].y,2.8f);
                        DesertShot(camera,folder+"/"+light.Name+"East",sites.Last().x,sites.Last().y,7.5f);
                        DesertShot(camera,folder+"/"+light.Name+"Meadow",-60,450,65);
                    }
                    s.Tick(0,s.Daylight,light.Rain,g.Weather.Sun);Require(clock==s.AnimationTime,"Pause freezes waves");
                }
                if(waves)
                {
                    typeof(WeatherState).GetProperty("Kind").SetValue(g.Weather.State,WeatherKind.Sunny);
                    AtmospherePolishVerification.Advance(g.Weather,40);LightingFlowVerification.SetTime(g,750);
                    s.Tick(0,s.Daylight,false,g.Weather.Sun);
                    for(int frame=0;frame<24;frame++)
                    {
                        s.Tick(.5f,s.Daylight,false,g.Weather.Sun);
                        Shot(camera,folder+"/WaveDeep"+frame.ToString("00"),new Vector3(85,s.SeaLevel,-75),14);
                        Shot(camera,folder+"/WaveCoast"+frame.ToString("00"),new Vector3(34,s.SeaLevel,-20),14);
                    }
                    var a=File.ReadAllBytes(folder+"/WaveDeep00.png");var b=File.ReadAllBytes(folder+"/WaveDeep23.png");
                    Require(!a.SequenceEqual(b),"Travelling waves change the actual rendered deep sea");
                    float clock=s.AnimationTime;s.Tick(0,s.Daylight,false,g.Weather.Sun);
                    Shot(camera,folder+"/WaveDeepPaused",new Vector3(85,s.SeaLevel,-75),14);
                    Require(clock==s.AnimationTime,"Wave sequence pauses on the shared clock");
                }
                if(surf)
                {
                    typeof(WeatherState).GetProperty("Kind").SetValue(g.Weather.State,WeatherKind.Sunny);
                    AtmospherePolishVerification.Advance(g.Weather,40);LightingFlowVerification.SetTime(g,750);
                    s.Tick(0,s.Daylight,false,g.Weather.Sun);
                    for(int frame=0;frame<48;frame++)
                    {
                        s.Tick(.25f,s.Daylight,false,g.Weather.Sun);
                        Shot(camera,folder+"/SurfEast"+frame.ToString("00"),Beach(s,-.5f),3.8f);
                        Shot(camera,folder+"/SurfSouth"+frame.ToString("00"),Beach(s,-1.9f),3.8f);
                        if(frame%16==0)NoFoamShot(camera,s.Water,folder+"/SurfEastNoFoam"+frame.ToString("00"),Beach(s,-.5f),3.8f);
                    }
                    float clock=s.AnimationTime;s.Tick(0,s.Daylight,false,g.Weather.Sun);
                    Shot(camera,folder+"/SurfEastPaused",Beach(s,-.5f),3.8f);
                    Require(clock==s.AnimationTime,"Breaking surf uses the shared paused clock");
                    Shot(camera,folder+"/SurfDeepPaused",new Vector3(85,s.SeaLevel,-75),14);
                    NoFoamShot(camera,s.Water,folder+"/SurfEastNoFoam",Beach(s,-.5f),3.8f);
                    NoFoamShot(camera,s.Water,folder+"/SurfDeepNoFoam",new Vector3(85,s.SeaLevel,-75),14);
                }
                Require(original==EditorJsonUtility.ToJson(material),"Runtime preserves shared material");
                for(int i=0;i<groundMaterials.Length;i++)Require(groundOriginals[i]==EditorJsonUtility.ToJson(groundMaterials[i]),"Runtime and MPB controls preserve shared ground materials");
            }
            finally
            {
                typeof(WeatherState).GetProperty("Kind").SetValue(g.Weather.State,kind);
                AtmospherePolishVerification.Advance(g.Weather,40);LightingFlowVerification.SetTime(g,minute);
                camera.transform.position=position;camera.orthographicSize=size;controller.enabled=follow;g.Cameras.SetOcclusionEnabled(true);
                for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i];for(int i=0;i<clouds.Length;i++)clouds[i].enabled=cloudEnabled[i];
            }
        }
        static Vector3 Beach(OceanSurface surface,float angle)
        {
            float radius=OceanSetup.Radius(angle)+.8f;
            return new Vector3(Mathf.Cos(angle)*radius,surface.SeaLevel,Mathf.Sin(angle)*radius);
        }
        static void DesertShot(Camera camera,string path,float x,float z,float size)
            =>Shot(camera,path,new Vector3(x,(float)MagicIsland.Interaction.StarterIslandLayout.Ground(x,z),z),size);
        static void NoFoamShot(Camera camera,Renderer water,string path,Vector3 focus,float size)
        {
            var original=new MaterialPropertyBlock();water.GetPropertyBlock(original);
            var control=new MaterialPropertyBlock();water.GetPropertyBlock(control);control.SetFloat("_FoamStrength",0);
            try{water.SetPropertyBlock(control);Shot(camera,path,focus,size);}
            finally{water.SetPropertyBlock(original);}
        }
        static void WetSandControl(Camera camera,GameManager game,string path,Vector3 focus,float size)
        {
            var renderers=game.Terrain.Terrain.GetComponentsInChildren<Renderer>(true).Concat(new[]{game.Ocean.Surface.Shore})
                .Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.HasProperty("_WetSandStrength")).ToArray();
            var originals=renderers.Select(r=>{var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);return block;}).ToArray();
            try
            {
                foreach(var r in renderers){var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);block.SetFloat("_WetSandStrength",0);r.SetPropertyBlock(block);}
                Shot(camera,path,focus,size);
            }
            finally{for(int i=0;i<renderers.Length;i++)renderers[i].SetPropertyBlock(originals[i]);}
        }
        static void Shot(Camera camera,string path,Vector3 focus,float size)
        {
            camera.orthographicSize=size;camera.transform.position=focus-camera.transform.forward*Mathf.Max(50,size*3);
            var target=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(path+".png",texture.EncodeToPNG());}
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
    }
}
