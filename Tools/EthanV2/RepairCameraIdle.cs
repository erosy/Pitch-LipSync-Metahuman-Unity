using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sandbox.CharacterSystem;
using Newtonsoft.Json;

public static class RepairCameraIdle
{
    const string ExpressionPath = "Assets/_Sandbox/Data/Expression/Confident Idle Camera.asset";
    const string ActivityPath = "Assets/_Sandbox/Data/Activity/Confident Idle Camera.asset";
    public static object MeasureEyeRanges()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit Mode required");
        var root = PrefabUtility.LoadPrefabContents("Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Candidate.prefab");
        try
        {
            var face = root.GetComponentInChildren<ExpressionModule>(true).Face; var mesh = face.sharedMesh;
            var vertices = mesh.vertices; var matrices = face.bones.Select((b, i) => b.localToWorldMatrix * mesh.bindposes[i]).ToArray();
            int[] starts = new int[vertices.Length];
            using (var counts = mesh.GetBonesPerVertex()) for (int i = 1; i < starts.Length; i++) starts[i] = starts[i - 1] + counts[i - 1];
            using (var weights = mesh.GetAllBoneWeights()) using (var counts = mesh.GetBonesPerVertex())
            {
                Func<int, Vector3, Vector3> skin = (index, point) => { var result = Vector3.zero; for (int k = 0; k < counts[index]; k++) { var w = weights[starts[index] + k]; result += matrices[w.boneIndex].MultiplyPoint3x4(point) * w.weight; } return result; };
                var rows = new System.Collections.Generic.List<object>();
                foreach (var eye in new[] { (sub: 6, bone: "FACIAL_L_Eye", side: "Left"), (sub: 7, bone: "FACIAL_R_Eye", side: "Right") })
                {
                    var center = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == eye.bone).position;
                    int pole = mesh.GetTriangles(eye.sub).Distinct().OrderByDescending(i => skin(i, vertices[i]).z).First();
                    var neutral = skin(pole, vertices[pole]) - center;
                    float neutralPitch = Mathf.Atan2(neutral.y, new Vector2(neutral.x, neutral.z).magnitude) * Mathf.Rad2Deg;
                    foreach (string shape in new[] { "eyeLookDown" + eye.side, "eyeLookUp" + eye.side, "eyeLookIn" + eye.side, "eyeLookOut" + eye.side })
                    {
                        var deltas = new Vector3[mesh.vertexCount]; mesh.GetBlendShapeFrameVertices(mesh.GetBlendShapeIndex(shape), 0, deltas, null, null);
                        var posed = skin(pole, vertices[pole] + deltas[pole]) - center;
                        rows.Add(new { shape, neutralPitch, rotationAt100Degrees = Vector3.Angle(neutral, posed),
                            pitchAt100 = Mathf.Atan2(posed.y, new Vector2(posed.x, posed.z).magnitude) * Mathf.Rad2Deg });
                    }
                }
                File.WriteAllText("Data/EthanV2/CameraGaze/measured_eye_ranges.json", JsonConvert.SerializeObject(rows, Formatting.Indented)); return rows;
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static object Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit Mode required");
        var source = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsInactive.Include).Single(c => c.name == "EthanV2_Candidate");
        var sourceModule = source.GetComponentInChildren<ExpressionModule>(true);
        var field = typeof(ExpressionModule).GetField("speechGaze", BindingFlags.Instance | BindingFlags.NonPublic);
        var sourceGaze = (SpeechGaze)field.GetValue(sourceModule);
        var root = PrefabUtility.LoadPrefabContents("Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Candidate.prefab");
        var graph = PlayableGraph.Create("Camera gaze isolated Edit Mode verification");
        try
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            var animator = root.GetComponentInChildren<Animator>(true); animator.applyRootMotion = false; animator.fireEvents = false;
            var frame = new GameObject("Verified gaze frame").transform;
            frame.SetParent(animator.GetBoneTransform(HumanBodyBones.Head), false);
            frame.localPosition = sourceGaze.head.localPosition; frame.localRotation = sourceGaze.head.localRotation;
            var target = new GameObject("Verified audience target").transform; target.SetParent(root.transform, false); target.position = Camera.main.transform.position;
            var gaze = new SpeechGaze { enabled = true, lookAtPlayerDuringIdle = true, head = frame, target = target,
                assistHead = false, horizontalEyeRange = sourceGaze.horizontalEyeRange, verticalEyeRange = sourceGaze.verticalEyeRange };
            var face = root.GetComponentInChildren<ExpressionModule>(true).Face;
            var activity = AssetDatabase.LoadAssetAtPath<ActivityDefinition>(ActivityPath);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); var playable = AnimationClipPlayable.Create(graph, activity.Animation.Clip);
            playable.SetApplyFootIK(false); AnimationPlayableOutput.Create(graph, "pose", animator).SetSourcePlayable(playable); graph.Play();
            var apply = typeof(SpeechGaze).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic);
            var restore = typeof(SpeechGaze).GetMethod("Restore", BindingFlags.Instance | BindingFlags.NonPublic);
            var rows = new System.Collections.Generic.List<object>();
            foreach (float phase in new[] { 0f, .25f, .5f, .75f, 1f })
            {
                restore.Invoke(gaze, null); playable.SetTime(activity.Animation.Clip.length * phase); graph.Evaluate(0);
                var baseline = new System.Collections.Generic.Dictionary<string, float>();
                for (int i = 0; i < face.sharedMesh.blendShapeCount; i++) { face.SetBlendShapeWeight(i, 0); baseline[face.sharedMesh.GetBlendShapeName(i)] = 0; }
                foreach (var entry in activity.Expression.Targets) { baseline[entry.Shape] = entry.Weight; face.SetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex(entry.Shape), entry.Weight); }
                var rotation = animator.GetBoneTransform(HumanBodyBones.Head).rotation;
                apply.Invoke(gaze, new object[] { face, root.transform, false, 1f, new System.Collections.Generic.HashSet<string>(), true,
                    new System.Collections.Generic.HashSet<string>(activity.Expression.Targets.Select(t => t.Shape)), baseline, null, null });
                var direction = Quaternion.Inverse(frame.rotation) * (target.position - frame.position);
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
                var eyes = Enumerable.Range(0, face.sharedMesh.blendShapeCount).Where(i => face.sharedMesh.GetBlendShapeName(i).StartsWith("eyeLook"))
                    .Select(i => new { name = face.sharedMesh.GetBlendShapeName(i), weight = face.GetBlendShapeWeight(i) }).ToArray();
                float headDifference = Quaternion.Angle(rotation, animator.GetBoneTransform(HumanBodyBones.Head).rotation);
                if (Mathf.Abs(yaw) > gaze.horizontalEyeRange || Mathf.Abs(pitch) > gaze.verticalEyeRange || headDifference > .01f) throw new Exception("Gaze target outside eye range or head pose changed");
                foreach (var eye in eyes)
                {
                    float expected = eye.name.Contains("Up") ? Mathf.Max(0, pitch / gaze.verticalEyeRange) * 100 : eye.name.Contains("Down") ? Mathf.Max(0, -pitch / gaze.verticalEyeRange) * 100
                        : eye.name == "eyeLookInLeft" || eye.name == "eyeLookOutRight" ? Mathf.Max(0, yaw / gaze.horizontalEyeRange) * 100 : Mathf.Max(0, -yaw / gaze.horizontalEyeRange) * 100;
                    if (Mathf.Abs(eye.weight - expected) > .01f) throw new Exception("Camera gaze weight mismatch: " + eye.name);
                }
                rows.Add(new { phase, targetYaw = yaw, targetPitch = pitch, headDifferenceDegrees = headDifference, eyes });
            }
            var result = new { validated = true, liveSceneSampled = false, samples = rows };
            Directory.CreateDirectory("Data/EthanV2/CameraGaze"); File.WriteAllText("Data/EthanV2/CameraGaze/verification.json", JsonConvert.SerializeObject(result, Formatting.Indented)); return result;
        }
        finally { graph.Destroy(); PrefabUtility.UnloadPrefabContents(root); }
    }
    public static object Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit Mode required; user controls Play Mode.");
        var character = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsInactive.Include).Single(c => c.name == "EthanV2_Candidate");
        var module = character.GetComponentInChildren<ExpressionModule>(true);
        var expression = AssetDatabase.LoadAssetAtPath<ExpressionDefinition>(ExpressionPath);
        if (!expression)
        {
            expression = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExpressionDefinition>("Assets/_Sandbox/Data/Expression/Confident Idle Upward.asset"));
            expression.name = "Confident Idle Camera";
            var data = new SerializedObject(expression); data.FindProperty("id").stringValue = "expression-confident-idle-camera";
            data.FindProperty("displayName").stringValue = "Confident Idle Camera";
            var targets = data.FindProperty("targets");
            // A zero entry also reserves a gaze axis, so remove direction entries entirely.
            for (int i = targets.arraySize - 1; i >= 0; i--)
                if (targets.GetArrayElementAtIndex(i).FindPropertyRelative("shape").stringValue.StartsWith("eyeLook")) targets.DeleteArrayElementAtIndex(i);
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(expression, ExpressionPath);
        }
        var activity = AssetDatabase.LoadAssetAtPath<ActivityDefinition>(ActivityPath);
        if (!activity)
        {
            activity = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ActivityDefinition>("Assets/_Sandbox/Data/Activity/Confident Idle.asset"));
            activity.name = "Confident Idle Camera";
            var data = new SerializedObject(activity); data.FindProperty("id").stringValue = "activity-confident-idle-camera";
            data.FindProperty("displayName").stringValue = "Confident Idle Camera"; data.FindProperty("expression").objectReferenceValue = expression;
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(activity, ActivityPath);
        }
        var gaze = (SpeechGaze)typeof(ExpressionModule).GetField("speechGaze", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module);
        var frame = gaze.head;
        if (!frame || frame.name != "SpeechGazeFrame") throw new Exception("Expected existing scene gaze helper; refusing to rotate a skeleton bone");
        var leftEye = character.GetComponentsInChildren<Transform>(true).Single(t => t.name == "FACIAL_L_Eye");
        var rightEye = character.GetComponentsInChildren<Transform>(true).Single(t => t.name == "FACIAL_R_Eye");
        Undo.RecordObject(frame, "Correct Ethan gaze frame");
        frame.SetPositionAndRotation((leftEye.position + rightEye.position) * .5f, Quaternion.LookRotation(character.transform.forward, character.transform.up));
        var moduleData = new SerializedObject(module); Undo.RecordObject(module, "Enable Ethan idle eye contact");
        moduleData.FindProperty("speechGaze.enabled").boolValue = true;
        moduleData.FindProperty("speechGaze.lookAtPlayerDuringIdle").boolValue = true;
        // Measured from this mesh's full-weight eye shapes, rather than the generic estimates.
        // Vertical uses the downward range because this scene's camera is below the raised eyes.
        moduleData.FindProperty("speechGaze.horizontalEyeRange").floatValue = 40.25f;
        moduleData.FindProperty("speechGaze.verticalEyeRange").floatValue = 34.67f;
        // Camera following should move the eyes while preserving the deliberately authored chin pose.
        moduleData.FindProperty("speechGaze.assistHead").boolValue = false;
        moduleData.ApplyModifiedProperties();
        var characterData = new SerializedObject(character); Undo.RecordObject(character, "Select camera-facing confident idle");
        characterData.FindProperty("defaultIdle").objectReferenceValue = activity; characterData.ApplyModifiedProperties();
        foreach (var item in new UnityEngine.Object[] { frame, module, character }) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        foreach (var item in new UnityEngine.Object[] { expression, activity }) AssetDatabase.SaveAssetIfDirty(item);
        EditorSceneManager.MarkSceneDirty(character.gameObject.scene);
        if (!EditorSceneManager.SaveScene(character.gameObject.scene)) throw new Exception("Could not save scene");
        var errors = character.ValidateConfiguration(); if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
        var result = new { expression = ExpressionPath, activity = ActivityPath, scene = character.gameObject.scene.path,
            gaze.enabled, gaze.lookAtPlayerDuringIdle, gaze.assistHead,
            frameForward = frame.forward.ToString("G8"), framePosition = frame.position.ToString("G8"), validated = true,
            materials = module.Face.sharedMaterials.Select(m => m ? m.name : "null").ToArray() };
        Directory.CreateDirectory("Data/EthanV2/CameraGaze");
        File.WriteAllText("Data/EthanV2/CameraGaze/repair.json", JsonConvert.SerializeObject(result, Formatting.Indented)); return result;
    }
}
