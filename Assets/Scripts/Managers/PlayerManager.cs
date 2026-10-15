using MagicIsland.Configuration;
using MagicIsland.Player;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagicIsland.Managers
{
    public sealed class PlayerManager : MonoBehaviour
    {
        public PlayerMovement Player { get; private set; }
        public string LastError { get; private set; }
        public bool OwnsPlayer => handle.IsValid();
        private AsyncOperationHandle<GameObject> handle;
        private int generation;
        public IEnumerator LoadPlayer(GameConfig config, Transform world)
        {
            Clear(); int current = generation; LastError = null;
            if (config == null || world == null) { LastError = "角色加载缺少配置或农场场景"; yield break; }
            handle = Addressables.InstantiateAsync(config.PlayerPrefabAddress, world); yield return handle;
            if (current != generation) yield break;
            var player = handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null ? handle.Result.GetComponent<PlayerMovement>() : null;
            if (player == null || player.Visual == null || player.Visual == player.transform || !player.Visual.IsChildOf(player.transform)
                || player.GetComponent<CharacterController>() == null || player.Animation == null || !player.Animation.IsValid
                || !player.Animation.Animator.transform.IsChildOf(player.Visual))
            { Clear(); LastError = "角色 AA 预制体缺少移动、碰撞、模型或动画配置"; yield break; }
            Player = player; Player.enabled = false; Player.Animation.Initialize(); Player.Animation.SetPaused(true);
            Player.Speed = config.PlayerSpeed; Player.transform.localPosition = new Vector3(0, .1f, -4.6f);
        }
        public void Initialize(PlayerMovement player, GameConfig config) { Player = player; player.Speed = config.PlayerSpeed; }
        public void SetInputEnabled(bool enabled) { if (Player != null) { Player.enabled = enabled; Player.Animation.SetPaused(!enabled); } }
        public void Clear()
        {
            generation++; if (Player != null) { Player.enabled = false; Player.Animation?.SetPaused(true); }
            Player = null;
            if (handle.IsValid()) { if (handle.IsDone) Release(handle); else handle.Completed += Release; }
            handle = default;
        }
        private static void Release(AsyncOperationHandle<GameObject> operation)
        { if (!operation.IsValid()) return; if (operation.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(operation); else Addressables.Release(operation); }
        public void RestorePosition(Vector3 position)
        {
            CharacterController character = Player.GetComponent<CharacterController>();
            bool wasEnabled = character.enabled;
            character.enabled = false;
            Player.transform.position = position;
            Player.ResetMotion();
            character.enabled = wasEnabled;
        }
        private void OnDestroy() => Clear();
    }
}
