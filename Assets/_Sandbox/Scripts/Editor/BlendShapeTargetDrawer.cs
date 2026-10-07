using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

// Resolve the mesh from this Inspector's root, never from shared/static character state.
public sealed class BlendShapeTargetDrawer : OdinValueDrawer<BlendShapeTarget>
{
    protected override void DrawPropertyLayout(GUIContent label)
    {
        var value = ValueEntry.SmartValue;
        if (value == null) { CallNextDrawer(label); return; }
        object root = Property.Tree.WeakTargets[0];
        string[] names = root is ExpressionController character ? character.GetBlendShapeNames()
            : root is BlendShapeDebugger debugger ? debugger.GetBlendShapeNames() : new string[0];
        EditorGUILayout.BeginVertical();
        if (label != null) EditorGUILayout.LabelField(label);
        var options = new string[names.Length + 1]; options[0] = string.IsNullOrEmpty(value.shape) ? "<Choose shape>" : value.shape;
        System.Array.Copy(names, 0, options, 1, names.Length);
        int selected = EditorGUILayout.Popup("Shape", 0, options);
        if (selected > 0) Property.Children["shape"].ValueEntry.WeakSmartValue = names[selected - 1];
        // Keep arbitrary shape names authorable, including when no mesh is assigned yet.
        Property.Children["shape"].Draw(new GUIContent("Shape name"));
        Property.Children["weight"].Draw(new GUIContent("Weight"));
        if (names.Length > 0 && System.Array.IndexOf(names, value.shape) < 0)
            EditorGUILayout.HelpBox("Shape is missing from the assigned Face.", MessageType.Warning);
        EditorGUILayout.EndVertical();
    }
}
