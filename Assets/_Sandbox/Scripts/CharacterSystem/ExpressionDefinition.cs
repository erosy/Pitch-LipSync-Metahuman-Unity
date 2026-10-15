using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    [Serializable]
    public sealed class BlendShapeWeight
    {
        [SerializeField, ValueDropdown(nameof(GetAvailableShapes))] string shape;
        [SerializeField, Range(0, 100)] float weight;

        public string Shape => shape;
        public float Weight => weight;

        public BlendShapeWeight() { }
        public BlendShapeWeight(string shape, float weight) { this.shape = shape; this.weight = weight; }

        IEnumerable<string> GetAvailableShapes()
        {
            var database = BlendShapeDatabase.Default;
            return database ? database.Names : Array.Empty<string>();
        }
    }

    [CreateAssetMenu(fileName = "Expression", menuName = "Character System/Expression Definition")]
    public sealed class ExpressionDefinition : CharacterDefinition
    {
        [SerializeField] List<BlendShapeWeight> targets = new List<BlendShapeWeight>();
        [SerializeField, Min(0)] float fadeDuration = .18f;

        public IReadOnlyList<BlendShapeWeight> Targets => targets;
        public float FadeDuration => fadeDuration;
        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        public BlendShapeDatabase Database => BlendShapeDatabase.Default;

        protected override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            var database = Database;
            if (!database) errors.Add("Shared BlendShapeDatabase is missing from Resources/CharacterSystem/BlendShapeDatabase.");
            else
                foreach (var error in database.ValidateConfiguration()) errors.Add("Blendshape Database: " + error);
            if (!IsFinite(fadeDuration) || fadeDuration < 0)
                errors.Add("Fade Duration must be finite and non-negative.");
            if (targets == null) { errors.Add("Targets list must not be null."); return; }
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null) { errors.Add($"Target {i}: entry is null."); continue; }
                if (string.IsNullOrWhiteSpace(target.Shape)) errors.Add($"Target {i}: Shape is required.");
                else
                {
                    if (!names.Add(target.Shape)) errors.Add($"Target {i}: duplicate shape '{target.Shape}'.");
                    if (database && !database.Contains(target.Shape))
                        errors.Add($"Target {i}: shape '{target.Shape}' is not in the shared blendshape database.");
                }
                if (!IsFinite(target.Weight) || target.Weight < 0 || target.Weight > 100)
                    errors.Add($"Target {i}: Weight must be finite and between 0 and 100.");
            }
        }
    }
}
