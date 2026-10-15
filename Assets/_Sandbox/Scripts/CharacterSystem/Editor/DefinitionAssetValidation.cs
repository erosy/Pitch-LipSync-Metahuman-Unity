using System.Collections.Generic;
using UnityEditor;

namespace Sandbox.CharacterSystem.Editor
{
    /// <summary>Project-wide authoring checks; runtime coordination has no AssetDatabase dependency.</summary>
    public static class DefinitionAssetValidation
    {
        public static List<string> ValidateIds()
        {
            var definitions = new List<CharacterDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
            {
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition) definitions.Add(definition);
            }
            var errors = new List<string>();
            CharacterDefinition.ValidateUniqueIds(definitions, errors);
            return errors;
        }
    }
}
