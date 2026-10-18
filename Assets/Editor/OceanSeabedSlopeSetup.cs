using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // An explicit edit of the existing baked floor, never a geography/ecology reroll.
    public static class OceanSeabedSlopeSetup
    {
        public const string FloorPath=OceanSetup.MeshFolder+"/Island Shore.asset";
        const string Marker="SeabedSlope:1:";
        const float MaximumSlope=.65f,Feather=6;
        struct Link { public int To; public float Distance; }
        struct Entry { public int Index; public float Value; }
        sealed class EntryComparer : IComparer<Entry>
        {
            public int Compare(Entry a,Entry b){int c=a.Value.CompareTo(b.Value);return c!=0?c:a.Index.CompareTo(b.Index);}
        }
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Seabed slope: "+why);}
        static string Hash(){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(FloorPath))).Replace("-","");}

        [MenuItem("Magic Island/Art/Soften Offshore Seabed (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(FloorPath);Require(mesh!=null,"missing baked floor");
            var importer=AssetImporter.GetAtPath(FloorPath);
            string marker=importer.userData??"";
            if(marker.StartsWith(Marker,StringComparison.Ordinal))
            {
                Require(marker==Marker+Hash(),"the softened floor was edited; preserve the authored mesh rather than applying this bake again");
                Debug.Log("Seabed slope already applied; authored resources retained.");return;
            }
            Require(marker.Length==0,"preserve existing mesh importer metadata");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath);
            var surface=prefab.GetComponent<OceanSurface>();var original=mesh.vertices;var triangles=mesh.triangles;
            Require(original.Length==OceanBiomeSetup.Segments*OceanBiomeSetup.Rings,"unsupported floor topology");
            var layout=JsonUtility.FromJson<OceanReefArtSetup.Layout>(File.ReadAllText(OceanReefArtSetup.LayoutPath));
            var patches=prefab.GetComponentsInChildren<OceanHabitatPatch>(true);
            Require(layout.version==1&&layout.groups.Length==patches.Length,"missing authored reef footprints");
            var footprints=new List<Bounds>();
            foreach(var group in layout.groups)
            {
                var patch=patches.Single(p=>AssetDatabase.GetAssetPath(p.GetComponentInChildren<MeshFilter>().sharedMesh)==group.path);
                var filter=patch.GetComponentInChildren<MeshFilter>();
                var bounds=group.bounds;bounds.Encapsulate(filter.sharedMesh.bounds);
                var toFloor=surface.Shore.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var footprint=new Bounds(toFloor.MultiplyPoint3x4(bounds.min),Vector3.zero);
                for(int corner=0;corner<8;corner++)
                    footprint.Encapsulate(toFloor.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1))));
                footprint.Expand(1.5f);footprints.Add(footprint);
            }
            var links=Enumerable.Range(0,original.Length).Select(_=>new List<Link>()).ToArray();
            var pinned=new bool[original.Length];
            for(int t=0;t<triangles.Length;t+=3)
            {
                int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                Connect(links,original,a,b);Connect(links,original,b,c);Connect(links,original,c,a);
                var lo=Vector3.Min(original[a],Vector3.Min(original[b],original[c]));
                var hi=Vector3.Max(original[a],Vector3.Max(original[b],original[c]));
                bool protect=NearCoast(original[a])||NearCoast(original[b])||NearCoast(original[c]);
                foreach(var bounds in footprints)
                    protect|=lo.x<=bounds.max.x&&hi.x>=bounds.min.x&&lo.z<=bounds.max.z&&hi.z>=bounds.min.z;
                foreach(var school in surface.Schools)
                {
                    float dx=school.Center.x-Mathf.Clamp(school.Center.x,lo.x,hi.x),dz=school.Center.z-Mathf.Clamp(school.Center.z,lo.z,hi.z);
                    protect|=dx*dx+dz*dz<=(school.HomeRadius+.5f)*(school.HomeRadius+.5f);
                }
                if(protect)pinned[a]=pinned[b]=pinned[c]=true;
            }
            // The lower envelope limits isolated peaks. No vertex is ever raised into fish water.
            var ceiling=Propagate(links,original.Select(p=>p.y).ToArray(),MaximumSlope);
            var distance=Propagate(links,pinned.Select(p=>p?0f:float.PositiveInfinity).ToArray(),1,Feather);
            var edited=(Vector3[])original.Clone();int changed=0;float largest=0;
            for(int i=0;i<edited.Length;i++)
            {
                if(pinned[i])continue;
                float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(distance[i]/Feather));
                float target=Mathf.Lerp(original[i].y,Mathf.Min(original[i].y,ceiling[i]),weight);
                if(original[i].y-target<.0001f)continue;
                edited[i].y=target;changed++;largest=Mathf.Max(largest,original[i].y-target);
            }
            Require(changed>0,"no unprotected offshore peaks to soften");
            Verify(original,edited,triangles,pinned,surface);
            var normals=mesh.normals;mesh.vertices=edited;mesh.RecalculateNormals();
            var newNormals=mesh.normals;
            for(int i=0;i<edited.Length;i++)if(original[i]==edited[i])newNormals[i]=normals[i];
            mesh.normals=newNormals;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
            // Unity emits spaces after empty YAML fields; retain the repository's LF/text convention.
            File.WriteAllText(FloorPath,string.Join("\n",File.ReadAllLines(FloorPath).Select(line=>line.TrimEnd()))+"\n");
            importer.userData=Marker+Hash();importer.SaveAndReimport();
            Directory.CreateDirectory("Logs/SeabedSlope");
            File.WriteAllText("Logs/SeabedSlope/Bake.txt",$"PASS: {changed} lowered vertices; maximum lowering {largest:F3}m; {pinned.Count(p=>p)} pinned vertices; topology, colours, coast, complete reef footprints and fish water volumes retained.\n");
            Debug.Log("Seabed slope bake PASSED: "+changed+" offshore vertices softened; no prefab, material or ecology writes.");
        }
        static bool NearCoast(Vector3 p)=>new Vector2(p.x,p.z).magnitude-IslandLayout.CoastRadius(Math.Atan2(p.z,p.x))<=7;
        static void Connect(List<Link>[] links,Vector3[] points,int a,int b)
        {
            if(links[a].Any(e=>e.To==b))return;
            float length=new Vector2(points[a].x-points[b].x,points[a].z-points[b].z).magnitude;
            links[a].Add(new Link{To=b,Distance=length});links[b].Add(new Link{To=a,Distance=length});
        }
        static float[] Propagate(List<Link>[] links,float[] values,float cost,float limit=float.PositiveInfinity)
        {
            var queue=new SortedSet<Entry>(new EntryComparer());
            for(int i=0;i<values.Length;i++)if(!float.IsPositiveInfinity(values[i]))queue.Add(new Entry{Index=i,Value=values[i]});
            while(queue.Count>0)
            {
                var current=queue.Min;queue.Remove(current);if(current.Value>=limit)continue;
                foreach(var link in links[current.Index])
                {
                    float next=current.Value+link.Distance*cost;if(next>=values[link.To])continue;
                    queue.Remove(new Entry{Index=link.To,Value=values[link.To]});values[link.To]=next;
                    queue.Add(new Entry{Index=link.To,Value=next});
                }
            }
            return values;
        }
        static void Verify(Vector3[] before,Vector3[] after,int[] triangles,bool[] pinned,OceanSurface surface)
        {
            for(int i=0;i<before.Length;i++)
            {
                Require(after[i].x==before[i].x&&after[i].z==before[i].z&&after[i].y<=before[i].y,"horizontal topology and lower-only edit");
                Require(!float.IsNaN(after[i].y)&&!float.IsInfinity(after[i].y),"finite height");
                if(pinned[i])Require(before[i]==after[i],"coast and complete habitat triangles remain fixed");
            }
            var oldVolume=new OceanFishWaterVolume(before,triangles);var newVolume=new OceanFishWaterVolume(after,triangles);
            foreach(var school in surface.Schools)
                Require(oldVolume.HighestFloor(school.Center,school.HomeRadius)==newVolume.HighestFloor(school.Center,school.HomeRadius),"exact original fish safety floor");
        }
        // A repeat bake and ordinary Setup must retain both the result and artist settings.
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"verification requires an isolated project");
            string prefab=File.ReadAllText(OceanSetup.PrefabPath),water=File.ReadAllText(OceanSetup.MaterialFolder+"/Water.mat");
            Rebuild();string floor=File.ReadAllText(FloorPath),meta=File.ReadAllText(FloorPath+".meta");
            Rebuild();PrototypeAddressablesSetup.EnsureSetup();AssetDatabase.SaveAssets();
            Require(floor==File.ReadAllText(FloorPath)&&meta==File.ReadAllText(FloorPath+".meta"),"repeat bake and Setup preserve the authored floor");
            Require(prefab==File.ReadAllText(OceanSetup.PrefabPath)&&water==File.ReadAllText(OceanSetup.MaterialFolder+"/Water.mat"),"prefab and hand-tuned water retained");
            Debug.Log("Seabed slope preservation verification PASSED.");
        }
    }
}
