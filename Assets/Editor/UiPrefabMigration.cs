using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using MagicIsland.Loading;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // In-place migration: preserve the edited hierarchy, transforms, text and colors.
    public static class UiPrefabMigration
    {
        private const string FontPath = "Assets/Art/Fonts/IslandUI.asset";
        public static TMP_FontAsset FontAsset
        {
            get
            {
                TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (asset != null) return asset;
                if (Shader.Find("TextMeshPro/Distance Field") == null)
                {
                    string resolved = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.textmeshpro").resolvedPath;
                    AssetDatabase.ImportPackage(Path.Combine(resolved, "Package Resources/TMP Essential Resources.unitypackage"), false);
                    throw new InvalidOperationException("Importing TMP Essential Resources. Run the UI upgrade again after import completes.");
                }
                Directory.CreateDirectory("Assets/Art/Fonts");
                const string temporaryFont = "Assets/Art/Fonts/IslandUI-BakeSource.ttf";
                string systemFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "simhei.ttf");
                File.Copy(systemFont, temporaryFont, true);
                AssetDatabase.ImportAsset(temporaryFont);
                Font source = AssetDatabase.LoadAssetAtPath<Font>(temporaryFont);
                asset = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 2048, 2048);
                asset.name = "IslandUI";
                string characters = string.Concat(Enumerable.Range(32, 95).Select(c => (char)c)) + "×·／—…";
                foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(PrototypeUiPrefabBuilder.Folder, "*.prefab"))
                    .Concat(Directory.GetFiles("Assets/Config", "*.asset"))
                    .Concat(Directory.GetFiles("Assets/Editor", "*.cs")))
                    characters += File.ReadAllText(path);
                characters = System.Text.RegularExpressions.Regex.Replace(characters, @"\\u([0-9a-fA-F]{4})",
                    match => ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString());
                characters = new string(characters.Where(c => !char.IsControl(c)).Distinct().ToArray());
                if (!asset.TryAddCharacters(characters, out string missing))
                    Debug.LogWarning("IslandUI font lacks characters: " + missing);
                asset.atlasPopulationMode = AtlasPopulationMode.Static;
                var serializedFont = new SerializedObject(asset);
                serializedFont.FindProperty("m_SourceFontFile").objectReferenceValue = null;
                serializedFont.FindProperty("m_SourceFontFile_EditorRef").objectReferenceValue = null;
                serializedFont.FindProperty("m_SourceFontFileGUID").stringValue = "";
                serializedFont.ApplyModifiedPropertiesWithoutUndo();
                Directory.CreateDirectory("Assets/Art/Fonts");
                AssetDatabase.Refresh();
                AssetDatabase.CreateAsset(asset, FontPath);
                asset.material.name = "IslandUI Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                foreach (Texture2D texture in asset.atlasTextures)
                {
                    texture.name = "IslandUI Atlas";
                    AssetDatabase.AddObjectToAsset(texture, asset);
                }
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.DeleteAsset(temporaryFont);
                return asset;
            }
        }

        public static void EnsureChineseFont(TMP_FontAsset font)
        {
            string characters = "";
            foreach (string path in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(PrototypeUiPrefabBuilder.Folder, "*.prefab"))
                .Concat(Directory.GetFiles("Assets/Config", "*.asset")))
                characters += File.ReadAllText(path);
            characters = System.Text.RegularExpressions.Regex.Replace(characters, @"\\u([0-9a-fA-F]{4})",
                match => ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString());
            string missing = new string(characters.Where(c => !char.IsControl(c) && !font.HasCharacter(c)).Distinct().ToArray());
            if (missing.Length > 0)
            {
                const string temporaryFont = "Assets/Art/Fonts/IslandUI-BakeSource.ttf";
                File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "simhei.ttf"), temporaryFont, true);
                AssetDatabase.ImportAsset(temporaryFont);
                try
                {
                    var serialized = new SerializedObject(font);
                    serialized.FindProperty("m_SourceFontFile_EditorRef").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(temporaryFont);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    if (!font.TryAddCharacters(missing, out string unavailable))
                        Debug.LogWarning("Chinese UI font could not bake: " + unavailable);
                    foreach (Texture2D texture in font.atlasTextures)
                        if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
                    font.atlasPopulationMode = AtlasPopulationMode.Static;
                    serialized.Update();
                    serialized.FindProperty("m_SourceFontFile_EditorRef").objectReferenceValue = null;
                    serialized.FindProperty("m_SourceFontFileGUID").stringValue = "";
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(font);
                    foreach (Texture2D texture in font.atlasTextures) EditorUtility.SetDirty(texture);
                    AssetDatabase.SaveAssets();
                }
                finally { AssetDatabase.DeleteAsset(temporaryFont); }
            }
            // New TMP components and text using the Latin default can also display Chinese.
            var settings = new SerializedObject(TMP_Settings.instance);
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            SerializedProperty fallback = settings.FindProperty("m_fallbackFontAssets");
            bool registered = false;
            for (int i = 0; i < fallback.arraySize; i++)
                registered |= fallback.GetArrayElementAtIndex(i).objectReferenceValue == font;
            if (!registered)
            {
                fallback.InsertArrayElementAtIndex(fallback.arraySize);
                fallback.GetArrayElementAtIndex(fallback.arraySize - 1).objectReferenceValue = font;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        public static TextAlignmentOptions Alignment(TextAnchor anchor)
        {
            TextAlignmentOptions[] options = { TextAlignmentOptions.TopLeft, TextAlignmentOptions.Top,
                TextAlignmentOptions.TopRight, TextAlignmentOptions.Left, TextAlignmentOptions.Center,
                TextAlignmentOptions.Right, TextAlignmentOptions.BottomLeft, TextAlignmentOptions.Bottom,
                TextAlignmentOptions.BottomRight };
            return options[(int)anchor];
        }

        [MenuItem("Magic Island/Upgrade UI To TMP And Layers")]
        public static void UpgradeAll()
        {
            TMP_FontAsset font = FontAsset;
            EnsureChineseFont(font);
            foreach (string path in Directory.GetFiles(PrototypeUiPrefabBuilder.Folder, "*.prefab"))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Text[] oldLabels = root.GetComponentsInChildren<Text>(true);
                    UIView view = root.GetComponent<UIView>();
                    // Only initialize layers once; later Inspector edits remain authoritative.
                    bool needsLayer = !File.ReadAllText(path).Contains("  layer:");
                    bool repairedFont = false;
                    foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (label.font != null && label.font.HasCharacter('岛')) continue;
                        label.font = font;
                        repairedFont = true;
                    }
                    if (oldLabels.Length == 0 && !needsLayer)
                    {
                        if (repairedFont) PrefabUtility.SaveAsPrefabAsset(root, path);
                        continue;
                    }
                    foreach (Text old in oldLabels)
                    {
                        GameObject target = old.gameObject;
                        string content = old.text;
                        Color color = old.color;
                        int size = old.fontSize;
                        TextAnchor alignment = old.alignment;
                        FontStyle style = old.fontStyle;
                        bool raycast = old.raycastTarget;
                        bool wrapping = old.horizontalOverflow == HorizontalWrapMode.Wrap;
                        bool autoSize = old.resizeTextForBestFit;
                        int min = old.resizeTextMinSize, max = old.resizeTextMaxSize;
                        Object.DestroyImmediate(old);
                        TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
                        text.font = font;
                        text.text = content;
                        text.fontSize = size;
                        text.color = color;
                        text.alignment = Alignment(alignment);
                        text.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : style == FontStyle.Italic
                            ? FontStyles.Italic : style == FontStyle.BoldAndItalic ? FontStyles.Bold | FontStyles.Italic : FontStyles.Normal;
                        text.raycastTarget = raycast;
                        text.enableWordWrapping = wrapping;
                        text.enableAutoSizing = autoSize;
                        text.fontSizeMin = min;
                        text.fontSizeMax = max;
                    }
                    // Component types changed, so serialize their new references explicitly.
                    if (view is MainMenuView menu)
                    {
                        menu.StatusText = Label(root, "Status");
                        if (needsLayer) menu.Layer = UILayer.Bottom;
                    }
                    else if (view is PrototypeHud hud)
                    {
                        hud.DayText = Label(root, "Day");
                        hud.InventoryText = Label(root, "Inventory");
                        hud.HintText = Label(root, "Target Hint");
                        hud.FeedbackText = Label(root, "Feedback");
                    }
                    else if (view is InventoryView bag)
                    {
                        bag.CapacityText = Label(root, "Capacity");
                        bag.DetailTitleText = Label(root, "Item Name");
                        bag.DetailText = Label(root, "Description");
                        if (needsLayer) bag.Layer = UILayer.PopUp;
                    }
                    foreach (InventorySlotView slot in root.GetComponentsInChildren<InventorySlotView>(true))
                    {
                        slot.NameText = Label(slot.gameObject, "Label");
                        slot.QuantityText = Label(slot.gameObject, "Quantity");
                    }
                    if (view != null) root.GetComponent<Canvas>().sortingOrder = (int)view.Layer * 1000;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static TMP_Text Label(GameObject root, string name) =>
            root.GetComponentsInChildren<TMP_Text>(true).First(text => text.name == name);
    }
}
