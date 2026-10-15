using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Sandbox.CharacterSystem.Editor
{
    [InitializeOnLoad]
    internal static class ExpressionPreviewEditorBridge
    {
        static double previousTime;
        static ExpressionPreviewEditorBridge()
        {
            previousTime = EditorApplication.timeSinceStartup;
            BlendShapeDebugger.EditorClock = () => EditorApplication.timeSinceStartup;
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
            float delta = (float)System.Math.Max(0, now - previousTime); previousTime = now;
            bool changed = false;
            foreach (var module in new List<ExpressionModule>(ExpressionModule.ActivePreviews))
            {
                if (!module) { ExpressionModule.ActivePreviews.Remove(module); continue; }
                if (UnityEngine.Application.IsPlaying(module.gameObject)) continue;
                module.TickPreview(delta); changed = true;
            }
            foreach (var debugger in new List<BlendShapeDebugger>(BlendShapeDebugger.ActivePreviews))
            {
                if (!debugger || UnityEngine.Application.IsPlaying(debugger.gameObject)) continue;
                debugger.TickPreview(now); changed = true;
            }
            if (changed) SceneView.RepaintAll();
        }
        internal static void StopAll()
        {
            foreach (var module in new List<ExpressionModule>(ExpressionModule.ActivePreviews))
                if (module) module.StopPreview();
            ExpressionModule.ActivePreviews.Clear();
            foreach (var debugger in new List<BlendShapeDebugger>(BlendShapeDebugger.ActivePreviews))
                if (debugger && debugger.IsHolding) debugger.StopRestore();
        }
    }
    internal sealed class ExpressionPreviewSaveGuard : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            ExpressionPreviewEditorBridge.StopAll();
            return paths;
        }
    }
}
