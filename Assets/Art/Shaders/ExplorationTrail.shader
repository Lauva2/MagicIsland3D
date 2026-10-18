Shader "MagicIsland/Exploration Trail"
{
    Properties
    {
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
        float _EdgeSoftness,_GrainStrength,_SnowCover;
        float4 _SnowColor;
        struct Input { float4 vertexColor; float2 trail; float blend; float3 worldPos; };
        void vert(inout appdata_full v,out Input o)
        { UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;o.trail=v.texcoord.xy;o.blend=v.texcoord1.x; }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float edge=abs(IN.trail.x);
            float breakup=BeachNoise(IN.worldPos.xz*.65+7.1);
            float boundary=.99-(1-breakup)*.08;
            o.Alpha=lerp(1,1-smoothstep(boundary-_EdgeSoftness,boundary,edge),saturate(IN.blend));
            float3 color=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            color=GammaToLinearSpace(color);
            #endif
            float grain=BeachNoise(IN.worldPos.xz*3.4-19.7);
            float wear=(1-smoothstep(.1,.4,edge+(breakup-.5)*.15))*.025;
            color*=1+((grain-.5)*_GrainStrength-wear)*IN.blend;
            float snow=saturate(IN.trail.y)*_SnowCover*IN.blend*lerp(.18,1,smoothstep(.1,.8,edge))
                *smoothstep(.2,.75,BeachNoise(IN.worldPos.xz*.95+24.3));
            o.Albedo=lerp(color,_SnowColor.rgb,snow);o.Smoothness=.065;o.Metallic=0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
