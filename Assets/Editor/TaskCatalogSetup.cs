using MagicIsland.Configuration;
using MagicIsland.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class TaskCatalogSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            const string path = "Assets/Config/TaskCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<TaskCatalog>(path) == null)
            {
                var catalog = ScriptableObject.CreateInstance<TaskCatalog>();
                catalog.Tasks.Add(Task("first_hoe", "第一次耕地", "选择锄头，靠近农田，用鼠标点击一块空地。", TaskAction.Hoe, 5));
                catalog.Tasks.Add(Task("first_plant", "播下第一颗种子", "选择适合当季的种子，用鼠标点击已耕好的土地。", TaskAction.Plant, 10));
                catalog.Tasks.Add(Task("first_water", "照料小苗", "选择水壶，给一株尚未浇水的作物浇水。", TaskAction.Water, 10));
                catalog.Tasks.Add(Task("first_sleep", "迎接新的一天", "到蓝色被子的床边，点击床并确认睡觉。", TaskAction.Sleep, 15));
                catalog.Tasks.Add(Task("first_harvest", "第一份收成", "每天浇水，等待成熟后，用鼠标点击作物收获。", TaskAction.Harvest, 20));
                catalog.Tasks.Add(Task("first_sell", "分享丰收", "到黄色棚顶的商店，出售一个收获的作物。", TaskAction.Sell, 25));
                AssetDatabase.CreateAsset(catalog, path);
            }
            string guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != TaskCatalog.Address) entry.SetAddress(TaskCatalog.Address);
        }
        private static TaskConfig Task(string id, string title, string description, TaskAction action, int reward)
            => new TaskConfig { Id = id, Title = title, Description = description, Action = action, GoldReward = reward };
    }
}
