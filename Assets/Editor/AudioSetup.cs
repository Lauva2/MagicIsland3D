using System;
using System.IO;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.UI;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.EditorTools
{
    public static class AudioSetup
    {
        public static string ClipPath(AudioCue cue) => "Assets/Art/Audio/" + (AudioCatalog.IsMusic(cue) ? "Music/" : AudioCatalog.IsEnvironment(cue)?"Environment/":"Effects/") + cue + ".wav";
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup configGroup)
        {
            const string path = "Assets/Config/AudioCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalog>();
                foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue))) catalog.Clips.Add(new AudioClipConfig { Cue = cue, Address = "Audio_" + cue });
                AssetDatabase.CreateAsset(catalog, path);
            }
            foreach(AudioCue cue in Enum.GetValues(typeof(AudioCue)))
                if(catalog.Find(cue)==null){catalog.Clips.Add(new AudioClipConfig{Cue=cue,Address="Audio_"+cue});EditorUtility.SetDirty(catalog);}
            if(!File.ReadAllText(path).Contains("ShoreHearingDistance:"))EditorUtility.SetDirty(catalog);
            var config = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), configGroup);
            if (config.address != AudioCatalog.Address) config.SetAddress(AudioCatalog.Address);
            var group = settings.FindGroup("Audio") ?? settings.CreateGroup("Audio", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            foreach (var clip in catalog.Clips)
            {
                string clipPath = ClipPath(clip.Cue);
                var configured = settings.groups.Where(g => g != null).SelectMany(g => g.entries).FirstOrDefault(e => e.address == clip.Address);
                if (configured != null && configured.guid != AssetDatabase.AssetPathToGUID(clipPath)) continue;
                if (!File.Exists(clipPath)) throw new InvalidOperationException("Missing prototype audio. Run "
                    +(AudioCatalog.IsEnvironment(clip.Cue)?"Tools/GenerateCoastalAudio.py":"Tools/GeneratePrototypeAudio.py")+": " + clipPath);
                var importer = AssetImporter.GetAtPath(clipPath) as AudioImporter;
                const string marker = "MagicIsland.PrototypeAudioV1";
                if (importer != null && !(importer.userData ?? "").Contains(marker))
                {
                    bool music = AudioCatalog.IsMusic(clip.Cue)||AudioCatalog.IsAmbient(clip.Cue);
                    var sample = importer.defaultSampleSettings;
                    sample.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                    sample.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                    sample.quality = .85f; sample.preloadAudioData = !music; importer.defaultSampleSettings = sample;
                    importer.forceToMono = true;
                    importer.userData += marker; importer.SaveAndReimport();
                }
                string guid = AssetDatabase.AssetPathToGUID(clipPath);
                var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
                if (entry.address != clip.Address) entry.SetAddress(clip.Address);
            }
            foreach (string prefabPath in Directory.GetFiles(PrototypeUiPrefabBuilder.Folder, "*.prefab"))
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath.Replace('\\', '/'));
                try
                {
                    bool changed = false;
                    foreach (var button in root.GetComponentsInChildren<Button>(true))
                        if (button.GetComponent<UIButtonAudio>() == null) { button.gameObject.AddComponent<UIButtonAudio>(); changed = true; }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, prefabPath.Replace('\\', '/'));
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
}
