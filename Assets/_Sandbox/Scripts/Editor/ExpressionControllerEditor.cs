using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExpressionController))]
public class ExpressionControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var controller = (ExpressionController)target;
        var source = serializedObject.FindProperty("lipSync").objectReferenceValue;
        var driver = serializedObject.FindProperty("blendShapeDriver").objectReferenceValue as uLipSync.uLipSyncBlendShape;
        if (!source || !driver)
            EditorGUILayout.HelpBox("Assign Lip Sync and Blend Shape Driver to switch automatically during speech. Without them, the selected expression uses its silent face.", MessageType.Info);
        else if (driver.skinnedMeshRenderer != controller.Face)
            EditorGUILayout.HelpBox("Face must match the renderer on Blend Shape Driver.", MessageType.Warning);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !controller.isActiveAndEnabled))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (controller.Expressions != null)
                foreach (var expression in controller.Expressions)
                    if (expression != null && !string.IsNullOrEmpty(expression.name) &&
                        GUILayout.Button(expression.name, GUILayout.Height(28)))
                        controller.SetExpression(expression.name);
            if (GUILayout.Button("Neutral", GUILayout.Height(28))) controller.Neutral();
        }
        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode to use the expression buttons.", MessageType.Info);
        else
            EditorGUILayout.LabelField("Lip Sync State", controller.IsSpeaking ? "Speaking" : "Silent");
    }
}

[CustomPropertyDrawer(typeof(ExpressionController.ShapeWeight))]
public class ExpressionShapeWeightDrawer : PropertyDrawer
{
    Mesh cachedMesh;
    readonly List<string> names = new List<string>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return 3f * EditorGUIUtility.singleLineHeight + 2f * EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(position, label);
        int previousIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel++;
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        var renderer = property.serializedObject.FindProperty("face").objectReferenceValue as SkinnedMeshRenderer;
        var mesh = renderer ? renderer.sharedMesh : null;
        if (mesh != cachedMesh || names.Count != (mesh ? mesh.blendShapeCount : 0) + 1)
        {
            cachedMesh = mesh;
            names.Clear();
            names.Add("None");
            if (mesh)
                for (int i = 0; i < mesh.blendShapeCount; i++) names.Add(mesh.GetBlendShapeName(i));
        }

        var shape = property.FindPropertyRelative("shape");
        var weight = property.FindPropertyRelative("weight");
        int selected = string.IsNullOrEmpty(shape.stringValue) ? 0 : names.IndexOf(shape.stringValue);
        var choices = new List<string>(names);
        if (selected < 0)
        {
            selected = choices.Count;
            choices.Add(shape.stringValue + " (missing)");
        }
        EditorGUI.BeginChangeCheck();
        int next = EditorGUI.Popup(position, "Blendshape", selected, choices.ToArray());
        if (EditorGUI.EndChangeCheck() && next < names.Count)
            shape.stringValue = next == 0 ? "" : names[next];
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(position, weight, new GUIContent("Weight"));
        EditorGUI.indentLevel = previousIndent;
        EditorGUI.EndProperty();
    }
}
