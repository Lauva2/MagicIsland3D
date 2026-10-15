        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness,_Metallic;
        float4 _WindShape,_IslandWindDirection;
        float _WindBend,_WindFlutter,_IslandWindTime,_IslandWindStrength;
        struct Input { float2 uv_MainTex; };
        void vert(inout appdata_full v)
        {
            float height=saturate((v.vertex.y-_WindShape.x)/_WindShape.y);
            float radial=saturate(length(v.vertex.xz)/_WindShape.z);
            float weight=lerp(height,radial,_WindShape.w);weight*=weight;
            float3 anchor=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
            float phase=dot(anchor.xz,float2(.71,.43))+anchor.y*.29;
            float t=_IslandWindTime;
            float sway=sin(t*1.1+phase)*.68+sin(t*.57+phase*1.63)*.32;
            float flutter=sin(t*3.2+phase+v.vertex.x*3.1+v.vertex.z*2.7);
            float scale=length(mul((float3x3)unity_ObjectToWorld,float3(1,0,0)));
            float amplitude=weight*_IslandWindStrength*scale;
            float2 along=_IslandWindDirection.xy;
            float2 crosswind=float2(-along.y,along.x);
            float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
            world.xz+=(along*sway*_WindBend+crosswind*flutter*_WindFlutter)*amplitude;
            world.y+=flutter*_WindFlutter*.35*amplitude;
            v.vertex=mul(unity_WorldToObject,float4(world,1));
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            fixed4 color=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            o.Albedo=color.rgb;o.Alpha=color.a;o.Metallic=_Metallic;o.Smoothness=_Glossiness;
        }
