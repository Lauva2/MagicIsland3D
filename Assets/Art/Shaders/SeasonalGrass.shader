Shader "MagicIsland/Seasonal Grass"
{
    Properties
    {
        _Color ("Tint", Color)=(1,1,1,1)
        _MainTex ("Albedo", 2D)="white" {}
        _Glossiness ("Smoothness", Range(0,1))=.04
        _Metallic ("Metallic", Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #include "SeasonalColor.cginc"
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness,_Metallic;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            fixed4 color=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            o.Albedo=IslandSeasonColor(color.rgb,_IslandSeasonGrass,1);
            o.Alpha=color.a;o.Smoothness=_Glossiness;o.Metallic=_Metallic;
        }
        ENDCG
    }
    Fallback "Standard"
}
