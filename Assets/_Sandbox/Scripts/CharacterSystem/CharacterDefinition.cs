using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    /// <summary>Shared authoring identity. Asset names and runtime state are independent of this ID.</summary>
    public abstract class CharacterDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable identifier, unique among definitions of this type.")]
        string id;
        [SerializeField] string displayName;

        public string Id => id;
        public string DisplayName => displayName;

        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            CollectValidationErrors(errors);
            return errors;
        }

        protected virtual void CollectValidationErrors(List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id)) errors.Add("Id is required.");
            else if (id != id.Trim()) errors.Add("Id must not have leading or trailing whitespace.");
            if (string.IsNullOrWhiteSpace(displayName)) errors.Add("Display Name is required.");
        }

        protected static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>Checks an authored set without an AssetDatabase dependency or changing assets.</summary>
        public static void ValidateUniqueIds(IEnumerable<CharacterDefinition> definitions, List<string> errors)
        {
            var byType = new Dictionary<Type, Dictionary<string, CharacterDefinition>>();
            foreach (var definition in definitions)
            {
                if (!definition || string.IsNullOrWhiteSpace(definition.Id)) continue;
                var type = definition.GetType();
                if (!byType.TryGetValue(type, out var byId))
                    byType.Add(type, byId = new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal));
                if (byId.TryGetValue(definition.Id, out var other))
                {
                    if (other != definition)
                        errors.Add($"Duplicate {type.Name} Id '{definition.Id}': '{other.name}' and '{definition.name}'.");
                }
                else byId.Add(definition.Id, definition);
            }
        }
    }
}
