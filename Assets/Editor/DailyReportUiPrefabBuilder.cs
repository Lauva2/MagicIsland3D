using System.IO;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class DailyReportUiPrefabBuilder
    {
        public static void CreateMissing()
        {
            string path = Factory.Folder + "/DailyReportPanel.prefab"; if (File.Exists(path)) return;
            var root = Factory.CanvasRoot("DailyReportPanel");
            try
            {
                var view = root.AddComponent<DailyReportView>(); view.Layer = UILayer.PopUp;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .2f, .15f, .58f)); shade.anchorMax = Vector2.one; shade.GetComponent<UnityEngine.UI.Image>().sprite = null;
                var card = Factory.Panel("Daily Report", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(720, 568), Factory.Cream); Factory.SoftShadow(card);
                Factory.Label("Title", card, new Vector2(32, -22), new Vector2(656, 42), "一天的岛屿生活", 30);
                view.DateText = Factory.Label("Date", card, new Vector2(32, -72), new Vector2(656, 30), "第 1 年 春季 1 日 · 经营日报", 20);
                TMP_Text[] values = new TMP_Text[3];
                for (int i = 0; i < 3; i++)
                {
                    var tile = Factory.Panel("Money " + i, card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32 + 224 * i, -124), new Vector2(208, 100), i == 2 ? new Color(.78f, .85f, .69f) : Factory.Sand);
                    values[i] = Factory.Label("Value", tile, new Vector2(16, -14), new Vector2(176, 72), i == 0 ? "收入\n0 金币" : i == 1 ? "支出\n0 金币" : "金币净变化\n0 金币", 22);
                    values[i].enableAutoSizing = true; values[i].fontSizeMin = 12; values[i].fontSizeMax = 22;
                }
                view.IncomeText = values[0]; view.ExpenseText = values[1]; view.NetText = values[2];
                view.HarvestText = Factory.Label("Harvest", card, new Vector2(32, -252), new Vector2(656, 30), "当天收获  0 份作物", 22);
                view.MatureText = Factory.Label("Mature", card, new Vector2(32, -294), new Vector2(656, 30), "今晨新成熟  0 格作物", 22);
                view.EggsText = Factory.Label("Eggs", card, new Vector2(32, -336), new Vector2(656, 58), "今晨新产蛋\n普通蛋 0 枚  ·  优质蛋 0 枚", 21);
                view.FeedbackText = Factory.Label("Feedback", card, new Vector2(32, -410), new Vector2(656, 52), "辛苦了一天，休息后体力已恢复。产物仍在农田和鸡舍，记得收取。", 19);
                view.ReturnMenuButton = Factory.Button("Return Menu", card, new Vector2(32, -494), new Vector2(176, 44), "返回主菜单", Factory.Sand);
                view.SaveButton = Factory.Button("Save", card, new Vector2(224, -494), new Vector2(160, 44), "保存进度", Factory.Sand);
                view.ContinueButton = Factory.Button("Start New Day", card, new Vector2(400, -494), new Vector2(288, 44), "开始新的一天", new Color(.7f, .81f, .62f));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
