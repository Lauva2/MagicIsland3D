using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.CameraSystem;
using MagicIsland.Managers;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class CameraOcclusionFlowVerification
    {
        const string Key = "MagicIsland.CameraOcclusionVerification";
        static CameraOcclusionFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            var paths = CameraOcclusionSetup.PrefabPaths.Concat(new[] { "Assets/Config/GameConfig.asset" }).ToArray();
            var bytes = paths.Select(File.ReadAllBytes).ToArray();
            PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Repeat setup preserves occlusion settings and artist edits");
            foreach (string path in CameraOcclusionSetup.PrefabPaths)
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<CameraOccluder>(true).Length > 0, "Configured AA model " + path);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool condition, string why) { if (!condition) throw new Exception(why); }
        static void Phase(int phase) { SessionState.SetInt(Key + ".Phase", phase); Debug.Log("Camera occlusion phase " + phase); }
        static Camera View(GameManager g) => g.Players.Player.ViewCamera;
        static CameraController Controller(GameManager g) => View(g).GetComponent<CameraController>();
        static void Update(GameManager g, float dt = 1f) => typeof(CameraManager).GetMethod("UpdateOcclusion", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(g.Cameras, new object[] { dt });
        static void Position(GameManager g, Vector3 point) { g.Players.RestorePosition(point); Controller(g).SnapToTarget(); }
        static CameraOccluder Tree(GameManager g) => g.Gathering.Nodes[0].GetComponentInChildren<CameraOccluder>();
        static int Copies() => Resources.FindObjectsOfTypeAll<Material>().Count(m => m.name.EndsWith(" (Camera Fade)", StringComparison.Ordinal));
        static void Capture(GameManager g, string name)
        {
            g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); GameUiPreview.CaptureInventory(View(g));
            File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 8 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Camera occlusion timeout " + phase);
                if (!EditorApplication.isPlaying) return;
                var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Camera flow bootstrap");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    string directory = Path.GetFullPath("Logs/CameraOcclusionSaves/" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(directory);
                    g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, directory, g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting, g.Configuration.Animals);
                    g.Settings.Initialize(Path.Combine(directory, "Settings"));
                    g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Fixtures(g);
                    Position(g, g.Gathering.Nodes[0].transform.position + new Vector3(0, .1f, 1.6f));
                    var tree = Tree(g); g.Cameras.SetOcclusionEnabled(false); Capture(g, "CameraTreeOpaquePreview");
                    g.Cameras.SetOcclusionEnabled(true); Update(g);
                    Require(tree.Opacity < .3f && tree.RuntimeMaterialCount > 0, "AA tree crown fades while player is behind it");
                    Require(g.Gathering.Nodes[0].WholeVisual.GetComponentsInChildren<Collider>().All(c => c.enabled), "Fade retains gathering colliders");
                    Capture(g, "CameraTreeFadePreview");
                    Position(g, g.Gathering.Nodes[0].transform.position + new Vector3(3, .1f, 1.6f)); Update(g);
                    Require(tree.Opacity == 1 && tree.RuntimeMaterialCount == 0, "Moving sideways restores exact AA tree materials");
                    Position(g, g.Gathering.Nodes[0].transform.position + new Vector3(0, .1f, 1.6f)); Update(g);
                    g.Gathering.Nodes[0].WholeVisual.SetActive(false);
                    Require(tree.Opacity == 1 && tree.RuntimeMaterialCount == 0, "Depleted tree visual immediately releases copies");
                    g.Gathering.Nodes[0].WholeVisual.SetActive(true);
                    var hall = g.Orders.Hall.GetComponent<CameraOccluder>(); var body = hall.Renderers.First(r => r.name == "Walls").bounds;
                    Position(g, new Vector3(body.center.x, .1f, body.max.z + .5f)); Update(g);
                    Require(hall.Opacity < .3f && hall.Renderers.All(r => r.GetComponentInParent<MagicIsland.Interaction.OrderBoard>() == null), "Hall shell fades but noticeboard remains opaque");
                    Capture(g, "CameraHallFadePreview");
                    Require(g.Inventory.TryAdd("wood", g.Configuration.Animals.BuildWood) && g.Inventory.TryAdd("stone", g.Configuration.Animals.BuildStone), "Prepare coop fixture materials");
                    g.Economy.TryEarn(g.Configuration.Animals.BuildGold);
                    Require(g.Animals.Build(), "Build actual AA coop");
                    var coop = g.Animals.Coop.GetComponent<CameraOccluder>(); var coopBody = coop.Renderers.First(r => r.name == "House").bounds;
                    Position(g, new Vector3(coopBody.center.x, .1f, coopBody.max.z + .5f)); Update(g);
                    Require(coop.Opacity < .3f && coop.Renderers.All(r => r.transform.parent.name != "Ready Eggs"), "Actual coop shell fades while egg marker is excluded");
                    g.Players.RestorePosition(g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); g.EnterHome();
                    Require(hall.Opacity == 1 && hall.RuntimeMaterialCount == 0, "Transition immediately restores exterior materials"); Phase(2);
                }
                else if (phase == 2 && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    var wall = g.Housing.Interior.OriginalWalls.First(w => w.name == "Left Wall").GetComponent<CameraOccluder>();
                    Require(wall != null && g.Housing.Interior.transform.Find("Floor").GetComponent<CameraOccluder>() == null, "AA interior marks walls but excludes floor");
                    // A slightly angled view demonstrates side-wall occlusion, including a camera that has not caught up.
                    View(g).transform.rotation = Quaternion.Euler(42, 25, 0);
                    PositionForView(g, g.Configuration.Housing.InteriorOrigin + new Vector3(-5.45f, .1f, 0)); Update(g);
                    Require(wall.Opacity < .3f, "Indoor wall fades for angled orthographic camera"); Capture(g, "CameraInteriorFadePreview");
                    Require(Tree(g).RuntimeMaterialCount == 0, "Interior culling excludes exterior occluders");
                    g.OpenPause(); Phase(3);
                }
                else if (phase == 3 && Object.FindObjectOfType<PauseView>() != null)
                {
                    Update(g); Require(g.Housing.Interior.OriginalWalls.Any(w => w.GetComponent<CameraOccluder>()?.Opacity < .3f), "Modal pause retains visibility despite disabled camera movement");
                    g.ResumeFromPause();
                    Require(g.Inventory.TryAdd("wood", g.Configuration.Housing.UpgradeWood) && g.Inventory.TryAdd("stone", g.Configuration.Housing.UpgradeStone), "Prepare expansion fixture materials");
                    g.Economy.TryEarn(g.Configuration.Housing.UpgradeGold);
                    Require(g.Housing.ApplyUpgrade(g.Farm.State.Day, g.Inventory, g.Economy) == MagicIsland.Interaction.HouseUpgradeResult.Success, "Request actual expansion");
                    Require(g.Housing.NextDay(g.Farm.State.Day + g.Configuration.Housing.BuildDays), "Complete expansion fixture");
                    Require(g.Housing.Interior.OriginalWalls.All(w => !w.activeSelf && (w.GetComponent<CameraOccluder>() == null || w.GetComponent<CameraOccluder>().RuntimeMaterialCount == 0)), "Expansion disables and restores original walls");
                    var expandedWall = g.Housing.Interior.GetComponentsInChildren<CameraOccluder>().First(c => c.name == "Expanded Left Wall");
                    PositionForView(g, g.Configuration.Housing.InteriorOrigin + new Vector3(-5.45f, .1f, 0)); Update(g);
                    Require(expandedWall.Opacity < .3f, "New AA expansion walls join occlusion automatically");
                    Capture(g, "CameraExpandedInteriorFadePreview");
                    Controller(g).Configure(g.Configuration.Config.CameraPitch, g.Configuration.Config.CameraSize);
                    Position(g, g.Configuration.Housing.InteriorOrigin + new Vector3(0, .1f, -3.6f)); g.ExitHome(); Phase(4);
                }
                else if (phase == 4 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                {
                    Require(g.Housing.Interior.GetComponentsInChildren<CameraOccluder>(true).All(c => c.Opacity == 1 && c.RuntimeMaterialCount == 0), "Leaving home restores all wall materials");
                    Position(g, g.Gathering.Nodes[0].transform.position + new Vector3(0, .1f, 1.6f)); Update(g);
                    Require(Tree(g).Opacity < .3f, "Returning island resumes automatic fade");
                    g.ReturnToMainMenu(); Phase(5);
                }
                else if (phase == 5 && Object.FindObjectOfType<PauseView>() != null)
                {
                    Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();
                    Require(Tree(g) == null || Tree(g).RuntimeMaterialCount == 0, "Menu transition restores before unloading"); Phase(6);
                }
                else if (phase == 6 && g.State == GameFlowState.MainMenu)
                {
                    Require(Copies() == 0, "No camera fade materials survive unloading");
                    g.EnterGame(); Phase(7);
                }
                else if (phase == 7 && g.State == GameFlowState.Playing)
                {
                    Position(g, g.Gathering.Nodes[0].transform.position + new Vector3(0, .1f, 1.6f)); Update(g);
                    Require(Tree(g).Opacity < .3f, "New game after unload registers fresh AA objects");
                    Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CameraOcclusionVerification.txt", "PASS: AA trees/buildings/interior walls, orthographic rays, layers, smooth fade, exact restoration, pause, transitions, unload/re-entry, material cleanup.\n");
                    Debug.Log("Camera occlusion flow verification PASSED."); Phase(8); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception exception) { Debug.LogException(exception); SessionState.SetBool(Key, false); EditorApplication.isPlaying = false; EditorApplication.Exit(1); }
        }
        static void PositionForView(GameManager g, Vector3 point)
        {
            g.Players.RestorePosition(point); View(g).transform.position = point + Vector3.up * .6f - View(g).transform.forward * 18;
        }
        static void Fixtures(GameManager g)
        {
            var root = new GameObject("Occlusion fixtures"); var source = new Material(Shader.Find("Standard")) { color = new Color(.2f, .5f, .3f, .75f) };
            var materialBytes = EditorJsonUtility.ToJson(source); var view = View(g); var foot = g.Players.Player.transform.position;
            var created = new System.Collections.Generic.List<CameraOccluder>();
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(cube.GetComponent<Collider>());
                    cube.transform.SetParent(root.transform); cube.transform.position = foot + Vector3.up * .95f - view.transform.forward * (3 + i * 3);
                    cube.transform.localScale = Vector3.one * 1.3f;
                    var renderer = cube.GetComponent<Renderer>(); renderer.sharedMaterials = new[] { source, source, null }; renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                    var marker = cube.AddComponent<CameraOccluder>(); marker.Renderers = new[] { renderer, renderer }; created.Add(marker);
                }
                var first = created[0]; var second = created[1]; var surface = first.Renderers[0];
                g.Cameras.RestoreOccluders(); Update(g, .05f);
                Require(first.Opacity < 1 && first.Opacity > g.Configuration.Config.CameraOccludedOpacity, "Fade progresses smoothly rather than snapping");
                Update(g); Require(created.All(c => Mathf.Approximately(c.Opacity, g.Configuration.Config.CameraOccludedOpacity) && c.RuntimeMaterialCount == 1), "All blockers fade and duplicate/shared/null material slots allocate once");
                Material copy = surface.sharedMaterial; Require(Mathf.Approximately(copy.color.a, .75f * first.Opacity) && copy.GetInt("_ZWrite") == 0 && copy.IsKeywordEnabled("_ALPHABLEND_ON"), "Standard transparency multiplies original alpha");
                Require(materialBytes == EditorJsonUtility.ToJson(source) && surface.shadowCastingMode == ShadowCastingMode.Off, "Source material unchanged and faded object casts no opaque shadow");
                for (int i = 0; i < 50; i++) Update(g, .02f);
                Require(surface.sharedMaterial == copy && first.RuntimeMaterialCount == 1, "No per-frame material allocation");
                first.AllowFade = false; Update(g); Require(first.Opacity == 1 && surface.sharedMaterial == source && surface.shadowCastingMode == ShadowCastingMode.TwoSided, "Per-component opt-out restores original shadow mode");
                first.AllowFade = true; first.gameObject.layer = 30; int mask = view.cullingMask; view.cullingMask &= ~(1 << 30); Update(g);
                Require(first.RuntimeMaterialCount == 0 && second.Opacity < 1, "Renderer culling mask ignores hidden area only"); view.cullingMask = mask; first.gameObject.layer = 0;
                first.Renderers[0].enabled = false; Update(g); Require(first.Opacity == 1, "Disabled renderer cannot occlude"); first.Renderers[0].enabled = true;
                first.transform.position = foot + Vector3.up * .95f + view.transform.forward * 3; Update(g); Require(first.Opacity == 1, "Object behind player cannot occlude");
                first.transform.position = foot + Vector3.up * .95f - view.transform.forward * 3;
                view.transform.position += Vector3.right * 2; Update(g); Require(first.Opacity < 1, "Orthographic camera lag uses parallel rays through target"); Controller(g).SnapToTarget();
                first.enabled = false; Require(first.RuntimeMaterialCount == 0 && surface.sharedMaterials[1] == source && surface.sharedMaterials[2] == null, "Disable restores all material slots immediately"); first.enabled = true;
                Update(g); g.Cameras.SetOcclusionEnabled(false); Require(created.All(c => c.RuntimeMaterialCount == 0), "Flow suspension restores copies"); g.Cameras.SetOcclusionEnabled(true);
                Update(g); g.Cameras.Clear(); Require(created.All(c => c.RuntimeMaterialCount == 0), "Manager Clear restores before releasing references");
                g.Cameras.Initialize(Controller(g), g.Configuration.Config); g.Cameras.SetOcclusionEnabled(true);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(source); }
        }
    }
}
