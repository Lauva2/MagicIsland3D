using System;
using System.Collections.Generic;
using System.IO;
using MagicIsland.CalendarSystem;
using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Magic;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;

namespace MagicIsland.EditorTools
{
    // Fill missing content only. Never rebuild an authored plant, effect, layout or palette during Setup.
    public static class MagicContentSetup
    {
        const string Art = "Assets/Art/Magic";
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group, ItemCatalog items)
        {
            Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            const string configPath = "Assets/Config/MagicConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<MagicConfig>(configPath);
            if (config == null) { config = ScriptableObject.CreateInstance<MagicConfig>(); AssetDatabase.CreateAsset(config, configPath); }
            Register(settings, group, configPath, MagicConfig.Address);
            AddItem(items, config.SeedId, "月光花种子", ItemType.Seed, 25, 0, "四季可种的魔法植物。每天浇水，四次睡觉后成熟。成熟的花瓣会在夜间发光。", new Color(.58f, .69f, .91f));
            AddItem(items, config.FlowerId, "月光花", ItemType.Crop, 0, 60, "亲手照料的魔法花朵。可在魔法手札中用于研习唤雨术，也可出售。", new Color(.65f, .85f, 1));
            AddItem(items, config.QualityFlowerId, "优质月光花", ItemType.Crop, 0, 90, "花瓣中凝结着更明亮的月光。研习魔法时优先消耗普通月光花。", new Color(.84f, .77f, 1));
            var crops = AssetDatabase.LoadAssetAtPath<CropCatalog>("Assets/Config/CropCatalog.asset");
            if (crops.Find(config.CropId) == null)
            {
                crops.Crops.Add(new CropConfig { Id = config.CropId, SeedItemId = config.SeedId, HarvestItemId = config.FlowerId, QualityHarvestItemId = config.QualityFlowerId,
                    GrowthDays = 4, Shape = CropShape.Bush, MatureColor = new Color(.65f, .85f, 1), PlantingSeasons = SeasonMask.All,
                    SeedlingPrefabAddress = "MoonflowerSeedling", GrowingPrefabAddress = "MoonflowerGrowing", MaturePrefabAddress = "MoonflowerMature" });
                EditorUtility.SetDirty(crops);
            }
            BuildPlants(settings, group);
            BuildRain(settings, group);
            BuildPanel(); UpgradeHud(); UpgradeShop();
        }
        static void Register(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        {
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
            if (entry.address != address) entry.SetAddress(address);
        }
        static void AddItem(ItemCatalog items, string id, string name, ItemType type, int buy, int sell, string description, Color color)
        {
            if (items.Find(id) != null) return;
            items.Items.Add(new ItemConfig { Id = id, DisplayName = name, Type = type, BuyPrice = buy, SellPrice = sell, MaxStack = 99,
                Description = description, Icon = items.Find("turnip_seed").Icon, IconColor = color }); EditorUtility.SetDirty(items);
        }
        static Material Mat(string name, Color color, bool glow = false)
        {
            string path = Art + "/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find(glow ? "Magic Island/Moonflower" : "Standard")) { name = name, color = color };
            if (glow) mat.SetFloat("_Glow", 1.15f); else mat.SetFloat("_Glossiness", .18f);
            AssetDatabase.CreateAsset(mat, path); return mat;
        }
        static CombineInstance Piece(Mesh mesh, Vector3 position, Vector3 scale, Quaternion rotation = default)
            => new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation == default ? Quaternion.identity : rotation, scale) };
        static Mesh MeshAsset(string name, List<CombineInstance> pieces)
        {
            string path = Art + "/" + name + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            mesh = new Mesh { name = name }; mesh.CombineMeshes(pieces.ToArray(), true, true); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        static void MeshPart(Transform parent, string name, Mesh mesh, Material mat)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.layer = 2; part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        static void BuildPlants(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            Mesh bulb = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Crops/FacetedBulb.asset");
            Mesh stem = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Crops/OctagonalStem.asset");
            var green = Mat("MoonflowerLeaves", new Color(.23f, .48f, .43f)); var blue = Mat("MoonflowerPetals", new Color(.55f, .79f, 1), true);
            var heart = Mat("MoonflowerHeart", new Color(.97f, .86f, .53f), true);
            foreach (var stage in new[] { "Seedling", "Growing", "Mature" })
            {
                string name = "Moonflower" + stage, path = Art + "/" + name + ".prefab";
                if (!File.Exists(path))
                {
                    var roots = new List<CombineInstance>(); var petals = new List<CombineInstance>(); var centers = new List<CombineInstance>();
                    bool mature = stage == "Mature", young = stage == "Seedling"; float height = young ? .18f : mature ? .62f : .38f;
                    roots.Add(Piece(stem, Vector3.zero, new Vector3(.035f, height, .035f)));
                    for (int i = 0; i < (young ? 2 : 5); i++)
                    {
                        float angle = i * 137.5f; var rotation = Quaternion.Euler(0, angle, 32);
                        roots.Add(Piece(bulb, Quaternion.Euler(0, angle, 0) * new Vector3(.12f, height * (.3f + i * .08f), 0), new Vector3(.27f, .075f, .12f), rotation));
                    }
                    if (!young)
                    {
                        int heads = mature ? 3 : 1;
                        for (int h = 0; h < heads; h++)
                        {
                            var center = new Vector3(h == 1 ? -.20f : h == 2 ? .20f : 0, height - h * .085f, h == 0 ? 0 : .10f);
                            if (h > 0) roots.Add(Piece(stem, new Vector3(center.x, 0, center.z), new Vector3(.025f, center.y, .025f)));
                            if (mature)
                            {
                                for (int i = 0; i < 5; i++)
                                {
                                    var rot = Quaternion.Euler(0, i * 72 + h * 23, 0);
                                    petals.Add(Piece(bulb, center + rot * new Vector3(0, .01f, .09f), new Vector3(.115f, .065f, .21f), rot));
                                }
                                centers.Add(Piece(bulb, center + Vector3.up * .04f, new Vector3(.10f, .075f, .10f)));
                            }
                            else petals.Add(Piece(bulb, center, new Vector3(.09f, .15f, .09f)));
                        }
                    }
                    var root = new GameObject(name); root.layer = 2;
                    try
                    {
                        MeshPart(root.transform, "Leaves and stems", MeshAsset(name + "Green", roots), green);
                        if (petals.Count > 0) MeshPart(root.transform, "Moon petals", MeshAsset(name + "Petals", petals), blue);
                        if (centers.Count > 0) MeshPart(root.transform, "Golden hearts", MeshAsset(name + "Hearts", centers), heart);
                        root.AddComponent<CropArtVisual>().ArtVersion = 1; PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                Register(settings, group, path, name);
            }
        }
        static void BuildRain(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            string path = Art + "/MagicRainEffect.prefab";
            if (!File.Exists(path))
            {
                var root = new GameObject("MagicRainEffect"); root.layer = 2;
                try
                {
                    var effect = root.AddComponent<MagicRainEffect>();
                    var child = new GameObject("Rain", typeof(ParticleSystem)); child.layer = 2; child.transform.SetParent(root.transform, false); child.transform.localPosition = Vector3.up * 2.1f;
                    var rain = child.GetComponent<ParticleSystem>(); rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = rain.main; main.playOnAwake = false; main.loop = false; main.duration = .8f; main.startLifetime = .7f; main.startSpeed = 0; main.startSize = .045f; main.maxParticles = 128; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new Color(.62f, .87f, 1, .85f);
                    var emission = rain.emission; emission.rateOverTime = 100;
                    var shape = rain.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(3.75f, .01f, 3.75f);
                    var velocity = rain.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = -3.8f;
                    rain.useAutoRandomSeed = false; rain.randomSeed = 7351;
                    string matPath = Art + "/RainDrops.mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null) { mat = new Material(Shader.Find("Particles/Standard Unlit")); mat.SetFloat("_Mode", 2); mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 0); mat.EnableKeyword("_ALPHABLEND_ON"); mat.renderQueue = 3000; AssetDatabase.CreateAsset(mat, matPath); }
                    var renderer = rain.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = mat; renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 4; renderer.velocityScale = .02f; renderer.shadowCastingMode = ShadowCastingMode.Off;
                    effect.Rain = rain;
                    var ring = new GameObject("Rune circle", typeof(MeshFilter), typeof(MeshRenderer)); ring.layer = 2; ring.transform.SetParent(root.transform, false); ring.transform.localPosition = Vector3.up * .035f;
                    var mesh = new Mesh { name = "Rain rune ring" }; var vertices = new List<Vector3>(); var indices = new List<int>();
                    for (int i = 0; i < 64; i++)
                    {
                        float a = i * Mathf.PI / 32, b = (i + 1) * Mathf.PI / 32; int k = vertices.Count;
                        vertices.AddRange(new[] { new Vector3(Mathf.Sin(a) * .46f, 0, Mathf.Cos(a) * .46f), new Vector3(Mathf.Sin(a) * .48f, 0, Mathf.Cos(a) * .48f), new Vector3(Mathf.Sin(b) * .46f, 0, Mathf.Cos(b) * .46f), new Vector3(Mathf.Sin(b) * .48f, 0, Mathf.Cos(b) * .48f) });
                        indices.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                    }
                    mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    string meshPath = Art + "/RainRune.asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (previous == null) AssetDatabase.CreateAsset(mesh, meshPath); else { Object.DestroyImmediate(mesh); mesh = previous; }
                    ring.GetComponent<MeshFilter>().sharedMesh = mesh; ring.GetComponent<MeshRenderer>().sharedMaterial = Mat("RainRune", new Color(.38f, .80f, .95f), true); ring.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    effect.Ring = ring.transform; PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { Object.DestroyImmediate(root); }
            }
            Register(settings, group, path, "MagicRainEffect");
        }
        [MenuItem("Magic Island/Art/Rebuild Magic Rain Rune Facing (Explicit)")]
        public static void RebuildRuneFacing()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/RainRune.asset");
            if (mesh == null) return;
            mesh.RecalculateNormals();
            if (mesh.normals.Length > 0 && mesh.normals[0].y < 0)
            {
                var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3) { int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap; }
                mesh.triangles = triangles; mesh.RecalculateNormals(); EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();
            }
        }
        static void BuildPanel()
        {
            string path = Factory.Folder + "/MagicPanel.prefab"; if (File.Exists(path)) return;
            var root = Factory.CanvasRoot("MagicPanel");
            try
            {
                var view = root.AddComponent<MagicView>(); view.Layer = UILayer.PopUp; view.SleepOnlyLayout = true;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.09f, .14f, .24f, .70f)); shade.anchorMax = Vector2.one; shade.GetComponent<Image>().sprite = null;
                var card = Factory.Panel("Moonlit journal", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(760, 594), Factory.Cream); Factory.SoftShadow(card);
                view.Title = Factory.Label("Title", card, new Vector2(32, -24), new Vector2(696, 44), "魔法手札 · 月光的回响", 30);
                view.Progress = Factory.Label("Progress", card, new Vector2(32, -80), new Vector2(696, 30), "启蒙进度 0 / 3", 20);
                view.Story = Factory.Label("Story", card, new Vector2(32, -126), new Vector2(696, 136), "研读古老符文", 22);
                var spell = Factory.Panel("Rain spell", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -280), new Vector2(696, 136), new Color(.83f, .90f, .95f));
                view.Spell = Factory.Label("Spell", spell, new Vector2(18, -10), new Vector2(660, 116), "唤雨术", 20);
                view.Feedback = Factory.Label("Feedback", card, new Vector2(32, -432), new Vector2(696, 52), "", 18);
                view.StudyButton = Factory.Button("Study", card, new Vector2(32, -510), new Vector2(490, 48), "研读符文", new Color(.71f, .82f, .94f)); view.ActionLabel = view.StudyButton.GetComponentInChildren<TMP_Text>();
                view.CloseButton = Factory.Button("Close", card, new Vector2(540, -510), new Vector2(188, 48), "关闭（K）", Factory.Sand);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void UpgradeHud()
        {
            string path = Factory.Folder + "/FarmHudPanel.prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<PrototypeHud>().MagicButton != null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hud = root.GetComponent<PrototypeHud>(); hud.MagicButton = Factory.Button("Magic", root.transform, Vector2.zero, new Vector2(108, 36), "魔法（K）", new Color(.75f, .82f, .94f));
                var rect = hud.MagicButton.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-374, -20);
                hud.MagicButton.GetComponentInChildren<TMP_Text>().fontSize = 18;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void UpgradeShop()
        {
            string path = Factory.Folder + "/ShopPanel.prefab"; var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var crops = AssetDatabase.LoadAssetAtPath<CropCatalog>("Assets/Config/CropCatalog.asset");
            int count = crops.Crops.Count * 2 + AssetDatabase.LoadAssetAtPath<FishingCatalog>("Assets/Config/FishingCatalog.asset").Fish.Count
                + AssetDatabase.LoadAssetAtPath<OrchardCatalog>("Assets/Config/OrchardCatalog.asset").Types.Count + BeachPickup.Items.Length;
            if (prefab.GetComponent<ShopView>().CropButtons.Length >= count) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<ShopView>(); int old = view.CropButtons.Length; Array.Resize(ref view.CropButtons, count);
                for (int i = old; i < count; i++)
                {
                    view.CropButtons[i] = Factory.Button("Magic Offer " + i, view.BalanceText.transform.parent, new Vector2(32 + i % 5 * 130, -130 - i / 5 * 50), new Vector2(122, 40), "魔法作物", Factory.Sand);
                    view.CropButtons[i].GetComponentInChildren<TMP_Text>().fontSize = 17;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
