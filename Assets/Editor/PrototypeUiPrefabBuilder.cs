using TMPro;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Loading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.EditorTools
{
    public static class PrototypeUiPrefabBuilder
    {
        public const string Folder = "Assets/Art/Prefabs/UI";
        private const string BackgroundPath = "Assets/Art/Textures/UI/MainMenuBackground.png";
        private const string RoundedPath = "Assets/Art/Textures/UI/RoundedPanel.png";
        private const string ThemeStamp = Folder + "/ThemeVersion.txt";
        internal static readonly Color Cream = new Color(0.98f, 0.96f, 0.88f, 0.96f);
        private static readonly Color Ink = new Color(0.2f, 0.29f, 0.23f);
        private static readonly Color Green = new Color(0.28f, 0.43f, 0.31f);
        internal static readonly Color Sand = new Color(0.92f, 0.89f, 0.8f);
        private static Sprite rounded;

        // Create only absent prefabs; edits made in the Inspector are preserved.
        [MenuItem("Magic Island/Create Missing UI Prefabs")]
        public static void CreateMissing()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            PrepareArtwork();
            if (File.Exists(BackgroundPath) && !File.Exists(ThemeStamp))
            {
                ApplyCozyTheme();
            }
            if (!File.Exists(Folder + "/MainMenuPanel.prefab")) CreateMenu();
            if (!File.Exists(Folder + "/FarmHudPanel.prefab")) CreateHud();
            InventoryUiPrefabBuilder.CreateMissing();
            SettingsUiPrefabBuilder.CreateMissing();
            ShopUiPrefabBuilder.CreateMissing();
            SleepUiPrefabBuilder.CreateMissing();
            FeedbackPanelPrefabBuilder.CreateMissing();
            CropInfoPanelPrefabBuilder.CreateMissing();
            TaskPanelPrefabBuilder.CreateMissing();
            OrderPanelPrefabBuilder.CreateMissing();
            ToolUpgradeSetup.CreatePanel();
            StorageSetup.CreatePanel();
            CookingSetup.CreatePanel();
            NPCSetup.CreatePanel();
            RelationshipSetup.CreatePanel();
            RelationshipSetup.UpgradeDialogue();
            StoryEventSetup.UpgradeDialogue();
            StoryEventSetup.UpgradeCooking();
            FishCookingSetup.UpgradePanel();
            EnsureMvcControls();
            CropUiUpgrade.Apply();
            CookingSetup.UpgradeInventory();
            FishingSetup.CreatePanel();
            FishingSetup.UpgradeShop();
            CraftingSetup.CreatePanels(); CraftingSetup.UpgradePanels();
            FurnitureSetup.UpgradePanel();
            HousingSetup.CreatePanel(); HousingSetup.UpgradeHud();
            HouseUpgradeSetup.CreatePanel();
            UiPrefabMigration.UpgradeAll();
        }

        private static void EnsureMvcControls()
        {
            EnsureContinueButton();
            string path = Folder + "/FarmHudPanel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PrototypeHud hud = root.GetComponent<PrototypeHud>();
                bool changed = false;
                if (hud.TimeText == null)
                {
                    hud.TimeText = Label("Clock", hud.DayText.transform.parent, new Vector2(300, -12), new Vector2(154, 32), "06:00", 22, TextAnchor.MiddleRight);
                    changed = true;
                }
                if (hud.GoldText == null)
                {
                    Transform top = hud.DayText.transform.parent;
                    top.GetComponent<RectTransform>().sizeDelta = new Vector2(470, 188);
                    hud.GoldText = Label("Gold", top, new Vector2(16, -144), new Vector2(438, 32), "金币 100", 20);
                    changed = true;
                }
                if (hud.StaminaText == null || hud.StaminaFill == null)
                {
                    Transform top = hud.DayText.transform.parent;
                    top.GetComponent<RectTransform>().sizeDelta = new Vector2(470, 230);
                    hud.GoldText.rectTransform.sizeDelta = new Vector2(188, 32);
                    hud.StaminaText = Label("Stamina", top, new Vector2(214, -144), new Vector2(240, 32), "体力 100 / 100", 20, TextAnchor.MiddleRight);
                    RectTransform track = Panel("Stamina Track", top, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -190), new Vector2(438, 18), Sand);
                    RectTransform fill = Panel("Stamina Fill", track, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(438, 18), new Color(0.44f, 0.66f, 0.45f));
                    hud.StaminaFill = fill.GetComponent<Image>(); hud.StaminaFill.type = Image.Type.Filled;
                    hud.StaminaFill.fillMethod = Image.FillMethod.Horizontal; hud.StaminaFill.fillOrigin = 0;
                    hud.StaminaFill.raycastTarget = false; track.GetComponent<Image>().raycastTarget = false;
                    changed = true;
                }
                if (hud.ReturnMenuButton == null) { hud.ReturnMenuButton = CreateReturnButton(root.transform); changed = true; }
                if (hud.InventoryButton == null) { hud.InventoryButton = CreateInventoryButton(root.transform); changed = true; }
                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void EnsureContinueButton()
        {
            string path = Folder + "/MainMenuPanel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MainMenuView menu = root.GetComponent<MainMenuView>();
                bool changed = false;
                if (menu.ContinueButton == null)
                {
                    Transform panel = menu.EnterButton.transform.parent;
                    menu.EnterButton.GetComponent<RectTransform>().sizeDelta = new Vector2(328, 44);
                    menu.EnterButton.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta = new Vector2(328, 44);
                    menu.EnterButton.GetComponentInChildren<TMP_Text>().text = "新游戏";
                    menu.ContinueButton = Button("Continue Game", panel, new Vector2(36, -374), new Vector2(328, 44), "继续游戏", Sand);
                    menu.ContinueButton.interactable = false;
                    changed = true;
                }
                if (menu.SettingsButton == null || menu.QuitButton == null)
                {
                    Transform panel = menu.EnterButton.transform.parent;
                    panel.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 560);
                    menu.StatusText.rectTransform.anchoredPosition = new Vector2(36, -230);
                    menu.StatusText.rectTransform.sizeDelta = new Vector2(328, 42);
                    menu.EnterButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(36, -284);
                    menu.ContinueButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(36, -336);
                    menu.ContinueButton.GetComponentInChildren<TMP_Text>().text = "加载游戏";
                    if (menu.SettingsButton == null)
                        menu.SettingsButton = Button("Settings", panel, new Vector2(36, -388), new Vector2(328, 44), "设置", Sand);
                    if (menu.QuitButton == null)
                        menu.QuitButton = Button("Quit", panel, new Vector2(36, -440), new Vector2(328, 44), "退出", Sand);
                    TMP_Text hint = root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Controls");
                    if (hint != null)
                    {
                        hint.rectTransform.anchoredPosition = new Vector2(36, -504);
                        hint.rectTransform.sizeDelta = new Vector2(328, 24);
                        hint.fontSize = 14;
                    }
                    changed = true;
                }
                TMP_Text controls = root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Controls");
                if (controls != null && Mathf.Approximately(controls.rectTransform.anchoredPosition.y, -409f))
                {
                    controls.rectTransform.anchoredPosition = new Vector2(36, -435);
                    controls.rectTransform.sizeDelta = new Vector2(328, 24);
                    controls.fontSize = 14;
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Button CreateReturnButton(Transform parent)
        {
            Button button = Button("Return To Menu", parent, new Vector2(-20, -20), new Vector2(136, 44), "返回主菜单", Sand);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            SoftShadow(rect);
            return button;
        }

        private static Button CreateInventoryButton(Transform parent)
        {
            Button button = Button("Inventory", parent, new Vector2(-20, -74), new Vector2(136, 44), "背包 (B)", Sand);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            SoftShadow(rect);
            return button;
        }

        [MenuItem("Magic Island/Apply Cozy UI Theme")]
        public static void ApplyCozyTheme()
        {
            PrepareArtwork();
            CreateMenu();
            CreateHud();
            EnsureContinueButton();
            File.WriteAllText(ThemeStamp, "Cozy island UI v2\n");
            AssetDatabase.ImportAsset(ThemeStamp);
        }

        private static void PrepareArtwork()
        {
            Directory.CreateDirectory("Assets/Art/Textures/UI");
            if (!File.Exists(RoundedPath))
            {
                Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - 15.5f, 0f);
                        float dy = Mathf.Max(Mathf.Abs(y - 31.5f) - 15.5f, 0f);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f,
                            Mathf.Clamp01(16.5f - Mathf.Sqrt(dx * dx + dy * dy))));
                    }
                texture.Apply();
                File.WriteAllBytes(RoundedPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(RoundedPath);
            TextureImporter shapeImporter = (TextureImporter)AssetImporter.GetAtPath(RoundedPath);
            if (shapeImporter.textureType != TextureImporterType.Sprite || shapeImporter.spriteBorder != Vector4.one * 20f)
            {
                shapeImporter.textureType = TextureImporterType.Sprite;
                shapeImporter.spriteImportMode = SpriteImportMode.Single;
                shapeImporter.spriteBorder = Vector4.one * 20f;
                shapeImporter.alphaIsTransparency = true;
                shapeImporter.textureCompression = TextureImporterCompression.Uncompressed;
                shapeImporter.mipmapEnabled = false;
                shapeImporter.SaveAndReimport();
            }
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            if (!File.Exists(BackgroundPath)) return;
            AssetDatabase.ImportAsset(BackgroundPath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(BackgroundPath);
            if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize != 2048 || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        internal static GameObject CanvasRoot(string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            return root;
        }

        internal static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        internal static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(name, parent, anchor, pivot, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            return rect;
        }

        internal static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size,
            string content, int fontSize = 20, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = UiPrefabMigration.FontAsset;
            text.text = content;
            text.fontSize = fontSize;
            text.color = Ink;
            text.alignment = UiPrefabMigration.Alignment(alignment);
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        internal static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string content, Color color)
        {
            RectTransform rect = Panel(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, size, color);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.83f);
            colors.pressedColor = new Color(0.77f, 0.85f, 0.72f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.78f, 0.78f, 0.73f, 0.65f);
            button.colors = colors;
            Label("Label", rect, Vector2.zero, size, content, 21, TextAnchor.MiddleCenter);
            return button;
        }

        internal static void SoftShadow(RectTransform rect)
        {
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.12f, 0.2f, 0.14f, 0.18f);
            shadow.effectDistance = new Vector2(0, -5);
        }

        private static void Save(GameObject root)
        {
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + root.name + ".prefab");
            Object.DestroyImmediate(root);
        }

        private static void CreateMenu()
        {
            GameObject root = CanvasRoot(RuntimeUi.MenuAddress);
            RectTransform frame = Rect("Background Frame", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            frame.anchorMax = Vector2.one;
            RectTransform background = Rect("Background", frame, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(1280, 720));
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            backgroundImage.color = Color.white;
            backgroundImage.raycastTarget = false;
            AspectRatioFitter fit = background.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = backgroundImage.sprite != null ? backgroundImage.sprite.rect.width / backgroundImage.sprite.rect.height : 16f / 9f;
            RectTransform panel = Panel("Menu Panel", root.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(64, 0), new Vector2(400, 470), Cream);
            SoftShadow(panel);
            Label("Eyebrow", panel, new Vector2(36, -30), new Vector2(328, 28), "MAGIC ISLAND", 15).color = Green;
            Label("Title", panel, new Vector2(32, -76), new Vector2(336, 76), "魔法岛", 52);
            Label("Subtitle", panel, new Vector2(36, -158), new Vector2(328, 42), "把日子种成喜欢的模样", 21);
            Panel("Divider", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -220), new Vector2(328, 2), new Color(0.75f, 0.76f, 0.64f));
            MainMenuView menu = root.AddComponent<MainMenuView>();
            menu.Layer = MagicIsland.UI.Views.UILayer.Bottom;
            menu.StatusText = Label("Status", panel, new Vector2(36, -244), new Vector2(328, 56),
                "种植、浇水、收获\n开始你的岛屿生活", 18);
            menu.EnterButton = Button("Enter Farm", panel, new Vector2(36, -322), new Vector2(328, 60), "开始岛屿生活", Green);
            menu.EnterButton.GetComponentInChildren<TMP_Text>().color = Cream;
            Label("Controls", panel, new Vector2(36, -409), new Vector2(328, 28), "WASD 移动  /  鼠标操作", 16).color = new Color(0.42f, 0.46f, 0.37f);
            Save(root);
        }

        private static void CreateHud()
        {
            GameObject root = CanvasRoot(RuntimeUi.HudAddress);
            PrototypeHud hud = root.AddComponent<PrototypeHud>();
            hud.ReturnMenuButton = CreateReturnButton(root.transform);
            hud.InventoryButton = CreateInventoryButton(root.transform);
            RectTransform top = Panel("Farm Status", root.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -20), new Vector2(470, 150), Cream);
            SoftShadow(top);
            Label("Title", top, new Vector2(16, -10), new Vector2(438, 34), "岛屿农场", 26);
            hud.DayText = Label("Day", top, new Vector2(16, -48), new Vector2(150, 30), "第 1 天");
            hud.InventoryText = Label("Inventory", top, new Vector2(170, -48), new Vector2(284, 30), "种子 50    已收获 0");
            Label("Controls", top, new Vector2(16, -95), new Vector2(280, 36), "WASD 移动 · 滚轮缩放", 18);
            hud.NextDayButton = Button("Next Day", top, new Vector2(320, -95), new Vector2(134, 40), "下一天", new Color(0.9f, 0.72f, 0.4f));

            RectTransform bottom = Panel("Toolbar", root.transform, Vector2.zero, Vector2.zero,
                new Vector2(20, 20), new Vector2(790, 120), Cream);
            SoftShadow(bottom);
            hud.ToolButtons = new Button[3];
            string[] names = { "Hoe", "Seeds", "Watering Can" };
            string[] labels = { "1 锄头", "2 种子", "3 水壶" };
            for (int i = 0; i < 3; i++)
                hud.ToolButtons[i] = Button(names[i], bottom, new Vector2(16 + 150 * i, -14), new Vector2(140, 44), labels[i], Sand);
            hud.HintText = Label("Target Hint", bottom, new Vector2(476, -14), new Vector2(298, 44), "靠近农田");
            hud.FeedbackText = Label("Feedback", bottom, new Vector2(16, -70), new Vector2(758, 35), "浇水后进入下一天，作物生长一阶段", 19);
            hud.FeedbackText.color = new Color(0.42f, 0.46f, 0.37f);
            Save(root);
        }
    }
}
