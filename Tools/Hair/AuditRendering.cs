var camera = UnityEngine.Camera.main;
var additional = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
var hair = UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude).First();
var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Sandbox/Characters/Ethan/Materials/Ethan_StrandHair.mat");
var importer = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter")).First(t => t != null);
var method = importer.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).First(m => m.Name == "GetShaderText" && m.GetParameters().Length == 4);
var args = new object[] { "Assets/_Sandbox/Shaders/EthanHair.shadergraph", null, null, null };
var source = (string)method.Invoke(null, args);
System.IO.Directory.CreateDirectory("Docs/HairRendering");
System.IO.File.WriteAllText("Docs/HairRendering/GeneratedBaseline.shader.txt", source);
var result = new {
    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
    playing = UnityEditor.EditorApplication.isPlaying,
    camera = camera.name,
    cameraPosition = camera.transform.position.ToString("F6"), cameraRotation = camera.transform.eulerAngles.ToString("F3"),
    fieldOfView = camera.fieldOfView, screen = new { UnityEngine.Screen.width, UnityEngine.Screen.height },
    quality = UnityEngine.QualitySettings.names[UnityEngine.QualitySettings.GetQualityLevel()],
    pipeline = UnityEditor.AssetDatabase.GetAssetPath(pipeline), msaa = pipeline.msaaSampleCount, renderScale = pipeline.renderScale,
    renderer = additional.scriptableRenderer.GetType().FullName,
    aa = additional.antialiasing.ToString(), taa = UnityEngine.JsonUtility.ToJson(additional.taaSettings),
    hair = hair.name, providers = hair.strandGroupProviders.Select(p => UnityEditor.AssetDatabase.GetAssetPath(p.hairAsset)).ToArray(),
    groupDefaults = UnityEngine.JsonUtility.ToJson(hair.strandGroupDefaults), groups = hair.strandGroupSettings.Select(g => UnityEngine.JsonUtility.ToJson(g)).ToArray(),
    shader = material.shader.name, shaderErrors = UnityEditor.ShaderUtil.GetShaderMessages(material.shader),
    animators = UnityEngine.Object.FindObjectsByType<UnityEngine.Animator>(UnityEngine.FindObjectsInactive.Exclude).Select(a => new { a.name, controller = a.runtimeAnimatorController?.name, clips = a.runtimeAnimatorController?.animationClips.Select(c => new { c.name, c.length }).ToArray() }).ToArray(),
    gpu = UnityEngine.SystemInfo.graphicsDeviceName, api = UnityEngine.SystemInfo.graphicsDeviceType.ToString(),
    frameTiming = UnityEngine.FrameTimingManager.IsFeatureEnabled()
};
System.IO.File.WriteAllText("Docs/HairRendering/BaselineSettings.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
return result;
