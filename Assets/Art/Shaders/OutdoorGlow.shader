Shader "MagicIsland/Outdoor Glow"
{
    Properties { _Color ("Warm glow", Color)=(1,.62,.24,1) _NightGlow ("Night amount", Range(0,2))=0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            float _NightGlow;
            struct v2f { float4 position:SV_POSITION; };
            v2f vert(appdata_base v){v2f o;o.position=UnityObjectToClipPos(v.vertex);return o;}
            fixed4 frag(v2f i):SV_Target { return float4(_Color.rgb*_NightGlow,0); }
            ENDCG
        }
    }
}
