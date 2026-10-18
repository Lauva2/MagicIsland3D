Shader "MagicIsland/Island Ocean"
{
    Properties
    {
        _ShallowColor ("浅水色", Color) = (0.055,0.78,0.73,1)
        _MidColor ("中层海色", Color) = (0.055,0.56,0.67,1)
        _DeepColor ("深海色", Color) = (0.035,0.28,0.50,1)
        _FoamColor ("泡沫色", Color) = (0.95,0.95,0.88,1)
        _DepthRange ("深浅过渡深度", Range(0.5,8)) = 4.6
        _ShallowOpacity ("浅水不透明度", Range(0,1)) = 0.38
        _FoamWidth ("岸边浪带尺度", Range(0.05,1.5)) = 0.42
        _FoamStrength ("浪花强度", Range(0,1)) = 0.48
        _NormalStrength ("细波纹强度", Range(0,0.3)) = 0.06
        _ReflectionStrength ("天空反光", Range(0,1)) = 0.38
        _CausticsStrength ("浅水光纹", Range(0,0.3)) = 0.045
        _WaveHeight ("波浪高度", Range(0,0.15)) = 0.035
        _WaveSpeed ("波浪速度", Range(0,3)) = 0.65
        _SwellStrength ("海面浪带强度", Range(0,1)) = 0.65
        _SwellLength ("海面浪带尺度", Range(2,30)) = 10
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
            float _SwellStrength, _SwellLength;
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
            float shoreBreaker(float depth,float phase,float width,float lace,float bedSlope)
            {
                // Move from deeper water toward zero depth; fade before the cycle wraps.
                float travel=smoothstep(.05,.95,phase);
                float front=lerp(width*1.8,.012,travel);
                float thickness=min(width*lerp(.11,.045,travel),bedSlope*.13);
                float aa=max(fwidth(depth),.004);
                float crest=1-smoothstep(thickness,thickness+aa,abs(depth-front));
                float wake=smoothstep(front,front+thickness+aa,depth)*(1-smoothstep(front+width*.14,front+width*.45,depth));
                float life=smoothstep(0,.16,phase)*(1-smoothstep(.74,1,phase));
                return (crest+wake*.06)*life*lace;
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
                // Nonperiodic, independently oriented ripples avoid rows of identical glints.
                float2 ripple=2*(float2(noise(p*1.3+warp+flow*3),noise(mul(float2x2(.80,-.60,.60,.80),p)*1.51-warp-flow*2+17.3))-.5);
                ripple*=1+_Rain*.35;
                // Broad travelling swells and a weaker crossing set. Slowly varying
                // phase and envelopes break long crests without repeating bright dots.
                float swellScale=6.283185/max(_SwellLength,2);
                float swellWarp=noise(p*.11+flow*.6)*1.9+noise(p*.27-flow*.3)*.5;
                float swellPhase=dot(p,float2(.83,.55))*swellScale+swellWarp*2.2-t*.6;
                float crossPhase=dot(p,float2(-.42,.91))*swellScale*1.63+swellWarp*1.3+t*.41;
                float swellGroup=lerp(.35,1,smoothstep(.28,.68,noise(p*.055+flow*1.3)));
                float swellMask=_SwellStrength*saturate(_WaveHeight/.035)*smoothstep(.3,1.8,depth)*swellGroup;
                float swell=sin(swellPhase)*.7+sin(crossPhase)*.3;
                float crest=pow(saturate(.5+.5*sin(swellPhase)),36)*smoothstep(.30,.60,noise(p*.19+flow*.8));
                slope+=swellMask*(float2(.83,.55)*cos(swellPhase)*.12+float2(-.42,.91)*cos(crossPhase)*.05);
                float3 normal=normalize(float3(-slope.x-ripple.x*_NormalStrength,1,-slope.y-ripple.y*_NormalStrength));
                float3 viewDirection=normalize(lerp(_WorldSpaceCameraPos-i.world,UNITY_MATRIX_V[2].xyz,unity_OrthoParams.w));
                float fresnel=.04+.45*pow(1-saturate(dot(normal,viewDirection)),3);
                float3 sky=float3(.30,.53,.66)*(1-_Rain*.28);
                color*=.94+.08*variation+.08*(normal.x+normal.z);
                color*=1+swell*swellMask*.16;
                // Blue crest tint, never an independent white foam layer over the sea.
                color+=float3(.10,.22,.30)*crest*swellMask*.55;
                float reflectionDepth=lerp(.2,1,smoothstep(.3,2.5,depth));
                color=lerp(color,sky,fresnel*_ReflectionStrength*reflectionDepth);
                float3 halfDirection=normalize(normalize(_SunDirection.xyz)+viewDirection);
                float sunReflection=pow(saturate(dot(normal,halfDirection)),96);
                color+=_SunColor.rgb*sunReflection*_Sparkle*min(_SunIntensity,1.5)*(1-_Rain*.7)*_Daylight;

                // Local timing, crest gaps and two independent pulses avoid a closed white rim.
                float width=max(_FoamWidth,.05);
                float shoreZone=1-smoothstep(width*1.55,width*2.1,depth);
                // Recover depth change per world metre. Flat sand shelves must not
                // turn into wide foam sheets when a front crosses their shared depth.
                float2 px=ddx(p),py=ddy(p),dz=float2(ddx(depth),ddy(depth));
                float determinant=px.x*py.y-px.y*py.x;
                determinant=(determinant<0?-1:1)*max(abs(determinant),.000001);
                float bedSlope=length(float2(dz.x*py.y-dz.y*px.y,px.x*dz.y-py.x*dz.x)/determinant);
                float beachSlope=smoothstep(.12,.32,bedSlope);
                float shorePhase=noise(p*.075+11.3)*.38+noise(p*.19-7.1)*.08;
                float breakup=detail(p*4.6+warp*1.2+float2(-t*.10,t*.07));
                float lace=smoothstep(.43,.65,breakup);
                // Bend the narrow front and soften it with small holes, instead of
                // clipping a straight depth contour into rectangular dashes.
                float foamWarp=detail(p*2.7+warp*.5+float2(-t*.07,t*.05))-.5;
                float foamDepth=depth+foamWarp*min(width*.20,bedSlope*.18);
                lace*=lerp(.35,1,smoothstep(.28,.60,detail(p*11+flow*2)));
                float gaps=smoothstep(.42,.66,noise(p*.38+float2(t*.012,-t*.009)));
                float secondGaps=smoothstep(.43,.68,noise(p*.29+17.2-float2(t*.008,t*.011)));
                float first=shoreBreaker(foamDepth,frac(t*.18+shorePhase),width,lace*gaps,bedSlope);
                float second=shoreBreaker(foamDepth,frac(t*.157+shorePhase*.83+.53),width*.86,lace*secondGaps,bedSlope)*.62;
                float contact=(1-smoothstep(.012,.065,depth))*lace*gaps*.12;
                float foam=saturate((first+second+contact)*shoreZone*beachSlope*_FoamStrength*saturate(_WaveHeight/.035));

                float2 q=p*2.1+warp*.8+flow;
                float cells=abs(sin(q.x+t*.23)*cos(q.y-t*.19)+.45*sin(q.x*.83+q.y*1.17-t*.13));
                float caustic=(1-smoothstep(.04,.14+fwidth(cells),cells));
                caustic*=smoothstep(.04,.20,depth)*(1-smoothstep(.55,2.2,depth));
                color+=float3(.55,.90,.78)*caustic*_CausticsStrength*_Daylight*(1-_Rain*.7);
                color=lerp(color,_FoamColor.rgb,foam);
                // Sunrise is still low-angle light: the water tint must not outshine
                // the lit seabed and become a luminous grey veil over the animals.
                float surfaceLight=saturate(_Daylight*.3+min(_SunIntensity,1)*saturate(_SunDirection.y)*.7);
                color=lerp(color*float3(.025,.045,.085),color,surfaceLight);
                color=lerp(color,color*float3(.76,.86,.94),_Rain*.65);
                // Clear shallow water, progressively absorbing at reef depths.
                // Keep the distant sea opaque before the finite seabed ends.
                float alpha=lerp(_ShallowOpacity,0.995,1-exp(-deep*deep*.85));
                // Become opaque before the finite seabed mesh ends; no visible outer ring.
                alpha=max(alpha,smoothstep(.8,1.8,deep));
                alpha=max(alpha,foam*.85);
                return float4(color,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
