Shader "MagicIsland/Meadow Ground"
{
    Properties
    {
        _MeadowMap ("草甸分布", 2D)="black" {}
        _MeadowRect ("草甸范围", Vector)=(-180,180,660,680)
        _MeadowStrength ("草甸层次", Range(0,1))=.65
        _Color ("Tint", Color)=(1,1,1,1)
        _WetSandStrength ("湿沙深浅", Range(0,1))=.75
        _WetSandSmoothness ("湿沙柔光", Range(.03,.5))=.28
        _WetSandBand ("湿沙高度：湿／干／海底淡出／海底起点", Vector)=(-.16,-.005,-.32,-.235)
        _ApproachStrength ("出生小径磨损", Range(0,1))=.48
        _ApproachColor ("小径暖沙色", Color)=(.88,.79,.59,1)
        [HideInInspector] _VillageApproach ("Spawn / lane XZ", Vector)=(0,0,0,0)
        [HideInInspector] _VillageApproachCurve ("Clear bend", Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        #include "SeasonalColor.cginc"
        #include "CoastalWetSand.cginc"
        sampler2D _MeadowMap;
        float4 _MeadowRect;
        float _MeadowStrength;
        float4 _Color;
        float4 _VillageApproach,_ApproachColor;
        float _ApproachStrength,_VillageApproachCurve;
        struct Input { float4 vertexColor; float grass; float3 worldPos; };
        void vert(inout appdata_full v,out Input o)
        { UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;o.grass=v.texcoord.x; }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float3 c=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c=GammaToLinearSpace(c);
            #endif
            float2 a=_VillageApproach.xy,b=_VillageApproach.zw,delta=b-a;
            float span=length(delta),t=saturate(dot(IN.worldPos.xz-a,delta)/max(dot(delta,delta),.0001));
            float2 side=float2(-delta.y,delta.x)/max(span,.0001);
            float2 center=lerp(a,b,t)+side*sin(t*3.141593)*_VillageApproachCurve;
            float width=.36+BeachNoise(IN.worldPos.xz*2.2+4.7)*.14;
            float wear=(1-smoothstep(width,width+.24,length(IN.worldPos.xz-center)))
                       *saturate(_ApproachStrength)*step(.1,span);
            o.Albedo=lerp(IslandSeasonColor(c*_Color.rgb,_IslandSeasonGrass,saturate(IN.grass)),_ApproachColor.rgb*_Color.rgb,wear);
            float4 meadow=tex2D(_MeadowMap,(IN.worldPos.xz-_MeadowRect.xy)/_MeadowRect.zw);
            float detail=(BeachNoise(IN.worldPos.xz*2.5+41)-.5)*.065;
            float variation=(meadow.g-.5)*.32+(meadow.a-.5)*.10+detail;
            o.Albedo*=1+variation*meadow.r*_MeadowStrength;
            IslandWetSand(IN.worldPos,IN.vertexColor.rgb,o.Albedo,o.Smoothness);
            o.Metallic=0;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
