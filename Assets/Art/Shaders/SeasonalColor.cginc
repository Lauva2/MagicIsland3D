#ifndef MAGIC_ISLAND_SEASONAL_COLOR
#define MAGIC_ISLAND_SEASONAL_COLOR
float4 _IslandSeasonGrass, _IslandSeasonFoliage, _IslandSeasonEvergreen;
float3 IslandSeasonColor(float3 original, float4 palette, float mask)
{
    // Retain the authored variation and texture luminance. Alpha is never recolored,
    // so camera fade and opaque foliage use exactly the same seasonal surface.
    float brightness=dot(original,float3(.2126,.7152,.0722));
    float targetBrightness=max(.01,dot(palette.rgb,float3(.2126,.7152,.0722)));
    float3 target=palette.rgb*clamp(brightness/targetBrightness,.65,1.35);
    return lerp(original,target,saturate(palette.a*mask));
}
#endif
