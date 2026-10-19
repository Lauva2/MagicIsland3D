#ifndef ISLAND_LANDSCAPE_CROWN_FADE
#define ISLAND_LANDSCAPE_CROWN_FADE
float _LandscapeCrownFadeCount;
float4 _LandscapeCrownFades[32];
float IslandCrownOpacity(float4 metadata)
{
    if (metadata.x > -2 || metadata.x < -3 || metadata.w <= 0 || metadata.w >= 1) return 1;
    for (int i = 0; i < (int)_LandscapeCrownFadeCount; i++)
        if (metadata.y == _LandscapeCrownFades[i].x) return _LandscapeCrownFades[i].y;
    return 1;
}
void IslandCrownClip(float opacity, float4 screenPosition)
{
    // Stable pixel coverage avoids sorting entire merged landscapes as transparent geometry.
    // Keep the original tree shadow, including the shared wind deformation.
    #ifndef UNITY_PASS_SHADOWCASTER
    if (opacity < 1)
    {
        float2 pixel = floor(screenPosition.xy / screenPosition.w * _ScreenParams.xy);
        float threshold = frac(52.9829189 * frac(dot(pixel, float2(.06711056, .00583715))));
        clip(opacity - threshold);
    }
    #endif
}
#endif
