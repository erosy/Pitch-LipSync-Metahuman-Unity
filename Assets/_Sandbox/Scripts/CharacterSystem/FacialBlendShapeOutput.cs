using System.Collections.Generic;
using UnityEngine;

// Sole renderer writer for the modular system. Never owns uLipSync's reserved indices.
public sealed class FacialBlendShapeOutput
{
    readonly Dictionary<string, int> indices = new Dictionary<string, int>();
    readonly HashSet<int> owned = new HashSet<int>();
    SkinnedMeshRenderer face;
    Mesh mesh;
    public SkinnedMeshRenderer Face => face;
    public Mesh Mesh => mesh;
    public HashSet<string> Available { get; } = new HashSet<string>();
    public void Bind(SkinnedMeshRenderer renderer)
    {
        face = renderer; mesh = face ? face.sharedMesh : null;
        indices.Clear(); owned.Clear(); Available.Clear();
        if (!mesh) return;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        { string name = mesh.GetBlendShapeName(i); indices[name] = i; Available.Add(name); }
    }
    public void SetOwned(IEnumerable<string> shapes, HashSet<string> reserved)
    {
        owned.Clear();
        foreach (var shape in shapes) if (!reserved.Contains(shape) && indices.TryGetValue(shape, out var index)) owned.Add(index);
    }
    public Dictionary<string, float> Read(IEnumerable<string> shapes)
    {
        var values = new Dictionary<string, float>();
        if (!face || face.sharedMesh != mesh) return values;
        foreach (var shape in shapes) if (indices.TryGetValue(shape, out var index)) values[shape] = face.GetBlendShapeWeight(index);
        return values;
    }
    public void Clear(HashSet<string> reserved = null)
    {
        if (!face || face.sharedMesh != mesh) return;
        foreach (var index in owned)
            if (reserved == null || !reserved.Contains(mesh.GetBlendShapeName(index))) face.SetBlendShapeWeight(index, 0);
    }
    public void Apply(IReadOnlyDictionary<string, float> expressions, IReadOnlyDictionary<string, float> activities)
    {
        if (!face || face.sharedMesh != mesh) return;
        foreach (var index in owned) face.SetBlendShapeWeight(index, 0);
        Write(expressions, false); Write(activities, true);
    }
    void Write(IReadOnlyDictionary<string, float> values, bool clampWeight)
    {
        if (values == null) return;
        foreach (var pair in values) if (indices.TryGetValue(pair.Key, out var index) && owned.Contains(index))
            face.SetBlendShapeWeight(index, clampWeight ? FacialMath.Weight(pair.Value) : FacialMath.NonNegative(pair.Value));
    }
}
