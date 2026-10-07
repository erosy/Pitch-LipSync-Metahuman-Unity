using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DefaultExecutionOrder(32000)]
[AddComponentMenu("Debug/Blend Shape Debugger")]
public class BlendShapeDebugger : MonoBehaviour
{


    public string shape;

    [SerializeField] SkinnedMeshRenderer face;
    [FormerlySerializedAs("previewDuration")]
    [Tooltip("Seconds to fade all listed blendshapes to their target weights.")]
    [SerializeField, Min(0.1f)] float fadeTime = 1f;

    class WeightState
    {
        public float original;
        public float start;
        public float target;
    }

    readonly Dictionary<int, WeightState> states = new Dictionary<int, WeightState>();
    SkinnedMeshRenderer activeFace;
    Mesh activeMesh;
    double startedAt;
    float duration;

    public SkinnedMeshRenderer Face => face;
    public bool IsHolding => states.Count > 0;

    public string[] GetBlendShapeNames()
    {
        var mesh = face ? face.sharedMesh : null;
        if (!mesh) return new string[0];
        var names = new string[mesh.blendShapeCount];
        for (int i = 0; i < names.Length; i++) names[i] = mesh.GetBlendShapeName(i);
        return names;
    }

    public void PlayAnimation()
    {
        if (!isActiveAndEnabled || !face || !face.sharedMesh) return;
        if (activeFace != face || activeMesh != face.sharedMesh) StopRestore();
        activeFace = face;
        activeMesh = face.sharedMesh;
        foreach (var blendState in states)
            blendState.Value.start = face.GetBlendShapeWeight(blendState.Key);
        
        if (string.IsNullOrEmpty(shape)) return;

        int index = activeMesh.GetBlendShapeIndex(shape);
        if (!states.TryGetValue(index, out var state))
            {
                state = new WeightState { original = face.GetBlendShapeWeight(index) };
                states.Add(index, state);
            }
            state.start = face.GetBlendShapeWeight(index);
            state.target = 100f;

        duration = Mathf.Max(0.1f, fadeTime);
        startedAt = CurrentTime;
    }

    public void StopRestore()
    {
        if (activeFace && activeFace.sharedMesh == activeMesh)
            foreach (var state in states)
                activeFace.SetBlendShapeWeight(state.Key, state.Value.original);
        states.Clear();
        activeFace = null;
        activeMesh = null;
    }

    double CurrentTime
    {
        get
        {
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject)) return UnityEditor.EditorApplication.timeSinceStartup;
#endif
            return Time.realtimeSinceStartupAsDouble;
        }
    }

    void ApplyAnimation()
    {
        if (states.Count == 0) return;
        if (!activeFace || activeFace.sharedMesh != activeMesh || activeFace != face)
        {
            StopRestore();
            return;
        }
        float progress = Mathf.Clamp01((float)((CurrentTime - startedAt) / duration));
        foreach (var state in states)
            activeFace.SetBlendShapeWeight(state.Key,
                Mathf.Lerp(state.Value.start, state.Value.target, progress));
    }

    void LateUpdate()
    {
        if (Application.IsPlaying(gameObject)) ApplyAnimation();
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update += EditorUpdate;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += StopRestore;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorUpdate;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= StopRestore;
#endif
        StopRestore();
    }

#if UNITY_EDITOR
    void EditorUpdate()
    {
        if (Application.IsPlaying(gameObject) || !IsHolding) return;
        ApplyAnimation();
        UnityEditor.SceneView.RepaintAll();
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif

    void Reset()
    {
        foreach (var candidate in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!candidate.sharedMesh || candidate.sharedMesh.blendShapeCount == 0) continue;
            if (!face || candidate.sharedMesh.blendShapeCount > face.sharedMesh.blendShapeCount)
                face = candidate;
        }
    }
}
