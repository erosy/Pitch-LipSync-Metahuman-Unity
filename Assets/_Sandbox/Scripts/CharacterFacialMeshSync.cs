using System;
using UnityEngine;

/// <summary>Mirrors the primary face's named shapes to separate eyes/mouth skins.</summary>
[ExecuteAlways, DefaultExecutionOrder(32100), DisallowMultipleComponent]
public sealed class CharacterFacialMeshSync : MonoBehaviour
{
    [SerializeField] SkinnedMeshRenderer source;
    [SerializeField] SkinnedMeshRenderer[] targets = Array.Empty<SkinnedMeshRenderer>();
    Mesh cachedMesh;
    int[][] indices;
    float[] previous;

    public void Configure(SkinnedMeshRenderer primary, SkinnedMeshRenderer[] secondary)
    { source = primary; targets = secondary; cachedMesh = null; }
    void OnEnable() { cachedMesh = null; }
    void LateUpdate() => Apply();

    public void Apply()
    {
        if (!source || !source.sharedMesh) return;
        if (cachedMesh != source.sharedMesh || indices == null || indices.Length != targets.Length)
        {
            cachedMesh = source.sharedMesh;
            previous = new float[cachedMesh.blendShapeCount];
            indices = new int[targets.Length][];
            for (int i = 0; i < previous.Length; i++) previous[i] = float.NaN;
            for (int t = 0; t < targets.Length; t++)
            {
                indices[t] = new int[previous.Length];
                for (int i = 0; i < previous.Length; i++)
                    indices[t][i] = targets[t] && targets[t].sharedMesh
                        ? targets[t].sharedMesh.GetBlendShapeIndex(cachedMesh.GetBlendShapeName(i)) : -1;
            }
        }
        for (int i = 0; i < previous.Length; i++)
        {
            float weight = source.GetBlendShapeWeight(i);
            if (weight == previous[i]) continue;
            previous[i] = weight;
            for (int t = 0; t < targets.Length; t++)
                if (targets[t] && indices[t][i] >= 0) targets[t].SetBlendShapeWeight(indices[t][i], weight);
        }
    }
}
