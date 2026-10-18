using System;
using System.Collections.Generic;
using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // Fixed editor-only dressing. Batches stay inside already reserved paths or unbuildable shores.
    public static partial class StarterIslandWorldSetup
    {
        static float ScatterValue(float x,float z)
        {
            double value=Math.Sin(x*12.9898+z*78.233)*43758.5453;
            return (float)(value-Math.Floor(value));
        }
        static float GroveDensity(float x,float z)
        {
            float field=(Mathf.Sin(x*.041f+Mathf.Sin(z*.017f))*Mathf.Cos(z*.037f)+1)*.5f;
            float density=Mathf.Lerp(.22f,.98f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.28f,.75f,field)));
            if(StarterIslandLayout.OriginalRegion(x,z)==3)density=Mathf.Lerp(.52f,1,density);
            // A broad meadow opens the view towards the lake from the first junction.
            float clearing=Mathf.Exp(-Mathf.Pow((x+65)/130,2)-Mathf.Pow((z-390)/125,2));
            return density*(1-.7f*clearing);
        }
        static readonly Vector2[] FirstTrail={new Vector2(0,26),new Vector2(0,160),new Vector2(0,240),
            new Vector2(-150,370),new Vector2(-100,520),new Vector2(-100,700),new Vector2(-75,700)};

        static void RouteDetails(Transform parent)
        {
            var batches=new SortedDictionary<string,Geometry>(StringComparer.Ordinal);
            Geometry Batch(string kind,float x,float z)
            {
                string key=kind+" "+Mathf.FloorToInt(x/128)+" "+Mathf.FloorToInt(z/128);
                if(!batches.TryGetValue(key,out var geometry)){geometry=new Geometry();batches.Add(key,geometry);}return geometry;
            }
            var random=new System.Random(821037);
            for(int segment=1;segment<FirstTrail.Length;segment++)
            {
                Vector2 a=FirstTrail[segment-1],b=FirstTrail[segment],direction=(b-a).normalized,side=new Vector2(-direction.y,direction.x);
                float length=Vector2.Distance(a,b);
                for(float distance=3;distance<length-2;distance+=3.8f)
                {
                    Vector2 center=a+direction*distance;
                    foreach(int sign in new[]{-1,1})
                    {
                        var point=center+side*sign*(3.8f+(float)random.NextDouble()*1.2f);
                        if(point.y<180)continue; // Keep the wooden crossing clear; use its own rail mesh.
                        float route=(float)StarterIslandLayout.RouteDistance(point.x,point.y);
                        if(route<3||route>5.6f||!StarterIslandLayout.Contains(point.x,point.y,1))continue;
                        var geometry=Batch("Route verge",point.x,point.y);
                        for(int j=0;j<3;j++)
                        {
                            var p=point+new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*.7f;
                            var root=P(p.x,p.y,.025f);float height=.24f+(float)random.NextDouble()*.30f;
                            Grass(geometry,root,height,new Color(.44f,.63f,.32f));
                            if(random.NextDouble()<.62)
                            {
                                Color tint=point.y>470&&j==0?new Color(.70f,.77f,.97f):j%2==0?new Color(.96f,.89f,.63f):new Color(.88f,.87f,.77f);
                                Flower(geometry,root+new Vector3(.2f,0,.13f),height+.12f,.14f,tint);
                            }
                        }
                        if(distance%38<4)
                            geometry.Ball(P(point.x,point.y,-.10f),new Vector3(.48f,.32f,.42f),new Color(.66f,.69f,.55f));
                    }
                }
            }
            // Lake reeds and low rocks make scalloped banks legible without filling open water.
            for(int i=0;i<240;i++)
            {
                double angle=i*Math.PI*2/240,radius=StarterIslandLayout.LakeRadius(angle);
                float x=160+(float)(Math.Cos(angle)*220*radius),z=630+(float)(Math.Sin(angle)*190*radius);
                if(StarterIslandLayout.OutletDistance(x,z)<6||StarterIslandLayout.InletDistance(x,z)<6||StarterIslandLayout.TreeIsle(x,z,-6))continue;
                float noise=ScatterValue(x,z);if(noise<.30f)continue;
                var geometry=Batch("Lake bank details",x,z);
                for(int j=0;j<4;j++)
                {
                    float px=x+Mathf.Cos((float)angle)*(.2f+j*.3f),pz=z+Mathf.Sin((float)angle)*(.2f+j*.3f);
                    if(StarterIslandLayout.CanPlace(Mathf.RoundToInt(px),Mathf.RoundToInt(pz)))continue;
                    var root=P(px,pz,-.05f);Grass(geometry,root,.8f+noise*.8f,new Color(.43f,.58f,.36f));
                    if(j%2==0)geometry.Tube(root,root+Vector3.up*(1.1f+noise*.9f),.045f,.025f,new Color(.55f,.47f,.30f));
                }
                if(i%7==0)geometry.Ball(P(x,z,-.22f),new Vector3(1.4f,.75f,1.1f),new Color(.65f,.70f,.62f));
            }
            // The added coastal noses have low outcrops; all remain outside usable construction cells.
            for(int i=0;i<360;i++)
            {
                double angle=i*Math.PI*2/360,radius=StarterIslandLayout.CoastRadius(angle)+.002;
                float x=(float)(Math.Cos(angle)*StarterIslandLayout.RadiusX*radius),z=(float)(StarterIslandLayout.CenterZ+Math.Sin(angle)*StarterIslandLayout.RadiusZ*radius);
                if(z<32||Mathf.Abs(x)<24&&z<220||ScatterValue(x,z)<.45f)continue;
                if(StarterIslandLayout.CanPlace(Mathf.RoundToInt(x),Mathf.RoundToInt(z)))continue;
                float scale=1.5f+ScatterValue(z,x)*3;bool buildable=false;
                foreach(float dx in new[]{-scale,0,scale})foreach(float dz in new[]{-scale,0,scale})
                    buildable|=StarterIslandLayout.CanPlace(Mathf.RoundToInt(x+dx),Mathf.RoundToInt(z+dz));
                if(buildable)continue;var geometry=Batch("Coastal outcrops",x,z);
                geometry.Ball(P(x,z,-scale*.20f),new Vector3(scale,scale*.65f,scale*.75f),new Color(.64f,.67f,.57f));
                geometry.Ball(P(x+scale*.45f,z,-scale*.28f),new Vector3(scale*.7f,scale*.42f,scale*.6f),new Color(.71f,.73f,.61f));
            }
            var signs=new Geometry();
            Arrow(signs,new Vector2(4.2f,205),new Vector2(0,1));
            Arrow(signs,new Vector2(-3.5f,235),new Vector2(-.75f,.66f));
            Arrow(signs,new Vector2(-145.8f,371),new Vector2(.32f,.95f));
            Arrow(signs,new Vector2(-95.5f,610),new Vector2(0,1));
            Part(parent,"Trail waystones",signs.Mesh(),details,false);
            foreach(var entry in batches)if(entry.Value.Count>0)Part(parent,entry.Key,entry.Value.Mesh(),details,false);
        }
        static void Grass(Geometry g,Vector3 p,float height,Color color)
        {
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI/3;var side=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.07f;
                var top=p+new Vector3(Mathf.Sin(a)*height*.26f,height,Mathf.Cos(a)*height*.26f);
                g.Quad(p-side,p+side,top+side*.12f,top-side*.12f,color);
                g.Quad(p+side,p-side,top-side*.12f,top+side*.12f,color);
            }
        }
        static void Flower(Geometry g,Vector3 p,float height,float radius,Color color)
        {
            var center=p+Vector3.up*height;g.Tube(p,center,.025f,.016f,new Color(.39f,.57f,.30f));
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2/5;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;var side=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*radius*.52f;
                g.Quad(center,center+radial*.65f+side,center+radial*1.5f+Vector3.up*.04f,center+radial*.65f-side,color);
            }
            g.Ball(center+Vector3.up*.03f,Vector3.one*.045f,new Color(.95f,.75f,.34f));
        }
        static void Arrow(Geometry g,Vector2 point,Vector2 direction)
        {
            var p=P(point.x,point.y,-.12f);var stone=new Color(.65f,.69f,.57f);var wood=new Color(.55f,.41f,.27f);var inset=new Color(.77f,.85f,.66f);
            g.Ball(p,new Vector3(.75f,.48f,.65f),stone);g.Tube(p,p+Vector3.up*1.65f,.10f,.08f,wood);
            var forward=new Vector3(direction.x,0,direction.y).normalized;var right=Vector3.Cross(Vector3.up,forward);
            var center=p+Vector3.up*1.7f;
            g.Quad(center-forward*.55f-right*.22f,center+forward*.5f-right*.22f,center+forward*.5f+right*.22f,center-forward*.55f+right*.22f,wood);
            g.Quad(center+forward*.35f-right*.42f,center+forward*1.05f,center+forward*.35f+right*.42f,center+forward*.35f+right*.42f,inset);
        }
    }
}
