using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExpressionController))]
public class ExpressionControllerEditor : OdinEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var character = (ExpressionController)target;
        if (character.ValidateConfiguration().Count > 0)
            EditorGUILayout.HelpBox(character.ConfigurationWarnings, MessageType.Warning);
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            using (new EditorGUI.DisabledScope(targets.Length != 1))
                if (GUILayout.Button("Load INA Defaults")) FacialSetupMigration.LoadDefaults(character);
    }
}
