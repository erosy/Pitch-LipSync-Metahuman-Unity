using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace Sandbox.CharacterSystem.Editor
{
    [CustomEditor(typeof(CharacterFlowController)), CanEditMultipleObjects]
    public sealed class CharacterFlowControllerEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            foreach (var selected in targets)
            {
                var errors = ((CharacterFlowController)selected).ValidateConfiguration();
                if (errors.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Warning);
            }
        }
    }

    [CustomEditor(typeof(CharacterSpeechPlayer)), CanEditMultipleObjects]
    public sealed class CharacterSpeechPlayerEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            foreach (var selected in targets)
            {
                var errors = new System.Collections.Generic.List<string>();
                ((CharacterSpeechPlayer)selected).ValidateConfiguration(errors);
                if (errors.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Warning);
            }
        }
    }
}
