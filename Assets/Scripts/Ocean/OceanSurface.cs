using UnityEngine;
using System.Linq;
using Unity.Profiling;

namespace MagicIsland.Ocean
{
    public sealed class OceanSurface : MonoBehaviour
    {
        public Renderer Water;
        public Renderer Shore;
        [System.NonSerialized] public Renderer[] WorldWaters=System.Array.Empty<Renderer>();
        [System.NonSerialized] public WorldWaterEcology WorldEcology;
        public float SeaLevel = -.22f;
        [HideInInspector] public int CoastArtVersion;
        [HideInInspector] public int WaterArtVersion;
        [HideInInspector] public int BiomeArtVersion;
        [HideInInspector] public int EffectsArtVersion;
        public OceanSwimObstacle[] SwimObstacles=System.Array.Empty<OceanSwimObstacle>();
        public OceanInteractionEffects Effects;
        public int GeographySeed=73192;
        [Min(1)] public int FishSeed=90421;
        public OceanFishAnimation FishAnimation=new OceanFishAnimation();
        public CoastalWildlifePopulation CoastalPopulation=new CoastalWildlifePopulation();
        public CoastalEcologyRhythm CoastalRhythm=new CoastalEcologyRhythm();
        [Header("Fish population ranges (rebake after editing)")]
        public OceanFishPopulation[] FishPopulations={
            new OceanFishPopulation(ShallowFishSpecies.SilverMinnow,6,10),
            new OceanFishPopulation(ShallowFishSpecies.Clownfish,3,5),
            new OceanFishPopulation(ShallowFishSpecies.Butterflyfish,3,5),
            new OceanFishPopulation(ShallowFishSpecies.SpottedRay,1,2,4,6)};
        public Renderer[] EcosystemRenderers=System.Array.Empty<Renderer>();
        public OceanSchool[] Schools=System.Array.Empty<OceanSchool>();
        public CoastalBird[] Birds=System.Array.Empty<CoastalBird>();
        public CoastalCrab[] Crabs=System.Array.Empty<CoastalCrab>();
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock ecologyProperties;
        private OceanFishWaterVolume fishWaterVolume;
        static readonly ProfilerMarker simulationMarker=new ProfilerMarker("MagicIsland.Ocean.Simulation");
        static readonly ProfilerMarker presentationMarker=new ProfilerMarker("MagicIsland.Ocean.Presentation");
        readonly Plane[] presentationPlanes=new Plane[6];
        Camera presentationCamera;
        Renderer[] ambientRenderers;
        // Nonpersistent diagnostic switch; normal gameplay always uses visibility filtering.
        [System.NonSerialized] public bool CullFishPresentation=true;
        public int LastFishSubmissions { get; private set; }
        public int LastVisibleSchools { get; private set; }
        CoastalWildlifeNavigation coastalNavigation;
        public void BindCoastalNavigation(CoastalWildlifeNavigation navigation)
        {
            coastalNavigation=navigation;
            for(int i=0;i<Birds.Length;i++)if(Birds[i]!=null)Birds[i].BindNavigation(navigation,i);
            foreach(var crab in Crabs)if(crab!=null)crab.Navigation=navigation;
        }
        public void BindPresentationCamera(Camera camera)=>presentationCamera=camera;
        public void PreparePresentation(Camera camera)
        {
            if (camera == null || camera != presentationCamera || !isActiveAndEnabled) return;
            using(presentationMarker.Auto())
            {
                GeometryUtility.CalculateFrustumPlanes(camera, presentationPlanes);
                LastFishSubmissions=0;
                LastVisibleSchools = 0;
                foreach(var school in Schools)if(school!=null&&school.gameObject.activeInHierarchy)
                {
                    bool visible = (camera.cullingMask & (1 << school.gameObject.layer)) != 0 &&
                                   GeometryUtility.TestPlanesAABB(presentationPlanes, school.PresentationBounds);
                    if (visible) LastVisibleSchools++;
                    if ((visible || !CullFishPresentation) && school.PresentLatest()) LastFishSubmissions++;
                }
                WorldEcology?.Present(camera);
            }
        }
        public void ConfigureFishHabitat(OceanSchool school)
        {
            school.Animation = FishAnimation;
            if (school.Anatomy == null)
                school.Anatomy = OceanFishAnatomy.Read(school.GetComponent<MeshFilter>().sharedMesh, school.Swimmers);
            if(fishWaterVolume==null)
            {
                var mesh = Shore.GetComponent<MeshFilter>().sharedMesh;
                fishWaterVolume = new OceanFishWaterVolume(mesh.vertices, mesh.triangles);
            }

            float waves = Water.sharedMaterial.GetFloat("_WaveHeight") * 1.5f;
            float floor = fishWaterVolume.HighestFloor(school.Center, school.HomeRadius);
            if (float.IsNaN(floor) || float.IsInfinity(floor))
                throw new System.InvalidOperationException("Fish habitat lies outside the baked ocean floor");
            school.ConfigureWater(floor + .07f, SeaLevel - Mathf.Max(.10f, waves + .025f), SwimObstacles);
        }
        public float AnimationTime { get; private set; }
        public float Daylight { get; private set; }
        public float Rain { get; private set; }
        public int SplashCount { get; private set; }
        public void DisturbWater(Vector3 worldPoint)
        {
            if(float.IsNaN(worldPoint.x)||float.IsNaN(worldPoint.y)||float.IsNaN(worldPoint.z)||float.IsInfinity(worldPoint.x)||float.IsInfinity(worldPoint.y)||float.IsInfinity(worldPoint.z))return;
            SplashCount++;var point=transform.InverseTransformPoint(worldPoint);
            foreach(var school in Schools)if(school!=null)school.Startle(point);
            WorldEcology?.Disturb(worldPoint);
            if(Effects!=null)Effects.Splash(worldPoint,this);
        }
        public float HeightAt(Vector3 world)
        {
            if(WorldEcology!=null&&WorldEcology.TryLakeHeight(world,AnimationTime,out float lakeHeight))return lakeHeight;
            var material=Water.sharedMaterial;float t=AnimationTime*material.GetFloat("_WaveSpeed"),height=material.GetFloat("_WaveHeight");
            return Water.transform.position.y+height*(Mathf.Sin(world.x*.85f+world.z*.32f+t)+.5f*Mathf.Sin(world.z*1.28f-world.x*.27f-t*.71f));
        }
        public bool IsValid => Water != null && Shore != null && Water.sharedMaterial != null
            &&FishSeed>0&&FishPopulations!=null&&FishPopulations.Length==4&&FishPopulations.All(p=>p!=null&&p.IsValid)&&FishPopulations.Select(p=>p.Species).Distinct().Count()==4
            &&FishAnimation!=null&&FishAnimation.IsValid&&CoastalRhythm!=null&&CoastalRhythm.IsValid
            &&CoastalPopulation!=null&&CoastalPopulation.IsValid
            &&Birds!=null&&Crabs!=null&&Birds.Length>=CoastalPopulation.MinimumBirds&&Birds.Length<=CoastalPopulation.MaximumBirds
            &&Crabs.Length>=CoastalPopulation.MinimumCrabs&&Crabs.Length<=CoastalPopulation.MaximumCrabs&&Birds.All(b=>b!=null&&b.Habits!=null&&b.Habits.IsValid)&&Crabs.All(c=>c!=null&&c.Habits!=null&&c.Habits.IsValid)
            && Water.sharedMaterial.shader != null && Water.sharedMaterial.shader.name == "MagicIsland/Island Ocean"
            && Water.GetComponent<MeshFilter>()?.sharedMesh != null && Shore.GetComponent<MeshFilter>()?.sharedMesh != null
            && Effects!=null&&Effects.IsValid&&GetComponentsInChildren<Collider>(true).Length == 0;
        public void Tick(float seconds, float daylight, bool raining, Light sunlight = null,Vector3? playerPosition=null,Vector3? fishingPosition=null,float twilight=0,float? rainAmount=null)
        {
            using(simulationMarker.Auto())
            {
            AnimationTime += Mathf.Max(0, seconds); Daylight = Mathf.Clamp01(daylight); Rain = Mathf.Clamp01(rainAmount ?? (raining ? 1 : 0));
            if (properties == null) properties = new MaterialPropertyBlock();
            Water.GetPropertyBlock(properties); properties.SetFloat("_OceanTime", AnimationTime);
            properties.SetFloat("_Daylight", Daylight); properties.SetFloat("_Rain", Rain);
            if (sunlight != null)
            {
                properties.SetVector("_SunDirection", -sunlight.transform.forward);
                properties.SetColor("_SunColor", sunlight.color); properties.SetFloat("_SunIntensity", sunlight.intensity);
            }
            Water.SetPropertyBlock(properties);
            foreach(var water in WorldWaters)if(water!=null)water.SetPropertyBlock(properties);
            if(ecologyProperties==null)ecologyProperties=new MaterialPropertyBlock();
            if(ambientRenderers==null)ambientRenderers=EcosystemRenderers.Where(r=>r!=null&&r.GetComponent<OceanSchool>()==null).ToArray();
            foreach(var renderer in ambientRenderers)if(renderer!=null){renderer.GetPropertyBlock(ecologyProperties);ecologyProperties.SetFloat("_OceanTime",AnimationTime);renderer.SetPropertyBlock(ecologyProperties);}
            Vector3? player=playerPosition.HasValue?(Vector3?)transform.InverseTransformPoint(playerPosition.Value):null;
            Vector3? fishing=fishingPosition.HasValue?(Vector3?)transform.InverseTransformPoint(fishingPosition.Value):null;
            if(seconds>0)coastalNavigation?.SetPlayer(player);
            foreach(var school in Schools)if(school!=null)
            {
                school.SetEnvironment(Daylight,raining,twilight);
                if(float.IsInfinity(school.WaterFloor)){ConfigureFishHabitat(school);school.ResetSteering();}
                school.React(seconds,fishing,player);school.Render(AnimationTime,presentationCamera==null);
            }
            foreach(var bird in Birds)if(bird!=null){bird.SetEnvironment(CoastalRhythm,Daylight,raining,twilight);bird.React(seconds,player,AnimationTime,Daylight,raining);bird.Render(AnimationTime,Daylight,raining);}
            foreach(var crab in Crabs)if(crab!=null){crab.SetEnvironment(CoastalRhythm,Daylight,raining);crab.React(seconds,player);crab.Render(AnimationTime);}
            if(Effects!=null)Effects.Tick(seconds,this,playerPosition);
            WorldEcology?.Tick(AnimationTime,seconds,Daylight,raining,twilight,playerPosition,fishingPosition,presentationCamera==null,true);
            }
        }
    }
}
