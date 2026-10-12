using System.IO;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class HouseUpgradeSetup
    {
        public static void Ensure(AddressableAssetSettings settings)
        {
            var config = AssetDatabase.LoadAssetAtPath<HousingConfig>("Assets/Config/HousingConfig.asset");
            EditorUtility.SetDirty(config);
            string exterior = "Assets/Art/Prefabs/Buildings/PlayerHouseExpansion.prefab", interior = "Assets/Art/Prefabs/Buildings/PlayerHomeExpansion.prefab";
            CreateExterior(exterior); CreateInterior(interior, config.InteriorLayer);
            Register(settings, exterior, config.ExteriorUpgradeAddress); Register(settings, interior, config.InteriorUpgradeAddress); UpgradeInteriorReferences();
        }
        static void Register(AddressableAssetSettings settings, string path, string address)
        { var guid = AssetDatabase.AssetPathToGUID(path); var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, settings.FindGroup("Buildings")); if (entry.address != address) entry.SetAddress(address); }
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/" + name + ".mat");
        static GameObject Box(Transform root, string name, Vector3 position, Vector3 size, Material material, bool solid = false)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.transform.SetParent(root, false); part.transform.localPosition = position; part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material; if (!solid) Object.DestroyImmediate(part.GetComponent<Collider>()); return part;
        }
        static void CreateExterior(string path)
        {
            if (File.Exists(path)) return; var root = new GameObject("PlayerHouseExpansion");
            try
            {
                var cream = Mat("Board Paper"); var wood = Mat("Hall Wood"); var sage = Mat("Hall Sage");
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(root.transform, "Roof Trim", new Vector3(side * .39f, 2.48f, -1.17f), new Vector3(.94f, .09f, .09f), cream).transform.localRotation = Quaternion.Euler(0, 0, side * -27);
                    Box(root.transform, "Door Side Trim", new Vector3(side * .45f, .95f, -1.04f), new Vector3(.07f, 1.91f, .09f), cream);
                    Box(root.transform, "Roof Bracket", new Vector3(side * .57f, 1.97f, -1.05f), new Vector3(.09f, .4f, .12f), wood);
                }
                Box(root.transform, "Roof Ridge", new Vector3(0, 2.69f, 0), new Vector3(.12f, .1f, 2.4f), cream);
                Box(root.transform, "Door Crown", new Vector3(0, 1.94f, -1.04f), new Vector3(.95f, .1f, .09f), cream);
                Box(root.transform, "Side Shutter", new Vector3(.659f, 1.35f, .28f), new Vector3(.04f, .75f, .83f), sage);
                Box(root.transform, "Side Window", new Vector3(.686f, 1.35f, .28f), new Vector3(.02f, .6f, .67f), cream);
                root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void CreateInterior(string path, int layer)
        {
            if (File.Exists(path)) return; var root = new GameObject("PlayerHomeExpansion");
            try
            {
                var wood = Mat("Hall Wood"); var cream = Mat("Hall Cream");
                Box(root.transform, "East Floor", new Vector3(8, -.16f, 1), new Vector3(4, .32f, 12), wood, true);
                Box(root.transform, "North Floor", new Vector3(0, -.16f, 6), new Vector3(12, .32f, 2), wood, true);
                Box(root.transform, "Expanded Back Wall", new Vector3(2, 1.6f, 7), new Vector3(16, 3.2f, .2f), cream, true);
                Box(root.transform, "Expanded Left Wall", new Vector3(-6, 1.6f, 1), new Vector3(.2f, 3.2f, 12), cream, true);
                Box(root.transform, "Expanded Right Wall", new Vector3(10, 1.6f, 1), new Vector3(.2f, 3.2f, 12), cream, true);
                var front = Box(root.transform, "Front Collision", new Vector3(2, 1.6f, -5), new Vector3(16, 3.2f, .2f), cream, true); front.GetComponent<Renderer>().enabled = false; front.layer = 2;
                for (int x = -5; x <= 9; x++)
                {
                    float center = x < 6 ? 6 : 1, length = x < 6 ? 1.8f : 11.8f;
                    Box(root.transform, "Floor Inlay", new Vector3(x, .007f, center), new Vector3(.015f, .009f, length), cream);
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.gameObject.layer != 2) t.gameObject.layer = layer;
                root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void UpgradeInteriorReferences()
        {
            const string path = "Assets/Art/Prefabs/Buildings/PlayerHomeInterior.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var room = root.GetComponent<HomeInterior>(); bool changed = false;
                if (room.OriginalWalls == null || room.OriginalWalls.Length == 0)
                { room.OriginalWalls = new[] { "Back Wall", "Left Wall", "Right Wall", "Front Collision" }.Select(n => root.transform.Find(n)?.gameObject).ToArray(); changed = true; }
                if (room.Windows == null || room.Windows.Length == 0)
                { room.Windows = new[] { "Window Frame", "Window Light", "Window Cross", "Window Bar" }.Select(n => root.transform.Find(n)).ToArray(); changed = true; }
                if (room.Skirting == null) { room.Skirting = root.transform.Find("Back Skirting"); changed = true; }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void CreatePanel()
        {
            string path = Factory.Folder + "/HouseUpgradePanel.prefab";
            if (!File.Exists(path))
            {
                var root = Factory.CanvasRoot("HouseUpgradePanel");
                try
                {
                    var view = root.AddComponent<HouseUpgradeView>(); view.Layer = UILayer.PopUp;
                    var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .2f, .15f, .58f)); shade.anchorMax = Vector2.one; shade.GetComponent<UnityEngine.UI.Image>().sprite = null;
                    var card = Factory.Panel("House Upgrade Card", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(680, 500), Factory.Cream); Factory.SoftShadow(card);
                    Factory.Label("Title", card, new Vector2(32, -20), new Vector2(540, 44), "岛屿办事处 · 小屋扩建", 30);
                    view.CloseButton = Factory.Button("Close", card, new Vector2(594, -22), new Vector2(54, 42), "×", Factory.Sand);
                    Factory.Label("Description", card, new Vector2(32, -84), new Vector2(616, 64), "室内 12 × 10 → 16 × 12 米\n增加右侧和后侧空间，保留家具、床与出口", 21);
                    view.CostText = Factory.Label("Costs", card, new Vector2(32, -174), new Vector2(616, 36), "木材 × 20    石料 × 10    金币 300", 21);
                    view.OwnedText = Factory.Label("Owned", card, new Vector2(32, -222), new Vector2(616, 60), "背包持有：木材 0 / 石料 0\n当前金币 100", 20);
                    view.StatusText = Factory.Label("Status", card, new Vector2(32, -302), new Vector2(616, 36), "初始小屋 → 舒适小屋 · 施工 1 天", 21);
                    view.FeedbackText = Factory.Label("Feedback", card, new Vector2(32, -354), new Vector2(616, 52), "材料与金币备齐后，确认支付并申请扩建", 19);
                    view.ApplyButton = Factory.Button("Apply", card, new Vector2(32, -434), new Vector2(402, 42), "确认支付并申请", new Color(.7f, .81f, .62f));
                    view.SaveButton = Factory.Button("Save Progress", card, new Vector2(456, -434), new Vector2(192, 42), "保存进度", Factory.Sand);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { Object.DestroyImmediate(root); }
            }
            path = Factory.Folder + "/OrderPanel.prefab"; if (!File.Exists(path)) return; var orderRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = orderRoot.GetComponent<OrderView>(); if (view.HouseUpgradeButton != null) return;
                view.HouseUpgradeButton = Factory.Button("House Upgrade", view.DeliverButton.transform.parent, new Vector2(464, -362), new Vector2(144, 32), "小屋扩建", Factory.Sand);
                view.HouseUpgradeButton.GetComponentInChildren<TMP_Text>().fontSize = 18;
                view.FeedbackText.rectTransform.sizeDelta = new Vector2(420, view.FeedbackText.rectTransform.sizeDelta.y);
                PrefabUtility.SaveAsPrefabAsset(orderRoot, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(orderRoot); }
        }
    }
}
