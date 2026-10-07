using System.Collections.Generic;
using UnityEngine;

// Snapshots let conversion be tested without components or renderer writes.
public sealed class LegacyBlinkSettings
{
    public bool enabled;
    public Vector2 interval, gap;
    public float close, hold, open, weight, repeatProbability;
}
public sealed class LegacyIdleSettings
{
    public bool enabled, mouthEnabled, gazeEnabled;
    public Vector2 mouthInterval, purseHold, twitchWeight, gazeInterval, gazeWeight, gazeHold;
    public float pucker, press, purseIn, purseOut, twitchIn, twitchHold, twitchOut, gazeIn, gazeOut;
}
public static class FacialLegacyConversion
{
    public static List<ExpressionDefinition> Expressions(ExpressionController.Expression[] legacy)
    {
        var definitions = new List<ExpressionDefinition>();
        if (legacy == null) return definitions;
        foreach (var expression in legacy)
        {
            if (expression == null) continue;
            var definition = new ExpressionDefinition { name = expression.name };
            if (!string.IsNullOrEmpty(expression.defaultFace.shape))
                definition.silentPose.targets.Add(new BlendShapeTarget(expression.defaultFace.shape, expression.defaultFace.weight));
            if (expression.speakingShapes != null)
                foreach (var target in expression.speakingShapes)
                    definition.speakingPose.targets.Add(new BlendShapeTarget(target.shape, target.weight));
            definitions.Add(definition);
        }
        return definitions;
    }
    public static List<FacialActivityDefinition> Activities(LegacyBlinkSettings blink, LegacyIdleSettings idle)
    {
        var definitions = FacialPresets.Activities();
        var b = definitions[0]; b.enabled = blink != null && blink.enabled;
        if (blink != null)
        {
            b.interval = blink.interval; b.easeIn = blink.close; b.hold = new Vector2(blink.hold, blink.hold); b.easeOut = blink.open;
            foreach (var target in b.variants[0].pose.targets) target.weight = blink.weight;
            b.repeat.probability = blink.repeatProbability; b.repeat.gap = blink.gap;
        }
        var purse = definitions[1]; var twitch = definitions[2]; var gaze = definitions[3];
        purse.enabled = twitch.enabled = idle != null && idle.enabled && idle.mouthEnabled;
        gaze.enabled = idle != null && idle.enabled && idle.gazeEnabled;
        if (idle == null) return definitions;
        purse.interval = twitch.interval = idle.mouthInterval;
        purse.easeIn = idle.purseIn; purse.hold = idle.purseHold; purse.easeOut = idle.purseOut;
        purse.variants[0].pose.targets[0].weight = idle.pucker;
        purse.variants[0].pose.targets[1].weight = purse.variants[0].pose.targets[2].weight = idle.press;
        twitch.easeIn = idle.twitchIn; twitch.hold = new Vector2(idle.twitchHold, idle.twitchHold); twitch.easeOut = idle.twitchOut;
        foreach (var variant in twitch.variants) SetWeightRange(variant, idle.twitchWeight);
        gaze.interval = idle.gazeInterval; gaze.easeIn = idle.gazeIn; gaze.hold = idle.gazeHold; gaze.easeOut = idle.gazeOut;
        foreach (var variant in gaze.variants) SetWeightRange(variant, idle.gazeWeight);
        return definitions;
    }
    static void SetWeightRange(ActivityVariant variant, Vector2 range)
    {
        float max = range.y;
        variant.strength = max > 0 ? new Vector2(range.x / max, 1) : Vector2.one;
        foreach (var target in variant.pose.targets) target.weight = max;
    }
}
