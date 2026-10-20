using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JiwooMaterialAssignment
{
    private const string PrefabPath = "Assets/_Sandbox/Characters/Jiwoo/Jiwoo_Normalized.prefab";
    private const string MaterialFolder = "Assets/_Sandbox/Characters/Jiwoo/Materials/";
    private static readonly string[] Names = {
        "Jiwoo_Head", "Jiwoo_Teeth", "Jiwoo_Saliva", "Jiwoo_Eyes", "Jiwoo_Eyes",
        "Jiwoo_EyeShell", "Jiwoo_Eyelashes", "Jiwoo_EyeEdge", "Jiwoo_Cartilage", "Jiwoo_Body"
    };

    public static object Inspect()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        return new
        {
            playing = EditorApplication.isPlaying,
            stage = PrefabStageUtility.GetCurrentPrefabStage()?.assetPath,
            materials = Names.Distinct().Select(name => new {
                name, path = AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat"))
            }).ToArray(),
            prefabSlots = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Select(r => new { r.name, slots = r.sharedMaterials.Select(m => m == null ? "null" : m.name).ToArray(), submeshes = r.sharedMesh.subMeshCount }).ToArray(),
            live = Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>()
                .Where(r => r.gameObject.scene.IsValid() && r.name == "Face")
                .Select(r => new { root = r.transform.root.name, scene = r.gameObject.scene.path,
                    prefabSource = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject),
                    slots = r.sharedMaterials.Select(m => m == null ? "null" : m.name).ToArray() }).ToArray()
        };
    }

    public static object Assign()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Leave Play Mode under user control; assignment requires Edit Mode.");
        var materials = Names.Select(n => AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + n + ".mat")).ToArray();
        if (materials.Any(m => m == null))
            throw new InvalidOperationException("A required Jiwoo material is missing.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var useStage = stage != null && stage.assetPath == PrefabPath;
        var root = useStage ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var face = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Face");
            if (face.sharedMesh.subMeshCount != Names.Length)
                throw new InvalidOperationException("Expected the verified ten material slots.");
            Undo.RecordObject(face, "Assign Jiwoo materials");
            face.sharedMaterials = materials;
            if (PrefabUtility.IsPartOfPrefabInstance(face))
                PrefabUtility.RecordPrefabInstancePropertyModifications(face);
            EditorUtility.SetDirty(face);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out var success);
            if (!success)
                throw new InvalidOperationException("Failed to save Jiwoo prefab material assignments.");
        }
        finally
        {
            if (!useStage)
                PrefabUtility.UnloadPrefabContents(root);
        }
        // Refresh only live instances of this exact prefab that still override
        // one or more slots with null/default Lit materials. Preserve other overrides.
        var updated = 0;
        foreach (var renderer in Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>())
        {
            if (!renderer.gameObject.scene.IsValid() || renderer.name != "Face") continue;
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject) != PrefabPath) continue;
            if (useStage && renderer.gameObject.scene == stage.scene) continue;
            var current = renderer.sharedMaterials;
            if (current.SequenceEqual(materials)) continue;
            if (current.Length != Names.Length || current.Any(m => m != null && m.name != "Lit" && m.name != "Default-Material" && !Names.Contains(m.name))) continue;
            Undo.RecordObject(renderer, "Assign Jiwoo instance materials");
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
            updated++;
        }
        SceneView.RepaintAll();
        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
            .GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Face");
        if (!saved.sharedMaterials.SequenceEqual(materials))
            throw new InvalidOperationException("Saved material references failed verification.");
        return new { verified = true, prefab = PrefabPath, materialSlots = Names, liveOverridesUpdated = updated, usedOpenPrefabStage = useStage };
    }
}
