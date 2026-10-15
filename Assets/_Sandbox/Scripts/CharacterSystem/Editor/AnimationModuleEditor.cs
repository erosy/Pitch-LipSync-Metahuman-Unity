using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Sandbox.CharacterSystem.Editor
{
    [CustomEditor(typeof(AnimationModule))]
    public sealed class AnimationModuleEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var module = (AnimationModule)target;
            // This is a playback command, not a serialized authoring edit. Odin's automatic
            // dirty tracking would autosave the prefab and stop the preview after scrubbing.
            bool authoredChange = GUI.changed;
            using (new EditorGUI.DisabledScope(!module.IsPreviewing))
            {
                EditorGUI.BeginChangeCheck();
                float time = EditorGUILayout.Slider(new GUIContent("Preview Time", "Normalized preview time; scrubbing does not save the prefab."),
                    module.PreviewNormalizedTime, 0, 1);
                if (EditorGUI.EndChangeCheck()) module.PreviewNormalizedTime = time;
            }
            GUI.changed = authoredChange;
            var errors = new List<string>();
            module.ValidateConfiguration(errors);
            if (errors.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Warning);
            if (module.IsPreviewing) Repaint();
        }
    }
}
