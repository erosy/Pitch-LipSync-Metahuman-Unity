using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Sandbox.CharacterSystem;
using Newtonsoft.Json;

public static class CreateEyeOnlyCameraGaze
{
    const string Prefab = "Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Candidate.prefab";
    const string Output = "Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Face_EyeOnlyCameraGaze.asset";
    const string Report = "Data/EthanV2/EyeOnlyCameraGaze";
    static readonly string[] Sources = { "eyeLookDownLeft", "eyeLookDownRight" };
    static readonly string[] Destinations = { "cameraEyeLookDownLeft", "cameraEyeLookDownRight" };
    static bool Nonzero(Vector3 v) => v.x != 0 || v.y != 0 || v.z != 0;
    static object Statistics(Mesh mesh, SkinnedMeshRenderer renderer)
    {
        var facialHair = Enumerable.Range(0, mesh.subMeshCount).Where(i => renderer.sharedMaterials[i]
            && (renderer.sharedMaterials[i].name.Contains("Eyebrow") || renderer.sharedMaterials[i].name.Contains("Eyelash"))).ToArray();
        long payload = 0;
        var p = new Vector3[mesh.vertexCount]; var n = new Vector3[p.Length]; var t = new Vector3[p.Length];
        for (int shape = 0; shape < mesh.blendShapeCount; shape++) for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
        {
            mesh.GetBlendShapeFrameVertices(shape, frame, p, n, t);
            for (int i = 0; i < p.Length; i++) if (Nonzero(p[i]) || Nonzero(n[i]) || Nonzero(t[i])) payload += 40;
        }
        return new { mesh.vertexCount, triangles = mesh.triangles.Length / 3, shapes = mesh.blendShapeCount,
            facialHairVertices = facialHair.SelectMany(mesh.GetTriangles).Distinct().Count(),
            facialHairTriangles = facialHair.Sum(i => mesh.GetTriangles(i).Length / 3),
            estimatedBlendshapePayloadMiB = payload / 1048576.0,
            runtimeMeshMiB = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh) / 1048576.0 };
    }
    public static object Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit Mode required; user controls Play Mode.");
        if (File.Exists(Output)) throw new Exception("Refusing to overwrite existing derivative");
        var character = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsInactive.Include).Single(c => c.name == "EthanV2_Candidate");
        var module = character.GetComponentInChildren<ExpressionModule>(true); var face = module.Face; var source = face.sharedMesh;
        var eyeSubmeshes = Enumerable.Range(0, source.subMeshCount).Where(i => face.sharedMaterials[i] && face.sharedMaterials[i].name == "Ethan_Eyes").ToArray();
        if (eyeSubmeshes.Length != 2) throw new Exception("Expected exactly two independent eyeball submeshes");
        var eyeVertices = new HashSet<int>(eyeSubmeshes.SelectMany(source.GetTriangles));
        var nonEyeVertices = new HashSet<int>(Enumerable.Range(0, source.subMeshCount).Except(eyeSubmeshes).SelectMany(source.GetTriangles));
        if (eyeVertices.Overlaps(nonEyeVertices)) throw new Exception("Eyeball vertices overlap skin or eyelid geometry");
        var before = Statistics(source, face);
        var derivative = UnityEngine.Object.Instantiate(source); derivative.name = "EthanV2 Face - Eye Only Camera Gaze"; derivative.hideFlags = HideFlags.None;
        var rows = new List<object>();
        foreach (int side in new[] { 0, 1 })
        {
            int shape = source.GetBlendShapeIndex(Sources[side]); if (shape < 0 || source.GetBlendShapeFrameCount(shape) != 1) throw new Exception("Expected single-frame source gaze");
            var p = new Vector3[source.vertexCount]; var n = new Vector3[p.Length]; var t = new Vector3[p.Length];
            source.GetBlendShapeFrameVertices(shape, 0, p, n, t);
            float removedPositionMax = 0; int retained = 0, removed = 0;
            for (int i = 0; i < p.Length; i++)
            {
                if (eyeVertices.Contains(i)) { if (Nonzero(p[i])) retained++; }
                else { if (Nonzero(p[i])) removed++; removedPositionMax = Mathf.Max(removedPositionMax, p[i].magnitude); p[i] = n[i] = t[i] = Vector3.zero; }
            }
            if (retained == 0 || removed == 0) throw new Exception("Source shape does not contain the expected eye and lid motion");
            derivative.AddBlendShapeFrame(Destinations[side], source.GetBlendShapeFrameWeight(shape, 0), p, n, t);
            rows.Add(new { source = Sources[side], destination = Destinations[side], retainedEyeballVertices = retained,
                removedNonEyeVertices = removed, largestRemovedPositionDeltaMm = removedPositionMax * 1000 });
        }
        Directory.CreateDirectory(Report); AssetDatabase.CreateAsset(derivative, Output); AssetDatabase.SaveAssetIfDirty(derivative);
        var prefab = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var prefabModule = prefab.GetComponentInChildren<ExpressionModule>(true);
            if (prefabModule.Face.sharedMesh != source) throw new Exception("Scene and prefab face meshes differ; refuse implicit replacement");
            prefabModule.Face.sharedMesh = derivative;
            var data = new SerializedObject(prefabModule); data.FindProperty("speechGaze.eyeOnlyDownwardGaze").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        Undo.RecordObject(face, "Use eye-only camera gaze mesh"); face.sharedMesh = derivative;
        var moduleData = new SerializedObject(module); moduleData.FindProperty("speechGaze.eyeOnlyDownwardGaze").boolValue = true; moduleData.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(face); PrefabUtility.RecordPrefabInstancePropertyModifications(module);
        EditorSceneManager.MarkSceneDirty(character.gameObject.scene); EditorSceneManager.SaveScene(character.gameObject.scene);
        var result = new { source = AssetDatabase.GetAssetPath(source), derivative = Output, eyeVertexCount = eyeVertices.Count,
            aliases = rows, before, after = Statistics(derivative, face), preservedOriginalExpressions = source.blendShapeCount,
            geometryUnchanged = true, hairSimulationChanged = false, assignedToCandidateAndScene = true };
        File.WriteAllText(Report + "/build.json", JsonConvert.SerializeObject(result, Formatting.Indented)); return result;
    }
}
