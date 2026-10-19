Shader "MagicIsland/Meadow Flora"
{
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #include "SeasonalColor.cginc"
        #include "MeadowTouch.cginc"
        float _IslandWindTime,_IslandWindStrength,_MeadowCutCount;
        float4 _IslandWindDirection,_MeadowCuts[128];
        struct Input {float4 vertexColor;float visible;float season;};
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;o.visible=1;o.season=v.texcoord2.y;
            float3 root=mul(unity_ObjectToWorld,float4(v.texcoord1.x,v.texcoord2.x,v.texcoord1.y,1)).xyz;
            for(int i=0;i<(int)_MeadowCutCount;i++)
                if(all(abs(root.xz-_MeadowCuts[i].xy)<=_MeadowCuts[i].zw)){o.visible=0;break;}
            // Explicit authoring encodes species in the existing weight; other plants retain their motion.
            float encoded=v.texcoord.x;
            float flower=encoded<=-2 && encoded>=-3;
            float grass=encoded<=-4 && encoded>=-5;
            float weight=flower ? -encoded-2 : grass ? -encoded-4 : encoded;
            float authoredWeight=weight;
            float phase=v.texcoord.y,time=_IslandWindTime;
            float sampleMask=MeadowSampleMask(root);
            float sway=.7*sin(time*1.1+phase)+.3*sin(time*.57+phase*1.6);
            float cross=0,amplitude=.045;
            if(flower)
            {
                sway=.74*sin(time*.71+phase)+.26*sin(time*1.23+phase*1.7);
                cross=.14*sin(time*.93+phase*2.1);amplitude=.032;
            }
            if(grass)
            {
                // Broad gusts travel through the meadow; individual clumps retain a small local sway.
                float field=dot(root.xz,float2(.052,.037));
                sway=.72*sin(time*.43-field)+.28*sin(time*.91+phase);
                cross=.13*sin(time*.67+phase*1.4);amplitude=.045;
                if(sampleMask>0)
                {
                    float fieldPhase=dot(root.xz,_IslandWindDirection.xy)*6.283185/max(8,_MeadowTouchWave.y);
                    float pulse=.8*sin(time*_MeadowTouchWave.z-fieldPhase+.35*sin(root.x*.07-root.z*.045))+.2*sin(time*.91+phase);
                    sway=lerp(sway,pulse,sampleMask);amplitude=lerp(amplitude,_MeadowTouchWave.x,sampleMask);
                    float height=max(0,mul(unity_ObjectToWorld,v.vertex).y-root.y-.01);
                    weight=lerp(weight,min(saturate(height/.3),saturate(authoredWeight*3)),sampleMask);
                }
            }
            float2 direction=_IslandWindDirection.xy;
            v.vertex.xz+=(direction*sway+float2(-direction.y,direction.x)*cross)
                *(weight*weight*amplitude*clamp(_IslandWindStrength,0,1.6));
            MeadowTouch(v,root,sampleMask,grass,flower,authoredWeight);
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            clip(IN.visible-.5);float3 c=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c=GammaToLinearSpace(c);
            #endif
            o.Albedo=IslandSeasonColor(c,_IslandSeasonGrass,IN.season*.65);o.Smoothness=.06;o.Metallic=0;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
