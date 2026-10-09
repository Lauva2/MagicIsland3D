using System;
using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
using MagicIsland.Saving;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class SaveManager : MonoBehaviour
    {
        public bool HasSave { get; private set; }
        public string LastError { get; private set; }
        public bool RecoveredBackup { get; private set; }
        public string SavePath => store?.FilePath;
        private SaveFileStore store;
        private GameConfig config;
        private ItemCatalog catalog;

        public void Initialize(GameConfig gameConfig, ItemCatalog items, string directory = null)
        {
            config = gameConfig;
            catalog = items;
            store = new SaveFileStore(directory ?? Path.Combine(Application.persistentDataPath, "Saves"));
            RefreshAvailability();
        }

        public void RefreshAvailability() => HasSave = TryLoad(out _);
        public bool TryLoad(out SaveData data)
        {
            if (store == null) { data = null; return false; }
            HasSave = store.TryRead(json =>
            {
                SaveData parsed = JsonUtility.FromJson<SaveData>(json);
                if (parsed == null) throw new InvalidOperationException("Empty save");
                parsed.Validate(config.FarmSize, config.CropGrowthDays, new InventoryState(config.InventorySlots, catalog.Definitions()));
                return parsed;
            }, out data, out string error, out bool recovered);
            LastError = error;
            RecoveredBackup = recovered;
            return HasSave;
        }

        public bool TrySave(FarmManager farm, InventoryManager inventory, PlayerManager players)
        {
            try
            {
                Vector3 position = players.Player.transform.position;
                SaveData data = SaveData.Capture(farm.State, inventory.State, position.x, position.y, position.z);
                data.Validate(config.FarmSize, config.CropGrowthDays, new InventoryState(config.InventorySlots, catalog.Definitions()));
                store.Write(JsonUtility.ToJson(data, true));
                LastError = null;
                HasSave = true;
                RecoveredBackup = false;
                return true;
            }
            catch (Exception error) { LastError = error.Message; return false; }
        }
    }
}
