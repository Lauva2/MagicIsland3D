using MagicIsland.Configuration;
using MagicIsland.Player;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class PlayerManager : MonoBehaviour
    {
        public PlayerMovement Player { get; private set; }
        public void Initialize(PlayerMovement player, GameConfig config) { Player = player; player.Speed = config.PlayerSpeed; }
        public void SetInputEnabled(bool enabled) { if (Player != null) Player.enabled = enabled; }
        public void Clear() => Player = null;
        public void RestorePosition(Vector3 position)
        {
            CharacterController character = Player.GetComponent<CharacterController>();
            bool wasEnabled = character.enabled;
            character.enabled = false;
            Player.transform.position = position;
            character.enabled = wasEnabled;
        }
    }
}
