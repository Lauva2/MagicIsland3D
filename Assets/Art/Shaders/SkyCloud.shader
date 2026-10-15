Shader "MagicIsland/Sky Cloud"
{
    Properties
    {
        _Daylight ("Daylight", Range(0,1)) = 1
        _Rain ("Rain", Range(0,1)) = 0
        _Color ("Cloud tint", Color) = (.96,.975,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Daylight, _Rain;
            float4 _Color;
            struct v2f { float4 position : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o; o.position=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal); return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 n=normalize(i.normal);
                float facing=saturate(dot(n,normalize(_WorldSpaceCameraPos-i.world)));
                float opacity=smoothstep(.02,.32,facing)*.88;
                float shade=lerp(.70,1,saturate(n.y*.5+.5));
                float3 night=float3(.025,.035,.055);
                #ifndef UNITY_COLORSPACE_GAMMA
                night=GammaToLinearSpace(night);
                #endif
                float3 color=lerp(night,_Color.rgb*shade*lerp(1,.64,_Rain),_Daylight);
                return float4(color,opacity);
            }
            ENDCG
        }
    }
}
