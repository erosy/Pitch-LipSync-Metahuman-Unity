using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    internal static class FacialMotionValidation
    {
        static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        static void Range(string label, Vector2 range, List<string> errors)
        {
            if (!Valid(range.x) || !Valid(range.y) || range.x > range.y)
                errors.Add(label + ": range must be finite, non-negative, and ordered.");
        }
        internal static void Check(IReadOnlyList<FacialMotionDefinition> motions, List<string> errors)
        {
            if (motions == null) { errors.Add("Facial Motions must not be null."); return; }
            foreach (var motion in motions)
            {
                if (motion == null) { errors.Add("Facial Motions contains a null entry."); continue; }
                Range(motion.name + " interval", motion.interval, errors);
                Range(motion.name + " hold", motion.hold, errors);
                if (!Valid(motion.easeIn) || !Valid(motion.easeOut)) errors.Add(motion.name + ": invalid easing duration.");
                if (motion.repeat != null)
                {
                    Range(motion.name + " repeat gap", motion.repeat.gap, errors);
                    if (!Valid(motion.repeat.probability) || motion.repeat.probability > 1
                        || motion.repeat.maximumAdditionalRepetitions < 0 || motion.repeat.maximumAdditionalRepetitions > 32)
                        errors.Add(motion.name + ": repeat probability must be 0–1 and additional repeats 0–32.");
                }
                if (motion.variants == null) continue;
                foreach (var variant in motion.variants)
                {
                    if (variant == null) continue;
                    Range(motion.name + "/" + variant.name + " strength", variant.strength, errors);
                    foreach (var target in FacialMath.Targets(variant.pose))
                        if (!Valid(target.weight) || target.weight > 100) errors.Add(motion.name + ": weights must be finite and 0–100.");
                }
            }
        }
    }
}
