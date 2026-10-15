using UnityEngine;

namespace MagicIsland.Ocean
{
    public sealed class OceanSurface : MonoBehaviour
    {
        public Renderer Water;
        public Renderer Shore;
        public float SeaLevel = -.22f;
        [HideInInspector] public int CoastArtVersion;
        [HideInInspector] public int WaterArtVersion;
        [HideInInspector] public int BiomeArtVersion;
        [HideInInspector] public int EffectsArtVersion;
        public OceanSwimObstacle[] SwimObstacles=System.Array.Empty<OceanSwimObstacle>();
        public OceanInteractionEffects Effects;
        public int GeographySeed=73192;
        [Header("Fish population ranges (rebake after editing)")]
        public OceanFishPopulation[] FishPopulations={
            new OceanFishPopulation(ShallowFishSpecies.SilverMinnow,6,10),
            new OceanFishPopulation(ShallowFishSpecies.Clownfish,3,5),
            new OceanFishPopulation(ShallowFishSpecies.Butterflyfish,3,5),
            new OceanFishPopulation(ShallowFishSpecies.SpottedRay,1,2)};
        public Renderer[] EcosystemRenderers=System.Array.Empty<Renderer>();
        public OceanSchool[] Schools=System.Array.Empty<OceanSchool>();
        public CoastalBird[] Birds=System.Array.Empty<CoastalBird>();
        public CoastalCrab[] Crabs=System.Array.Empty<CoastalCrab>();
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock ecologyProperties;
        public float AnimationTime { get; private set; }
        public float Daylight { get; private set; }
        public float Rain { get; private set; }
        public int SplashCount { get; private set; }
        public void DisturbWater(Vector3 worldPoint)
        {
            if(float.IsNaN(worldPoint.x)||float.IsNaN(worldPoint.y)||float.IsNaN(worldPoint.z)||float.IsInfinity(worldPoint.x)||float.IsInfinity(worldPoint.y)||float.IsInfinity(worldPoint.z))return;
            SplashCount++;var point=transform.InverseTransformPoint(worldPoint);
            foreach(var school in Schools)if(school!=null)school.Startle(point);
            if(Effects!=null)Effects.Splash(worldPoint,this);
        }
        public float HeightAt(Vector3 world)
        {
            var material=Water.sharedMaterial;float t=AnimationTime*material.GetFloat("_WaveSpeed"),height=material.GetFloat("_WaveHeight");
            return Water.transform.position.y+height*(Mathf.Sin(world.x*.85f+world.z*.32f+t)+.5f*Mathf.Sin(world.z*1.28f-world.x*.27f-t*.71f));
        }
        public bool IsValid => Water != null && Shore != null && Water.sharedMaterial != null
            && Water.sharedMaterial.shader != null && Water.sharedMaterial.shader.name == "MagicIsland/Island Ocean"
            && Water.GetComponent<MeshFilter>()?.sharedMesh != null && Shore.GetComponent<MeshFilter>()?.sharedMesh != null
            && Effects!=null&&Effects.IsValid&&GetComponentsInChildren<Collider>(true).Length == 0;
        public void Tick(float seconds, float daylight, bool raining, Light sunlight = null,Vector3? playerPosition=null,Vector3? fishingPosition=null)
        {
            AnimationTime += Mathf.Max(0, seconds); Daylight = Mathf.Clamp01(daylight); Rain = raining ? 1 : 0;
            if (properties == null) properties = new MaterialPropertyBlock();
            Water.GetPropertyBlock(properties); properties.SetFloat("_OceanTime", AnimationTime);
            properties.SetFloat("_Daylight", Daylight); properties.SetFloat("_Rain", Rain);
            if (sunlight != null)
            {
                properties.SetVector("_SunDirection", -sunlight.transform.forward);
                properties.SetColor("_SunColor", sunlight.color); properties.SetFloat("_SunIntensity", sunlight.intensity);
            }
            Water.SetPropertyBlock(properties);
            if(ecologyProperties==null)ecologyProperties=new MaterialPropertyBlock();
            foreach(var renderer in EcosystemRenderers)if(renderer!=null){renderer.GetPropertyBlock(ecologyProperties);ecologyProperties.SetFloat("_OceanTime",AnimationTime);renderer.SetPropertyBlock(ecologyProperties);}
            Vector3? player=playerPosition.HasValue?(Vector3?)transform.InverseTransformPoint(playerPosition.Value):null;
            Vector3? fishing=fishingPosition.HasValue?(Vector3?)transform.InverseTransformPoint(fishingPosition.Value):null;
            foreach(var school in Schools)if(school!=null){school.React(seconds,fishing);school.Render(AnimationTime);}
            foreach(var bird in Birds)if(bird!=null){bird.React(seconds,player,AnimationTime,Daylight,raining);bird.Render(AnimationTime,Daylight,raining);}
            foreach(var crab in Crabs)if(crab!=null){crab.React(seconds,player);crab.Render(AnimationTime);}
            if(Effects!=null)Effects.Tick(seconds,this,playerPosition);
        }
    }
}
