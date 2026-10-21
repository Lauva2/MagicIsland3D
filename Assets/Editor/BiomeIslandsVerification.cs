using System;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class BiomeIslandsVerification
    {
        const string Key="MagicIsland.BiomeIslandsVerification";
        static BiomeIslandsVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        static void Check(bool b,string why){if(!b)throw new InvalidOperationException("Biome islands: "+why);}
        public static void BakeAndVerify(){BiomeIslandsSetup.Rebuild();VerifyAssets();}
        public static void VerifyAssets()
        {
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.World.ArtVersion==3,"AA prefab and references");
                Check(t.World.Waters.Length==0,"no phantom lake or river");
                Check(t.World.transform.Find("Village boardwalk")==null&&t.World.transform.Find("Outlet bridge")==null,"no old bridge objects");
                Physics.SyncTransforms();
                foreach(var biome in BiomeIslandLayout.Biomes)
                {
                    BiomeIslandLayout.Landing(biome,out double x,out double z);
                    Check(t.TryGround(new Vector3((float)x,0,(float)z),out _),"real ground "+biome);
                }
                for(int z=26;z<145;z+=2)Check(!IslandLayout.ContainsLand(0,z),"old connection removed "+z);
                Check(!IslandLayout.ContainsLand(-285,590)&&!IslandLayout.ContainsLand(270,650)&&!IslandLayout.ContainsLand(0,820),"separate sea channels");
                foreach(var ground in t.World.Grounds)
                {
                    var mesh=ground.sharedMesh;Check(mesh==ground.GetComponent<MeshFilter>().sharedMesh,"visible and collision geometry agree");
                    Check(mesh.vertices.All(p=>!float.IsNaN(p.y)&&!float.IsInfinity(p.y)),"finite ground");
                }
                Check(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeIslandsSetup.Source)!=null,"original authored prefab preserved outside AA");
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(BiomeIslandsSetup.Source).GetComponent<IslandTerrain>();
                Check(source.World.ArtVersion==2,"source stays mainland art");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var bytes=File.ReadAllBytes(TerrainSetup.PrefabPath);BiomeIslandsSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            Check(bytes.SequenceEqual(File.ReadAllBytes(TerrainSetup.PrefabPath)),"repeat/ordinary Setup preserve art");
            Debug.Log("Biome islands asset verification PASSED.");
        }
        public static void Run()
        {
            Check(Application.persistentDataPath.Contains("MagicIslandValidation"),"use isolated validation project");
            UiPrefabMigration.EnsureChineseFont(UiPrefabMigration.FontAsset);AssetDatabase.SaveAssets();
            VerifyAssets();Directory.CreateDirectory("Logs/BiomeIslands");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();
            var settings=AddressableAssetSettingsDefaultObject.Settings;settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".Phase",0);SessionState.SetInt(Key+".Island",0);
            SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Phase(int phase){SessionState.SetInt(Key+".Phase",phase);Debug.Log("Biome islands phase "+phase);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int phase=SessionState.GetInt(Key+".Phase",0);
            if(phase==11&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            try
            {
                Check(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)<420,"timeout phase "+phase);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Check(g.State!=GameFlowState.Failed,"initialization");
                if(phase==0&&g.State==GameFlowState.MainMenu)
                {
                    string dir=Path.GetFullPath("Logs/BiomeIslands/Saves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
                    g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals,g.Configuration.Orchards);
                    g.Settings.Initialize(dir+"/Settings");g.EnterGame();Phase(1);
                }
                else if(phase==1&&g.State==GameFlowState.Playing)
                {
                    Check(!File.Exists(g.Saves.SavePath),"new game does not save");
                    VerifyTravel(g);GrasslandIslandCoastVerification.Visit(g);GrasslandIslandMeadowVerification.Visit(g);GrasslandGroveVerification.Visit(g);GrasslandFloraVerification.Visit(g);GrasslandGroundLayersVerification.Visit(g);GrasslandShrubStoneVerification.Visit(g);GrasslandCrownVerification.Visit(g);GrasslandCrownWindVerification.Visit(g);GrasslandCrownOcclusionVerification.Visit(g);GrasslandTouchVerification.Visit(g);GrasslandReadabilityVerification.Visit(g);GrasslandPatchVerification.Visit(g);GrasslandBlendVerification.Visit(g);GrasslandWildflowerVerification.Visit(g);GrasslandEdgeVerification.Visit(g);GrasslandPathEdgeVerification.Visit(g);GrasslandSampleFinishVerification.Visit(g);GrasslandSpatialVerification.Visit(g);GrasslandShrubDetailVerification.Visit(g);GrasslandShrubWindVerification.Visit(g);GrasslandFlowerVisitVerification.Visit(g);GrasslandGroundDetailVerification.Visit(g);GrasslandBladeDetailVerification.Visit(g);GrasslandFlowerDetailVerification.Visit(g);
                    if(GrasslandIslandMeadowVerification.BeginPlacement(g))Phase(12);
                    else{g.ToggleMap();Check(!g.TryTestBiomeTravel(IslandBiome.Desert),"map loading blocks travel");Phase(2);}
                }
                else if(phase==12&&g.Placements.Ready)
                {
                    GrasslandIslandMeadowVerification.VerifyPlacement(g);g.ToggleMap();Check(!g.TryTestBiomeTravel(IslandBiome.Desert),"map loading blocks travel");Phase(2);
                }
                else if(phase==2&&Object.FindObjectOfType<MapView>()!=null)
                {
                    var view=Object.FindObjectOfType<MapView>();if(!g.Map.Model.WorldOverview)view.ScaleButton.onClick.Invoke();
                    Check(g.Map.Model.Locations.Count==9&&!g.Map.Model.Locations.Any(l=>l.Id=="bridge"||l.Id=="lake"),"island map has no obsolete mainland features");
                    foreach(var entry in g.Map.Model.Locations)
                        Check((entry.Name+entry.Description).All(c=>char.IsControl(c)||view.DetailText.font.HasCharacter(c)),"map Chinese glyphs");
                    Check(!g.TryTestBiomeTravel(IslandBiome.Snow),"modal blocks travel");g.CloseMap();
                    Check(g.TryTestBiomeTravel(IslandBiome.Beach),"return to village");
                    g.Players.RestorePosition(g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome();Phase(3);
                }
                else if(phase==3&&g.State==GameFlowState.Playing&&g.Housing.Inside)
                {
                    Check(!g.TryTestBiomeTravel(IslandBiome.Grassland),"indoor travel blocked");GrasslandCrownWindVerification.Inside(g);GrasslandCrownOcclusionVerification.Inside(g);GrasslandTouchVerification.Inside(g);GrasslandShrubWindVerification.Inside(g);
                    g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin+new Vector3(-1.5f,.1f,2));g.RequestRest();Phase(4);
                }
                else if(phase==4&&Object.FindObjectOfType<SleepView>()!=null){Object.FindObjectOfType<SleepView>().ConfirmButton.onClick.Invoke();Phase(5);}
                else if(phase==5&&g.UI.HasDailyReport&&!g.Saves.SleepSavePending)
                {Check(g.Saves.TryLoad(out var save)&&save.Day==2,"bed sleep saves");g.StartNewDayFromReport();g.ReturnToMainMenu();Phase(6);}
                else if(phase==6&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(7);}
                else if(phase==7&&g.State==GameFlowState.MainMenu)
                {
                    Check(!g.Terrain.OwnsTerrain&&!g.Ocean.OwnsOcean&&Object.FindObjectsOfType<StarterIslandWorld>(true).Length==0,"AA cleanup");GrasslandCrownWindVerification.Released(g);GrasslandCrownOcclusionVerification.Released(g);GrasslandTouchVerification.Released(g);
                    int index=SessionState.GetInt(Key+".Island",0);var biome=BiomeIslandLayout.Biomes[index];
                    if(index==0)VerifyLegacyRejection(g);
                    Check(g.Saves.TryLoad(out var save),"sleep fixture");BiomeIslandLayout.Landing(biome,out double x,out double z);
                    save.PlayerArea=HousingState.Island;save.PlayerX=(float)x;save.PlayerZ=(float)z;save.PlayerY=(float)IslandLayout.Height(x,z)+.05f;
                    File.WriteAllText(g.Saves.SavePath,JsonUtility.ToJson(save,true));SessionState.SetString(Key+".Save",File.ReadAllText(g.Saves.SavePath));
                    g.Saves.RefreshAvailability();g.ContinueGame();Phase(8);
                }
                else if(phase==8&&g.State==GameFlowState.Playing)
                {
                    var biome=BiomeIslandLayout.Biomes[SessionState.GetInt(Key+".Island",0)];BiomeIslandLayout.Landing(biome,out double x,out double z);
                    var p=g.Players.Player.transform.position;Check(Vector2.Distance(new Vector2(p.x,p.z),new Vector2((float)x,(float)z))<.1f,"restore island "+biome);
                    Check(File.ReadAllText(g.Saves.SavePath)==SessionState.GetString(Key+".Save",""),"continue does not write");g.ReturnToMainMenu();Phase(9);
                }
                else if(phase==9&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(10);}
                else if(phase==10&&g.State==GameFlowState.MainMenu)
                {
                    int next=SessionState.GetInt(Key+".Island",0)+1;
                    if(next<BiomeIslandLayout.Biomes.Length){SessionState.SetInt(Key+".Island",next);Phase(7);}
                    else{Check(!g.Terrain.OwnsTerrain&&!g.Ocean.OwnsOcean,"final release");Debug.Log("Biome islands Packed flow verification PASSED: five safe teleports, modal/indoor/blocked target, map, bed sleep, five continues, cleanup, no daytime writes.");Phase(11);EditorApplication.isPlaying=false;}
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
        static void VerifyTravel(GameManager g)
        {
            var camera=g.Players.Player.ViewCamera;float far=camera.farClipPlane;camera.farClipPlane=2200;
            foreach(var biome in BiomeIslandLayout.Biomes)
            {
                Check(g.TryTestBiomeTravel(biome),"travel "+biome);var p=g.Players.Player.transform.position;
                Check(g.Terrain.Contains(p,.4f)&&BiomeIslandLayout.At(p.x,p.z)==biome,"correct dry island "+biome);
                Check(g.Interactions.Current==InteractionTarget.None,"stale target cleared");
                g.Weather.Tick(720,true);
                Capture(camera,"Logs/BiomeIslands/"+biome+".png");
            }
            camera.farClipPlane=far;
            var beforePosition=camera.transform.position;var beforeRotation=camera.transform.rotation;float beforeSize=camera.orthographicSize;
            try
            {
                camera.transform.rotation=Quaternion.Euler(70,0,0);camera.transform.position=new Vector3(-30,0,610)-camera.transform.forward*1800;
                camera.orthographicSize=700;camera.farClipPlane=3200;g.Weather.Tick(720,true);
                Capture(camera,"Logs/BiomeIslands/Overview.png");
            }
            finally{camera.transform.SetPositionAndRotation(beforePosition,beforeRotation);camera.orthographicSize=beforeSize;camera.farClipPlane=far;}
            // Saturate the entire search area with a real obstacle; failure must leave position unchanged.
            var wall=new GameObject("Blocked teleport fixture",typeof(BoxCollider));
            try
            {
                BiomeIslandLayout.Landing(IslandBiome.Desert,out double x,out double z);wall.transform.position=new Vector3((float)x,150,(float)z);wall.GetComponent<BoxCollider>().size=new Vector3(40,400,40);Physics.SyncTransforms();
                var before=g.Players.Player.transform.position;Check(!g.TryTestBiomeTravel(IslandBiome.Desert)&&g.Players.Player.transform.position==before,"blocked target atomic failure");
            }
            finally{Object.DestroyImmediate(wall);Physics.SyncTransforms();}
            Check(!g.TryTestBiomeTravel((IslandBiome)99),"unknown biome rejected");
            Check(!g.Terrain.TryResolve(new Vector3(0,0,80),out _),"no invisible boardwalk collider");
            Check(!File.Exists(g.Saves.SavePath),"travel has no disk writes");
        }
        static void VerifyLegacyRejection(GameManager g)
        {
            string path=g.Saves.SavePath,backup=path+".bak";byte[] original=File.ReadAllBytes(path),oldBackup=File.Exists(backup)?File.ReadAllBytes(backup):null;
            try
            {
                if(File.Exists(backup))File.Delete(backup);Check(g.Saves.TryLoad(out var valid),"legacy fixture base");
                valid.PlayerArea=HousingState.Island;valid.PlayerX=0;valid.PlayerZ=80;valid.PlayerY=.05f;
                string invalid=JsonUtility.ToJson(valid,true);File.WriteAllText(path,invalid);
                Check(!g.Saves.TryLoad(out _)&&g.Saves.LastError.Contains("群岛")&&File.ReadAllText(path)==invalid,"removed old player position rejected without rewrite");
                File.WriteAllBytes(path,original);Check(g.Saves.TryLoad(out valid),"restore fixture");
                Check(StarterIslandLayout.CanPlace(100,200)&&!IslandLayout.ContainsLand(100,200),"old legal construction now in removed land");
                valid.PlacedObjects=new[]{new SavedPlacedObject{Id=1,ItemId=g.Configuration.Crafting.Placeables().First().ItemId,GridX=100,GridZ=200}};
                invalid=JsonUtility.ToJson(valid,true);File.WriteAllText(path,invalid);
                Check(!g.Saves.TryLoad(out _)&&g.Saves.LastError.Contains("建筑")&&File.ReadAllText(path)==invalid,"removed old construction rejected as whole checkpoint");
                File.WriteAllBytes(backup,original);Check(g.Saves.TryLoad(out _)&&g.Saves.RecoveredBackup&&File.ReadAllText(path)==invalid,"valid backup fallback preserves invalid primary");
            }
            finally{File.WriteAllBytes(path,original);if(oldBackup==null){if(File.Exists(backup))File.Delete(backup);}else File.WriteAllBytes(backup,oldBackup);g.Saves.RefreshAvailability();}
            Debug.Log("Biome islands legacy rejection / backup preservation PASSED.");
        }
        static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(960,600,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(960,600,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
