using System;
using System.IO;
using MagicIsland.Saving;
using MagicIsland.Settings;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class SettingsManager : MonoBehaviour
    {
        private SavedSettings state = new SavedSettings();
        private SaveFileStore store;
        private bool dirty;
        private float changedAt;
        public float Volume => state.Volume;
        public bool Fullscreen => state.Fullscreen;
        public string Feedback { get; private set; }
        public string SettingsPath => store?.FilePath;
        public void Initialize(string directory = null)
        {
            store = new SaveFileStore(directory ?? Path.Combine(Application.persistentDataPath, "Settings"), "settings.json");
            state = new SavedSettings { Fullscreen = Screen.fullScreen };
            if (store.TryRead(json => { var s = JsonUtility.FromJson<SavedSettings>(json); if (!SettingsState.Valid(s)) throw new ArgumentException("Invalid settings"); return s; }, out var saved, out var error, out var recovered))
            { state = saved; Feedback = recovered ? "已从备份恢复设置" : "设置自动保存，与游戏进度独立"; }
            else Feedback = error == null ? "设置自动保存，与游戏进度独立" : "设置文件无法读取，已使用默认设置";
            dirty = false; Apply();
        }
        private void Apply() { AudioListener.volume = state.Volume; Screen.fullScreen = state.Fullscreen; }
        public void SetVolume(float value) { state.Volume = SettingsState.Clamp(value); AudioListener.volume = state.Volume; MarkDirty(); }
        public void SetFullscreen(bool value) { state.Fullscreen = value; Screen.fullScreen = value; MarkDirty(); }
        private void MarkDirty() { dirty = true; changedAt = UnityEngine.Time.unscaledTime; Feedback = "设置已应用，正在保存…"; }
        public bool Flush()
        {
            if (!dirty) return true;
            try { if (store == null) throw new InvalidOperationException("Settings not initialized"); store.Write(JsonUtility.ToJson(state, true)); dirty = false; Feedback = "设置已保存，下次启动自动恢复"; return true; }
            catch (Exception) { changedAt = UnityEngine.Time.unscaledTime; Feedback = "设置已应用，保存失败，关闭设置时会重试"; return false; }
        }
        private void Update() { if (dirty && UnityEngine.Time.unscaledTime - changedAt >= .5f) Flush(); }
        private void OnApplicationQuit() => Flush();
    }
}
