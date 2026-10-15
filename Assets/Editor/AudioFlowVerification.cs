using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.Configuration;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Loading;
using MagicIsland.Managers;
using MagicIsland.UI;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class AudioFlowVerification
    {
        const string Key = "MagicIsland.AudioVerification";
        static AudioManager failure;
        static AudioCatalog bad;
        static AudioFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            string[] paths = Directory.GetFiles(PrototypeUiPrefabBuilder.Folder, "*.prefab").Concat(new[] { "Assets/Config/AudioCatalog.asset" }).ToArray();
            var bytes = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Repeated setup preserves all audio/button edits");
            foreach (string path in paths.Where(p => p.EndsWith(".prefab")))
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Button>(true).All(b => b.GetComponent<UIButtonAudio>() != null), "Serialized UI button audio " + path);
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>("Assets/Config/AudioCatalog.asset");
            Require(catalog.IsValid && catalog.Clips.Count == Enum.GetValues(typeof(AudioCue)).Length, "Complete audio configuration");
            foreach (var entry in catalog.Clips)
            {
                string path = AudioSetup.ClipPath(entry.Cue); var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Require(clip != null && clip.samples > 0 && clip.channels == 1 && clip.frequency > 10000, "Imported nonempty WAV " + entry.Cue);
                Require(AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)).address == entry.Address, "Audio AA address " + entry.Cue);
                float[] samples = WaveSamples(path);
                Require(samples.Any(s => Mathf.Abs(s) > .01f) && samples.All(s => !float.IsNaN(s) && Mathf.Abs(s) < .99f), "Audible unclipped waveform " + entry.Cue);
                if (AudioCatalog.IsMusic(entry.Cue)) Require(clip.length > 15 && Mathf.Abs(samples[0] - samples[samples.Length - 1]) < .035f, "Smooth loop seam " + entry.Cue);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool condition, string why) { if (!condition) throw new Exception(why); }
        static float[] WaveSamples(string path)
        {
            // Music is streamed in Unity. Inspect source PCM rather than attempting GetData on a streaming clip.
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                Require(new string(reader.ReadChars(4)) == "RIFF", "WAV RIFF header"); reader.ReadInt32();
                Require(new string(reader.ReadChars(4)) == "WAVE", "WAV format");
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    string chunk = new string(reader.ReadChars(4)); int length = reader.ReadInt32();
                    if (chunk == "data") { var samples = new float[length / 2]; for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f; return samples; }
                    reader.BaseStream.Position += length + (length % 2);
                }
            }
            throw new Exception("WAV data chunk missing");
        }
        static void Phase(int value) { SessionState.SetInt(Key + ".Phase", value); Debug.Log("Audio phase " + value); }
        static T View<T>() where T : Component => Object.FindObjectOfType<T>();
        static AudioSource[] Music(GameManager g) => g.Audio.GetComponentsInChildren<AudioSource>().Where(s => s.name.StartsWith("Music ")).ToArray();
        static void MusicTick(GameManager g, float dt = 2) => typeof(AudioManager).GetMethod("TickMusic", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(g.Audio, new object[] { dt });
        static void Sources(GameManager g)
        {
            Require(g.Audio.GetComponentsInChildren<AudioSource>().Length == AudioCatalog.SourceCount, "Bounded music/effect/environment sources");
            Require(Object.FindObjectsOfType<AudioListener>().Count(l => l.enabled) == 1, "One persistent audio listener across scenes");
        }
        static void Sound(GameManager g, AudioCue cue, Action action)
        { int before = g.Audio.PlayedSoundCount; action(); Require(g.Audio.PlayedSoundCount == before + 1 && g.Audio.LastSound == cue, "Exactly one committed sound " + cue); }
        static void Silent(GameManager g, Action action)
        { int before = g.Audio.PlayedSoundCount; action(); Require(g.Audio.PlayedSoundCount == before, "Failed or blocked operation is silent"); }
        static void Plot(GameManager g, int index) => g.Players.RestorePosition(g.Farm.Grid.CellCenter(index) + new Vector3(0, .1f, -1));
        static void NoSave(GameManager g) => Require(!File.Exists(g.Saves.SavePath), "Audio/gameplay must not save daytime progress");
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 23 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Audio timeout " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Audio bootstrap failure");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Audio.Ready && g.Audio.LoadedClipCount == Enum.GetValues(typeof(AudioCue)).Length && g.Audio.OwnedHandleCount == Enum.GetValues(typeof(AudioCue)).Length && g.Audio.CurrentMusic == AudioCue.MenuMusic, "AA-loaded menu music and owned handles");
                    Sources(g); MusicTick(g); Require(Music(g).Count(s => s.clip != null) == 1 && Music(g).Max(s => s.volume) > .2f && Music(g).Any(s => s.isPlaying), "Menu music source is playing with configured volume");
                    Require(g.Audio.PlayedSoundCount == 0, "Startup and hidden UI preload are silent");
                    string directory = Path.GetFullPath("Logs/AudioSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); SessionState.SetString(Key + ".Dir", directory);
                    g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, directory, g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting, g.Configuration.Animals);
                    g.Settings.Initialize(Path.Combine(directory, "Settings"));
                    var menu = View<MainMenuView>(); Sound(g, AudioCue.UiClick, () => menu.SettingsButton.onClick.Invoke()); Phase(1);
                }
                else if (p == 1 && View<SettingsView>() != null)
                {
                    Require(g.Audio.LastSound == AudioCue.UiOpen, "Settings opens with panel sound");
                    g.Settings.SetVolume(0); Require(AudioListener.volume == 0 && g.Audio.CurrentMusic == AudioCue.MenuMusic, "Mute applies to both music/effects without restarting");
                    g.Settings.SetVolume(.37f); g.Settings.Flush(); Require(Mathf.Approximately(AudioListener.volume, .37f), "Existing master volume controls all sources");
                    Silent(g, () => { var button = View<MainMenuView>().ContinueButton; bool enabled = button.interactable; button.interactable = false; button.onClick.Invoke(); button.interactable = enabled; });
                    View<SettingsView>().CloseButton.onClick.Invoke(); g.EnterGame(); Phase(2);
                }
                else if (p == 2 && g.State == GameFlowState.Playing)
                {
                    Require(g.Audio.CurrentMusic == AudioCue.IslandMusic && !g.UI.IsInventoryVisible && !g.UI.HasDailyReport, "Island music and preloaded hidden panels");
                    MusicTick(g); Require(Music(g).Count(s => s.clip != null) == 1, "Previous menu track stops after fade"); Sources(g);
                    var current = Music(g).Single(s => s.clip != null); float time = current.time;
                    g.Audio.SetMusic(AudioCue.IslandMusic); Require(current.clip != null && current.time >= time, "Repeated music request does not restart track");
                    int before = g.Audio.PlayedSoundCount; g.ToggleInventory(); Require(g.Audio.LastSound == AudioCue.UiOpen && g.Audio.PlayedSoundCount == before + 1, "Inventory open sound");
                    g.UI.SetInventoryVisible(true); Require(g.Audio.PlayedSoundCount == before + 1, "Repeated visibility request is silent"); g.CloseInventory();
                    Require(g.Audio.LastSound == AudioCue.UiClose, "Inventory close sound");
                    g.Audio.BindGameplay(g.Tools, g.Shop); g.Audio.BindGameplay(g.Tools, g.Shop);
                    Plot(g, 0); g.Tools.SelectTool(FarmTool.Hoe); Sound(g, AudioCue.Hoe, () => Require(g.Tools.TryAct(0, out _), "Till"));
                    Silent(g, () => Require(!g.Tools.TryAct(0, out _), "Action lock rejects repeat")); Phase(3);
                }
                else if (p == 3 && !g.Tools.Busy)
                {
                    Silent(g, () => Require(!g.Tools.TryAct(0, out _), "Already tilled"));
                    g.Tools.SelectTool(FarmTool.Seeds); Sound(g, AudioCue.Plant, () => Require(g.Tools.TryAct(0, out _), "Plant")); Phase(4);
                }
                else if (p == 4 && !g.Tools.Busy)
                { g.Tools.SelectTool(FarmTool.WateringCan); Sound(g, AudioCue.Water, () => Require(g.Tools.TryAct(0, out _), "Water")); Phase(5); }
                else if (p == 5 && !g.Tools.Busy)
                {
                    Silent(g, () => Require(!g.Tools.TryAct(0, out _), "Already watered"));
                    g.Farm.State.Cells[0].Growth = 3; g.Farm.Grid.Refresh(); Sound(g, AudioCue.Harvest, () => Require(g.Tools.TryAct(0, out _), "Harvest")); Phase(6);
                }
                else if (p == 6 && !g.Tools.Busy)
                {
                    g.Inventory.TryAdd("basic_fertilizer", 1); g.Tools.SelectTool(FarmTool.Fertilizer);
                    Sound(g, AudioCue.Fertilize, () => Require(g.Tools.TryAct(0, out _), "Fertilize empty tilled soil")); Phase(7);
                }
                else if (p == 7 && !g.Tools.Busy)
                {
                    Silent(g, () => Require(!g.Tools.TryAct(0, out _), "Already fertilized"));
                    g.Players.RestorePosition(g.Gathering.Nodes[0].transform.position + new Vector3(0, .1f, -1));
                    g.Tools.SelectTool(FarmTool.WateringCan); Silent(g, () => Require(!g.Tools.TryGather(0, out _), "Wrong gathering tool"));
                    g.Tools.SelectTool(FarmTool.Axe); Sound(g, AudioCue.Axe, () => Require(g.Tools.TryGather(0, out _), "Chop")); Phase(8);
                }
                else if (p == 8 && !g.Tools.Busy)
                {
                    int index = g.Configuration.Gathering.Nodes.FindIndex(n => n.TypeId == "rock");
                    g.Players.RestorePosition(g.Gathering.Nodes[index].transform.position + new Vector3(0, .1f, -1)); g.Tools.SelectTool(FarmTool.Pickaxe);
                    Sound(g, AudioCue.Pickaxe, () => Require(g.Tools.TryGather(index, out _), "Mine")); Phase(9);
                }
                else if (p == 9 && !g.Tools.Busy)
                { g.Players.RestorePosition(g.Shop.Counter.position + new Vector3(0, .1f, -1)); g.OpenShop(); Phase(10); }
                else if (p == 10 && View<ShopView>() != null)
                {
                    g.Economy.Restore(0); Silent(g, () => Require(g.TradeShop(true) == TradeResult.InsufficientGold, "Unaffordable trade"));
                    g.Economy.TryEarn(100); Sound(g, AudioCue.Trade, () => Require(g.TradeShop(true) == TradeResult.Success, "Buy"));
                    g.Inventory.TryAdd("turnip", 1);
                    Sound(g, AudioCue.Trade, () => Require(g.TradeShop(false) == TradeResult.Success, "Sell crop"));
                    g.CloseShop(); NoSave(g);
                    g.Players.RestorePosition(g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); g.EnterHome(); Phase(11);
                }
                else if (p == 11 && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    Require(g.Audio.CurrentMusic == AudioCue.HomeMusic, "Home switches music");
                    MusicTick(g, .2f); Require(Music(g).Any(s => s.volume > 0), "Crossfade preserves audible music"); MusicTick(g);
                    Require(Music(g).Count(s => s.clip != null) == 1 && Music(g).Single(s => s.clip != null).clip.name == "HomeMusic", "Home fade completes and releases previous playback");
                    g.OpenPause(); Phase(12);
                }
                else if (p == 12 && View<PauseView>() != null)
                {
                    Require(g.Audio.CurrentMusic == AudioCue.HomeMusic, "Pause retains home music"); MusicTick(g);
                    Silent(g, () => Require(!g.Tools.TryAct(0, out _), "Paused tool request")); g.ResumeFromPause();
                    g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin + new Vector3(0, .1f, -3.6f)); g.ExitHome(); Phase(13);
                }
                else if (p == 13 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                {
                    Require(g.Audio.CurrentMusic == AudioCue.IslandMusic, "Leaving home restores island music");
                    g.ReturnToMainMenu(); Phase(14);
                }
                else if (p == 14 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(15); }
                else if (p == 15 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Audio.CurrentMusic == AudioCue.MenuMusic && g.Audio.Ready && g.Audio.OwnedHandleCount == Enum.GetValues(typeof(AudioCue)).Length, "Menu reuses clips without new loads"); MusicTick(g); Sources(g); NoSave(g);
                    bad = Object.Instantiate(g.Configuration.Audio); bad.Clips[2].Address = "Audio_Verification_Missing";
                    failure = new GameObject("Audio failure fixture").AddComponent<AudioManager>(); failure.StartCoroutine(failure.Initialize(bad)); Phase(16);
                }
                else if (p == 16 && failure.LastError != null)
                {
                    Require(!failure.Ready && failure.OwnedHandleCount == 0 && failure.LoadedClipCount == 0 && failure.GetComponentsInChildren<AudioSource>().Length == 0, "Partial AA failure clears all handles and sources");
                    failure.StartCoroutine(failure.Initialize(g.Configuration.Audio)); failure.Clear(); Phase(17);
                }
                else if (p == 17)
                {
                    Require(!failure.Ready && failure.OwnedHandleCount == 0 && failure.GetComponentsInChildren<AudioSource>().Length == 0, "Cancelled loading cannot resurrect audio");
                    Object.Destroy(failure.gameObject); Object.Destroy(bad); g.Audio.Clear();
                    Require(!g.Audio.Ready && g.Audio.OwnedHandleCount == 0, "Full clear releases cached clips"); Phase(18);
                }
                else if (p == 18) { g.Audio.StartCoroutine(g.Audio.Initialize(g.Configuration.Audio)); Phase(19); }
                else if (p == 19 && g.Audio.Ready) { g.Audio.SetMusic(AudioCue.MenuMusic); Sources(g); g.EnterGame(); Phase(20); }
                else if (p == 20 && g.State == GameFlowState.Playing)
                {
                    Require(g.Audio.CurrentMusic == AudioCue.IslandMusic, "Reloaded audio follows new game"); Plot(g, 0); g.Tools.SelectTool(FarmTool.Hoe);
                    Sound(g, AudioCue.Hoe, () => Require(g.Tools.TryAct(0, out _), "Fresh gameplay event binding")); Sources(g);
                    g.Settings.Initialize(Path.Combine(SessionState.GetString(Key + ".Dir", ""), "Settings")); Require(Mathf.Approximately(AudioListener.volume, .37f), "Master volume reload persists separately");
                    NoSave(g); Phase(21);
                }
                else if (p == 21 && !g.Tools.Busy) { g.ReturnToMainMenu(); Phase(22); }
                else if (p == 22 && View<PauseView>() != null)
                {
                    View<PauseView>().ReturnMenuButton.onClick.Invoke();
                    File.WriteAllText("Logs/AudioVerification.txt", "PASS: complete WAV/AA catalog; waveform/loop validation; committed tool/trade effects; UI bindings; bounded sources; crossfade; mute/persistence; pause/home/menu; failed/cancelled loads and reload; no daytime progress saves.\n");
                    Debug.Log("Audio flow verification PASSED."); Phase(23); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.isPlaying = false; EditorApplication.Exit(1); }
        }
    }
}
