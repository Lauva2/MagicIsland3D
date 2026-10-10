using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class ToolUpgradeSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup configuration)
        {
            const string path = "Assets/Config/ToolUpgradeCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<ToolUpgradeCatalog>(path) == null)
            {
                var catalog = ScriptableObject.CreateInstance<ToolUpgradeCatalog>();
                catalog.Upgrades.Add(new ToolUpgradeConfig { Tool = FarmTool.Axe, DisplayName = "斧头", Wood = 6, Stone = 2, Gold = 50 });
                catalog.Upgrades.Add(new ToolUpgradeConfig { Tool = FarmTool.Pickaxe, DisplayName = "镐子", Wood = 3, Stone = 4, Gold = 75 });
                AssetDatabase.CreateAsset(catalog, path);
            }
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), configuration);
            if (entry.address != ToolUpgradeCatalog.Address) entry.SetAddress(ToolUpgradeCatalog.Address);
            CreateWorkbench();
            const string benchPath = "Assets/Art/Prefabs/Buildings/ToolWorkbench.prefab";
            var benchEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(benchPath)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(benchPath), settings.FindGroup("Buildings"));
            if (benchEntry.address != "ToolWorkbench") benchEntry.SetAddress("ToolWorkbench");
        }
        public static void CreatePanel()
        {
            string path = Factory.Folder + "/ToolUpgradePanel.prefab"; if (File.Exists(path)) return;
            var root = Factory.CanvasRoot("ToolUpgradePanel");
            try
            {
                var view = root.AddComponent<ToolUpgradeView>(); view.Layer = UILayer.PopUp;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.12f, 0.2f, 0.15f, 0.58f));
                shade.anchorMax = Vector2.one; shade.GetComponent<Image>().sprite = null;
                var panel = Factory.Panel("Upgrade Card", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(720, 480), Factory.Cream); Factory.SoftShadow(panel);
                Factory.Label("Title", panel, new Vector2(32, -22), new Vector2(560, 44), "岛屿工具工作台", 30);
                view.CloseButton = Factory.Button("Close", panel, new Vector2(634, -24), new Vector2(54, 42), "×", Factory.Sand);
                view.BalanceText = Factory.Label("Balance", panel, new Vector2(32, -80), new Vector2(656, 32), "木材 0 · 石料 0 · 金币 100", 20);
                view.TitleTexts = new TMP_Text[2]; view.CostTexts = new TMP_Text[2]; view.EffectTexts = new TMP_Text[2]; view.UpgradeButtons = new Button[2];
                for (int i = 0; i < 2; i++)
                {
                    var card = Factory.Panel("Tool " + i, panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32 + i * 336, -132), new Vector2(320, 228), Factory.Sand);
                    view.TitleTexts[i] = Factory.Label("Name", card, new Vector2(20, -18), new Vector2(280, 36), i == 0 ? "斧头 · 普通" : "镐子 · 普通", 25);
                    view.CostTexts[i] = Factory.Label("Cost", card, new Vector2(20, -66), new Vector2(280, 30), "木材 6 · 石料 2 · 50 金币", 18);
                    view.EffectTexts[i] = Factory.Label("Effect", card, new Vector2(20, -106), new Vector2(280, 40), "砍树 3 次 → 2 次 · 每次 4 体力", 17);
                    view.UpgradeButtons[i] = Factory.Button("Upgrade", card, new Vector2(20, -166), new Vector2(280, 44), "升级工具", new Color(.70f, .81f, .62f));
                }
                view.FeedbackText = Factory.Label("Feedback", panel, new Vector2(32, -380), new Vector2(656, 28), "在工作台强化工具，轻松采集岛屿材料", 18);
                view.SaveButton = Factory.Button("Save Progress", panel, new Vector2(488, -424), new Vector2(200, 36), "保存进度", Factory.Sand);
                Factory.Label("Hint", panel, new Vector2(32, -426), new Vector2(430, 26), "立即升级 · 自动保存 · ESC 关闭", 17);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
        }
        private static void CreateWorkbench()
        {
            const string path = "Assets/Art/Prefabs/Buildings/ToolWorkbench.prefab"; if (File.Exists(path)) return;
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Wood.mat");
            var sage = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Sage.mat");
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Stone.mat");
            var paper = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Board Paper.mat");
            var root = new GameObject("ToolWorkbench", typeof(ToolWorkbench));
            try
            {
                Box(root.transform, "Top", new Vector3(0, .82f, 0), new Vector3(1.6f, .16f, .8f), wood);
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                    Box(root.transform, "Leg", new Vector3(x * .62f, .36f, z * .27f), new Vector3(.13f, .72f, .13f), sage);
                Box(root.transform, "Shelf", new Vector3(0, .24f, 0), new Vector3(1.4f, .1f, .65f), wood);
                Box(root.transform, "Tool Rack", new Vector3(0, 1.2f, .31f), new Vector3(1.5f, .62f, .09f), sage);
                Box(root.transform, "Axe Handle", new Vector3(-.45f, 1.14f, .20f), new Vector3(.06f, .40f, .06f), wood);
                Box(root.transform, "Axe Head", new Vector3(-.37f, 1.33f, .18f), new Vector3(.24f, .13f, .10f), stone);
                Box(root.transform, "Pick Handle", new Vector3(.30f, 1.13f, .20f), new Vector3(.06f, .40f, .06f), wood);
                Box(root.transform, "Pick Head", new Vector3(.30f, 1.33f, .18f), new Vector3(.37f, .08f, .10f), stone);
                Box(root.transform, "Plans", new Vector3(.22f, .91f, -.12f), new Vector3(.45f, .02f, .32f), paper);
                var label = new GameObject("Workbench Name", typeof(TextMeshPro)); label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(0, .71f, -.412f);
                var text = label.GetComponent<TextMeshPro>(); text.font = UiPrefabMigration.FontAsset; text.text = "工具工作台"; text.fontSize = 1.4f;
                text.rectTransform.sizeDelta = new Vector2(1.4f, .15f); text.alignment = TextAlignmentOptions.Center; text.color = new Color(1, .97f, .83f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
