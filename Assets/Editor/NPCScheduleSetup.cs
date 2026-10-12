using MagicIsland.Configuration;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class NPCScheduleSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            const string path = "Assets/Config/NPCScheduleCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<NPCScheduleCatalog>(path) == null)
            {
                var catalog = ScriptableObject.CreateInstance<NPCScheduleCatalog>();
                foreach (var npc in AssetDatabase.LoadAssetAtPath<NPCCatalog>("Assets/Config/NPCCatalog.asset").NPCs)
                    catalog.Schedules.Add(new NPCScheduleConfig { NpcId = npc.Id });
                AssetDatabase.CreateAsset(catalog, path);
            }
            var guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != NPCScheduleCatalog.Address) entry.SetAddress(NPCScheduleCatalog.Address);
        }
    }
}
