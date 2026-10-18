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
            BeachForageSetup.Ensure(catalog,settings);
            SeasonConfigSetup.Ensure(settings, configGroup);
            TaskCatalogSetup.Ensure(settings, configGroup);
            OrderCatalogSetup.Ensure(settings, configGroup);
            ToolUpgradeSetup.Ensure(settings, configGroup);
            StorageSetup.Ensure(settings, configGroup);
            CookingSetup.Ensure(settings, configGroup, catalog);
            NPCSetup.Ensure(settings, configGroup);
            RelationshipSetup.Ensure(settings, configGroup);
            StoryEventSetup.Ensure(settings, configGroup, catalog);
            CropVisualPrefabBuilder.Ensure(AssetDatabase.LoadAssetAtPath<CropCatalog>("Assets/Config/CropCatalog.asset"), settings);
            WeatherConfigSetup.Ensure(settings, configGroup);
            CloudSetup.Ensure(settings);
            WeatherConfigSetup.UpgradeHud();
            SeasonConfigSetup.UpgradeUi();
            GatheringUiUpgrade.Apply();
            FishingSetup.Ensure(settings, configGroup, catalog);
            NPCScheduleSetup.Ensure(settings, configGroup);
            CraftingSetup.Ensure(settings, configGroup, catalog);
            FurnitureSetup.Ensure(settings, catalog);
            AnimalSetup.Ensure(settings, configGroup, catalog);
            AnimalFriendshipSetup.Ensure();
            QualityEggSetup.Ensure(catalog);
            FeedCraftingSetup.Ensure();
            InventoryUpgradeSetup.Ensure(settings, configGroup, config);
            ShippingSetup.Ensure(settings, configGroup);
            FertilizerSetup.Ensure(settings, configGroup, catalog);
            FishCookingSetup.Ensure(catalog);
            EggCookingSetup.Ensure(catalog);
            OrderCatalogSetup.EnsureAnimalOffers();
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
            HousingSetup.Ensure(settings, configGroup);
            HouseUpgradeSetup.Ensure(settings);
            CameraOcclusionSetup.Ensure();
            AudioSetup.Ensure(settings, configGroup);
            PlayerCharacterSetup.Ensure(settings, config);
            InteractionPromptSetup.Ensure(settings, config);
            OceanSetup.Ensure(settings, config);
            TerrainSetup.Ensure(settings, config);
            CoastArtSetup.Ensure(settings);
            OceanSetup.UpgradeWater();
            OceanBiomeSetup.Ensure();
            OceanInteractionEffectsSetup.Ensure();
            VillageArtSetup.Ensure(settings);
            FacilityArtSetup.Ensure(settings,config);
            FarmArtSetup.Ensure(settings, config);
            GatheringArtSetup.Ensure();
            OrchardSetup.Ensure(settings);
            VegetationWindSetup.Ensure();
            SeasonalDetailsSetup.Ensure(settings);
            IslandBiomeSetup.Ensure();
            MagicContentSetup.Ensure(settings, configGroup, catalog);
            OutdoorNightLightingSetup.Ensure();
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
            foreach (string name in new[] { RuntimeUi.MenuAddress, RuntimeUi.HudAddress, RuntimeUi.InventoryAddress, "SettingsPanel", "ShopPanel", "SleepPanel", "FeedbackPanel", "CropInfoPanel", "TaskPanel", "OrderPanel", "ToolUpgradePanel", "StoragePanel", "CookingPanel", "DialoguePanel", "GiftPanel", "FishingPanel", "CraftingPanel", "PlacementPanel", "SceneTransitionPanel", "HouseUpgradePanel", "CoopPanel", "AnimalShopPanel", "DailyReportPanel", "InventoryUpgradePanel", "ShippingPanel", "PausePanel", "MapPanel", "MagicPanel" })
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
                || hud.ToolButtons == null || hud.ToolButtons.Length != 7 || hud.ToolButtons.Any(button => button == null))
                throw new System.Exception("UI prefab component references are incomplete.");
            var map = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/MapPanel.prefab").GetComponent<MapView>();
            if (map == null || map.Layer != UILayer.PopUp || !map.IsValid || hud.MapButton == null)
                throw new System.Exception("MapPanel requires wired map, TMP markers and MVC buttons on PopUp layer");
            var magic = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/MagicPanel.prefab").GetComponent<MagicView>();
            if (magic == null || !magic.IsValid || magic.Layer != UILayer.PopUp || hud.MagicButton == null)
                throw new System.Exception("MagicPanel requires wired TMP and MVC buttons on PopUp layer");
            InventoryView inventory = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/InventoryPanel.prefab")
                .GetComponent<InventoryView>();
            if (inventory == null || inventory.SortButton == null || inventory.UseSeedButton == null || inventory.UseFoodButton == null || inventory.SaveButton == null || inventory.FeedbackText == null || inventory.CloseButton == null || inventory.CapacityText == null
                || inventory.DetailTitleText == null || inventory.DetailText == null || inventory.DragPreview == null || inventory.DragIcon == null || inventory.DragNameText == null || inventory.DragQuantityText == null
                || inventory.DragPreview.GetComponent<CanvasGroup>() == null || inventory.DragPreview.GetComponent<CanvasGroup>().blocksRaycasts
                || !inventory.ThreeRowLayout || inventory.PageControls == null || inventory.PreviousPageButton == null || inventory.NextPageButton == null || inventory.PageText == null
                || inventory.Slots == null || inventory.Slots.Length != InventoryView.DisplaySlots
                || inventory.Slots.Any(slot => slot == null || slot.Icon == null || slot.NameText == null || slot.QuantityText == null || slot.SelectButton == null || slot.LockIcon == null || slot.LockIcon.GetComponent<CanvasRenderer>() == null || slot.GetComponent<InventorySlotInput>() == null))
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
            var cooking = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/CookingPanel.prefab").GetComponent<CookingView>();
            var dialogue = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/DialoguePanel.prefab").GetComponent<DialogueView>();
            if (dialogue == null || dialogue.Layer != UILayer.PopUp || dialogue.SpeakerText == null || dialogue.BodyText == null || dialogue.ProgressText == null || dialogue.RelationshipText == null || dialogue.GiftButton == null || dialogue.StoryButton == null || dialogue.NextButton == null || dialogue.CloseButton == null)
                throw new System.Exception("DialoguePanel needs wired TMP and buttons on PopUp layer");
            var gift = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/GiftPanel.prefab").GetComponent<GiftView>();
            if (gift == null || gift.Layer != UILayer.PopUp || gift.TitleText == null || gift.StatusText == null || gift.GuideText == null || gift.DetailText == null || gift.PreviewText == null || gift.FeedbackText == null
                || gift.GiveButton == null || gift.SaveButton == null || gift.CloseButton == null || gift.Slots.Length < InventoryUpgradeState.MaximumCapacity
                || gift.Slots.Any(s => s == null || s.SelectButton == null || s.NameText == null || s.QuantityText == null || s.Icon == null)) throw new System.Exception("GiftPanel requires wired inventory grid, TMP and buttons on PopUp layer");
            if (cooking == null || cooking.Layer != UILayer.PopUp || cooking.CloseButton == null || cooking.SaveButton == null || cooking.FeedbackText == null
                || cooking.CookButtons.Length < AssetDatabase.LoadAssetAtPath<CookingCatalog>("Assets/Config/CookingCatalog.asset").Recipes.Count
                || cooking.TitleTexts.Length != cooking.CookButtons.Length || cooking.IngredientTexts.Length != cooking.CookButtons.Length || cooking.ResultTexts.Length != cooking.CookButtons.Length || cooking.RecipeScroll == null
                || cooking.CookButtons.Any(b => b == null) || cooking.TitleTexts.Concat(cooking.IngredientTexts).Concat(cooking.ResultTexts).Any(t => t == null))
                throw new System.Exception("CookingPanel needs wired recipe TMP and buttons on PopUp layer");
            if (storage == null || storage.Layer != UILayer.PopUp || storage.CloseButton == null || storage.MoveOneButton == null || storage.MoveStackButton == null || storage.SaveButton == null
                || storage.BagCapacityText == null || storage.ChestCapacityText == null || storage.DetailsText == null || storage.FeedbackText == null || storage.BagSlots == null || storage.ChestSlots == null
                || !storage.ShortcutsLayout || storage.DepositMatchingButton == null || storage.PageControls == null || storage.PageText == null || storage.PreviousPageButton == null || storage.NextPageButton == null
                || storage.DragPreview == null || storage.DragIcon == null || storage.DragNameText == null || storage.DragQuantityText == null || storage.DragPreview.GetComponent<CanvasGroup>() == null || storage.DragPreview.GetComponent<CanvasGroup>().blocksRaycasts
                || storage.BagSlots.Length != InventoryView.DisplaySlots || storage.BagSlots.Any(s => s == null || s.LockIcon == null || s.LockIcon.GetComponent<CanvasRenderer>() == null) || storage.ChestSlots.Length < 36
                || storage.BagSlots.Concat(storage.ChestSlots).Any(s => s == null || s.Icon == null || s.SelectButton == null || s.NameText == null || s.QuantityText == null || s.GetComponent<InventorySlotInput>() == null))
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
                || task.ClaimButton == null || task.CollapseButton == null || float.IsNaN(task.ExpandedHeight) || float.IsInfinity(task.ExpandedHeight) || task.ExpandedHeight < 52)
                throw new System.Exception("TaskPanel must have wired TMP and button references on Common layer.");
            CropInfoView cropInfo = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/CropInfoPanel.prefab").GetComponent<CropInfoView>();
            if (cropInfo == null || cropInfo.Group == null || cropInfo.NameText == null || cropInfo.DetailsText == null || cropInfo.HintText == null
                || cropInfo.Layer != UILayer.Common || cropInfo.Group.blocksRaycasts
                || cropInfo.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(graphic => graphic.raycastTarget))
                throw new System.Exception("CropInfoPanel must have wired TMP references and allow mouse input through it.");
            var prompt = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/InteractionPromptPanel.prefab").GetComponent<InteractionPromptView>();
            if (prompt == null || prompt.Layer != UILayer.Common || prompt.Group == null || prompt.Group.blocksRaycasts || prompt.Group.interactable
                || prompt.NameText == null || prompt.OperationText == null || prompt.DetailsText == null || prompt.StatusText == null || prompt.StatusMark == null
                || prompt.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(graphic => graphic.raycastTarget))
                throw new System.Exception("InteractionPromptPanel needs wired TMP/status fields on Common and must allow mouse through.");
            if (sleep == null || sleep.MessageText == null || sleep.ConfirmButton == null || sleep.CancelButton == null)
                throw new System.Exception("SleepPanel component references are incomplete.");
            ShopView shop = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/ShopPanel.prefab").GetComponent<ShopView>();
            if (shop == null || shop.CropButtons == null || shop.CropButtons.Length < 5 || shop.CropButtons.Any(button => button == null)
                || shop.CloseButton == null || shop.BuyButton == null || shop.SellButton == null || shop.SaveButton == null
                || shop.BalanceText == null || shop.StockText == null || shop.FeedbackText == null || shop.BuyText == null || shop.SellText == null)
                throw new System.Exception("ShopPanel component references are incomplete.");
            var fishing = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/FishingPanel.prefab").GetComponent<FishingView>();
            var backpack = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/InventoryUpgradePanel.prefab").GetComponent<InventoryUpgradeView>();
            if (backpack == null || backpack.Layer != UILayer.PopUp || backpack.CapacityText == null || backpack.NextText == null || backpack.CostText == null || backpack.GoldText == null || backpack.FeedbackText == null || backpack.UpgradeButton == null || backpack.SaveButton == null || backpack.CloseButton == null)
                throw new System.Exception("InventoryUpgradePanel needs wired TMP and buttons");
            var shipping = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/ShippingPanel.prefab").GetComponent<ShippingView>();
            if (shipping == null || shipping.Layer != UILayer.PopUp || shipping.BagSlots.Length != 30 || shipping.BoxSlots.Length != 18 || shipping.BagSlots.Any(s => s == null || s.LockIcon == null) || shipping.ExpectedIncomeText == null || shipping.CloseButton == null || shipping.SaveButton == null || shipping.MoveOneButton == null || shipping.MoveStackButton == null || shipping.PageControls == null) throw new System.Exception("ShippingPanel needs wired grid, locks, TMP and buttons");
            var report = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/DailyReportPanel.prefab").GetComponent<DailyReportView>();
            var pause = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/PausePanel.prefab").GetComponent<PauseView>();
            var preferences = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/SettingsPanel.prefab").GetComponent<SettingsView>();
            if (pause == null || pause.Layer != UILayer.PopUp || pause.TitleText == null || pause.MessageText == null || pause.ContinueButton == null || pause.SettingsButton == null || pause.ReturnMenuButton == null || preferences.FeedbackText == null) throw new System.Exception("Pause and settings require wired MVC components");
            foreach (string path in Directory.GetFiles(PrototypeUiPrefabBuilder.Folder, "*.prefab"))
            {
                var view = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/')).GetComponent<UIView>();
                var save = view.GetType().GetField("SaveButton")?.GetValue(view) as UnityEngine.UI.Button;
                if (save != null && save.gameObject.activeSelf) throw new System.Exception("Save buttons must be hidden outside failed sleep settlement: " + path);
            }
            if (report == null || report.Layer != UILayer.PopUp || report.DateText == null || report.IncomeText == null || report.ExpenseText == null || report.NetText == null
                || report.ShippingText == null || report.HarvestText == null || report.MatureText == null || report.EggsText == null || report.FeedbackText == null || report.ContinueButton == null || report.SaveButton == null || report.ReturnMenuButton == null)
                throw new System.Exception("DailyReportPanel needs wired TMP and buttons on PopUp layer");
            var coop = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/CoopPanel.prefab").GetComponent<CoopView>();
            var animalShop = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/AnimalShopPanel.prefab").GetComponent<AnimalShopView>();
            if (animalShop == null || animalShop.Layer != UILayer.PopUp || animalShop.QualitySellButton == null || animalShop.QualityEggPriceText == null)
                throw new System.Exception("AnimalShopPanel needs quality egg price and sale button");
            if (coop == null || coop.Layer != UILayer.PopUp || coop.GuideText == null || coop.PetButtons == null || coop.PetButtons.Length != 2 || coop.PetButtons.Any(b => b == null)
                || coop.ChickenNameTexts == null || coop.ChickenNameTexts.Length != 2 || coop.AffinityTexts == null || coop.AffinityTexts.Length != 2 || coop.CareTexts == null || coop.CareTexts.Length != 2
                || coop.HeartGraphics == null || coop.HeartGraphics.Length != 2 || coop.HeartGraphics.Any(g => g == null || g.raycastTarget || g.GetComponent<CanvasRenderer>() == null) || coop.ChickenNameTexts.Concat(coop.AffinityTexts).Concat(coop.CareTexts).Any(t => t == null)
                || coop.AffinityBars == null || coop.AffinityBars.Length != 2 || coop.AffinityBars.Any(b => b == null || b.fillRect == null || b.interactable || b.minValue != 0 || b.maxValue != 100))
                throw new System.Exception("CoopPanel needs two wired chicken cards, sliders, TMP and pet buttons");
            if (fishing == null || fishing.Layer != UILayer.PopUp || fishing.StatusText == null || fishing.HintText == null || fishing.TimerText == null || fishing.FeedbackText == null
                || fishing.ProgressFill == null || fishing.CastButton == null || fishing.ReelButton == null || fishing.CloseButton == null || fishing.SaveButton == null) throw new System.Exception("FishingPanel needs TMP, progress and buttons on PopUp layer");
            Debug.Log("UI prefab validation succeeded; AA addresses match names.");
        }
    }
}
