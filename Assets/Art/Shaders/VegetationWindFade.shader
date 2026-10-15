Shader "MagicIsland/Vegetation Wind Fade"
{
    Properties
    {
        _Color ("Tint", Color)=(1,1,1,1)
        _MainTex ("Albedo", 2D)="white" {}
        _Glossiness ("Smoothness", Range(0,1))=.06
        _Metallic ("Metallic", Range(0,1))=0
        [HideInInspector] _Mode ("Mode", Float)=0
        [HideInInspector] _SrcBlend ("Source blend", Float)=1
        [HideInInspector] _DstBlend ("Destination blend", Float)=0
        [HideInInspector] _ZWrite ("Depth write", Float)=1
        [HideInInspector] _WindShape ("Root height, height, radius, radial", Vector)=(0,1,1,0)
        [HideInInspector] _WindBend ("Bend", Float)=0
        [HideInInspector] _WindFlutter ("Flutter", Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert alpha:fade
        #pragma target 3.0
        #include "VegetationWind.cginc"
        ENDCG
    }
    Fallback "Transparent/Diffuse"
}
