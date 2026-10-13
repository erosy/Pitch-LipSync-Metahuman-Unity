using System;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
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
                if (GUILayout.Button("Load INA Defaults")) LoadDefaults(character);
    }

    static void LoadDefaults(ExpressionController character)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorUtility.DisplayDialog("Load INA Defaults", "Replace this character's expression and activity definitions with INA defaults? References, intensity, crossfade, and speech delay are retained.", "Load defaults", "Cancel")) return;
        Undo.RecordObject(character, "Load INA Defaults");
        Set(character, "expressionDefinitions", FacialPresets.Expressions());
        Set(character, "activityDefinitions", FacialPresets.Activities());
        EditorUtility.SetDirty(character);
        PrefabUtility.RecordPrefabInstancePropertyModifications(character);
        if (character.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(character.gameObject.scene);
    }

    static void Set(ExpressionController character, string name, object value)
    {
        var field = typeof(ExpressionController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing serialized field: " + name);
        field.SetValue(character, value);
    }
}
