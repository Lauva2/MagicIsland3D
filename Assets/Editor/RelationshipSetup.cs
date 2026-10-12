using System.IO;
using MagicIsland.Configuration;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class RelationshipSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            const string path = "Assets/Config/RelationshipCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<RelationshipCatalog>(path) == null)
            {
                var catalog = ScriptableObject.CreateInstance<RelationshipCatalog>();
                foreach (var npc in AssetDatabase.LoadAssetAtPath<NPCCatalog>("Assets/Config/NPCCatalog.asset").NPCs)
                    catalog.Relationships.Add(new RelationshipConfig { NpcId = npc.Id, FavoriteItemId = "turnip_soup",
                        FamiliarGreeting = "你来了！最近常常见到你，小岛也变得热闹了。今天准备做些什么？",
                        FriendlyGreeting = "见到你真高兴！有你这样的朋友，小岛的每一天都让人期待。",
                        FavoriteReply = "是芜菁汤！我很喜欢这份温暖的味道，谢谢你。", StandardReply = "谢谢你的礼物！这份心意我收下了。" });
                AssetDatabase.CreateAsset(catalog, path);
            }
            string guid = AssetDatabase.AssetPathToGUID(path); var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != RelationshipCatalog.Address) entry.SetAddress(RelationshipCatalog.Address);
        }
        public static void UpgradeDialogue()
        {
            string path = Factory.Folder + "/DialoguePanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<DialogueView>(); if (view.RelationshipText != null && view.GiftButton != null) return;
                var panel = view.SpeakerText.transform.parent;
                if (view.RelationshipText == null) view.RelationshipText = Factory.Label("Relationship", panel, new Vector2(32, -62), new Vector2(896, 24), "未见面 · 完成首次交谈后可送礼", 17);
                if (view.GiftButton == null) view.GiftButton = Factory.Button("Gift", panel, new Vector2(514, -232), new Vector2(196, 48), "送礼", Factory.Sand);
                view.ProgressText.rectTransform.sizeDelta = new Vector2(454, 30);
                view.BodyText.rectTransform.anchoredPosition = new Vector2(32, -106); view.BodyText.rectTransform.sizeDelta = new Vector2(896, 112);
                var accent = panel.Find("Accent"); if (accent != null) accent.GetComponent<RectTransform>().anchoredPosition = new Vector2(32, -90);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void CreatePanel()
        {
            string path = Factory.Folder + "/GiftPanel.prefab"; if (File.Exists(path)) return; var root = Factory.CanvasRoot("GiftPanel");
            try
            {
                var view = root.AddComponent<GiftView>(); view.Layer = UILayer.PopUp;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .2f, .15f, .58f)); shade.anchorMax = Vector2.one; shade.GetComponent<Image>().sprite = null;
                var panel = Factory.Panel("Gift Card", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(820, 640), Factory.Cream); Factory.SoftShadow(panel);
                view.TitleText = Factory.Label("Title", panel, new Vector2(34, -20), new Vector2(630, 42), "给林舟挑选礼物", 28);
                view.CloseButton = Factory.Button("Close", panel, new Vector2(730, -22), new Vector2(54, 42), "×", Factory.Sand);
                view.StatusText = Factory.Label("Status", panel, new Vector2(34, -76), new Vector2(752, 28), "初识 · 好感度 0 / 100 · 今日可送礼", 20);
                view.GuideText = Factory.Label("Guide", panel, new Vector2(34, -114), new Vector2(752, 28), "每日一次 · 可送作物和料理 · 偏好 芜菁汤", 18);
                var scrollRect = Factory.Rect("Item Scroll", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -156), new Vector2(752, 242));
                var scroll = scrollRect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 25;
                var viewport = Factory.Panel("Viewport", scrollRect, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.82f, .84f, .74f)); viewport.anchorMax = Vector2.one; viewport.gameObject.AddComponent<RectMask2D>();
                var content = Factory.Rect("Slots", viewport, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(752, 0));
                var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(105, 94); grid.spacing = new Vector2(12, 12); grid.padding = new RectOffset(31, 31, 8, 8);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 6; content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; scroll.viewport = viewport; scroll.content = content;
                view.Slots = new InventorySlotView[36]; var icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png");
                for (int i = 0; i < 36; i++)
                {
                    var button = Factory.Button("Slot " + (i + 1), content, Vector2.zero, new Vector2(105, 94), "空格", Factory.Sand);
                    var slot = button.gameObject.AddComponent<InventorySlotView>(); slot.SelectButton = button; slot.NameText = button.GetComponentInChildren<TMP_Text>(); slot.NameText.fontSize = 14;
                    slot.NameText.rectTransform.anchoredPosition = new Vector2(7, -65); slot.NameText.rectTransform.sizeDelta = new Vector2(91, 23);
                    var iconRect = Factory.Rect("Icon", button.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(35, -10), new Vector2(35, 35)); slot.Icon = iconRect.gameObject.AddComponent<Image>(); slot.Icon.sprite = icon; slot.Icon.raycastTarget = false;
                    slot.QuantityText = Factory.Label("Quantity", button.transform, new Vector2(60, -39), new Vector2(37, 24), "", 18, TextAnchor.MiddleRight); view.Slots[i] = slot;
                }
                var details = Factory.Panel("Details", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -414), new Vector2(752, 96), Factory.Sand);
                view.DetailText = Factory.Label("Selected", details, new Vector2(20, -12), new Vector2(712, 28), "选择一个作物或料理", 21);
                view.PreviewText = Factory.Label("Preview", details, new Vector2(20, -50), new Vector2(712, 30), "赠送 1 个 · 好感度 +20 · 偏好礼物", 18);
                view.GiveButton = Factory.Button("Give", panel, new Vector2(34, -530), new Vector2(280, 44), "赠送一个", new Color(.70f, .81f, .62f));
                view.SaveButton = Factory.Button("Save Progress", panel, new Vector2(606, -530), new Vector2(180, 44), "保存进度", Factory.Sand);
                view.FeedbackText = Factory.Label("Feedback", panel, new Vector2(34, -592), new Vector2(752, 30), "选择礼物后确认赠送，成功后自动保存", 17);
                view.FeedbackText.enableAutoSizing = true; view.FeedbackText.fontSizeMin = 14; view.FeedbackText.fontSizeMax = 17;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
