using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    // IslandTerrain owns these baked dependencies. OceanSurface supplies the only animation clock.
    // Bind after terrain and ocean load; clear before either AA owner is released.
    public sealed class WorldWaterEcology : MonoBehaviour
    {
        [Serializable] public sealed class Habitat
        {
            public OceanSchool School;
            public MeshFilter Floor;
            public float WaterLevel;
            public bool Freshwater;
        }
        public int ArtVersion;
        public Habitat[] Habitats=Array.Empty<Habitat>();
        public Renderer[] Ambient=Array.Empty<Renderer>();
        // Optional baked grassland batches; owned by IslandTerrain, driven by the same paused clock.
        public Renderer[] GrasslandAmbient=Array.Empty<Renderer>();
        public Renderer MeadowRabbits;
        public MeadowRabbitHabitat[] RabbitHabitats=Array.Empty<MeadowRabbitHabitat>();
        public Renderer AncientTree;
        public Renderer LakeSurface;
        public bool Bound { get; private set; }
        public int LastFishSubmissions { get; private set; }
        MaterialPropertyBlock properties;
        MaterialPropertyBlock[] grasslandOriginals;
        MeadowInsectMotion meadowMotion;
        MeadowRabbitPopulation rabbitPopulation;
        float meadowRain;
        readonly Plane[] planes=new Plane[6];
        public bool IsValid
        {
            get
            {
                if((ArtVersion!=1&&ArtVersion!=2)||Habitats==null||Ambient==null||AncientTree==null)return false;
                if(ArtVersion==1&&(Habitats.Length==0||LakeSurface==null))return false;
                if(ArtVersion==2&&(Habitats.Length!=0||LakeSurface!=null))return false;
                foreach(var h in Habitats)
                    if(h==null||h.School==null||h.Floor==null||h.Floor.sharedMesh==null||h.School.Swimmers.Length!=h.School.Members
                        ||h.School.transform.parent!=transform||h.Floor.transform.parent!=transform||!Finite(h.WaterLevel))return false;
                foreach(var r in Ambient)if(r==null||r.sharedMaterial==null)return false;
                if(GrasslandAmbient==null)return false;
                foreach(var r in GrasslandAmbient)if(r==null||r.sharedMaterial==null||r.GetComponent<MeshFilter>()?.sharedMesh==null)return false;
                if(RabbitHabitats==null||RabbitHabitats.Length>8||(MeadowRabbits==null)!=(RabbitHabitats.Length==0))return false;
                foreach(var habitat in RabbitHabitats)if(habitat==null||!habitat.IsValid)return false;
                if(MeadowRabbits!=null&&(MeadowRabbits.sharedMaterial==null||MeadowRabbits.GetComponent<MeshFilter>()?.sharedMesh==null))return false;
                return GetComponentsInChildren<Collider>(true).Length==0;
            }
        }
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        public void Bind(OceanFishAnimation animation,float light=1,bool raining=false,float twilight=0)
        {
            Clear();
            if(!IsValid||animation==null||!animation.IsValid)throw new InvalidOperationException("Incomplete baked world water ecology");
            grasslandOriginals=new MaterialPropertyBlock[GrasslandAmbient.Length];meadowRain=raining?1:0;
            for(int i=0;i<GrasslandAmbient.Length;i++){grasslandOriginals[i]=new MaterialPropertyBlock();GrasslandAmbient[i].GetPropertyBlock(grasslandOriginals[i]);}
            meadowMotion=new MeadowInsectMotion(GrasslandAmbient);
            foreach(var h in Habitats)
            {
                var s=h.School;var mesh=h.Floor.sharedMesh;
                var floor=new OceanFishWaterVolume(mesh.vertices,mesh.triangles);
                float highest=floor.HighestFloor(s.Center,s.HomeRadius);
                if(!Finite(highest))throw new InvalidOperationException("World fish outside its baked floor");
                s.Animation=animation;s.SetEnvironment(light,raining,twilight);s.Anatomy=OceanFishAnatomy.Read(s.GetComponent<MeshFilter>().sharedMesh,s.Swimmers);
                s.ConfigureWater(highest+.07f,h.WaterLevel-.16f,s.Obstacles);s.ResetSteering();s.Render(0);
            }
            if(MeadowRabbits!=null)rabbitPopulation=new MeadowRabbitPopulation(MeadowRabbits,RabbitHabitats,transform,GetComponentInParent<MagicIsland.TerrainSystem.IslandTerrain>());
            Bound=true;
            SetGrassland(0,light);
        }
        public void Tick(float time,float seconds,float light,bool raining,float twilight,Vector3? player,Vector3? fishing,bool submit,bool outside)
        {
            if(!Bound)return;
            if(gameObject.activeSelf!=outside)gameObject.SetActive(outside);
            // The tree is outside this ecology subtree and follows the same indoor visibility rule.
            if(AncientTree!=null)AncientTree.enabled=outside;
            if(!outside)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            meadowRain=Mathf.MoveTowards(meadowRain,raining?1:0,Mathf.Max(0,seconds)*.35f);
            meadowMotion?.Tick(seconds,player);
            rabbitPopulation?.Tick(seconds,light,raining,player);
            SetGrassland(time,light);
            foreach(var r in Ambient)SetTime(r,time,light);
            SetTime(AncientTree,time,light);
            Vector3? p=player.HasValue?(Vector3?)transform.InverseTransformPoint(player.Value):null;
            Vector3? f=fishing.HasValue?(Vector3?)transform.InverseTransformPoint(fishing.Value):null;
            foreach(var h in Habitats)
            {var s=h.School;s.SetEnvironment(light,raining,twilight);s.React(seconds,f,p);s.Render(time,submit);}
        }
        void SetTime(Renderer r,float time,float light)
        {r.GetPropertyBlock(properties);properties.SetFloat("_OceanTime",time);properties.SetFloat("_Daylight",light);r.SetPropertyBlock(properties);}
        void SetGrassland(float time,float light)
        {
            if(properties==null)properties=new MaterialPropertyBlock();
            for(int i=0;i<GrasslandAmbient.Length;i++)
            {var r=GrasslandAmbient[i];r.GetPropertyBlock(properties);properties.SetFloat("_OceanTime",time);properties.SetFloat("_Daylight",Mathf.Clamp01(light));properties.SetFloat("_MeadowRain",meadowRain);meadowMotion?.Apply(i,properties);r.SetPropertyBlock(properties);}
        }
        public void Present(Camera camera)
        {
            LastFishSubmissions=0;if(!Bound||!gameObject.activeInHierarchy||camera==null)return;
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            foreach(var h in Habitats)
                if((camera.cullingMask&(1<<h.School.gameObject.layer))!=0&&GeometryUtility.TestPlanesAABB(planes,h.School.PresentationBounds)&&h.School.PresentLatest())LastFishSubmissions++;
        }
        public void Disturb(Vector3 world)
        {if(Bound)foreach(var h in Habitats)h.School.Startle(transform.InverseTransformPoint(world));}
        public bool TryLakeHeight(Vector3 world,float time,out float height)
        {
            height=0;if(!Bound||LakeSurface==null)return false;
            var p=transform.InverseTransformPoint(world);
            if(MagicIsland.Interaction.StarterIslandLayout.LakeDistance(p.x,p.z)>=0)return false;
            var m=LakeSurface.sharedMaterial;float t=time*m.GetFloat("_WaveSpeed"),wave=m.GetFloat("_WaveHeight");
            height=transform.TransformPoint(new Vector3(p.x,(float)MagicIsland.Interaction.StarterIslandLayout.LakeLevel,p.z)).y+wave*(Mathf.Sin(world.x*.85f+world.z*.32f+t)+.5f*Mathf.Sin(world.z*1.28f-world.x*.27f-t*.71f));return true;
        }
        public void Clear()
        {
            Bound=false;LastFishSubmissions=0;
            if(Habitats!=null)foreach(var h in Habitats)if(h?.School!=null)
            {h.School.ResetSteering();h.School.Animation=null;h.School.Anatomy=null;h.School.GetComponent<Renderer>()?.SetPropertyBlock(null);}
            if(Ambient!=null)foreach(var r in Ambient)if(r!=null)r.SetPropertyBlock(null);
            if(AncientTree!=null){AncientTree.SetPropertyBlock(null);AncientTree.enabled=true;}
            if(grasslandOriginals!=null&&GrasslandAmbient!=null)
                for(int i=0;i<Math.Min(grasslandOriginals.Length,GrasslandAmbient.Length);i++)if(GrasslandAmbient[i]!=null)GrasslandAmbient[i].SetPropertyBlock(grasslandOriginals[i]);
            grasslandOriginals=null;meadowMotion=null;meadowRain=0;
            rabbitPopulation?.Release();rabbitPopulation=null;
        }
        void OnDestroy()=>Clear();
    }
}
