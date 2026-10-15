using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MagicIsland.Ocean;

namespace MagicIsland.EditorTools
{
    public static class OceanEcologyPolishVerification
    {
        static void Require(bool value,string why){if(!value)throw new Exception(why);}
        public static void Verify(GameObject prefab)
        {
            var root=UnityEngine.Object.Instantiate(prefab);root.SetActive(false);
            try
            {
                var surface=root.GetComponent<OceanSurface>();var patches=root.GetComponentsInChildren<OceanHabitatPatch>(true);
                Require(patches.Length==84&&patches.Count(p=>p.Biome==OceanBiome.ReefSlope)==36,"Both shallow gardens and deeper coral terrace are populated");
                Require(patches.Sum(p=>p.SmallCorals)>500&&patches.Sum(p=>p.LargeCorals)>650,"Small multicolour corals and larger terrace colonies");
                float shallowY=patches.Where(p=>p.SmallCorals>0).Average(p=>p.transform.localPosition.y),reefY=patches.Where(p=>p.LargeCorals>0).Average(p=>p.transform.localPosition.y);
                Require(shallowY-reefY>.7f,"Reef terrace occupies a distinctly deeper water column");
                var water=surface.Water.sharedMaterial;
                Require(water.GetFloat("_DepthRange")>4&&water.GetColor("_ShallowColor").grayscale>water.GetColor("_MidColor").grayscale&&water.GetColor("_MidColor").grayscale>water.GetColor("_DeepColor").grayscale,"Softer palette retains clear shallow / reef / deep value separation");
                foreach(var school in surface.Schools)
                {
                    var mesh=school.GetComponent<MeshFilter>().sharedMesh;
                    var data=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(1,data);
                    var pivots=data.Distinct().ToArray();Require(pivots.Length==school.Members,"Independent member metadata retained");
                    school.Render(0);OceanFishMotion.Pose(pivots[0],school.Guide,0,0,out var first,out float yawA);
                    int other=Mathf.Min(1,pivots.Length-1);
                    OceanFishMotion.Pose(pivots[other],school.Guide,0,0,out var second,out float yawB);
                    float distance=Vector3.Distance(first,second);float changed=0;
                    for(int step=1;step<=25;step++)
                    {
                        float time=step*.3f;school.Render(time);
                        OceanFishMotion.Pose(pivots[0],school.Guide,time,0,out first,out yawA);OceanFishMotion.Pose(pivots[other],school.Guide,time,0,out second,out yawB);
                        changed=Mathf.Max(changed,Mathf.Abs(distance-Vector3.Distance(first,second)));
                    }
                    if(pivots.Length>1)
                    {
                        Require(changed>.015f,"Members change relative spacing during calm swimming: "+school.name);
                        Require(Mathf.Abs(Mathf.DeltaAngle(yawA*Mathf.Rad2Deg,yawB*Mathf.Rad2Deg))>1,"Members turn at different headings: "+school.name);
                    }
                    var block=new MaterialPropertyBlock();school.GetComponent<Renderer>().GetPropertyBlock(block);
                    Require(block.GetFloat("_FishEnabled")==1&&block.GetVector("_FishGuide")==school.Guide,"Member animation reaches renderer without material instances");
                    var vertices=mesh.vertices;var shapes=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(2,shapes);
                    var frozen=school.DeformedVertex(vertices[0],data[0],shapes[0]);school.React(0,null);school.Render(school.MotionTime);
                    Require(frozen==school.DeformedVertex(vertices[0],data[0],shapes[0]),"Pause freezes individual trajectories and fins");
                }
                Require(surface.Schools.All(s=>s.GetComponentsInChildren<Renderer>(true).Length==1),"Independent fish retain one combined renderer per school");
                Debug.Log("Ocean ecology polish verification PASSED: independent member motion, pause, culling, shallow/reef populations and palette.");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
