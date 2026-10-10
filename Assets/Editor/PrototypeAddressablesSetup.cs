using System.Linq;
using System.IO;
using MagicIsland.Loading;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class PrototypeAddressablesSetup
    {
        private const string MenuScene = "Assets/Scenes/MainMenu.unity";
        private const string GameScene = "Assets/Scenes/MainGame.unity";

        static PrototypeAddressablesSetup() => EditorApplication.delayCall += EnsureSetup;

        [MenuItem("Magic Island/Configure Prototype Addressables")]
        public static void EnsureSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            PrototypeUiPrefabBuilder.CreateMissing();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) return;
            const string configPath = "Assets/Config/GameConfig.asset";
            Directory.CreateDirectory("Assets/Config");
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }
            AddressableAssetGroup configGroup = settings.FindGroup("Configuration");
            if (configGroup == null)
                configGroup = settings.CreateGroup("Configuration", false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var configEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(configPath))
                ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(configPath), configGroup);
            if (configEntry.address != GameConfig.Address) configEntry.SetAddress(GameConfig.Address);
            const string catalogPath = "Assets/Config/ItemCatalog.asset";
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png");
                catalog.Items.Add(new ItemConfig { BuyPrice = 10, Id = "turnip_seed", DisplayName = "芜菁种子", Type = ItemType.Seed,
                    MaxStack = 99, Icon = icon, IconColor = new Color(0.87f, 0.69f, 0.38f), Description = "播种在耕好的土地上。每天浇水，等待芜菁成熟。" });
                catalog.Items.Add(new ItemConfig { SellPrice = 25, Id = "turnip", DisplayName = "芜菁", Type = ItemType.Crop,
                    MaxStack = 99, Icon = icon, IconColor = new Color(0.46f, 0.65f, 0.37f), Description = "刚从农田收获的芜菁。岛屿生活的第一份收成。" });
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            var catalogEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(catalogPath))
                ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(catalogPath), configGroup);
            if (catalogEntry.address != ItemCatalog.Address) catalogEntry.SetAddress(ItemCatalog.Address);
            CropCatalogSetup.Ensure(config, catalog, settings, configGroup);
            GatheringCatalogSetup.Ensure(catalog, settings, configGroup);
            SeasonConfigSetup.Ensure(settings, configGroup);
            TaskCatalogSetup.Ensure(settings, configGroup);
            OrderCatalogSetup.Ensure(settings, configGroup);
            ToolUpgradeSetup.Ensure(settings, configGroup);
            StorageSetup.Ensure(settings, configGroup);
            CropVisualPrefabBuilder.Ensure(AssetDatabase.LoadAssetAtPath<CropCatalog>("Assets/Config/CropCatalog.asset"), settings);
            WeatherConfigSetup.Ensure(settings, configGroup);
            WeatherConfigSetup.UpgradeHud();
            SeasonConfigSetup.UpgradeUi();
            GatheringUiUpgrade.Apply();
            var existing = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(GameScene));
            if (existing == null)
            {
                AddressableAssetGroup group = settings.FindGroup("Farm Prototype");
                if (group == null)
                    group = settings.CreateGroup("Farm Prototype", false, false, true, null,
                        typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(GameScene), group)
                    .SetAddress(config.GameSceneAddress);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            else if (existing.address != config.GameSceneAddress)
            {
                existing.SetAddress(config.GameSceneAddress);
                AssetDatabase.SaveAssets();
            }
            AddressableAssetGroup uiGroup = settings.FindGroup("UI");
            if (uiGroup == null)
                uiGroup = settings.CreateGroup("UI", false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            foreach (string path in Directory.GetFiles(PrototypeUiPrefabBuilder.Folder, "*.prefab"))
            {
                string assetPath = path.Replace('\\', '/');
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid)) continue;
                var uiEntry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, uiGroup);
                string address = Path.GetFileNameWithoutExtension(assetPath);
                if (uiEntry.address != address) uiEntry.SetAddress(address);
            }
            EnsurePrototypeShader();
            AssetDatabase.SaveAssets();
            // The bootstrap is bundled with the player. MainGame belongs to AA only.
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != GameScene).ToList();
            if (!scenes.Any(scene => scene.path == MenuScene))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(MenuScene, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            else if (scenes.Count != EditorBuildSettings.scenes.Length)
                EditorBuildSettings.scenes = scenes.ToArray();
            ValidateUiPrefabs();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PrototypeUiValidation.txt",
                "PASS: MainMenuPanel and FarmHudPanel prefab components, serialized references, AA addresses.\n"
                + "UTC: " + System.DateTime.UtcNow.ToString("O") + "\n");
        }

        private static void EnsurePrototypeShader()
        {
            // Procedural materials have no serialized shader reference in the scene.
            Object graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            if (graphics == null) return;
            var serialized = new SerializedObject(graphics);
            SerializedProperty shaders = serialized.FindProperty("m_AlwaysIncludedShaders");
            Shader standard = Shader.Find("Standard");
            if (shaders == null || standard == null) return;
            for (int i = 0; i < shaders.arraySize; i++)
                if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == standard) return;
            shaders.InsertArrayElementAtIndex(shaders.arraySize);
            shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = standard;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Magic Island/Build Prototype Addressables")]
        public static void BuildContent()
        {
            EnsureSetup();
            ValidateUiPrefabs();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error)) throw new System.Exception(result.Error);
            Debug.Log("Prototype Addressables content build succeeded.");
        }

        [MenuItem("Magic Island/Validate UI Prefabs")]
        public static void ValidateUiPrefabs()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (string name in new[] { RuntimeUi.MenuAddress, RuntimeUi.HudAddress, RuntimeUi.InventoryAddress, "SettingsPanel", "ShopPanel", "SleepPanel", "FeedbackPanel", "CropInfoPanel", "TaskPanel", "OrderPanel", "ToolUpgradePanel", "StoragePanel" })
            {
                string path = PrototypeUiPrefabBuilder.Folder + "/" + name + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<UnityEngine.Canvas>() == null
                    || prefab.GetComponent<UnityEngine.UI.CanvasScaler>() == null
                    || prefab.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    throw new System.Exception("Missing UI prefab or Canvas components: " + name);
                if (prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length != 0
                    || prefab.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.font == null)
                    || prefab.GetComponent<UIView>() == null)
                    throw new System.Exception("UI requires UIView and TMP texts with fonts: " + name);
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                if (entry == null || entry.address != name)
                    throw new System.Exception("UI address must match prefab name: " + name);
            }
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/MainMenuPanel.prefab")
                .GetComponent<MainMenuView>();
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/FarmHudPanel.prefab")
                .GetComponent<MagicIsland.Interaction.PrototypeHud>();
            if (menu == null || menu.StatusText == null || menu.EnterButton == null || menu.ContinueButton == null
                || menu.SettingsButton == null || menu.QuitButton == null
                || hud == null || hud.WeatherText == null || hud.SeedText == null || hud.DayText == null || hud.StaminaText == null || hud.StaminaFill == null || hud.TimeText == null || hud.InventoryText == null || hud.GoldText == null
                || hud.HintText == null || hud.FeedbackText == null || hud.NextDayButton == null || hud.ReturnMenuButton == null || hud.InventoryButton == null
                || hud.ToolButtons == null || hud.ToolButtons.Length != 5 || hud.ToolButtons.Any(button => button == null))
                throw new System.Exception("UI prefab component references are incomplete.");
            InventoryView inventory = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/InventoryPanel.prefab")
                .GetComponent<InventoryView>();
            if (inventory == null || inventory.UseSeedButton == null || inventory.CloseButton == null || inventory.CapacityText == null
                || inventory.DetailTitleText == null || inventory.DetailText == null || inventory.Slots == null || inventory.Slots.Length < 36
                || inventory.Slots.Any(slot => slot == null || slot.Icon == null || slot.NameText == null || slot.QuantityText == null || slot.SelectButton == null))
                throw new System.Exception("InventoryPanel component references are incomplete.");
            SettingsView options = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/SettingsPanel.prefab")
                .GetComponent<SettingsView>();
            if (options == null || options.CloseButton == null || options.VolumeSlider == null
                || options.FullscreenToggle == null || options.VolumeText == null)
                throw new System.Exception("SettingsPanel component references are incomplete.");
            FeedbackView feedback = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/FeedbackPanel.prefab").GetComponent<FeedbackView>();
            if (feedback == null || feedback.Group == null || feedback.MessageText == null || feedback.DetailText == null || feedback.Accent == null
                || feedback.Layer != UILayer.Top || feedback.Group.blocksRaycasts
                || feedback.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(graphic => graphic.raycastTarget))
                throw new System.Exception("FeedbackPanel must have wired TMP components and allow mouse input through it.");
            SleepView sleep = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/SleepPanel.prefab").GetComponent<SleepView>();
            OrderView order = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/OrderPanel.prefab").GetComponent<OrderView>();
            var upgrade = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/ToolUpgradePanel.prefab").GetComponent<ToolUpgradeView>();
            var storage = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/StoragePanel.prefab").GetComponent<StorageView>();
            if (storage == null || storage.Layer != UILayer.PopUp || storage.CloseButton == null || storage.MoveOneButton == null || storage.MoveStackButton == null || storage.SaveButton == null
                || storage.BagCapacityText == null || storage.ChestCapacityText == null || storage.DetailsText == null || storage.FeedbackText == null || storage.BagSlots == null || storage.ChestSlots == null
                || storage.BagSlots.Length < 36 || storage.ChestSlots.Length < 36 || storage.BagSlots.Concat(storage.ChestSlots).Any(s => s == null || s.Icon == null || s.SelectButton == null || s.NameText == null || s.QuantityText == null))
                throw new System.Exception("StoragePanel requires both slot grids, TMP references and buttons on PopUp layer");
            if (upgrade == null || upgrade.Layer != UILayer.PopUp || upgrade.CloseButton == null || upgrade.SaveButton == null || upgrade.BalanceText == null || upgrade.FeedbackText == null
                || upgrade.UpgradeButtons.Length != 2 || upgrade.TitleTexts.Length != 2 || upgrade.CostTexts.Length != 2 || upgrade.EffectTexts.Length != 2
                || upgrade.UpgradeButtons.Any(b => b == null) || upgrade.TitleTexts.Any(t => t == null) || upgrade.CostTexts.Any(t => t == null) || upgrade.EffectTexts.Any(t => t == null))
                throw new System.Exception("ToolUpgradePanel requires wired TMP references on PopUp layer");
            if (order == null || order.Layer != UILayer.PopUp || order.DateText == null || order.RequestText == null || order.StockText == null
                || order.RewardText == null || order.StatusText == null || order.FeedbackText == null || order.CropIcon == null || order.DeliverButton == null || order.CloseButton == null)
                throw new System.Exception("OrderPanel needs wired TMP and button references on PopUp layer.");
            TaskView task = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/TaskPanel.prefab").GetComponent<TaskView>();
            if (task == null || task.Layer != UILayer.Common || task.Card == null || task.Details == null || task.HeaderText == null
                || task.TitleText == null || task.DescriptionText == null || task.ProgressText == null || task.FeedbackText == null
                || task.ClaimButton == null || task.CollapseButton == null)
                throw new System.Exception("TaskPanel must have wired TMP and button references on Common layer.");
            CropInfoView cropInfo = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/CropInfoPanel.prefab").GetComponent<CropInfoView>();
            if (cropInfo == null || cropInfo.Group == null || cropInfo.NameText == null || cropInfo.DetailsText == null || cropInfo.HintText == null
                || cropInfo.Layer != UILayer.Common || cropInfo.Group.blocksRaycasts
                || cropInfo.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(graphic => graphic.raycastTarget))
                throw new System.Exception("CropInfoPanel must have wired TMP references and allow mouse input through it.");
            if (sleep == null || sleep.MessageText == null || sleep.ConfirmButton == null || sleep.CancelButton == null)
                throw new System.Exception("SleepPanel component references are incomplete.");
            ShopView shop = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/ShopPanel.prefab").GetComponent<ShopView>();
            if (shop == null || shop.CropButtons == null || shop.CropButtons.Length < 3 || shop.CropButtons.Any(button => button == null)
                || shop.CloseButton == null || shop.BuyButton == null || shop.SellButton == null
                || shop.BalanceText == null || shop.StockText == null || shop.FeedbackText == null || shop.BuyText == null || shop.SellText == null)
                throw new System.Exception("ShopPanel component references are incomplete.");
            Debug.Log("UI prefab validation succeeded: MainMenuPanel, FarmHudPanel, InventoryPanel, SettingsPanel; AA addresses match names.");
        }
    }
}
