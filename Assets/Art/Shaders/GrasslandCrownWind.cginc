// UV0 is authored only on the explicitly refined crown vertices. All other detail stays still.
float _IslandWindTime,_IslandWindStrength,_CrownSway,_CrownFlutter;
float4 _IslandWindDirection;
void IslandCrownWind(inout appdata_full v)
{
    if(v.texcoord.x<=-2 && v.texcoord.x>=-3 && v.texcoord.w>0 && v.texcoord.w<1)
    {
        float weight=saturate(-v.texcoord.x-2),tree=v.texcoord.y,leaf=v.texcoord.z,t=_IslandWindTime;
        float sway=.72*sin(t*.57+tree)+.28*sin(t*.91+tree*1.6);
        float flutter=.68*sin(t*1.17+leaf)+.32*sin(t*1.83+leaf*1.4);
        float2 along=_IslandWindDirection.xy,crosswind=float2(-along.y,along.x);
        float2 delta=(along*(sway*clamp(_CrownSway,0,.16))+crosswind*(flutter*clamp(_CrownFlutter,0,.035)))
            *clamp(_IslandWindStrength,0,1.6);
        float3 local=mul((float3x3)unity_WorldToObject,float3(delta.x,0,delta.y));
        v.vertex.xyz+=local*weight*weight;
        // Inverse-transpose of the height-weighted shear, including its shadow caster.
        v.normal.y-=dot(v.normal,local)*2*weight*v.texcoord.w;
        v.normal=normalize(v.normal);
    }
}
