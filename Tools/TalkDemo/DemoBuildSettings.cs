using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class TalkDemoBuildSettings
{
    public static object Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit Mode only; user controls Play Mode.");
        if (UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile())
            throw new Exception("An overriding custom build profile is active; inspect its settings before applying.");
        var folder = "Temp/restore/TalkDemo/DemoBuildSettings-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(folder);
        File.Copy("ProjectSettings/ProjectSettings.asset", folder + "/ProjectSettings.asset");
        PlayerSettings.productName = "Talk Demo";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.template = "PROJECT:TalkDemo";
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.BuildTimes;
        PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
        AssetDatabase.SaveAssets();
        return new { product = PlayerSettings.productName, compression = PlayerSettings.WebGL.compressionFormat.ToString(),
            fallback = PlayerSettings.WebGL.decompressionFallback, template = PlayerSettings.WebGL.template,
            optimization = UnityEditor.WebGL.UserBuildSettings.codeOptimization.ToString(),
            il2cpp = PlayerSettings.GetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL).ToString(),
            backup = folder, buildPerformed = false };
    }
}
