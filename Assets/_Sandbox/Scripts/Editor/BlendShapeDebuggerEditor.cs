using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(BlendShapeDebugger))]
public class BlendShapeDebuggerEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new PropertyField(serializedObject.FindProperty("face")));
        root.Add(new PropertyField(serializedObject.FindProperty("fadeTime")));
        var shape = serializedObject.FindProperty("shape");
        var dropdown = new PopupField<string>(
            "Shape", new List<string> { "None" }, 0);

        void Refresh()
        {
            var debugger = (BlendShapeDebugger)target;
            var names = new List<string> { "None" };
            names.AddRange(debugger.GetBlendShapeNames());

            if (!string.IsNullOrEmpty(shape.stringValue) &&
                !names.Contains(shape.stringValue))
                names.Add(shape.stringValue);

            dropdown.choices = names;
            dropdown.SetValueWithoutNotify(
                string.IsNullOrEmpty(shape.stringValue)
                    ? "None"
                    : shape.stringValue);
        }

        dropdown.RegisterValueChangedCallback(evt =>
        {
            serializedObject.Update();
            shape.stringValue = evt.newValue == "None" ? "" : evt.newValue;
            serializedObject.ApplyModifiedProperties();
        });

        root.TrackPropertyValue(shape, _ => Refresh());
        root.TrackPropertyValue(
            serializedObject.FindProperty("face"), _ => Refresh());

        Refresh();
        root.Add(dropdown);

        root.Add(new HelpBox("Add blendshapes. Play Animation fades them together and holds the result in Edit Mode or Play Mode. Stop / Restore restores the weights captured before playback.", HelpBoxMessageType.Info));
        root.Add(new Button(() =>
        {
            serializedObject.ApplyModifiedProperties();
            ((BlendShapeDebugger)target).PlayAnimation();
        })
        { text = "Play Animation" });
        root.Add(new Button(() => ((BlendShapeDebugger)target).StopRestore())
        {
            text = "Stop / Restore"
        });
        return root;
    }
}
