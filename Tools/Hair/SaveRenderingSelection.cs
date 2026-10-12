if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new System.Exception("Stop Play Mode first.");
var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Sandbox/Characters/Ethan/Materials/Ethan_StrandHair.mat");
UnityEditor.Undo.RecordObject(material, "Smooth Ethan hair highlights");
material.SetFloat("_HairWidthScale", 1f); material.SetFloat("_HairSmoothness", .2f);
UnityEditor.EditorUtility.SetDirty(material);
UnityEditor.AssetDatabase.SaveAssets();
var camera = UnityEngine.Camera.main;
var additional = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
var pipeline = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
if (additional.antialiasing != UnityEngine.Rendering.Universal.AntialiasingMode.TemporalAntiAliasing || additional.taaSettings.quality != UnityEngine.Rendering.Universal.TemporalAAQuality.High || pipeline.msaaSampleCount != 1 || pipeline.renderScale != 1)
    throw new System.Exception("Comparison did not restore baseline camera/pipeline settings.");
var hair = UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude).First();
if (hair.strandGroupDefaults.settingsGeometry.stagingSubdivision || hair.strandGroupProviders[0].hairAsset.strandGroups[0].strandParticleCount != 32)
    throw new System.Exception("Hair geometry differs from the intended selection.");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
// Preserve the Editor's current scene edits before the required reopen verification.
if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new System.Exception("Scene could not be saved.");
var path = scene.path;
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Sandbox/Characters/Ethan/Materials/Ethan_StrandHair.mat");
hair = UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude).First();
var result = new { sceneReopened = path, hairWidthScale = material.GetFloat("_HairWidthScale"), hairSmoothness = material.GetFloat("_HairSmoothness"), renderScale = pipeline.renderScale, msaa = pipeline.msaaSampleCount,
    physics = UnityEngine.JsonUtility.ToJson(hair.strandGroupDefaults.settingsPhysics), executive = UnityEngine.JsonUtility.ToJson(hair.settingsExecutive),
    subdivision = hair.strandGroupDefaults.settingsGeometry.stagingSubdivision, strands = hair.strandGroupProviders[0].hairAsset.strandGroups[0].strandCount,
    simulationPoints = hair.strandGroupProviders[0].hairAsset.strandGroups[0].strandParticleCount,
    shaderMessages = UnityEditor.ShaderUtil.GetShaderMessages(material.shader) };
System.IO.File.WriteAllText("Docs/HairRendering/SavedSelection.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
return result;
