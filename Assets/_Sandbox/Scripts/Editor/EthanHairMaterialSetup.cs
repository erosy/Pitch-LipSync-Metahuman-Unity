using System;
using Unity.DemoTeam.Hair;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Creates the material through Unity APIs and preserves the user's prefab/scene edits.
[InitializeOnLoad]
public static class EthanHairMaterialSetup
{
    const string ShaderPath = "Assets/_Sandbox/Shaders/EthanHair.shadergraph";
    const string MaterialPath = "Assets/_Sandbox/Characters/Ethan/Materials/Ethan_StrandHair.mat";
    const string PrefabPath = "Assets/_Sandbox/Characters/Ethan/Ethan.prefab";
    const string AttemptKey = "EthanHairMaterialSetup.Attempted";

    static EthanHairMaterialSetup()
    {
        EditorApplication.update += InstallWhenReady;
    }

    static void InstallWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        EditorApplication.update -= InstallWhenReady;
        if (SessionState.GetBool(AttemptKey, false) ||
            AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
            return;
        SessionState.SetBool(AttemptKey, true);
        ApplyMaterial();
    }

    [MenuItem("Tools/Hair/Apply Ethan Hair Material")]
    public static void ApplyMaterial()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before applying Ethan's persistent hair material.");
            return;
        }
        try
        {
            AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                throw new InvalidOperationException("EthanHair shader has not imported. Check shader errors, then run Tools/Hair/Apply Ethan Hair Material.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Ethan_StrandHair" };
                if (!material.HasProperty("_HairColor"))
                    throw new InvalidOperationException("EthanHair shader is missing its exposed Hair Color property.");
                ColorUtility.TryParseHtmlString("#39271D", out var brown);
                material.SetColor("_HairColor", brown);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var hair = prefabRoot.GetComponentsInChildren<HairInstance>(true);
                if (hair.Length == 0)
                    throw new InvalidOperationException("Ethan prefab contains no Hair Instance.");
                foreach (var instance in hair)
                    Assign(instance, material);
                if (PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save Ethan's hair material assignment.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            // Update open Ethan instances too, including Prefab Mode, without saving scenes.
            foreach (var instance in Resources.FindObjectsOfTypeAll<HairInstance>())
            {
                if (EditorUtility.IsPersistent(instance) || !instance.gameObject.scene.IsValid())
                    continue;
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance.gameObject) != PrefabPath &&
                    PrefabStageUtility.GetPrefabStage(instance.gameObject)?.assetPath != PrefabPath)
                    continue;
                Undo.RecordObject(instance, "Assign Ethan hair material");
                Assign(instance, material);
                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Ethan hair material applied. Change Hair Color on Ethan_StrandHair.mat.", material);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
        }
    }

    static void Assign(HairInstance instance, Material material)
    {
        instance.strandGroupDefaults.settingsRendering.material = true;
        instance.strandGroupDefaults.settingsRendering.materialAsset = material;
        // Existing group overrides continue to govern geometry and physics.
        if (instance.strandGroupSettings != null)
            for (int i = 0; i < instance.strandGroupSettings.Length; i++)
                if (instance.strandGroupSettings[i].settingsRenderingToggle)
                {
                    instance.strandGroupSettings[i].settingsRendering.material = true;
                    instance.strandGroupSettings[i].settingsRendering.materialAsset = material;
                }
        EditorUtility.SetDirty(instance);
    }
}
