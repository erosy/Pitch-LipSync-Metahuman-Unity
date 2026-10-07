using System.Collections.Generic;
using UnityEngine;

// Persistent expression playback: holds its final pose until a different target is selected.
public sealed class FacialExpressionPlayback
{
    readonly Dictionary<string, float> current = new Dictionary<string, float>();
    readonly Dictionary<string, float> start = new Dictionary<string, float>();
    readonly Dictionary<string, float> target = new Dictionary<string, float>();
    readonly Dictionary<string, float> next = new Dictionary<string, float>();
    readonly List<string> shapes = new List<string>();
    float elapsed;
    bool dirty = true;
    public bool IsTransitioning { get; private set; } = true;
    public IReadOnlyDictionary<string, float> Output => current;

    public void Configure(IEnumerable<string> ownedShapes, IReadOnlyDictionary<string, float> initial = null)
    {
        current.Clear(); start.Clear(); target.Clear(); next.Clear(); shapes.Clear();
        foreach (var shape in ownedShapes)
        {
            if (current.ContainsKey(shape)) continue;
            shapes.Add(shape); current[shape] = initial != null && initial.TryGetValue(shape, out var value) ? value : 0;
        }
        dirty = true; IsTransitioning = true;
    }
    public void Tick(float deltaTime, IReadOnlyList<ExpressionDefinition> definitions, string name, bool speaking, float intensity, float fadeTime)
    {
        next.Clear(); foreach (var shape in shapes) next[shape] = 0;
        if (!string.IsNullOrEmpty(name) && definitions != null)
            foreach (var definition in definitions)
            {
                if (definition == null || definition.name != name) continue;
                foreach (var shape in FacialMath.Targets(speaking ? definition.speakingPose : definition.silentPose))
                    if (!string.IsNullOrEmpty(shape.shape) && next.ContainsKey(shape.shape))
                        // Preserve the legacy global intensity multiplier, including weights above 100.
                        next[shape.shape] = FacialMath.NonNegative(shape.weight * intensity);
                break;
            }
        bool changed = dirty || target.Count != next.Count;
        foreach (var pair in next) if (!target.TryGetValue(pair.Key, out var value) || value != pair.Value) changed = true;
        if (changed)
        {
            start.Clear(); target.Clear();
            foreach (var pair in current) start[pair.Key] = pair.Value;
            foreach (var pair in next) target[pair.Key] = pair.Value;
            elapsed = 0; dirty = false;
        }
        float duration = Mathf.Max(.01f, FacialMath.NonNegative(fadeTime));
        elapsed = Mathf.Min(duration, elapsed + FacialMath.NonNegative(deltaTime));
        float progress = FacialMath.Smooth(elapsed / duration);
        foreach (var shape in shapes) current[shape] = Mathf.Lerp(start[shape], target[shape], progress);
        IsTransitioning = elapsed < duration;
    }
}
