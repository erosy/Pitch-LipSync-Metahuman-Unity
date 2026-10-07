using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FacialSetupMigration
{
    public sealed class Snapshot
    {
        public ExpressionController character;
        public BlinkController blink;
        public IdleFaceController idle;
        public BlendShapeDebugger debugger;
        public List<ExpressionDefinition> expressions;
        public List<FacialActivityDefinition> activities;
    }
    static SerializedProperty Field(SerializedObject source, string name) => source.FindProperty(name)
        ?? throw new InvalidOperationException("Missing legacy field: " + name);
    static float Float(SerializedObject source, string name) => Field(source, name).floatValue;
    static Vector2 Range(SerializedObject source, string name) => Field(source, name).vector2Value;
    static bool Bool(SerializedObject source, string name) => Field(source, name).boolValue;
    static UnityEngine.Object Reference(SerializedObject source, string name) => Field(source, name).objectReferenceValue;
    static FieldInfo RuntimeField(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing serialized field: " + name);

    // This function only reads serialized data and constructs new ordinary C# definitions.
    public static Snapshot Inspect(ExpressionController character)
    {
        if (!character) throw new ArgumentNullException(nameof(character));
        var snapshot = new Snapshot { character = character, blink = character.GetComponent<BlinkController>(), idle = character.GetComponent<IdleFaceController>() };
        if (!character.Face || !character.Face.sharedMesh) throw new InvalidOperationException("Assign a valid Face before migration.");
        var controllerData = new SerializedObject(character);
        snapshot.debugger = Reference(controllerData, "blendShapeDebugger") as BlendShapeDebugger;
        void ValidateFace(SerializedObject component)
        {
            if (Reference(component, "face") != character.Face) throw new InvalidOperationException(component.targetObject.GetType().Name + " has a different or missing Face. Migration made no changes.");
            var debugger = Reference(component, "blendShapeDebugger") as BlendShapeDebugger;
            if (!debugger) return;
            if (snapshot.debugger && snapshot.debugger != debugger) throw new InvalidOperationException("Multiple debugger references disagree. Resolve them before migration.");
            snapshot.debugger = debugger;
        }
        LegacyBlinkSettings blink = null; LegacyIdleSettings idle = null;
        if (snapshot.blink)
        {
            var data = new SerializedObject(snapshot.blink); ValidateFace(data);
            blink = new LegacyBlinkSettings { enabled = snapshot.blink.enabled, interval = Range(data, "blinkInterval"), gap = Range(data, "doubleBlinkGap"),
                close = Float(data, "closingDuration"), hold = Float(data, "closedHoldDuration"), open = Float(data, "openingDuration"),
                weight = Float(data, "closureWeight"), repeatProbability = Float(data, "doubleBlinkProbability") };
        }
        if (snapshot.idle)
        {
            var data = new SerializedObject(snapshot.idle); ValidateFace(data);
            if (Reference(data, "expressionController") != character) throw new InvalidOperationException("IdleFaceController references another expression controller. Migration made no changes.");
            idle = new LegacyIdleSettings { enabled = snapshot.idle.enabled, mouthEnabled = Bool(data, "enableMouthGestures"), gazeEnabled = Bool(data, "enableGazeShifts"),
                mouthInterval = Range(data, "mouthInterval"), purseHold = Range(data, "purseHold"), twitchWeight = Range(data, "twitchWeight"),
                gazeInterval = Range(data, "gazeInterval"), gazeWeight = Range(data, "gazeWeight"), gazeHold = Range(data, "gazeHold"),
                pucker = Float(data, "puckerWeight"), press = Float(data, "lipPressWeight"), purseIn = Float(data, "purseEaseIn"), purseOut = Float(data, "purseEaseOut"),
                twitchIn = Float(data, "twitchEaseIn"), twitchHold = Float(data, "twitchHold"), twitchOut = Float(data, "twitchEaseOut"),
                gazeIn = Float(data, "gazeEaseIn"), gazeOut = Float(data, "gazeEaseOut") };
        }
        if (snapshot.debugger)
        {
            if (snapshot.debugger.Face != character.Face) throw new InvalidOperationException("Debugger's Face differs. Migration made no changes.");
            if (snapshot.debugger.Character && snapshot.debugger.Character != character) throw new InvalidOperationException("Debugger belongs to another character.");
            if (snapshot.debugger.IsHolding) throw new InvalidOperationException("Stop / Restore the debugger preview before migrating.");
        }
        snapshot.expressions = FacialLegacyConversion.Expressions((ExpressionController.Expression[])RuntimeField(typeof(ExpressionController), "expressions").GetValue(character));
        snapshot.activities = FacialLegacyConversion.Activities(blink, idle);
        return snapshot;
    }

    public static void Migrate(ExpressionController character)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || character.IsMigrated) return;
        Snapshot snapshot;
        try { snapshot = Inspect(character); }
        catch (Exception error) { Debug.LogError(error.Message, character); return; }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Migrate Facial Setup");
        try
        {
            Undo.RecordObject(character, "Migrate Facial Setup");
            Set(character, "expressionDefinitions", snapshot.expressions); Set(character, "activityDefinitions", snapshot.activities);
            Set(character, "blendShapeDebugger", snapshot.debugger);
            Set(character, "configurationVersion", ExpressionController.CurrentConfigurationVersion);
            if (snapshot.debugger)
            {
                Undo.RecordObject(snapshot.debugger, "Migrate Facial Setup");
                Set(snapshot.debugger, "character", character);
                var data = new SerializedObject(snapshot.debugger);
                if (data.FindProperty("pose.targets").arraySize == 0 && !string.IsNullOrEmpty(snapshot.debugger.shape))
                    Set(snapshot.debugger, "pose", new BlendShapePose(new BlendShapeTarget(snapshot.debugger.shape, 100)));
                Dirty(snapshot.debugger);
            }
            // Flush component data first so destruction and all serialized changes share one Undo.
            Dirty(character); Undo.FlushUndoRecordObjects();
            if (snapshot.blink) Undo.DestroyObjectImmediate(snapshot.blink);
            if (snapshot.idle) Undo.DestroyObjectImmediate(snapshot.idle);
            Undo.CollapseUndoOperations(group);
            Debug.Log("Facial setup migrated. Review the copied settings, then save the scene. Undo restores the previous setup.", character);
        }
        catch (Exception error)
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(group);
            Debug.LogError("Migration rolled back: " + error.Message, character);
        }
    }

    public static void LoadDefaults(ExpressionController character)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (character.GetComponent<BlinkController>() || character.GetComponent<IdleFaceController>())
        { Debug.LogWarning("Migrate the existing blink/idle setup first. Loading defaults cannot replace that setup safely.", character); return; }
        if (!EditorUtility.DisplayDialog("Load INA Defaults", "Replace this character's expression and activity definitions with INA defaults? References, intensity, crossfade, and speech delay are retained.", "Load defaults", "Cancel")) return;
        Undo.RecordObject(character, "Load INA Defaults");
        Set(character, "expressionDefinitions", FacialPresets.Expressions()); Set(character, "activityDefinitions", FacialPresets.Activities());
        Set(character, "configurationVersion", ExpressionController.CurrentConfigurationVersion); Dirty(character);
    }
    static void Set(UnityEngine.Object target, string field, object value) => RuntimeField(target.GetType(), field).SetValue(target, value);
    static void Dirty(Component component)
    {
        EditorUtility.SetDirty(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        if (component.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }
}
