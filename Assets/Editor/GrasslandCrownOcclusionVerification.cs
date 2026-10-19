using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using MagicIsland.CameraSystem;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandCrownOcclusionVerification
    {
        const string Folder = "Logs/GrasslandCrownOcclusion";
        static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException("Crown occlusion verification: " + why); }
        static string Hash(string p) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p))); }
        static readonly MethodInfo update = typeof(CameraManager).GetMethod("UpdateOcclusion", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo blocks = typeof(LandscapeCrownOccluder).GetMethod("Blocks", BindingFlags.Instance | BindingFlags.NonPublic);
        static void Tick(GameManager g, float seconds) => update.Invoke(g.Cameras, new object[] { seconds });
        static bool Blocks(LandscapeCrownOccluder c, int i, Camera camera, Vector3 point) => (bool)blocks.Invoke(c, new object[] { i, camera, point, .1f });
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/Logs/"), "isolated project required");
            string path = TerrainSetup.PrefabPath;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var plan = GrasslandCrownOcclusionSetup.Plan(root.GetComponent<IslandTerrain>());
                Check(plan.Length == 8 && plan.Sum(e => e.Crowns.Length) == 21, "21 original trees in 8 existing renderers");
                var partial = plan[0].Filter.gameObject.AddComponent<LandscapeCrownOccluder>(); bool refused = false;
                try { GrasslandCrownOcclusionSetup.Plan(root.GetComponent<IslandTerrain>()); } catch (InvalidOperationException) { refused = true; }
                Check(refused, "partial migration refused before writes");
                Object.DestroyImmediate(partial);
                var conflict = plan[0].Filter.gameObject.AddComponent<CameraOccluder>(); conflict.Renderers = new[] { plan[0].Filter.GetComponent<Renderer>() }; refused = false;
                try { GrasslandCrownOcclusionSetup.Plan(root.GetComponent<IslandTerrain>()); } catch (InvalidOperationException) { refused = true; }
                Check(refused, "duplicate renderer ownership refused before writes"); Object.DestroyImmediate(conflict);
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(plan[0].Filter.sharedMesh)); string metadata = importer.userData;
                try
                {
                    importer.userData = "Artist.Custom"; refused = false;
                    try { GrasslandCrownOcclusionSetup.Plan(root.GetComponent<IslandTerrain>()); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "foreign source metadata refused before writes");
                }
                finally { importer.userData = metadata; }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var files = Directory.GetFiles("Assets/Art", "*", SearchOption.AllDirectories).Where(p => p.Replace('\\', '/') != path).ToArray();
            var hashes = files.Select(Hash).ToArray();
            GrasslandCrownOcclusionSetup.Rebuild();
            byte[] prepared = File.ReadAllBytes(path);
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var c = root.GetComponentsInChildren<LandscapeCrownOccluder>(true).First();
                    c.AllowFade = false; var crown = c.Crowns[0]; crown.Bounds.Expand(.4f); c.Crowns[0] = crown;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                string edited = Hash(path);
                GrasslandCrownOcclusionSetup.Rebuild(); PrototypeAddressablesSetup.EnsureSetup();
                Check(Hash(path) == edited, "repeat and normal Setup preserve manual bounds and opt-out");
            }
            finally { File.WriteAllBytes(path, prepared); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            for (int i = 0; i < files.Length; i++) Check(Hash(files[i]) == hashes[i], "all meshes/materials/collisions/GUIDs/other art retained " + files[i]);
            Debug.Log("Grassland crown occlusion bake verification PASSED: 21 crowns, 8 existing renderers; partial migration refusal, manual edit preservation, no mesh/material/AA address changes.");
        }
        public static void After() => GrasslandCrownWindVerification.After();
        public static void Inside(GameManager g)
        {
            var all = g.Terrain.Terrain.GetComponentsInChildren<LandscapeCrownOccluder>(true);
            foreach (var c in all) for (int i = 0; i < c.Crowns.Length; i++) Check(c.Opacity(i) == 1, "entering home restores every crown");
            Tick(g, 1);
            foreach (var c in all) for (int i = 0; i < c.Crowns.Length; i++) Check(c.Opacity(i) == 1, "indoor camera mask excludes outdoor crowns");
        }
        public static void Released(GameManager g)
        {
            Check(Object.FindObjectsOfType<LandscapeCrownOccluder>(true).Length == 0, "AA exit releases crown instances");
        }
        public static void Visit(GameManager g)
        {
            var all = g.Terrain.Terrain.GetComponentsInChildren<LandscapeCrownOccluder>(true);
            Check(all.Length == 8 && all.Sum(c => c.Crowns.Length) == 21, "actual Packed component bindings");
            var camera = g.Players.Player.ViewCamera; var controller = camera.GetComponent<CameraController>();
            var position = g.Players.Player.transform.position; var cameraPosition = camera.transform.position; var rotation = camera.transform.rotation;
            float size = camera.orthographicSize, far = camera.farClipPlane;
            var materials = all.Select(c => c.Surface.sharedMaterial).ToArray();
            int colliderCount = g.Terrain.Terrain.GetComponentsInChildren<Collider>(true).Length;
            LandscapeCrownOccluder selected = null; int index = -1; Vector3 safe = default;
            var original = new MaterialPropertyBlock(); var block = new MaterialPropertyBlock();
            Directory.CreateDirectory(Folder);
            try
            {
                // Use real dry ground and a complete free player capsule behind an existing crown.
                var character = g.Players.Player.GetComponent<CharacterController>(); Physics.SyncTransforms();
                foreach (var c in all.Where(c => c.Crowns.Length > 1))
                {
                    for (int i = 0; i < c.Crowns.Length && selected == null; i++)
                    {
                        var center = c.Surface.transform.TransformPoint(c.Crowns[i].Bounds.center);
                        foreach (int z in Enumerable.Range(3, 11).OrderBy(z => Mathf.Abs(z - 8)))
                        {
                            if (selected != null) break;
                            if (!g.Terrain.TryResolve(center + Vector3.forward * z, out var ground)) continue;
                            var capsuleCenter = ground + character.center;
                            float radius = character.radius + character.skinWidth;
                            float half = Mathf.Max(0, character.height * .5f - radius);
                            if (Physics.OverlapCapsule(capsuleCenter - Vector3.up * half, capsuleCenter + Vector3.up * half, radius, ~0, QueryTriggerInteraction.Ignore)
                                .Any(hit => !g.Terrain.Terrain.IsGround(hit) && !hit.transform.IsChildOf(character.transform))) continue;
                            g.Players.RestorePosition(ground); controller.SnapToTarget();
                            if (!Blocks(c, i, camera, ground + Vector3.up * .95f)
                                || !HitsCrown(c, i, camera, ground + Vector3.up * .95f)
                                || !HitsCrown(c, i, camera, ground + Vector3.up * 1.5f)) continue;
                            selected = c; index = i; safe = ground;
                        }
                    }
                    if (selected != null) break;
                }
                Check(selected != null, "real default-camera crown occlusion at a safe grounded player position");
                var selectedCameraPosition = camera.transform.position;
                try
                {
                    // Move the test ray origin outside the large tree bounds before testing a target in front of it.
                    camera.transform.position -= camera.transform.forward * 100;
                    var viewport = camera.WorldToViewportPoint(safe + Vector3.up * .95f); viewport.z = 2;
                    Check(!Blocks(selected, index, camera, camera.ViewportToWorldPoint(viewport)), "crown behind the target is excluded");
                }
                finally { camera.transform.position = selectedCameraPosition; }
                Debug.Log("Crown occlusion preview grounded player: " + safe + ", " + selected.name + " crown " + index);
                g.Cameras.RestoreOccluders(); selected.Surface.GetPropertyBlock(original);
                selected.Surface.GetPropertyBlock(block); block.SetFloat("_CrownVerificationSentinel", 17); selected.Surface.SetPropertyBlock(block);
                g.Weather.Tick(720, true); g.Ocean.Surface.Tick(0, 1, false, g.Weather.Sun);
                camera.farClipPlane = 3200; var opaque = Capture(camera, Folder + "/Opaque.png");
                Tick(g, g.Configuration.Config.CameraFadeDuration * .25f);
                Check(selected.Opacity(index) < 1 && selected.Opacity(index) > g.Configuration.Config.CameraOccludedOpacity, "fade progresses smoothly");
                Capture(camera, Folder + "/Transition.png"); Tick(g, 1);
                Check(Mathf.Approximately(selected.Opacity(index), g.Configuration.Config.CameraOccludedOpacity), "AA minimum opacity reached");
                Check(Enumerable.Range(0, selected.Crowns.Length).Any(i => selected.Opacity(i) == 1), "other tree in the same merged renderer remains opaque");
                Capture(camera, Folder + "/Faded.png");
                Check(!File.ReadAllBytes(Folder + "/Opaque.png").SequenceEqual(File.ReadAllBytes(Folder + "/Faded.png")), "actual Packed shader changes GPU coverage");
                var shadows = selected.Surface.shadowCastingMode;
                for (int i = 0; i < all.Length; i++) Check(all[i].Surface.sharedMaterial == materials[i], "no material clones or shared asset writes");
                Check(shadows != UnityEngine.Rendering.ShadowCastingMode.Off, "original moving tree shadow retained");
                camera.transform.position += Vector3.right * 2; Tick(g, 1);
                Check(selected.Opacity(index) < 1, "orthographic follow lag retains the same target ray"); controller.SnapToTarget();
                int mask = camera.cullingMask; camera.cullingMask &= ~(1 << selected.Surface.gameObject.layer); Tick(g, 1);
                Check(selected.Opacity(index) == 1, "culled crowns restore"); camera.cullingMask = mask;
                Tick(g, 1); selected.Surface.enabled = false; Tick(g, 1); Check(selected.Opacity(index) == 1, "disabled renderer restores"); selected.Surface.enabled = true;
                Tick(g, 1); selected.enabled = false; Check(selected.Opacity(index) == 1, "disable restores immediately");
                selected.Surface.GetPropertyBlock(block); Check(block.GetFloat("_CrownVerificationSentinel") == 17 && block.GetFloat("_LandscapeCrownFadeCount") == 0, "exact previous MPB restored"); selected.enabled = true;
                Tick(g, 1); selected.AllowFade = false; Tick(g, 1); Check(selected.Opacity(index) == 1, "opt-out restores"); selected.AllowFade = true;
                Tick(g, 1); g.Cameras.SetOcclusionEnabled(false); Check(all.All(c => Enumerable.Range(0, c.Crowns.Length).All(i => c.Opacity(i) == 1)), "flow suspension restores all crowns"); g.Cameras.SetOcclusionEnabled(true);
                Tick(g, 1); g.Cameras.Clear(); Check(selected.Opacity(index) == 1, "manager Clear restores before releasing references"); g.Cameras.Initialize(controller, g.Configuration.Config); g.Cameras.SetOcclusionEnabled(true);
                g.Players.RestorePosition(safe + Vector3.right * 12); controller.SnapToTarget(); Tick(g, 1); Check(selected.Opacity(index) == 1, "walking away restores original crown");
                g.Players.RestorePosition(safe); controller.SnapToTarget(); g.Cameras.RestoreOccluders(); var restored = Capture(camera, Folder + "/Restored.png");
                int differing = 0;
                for (int p = 0; p < opaque.Length; p++)
                {
                    if (opaque[p].Equals(restored[p])) continue; differing++;
                    Check(Mathf.Abs(opaque[p].r - restored[p].r) <= 1 && Mathf.Abs(opaque[p].g - restored[p].g) <= 1 && Mathf.Abs(opaque[p].b - restored[p].b) <= 1, "restored frame differs only within one color level of GPU rounding");
                }
                Check(differing <= 4, "restored frame: at most four rounding pixels after native renderer re-registration");
                Check(g.Terrain.Terrain.GetComponentsInChildren<Collider>(true).Length == colliderCount && !File.Exists(g.Saves.SavePath), "collisions and daytime no-save rule retained");
                ShaderIsolation(camera, materials[0], all.SelectMany(c => c.Crowns).Select(c => c.Phase).Distinct().OrderBy(p => p).ToArray());
                typeof(CameraOcclusionFlowVerification).GetMethod("Fixtures", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { g });
                Debug.Log("Grassland crown occlusion Packed verification PASSED: safe grounded player with actual chest/head triangle obstruction, smooth per-tree GPU coverage, other trees opaque, shared materials/shadows/collisions retained, follow lag, culling/disable/opt-out/Clear/MPB restoration, restored frame rounding pixels=" + differing + ", exact-phase GPU fixture and original occluder regressions.");
            }
            finally
            {
                g.Cameras.RestoreOccluders();
                if (selected != null) { selected.Surface.enabled = true; selected.AllowFade = true; selected.enabled = true; selected.Surface.SetPropertyBlock(original.isEmpty ? null : original); }
                g.Players.RestorePosition(position); camera.transform.SetPositionAndRotation(cameraPosition, rotation); camera.orthographicSize = size; camera.farClipPlane = far;
            }
        }
        static bool HitsCrown(LandscapeCrownOccluder c, int index, Camera camera, Vector3 point)
        {
            var mesh = c.GetComponent<MeshFilter>().sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles;
            var uv = new System.Collections.Generic.List<Vector4>(); mesh.GetUVs(0, uv);
            var ray = camera.ViewportPointToRay(camera.WorldToViewportPoint(point));
            var origin = c.Surface.transform.InverseTransformPoint(ray.origin); var delta = c.Surface.transform.InverseTransformPoint(point) - origin;
            float distance = delta.magnitude; var direction = delta.normalized;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t]; if (uv[a].x > -2 || uv[a].x < -3 || uv[a].y != c.Crowns[index].Phase) continue;
                var edge1 = vertices[triangles[t + 1]] - vertices[a]; var edge2 = vertices[triangles[t + 2]] - vertices[a];
                var cross = Vector3.Cross(direction, edge2); float determinant = Vector3.Dot(edge1, cross); if (determinant < .000001f) continue;
                float inverse = 1 / determinant; var offset = origin - vertices[a]; float u = Vector3.Dot(offset, cross) * inverse;
                if (u < 0 || u > 1) continue; var q = Vector3.Cross(offset, edge1); float v = Vector3.Dot(direction, q) * inverse;
                if (v < 0 || u + v > 1) continue; float hit = Vector3.Dot(edge2, q) * inverse;
                if (hit > 0 && hit < distance - .05f) return true;
            }
            return false;
        }
        static void ShaderIsolation(Camera camera, Material material, float[] phases)
        {
            int closest = Enumerable.Range(0, phases.Length - 1).OrderBy(i => phases[i + 1] - phases[i]).First();
            var fixture = new GameObject("Merged crown shader fixture"); var mesh = new Mesh();
            var position = camera.transform.position; var rotation = camera.transform.rotation;
            int mask = camera.cullingMask; float strength = Shader.GetGlobalFloat("_IslandWindStrength");
            var clear = camera.clearFlags; var background = camera.backgroundColor;
            try
            {
                fixture.layer = 30; fixture.transform.SetPositionAndRotation(position + camera.transform.forward * 10, rotation);
                var vertices = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector4>();
                var triangles = new System.Collections.Generic.List<int>(); var normals = new System.Collections.Generic.List<Vector3>(); var colors = new System.Collections.Generic.List<Color>();
                for (int i = 0; i < 3; i++)
                {
                    float x = (i - 1) * 4;
                    vertices.AddRange(new[] { new Vector3(x - 1, -1, 0), new Vector3(x - 1, 1, 0), new Vector3(x + 1, 1, 0), new Vector3(x + 1, -1, 0) });
                    triangles.AddRange(new[] { i * 4, i * 4 + 1, i * 4 + 2, i * 4, i * 4 + 2, i * 4 + 3 });
                    for (int j = 0; j < 4; j++) { normals.Add(Vector3.back); colors.Add(new Color(.3f, .6f, .3f)); uv.Add(i == 1 ? Vector4.zero : new Vector4(-2.5f, phases[closest + (i == 2 ? 1 : 0)], 0, .1f)); }
                }
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.RecalculateBounds();
                fixture.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = fixture.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; Shader.SetGlobalFloat("_IslandWindStrength", 0);
                var before = Capture(camera, Folder + "/ShaderOpaque.png");
                var block = new MaterialPropertyBlock(); var data = new Vector4[32]; data[0] = new Vector4(phases[closest], 0, 0, 0); data[1] = new Vector4(phases[closest + 1], 1, 0, 0);
                block.SetVectorArray("_LandscapeCrownFades", data); block.SetFloat("_LandscapeCrownFadeCount", 2); renderer.SetPropertyBlock(block);
                var after = Capture(camera, Folder + "/ShaderSelected.png");
                for (int i = 0; i < 3; i++)
                {
                    // The offscreen target has its own aspect, independent of the editor Game view.
                    int x = Mathf.RoundToInt(640 + (i - 1) * 4 / (2 * camera.orthographicSize * (1280f / 800)) * 1280), y = 400, changed = 0;
                    for (int dy = -20; dy <= 20; dy++) for (int dx = -20; dx <= 20; dx++)
                    { int pixel = (y + dy) * 1280 + x + dx; Check(before[pixel].g > 5, "visible GPU fixture"); if (!before[pixel].Equals(after[pixel])) changed++; }
                    Check(i == 0 ? changed > 1500 : changed == 0, "only selected exact phase clips; nearest phase and unmarked trunk pixels retained");
                }
            }
            finally { Object.DestroyImmediate(fixture); Object.DestroyImmediate(mesh); camera.cullingMask = mask; camera.clearFlags = clear; camera.backgroundColor = background; Shader.SetGlobalFloat("_IslandWindStrength", strength); }
        }
        static Color32[] Capture(Camera camera, string path)
        {
            var rt = new RenderTexture(1280, 800, 24); var old = camera.targetTexture; var active = RenderTexture.active; var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try { camera.targetTexture = rt; camera.Render(); camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); return image.GetPixels32(); }
            finally { camera.targetTexture = old; RenderTexture.active = active; Object.DestroyImmediate(rt); Object.DestroyImmediate(image); }
        }
    }
}
