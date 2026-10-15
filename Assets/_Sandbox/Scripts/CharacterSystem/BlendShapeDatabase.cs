using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    /// <summary>The shared blendshape vocabulary all game characters must support.</summary>
    [CreateAssetMenu(fileName = "BlendShapeDatabase", menuName = "Character System/Blendshape Database")]
    public sealed class BlendShapeDatabase : ScriptableObject
    {
        public const string DefaultResourcePath = "CharacterSystem/BlendShapeDatabase";
        static BlendShapeDatabase defaultDatabase;
        [SerializeField] List<string> blendShapes = new List<string>();

        public static BlendShapeDatabase Default
        {
            get
            {
                if (!defaultDatabase) defaultDatabase = Resources.Load<BlendShapeDatabase>(DefaultResourcePath);
                return defaultDatabase;
            }
        }

        public IReadOnlyList<string> Names => blendShapes;

        public bool Contains(string shape) => !string.IsNullOrEmpty(shape)
            && blendShapes != null && blendShapes.Contains(shape);

        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (blendShapes == null || blendShapes.Count == 0)
            {
                errors.Add("Blendshape database must contain at least one shape name.");
                return errors;
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < blendShapes.Count; i++)
            {
                string shape = blendShapes[i];
                if (string.IsNullOrWhiteSpace(shape)) errors.Add($"Blendshape {i}: name is required.");
                else
                {
                    if (shape != shape.Trim()) errors.Add($"Blendshape {i}: name must not have surrounding whitespace.");
                    if (!names.Add(shape)) errors.Add($"Blendshape {i}: duplicate name '{shape}'.");
                }
            }
            return errors;
        }

        /// <summary>Requires the full standard vocabulary. Additional character-specific shapes are allowed.</summary>
        public List<string> ValidateMesh(Mesh mesh)
        {
            var errors = ValidateConfiguration();
            if (!mesh) { errors.Add("Assign a character face mesh to validate blendshape compatibility."); return errors; }
            if (blendShapes == null) return errors;
            foreach (var shape in blendShapes)
                if (!string.IsNullOrWhiteSpace(shape) && mesh.GetBlendShapeIndex(shape) < 0)
                    errors.Add($"Mesh '{mesh.name}' is missing standard blendshape '{shape}'.");
            return errors;
        }
    }
}
