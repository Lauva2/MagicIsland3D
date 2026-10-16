using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class OceanPlayerVerificationBuild
    {
        // Explicit batch entry. Run only in an isolated validation copy, not the player's project.
        public static void Build()
        {
            if(!Application.persistentDataPath.Contains("MagicIslandValidation"))throw new Exception("Create an isolated validation project before building this probe");
            PrototypeAddressablesSetup.BuildContent();
            string output=Path.GetFullPath("Logs/OceanPlayer/MagicIslandOcean.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/MainMenu.unity"},locationPathName=output,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development,extraScriptingDefines=new[]{"MAGIC_ISLAND_OCEAN_VERIFICATION"}});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Ocean player build failed: "+report.summary.result);
            Debug.Log("Ocean verification player build PASSED: "+output);
        }
    }
}
