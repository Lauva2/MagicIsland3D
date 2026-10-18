#ifndef MAGIC_ISLAND_COASTAL_WET_SAND
#define MAGIC_ISLAND_COASTAL_WET_SAND
float _WetSandStrength, _WetSandSmoothness;
float4 _WetSandBand;
float BeachHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float BeachNoise(float2 p)
{
    float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(BeachHash(cell),BeachHash(cell+float2(1,0)),f.x),
                lerp(BeachHash(cell+float2(0,1)),BeachHash(cell+1),f.x),f.y);
}
void IslandWetSand(float3 position,float3 vertexColor,inout float3 albedo,out float smoothness)
{
    // Match the real sloping beach without changing geometry, colours or seasonal grass.
    // The height band is authored in world metres; high mainland sand/snow stays dry.
    float sand=smoothstep(.055,.16,vertexColor.r-vertexColor.b)
              *(1-smoothstep(.0,.06,vertexColor.g-vertexColor.r));
    float patches=BeachNoise(position.xz*1.15+23.7);
    float height=position.y+(patches-.5)*.024;
    float wet=(1-smoothstep(_WetSandBand.x,_WetSandBand.y,height))
               *smoothstep(_WetSandBand.z,_WetSandBand.w,height)*sand*saturate(_WetSandStrength);
    albedo*=lerp(float3(1,1,1),float3(.74,.72,.68),wet);
    // Broad, broken highlights respond to existing lights and shadows, with no emission.
    float sheen=lerp(.35,1,smoothstep(.3,.75,BeachNoise(position.xz*2.7-8.2)));
    smoothness=lerp(.03,_WetSandSmoothness,wet*sheen);
}
#endif
