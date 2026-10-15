using System;
using System.Collections.Generic;
using UnityEngine;

public interface IFacialRandom { float Unit(); }
public sealed class FacialRandom : IFacialRandom
{
    readonly System.Random random = new System.Random();
    public float Unit() => (float)random.NextDouble();
}

public static class FacialMath
{
    public static float NonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
    public static Vector2 Range(Vector2 value)
    {
        float a = NonNegative(value.x), b = NonNegative(value.y);
        return new Vector2(Mathf.Min(a, b), Mathf.Max(a, b));
    }
    public static float Sample(Vector2 range, IFacialRandom random)
    {
        range = Range(range);
        return Mathf.Lerp(range.x, range.y, Mathf.Clamp01(random.Unit()));
    }
    public static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3 - 2 * value); }
    public static float Weight(float value) => Mathf.Clamp(NonNegative(value), 0, 100);
    public static IEnumerable<BlendShapeTarget> Targets(BlendShapePose pose)
    {
        if (pose?.targets == null) yield break;
        foreach (var target in pose.targets) if (target != null) yield return target;
    }
}

