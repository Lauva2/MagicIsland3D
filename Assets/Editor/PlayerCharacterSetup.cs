using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class PlayerCharacterSetup
    {
        public const string PrefabPath = "Assets/Art/Prefabs/Characters/PlayerCharacter.prefab";
        public const string AnimationFolder = "Assets/Art/Animations/Player";
        public const string ControllerPath = AnimationFolder + "/PlayerCharacter.controller";
        public static readonly string[] States = { "Idle", "Walk", "Hoe", "Plant", "Water", "Axe", "Pickaxe", "Harvest", "Fertilize" };

        public static void Ensure(AddressableAssetSettings settings, GameConfig config)
        {
            Directory.CreateDirectory("Assets/Art/Prefabs/Characters"); Directory.CreateDirectory("Assets/Art/Materials/Player");
            Directory.CreateDirectory(AnimationFolder); AssetDatabase.Refresh();
            foreach (string state in States) if (!File.Exists(AnimationFolder + "/" + state + ".anim")) CreateClip(state);
            if (!File.Exists(ControllerPath)) CreateController();
            if (!File.Exists(PrefabPath)) CreatePrefab();
            var group = settings.FindGroup("Characters") ?? settings.CreateGroup("Characters", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != "PlayerCharacter") entry.SetAddress("PlayerCharacter");
            const string configPath = "Assets/Config/GameConfig.asset";
            if (!File.ReadAllText(configPath).Contains("PlayerPrefabAddress:")) EditorUtility.SetDirty(config);
        }
        private static Material Mat(string name, Color color)
        {
            string path = "Assets/Art/Materials/Player/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", .08f); AssetDatabase.CreateAsset(material, path); return material;
        }
        private static Transform Joint(Transform parent, string name, Vector3 position)
        { var joint = new GameObject(name).transform; joint.SetParent(parent, false); joint.localPosition = position; return joint; }
        private static void Part(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(primitive); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static void CreatePrefab()
        {
            var root = new GameObject("PlayerCharacter", typeof(CharacterController), typeof(PlayerMovement));
            try
            {
                var character = root.GetComponent<CharacterController>(); character.height = 1.6f; character.radius = .28f;
                character.center = Vector3.up * .8f; character.stepOffset = .2f;
                var visual = Joint(root.transform, "Visual", Vector3.zero);
                var model = Joint(visual, "Model", Vector3.zero); var animator = model.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var animation = root.AddComponent<PlayerAnimation>(); animation.Animator = animator;
                var bob = Joint(model, "Bob", Vector3.zero);
                var shirt = Mat("Island Shirt", new Color(.30f, .58f, .8f)); var skin = Mat("Island Skin", new Color(.96f, .78f, .57f));
                var hat = Mat("Island Hat", new Color(.95f, .76f, .35f)); var boots = Mat("Island Boots", new Color(.36f, .25f, .17f));
                var trousers = Mat("Island Trousers", new Color(.35f, .43f, .44f)); var eyes = Mat("Island Eyes", new Color(.12f, .17f, .14f));
                Part(bob, "Body", PrimitiveType.Capsule, new Vector3(0, .78f, 0), new Vector3(.5f, .38f, .4f), shirt);
                var head = Joint(bob, "Head", new Vector3(0, 1.35f, 0));
                Part(head, "Head", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .48f, skin);
                Part(head, "Hat Brim", PrimitiveType.Cylinder, new Vector3(0, .18f, 0), new Vector3(.67f, .025f, .67f), hat);
                Part(head, "Hat Crown", PrimitiveType.Cylinder, new Vector3(0, .26f, 0), new Vector3(.45f, .07f, .45f), hat);
                Part(head, "Left Eye", PrimitiveType.Sphere, new Vector3(-.09f, .035f, .222f), Vector3.one * .035f, eyes);
                Part(head, "Right Eye", PrimitiveType.Sphere, new Vector3(.09f, .035f, .222f), Vector3.one * .035f, eyes);
                Part(head, "Nose", PrimitiveType.Sphere, new Vector3(0, -.025f, .242f), Vector3.one * .065f, skin);
                foreach (int side in new[] { -1, 1 })
                {
                    string prefix = side < 0 ? "Left" : "Right";
                    var arm = Joint(bob, prefix + "Arm", new Vector3(side * .29f, 1.03f, 0)); arm.localRotation = Quaternion.Euler(0, 0, -side * 10);
                    Part(arm, "Sleeve", PrimitiveType.Capsule, new Vector3(0, -.18f, 0), new Vector3(.16f, .20f, .16f), shirt);
                    Part(arm, "Hand", PrimitiveType.Sphere, new Vector3(0, -.40f, 0), Vector3.one * .15f, skin);
                    if (side > 0) animation.ToolSocket = Joint(arm, "ToolSocket", new Vector3(0, -.40f, .10f));
                    var leg = Joint(bob, prefix + "Leg", new Vector3(side * .15f, .53f, 0));
                    Part(leg, "Trouser", PrimitiveType.Capsule, new Vector3(0, -.20f, 0), new Vector3(.17f, .19f, .17f), trousers);
                    Part(leg, "Boot", PrimitiveType.Cube, new Vector3(0, -.39f, .055f), new Vector3(.19f, .13f, .30f), boots);
                }
                var movement = root.GetComponent<PlayerMovement>(); movement.Visual = visual; movement.Animation = animation; movement.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void Curve(AnimationClip clip, string path, string property, float duration, params float[] values)
        {
            var keys = new Keyframe[values.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(duration * i / (keys.Length - 1), values[i]);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            { AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto); }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }
        private static void Rotation(AnimationClip clip, string path, float duration, float z, params float[] x)
        {
            Curve(clip, path, "localEulerAnglesRaw.x", duration, x);
            Curve(clip, path, "localEulerAnglesRaw.y", duration, 0, 0);
            Curve(clip, path, "localEulerAnglesRaw.z", duration, z, z);
        }
        private static void CreateClip(string state)
        {
            bool walking = state == "Walk", idle = state == "Idle", swing = state == "Hoe" || state == "Axe" || state == "Pickaxe";
            float duration = idle ? 2.4f : walking ? .8f : 1f;
            var clip = new AnimationClip { name = state, frameRate = 30 };
            if (idle) Curve(clip, "Bob", "m_LocalPosition.y", duration, 0, .008f, 0);
            else if (walking) Curve(clip, "Bob", "m_LocalPosition.y", duration, 0, .025f, 0, .025f, 0);
            else Curve(clip, "Bob", "m_LocalPosition.y", duration, 0, -.035f, -.07f, -.025f, 0);
            Curve(clip, "Bob", "m_LocalPosition.x", duration, 0, 0); Curve(clip, "Bob", "m_LocalPosition.z", duration, 0, 0);
            Rotation(clip, "Bob", duration, 0, idle || walking ? new[] { 0f, 0f } : new[] { 0f, swing ? -8f : 8f, swing ? 18f : 22f, 6f, 0f });
            Rotation(clip, "Bob/Head", duration, 0, idle ? new[] { 0f, 2f, 0f } : new[] { 0f, 0f });
            Rotation(clip, "Bob/LeftArm", duration, 10, walking ? new[] { 0f, 25f, 0f, -25f, 0f } : idle ? new[] { 0f, 2f, 0f } : new[] { 0f, -20f, -40f, -15f, 0f });
            Rotation(clip, "Bob/RightArm", duration, -10, walking ? new[] { 0f, -25f, 0f, 25f, 0f } : idle ? new[] { 0f, -2f, 0f }
                : state == "Water" ? new[] { 0f, -38f, -45f, -35f, 0f }
                : state == "Harvest" ? new[] { 0f, -25f, -65f, -20f, 0f }
                : swing ? new[] { 0f, -100f, 32f, -20f, 0f } : new[] { 0f, -32f, -58f, -18f, 0f });
            Rotation(clip, "Bob/LeftLeg", duration, 0, walking ? new[] { 0f, -28f, 0f, 28f, 0f } : new[] { 0f, 0f });
            Rotation(clip, "Bob/RightLeg", duration, 0, walking ? new[] { 0f, 28f, 0f, -28f, 0f } : new[] { 0f, 0f });
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = idle || walking; settings.loopBlend = idle || walking;
            AnimationUtility.SetAnimationClipSettings(clip, settings); AssetDatabase.CreateAsset(clip, AnimationFolder + "/" + state + ".anim");
        }
        private static void CreateController()
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float); controller.AddParameter("StrideRate", AnimatorControllerParameterType.Float);
            controller.parameters = new[] { new AnimatorControllerParameter { name = "MoveSpeed", type = AnimatorControllerParameterType.Float }, new AnimatorControllerParameter { name = "StrideRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1 } };
            var machine = controller.layers[0].stateMachine; AnimatorState idle = null, walk = null;
            foreach (string name in States)
            {
                var state = machine.AddState(name); state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "/" + name + ".anim"); state.writeDefaultValues = false;
                if (name == "Idle") idle = state;
                else if (name == "Walk") { walk = state; state.speedParameter = "StrideRate"; state.speedParameterActive = true; }
            }
            machine.defaultState = idle;
            var start = idle.AddTransition(walk); start.hasExitTime = false; start.hasFixedDuration = true; start.duration = .14f; start.AddCondition(AnimatorConditionMode.Greater, .05f, "MoveSpeed");
            var stop = walk.AddTransition(idle); stop.hasExitTime = false; stop.hasFixedDuration = true; stop.duration = .14f; stop.AddCondition(AnimatorConditionMode.Less, .05f, "MoveSpeed");
            EditorUtility.SetDirty(controller);
        }
    }
}
