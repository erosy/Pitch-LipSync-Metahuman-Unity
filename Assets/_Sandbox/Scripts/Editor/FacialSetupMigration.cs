using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FacialSetupMigration
{
    static FieldInfo RuntimeField(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing serialized field: " + name);

    public static void LoadDefaults(ExpressionController character)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorUtility.DisplayDialog("Load INA Defaults", "Replace this character's expression and activity definitions with INA defaults? References, intensity, crossfade, and speech delay are retained.", "Load defaults", "Cancel")) return;
        Undo.RecordObject(character, "Load INA Defaults");
        Set(character, "expressionDefinitions", FacialPresets.Expressions()); Set(character, "activityDefinitions", FacialPresets.Activities());
        Dirty(character);
    }
    static void Set(UnityEngine.Object target, string field, object value) => RuntimeField(target.GetType(), field).SetValue(target, value);
    static void Dirty(Component component)
    {
        EditorUtility.SetDirty(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        if (component.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }
}
