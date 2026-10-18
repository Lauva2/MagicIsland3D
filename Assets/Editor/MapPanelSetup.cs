using System.IO;
using MagicIsland.Interaction;
using MagicIsland.UI;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class MapPanelSetup
    {
        public const string Path = "Assets/Art/Prefabs/UI/MapPanel.prefab";
        public static void CreateAndUpgrade()
        {
            if (!File.Exists(Path)) Create();
            StarterIslandWorldSetup.EnsureMap();
            var mapAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            if (mapAsset.GetComponent<MapView>().MapGraphic.GetComponent<CanvasRenderer>() == null)
            {
                var mapRoot = PrefabUtility.LoadPrefabContents(Path);
                try { mapRoot.GetComponent<MapView>().MapGraphic.gameObject.AddComponent<CanvasRenderer>(); PrefabUtility.SaveAsPrefabAsset(mapRoot, Path); }
                finally { Object.DestroyImmediate(mapRoot); }
            }
            const string hudPath = "Assets/Art/Prefabs/UI/FarmHudPanel.prefab"; var hudAsset = AssetDatabase.LoadAssetAtPath<GameObject>(hudPath);
            if (hudAsset.GetComponent<PrototypeHud>().MapButton != null) return;
            var root = PrefabUtility.LoadPrefabContents(hudPath);
            try
            {
                var hud = root.GetComponent<PrototypeHud>(); hud.MapButton = Factory.Button("Map", root.transform, Vector2.zero, new Vector2(144, 42), "地图（M）", Factory.Sand);
                var rect = hud.MapButton.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-20, -132);
                PrefabUtility.SaveAsPrefabAsset(root, hudPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void Create()
        {
            var root = Factory.CanvasRoot("MapPanel");
            try
            {
                var view = root.AddComponent<MapView>(); view.Layer = UILayer.PopUp; view.SleepOnlyLayout = true;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.10f, .18f, .17f, .64f)); shade.anchorMax = Vector2.one; shade.GetComponent<Image>().sprite = null;
                var card = Factory.Panel("Map Card", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(1120, 636), Factory.Cream); Factory.SoftShadow(card);
                Factory.Label("Title", card, new Vector2(24, -18), new Vector2(500, 42), "岛屿地图", 30);
                Factory.Label("Hint", card, new Vector2(24, -61), new Vector2(512, 26), "点击地图标记或右侧地点查看用途", 18);
                view.RegionText = Factory.Label("Region", card, new Vector2(560, -61), new Vector2(530, 26), "当前位置：岛屿", 19);
                view.CloseButton = Factory.Button("Close", card, new Vector2(966, -20), new Vector2(130, 42), "关闭（M）", Factory.Sand);
                view.MapArea = Factory.Rect("Map Area", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -94), new Vector2(512, 512));
                view.MapGraphic = view.MapArea.gameObject.AddComponent<IslandMapGraphic>(); view.MapGraphic.raycastTarget = false;
                Factory.Label("Compass", view.MapArea, new Vector2(14, -10), new Vector2(74, 32), "北 ↑", 20);
                view.LocationButtons = new Button[MapView.MaximumLocations]; view.MarkerButtons = new Button[MapView.MaximumLocations]; view.LocationLabels = new TMP_Text[MapView.MaximumLocations]; view.MarkerLabels = new TMP_Text[MapView.MaximumLocations];
                for (int i = 0; i < MapView.MaximumLocations; i++)
                {
                    view.LocationButtons[i] = Factory.Button("Location " + i, card, new Vector2(560 + i / 6 * 272, -98 - i % 6 * 45), new Vector2(258, 38), (i + 1) + "  地点", Factory.Sand);
                    view.LocationLabels[i] = view.LocationButtons[i].GetComponentInChildren<TMP_Text>(); view.LocationLabels[i].fontSize = 19;
                    view.MarkerButtons[i] = Factory.Button("Marker " + i, view.MapArea, Vector2.zero, new Vector2(22, 22), (i + 1).ToString(), new Color(.26f, .42f, .30f));
                    view.MarkerButtons[i].GetComponent<RectTransform>().pivot = Vector2.one * .5f;
                    view.MarkerLabels[i] = view.MarkerButtons[i].GetComponentInChildren<TMP_Text>(); view.MarkerLabels[i].fontSize = 13; view.MarkerLabels[i].color = Factory.Cream;
                }
                view.PlayerMarker = Factory.Panel("Player", view.MapArea, new Vector2(0, 1), Vector2.one * .5f, Vector2.zero, new Vector2(24, 24), new Color(.99f, .82f, .30f));
                view.PlayerMarker.GetComponent<Image>().raycastTarget = false; Factory.SoftShadow(view.PlayerMarker);
                Factory.Label("Player Label", view.PlayerMarker, Vector2.zero, new Vector2(24, 24), "我", 14, TextAnchor.MiddleCenter);
                var details = Factory.Panel("Details", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(560, -388), new Vector2(530, 168), Factory.Sand);
                view.DetailTitle = Factory.Label("Detail Title", details, new Vector2(18, -10), new Vector2(494, 34), "农田", 25);
                view.DetailText = Factory.Label("Detail Text", details, new Vector2(18, -48), new Vector2(494, 110), "翻土、播种、浇水与收获。", 19);
                Factory.Label("Legend", card, new Vector2(24, -609), new Vector2(512, 22), "金色“我”为当前位置 · 北方在上", 16);
                Factory.Label("Controls", card, new Vector2(560, -572), new Vector2(530, 42), "地图打开时游戏暂停\nM / Esc 关闭 · 进度在睡觉时保存", 17);
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
