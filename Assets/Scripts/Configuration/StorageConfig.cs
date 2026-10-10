using UnityEngine;
namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Storage Config")]
    public sealed class StorageConfig : ScriptableObject
    {
        public const string Address = "StorageConfig";
        [Range(1, 36)] public int Slots = 18;
        public string ChestAddress = "StorageChest";
        public bool IsValid => Slots >= 1 && Slots <= 36 && !string.IsNullOrWhiteSpace(ChestAddress);
    }
}
