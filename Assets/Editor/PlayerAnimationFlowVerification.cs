using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.CameraSystem;
using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Managers;
using MagicIsland.Player;
using MagicIsland.Tools;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class PlayerAnimationFlowVerification
    {
        const string Key = "MagicIsland.PlayerAnimationVerification";
        static PlayerManager loading;
        static GameConfig bad;
        static ToolManager toolLoading;
        static PlayerAnimationFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            var paths = PlayerCharacterSetup.States.Select(s => PlayerCharacterSetup.AnimationFolder + "/" + s + ".anim")
                .Concat(new[] { PlayerCharacterSetup.PrefabPath, PlayerCharacterSetup.ControllerPath, "Assets/Config/GameConfig.asset" }).ToArray();
            var bytes = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Setup preserves artist player/clip/controller edits");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCharacterSetup.PrefabPath);
            Require(prefab.GetComponent<PlayerMovement>() != null && prefab.GetComponentsInChildren<Collider>(true).Length == 1, "AA actor has one physical controller and no visual colliders");
            foreach (string state in PlayerCharacterSetup.States)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerCharacterSetup.AnimationFolder + "/" + state + ".anim");
                Require(clip != null && clip.length > 0 && AnimationUtility.GetAnimationEvents(clip).Length == 0, "Visual-only animation " + state);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool condition, string why) { if (!condition) throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key + ".Phase", p); Debug.Log("Player animation phase " + p); }
        static T View<T>() where T : Component => Object.FindObjectOfType<T>();
        static PlayerAnimation Rig(GameManager g) => g.Players.Player.Animation;
        static void Move(GameManager g, Vector2 input, float dt) => typeof(PlayerMovement).GetMethod("TickMovement", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(g.Players.Player, new object[] { input, dt });
        static void Plot(GameManager g, int index) => g.Players.RestorePosition(g.Farm.Grid.CellCenter(index) + new Vector3(0, .1f, -1));
        static void Action(GameManager g, ToolActionKind kind, Func<bool> act)
        {
            int sounds = g.Audio.PlayedSoundCount; int stamina = g.Stamina.State.Current;
            Require(act() && g.Tools.Busy && g.Players.Player.ToolActionLocked && Rig(g).Action == kind, "Commit starts matching visual state " + kind);
            Require(g.Audio.PlayedSoundCount == sounds + 1 && g.Stamina.State.Current <= stamina, "Action has one synchronized success sound");
            Rig(g).SampleAction(.45f);
            g.Tools.Presentation.Animate(kind, .45f);
            Require(Mathf.Approximately(Rig(g).ActionProgress, .45f) && !Rig(g).Animator.applyRootMotion, "Manual pose follows gate progress without root motion");
            CheckTools(g, kind);
            Vector3 position = g.Players.Player.transform.position; Move(g, Vector2.up, .1f);
            Require(Vector3.ProjectOnPlane(g.Players.Player.transform.position - position, Vector3.up).sqrMagnitude < .00001f, "Action lock blocks translation");
            Capture(g, "Player" + kind + "Preview");
            if (kind == ToolActionKind.Hoe || kind == ToolActionKind.Water || kind == ToolActionKind.Axe || kind == ToolActionKind.Pickaxe)
            {
                try
                {
                    foreach (float progress in new[] { 0f, .25f, .5f, .75f, 1f })
                    {
                        Rig(g).SampleAction(progress); g.Tools.Presentation.Animate(kind, progress);
                        Capture(g, "Player" + kind + "Pose" + Mathf.RoundToInt(progress * 100).ToString("D3"));
                    }
                }
                finally { Rig(g).SampleAction(.45f); g.Tools.Presentation.Animate(kind, .45f); }
            }
        }
        static void CheckTools(GameManager g, ToolActionKind kind)
        {
            var models = g.Tools.Presentation.Grip.GetComponentInChildren<MagicIsland.Tools.FarmToolModels>(true);
            var source = g.Tools.Presentation.EffectRoot.GetComponent<MagicIsland.Tools.FarmActionEffects>().HeldToolsPrefab;
            Require(g.Tools.OwnsEffects && models != null && models.IsValid && source != null, "Packed AA owns baked farm tools with the effects dependency");
            var roots = new[] { models.Hoe, models.WateringCan, models.Axe, models.Pickaxe };
            var originals = new[] { source.Hoe, source.WateringCan, source.Axe, source.Pickaxe };
            var kinds = new[] { ToolActionKind.Hoe, ToolActionKind.Water, ToolActionKind.Axe, ToolActionKind.Pickaxe };
            for (int i = 0; i < roots.Length; i++)
            {
                Require(roots[i].gameObject.activeSelf == (kind == kinds[i]), "Only the selected baked tool is visible");
                Require(roots[i].GetComponent<MeshFilter>().sharedMesh == originals[i].GetComponent<MeshFilter>().sharedMesh
                    && roots[i].GetComponent<MeshRenderer>().sharedMaterial == originals[i].GetComponent<MeshRenderer>().sharedMaterial,
                    "Tool instances share AA geometry/materials without cloning");
            }
        }
        static void Capture(GameManager g, string name)
        {
            g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget();
            GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true);
            var camera = g.Players.Player.ViewCamera;
            float size = camera.orthographicSize;
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            var canvases = Object.FindObjectsOfType<Canvas>(); var enabled = canvases.Select(c => c.enabled).ToArray();
            var environment = Object.FindObjectsOfType<Renderer>().Where(r => !r.transform.IsChildOf(g.Players.Player.transform)).ToArray();
            var visible = environment.Select(r => r.enabled).ToArray();
            var clearFlags = camera.clearFlags; Color background = camera.backgroundColor;
            try
            {
                foreach (var canvas in canvases) canvas.enabled = false;
                // Isolate the actual AA actor and held tool for close inspection, retaining game lighting.
                foreach (var renderer in environment) renderer.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.72f, .79f, .73f);
                camera.orthographicSize = 1.35f;
                camera.transform.rotation = Rig(g).Animator.transform.rotation * Quaternion.Euler(28, 155, 0);
                camera.transform.position = g.Players.Player.transform.position + Vector3.up * .85f - camera.transform.forward * 7;
                GameUiPreview.CaptureInventory(camera);
                File.Copy("Logs/InventoryPreview.png", "Logs/" + name + "Detail.png", true);
                if (name.Contains("Pose"))
                {
                    camera.transform.rotation = Rig(g).Animator.transform.rotation * Quaternion.Euler(12, -90, 0);
                    camera.transform.position = g.Players.Player.transform.position + Vector3.up * .85f - camera.transform.forward * 7;
                    GameUiPreview.CaptureInventory(camera);
                    File.Copy("Logs/InventoryPreview.png", "Logs/" + name + "Side.png", true);
                }
            }
            finally
            {
                camera.orthographicSize = size; camera.transform.SetPositionAndRotation(position, rotation);
                camera.clearFlags = clearFlags; camera.backgroundColor = background;
                for (int i = 0; i < canvases.Length; i++) if (canvases[i] != null) canvases[i].enabled = enabled[i];
                for (int i = 0; i < environment.Length; i++) if (environment[i] != null) environment[i].enabled = visible[i];
            }
        }
        static void Movement(GameManager g)
        {
            var player = g.Players.Player; var rig = Rig(g);
            g.Players.RestorePosition(new Vector3(0, .1f, -8));
            Move(g, Vector2.up, .04f); float straight = player.ActualPlanarSpeed;
            g.Players.RestorePosition(new Vector3(0, .1f, -8)); Move(g, Vector2.one, .04f);
            Require(Mathf.Abs(player.ActualPlanarSpeed - straight) < .1f && straight > 3.5f, "Diagonal WASD has the same speed as straight movement");
            for (int i = 0; i < 12; i++) { Move(g, Vector2.up, .04f); rig.Animator.Update(.04f); }
            Require(rig.Animator.GetCurrentAnimatorStateInfo(0).IsName("Walk") && rig.MovementRatio > .9f, "Actual displacement selects Walk");
            Require(Mathf.Abs(rig.Animator.transform.Find("Bob/LeftLeg").localEulerAngles.x) > .1f, "Walk clip moves leg joints"); Capture(g, "PlayerWalkPreview");
            player.Speed = 2; Move(g, Vector2.up, .04f); Require(Mathf.Abs(rig.Animator.GetFloat("StrideRate") - .5f) < .05f, "Walk cadence scales with physical speed"); player.Speed = 4;
            for (int i = 0; i < 16; i++) { Move(g, Vector2.zero, .04f); rig.Animator.Update(.04f); }
            Require(rig.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && player.ActualPlanarSpeed < .01f, "Stopping smoothly transitions back to Idle");
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                blocker.transform.position = new Vector3(0, .8f, -6.9f); blocker.transform.localScale = new Vector3(2, 1.6f, .2f); Physics.SyncTransforms();
                g.Players.RestorePosition(new Vector3(0, .1f, -8));
                for (int i = 0; i < 35; i++) { Move(g, Vector2.up, .04f); rig.Animator.Update(.04f); }
                Require(player.ActualPlanarSpeed < .1f && rig.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Pressing into a wall does not walk in place");
            }
            finally { Object.DestroyImmediate(blocker); }
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 20 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Player animation timeout " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Player startup failure");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                {
                    string directory = Path.GetFullPath("Logs/PlayerAnimationSaves/" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(directory);
                    g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, directory, g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting, g.Configuration.Animals);
                    g.Settings.Initialize(Path.Combine(directory, "Settings"));
                    g.EnterGame(); Phase(1);
                }
                else if (p == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Players.OwnsPlayer && Rig(g).IsValid && g.Players.Player.GetComponentsInChildren<Renderer>().Length >= 10, "AA PlayerManager owns articulated prefab");
                    Require(g.Tools.Presentation.Grip.parent == Rig(g).ToolSocket && g.Players.Player.ViewCamera.GetComponent<CameraController>().Target == g.Players.Player.transform, "Rig hand socket and camera bound to AA player");
                    CheckTools(g, ToolActionKind.Hoe);
                    // A noon fixture makes palette and tool overlap visible in acceptance captures.
                    g.Time.Clock.Restore(720); g.Weather.Tick(720, true);
                    Movement(g); Plot(g, 0); g.Tools.SelectTool(FarmTool.Hoe);
                    Action(g, ToolActionKind.Hoe, () => g.Tools.TryAct(0, out _));
                    int stamina = g.Stamina.State.Current, sounds = g.Audio.PlayedSoundCount;
                    Require(!g.Tools.TryAct(0, out _) && g.Stamina.State.Current == stamina && g.Audio.PlayedSoundCount == sounds, "Repeat cannot settle or sound twice");
                    g.OpenPause(); Phase(2);
                }
                else if (p == 2 && View<PauseView>() != null)
                {
                    Require(Rig(g).Paused && Rig(g).Action == ToolActionKind.Hoe && Rig(g).Animator.speed == 0, "Pause preserves in-progress action pose");
                    SessionState.SetFloat(Key + ".Progress", g.Tools.ActionProgress); Phase(3);
                }
                else if (p == 3 && View<PauseView>() != null)
                {
                    Require(Mathf.Approximately(g.Tools.ActionProgress, SessionState.GetFloat(Key + ".Progress", -1)), "Paused gate does not advance"); g.ResumeFromPause(); Phase(4);
                }
                else if (p == 4 && !g.Tools.Busy)
                {
                    Require(!Rig(g).Action.HasValue && !g.Players.Player.ToolActionLocked, "End of gate releases animation and movement");
                    g.Tools.SelectTool(FarmTool.Seeds); Action(g, ToolActionKind.Seed, () => g.Tools.TryAct(0, out _)); Phase(5);
                }
                else if (p == 5 && !g.Tools.Busy) { g.Tools.SelectTool(FarmTool.WateringCan); Action(g, ToolActionKind.Water, () => g.Tools.TryAct(0, out _)); Phase(6); }
                else if (p == 6 && !g.Tools.Busy)
                {
                    g.Farm.State.Cells[0].Growth = 3; g.Farm.Grid.Refresh(); Action(g, ToolActionKind.Harvest, () => g.Tools.TryAct(0, out _)); Phase(7);
                }
                else if (p == 7 && !g.Tools.Busy)
                {
                    g.Inventory.TryAdd("basic_fertilizer", 1); g.Tools.SelectTool(FarmTool.Fertilizer); Action(g, ToolActionKind.Fertilizer, () => g.Tools.TryAct(0, out _)); Phase(8);
                }
                else if (p == 8 && !g.Tools.Busy)
                {
                    g.Players.RestorePosition(g.Gathering.Nodes[0].transform.position + new Vector3(0, .1f, -1.6f)); g.Tools.SelectTool(FarmTool.Axe);
                    Action(g, ToolActionKind.Axe, () => g.Tools.TryGather(0, out _)); Phase(9);
                }
                else if (p == 9 && !g.Tools.Busy)
                {
                    int index = g.Configuration.Gathering.Nodes.FindIndex(n => n.TypeId == "rock"); g.Players.RestorePosition(g.Gathering.Nodes[index].transform.position + new Vector3(0, .1f, -1.6f)); g.Tools.SelectTool(FarmTool.Pickaxe);
                    Action(g, ToolActionKind.Pickaxe, () => g.Tools.TryGather(index, out _)); Phase(10);
                }
                else if (p == 10 && !g.Tools.Busy)
                {
                    g.Players.RestorePosition(g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); g.EnterHome(); Phase(11);
                }
                else if (p == 11 && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    Require(!Rig(g).Paused && !Rig(g).Action.HasValue && g.Players.Player.gameObject.layer == g.Configuration.Housing.InteriorLayer && g.Players.OwnsPlayer, "Scene transition preserves AA actor and resets pose");
                    Capture(g, "PlayerIdleHomePreview"); g.ReturnToMainMenu(); Phase(12);
                }
                else if (p == 12 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(13); }
                else if (p == 13 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Players.Player == null && !g.Players.OwnsPlayer && Object.FindObjectsOfType<PlayerMovement>().Length == 0, "AA actor and handle released on menu return");
                    Require(!g.Tools.OwnsEffects && g.Tools.Presentation == null && Object.FindObjectsOfType<MagicIsland.Tools.FarmToolModels>().Length == 0,
                        "Menu releases held tool instances and their AA dependency");
                    toolLoading = new GameObject("Tool load fixture").AddComponent<ToolManager>();
                    toolLoading.StartCoroutine(toolLoading.LoadEffects("Tool_Verification_Missing"));
                    loading = new GameObject("Player load fixture").AddComponent<PlayerManager>(); bad = Object.Instantiate(g.Configuration.Config); bad.PlayerPrefabAddress = "Player_Verification_Missing";
                    loading.StartCoroutine(loading.LoadPlayer(bad, loading.transform)); Phase(14);
                }
                else if (p == 14 && loading.LastError != null && toolLoading.LastError != null)
                {
                    Require(!loading.OwnsPlayer && loading.Player == null, "Failed player AA load releases handle");
                    Require(!toolLoading.OwnsEffects && toolLoading.Presentation == null, "Failed tool dependency load releases handle");
                    toolLoading.StartCoroutine(toolLoading.LoadEffects(g.Configuration.Config.ToolEffectsPrefabAddress)); toolLoading.Clear();
                    loading.StartCoroutine(loading.LoadPlayer(g.Configuration.Config, loading.transform)); loading.Clear(); Phase(15);
                }
                else if (p == 15)
                {
                    Require(loading.Player == null && !loading.OwnsPlayer, "Cancelled load cannot bind late player"); Phase(16);
                    Require(!toolLoading.OwnsEffects && toolLoading.Presentation == null, "Cancelled tool dependency cannot bind late models");
                }
                else if (p == 16)
                {
                    Require(loading.GetComponentsInChildren<PlayerMovement>().Length == 0, "Cancelled AA instance is released after completion"); Object.Destroy(loading.gameObject); Object.Destroy(bad); g.EnterGame(); Phase(17);
                    Require(Object.FindObjectsOfType<MagicIsland.Tools.FarmToolModels>().Length == 0, "No late tool instance survived cancellation"); Object.Destroy(toolLoading.gameObject);
                }
                else if (p == 17 && g.State == GameFlowState.Playing)
                {
                    Require(g.Players.OwnsPlayer && Rig(g).IsValid && !Rig(g).Action.HasValue && Object.FindObjectsOfType<PlayerMovement>().Length == 1, "Re-entry creates one fresh actor");
                    CheckTools(g, ToolActionKind.Hoe);
                    g.ReturnToMainMenu(); Phase(18);
                }
                else if (p == 18 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(19); }
                else if (p == 19 && g.State == GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/PlayerAnimationVerification.txt", "PASS: AA player/rig/controller; editor preservation; real movement, diagonal/collision/cadence/idle; seven committed action states and sound; pause/action locks; home/menu; failed/cancelled AA loads; actor/handle cleanup and re-entry.\n");
                    Debug.Log("Player animation flow verification PASSED."); Phase(20); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.isPlaying = false; EditorApplication.Exit(1); }
        }
    }
}
