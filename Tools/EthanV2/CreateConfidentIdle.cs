using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sandbox.CharacterSystem;
using Newtonsoft.Json;

// Run through Pipeline run_script, outside Assets: no runtime component or scene assignment.
public static class CreateConfidentIdle
{
    const string Candidate = "Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Candidate.prefab";
    const string ClipPath = "Assets/_Sandbox/Animations/Confident Idle.anim";
    const string AnimationPath = "Assets/_Sandbox/Data/Animation/Confident Idle.asset";
    const string ExpressionPath = "Assets/_Sandbox/Data/Expression/Confident Idle Upward.asset";
    const string ActivityPath = "Assets/_Sandbox/Data/Activity/Confident Idle.asset";
    const string Report = "Data/EthanV2/ConfidentIdle";
    static readonly string[] Muscles = { "Neck Nod Down-Up", "Neck Tilt Left-Right", "Neck Turn Left-Right",
        "Head Nod Down-Up", "Head Tilt Left-Right", "Head Turn Left-Right" };
    static void Guard() { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit Mode required; user controls Play Mode."); }
    static void Save(string name, object data) { Directory.CreateDirectory(Report); File.WriteAllText(Report + "/" + name + ".json", JsonConvert.SerializeObject(data, Formatting.Indented)); }
    static AnimationDefinition Source() => AssetDatabase.LoadAssetAtPath<AnimationDefinition>("Assets/_Sandbox/Data/Animation/Idle.asset");
    sealed class Sampler : IDisposable
    {
        public readonly GameObject Root;
        public readonly Animator Animator;
        public readonly Transform Neck, Head;
        public readonly HumanPoseHandler Handler;
        readonly PlayableGraph graph;
        readonly AnimationClipPlayable playable;
        readonly Transform[] transforms;
        readonly Vector3[] positions, scales;
        readonly Quaternion[] rotations;
        public Sampler(AnimationClip clip)
        {
            Root = PrefabUtility.LoadPrefabContents(Candidate);
            foreach (var behaviour in Root.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            Animator = Root.GetComponentInChildren<Animator>(true);
            Animator.applyRootMotion = false; Animator.fireEvents = false;
            if (!Animator.avatar || !Animator.avatar.isHuman || !Animator.avatar.isValid) throw new Exception("Valid humanoid avatar required");
            Neck = Animator.GetBoneTransform(HumanBodyBones.Neck); Head = Animator.GetBoneTransform(HumanBodyBones.Head);
            Handler = new HumanPoseHandler(Animator.avatar, Animator.transform);
            transforms = Root.GetComponentsInChildren<Transform>(true);
            positions = transforms.Select(t => t.localPosition).ToArray();
            rotations = transforms.Select(t => t.localRotation).ToArray();
            scales = transforms.Select(t => t.localScale).ToArray();
            graph = PlayableGraph.Create("Confident idle asset authoring"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph, "Sample", Animator).SetSourcePlayable(playable); graph.Play();
        }
        public void Sample(float time)
        {
            for (int i = 0; i < transforms.Length; i++) { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; transforms[i].localScale = scales[i]; }
            playable.SetTime(time); graph.Evaluate(0);
        }
        public void Dispose() { if (graph.IsValid()) graph.Destroy(); Handler.Dispose(); PrefabUtility.UnloadPrefabContents(Root); }
    }
    static Quaternion Offset => Quaternion.Euler(-5, 3, -2);
    static Quaternion InCharacter(GameObject root, Quaternion offset) => root.transform.rotation * offset * Quaternion.Inverse(root.transform.rotation);
    static void Identity(UnityEngine.Object asset, string id, string display)
    {
        var so = new SerializedObject(asset); so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = display; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Reference(UnityEngine.Object asset, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(asset); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static object Build()
    {
        Guard();
        foreach (string path in new[] { ClipPath, AnimationPath, ExpressionPath, ActivityPath })
            if (File.Exists(path)) throw new Exception("Refusing to overwrite " + path);
        var sourceDefinition = Source(); var source = sourceDefinition.Clip;
        var bindings = Muscles.Select(n => EditorCurveBinding.FloatCurve("", typeof(Animator), n)).ToArray();
        var indices = Muscles.Select(n => Array.IndexOf(HumanTrait.MuscleName, n)).ToArray();
        if (indices.Any(i => i < 0)) throw new Exception("Missing humanoid muscle");
        var originalCurves = bindings.Select(b => AnimationUtility.GetEditorCurve(source, b)).ToArray();
        if (originalCurves.Any(c => c == null)) throw new Exception("Expected head and neck muscle curves on source");
        var keys = bindings.Select(b => new List<Keyframe>()).ToArray();
        int frames = Mathf.CeilToInt(source.length * source.frameRate);
        float maxDelta = 0;
        using (var sampler = new Sampler(source))
        {
            var original = new HumanPose(); var posed = new HumanPose();
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = Mathf.Min(frame / source.frameRate, source.length);
                sampler.Sample(time); sampler.Handler.GetHumanPose(ref original);
                var headRotation = sampler.Head.rotation;
                sampler.Neck.rotation = InCharacter(sampler.Root, Quaternion.Slerp(Quaternion.identity, Offset, 1f / 3)) * sampler.Neck.rotation;
                sampler.Head.rotation = InCharacter(sampler.Root, Offset) * headRotation;
                sampler.Handler.GetHumanPose(ref posed);
                for (int m = 0; m < bindings.Length; m++)
                {
                    float delta = posed.muscles[indices[m]] - original.muscles[indices[m]];
                    maxDelta = Mathf.Max(maxDelta, Mathf.Abs(delta));
                    keys[m].Add(new Keyframe(time, originalCurves[m].Evaluate(time) + delta));
                }
            }
        }
        if (maxDelta < .001f) throw new Exception("HumanPose readback did not capture the requested offset");
        var clip = UnityEngine.Object.Instantiate(source); clip.name = "Confident Idle"; clip.hideFlags = HideFlags.None;
        for (int m = 0; m < bindings.Length; m++)
        {
            var curve = new AnimationCurve(keys[m].ToArray());
            for (int k = 0; k < curve.length; k++) { AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear); }
            AnimationUtility.SetEditorCurve(clip, bindings[m], curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(source); settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, ClipPath);
        var animation = UnityEngine.Object.Instantiate(sourceDefinition); animation.name = "Confident Idle";
        Identity(animation, "animation-confident-idle", "Confident Idle"); Reference(animation, "clip", clip);
        AssetDatabase.CreateAsset(animation, AnimationPath);
        var expression = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExpressionDefinition>("Assets/_Sandbox/Data/Expression/Confident_idle.asset"));
        expression.name = "Confident Idle Upward"; Identity(expression, "expression-confident-idle-upward", "Confident Idle Upward");
        var serialized = new SerializedObject(expression); var targets = serialized.FindProperty("targets");
        foreach (string shape in new[] { "eyeLookUpLeft", "eyeLookUpRight", "eyeLookDownLeft", "eyeLookDownRight" })
        {
            int index = -1;
            for (int i = 0; i < targets.arraySize; i++) if (targets.GetArrayElementAtIndex(i).FindPropertyRelative("shape").stringValue == shape) index = i;
            if (index < 0) { index = targets.arraySize; targets.arraySize++; }
            var entry = targets.GetArrayElementAtIndex(index); entry.FindPropertyRelative("shape").stringValue = shape;
            entry.FindPropertyRelative("weight").floatValue = shape.StartsWith("eyeLookUp") ? 10 : 0;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(expression, ExpressionPath);
        var activity = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ActivityDefinition>("Assets/_Sandbox/Data/Activity/Idle.asset"));
        activity.name = "Confident Idle"; Identity(activity, "activity-confident-idle", "Confident Idle");
        Reference(activity, "animation", animation); Reference(activity, "expression", expression);
        var activitySerialized = new SerializedObject(activity); activitySerialized.FindProperty("isIdle").boolValue = true; activitySerialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(activity, ActivityPath);
        // Save only new assets, preserving unrelated dirty scenes and assets.
        foreach (var asset in new UnityEngine.Object[] { clip, animation, expression, activity }) AssetDatabase.SaveAssetIfDirty(asset);
        var result = new { clip = ClipPath, animation = AnimationPath, expression = ExpressionPath, activity = ActivityPath,
            source = AssetDatabase.GetAssetPath(source), sampleCount = frames + 1, source.frameRate, source.length, maxMuscleDelta = maxDelta,
            copiedOnly = true, assigned = false, offsetsDegrees = new { chinUp = 5, screenLeftTurn = 3, screenLeftLean = 2 } };
        Save("build", result); return result;
    }
    public static object Inspect()
    {
        Guard(); var root = PrefabUtility.LoadPrefabContents(Candidate);
        try
        {
            var animator = root.GetComponentInChildren<Animator>(true); var source = Source().Clip;
            var result = new { root.activeSelf, animator.enabled, animator.isHuman, avatar = AssetDatabase.GetAssetPath(animator.avatar),
                source.name, source.length, source.frameRate, source.isHumanMotion,
                muscles = Muscles.Select(n => new { name = n, index = Array.IndexOf(HumanTrait.MuscleName, n) }).ToArray(),
                bindings = AnimationUtility.GetCurveBindings(source).Select(b => new { b.path, type = b.type.Name, b.propertyName }).ToArray(),
                head = animator.GetBoneTransform(HumanBodyBones.Head).position.ToString("G8"),
                rootRotation = root.transform.rotation.eulerAngles.ToString("G8") };
            Save("source_inspection", result); return result;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static bool SameCurve(AnimationCurve a, AnimationCurve b) => a != null && b != null
        && a.preWrapMode == b.preWrapMode && a.postWrapMode == b.postWrapMode && a.keys.SequenceEqual(b.keys);
    public static object Verify()
    {
        Guard(); var source = Source().Clip; var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        var changes = new List<string>(); var bindings = AnimationUtility.GetCurveBindings(source);
        foreach (var binding in bindings)
            if (!SameCurve(AnimationUtility.GetEditorCurve(source, binding), AnimationUtility.GetEditorCurve(copy, binding)))
                changes.Add(binding.propertyName);
        if (!changes.OrderBy(n => n).SequenceEqual(Muscles.OrderBy(n => n))) throw new Exception("Unexpected changed curves: " + string.Join(", ", changes));
        if (AnimationUtility.GetCurveBindings(copy).Length != bindings.Length) throw new Exception("Curve count changed");
        var animation = AssetDatabase.LoadAssetAtPath<AnimationDefinition>(AnimationPath);
        var expression = AssetDatabase.LoadAssetAtPath<ExpressionDefinition>(ExpressionPath);
        var activity = AssetDatabase.LoadAssetAtPath<ActivityDefinition>(ActivityPath);
        var errors = animation.ValidateConfiguration().Concat(expression.ValidateConfiguration()).Concat(activity.ValidateConfiguration()).ToList();
        if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
        if (!activity.IsIdle || !animation.Loop || activity.Animation != animation || activity.Expression != expression) throw new Exception("Incorrect definition wiring");
        if (Mathf.Abs(copy.length - source.length) > .00001f || copy.frameRate != source.frameRate) throw new Exception("Duration or frame rate changed");
        if (!AnimationUtility.GetAnimationClipSettings(copy).loopTime) throw new Exception("Clip must loop");
        Func<AnimationClip, string> events = c => JsonConvert.SerializeObject(AnimationUtility.GetAnimationEvents(c)
            .Select(e => new { e.time, e.functionName, e.stringParameter, e.floatParameter, e.intParameter, e.messageOptions,
                reference = e.objectReferenceParameter ? AssetDatabase.GetAssetPath(e.objectReferenceParameter) : null }));
        if (events(source) != events(copy)) throw new Exception("Events changed");
        if (animation.PlaybackSpeed != Source().PlaybackSpeed || animation.CrossfadeDuration != Source().CrossfadeDuration) throw new Exception("Playback settings changed");
        var originalExpression = AssetDatabase.LoadAssetAtPath<ExpressionDefinition>("Assets/_Sandbox/Data/Expression/Confident_idle.asset");
        foreach (var target in originalExpression.Targets.Where(t => !t.Shape.StartsWith("eyeLookUp") && !t.Shape.StartsWith("eyeLookDown")))
            if (!expression.Targets.Any(t => t.Shape == target.Shape && t.Weight == target.Weight)) throw new Exception("Original facial weight changed: " + target.Shape);
        foreach (var shape in new[] { "eyeLookUpLeft", "eyeLookUpRight", "eyeLookDownLeft", "eyeLookDownRight" })
            if (expression.Targets.Single(t => t.Shape == shape).Weight != (shape.StartsWith("eyeLookUp") ? 10 : 0)) throw new Exception("Incorrect gaze weight");
        CharacterDefinition.ValidateUniqueIds(new CharacterDefinition[] { Source(), animation, originalExpression, expression, activity }, errors);
        if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
        var rows = new List<object>(); float maxHeadError = 0, maxBodyPosition = 0, maxBodyAngle = 0;
        using (var original = new Sampler(source)) using (var edited = new Sampler(copy))
        {
            var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
            foreach (float phase in new[] { 0f, .125f, .25f, .375f, .5f, .625f, .75f, .875f, .999f, 1f })
            {
                original.Sample(source.length * phase); edited.Sample(copy.length * phase);
                Quaternion expected = InCharacter(original.Root, Offset) * original.Head.rotation;
                float headError = Quaternion.Angle(expected, edited.Head.rotation); maxHeadError = Mathf.Max(maxHeadError, headError);
                foreach (var bone in bones)
                {
                    var a = original.Animator.GetBoneTransform(bone); var b = edited.Animator.GetBoneTransform(bone);
                    maxBodyPosition = Mathf.Max(maxBodyPosition, Vector3.Distance(a.position, b.position));
                    maxBodyAngle = Mathf.Max(maxBodyAngle, Quaternion.Angle(a.rotation, b.rotation));
                }
                var delta = edited.Head.rotation * Quaternion.Inverse(original.Head.rotation);
                rows.Add(new { phase, offsetEuler = delta.eulerAngles.ToString("G8"), targetErrorDegrees = headError });
            }
            original.Sample(0); edited.Sample(0); var originalStart = original.Head.rotation; var editedStart = edited.Head.rotation;
            original.Sample(source.length); edited.Sample(copy.length);
            var result = new { changedCurves = changes, unchangedCurveCount = bindings.Length - changes.Count, samples = rows,
                maxHeadTargetErrorDegrees = maxHeadError, maxBodyPositionDifferenceMeters = maxBodyPosition, maxBodyRotationDifferenceDegrees = maxBodyAngle,
                sourceHeadLoopSeamDegrees = Quaternion.Angle(originalStart, original.Head.rotation), copiedHeadLoopSeamDegrees = Quaternion.Angle(editedStart, edited.Head.rotation),
                definitionsValid = true, copy.length, copy.frameRate, originalAssetsChanged = false, assigned = false };
            Save("verification", result);
            if (maxHeadError > .5f) throw new Exception("Head offset target error exceeds 0.5 degrees: " + maxHeadError);
            if (maxBodyPosition > .003f || maxBodyAngle > .1f) throw new Exception("Unexpected body pose change");
            return result;
        }
    }
    static Mesh FrozenMesh(SkinnedMeshRenderer renderer)
    {
        // CPU skin all influences so previews do not depend on rendering quality's bone limit.
        var source = renderer.sharedMesh; var mesh = UnityEngine.Object.Instantiate(source);
        var positions = source.vertices; var normals = source.normals; var tangents = source.tangents;
        var dp = new Vector3[positions.Length]; var dn = new Vector3[positions.Length]; var dt = new Vector3[positions.Length];
        for (int shape = 0; shape < source.blendShapeCount; shape++)
        {
            float weight = renderer.GetBlendShapeWeight(shape) * .01f; if (weight == 0) continue;
            source.GetBlendShapeFrameVertices(shape, 0, dp, dn, dt);
            for (int i = 0; i < positions.Length; i++) { positions[i] += dp[i] * weight; normals[i] += dn[i] * weight; if (tangents.Length > 0) tangents[i] += new Vector4(dt[i].x, dt[i].y, dt[i].z, 0) * weight; }
        }
        var matrices = renderer.bones.Select((b, i) => b.localToWorldMatrix * source.bindposes[i]).ToArray();
        var normalMatrices = matrices.Select(m => m.inverse.transpose).ToArray();
        var output = new Vector3[positions.Length]; var outputNormals = new Vector3[positions.Length]; var outputTangents = new Vector4[positions.Length];
        using (var counts = source.GetBonesPerVertex()) using (var weights = source.GetAllBoneWeights())
        {
            int at = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 tangent = Vector3.zero;
                for (int k = 0; k < counts[i]; k++)
                {
                    var w = weights[at++]; output[i] += matrices[w.boneIndex].MultiplyPoint3x4(positions[i]) * w.weight;
                    outputNormals[i] += normalMatrices[w.boneIndex].MultiplyVector(normals[i]) * w.weight;
                    if (tangents.Length > 0) tangent += matrices[w.boneIndex].MultiplyVector(tangents[i]) * w.weight;
                }
                outputNormals[i].Normalize(); tangent.Normalize();
                if (tangents.Length > 0) outputTangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
            }
        }
        mesh.ClearBlendShapes(); mesh.vertices = output; mesh.normals = outputNormals;
        if (tangents.Length > 0) mesh.tangents = outputTangents;
        mesh.RecalculateBounds(); return mesh;
    }
    public static object Render()
    {
        Guard(); Directory.CreateDirectory(Report + "/Previews"); var scene = EditorSceneManager.NewPreviewScene(); int count = 0;
        try
        {
            var cameraObject = new GameObject("Confident idle comparison camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.cameraType = CameraType.Preview;
            camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .14f, .17f);
            camera.orthographic = true; camera.orthographicSize = .28f; camera.nearClipPlane = .01f; camera.farClipPlane = 10;
            foreach (var data in new[] { (rotation: new Vector3(20, 155, 0), intensity: 1.2f), (rotation: new Vector3(-10, 215, 0), intensity: .65f) })
            {
                var lightObject = new GameObject("Review light"); SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = data.intensity;
                light.cullingMask = 1 << 30; lightObject.transform.rotation = Quaternion.Euler(data.rotation);
            }
            foreach (bool after in new[] { false, true })
            {
                var clip = after ? AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) : Source().Clip;
                var expression = AssetDatabase.LoadAssetAtPath<ExpressionDefinition>(after ? ExpressionPath : "Assets/_Sandbox/Data/Expression/Confident_idle.asset");
                using (var sampler = new Sampler(clip))
                {
                    foreach (float phase in new[] { 0f, .25f, .5f, .75f, 1f })
                    {
                        sampler.Sample(clip.length * phase);
                        var skins = sampler.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.enabled && r.sharedMesh).ToArray();
                        foreach (var renderer in skins)
                        {
                            for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++) renderer.SetBlendShapeWeight(i, 0);
                            foreach (var target in expression.Targets) { int index = renderer.sharedMesh.GetBlendShapeIndex(target.Shape); if (index >= 0) renderer.SetBlendShapeWeight(index, target.Weight); }
                        }
                        var frozen = new List<GameObject>(); var meshes = new List<Mesh>();
                        try
                        {
                            foreach (var renderer in skins)
                            {
                                var mesh = FrozenMesh(renderer); meshes.Add(mesh); var go = new GameObject("Frozen " + renderer.name);
                                SceneManager.MoveGameObjectToScene(go, scene); frozen.Add(go); go.layer = 30;
                                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                            }
                            foreach (string view in phase == .5f ? new[] { "Front", "ThreeQuarter" } : new[] { "Front" })
                            {
                                var center = new Vector3(0, 1.70f, -.5f);
                                camera.transform.position = center + Quaternion.Euler(0, view == "Front" ? 0 : 30, 0) * Vector3.forward * 3;
                                camera.transform.LookAt(center);
                                var renderTexture = RenderTexture.GetTemporary(640, 720, 24); var previous = RenderTexture.active;
                                try
                                {
                                    camera.targetTexture = renderTexture; camera.Render(); RenderTexture.active = renderTexture;
                                    var texture = new Texture2D(640, 720, TextureFormat.RGB24, false);
                                    try { texture.ReadPixels(new Rect(0, 0, 640, 720), 0, 0); texture.Apply(); File.WriteAllBytes(Report + "/Previews/" + view + "_" + phase.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "_" + (after ? "Confident" : "Original") + ".png", texture.EncodeToPNG()); count++; }
                                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                                }
                                finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(renderTexture); }
                            }
                        }
                        finally { foreach (var go in frozen) UnityEngine.Object.DestroyImmediate(go); foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh); }
                    }
                }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        var result = new { imageCount = count, staticEvaluatedMeshes = true, scalpSimulationNotRendered = true, liveSceneChanged = false };
        Save("previews", result); return result;
    }
}
