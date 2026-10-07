using System.Collections.Generic;
using UnityEngine;

public static class FacialValidation
{
    public static void Check(IReadOnlyList<ExpressionDefinition> expressions, IReadOnlyList<FacialActivityDefinition> activities, List<string> warnings)
    {
        if (expressions != null) foreach (var definition in expressions)
        {
            if (definition == null) continue;
            Weights(definition.name, definition.silentPose, warnings); Weights(definition.name, definition.speakingPose, warnings);
        }
        if (activities == null) return;
        foreach (var activity in activities)
        {
            if (activity == null) continue;
            Range(activity.name + " interval", activity.interval, warnings); Range(activity.name + " hold", activity.hold, warnings);
            if (!Valid(activity.easeIn) || !Valid(activity.easeOut)) warnings.Add(activity.name + ": phase durations must be finite and non-negative.");
            if (activity.repeat != null)
            {
                Range(activity.name + " repeat gap", activity.repeat.gap, warnings);
                if (!Valid(activity.repeat.probability) || activity.repeat.probability > 1 || activity.repeat.maximumAdditionalRepetitions < 0 || activity.repeat.maximumAdditionalRepetitions > 32)
                    warnings.Add(activity.name + ": repeat probability must be 0–1 and additional repeats 0–32.");
            }
            if (activity.eligibility == ActivityEligibility.SelectedExpressions)
            {
                if (activity.expressionNames == null || activity.expressionNames.Count == 0) warnings.Add(activity.name + ": no selected expressions.");
                else foreach (var name in activity.expressionNames)
                {
                    bool found = string.IsNullOrEmpty(name);
                    if (expressions != null) foreach (var expression in expressions) if (expression != null && expression.name == name) found = true;
                    if (!found) warnings.Add(activity.name + ": unknown selected expression '" + name + "'.");
                }
            }
            if (activity.variants != null) foreach (var variant in activity.variants)
            {
                if (variant == null) continue;
                Range(activity.name + "/" + variant.name + " strength", variant.strength, warnings);
                Weights(activity.name + "/" + variant.name, variant.pose, warnings);
            }
        }
    }
    static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    static void Range(string label, Vector2 range, List<string> warnings)
    { if (!Valid(range.x) || !Valid(range.y) || range.x > range.y) warnings.Add(label + ": range must be finite, non-negative, and ordered (min ≤ max)."); }
    static void Weights(string label, BlendShapePose pose, List<string> warnings)
    {
        foreach (var target in FacialMath.Targets(pose)) if (!Valid(target.weight) || target.weight > 100) warnings.Add(label + ": weights must be 0–100.");
    }
}
