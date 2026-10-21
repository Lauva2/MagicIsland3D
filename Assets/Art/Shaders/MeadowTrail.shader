Shader "MagicIsland/Meadow Trail"
{
    Properties
    {
        _MeadowMap ("草甸分布", 2D)="black" {}
        _MeadowRect ("草甸范围", Vector)=(-180,180,660,680)
        _LegacyRoad ("原四边形路面", Float)=0
        _EdgeSoftness ("路肩柔化", Range(.08,.4))=.26
        _GrainStrength ("地面磨损", Range(0,.1))=.035
        _SnowCover ("薄雪覆盖", Range(0,1))=.65
        _SnowColor ("薄雪色", Color)=(.89,.93,.93,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" }
        CGPROGRAM
        #pragma surface surf Standard alpha:fade fullforwardshadows vertex:vert
        #pragma target 3.0
        #include "CoastalWetSand.cginc"
        sampler2D _MeadowMap;
        float4 _MeadowRect;
        float _LegacyRoad;
        float _EdgeSoftness,_GrainStrength,_SnowCover;
        float4 _SnowColor;
        struct Input { float4 vertexColor; float2 trail; float blend; float3 worldPos; float shoulder; };
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;o.trail=v.texcoord.xy;o.blend=v.texcoord1.x;
            // Only explicitly baked road vertices opt in; legacy UV data retains its old rendering.
            o.shoulder=v.texcoord1.y>=-914 && v.texcoord1.y<=-913 ? -v.texcoord1.y-913 : 0;
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float4 meadow=tex2D(_MeadowMap,(IN.worldPos.xz-_MeadowRect.xy)/_MeadowRect.zw);
            float edge=lerp(abs(IN.trail.x),meadow.b*4/2.15,saturate(_LegacyRoad));
            if(IN.shoulder>0)
            {
                float variation=(BeachNoise(IN.worldPos.xz*.33+34.2)-.5)*.28
                    +(BeachNoise(IN.worldPos.xz*.93-14.8)-.5)*.08;
                edge=max(0,edge+variation*saturate(IN.shoulder)*smoothstep(.45,.85,edge));
            }
            float breakup=BeachNoise(IN.worldPos.xz*.65+7.1);
            float boundary=.99-(1-breakup)*.08;
            o.Alpha=lerp(1,1-smoothstep(boundary-_EdgeSoftness,boundary,edge),saturate(IN.blend));
            float meadowEdge=1-smoothstep(.69,1.02-(1-breakup)*.10,edge);
            o.Alpha=lerp(o.Alpha,meadowEdge,meadow.r);
            float3 color=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            color=GammaToLinearSpace(color);
            #endif
            float grain=BeachNoise(IN.worldPos.xz*3.4-19.7);
            float wear=(1-smoothstep(.1,.4,edge+(breakup-.5)*.15))*.025;
            color*=1+((grain-.5)*_GrainStrength-wear)*IN.blend;
            float snow=saturate(IN.trail.y)*_SnowCover*IN.blend*lerp(.18,1,smoothstep(.1,.8,edge))
                *smoothstep(.2,.75,BeachNoise(IN.worldPos.xz*.95+24.3));
            color*=1-meadow.r*(.025+BeachNoise(IN.worldPos.xz*1.7)*.035);
            o.Albedo=lerp(color,_SnowColor.rgb,snow);o.Smoothness=lerp(.065,.08,saturate(_LegacyRoad));o.Metallic=0;
        }
        ENDCG
        // Retain the old road shadow outside the grassland; soft grassland edges cast no raised-strip shadow.
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            CGPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_shadowcaster
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MeadowMap;
            float4 _MeadowRect;
            struct shadowOut { V2F_SHADOW_CASTER; float3 world : TEXCOORD1; };
            shadowOut shadowVert(appdata_base v)
            {
                shadowOut o;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            float4 shadowFrag(shadowOut i) : SV_Target
            {
                float mask=tex2D(_MeadowMap,(i.world.xz-_MeadowRect.xy)/_MeadowRect.zw).r;
                clip(.01-mask);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
