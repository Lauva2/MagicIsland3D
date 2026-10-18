using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.Tools;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagicIsland.Managers
{
    public sealed partial class AudioManager : MonoBehaviour
    {
        private readonly Dictionary<AudioCue, AudioClip> clips = new Dictionary<AudioCue, AudioClip>();
        private readonly List<AsyncOperationHandle<AudioClip>> handles = new List<AsyncOperationHandle<AudioClip>>();
        private readonly Dictionary<AudioCue, float> nextUiSound = new Dictionary<AudioCue, float>();
        private readonly float[] fadeFrom = new float[2], fadeTo = new float[2];
        private AudioSource[] music, effects;
        private GameObject sourceRoot;
        private AudioListener listener;
        private readonly Dictionary<AudioListener, bool> sceneListeners = new Dictionary<AudioListener, bool>();
        private AudioCatalog catalog;
        private ToolManager tools;
        private ShopManager shop;
        private int generation, nextVoice;
        private float fadeElapsed;
        public bool Ready => catalog != null && sourceRoot != null && clips.Count == catalog.Clips.Count;
        public string LastError { get; private set; }
        public AudioCue? CurrentMusic { get; private set; }
        public AudioCue? LastSound { get; private set; }
        public int PlayedSoundCount { get; private set; }
        public int LoadedClipCount => clips.Count;
        public int OwnedHandleCount => handles.Count;

        public IEnumerator Initialize(AudioCatalog config)
        {
            Clear(); LastError = null; int current = generation;
            if (config == null || !config.IsValid) { LastError = "音频配置不完整，请检查 AudioCatalog"; yield break; }
            catalog = config;
            foreach (var entry in config.Clips)
            {
                var handle = Addressables.LoadAssetAsync<AudioClip>(entry.Address); handles.Add(handle); yield return handle;
                if (current != generation) yield break;
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null || handle.Result.length <= 0)
                { Clear(); LastError = "音频暂时无法加载，请检查 AA 地址：" + entry.Address; yield break; }
                clips.Add(entry.Cue, handle.Result);
            }
            sourceRoot = new GameObject("Audio Sources"); sourceRoot.transform.SetParent(transform, false);
            listener = sourceRoot.AddComponent<AudioListener>(); listener.enabled = false;
            RefreshListeners(); listener.enabled = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
            music = new[] { Source("Music A", true), Source("Music B", true) };
            effects = new AudioSource[8]; for (int i = 0; i < effects.Length; i++) effects[i] = Source("Effect " + i, false);
            CreateEnvironmentSources();
        }
        private AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name); go.transform.SetParent(sourceRoot.transform, false);
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = loop;
            source.spatialBlend = 0; source.volume = 0; source.priority = loop ? 64 : 128;
            source.ignoreListenerPause = loop; return source;
        }
        public void BindGameplay(ToolManager toolManager, ShopManager shopManager, MagicIsland.Player.PlayerMovement player=null)
        {
            RefreshListeners();
            UnbindGameplay(); tools = toolManager; shop = shopManager;
            if (tools != null) tools.ActionSucceeded += ToolSound;
            if (shop != null) shop.TransactionSucceeded += TradeSound;
            BindEnvironment(player);
        }
        public void UnbindGameplay()
        {
            if (tools != null) tools.ActionSucceeded -= ToolSound;
            if (shop != null) shop.TransactionSucceeded -= TradeSound;
            UnbindEnvironment();
            tools = null; shop = null; StopEffects();
        }
        private void TradeSound() => PlaySound(AudioCue.Trade);
        private void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => RefreshListeners();
        internal void RefreshListeners()
        {
            if (listener == null) return;
            foreach (var removed in sceneListeners.Keys.Where(l => l == null).ToArray()) sceneListeners.Remove(removed);
            foreach (var other in FindObjectsOfType<AudioListener>(true))
            {
                if (other == listener) continue;
                if (!sceneListeners.ContainsKey(other)) sceneListeners.Add(other, other.enabled);
                other.enabled = false;
            }
        }
        private void ToolSound(ToolActionKind kind)
        {
            switch (kind)
            {
                case ToolActionKind.Hoe: PlaySound(AudioCue.Hoe); break;
                case ToolActionKind.Seed: PlaySound(AudioCue.Plant); break;
                case ToolActionKind.RainSpell:
                case ToolActionKind.Water: PlaySound(AudioCue.Water); break;
                case ToolActionKind.Harvest: PlaySound(AudioCue.Harvest); break;
                case ToolActionKind.Axe: PlaySound(AudioCue.Axe); break;
                case ToolActionKind.Pickaxe: PlaySound(AudioCue.Pickaxe); break;
                case ToolActionKind.Fertilizer: PlaySound(AudioCue.Fertilize); break;
            }
        }
        public bool PlaySound(AudioCue cue)
        {
            if (!Ready || !isActiveAndEnabled || AudioCatalog.IsMusic(cue) || AudioCatalog.IsEnvironment(cue) || !clips.TryGetValue(cue, out var clip)) return false;
            if (cue == AudioCue.UiClick || cue == AudioCue.UiOpen || cue == AudioCue.UiClose)
            {
                if (nextUiSound.TryGetValue(cue, out float next) && UnityEngine.Time.unscaledTime < next) return false;
                nextUiSound[cue] = UnityEngine.Time.unscaledTime + .045f;
            }
            // A bounded voice pool prevents rapid UI/actions from accumulating AudioSources.
            AudioSource source = effects[nextVoice++ % effects.Length]; if (nextVoice == int.MaxValue) nextVoice = 0;
            source.Stop(); source.clip = clip; source.volume = catalog.EffectsVolume * catalog.Find(cue).Gain; source.Play();
            LastSound = cue; PlayedSoundCount++; return true;
        }
        public void SetMusic(AudioCue? cue)
        {
            if (!Ready || (cue.HasValue && !AudioCatalog.IsMusic(cue.Value)) || CurrentMusic == cue) return;
            CurrentMusic = cue; int selected = -1;
            if (cue.HasValue)
            {
                AudioClip clip = clips[cue.Value];
                for (int i = 0; i < 2; i++) if (music[i].clip == clip) selected = i;
                if (selected < 0)
                {
                    selected = music[0].volume <= music[1].volume ? 0 : 1;
                    music[selected].Stop(); music[selected].volume = 0; music[selected].clip = clip; music[selected].Play();
                }
            }
            for (int i = 0; i < 2; i++)
            {
                fadeFrom[i] = music[i].volume;
                fadeTo[i] = i == selected ? catalog.MusicVolume * catalog.Find(cue.Value).Gain : 0;
            }
            fadeElapsed = 0;
        }
        private void Update() { TickMusic(UnityEngine.Time.unscaledDeltaTime);TickAmbience(UnityEngine.Time.unscaledDeltaTime); }
        internal void TickMusic(float deltaTime)
        {
            if (!Ready || !isActiveAndEnabled) return;
            fadeElapsed += Mathf.Max(0, deltaTime);
            float progress = Mathf.Clamp01(fadeElapsed / catalog.CrossfadeDuration), blend = Mathf.SmoothStep(0, 1, progress);
            for (int i = 0; i < 2; i++)
            {
                music[i].volume = Mathf.Lerp(fadeFrom[i], fadeTo[i], blend);
                if (progress >= 1 && fadeTo[i] == 0 && music[i].clip != null) { music[i].Stop(); music[i].clip = null; }
            }
        }
        public void StopEffects()
        {
            if (effects != null) foreach (var source in effects) if (source != null) { source.Stop(); source.clip = null; }
            StopEnvironmentVoices();
        }
        public void Clear()
        {
            generation++; UnbindGameplay();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneLoaded;
            if (listener != null) listener.enabled = false;
            foreach (var pair in sceneListeners) if (pair.Key != null) pair.Key.enabled = pair.Value;
            sceneListeners.Clear(); listener = null;
            if (music != null) foreach (var source in music) if (source != null) { source.Stop(); source.clip = null; }
            if (sourceRoot != null) Destroy(sourceRoot); sourceRoot = null; music = effects = null;
            foreach (var handle in handles)
                if (handle.IsValid()) { if (handle.IsDone) Addressables.Release(handle); else handle.Completed += h => { if (h.IsValid()) Addressables.Release(h); }; }
            handles.Clear(); clips.Clear(); nextUiSound.Clear(); catalog = null; CurrentMusic = LastSound = null;
            nextVoice = PlayedSoundCount = 0; fadeElapsed = 0;
            System.Array.Clear(fadeFrom, 0, fadeFrom.Length); System.Array.Clear(fadeTo, 0, fadeTo.Length);
        }
        private void OnDisable() { SuspendEnvironment();StopEnvironmentLoops();StopEffects(); if (music != null) foreach (var source in music) if (source != null) source.Pause(); }
        private void OnEnable() { if (music != null) foreach (var source in music) if (source != null) source.UnPause(); }
        private void OnDestroy() => Clear();
    }
}
