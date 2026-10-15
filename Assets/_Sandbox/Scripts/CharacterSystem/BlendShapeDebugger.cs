using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Sandbox.CharacterSystem;

[ExecuteAlways, DefaultExecutionOrder(32000)]
[AddComponentMenu("Debug/Blend Shape Debugger")]
public class BlendShapeDebugger : MonoBehaviour
{
    static readonly HashSet<BlendShapeDebugger> activeDebuggers = new HashSet<BlendShapeDebugger>();
    internal static IReadOnlyCollection<BlendShapeDebugger> ActivePreviews => activeDebuggers;
    [SerializeField, FoldoutGroup("References"), LabelText("Expression Module")] ExpressionModule character;
    [SerializeField, FoldoutGroup("Preview")] BlendShapePose pose = new BlendShapePose();
    [SerializeField, FoldoutGroup("Preview"), Min(.1f)] float fadeTime = 1;
    sealed class WeightState { public float original, start, target; }
    readonly Dictionary<int, WeightState> states = new Dictionary<int, WeightState>();
    SkinnedMeshRenderer activeFace;
    Mesh activeMesh;
    double startedAt;
    float duration;
    // The editor bridge supplies EditorApplication's clock without UnityEditor runtime dependencies.
    public static Func<double> EditorClock;
    double Clock => !Application.IsPlaying(gameObject) && EditorClock != null ? EditorClock() : Time.realtimeSinceStartupAsDouble;
    IFacialPreviewOwner PreviewOwner => character ? character : null;
    IFacialPreviewOwner activeOwner;
    SkinnedMeshRenderer ConfiguredFace => PreviewOwner?.Face;
    public ExpressionModule Character => character;
    public SkinnedMeshRenderer Face => IsHolding ? activeFace : ConfiguredFace;
    [ShowInInspector, ReadOnly, FoldoutGroup("Preview")] public bool IsHolding => states.Count > 0;
    [ShowInInspector, ReadOnly, MultiLineProperty(4), FoldoutGroup("References")]
    public string Warnings
    {
        get
        {
            var warnings = new List<string>();
            if (!ConfiguredFace || !ConfiguredFace.sharedMesh) warnings.Add("Assign a character with a Face.");
            if (PreviewOwner != null && PreviewOwner.Debugger != this) warnings.Add("Also assign this debugger in the module's References group.");
            foreach (var target in FacialMath.Targets(pose))
                if (!ConfiguredFace || !ConfiguredFace.sharedMesh || ConfiguredFace.sharedMesh.GetBlendShapeIndex(target.shape ?? "") < 0)
                    warnings.Add("Missing preview shape: " + target.shape);
            return string.Join("\n", warnings);
        }
    }
    public string[] GetBlendShapeNames()
    {
        var mesh = ConfiguredFace ? ConfiguredFace.sharedMesh : null;
        if (!mesh) return new string[0];
        var names = new string[mesh.blendShapeCount];
        for (int i = 0; i < names.Length; i++) names[i] = mesh.GetBlendShapeName(i);
        return names;
    }
    [Button("Preview Pose", DirtyOnClick = false), FoldoutGroup("Preview")]
    public void PlayAnimation()
    {
        var renderer = ConfiguredFace;
        if (!isActiveAndEnabled || !renderer || !renderer.sharedMesh) return;
        if (activeFace != renderer || activeMesh != renderer.sharedMesh) StopRestore();
        if (!IsHolding)
        {
            ExpressionModule.StopPreviewsFor(renderer);
            StopForFace(renderer, this);
            PreviewOwner?.BeginDebuggerPreview(this);
            activeOwner = PreviewOwner;
        }
        activeFace = renderer; activeMesh = renderer.sharedMesh;
        foreach (var state in states) state.Value.start = renderer.GetBlendShapeWeight(state.Key);
        var targets = new List<BlendShapeTarget>(FacialMath.Targets(pose));
        foreach (var target in targets)
        {
            if (activeOwner != null && !activeOwner.CanPreviewShape(target.shape)) continue;
            int index = activeMesh.GetBlendShapeIndex(target.shape ?? "");
            if (index < 0) { Debug.LogWarning(name + ": missing preview shape '" + target.shape + "'.", this); continue; }
            if (!states.TryGetValue(index, out var state))
            { state = new WeightState { original = renderer.GetBlendShapeWeight(index) }; states.Add(index, state); }
            state.start = renderer.GetBlendShapeWeight(index); state.target = FacialMath.Weight(target.weight);
        }
        duration = Mathf.Max(.1f, FacialMath.NonNegative(fadeTime)); startedAt = Clock;
        if (IsHolding) activeDebuggers.Add(this);
        else StopRestore();
    }
    [Button("Stop / Restore", DirtyOnClick = false), FoldoutGroup("Preview")]
    public void StopRestore()
    {
        activeDebuggers.Remove(this);
        if (activeFace && activeFace.sharedMesh == activeMesh)
            foreach (var state in states)
                if (activeOwner == null || activeOwner.CanPreviewShape(activeMesh.GetBlendShapeName(state.Key)))
                    activeFace.SetBlendShapeWeight(state.Key, state.Value.original);
        states.Clear(); activeFace = null; activeMesh = null;
        var previousOwner = activeOwner; activeOwner = null;
        previousOwner?.EndDebuggerPreview(this);
    }
    public static void StopForFace(SkinnedMeshRenderer renderer, BlendShapeDebugger except = null)
    {
        foreach (var debugger in new List<BlendShapeDebugger>(activeDebuggers))
            if (debugger && debugger != except && debugger.Face == renderer) debugger.StopRestore();
    }
    public void TickPreview(double time)
    {
        if (!IsHolding) return;
        if (!activeFace || activeFace.sharedMesh != activeMesh || activeFace != ConfiguredFace) { StopRestore(); return; }
        float progress = Mathf.Clamp01((float)((time - startedAt) / duration));
        foreach (var state in states)
            if (activeOwner == null || activeOwner.CanPreviewShape(activeMesh.GetBlendShapeName(state.Key)))
                activeFace.SetBlendShapeWeight(state.Key, Mathf.Lerp(state.Value.start, state.Value.target, progress));
    }
    void LateUpdate() { if (Application.IsPlaying(gameObject)) TickPreview(Clock); }
    void OnDisable() => StopRestore();
    void Reset()
    {
        character = GetComponentInParent<ExpressionModule>(true);
    }
}
