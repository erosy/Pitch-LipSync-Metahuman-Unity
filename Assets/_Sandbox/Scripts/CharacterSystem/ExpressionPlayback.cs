using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    // Per-instance pose blending. Definitions remain immutable configuration.
    internal sealed class ExpressionPlayback
    {
        readonly Dictionary<string, float> current = new Dictionary<string, float>();
        readonly Dictionary<string, float> start = new Dictionary<string, float>();
        readonly Dictionary<string, float> target = new Dictionary<string, float>();
        readonly HashSet<string> targetShapes = new HashSet<string>();
        readonly HashSet<string> protectedShapes = new HashSet<string>();
        float elapsed, duration;
        public ExpressionDefinition Definition { get; private set; }
        public bool IsTransitioning => elapsed < duration;
        public IReadOnlyDictionary<string, float> Output => current;
        public HashSet<string> ProtectedShapes
        {
            get
            {
                protectedShapes.Clear(); protectedShapes.UnionWith(targetShapes);
                foreach (var pair in current) if (pair.Value != 0) protectedShapes.Add(pair.Key);
                return protectedShapes;
            }
        }

        public ExpressionPlayback(IEnumerable<string> shapes, IReadOnlyDictionary<string, float> initial)
        {
            foreach (var shape in shapes)
                current[shape] = initial != null && initial.TryGetValue(shape, out var value) ? value : 0;
        }

        public void Play(ExpressionDefinition definition, float intensity, float neutralFade)
        {
            Definition = definition;
            start.Clear(); target.Clear(); targetShapes.Clear();
            foreach (var pair in current) { start[pair.Key] = pair.Value; target[pair.Key] = 0; }
            if (definition)
                foreach (var weight in definition.Targets)
                    if (current.ContainsKey(weight.Shape))
                    { target[weight.Shape] = weight.Weight * intensity; targetShapes.Add(weight.Shape); }
            duration = definition ? definition.FadeDuration : neutralFade;
            elapsed = 0;
            Tick(0);
        }

        public void Tick(float delta)
        {
            elapsed = Mathf.Min(duration, elapsed + FacialMath.NonNegative(delta));
            float factor = duration > 0 ? FacialMath.Smooth(elapsed / duration) : 1;
            foreach (var pair in target) current[pair.Key] = Mathf.Lerp(start[pair.Key], pair.Value, factor);
        }
    }
}
