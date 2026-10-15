Shader "MagicIsland/Island Ocean"
{
    Properties
    {
        _ShallowColor ("浅水色", Color) = (0.38,0.68,0.64,1)
        _MidColor ("中层海色", Color) = (0.19,0.46,0.52,1)
        _DeepColor ("深海色", Color) = (0.08,0.25,0.35,1)
        _FoamColor ("泡沫色", Color) = (0.87,0.92,0.87,1)
        _DepthRange ("深浅过渡深度", Range(0.5,8)) = 4.6
        _ShallowOpacity ("浅水不透明度", Range(0,1)) = 0.22
        _FoamWidth ("岸边浪线间距", Range(0.05,1.5)) = 0.42
        _FoamStrength ("浪花强度", Range(0,1)) = 0.48
        _NormalStrength ("细波纹强度", Range(0,0.3)) = 0.06
        _ReflectionStrength ("天空反光", Range(0,1)) = 0.38
        _CausticsStrength ("浅水光纹", Range(0,0.3)) = 0.045
        _WaveHeight ("波浪高度", Range(0,0.15)) = 0.035
        _WaveSpeed ("波浪速度", Range(0,3)) = 0.65
        _Sparkle ("阳光反射强度", Range(0,1)) = 0.10
        [HideInInspector] _SunDirection ("太阳方向", Vector) = (0.3,0.8,-0.4,0)
        [HideInInspector] _SunColor ("阳光色", Color) = (1,0.975,0.92,1)
        [HideInInspector] _SunIntensity ("阳光亮度", Float) = 1
        _OceanTime ("动画时间", Float) = 0
        _Daylight ("日照", Range(0,1)) = 1
        _Rain ("雨量", Range(0,1)) = 0
        _DepthBias ("额外水深", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
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
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _ShallowColor, _MidColor, _DeepColor, _FoamColor, _SunDirection, _SunColor;
            float _DepthRange, _ShallowOpacity, _FoamWidth, _WaveHeight, _WaveSpeed, _Sparkle;
            float _FoamStrength, _NormalStrength, _ReflectionStrength, _CausticsStrength, _SunIntensity;
            float _OceanTime, _Daylight, _Rain, _DepthBias;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float4 screen : TEXCOORD0; float3 world : TEXCOORD1; float eye : TEXCOORD2; };
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float detail(float2 p)
            {
                // Rotated octaves remove the square cells of a single noise layer.
                float n=noise(p)*.57;
                p=mul(float2x2(.80,-.60,.60,.80),p)*2.03+7.1;
                return n+noise(p)*.29+noise(p*2.07-3.2)*.14;
            }
            v2f vert(appdata v)
            {
                v2f o; float3 w=mul(unity_ObjectToWorld,v.vertex).xyz; float t=_OceanTime*_WaveSpeed;
                w.y += _WaveHeight*(sin(w.x*0.85+w.z*0.32+t)+0.5*sin(w.z*1.28-w.x*0.27-t*0.71));
                o.world=w; o.pos=mul(UNITY_MATRIX_VP,float4(w,1)); o.screen=ComputeScreenPos(o.pos);
                o.eye=-mul(UNITY_MATRIX_V,float4(w,1)).z; return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float raw=SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen));
                float orthoRaw=raw;
                #if defined(UNITY_REVERSED_Z)
                orthoRaw=1-orthoRaw;
                #endif
                float sceneEye=lerp(LinearEyeDepth(raw),lerp(_ProjectionParams.y,_ProjectionParams.z,orthoRaw),unity_OrthoParams.w);
                float vertical=lerp(abs(normalize(_WorldSpaceCameraPos-i.world).y),abs(UNITY_MATRIX_V._m21),unity_OrthoParams.w);
                float depth=max(0,(sceneEye-i.eye)*vertical+_DepthBias);
                float t=_OceanTime*_WaveSpeed; float2 p=i.world.xz;
                float2 flow=float2(t*.035,-t*.024);
                float2 warp=float2(noise(p*.41+flow),noise(p*.37-flow+13.7))-.5;
                float variation=detail(p*.21+flow*.5);
                float deep=depth/max(_DepthRange,.01);
                float3 color=lerp(_ShallowColor.rgb,_MidColor.rgb,smoothstep(0,.60,deep));
                color=lerp(color,_DeepColor.rgb,smoothstep(.30,1.10,deep));

                // Large waves match OceanSurface.HeightAt; fine normals only affect the reflection.
                float c1=cos(p.x*.85+p.y*.32+t),c2=.5*cos(p.y*1.28-p.x*.27-t*.71);
                float2 slope=_WaveHeight*float2(.85*c1-.27*c2,.32*c1+1.28*c2);
                float2 ripple=float2(sin(p.x*2.7+p.y*1.6+warp.x*3+t*.73),sin(p.y*3.1-p.x*1.2+warp.y*3-t*.61));
                ripple+=float2(sin(p.x*7.3+p.y*4.7-t*1.3),cos(p.y*6.7-p.x*3.8+t*1.1))*(.18+_Rain*.20);
                float3 normal=normalize(float3(-slope.x-ripple.x*_NormalStrength,1,-slope.y-ripple.y*_NormalStrength));
                float3 viewDirection=normalize(lerp(_WorldSpaceCameraPos-i.world,UNITY_MATRIX_V[2].xyz,unity_OrthoParams.w));
                float fresnel=.04+.45*pow(1-saturate(dot(normal,viewDirection)),3);
                float3 sky=float3(.30,.53,.66)*(1-_Rain*.28);
                color*=.94+.08*variation+.08*(normal.x+normal.z);
                color=lerp(color,sky,fresnel*_ReflectionStrength);
                float rippleSheen=pow(saturate(.5+.5*(ripple.x*.6+ripple.y*.4)),12);
                color+=sky*rippleSheen*_ReflectionStrength*.10;
                float3 halfDirection=normalize(normalize(_SunDirection.xyz)+viewDirection);
                float sunReflection=pow(saturate(dot(normal,halfDirection)),96);
                color+=_SunColor.rgb*sunReflection*_Sparkle*min(_SunIntensity,1.5)*(1-_Rain*.7)*_Daylight;

                // Depth-contour waves travel toward the beach and dissolve into broken lace.
                // Reserve breaking surf for the beach, not the broad raised sand shelf.
                float shoreZone=1-smoothstep(_FoamWidth*.55,_FoamWidth*1.35,depth);
                float wave=sin(6.283185*(depth/max(_FoamWidth*2.4,.12)+t*.11+warp.x*.22));
                float aa=max(fwidth(wave),.015);
                float roller=smoothstep(.90-aa,.99,wave);
                float breakup=detail(p*3.6+warp*2+float2(-t*.14,t*.08));
                float lace=smoothstep(.25,.68,breakup);
                float contact=(1-smoothstep(.025,.12,depth))*(.45+.55*lace);
                float foam=saturate((roller*shoreZone*lace+contact*.55)*_FoamStrength);

                float2 q=p*2.1+warp*.8+flow;
                float cells=abs(sin(q.x+t*.23)*cos(q.y-t*.19)+.45*sin(q.x*.83+q.y*1.17-t*.13));
                float caustic=(1-smoothstep(.04,.14+fwidth(cells),cells));
                caustic*=smoothstep(.04,.20,depth)*(1-smoothstep(.55,2.2,depth));
                color+=float3(.55,.90,.78)*caustic*_CausticsStrength*_Daylight*(1-_Rain*.7);
                color=lerp(color,_FoamColor.rgb,foam);
                color=lerp(color*float3(.025,.045,.085),color,saturate(_Daylight));
                color=lerp(color,color*float3(.76,.86,.94),_Rain*.65);
                float alpha=lerp(_ShallowOpacity,0.995,1-exp(-deep*1.55));
                // Become opaque before the finite seabed mesh ends; no visible outer ring.
                alpha=max(alpha,smoothstep(.65,1.10,deep));
                alpha=max(alpha,foam*.85);
                return float4(color,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
