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
        public bool IsBusy { get; private set; }
        public string LastError { get; private set; }
        private AsyncOperationHandle<SceneInstance> handle;
        private bool ownsScene;

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
