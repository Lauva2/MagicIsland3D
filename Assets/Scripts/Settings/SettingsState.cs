using System;
namespace MagicIsland.Settings
{
    [Serializable] public sealed class SavedSettings
    { public int Version = 1; public float Volume = 1; public bool Fullscreen; }
    public static class SettingsState
    {
        public static bool Valid(SavedSettings s) => s != null && s.Version == 1 && !float.IsNaN(s.Volume) && !float.IsInfinity(s.Volume) && s.Volume >= 0 && s.Volume <= 1;
        public static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1 : Math.Max(0, Math.Min(1, value));
    }
}
