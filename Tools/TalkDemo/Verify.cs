using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.DemoTeam.Hair;
using Sandbox.CharacterSystem;

public static class VerifyTalkDemo
{
    public static object Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Verification is Edit Mode only.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Sandbox/Scenes/Talk Demo.unity") throw new InvalidOperationException("Open Talk Demo.");
        var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TalkDemoController>(true)).Single();
        var errors = controller.ValidateConfiguration();
        var input = controller.GetComponent<TalkDemoSpeechInput>();
        if (!input) errors.Add("Missing speech input.");
        else errors.AddRange(input.ValidateConfiguration());
        var data = new SerializedObject(controller);
        var camera = (Camera)data.FindProperty("characterCamera").objectReferenceValue;
        var root = controller.gameObject;
        var characters = root.GetComponentsInChildren<Character>(true);
        foreach(var character in characters)
        {
            var fallback=character.GetComponent<CharacterHairFallback>();
            if(!fallback) { errors.Add(character.name+": missing mesh hair fallback."); continue; }
            fallback.ValidateConfiguration(errors);
            var readiness=new SerializedObject(character.GetComponent<CharacterReadiness>());
            if(readiness.FindProperty("hairFallback").objectReferenceValue!=fallback)errors.Add("Readiness fallback reference mismatch.");
            var replacement=character.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Scalp Mesh Fallback");
            if(replacement.transform.parent.name!="head" || replacement.gameObject.activeSelf)errors.Add("Fallback must be head-bound and initially hidden.");
            var face=character.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Face");
            var source=PrefabUtility.GetCorrespondingObjectFromSource(face);
            if(face.sharedMesh!=source.sharedMesh || !face.sharedMaterials.SequenceEqual(source.sharedMaterials))errors.Add("Facial mesh or materials changed.");
        }
        if (characters.Length != 2 || characters.Count(c => c.gameObject.activeSelf) != 1)
            errors.Add("Exactly two candidates with one active are required.");
        if (characters.Any(c => c.GetComponentsInChildren<AudioSource>(true).Any(a => a.playOnAwake || a.isPlaying)))
            errors.Add("Audio must not start automatically.");
        if (characters.Any(c => c.GetComponentsInChildren<uLipSync.uLipSyncMicrophone>(true).Any(m => m.enabled)))
            errors.Add("Demo microphone capture must be disabled.");
        var hairReport = root.GetComponentsInChildren<HairInstance>(true).Select(h => {
            var points = h.strandGroupProviders.SelectMany(p => p.hairAsset.strandGroups).Select(g => g.strandParticleCount).ToArray();
            if (points.Any(p => p != 32) || h.settingsExecutive.updateSimulationRate != HairInstance.SettingsExecutive.UpdateRate.Fixed30Hz
                || h.settingsExecutive.updateSimulationInEditor)
                errors.Add(h.name + ": hair production defaults do not match.");
            return new { h.name, points, rate = h.settingsExecutive.updateSimulationRate.ToString(), editSimulation = h.settingsExecutive.updateSimulationInEditor };
        }).ToArray();
        var bindings = data.FindProperty("personas");
        var framing = Enumerable.Range(0, bindings.arraySize).Select(i => {
            var binding = bindings.GetArrayElementAtIndex(i);
            var character = (Character)binding.FindPropertyRelative("character").objectReferenceValue;
            var p = binding.FindPropertyRelative("cameraPosition").vector3Value;
            var t = binding.FindPropertyRelative("cameraTarget").vector3Value;
            var head = character.GetComponentsInChildren<Transform>(true).First(b => b.name == "head");
            var verticalHalfHeight = Vector3.Distance(p, t) * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            return new {
                key = binding.FindPropertyRelative("key").stringValue,
                prefab = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(character.gameObject)),
                cameraPosition = p.ToString(), target = t.ToString(), headY = head.position.y,
                approximateBottom = t.y - verticalHalfHeight, approximateTop = t.y + verticalHalfHeight
            };
        }).ToArray();
        if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
        return new { errors, scene = scene.path, scene.isDirty, playing = EditorApplication.isPlaying, framing, hairReport };
    }
}
