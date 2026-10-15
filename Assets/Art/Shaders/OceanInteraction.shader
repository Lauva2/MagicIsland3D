Shader "MagicIsland/Ocean Interaction"
{
    Properties
    {
        _Tint ("Tint", Color)=(.8,.94,.89,1)
        _Shape ("Shape: ring, solid, dust", Float)=0
        _Broken ("Broken ring", Range(0,1))=0
        _Opacity ("Opacity", Range(0,1))=0
        _Daylight ("Daylight", Range(0,1))=1
        _OnWater ("Follow water", Float)=0
        _WaterLevel ("Water level", Float)=-.22
        _WaveHeight ("Wave height", Float)=.035
        _WaveTime ("Wave time", Float)=0
        _Phase ("Phase", Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Tint;
            float _Shape,_Broken,_Opacity,_Daylight,_OnWater,_WaterLevel,_WaveHeight,_WaveTime,_Phase;
            struct v2f { float4 position:SV_POSITION;float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;
                float wave=_WaveHeight*(sin(w.x*.85+w.z*.32+_WaveTime)+.5*sin(w.z*1.28-w.x*.27-_WaveTime*.71));
                if(_OnWater>.5)w.y=_WaterLevel+wave+.018;
                o.position=mul(UNITY_MATRIX_VP,float4(w,1));o.uv=v.texcoord.xy;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float mask=1;
                if(_Shape<.5)
                {
                    mask=smoothstep(0,.24,i.uv.y)*(1-smoothstep(.60,1,i.uv.y));
                    float broken=smoothstep(-.25,.65,sin(i.uv.x*18.84956+_Phase));
                    mask*=lerp(.72+.28*sin(i.uv.x*43.98+_Phase),broken,_Broken);
                }
                else if(_Shape>1.5)
                {
                    float radius=length(i.uv*2-1);mask=pow(saturate(1-radius*radius),2);
                    mask*=.72+.28*sin(i.uv.x*14+i.uv.y*11+_Phase);
                }
                float3 light=lerp(float3(.025,.045,.07),float3(1,1,1),_Daylight);
                return float4(_Tint.rgb*light,_Tint.a*_Opacity*mask);
            }
            ENDCG
        }
    }
    Fallback Off
}
