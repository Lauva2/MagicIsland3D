using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MagicIsland.Managers
{
    public sealed class SceneManager : MonoBehaviour
    {
        public Scene GameScene { get; private set; }
        public Scene HomeScene { get; private set; }
        public bool IsBusy { get; private set; }
        public string LastError { get; private set; }
        private AsyncOperationHandle<SceneInstance> handle;
        private bool ownsScene;
        private AsyncOperationHandle<SceneInstance> homeHandle;

        public IEnumerator LoadHomeScene(string address)
        {
            LastError = null;
            if (HomeScene.IsValid() && HomeScene.isLoaded) yield break;
            if (IsBusy) { LastError = "正在切换场景，请稍候"; yield break; }
            IsBusy = true; homeHandle = Addressables.LoadSceneAsync(address, LoadSceneMode.Additive); yield return homeHandle;
            if (homeHandle.Status == AsyncOperationStatus.Succeeded) HomeScene = homeHandle.Result.Scene;
            else { LastError = "小屋场景暂时无法加载，请重试"; if (homeHandle.IsValid()) Addressables.Release(homeHandle); homeHandle = default; }
            IsBusy = false;
        }
        public IEnumerator UnloadHomeScene()
        {
            LastError = null;
            if (!homeHandle.IsValid()) { HomeScene = default; yield break; }
            if (IsBusy) { LastError = "正在切换场景，请稍候"; yield break; }
            IsBusy = true; var unload = Addressables.UnloadSceneAsync(homeHandle, false); yield return unload;
            if (unload.Status != AsyncOperationStatus.Succeeded) LastError = "小屋场景暂时无法卸载，请重试";
            Addressables.Release(unload);
            if (LastError == null) { HomeScene = default; homeHandle = default; }
            IsBusy = false;
        }

        public void UseEditorScene(Scene scene) => GameScene = scene;

        public IEnumerator LoadGameScene(string address)
        {
            if (IsBusy || GameScene.IsValid()) yield break;
            IsBusy = true;
            LastError = null;
            handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Additive);
            yield return handle;
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                ownsScene = true;
                GameScene = handle.Result.Scene;
                UnitySceneManager.SetActiveScene(GameScene);
            }
            else
            {
                LastError = handle.OperationException?.Message ?? "Scene loading failed";
                if (handle.IsValid()) Addressables.Release(handle);
            }
            IsBusy = false;
        }

        public IEnumerator UnloadGameScene()
        {
            if (IsBusy) yield break;
            yield return UnloadHomeScene();
            if (LastError != null) yield break;
            IsBusy = true;
            LastError = null;
            if (ownsScene && handle.IsValid())
            {
                var unload = Addressables.UnloadSceneAsync(handle, false);
                yield return unload;
                if (unload.Status != AsyncOperationStatus.Succeeded)
                    LastError = unload.OperationException?.Message ?? "Scene unloading failed";
                Addressables.Release(unload);
                if (LastError != null) { IsBusy = false; yield break; }
            }
            else if (GameScene.IsValid() && GameScene.isLoaded)
            {
                // Only used when directly playing MainGame in the Editor.
                var unload = UnitySceneManager.UnloadSceneAsync(GameScene);
                if (unload != null) yield return unload;
            }
            ownsScene = false;
            GameScene = default;
            IsBusy = false;
        }

        private void OnDestroy()
        {
            if (homeHandle.IsValid())
            {
                if (!homeHandle.IsDone) homeHandle.Completed += h => { if (h.Status == AsyncOperationStatus.Succeeded) Addressables.UnloadSceneAsync(h); else if (h.IsValid()) Addressables.Release(h); };
                else Addressables.UnloadSceneAsync(homeHandle);
            }
            if (!handle.IsValid()) return;
            if (!handle.IsDone)
                handle.Completed += operation =>
                {
                    if (operation.Status == AsyncOperationStatus.Succeeded) Addressables.UnloadSceneAsync(operation);
                    else if (operation.IsValid()) Addressables.Release(operation);
                };
            else if (ownsScene) Addressables.UnloadSceneAsync(handle);
        }
    }
}
