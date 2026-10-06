using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExpressionController))]
public class ExpressionControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        var controller = (ExpressionController)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        using (new EditorGUILayout.HorizontalScope())
        {
            // one button per expression in the list, plus Neutral
            foreach (var expression in controller.Expressions)
                if (GUILayout.Button(expression.name, GUILayout.Height(28))) controller.SetExpression(expression.name);
            if (GUILayout.Button("Neutral", GUILayout.Height(28))) controller.Neutral();
        }
        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode to use the expression buttons.", MessageType.Info);
    }
}
