using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicIsland.Configuration
{
    public enum AudioCue { MenuMusic, IslandMusic, HomeMusic, UiClick, UiOpen, UiClose, Hoe, Plant, Water, Harvest, Axe, Pickaxe, Fertilize, Trade,
        Waves, Wind, Rain, SeagullCall, WingFlap, StepGrass, StepSand, StepWood }
    [Serializable]
    public sealed class AudioClipConfig
    {
        public AudioCue Cue;
        public string Address;
        [Range(0f, 1f)] public float Gain = 1f;
    }
    [CreateAssetMenu(menuName = "Magic Island/Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        public const string Address = "AudioCatalog";
        [Range(0f, 1f)] public float MusicVolume = .35f, EffectsVolume = .65f;
        [Range(.1f, 3f)] public float CrossfadeDuration = .8f;
        [Header("Environment")]
        [Range(0, 1)] public float WavesVolume=.28f, WindVolume=.10f, RainVolume=.24f, BirdVolume=.45f, FootstepVolume=.45f;
        [Min(1)] public float ShoreHearingDistance=16, BirdHearingDistance=26;
        [Min(.1f)] public float AmbientFadeSeconds=1.3f, FootstepStride=1.65f;
        [Min(1)] public float BirdMinInterval=9, BirdMaxInterval=20;
        public List<AudioClipConfig> Clips = new List<AudioClipConfig>();
        public AudioClipConfig Find(AudioCue cue) => Clips?.Find(c => c != null && c.Cue == cue);
        public static bool IsMusic(AudioCue cue) => cue == AudioCue.MenuMusic || cue == AudioCue.IslandMusic || cue == AudioCue.HomeMusic;
        public static bool IsAmbient(AudioCue cue) => cue==AudioCue.Waves||cue==AudioCue.Wind||cue==AudioCue.Rain;
        public static bool IsEnvironment(AudioCue cue) => cue>=AudioCue.Waves&&cue<=AudioCue.StepWood;
        public const int SourceCount=16;
        public bool IsValid => MusicVolume >= 0 && MusicVolume <= 1 && EffectsVolume >= 0 && EffectsVolume <= 1
            && WavesVolume>=0&&WavesVolume<=1&&WindVolume>=0&&WindVolume<=1&&RainVolume>=0&&RainVolume<=1
            && BirdVolume>=0&&BirdVolume<=1&&FootstepVolume>=0&&FootstepVolume<=1
            && ShoreHearingDistance>=1&&ShoreHearingDistance<=100&&BirdHearingDistance>=1&&BirdHearingDistance<=100
            && AmbientFadeSeconds>=.1f&&AmbientFadeSeconds<=10&&FootstepStride>=.1f&&FootstepStride<=5
            && BirdMinInterval>=1&&BirdMaxInterval>=BirdMinInterval&&BirdMaxInterval<=300
            && CrossfadeDuration >= .1f && CrossfadeDuration <= 3 && Clips != null
            && Clips.Count == Enum.GetValues(typeof(AudioCue)).Length
            && Clips.All(c => c != null && Enum.IsDefined(typeof(AudioCue), c.Cue) && !string.IsNullOrWhiteSpace(c.Address) && c.Gain >= 0 && c.Gain <= 1)
            && Clips.Select(c => c.Cue).Distinct().Count() == Clips.Count
            && Clips.Select(c => c.Address).Distinct(StringComparer.Ordinal).Count() == Clips.Count;
    }
}
