using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static partial class OceanBiomeSetup
    {
        static readonly Color White=new Color(.94f,.94f,.87f,0),Ink=new Color(.08f,.12f,.15f,0),Gold=new Color(.96f,.63f,.19f,0);

        static void BuildWildlife(GameObject root,OceanSurface surface,OceanGeography geography,Material material)
        {
            BuildFishSchools(root,surface,geography,material);
            BuildCoastalWildlife(root,surface,material);
        }

        static void BuildFishSchools(GameObject root,OceanSurface surface,OceanGeography geography,Material material,Transform target=null,OceanSchool[] templates=null,bool commitMeshes=true)
        {
            var floor=new BakedFloor(surface.Shore.GetComponent<MeshFilter>().sharedMesh);
            var random=new System.Random(unchecked(surface.FishSeed+2401));
            var plan=OceanFishPopulation.CreatePlan(surface.FishPopulations,surface.FishSeed);
            var schoolsRoot=target??root.transform.Find("Reef fish schools");
            var sources=new Mesh[4];
            for(int i=0;i<4;i++)sources[i]=UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder+"/"+((ShallowFishSpecies)i)+".asset")??SaveMesh(MeshFolder+"/"+((ShallowFishSpecies)i)+".asset",WildFishMesh(i));
            var schools=new List<OceanSchool>();
            try
            {
            for(int index=0;index<plan.Length;index++)
            {
                int species=(int)plan[index].Species;int members=plan[index].Members;
                var builder=new Geometry();float memberEnvelope=0;
                for(int j=0;j<members;j++)
                {
                    float spread=species==3?.65f:1;
                    var position=new Vector3((float)Range(random,-.70,.70)*spread,(float)Range(random,-.025,.025),(float)Range(random,-.85,.85));
                    float yaw=(float)Range(random,-8,8),scale=(float)Range(random,.85,1.25);
                    float bodyRadius=sources[species].vertices.Max(v=>new Vector2(v.x,v.z).magnitude)*scale;
                    memberEnvelope=Mathf.Max(memberEnvelope,new Vector2(position.x,position.z).magnitude+bodyRadius+.14f);
                    builder.Add(sources[species],Matrix4x4.TRS(position,Quaternion.Euler(0,yaw,0),Vector3.one*scale),Tint(random,Color.white),false,new Vector4(position.x,position.y,position.z,1+j*2.399963f),new Vector4(yaw*Mathf.Deg2Rad,species,0,0));
                }
                var mesh=builder.Mesh();float orbit=species==0?1.7f:species==1?.9f:species==2?1.2f:1.4f;
                Vector3 center=Vector3.zero; // Final center and individual spawns are sampled by BakeFishHabitats.
                // Bounds include lagged guides on the opposite side of the orbit and fin motion.
                mesh.bounds=new Bounds(Vector3.zero,new Vector3(2*(2*orbit+memberEnvelope+OceanSchool.MaxScatterDistance),2*(Mathf.Max(Mathf.Abs(mesh.bounds.min.y),Mathf.Abs(mesh.bounds.max.y))+.02f),2*(2*orbit+memberEnvelope+OceanSchool.MaxScatterDistance)));
                var go=MeshObject(schoolsRoot,"Fish school "+index,mesh,material,Vector3.zero);
                var school=go.AddComponent<OceanSchool>();school.Species=(ShallowFishSpecies)species;school.Members=members;school.Center=center;
                school.Radius=orbit;school.Speed=species==0?.40f:species==1?.23f:species==2?.17f:.12f;school.Phase=(float)Range(random,0,Math.PI*2);
                school.OrbitAspect=species==3?.78f:.58f;
                if(templates!=null)CopyFishStyle(templates.FirstOrDefault(t=>t.Species==school.Species),school);
                schools.Add(school);
            }
            surface.Schools=schools.ToArray();
            BakeFishHabitats(surface,schools,floor,geography);
            if(commitMeshes)foreach(var school in schools)
            {
                var filter=school.GetComponent<MeshFilter>();filter.sharedMesh=SaveMesh(MeshFolder+"/School "+Array.IndexOf(surface.Schools,school).ToString("00")+".asset",filter.sharedMesh);
            }
            }
            catch
            {
                foreach(var school in schools)if(school!=null){var mesh=school.GetComponent<MeshFilter>().sharedMesh;if(mesh!=null&&!UnityEditor.AssetDatabase.Contains(mesh))Object.DestroyImmediate(mesh);}
                throw;
            }

        }

        static void CopyFishStyle(OceanSchool source,OceanSchool target)
        {
            if(source==null)return;
            var generated=new[]{"Species","Members","Center","Phase","Habitat","HomeRadius","Swimmers","Obstacles"};
            foreach(var field in typeof(OceanSchool).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public))
                if(!field.IsInitOnly&&!field.IsNotSerialized&&!generated.Contains(field.Name))field.SetValue(target,field.GetValue(source));
        }

        // Rounded low-poly forms use vertex colours, with no texture or per-animal material.
        static void Ellipsoid(Geometry g,Vector3 center,Vector3 size,Func<float,Color> tint)
        {
            for(int ring=0;ring<8;ring++)for(int side=0;side<10;side++)
            {
                float a=ring*Mathf.PI/8,b=(ring+1)*Mathf.PI/8,c=side*Mathf.PI/5,d=(side+1)*Mathf.PI/5;
                Func<float,float,Vector3> point=(latitude,longitude)=>center+Vector3.Scale(new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Sin(latitude)*Mathf.Sin(longitude),Mathf.Cos(latitude)),size);
                var p=point(a,c);var q=point(b,c);var r=point(a,d);var s=point(b,d);Color color=tint(Mathf.Cos((a+b)*.5f));
                g.Triangle(p,q,r,color);g.Triangle(r,q,s,color);
            }
        }
        static void Eye(Geometry g,Vector3 center,float radius)=>Ellipsoid(g,center,Vector3.one*radius,v=>Ink);
        static Mesh WildFishMesh(int species)
        {
            var g=new Geometry();
            if(species==3)
            {
                Color back=new Color(.32f,.43f,.48f,0),edge=new Color(.55f,.66f,.65f,.5f);
                Ellipsoid(g,Vector3.zero,new Vector3(.20f,.055f,.31f),v=>back);
                foreach(int side in new[]{-1,1})
                {
                    g.Triangle(new Vector3(side*.10f,.02f,.25f),new Vector3(side*.53f,.015f,-.02f),new Vector3(side*.12f,.01f,-.28f),edge);
                    Eye(g,new Vector3(side*.085f,.05f,.15f),.025f);
                    for(int j=0;j<4;j++)Ellipsoid(g,new Vector3(side*(.14f+j*.065f),.025f,.10f-j*.048f),new Vector3(.025f,.005f,.03f),v=>White);
                }
                g.Tube(new Vector3(0,0,-.23f),new Vector3(.04f,-.005f,-.80f),.015f,back);
                return g.Mesh();
            }
            Color body=species==0?new Color(.59f,.73f,.75f,0):species==1?new Color(.88f,.55f,.34f,0):new Color(.87f,.76f,.43f,0);
            float length=species==0?.25f:.31f,width=species==2?.075f:.065f,height=species==0?.055f:species==1?.10f:.16f;
            Ellipsoid(g,Vector3.zero,new Vector3(width,height,length),v=>species==1&&((v>-.70f&&v<-.40f)||(v>.35f&&v<.70f))?White:species==2&&v>.35f&&v<.58f?Ink:body);
            Color fin=species==0?White:body*.85f;fin.a=.7f;
            g.Triangle(new Vector3(0,0,-length*.8f),new Vector3(-width*1.7f,0,-length*1.6f),new Vector3(width*1.7f,0,-length*1.6f),fin);
            g.Triangle(new Vector3(0,height*.6f,.12f),new Vector3(0,height*1.7f,-.02f),new Vector3(0,height*.6f,-.18f),fin);
            foreach(int side in new[]{-1,1})
            {
                g.Triangle(new Vector3(side*width*.7f,0,.04f),new Vector3(side*width*2.3f,-.02f,-.12f),new Vector3(side*width*.7f,-.025f,-.07f),fin);
                Eye(g,new Vector3(side*width*.72f,height*.3f,length*.65f),.016f);
            }
            return g.Mesh();
        }
        static Mesh GullBody()
        {
            var g=new Geometry();
            Ellipsoid(g,new Vector3(0,.25f,0),new Vector3(.11f,.13f,.27f),v=>v<-.2f?new Color(.73f,.78f,.79f,0):White);
            Ellipsoid(g,new Vector3(0,.40f,.23f),new Vector3(.09f,.095f,.11f),v=>White);
            g.Triangle(new Vector3(-.045f,.37f,.30f),new Vector3(0,.37f,.48f),new Vector3(.045f,.37f,.30f),Gold);
            g.Triangle(new Vector3(-.045f,.37f,.30f),new Vector3(0,.42f,.31f),new Vector3(0,.37f,.48f),Gold);
            g.Triangle(new Vector3(.045f,.37f,.30f),new Vector3(0,.37f,.48f),new Vector3(0,.42f,.31f),Gold);
            g.Triangle(new Vector3(-.08f,.24f,-.16f),new Vector3(-.13f,.25f,-.39f),new Vector3(.13f,.25f,-.39f),White);
            g.Triangle(new Vector3(-.08f,.24f,-.16f),new Vector3(.13f,.25f,-.39f),new Vector3(.08f,.24f,-.16f),White);
            for(int side=-1;side<=1;side+=2)
            {
                Eye(g,new Vector3(side*.078f,.42f,.27f),.018f);
                g.Tube(new Vector3(side*.05f,.17f,-.03f),new Vector3(side*.05f,.025f,-.02f),.012f,Gold);
                g.Triangle(new Vector3(side*.05f,.025f,-.055f),new Vector3(side*.05f-.04f,.015f,.065f),new Vector3(side*.05f+.04f,.015f,.065f),Gold);
            }
            return g.Mesh();
        }
        static Mesh GullWing(int side)
        {
            var g=new Geometry();Color grey=new Color(.73f,.78f,.81f,0);
            var a=new Vector3(0,0,.11f);var b=new Vector3(side*.37f,.015f,.06f);var c=new Vector3(side*.78f,-.02f,-.21f);var d=new Vector3(side*.38f,0,-.19f);var e=new Vector3(0,0,-.16f);
            g.Triangle(a,b,d,grey);g.Triangle(a,d,e,White);g.Triangle(b,c,d,Ink);
            for(int i=0;i<4;i++)g.Triangle(Vector3.Lerp(b,c,.3f+i*.16f),c+new Vector3(-side*i*.055f,0,-.03f),d,Ink);
            var mesh=g.Mesh();var bounds=mesh.bounds;bounds.Expand(Vector3.one*.5f);mesh.bounds=bounds;return mesh;
        }
        static Mesh CrabMesh()
        {
            var g=new Geometry();Color red=new Color(.81f,.32f,.18f,0),light=new Color(.96f,.54f,.27f,0);
            Ellipsoid(g,new Vector3(0,.10f,0),new Vector3(.17f,.075f,.12f),v=>red);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<4;i++)
                {
                    var a=new Vector3(side*.12f,.10f,.075f-i*.045f);var b=new Vector3(side*(.24f+i*.013f),.08f,.12f-i*.085f);var c=new Vector3(side*.29f,.018f,.14f-i*.09f);
                    var leg=new Geometry();leg.Tube(a,b,.016f,red);leg.Tube(b,c,.012f,light);
                    var mesh=leg.Mesh();
                    g.Add(mesh,Matrix4x4.identity,Color.white,false,new Vector4(a.x,a.y,a.z,100+i),new Vector4((i+(side>0?1:0))*Mathf.PI,0,0,0));
                    Object.DestroyImmediate(mesh);
                }
                var p=new Vector3(side*.22f,.15f,.18f);var pivot=new Vector3(side*.12f,.11f,.07f);
                var claw=new Geometry();claw.Tube(pivot,p,.024f,light);
                claw.Triangle(p,p+new Vector3(side*.07f,.03f,.11f),p+new Vector3(-side*.03f,.05f,.08f),light);
                var clawMesh=claw.Mesh();
                g.Add(clawMesh,Matrix4x4.identity,Color.white,false,new Vector4(pivot.x,pivot.y,pivot.z,side<0?110:111),Vector4.zero);
                Object.DestroyImmediate(clawMesh);
                g.Tube(new Vector3(side*.065f,.15f,.06f),new Vector3(side*.065f,.22f,.085f),.012f,red);Eye(g,new Vector3(side*.065f,.22f,.085f),.022f);
            }
            return g.Mesh();
        }
    }
}
