using UnityEngine;

namespace MagicIsland.Interaction
{
    public sealed class BeachPickup : MonoBehaviour
    {
        public string Id { get; private set; }
        public string ItemId { get; private set; }
        public Collider Target;
        public void Bind(string id,string itemId){Id=id;ItemId=itemId;}
        public static readonly string[] Items={"beach_shell","driftwood","sea_glass"};
        public static string Address(string itemId)=>itemId=="beach_shell"?"BeachShellPickup":itemId=="driftwood"?"BeachDriftwood":itemId=="sea_glass"?"BeachSeaGlass":null;
    }
}
