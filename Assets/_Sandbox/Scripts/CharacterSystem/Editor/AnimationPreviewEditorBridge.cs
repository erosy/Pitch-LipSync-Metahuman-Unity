using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sandbox.CharacterSystem.Editor
{
    [InitializeOnLoad]
    internal static class AnimationPreviewEditorBridge
    {
        static double previousTime;

        static AnimationPreviewEditorBridge()
        {
            previousTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += StopAll;
            EditorApplication.quitting += StopAll;
            EditorApplication.playModeStateChanged += _ => StopAll();
            EditorSceneManager.sceneClosing += (scene, removing) => StopAll();
            EditorSceneManager.sceneSaving += (scene, path) => StopAll();
            PrefabStage.prefabStageClosing += _ => StopAll();
            PrefabStage.prefabSaving += _ => StopAll();
        }

        static void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)System.Math.Max(0, now - previousTime);
            previousTime = now;
            if (AnimationModule.ActivePreviews.Count == 0) return;
            bool changed = false;
            foreach (var module in new List<AnimationModule>(AnimationModule.ActivePreviews))
            {
                if (!module) { AnimationModule.ActivePreviews.Remove(module); continue; }
                if (Application.IsPlaying(module.gameObject)) continue;
                module.TickPreview(delta);
                changed = true;
            }
            if (changed) SceneView.RepaintAll();
        }

        internal static void StopAll()
        {
            foreach (var module in new List<AnimationModule>(AnimationModule.ActivePreviews))
                if (module) module.StopPreview();
            AnimationModule.ActivePreviews.Clear();
        }
    }

    internal sealed class AnimationPreviewSaveGuard : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            AnimationPreviewEditorBridge.StopAll();
            return paths;
        }
    }
}
