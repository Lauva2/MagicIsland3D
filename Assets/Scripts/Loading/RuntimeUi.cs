using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicIsland.Loading
{
    public static class RuntimeUi
    {
        // Addresses always match the UI prefab file names (without extension).
        public const string MenuAddress = "MainMenuPanel";
        public const string HudAddress = "FarmHudPanel";
        public const string InventoryAddress = "InventoryPanel";

        public static void EnsureEventSystem(Transform parent = null)
        {
            if (EventSystem.current == null)
            {
                GameObject events = new GameObject("UI Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                if (parent != null) events.transform.SetParent(parent, false);
            }
        }
    }
}
