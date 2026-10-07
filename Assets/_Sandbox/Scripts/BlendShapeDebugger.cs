using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways, DefaultExecutionOrder(32000)]
[AddComponentMenu("Debug/Blend Shape Debugger")]
public class BlendShapeDebugger : MonoBehaviour
{
    [SerializeField, FoldoutGroup("References")] ExpressionController character;
    [SerializeField, HideInInspector] SkinnedMeshRenderer face; // Legacy fallback until migration.
    [HideInInspector] public string shape; // Legacy single-shape preview remains serialized.
    [SerializeField, FoldoutGroup("Preview")] BlendShapePose pose = new BlendShapePose();
    [FormerlySerializedAs("previewDuration")]
    [SerializeField, FoldoutGroup("Preview"), Min(.1f)] float fadeTime = 1;
    sealed class WeightState { public float original, start, target; }
    readonly Dictionary<int, WeightState> states = new Dictionary<int, WeightState>();
    SkinnedMeshRenderer activeFace;
    Mesh activeMesh;
    double startedAt;
    float duration;
    // The editor bridge supplies EditorApplication's clock without UnityEditor runtime dependencies.
    public static Func<double> EditorClock;
    double Clock => !Application.isPlaying && EditorClock != null ? EditorClock() : Time.realtimeSinceStartupAsDouble;
    SkinnedMeshRenderer ConfiguredFace => character ? character.Face : face;
    public ExpressionController Character => character;
    public SkinnedMeshRenderer Face => IsHolding ? activeFace : ConfiguredFace;
    [ShowInInspector, ReadOnly, FoldoutGroup("Preview")] public bool IsHolding => states.Count > 0;
    [ShowInInspector, ReadOnly, MultiLineProperty(4), FoldoutGroup("References")]
    public string Warnings
    {
        get
        {
            var warnings = new List<string>();
            if (!ConfiguredFace || !ConfiguredFace.sharedMesh) warnings.Add("Assign a character with a Face.");
            if (character && character.Debugger != this) warnings.Add("Also assign this debugger in the character's References group so facial output yields during previews.");
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
    [Button("Preview Pose"), FoldoutGroup("Preview")]
    public void PlayAnimation()
    {
        var renderer = ConfiguredFace;
        if (!isActiveAndEnabled || !renderer || !renderer.sharedMesh) return;
        if (activeFace != renderer || activeMesh != renderer.sharedMesh) StopRestore();
        activeFace = renderer; activeMesh = renderer.sharedMesh;
        foreach (var state in states) state.Value.start = renderer.GetBlendShapeWeight(state.Key);
        var targets = new List<BlendShapeTarget>(FacialMath.Targets(pose));
        if (targets.Count == 0 && !string.IsNullOrEmpty(shape)) targets.Add(new BlendShapeTarget(shape, 100));
        foreach (var target in targets)
        {
            int index = activeMesh.GetBlendShapeIndex(target.shape ?? "");
            if (index < 0) { Debug.LogWarning(name + ": missing preview shape '" + target.shape + "'.", this); continue; }
            if (!states.TryGetValue(index, out var state))
            { state = new WeightState { original = renderer.GetBlendShapeWeight(index) }; states.Add(index, state); }
            state.start = renderer.GetBlendShapeWeight(index); state.target = FacialMath.Weight(target.weight);
        }
        duration = Mathf.Max(.1f, FacialMath.NonNegative(fadeTime)); startedAt = Clock;
    }
    [Button("Stop / Restore"), FoldoutGroup("Preview")]
    public void StopRestore()
    {
        if (activeFace && activeFace.sharedMesh == activeMesh)
            foreach (var state in states) activeFace.SetBlendShapeWeight(state.Key, state.Value.original);
        states.Clear(); activeFace = null; activeMesh = null;
    }
    public void TickPreview(double time)
    {
        if (!IsHolding) return;
        if (!activeFace || activeFace.sharedMesh != activeMesh || activeFace != ConfiguredFace) { StopRestore(); return; }
        float progress = Mathf.Clamp01((float)((time - startedAt) / duration));
        foreach (var state in states)
            activeFace.SetBlendShapeWeight(state.Key, Mathf.Lerp(state.Value.start, state.Value.target, progress));
    }
    void LateUpdate() { if (Application.isPlaying) TickPreview(Clock); }
    void OnDisable() => StopRestore();
    void Reset() => character = GetComponent<ExpressionController>();
}
