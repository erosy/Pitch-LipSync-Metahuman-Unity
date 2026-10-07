using Sirenix.OdinInspector.Editor;
using UnityEditor;

[CustomEditor(typeof(BlendShapeDebugger))]
public class BlendShapeDebuggerEditor : OdinEditor { }

[InitializeOnLoad]
public static class BlendShapeDebuggerEditorBridge
{
    static BlendShapeDebuggerEditorBridge()
    {
        BlendShapeDebugger.EditorClock = () => EditorApplication.timeSinceStartup;
        EditorApplication.update += Update;
        AssemblyReloadEvents.beforeAssemblyReload += RestoreAll;
    }
    static void Update()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool changed = false;
        foreach (var debugger in UnityEngine.Object.FindObjectsByType<BlendShapeDebugger>(UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None))
            if (debugger.isActiveAndEnabled && debugger.IsHolding) { debugger.TickPreview(EditorApplication.timeSinceStartup); changed = true; }
        if (changed) { SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate(); }
    }
    static void RestoreAll()
    {
        foreach (var debugger in UnityEngine.Object.FindObjectsByType<BlendShapeDebugger>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
            debugger.StopRestore();
    }
}
