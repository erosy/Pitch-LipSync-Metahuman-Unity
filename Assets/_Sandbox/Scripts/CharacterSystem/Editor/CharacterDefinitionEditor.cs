using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace Sandbox.CharacterSystem.Editor
{
    [CustomEditor(typeof(CharacterDefinition), true), CanEditMultipleObjects]
    public sealed class CharacterDefinitionEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            foreach (var selected in targets)
            {
                var definition = (CharacterDefinition)selected;
                var errors = definition.ValidateConfiguration();
                if (errors.Count > 0)
                    EditorGUILayout.HelpBox(definition.name + ":\n" + string.Join("\n", errors), MessageType.Warning);
            }
            var idErrors = DefinitionAssetValidation.ValidateIds();
            if (idErrors.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", idErrors), MessageType.Warning);
        }
    }

    [CustomEditor(typeof(Character)), CanEditMultipleObjects]
    public sealed class CharacterEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            foreach (var selected in targets)
            {
                var character = (Character)selected;
                var errors = character.ValidateConfiguration();
                if (errors.Count > 0)
                    EditorGUILayout.HelpBox(character.name + ":\n" + string.Join("\n", errors), MessageType.Warning);
            }
        }
    }
}
