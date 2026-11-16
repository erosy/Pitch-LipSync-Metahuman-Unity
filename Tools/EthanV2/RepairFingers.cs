using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sandbox.CharacterSystem;
using Newtonsoft.Json;

public static class RepairV2Fingers
{
    const string Prefab = "Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab";
    const string Output = "Data/EthanV2/FingerRepair/";
    static readonly string[] Fingers = { "thumb", "index", "middle", "ring", "pinky" };
    static bool IsFinger(string name) => Fingers.Any(f => name.StartsWith(f + "_")) && (name.EndsWith("_l") || name.EndsWith("_r"));
    static bool IsJoint(string name) => IsFinger(name) && (name.Split('_').Length == 3);
    static string SourceName(string name)
    {
        var parts = name.Split('_');
        return (name.EndsWith("_l") ? "Left" : "Right") + char.ToUpper(parts[0][0]) + parts[0].Substring(1)
            + (parts[1] == "01" ? "Proximal" : parts[1] == "02" ? "Intermediate" : parts[1] == "03" ? "Distal" : "Metacarpal");
    }
    static Vector3[] Skin(SkinnedMeshRenderer renderer, Transform root)
    {
        var mesh = renderer.sharedMesh;
        var vertices = mesh.vertices;
        var matrices = renderer.bones.Select((b,i) => root.worldToLocalMatrix * b.localToWorldMatrix * mesh.bindposes[i]).ToArray();
        var result = new Vector3[vertices.Length];
        using (var counts = mesh.GetBonesPerVertex()) using (var weights = mesh.GetAllBoneWeights())
        {
            int at = 0;
            for (int i = 0; i < result.Length; i++)
                for (int j = 0; j < counts[i]; j++) { var w = weights[at++]; result[i] += matrices[w.boneIndex].MultiplyPoint3x4(vertices[i]) * w.weight; }
        }
        return result;
    }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    public static object VerifySaved()
    {
        Assert(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode required");
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var pairs=root.GetComponent<CharacterSkeletonPoseBridge>().Bindings.Where(p=>IsFinger(p.target.name)).ToArray();
            Assert(pairs.Length==210,"Saved finger binding count changed");
            foreach(var p in pairs)
            {
                Assert(p.source.name==SourceName(p.target.name),"Saved finger source mismatch");
                Assert(p.mode==(IsJoint(p.target.name)?CharacterSkeletonPoseBridge.TransferMode.RotationOnly:CharacterSkeletonPoseBridge.TransferMode.FollowParent),"Saved finger mode mismatch");
            }
            var rest=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
            Action reset=()=>{foreach(var p in rest){p.Key.localPosition=p.Value.Item1;p.Key.localRotation=p.Value.Item2;p.Key.localScale=p.Value.Item3;}};
            var definitions=AssetDatabase.FindAssets("t:AnimationDefinition",new[]{"Assets/_Sandbox"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AnimationDefinition>).Where(d=>d&&d.Clip&&d.Clip.isHumanMotion).ToArray();
            var samples=Sample(root,pairs,definitions,reset,"after");
            var instances=Resources.FindObjectsOfTypeAll<CharacterSkeletonPoseBridge>().Where(b=>b!=root.GetComponent<CharacterSkeletonPoseBridge>()&&b.gameObject.scene.IsValid()&&b.gameObject.scene.isLoaded&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(b.gameObject)==Prefab)
                .Select(b=>new {scene=b.gameObject.scene.name,b.name,repairedBindings=b.Bindings.Count(p=>IsFinger(p.target.name)&&p.mode!=CharacterSkeletonPoseBridge.TransferMode.WorldPose),dirty=b.gameObject.scene.isDirty}).ToArray();
            Assert(instances.All(b=>b.repairedBindings==210),"Scene instance has stale finger overrides");
            var report=new {passed=true,samples,instances,playModeEntered=false};
            File.WriteAllText(Output+"saved_verification.json",JsonConvert.SerializeObject(report,Formatting.Indented));
            return report;
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    public static object Run()
    {
        Assert(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode required");
        Directory.CreateDirectory(Output + "Before");
        string backup = Output + "Before/Ethan_V2.prefab";
        if (!File.Exists(backup)) File.Copy(Prefab, backup);
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var bridge = root.GetComponent<CharacterSkeletonPoseBridge>();
            var pairs = bridge.Bindings.Where(p => IsFinger(p.target.name)).ToArray();
            var allRest = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale));
            Action reset = () => { foreach (var p in allRest) { p.Key.localPosition = p.Value.Item1; p.Key.localRotation = p.Value.Item2; p.Key.localScale = p.Value.Item3; } };
            var body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "BAKED_body_lod0_mesh");
            var neutral = Skin(body, root.transform);
            var animator = root.GetComponentInChildren<AnimationModule>(true).Animator;
            var sourceBones = animator.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);
            var old = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan/Ethan.prefab");
            var oldBones = old.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            var changes = pairs.Select(p => new { target = p.target.name, before = p.source.name, after = SourceName(p.target.name), mode = IsJoint(p.target.name) ? "RotationOnly" : "FollowParent" }).ToArray();
            Assert(pairs.Count(p=>IsJoint(p.target.name) && !p.target.name.Contains("metacarpal")) == 30, "Expected 30 finger phalanges");
            foreach (var p in pairs.Where(p=>IsJoint(p.target.name)))
            {
                var source = sourceBones[SourceName(p.target.name)];
                Assert(Vector3.Distance(source.localPosition, oldBones[source.name].localPosition) < 1e-5f, "Driver joint differs from old Ethan: " + source.name);
                Assert(Quaternion.Angle(source.localRotation, oldBones[source.name].localRotation) < .01f, "Driver rotation differs from old Ethan: " + source.name);
            }
            // Measure the existing bridge on the same saved rest pose and clip samples.
            var definitions = AssetDatabase.FindAssets("t:AnimationDefinition", new[]{"Assets/_Sandbox"}).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AnimationDefinition>).Where(d=>d && d.Clip && d.Clip.isHumanMotion).ToArray();
            var before = Sample(root, pairs, definitions, reset, "before");
            reset();
            foreach (var p in pairs)
            {
                p.source = sourceBones[SourceName(p.target.name)];
                p.sourceBindInverse = (root.transform.worldToLocalMatrix * p.source.localToWorldMatrix).inverse;
                p.mode = IsJoint(p.target.name) ? CharacterSkeletonPoseBridge.TransferMode.RotationOnly : CharacterSkeletonPoseBridge.TransferMode.FollowParent;
                p.targetLocalBindPosition = p.target.localPosition;
                p.targetLocalBindRotation = p.target.localRotation;
            }
            bridge.ApplyPose();
            var repairedNeutral = Skin(body, root.transform);
            float neutralError = neutral.Zip(repairedNeutral, Vector3.Distance).Max();
            Assert(neutralError < .00001f, "Neutral body skin changed");
            var after = Sample(root, pairs, definitions, reset, "after");
            reset();
            // Curl each source joint separately. Other fingers must remain still,
            // while helpers keep their rest transforms relative to their own joints.
            float crossFingerError = 0, localPositionError = 0;
            int isolatedCurls = 0;
            foreach (var joint in pairs.Where(p=>IsJoint(p.target.name) && !p.target.name.Contains("metacarpal")))
            {
                reset(); bridge.ApplyPose();
                var unchanged = pairs.Where(p => p.target.name.Split('_')[0] != joint.target.name.Split('_')[0] || p.target.name.EndsWith("_l") != joint.target.name.EndsWith("_l")).ToDictionary(p=>p.target,p=>p.target.position);
                joint.source.localRotation *= Quaternion.AngleAxis(60,Vector3.right);
                bridge.ApplyPose();
                foreach (var p in unchanged) crossFingerError = Mathf.Max(crossFingerError,Vector3.Distance(p.Key.position,p.Value));
                foreach (var p in pairs) localPositionError = Mathf.Max(localPositionError, root.transform.InverseTransformVector(p.target.parent.TransformVector(p.target.localPosition-p.targetLocalBindPosition)).magnitude);
                isolatedCurls++;
            }
            Assert(crossFingerError < .00001f && localPositionError < .00001f, "Isolated finger motion failed");
            reset();
            EditorUtility.SetDirty(bridge);
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            var report = new { passed=true, bindings=pairs.Length, joints=pairs.Count(p=>IsJoint(p.target.name)), correctedSourceMappings=changes.Count(p=>p.before!=p.after), neutralSkinErrorM=neutralError,
                isolatedCurls, crossFingerErrorM=crossFingerError, localPositionErrorM=localPositionError, before, after, changes, playModeEntered=false, sourceFbxModified=false };
            File.WriteAllText(Output + "verification.json", JsonConvert.SerializeObject(report, Formatting.Indented));
            return new { report.passed, report.bindings, report.joints, report.correctedSourceMappings, report.neutralSkinErrorM, isolatedCurls, crossFingerError, localPositionError, before, after };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static object Sample(GameObject root, CharacterSkeletonPoseBridge.Binding[] pairs, AnimationDefinition[] definitions, Action reset, string label)
    {
        var bridge = root.GetComponent<CharacterSkeletonPoseBridge>();
        var animator = root.GetComponentInChildren<AnimationModule>(true).Animator;
        var rest = pairs.ToDictionary(p=>p.target,p=>(p.target.localPosition,p.target.localRotation));
        float maxLocalDisplacement=0, maxHelperRotation=0, maxRotationError=0;
        int frames=0;
        foreach (var definition in definitions)
        {
            reset();
            var graph = PlayableGraph.Create("Disposable finger rig verification");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, definition.Clip); playable.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph,"finger sample",animator).SetSourcePlayable(playable); graph.Play();
                for (int i=0;i<9;i++)
                {
                    playable.SetTime(definition.Clip.length*i/8.0); graph.Evaluate(0); bridge.ApplyPose();
                    foreach (var p in pairs)
                    {
                        float distance=root.transform.InverseTransformVector(p.target.parent.TransformVector(p.target.localPosition-rest[p.target].Item1)).magnitude;
                        maxLocalDisplacement=Mathf.Max(maxLocalDisplacement,distance);
                        if (!IsJoint(p.target.name)) maxHelperRotation=Mathf.Max(maxHelperRotation,Quaternion.Angle(rest[p.target].Item2,p.target.localRotation));
                        else maxRotationError=Mathf.Max(maxRotationError,Quaternion.Angle(p.target.rotation,(p.source.localToWorldMatrix*p.sourceBindInverse*p.targetBind).rotation));
                    }
                    frames++;
                }
            }
            finally { graph.Destroy(); }
        }
        if (label=="after") { Assert(maxLocalDisplacement<.00001f,"Finger spacing changed"); Assert(maxHelperRotation<.1f,"Corrective detached from parent"); Assert(maxRotationError<.15f,"Finger rotation differs from old Ethan transfer"); }
        return new { clips=definitions.Select(d=>d.name).ToArray(), frames, maxLocalDisplacementM=maxLocalDisplacement, maxHelperLocalRotationDegrees=maxHelperRotation, maxJointRotationErrorDegrees=maxRotationError };
    }
}
