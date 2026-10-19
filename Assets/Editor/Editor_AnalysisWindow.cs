using MagicIsland.Interaction;
using MagicIsland.Managers;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // Uses Unity's built-in EditorWindow; the reference project's Odin dependency is unnecessary.
    public sealed class Editor_AnalysisWindow : EditorWindow
    {
        [MenuItem("Magic Island/测试工具窗口")]
        static void Open()=>GetWindow<Editor_AnalysisWindow>("测试工具窗口");
        void OnGUI()
        {
            var game=GameManager.Instance;
            using(new EditorGUI.DisabledScope(!Application.isPlaying||game==null||!game.CanTestBiomeTravel))
                foreach(var biome in BiomeIslandLayout.Biomes)
                    if(GUILayout.Button("传送到"+BiomeIslandLayout.Name(biome),GUILayout.Height(36)))game.TryTestBiomeTravel(biome);
        }
        void OnInspectorUpdate()=>Repaint();
    }
}
