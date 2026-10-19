using System;
using UnityEngine;

namespace MagicIsland.TerrainSystem
{
    [Serializable]
    public struct MeadowTouchSettings
    {
        public int ArtVersion;
        public Vector2 Center;
        public float AreaRadius, Feather, InfluenceRadius, BendDistance, RecoverySeconds, Spacing;
        public float FlowerFactor, EnvelopeRadius, WaveAmplitude, WaveLength, WaveSpeed;
        public Renderer[] Renderers;
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        public bool IsValid => ArtVersion == 0 || (ArtVersion == 1 && Finite(Center.x) && Finite(Center.y)
            && AreaRadius >= 8 && AreaRadius <= 60 && Feather >= 1 && Feather < AreaRadius
            && InfluenceRadius >= .5f && InfluenceRadius <= 1.5f && BendDistance > 0 && BendDistance <= .20f
            && RecoverySeconds >= .2f && RecoverySeconds <= 1.2f && Spacing >= .2f && Spacing <= .5f
            && FlowerFactor >= 0 && FlowerFactor <= .4f && EnvelopeRadius >= 1.2f && EnvelopeRadius <= 1.3f
            && WaveAmplitude >= .045f && WaveAmplitude <= .07f && WaveLength >= 8 && WaveLength <= 40 && WaveSpeed > 0 && WaveSpeed <= 1.5f
            && Renderers != null && Renderers.Length > 0 && Renderers.Length <= 64 && Array.TrueForAll(Renderers, r => r != null));
    }
}
