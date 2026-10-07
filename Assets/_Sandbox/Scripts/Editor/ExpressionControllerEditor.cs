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
        {
            using (new EditorGUI.DisabledScope(targets.Length != 1))
            {
                using (new EditorGUI.DisabledScope(character.IsMigrated))
                    if (GUILayout.Button("Migrate Facial Setup")) FacialSetupMigration.Migrate(character);
                if (GUILayout.Button("Load INA Defaults")) FacialSetupMigration.LoadDefaults(character);
            }
        }
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
            if (character.Expressions != null)
                foreach (var expression in character.Expressions)
                    if (expression != null && !string.IsNullOrEmpty(expression.name) && expression.name != "Joy")
                        if (GUILayout.Button("Select " + expression.name)) character.SetExpression(expression.name);
    }
}
