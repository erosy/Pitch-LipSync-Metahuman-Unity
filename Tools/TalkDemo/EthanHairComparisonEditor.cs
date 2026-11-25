using System;
using System.IO;
using System.Linq;
using Sandbox.CharacterSystem;
using Unity.DemoTeam.Hair;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Run from the project root with Unity Pipeline's run_script command.
public static class EthanHairComparisonEditor
{
    const string Folder = "Assets/_Sandbox/EthanHairComparison/";
    const string ScenePath = Folder + "Ethan Hair Comparison.unity";
    const string OriginalScene = "Assets/_Sandbox/Scenes/Talk Demo.unity";
    const string Output = "Docs/EthanHairComparison/";

    public static object Generate()
    {
        RequireEditMode();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string atlasPath = Folder + "EthanGroomAtlas.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = 0.30f;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        var shell = MakeMaterial(atlas, false);
        var outer = MakeMaterial(atlas, true);
        var mesh = BuildMesh();

        if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(OriginalScene, ScenePath))
            throw new InvalidOperationException("Could not copy Talk Demo.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForGeneration = !scene.isLoaded;
        if (openedForGeneration) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        else if (scene.isDirty) throw new InvalidOperationException("Save or close the comparison scene before regenerating it.");
        try
        {
            var demo = Find<TalkDemoController>(scene);
            var personas = new SerializedObject(demo).FindProperty("personas");
            var ethan = (Character)personas.GetArrayElementAtIndex(0).FindPropertyRelative("character").objectReferenceValue;
            if (personas.GetArrayElementAtIndex(0).FindPropertyRelative("key").stringValue != "Ethan Reeve")
                throw new InvalidOperationException("Unexpected Talk Demo persona order.");
            var fallback = ethan.GetComponent<CharacterHairFallback>();
            var ribbons = ((GameObject)new SerializedObject(fallback).FindProperty("fallbackRoot")
                .objectReferenceValue).GetComponent<MeshRenderer>();
            var scalpProxy = ribbons.transform.parent;
            var bone = scalpProxy.GetComponentsInParent<Transform>(true).First(t => t.name == "head");
            var existing = ribbons.transform.Find("Groom-baked hair");
            var candidate = existing ? existing.gameObject : new GameObject("Groom-baked hair");
            if (!existing) candidate.transform.SetParent(ribbons.transform, false);
            if (!candidate.TryGetComponent(out MeshFilter meshFilter)) meshFilter = candidate.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            if (!candidate.TryGetComponent(out MeshRenderer renderer)) renderer = candidate.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { shell, outer };
            renderer.shadowCastingMode = ribbons.shadowCastingMode;
            renderer.receiveShadows = ribbons.receiveShadows;
            renderer.lightProbeUsage = ribbons.lightProbeUsage;
            renderer.reflectionProbeUsage = ribbons.reflectionProbeUsage;
            renderer.renderingLayerMask = ribbons.renderingLayerMask;
            if (!demo.TryGetComponent(out EthanHairComparison comparison))
                comparison = demo.gameObject.AddComponent<EthanHairComparison>();
            var settings = new SerializedObject(comparison);
            settings.FindProperty("fallback").objectReferenceValue = fallback;
            settings.FindProperty("ribbons").objectReferenceValue = ribbons;
            settings.FindProperty("cards").objectReferenceValue = renderer;
            settings.FindProperty("head").objectReferenceValue = bone;
            settings.FindProperty("demo").objectReferenceValue = demo;
            settings.FindProperty("speech").objectReferenceValue = demo.GetComponent<TalkDemoSpeechInput>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            comparison.ApplyMode(EthanHairComparison.HairMode.Cards);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Prototype scene save failed.");
        }
        finally
        {
            if (openedForGeneration) EditorSceneManager.CloseScene(scene, true);
        }

        return new { scene = ScenePath, mesh.vertexCount, triangles = mesh.GetIndexCount(0) / 3 + mesh.GetIndexCount(1) / 3 };
    }

    public static object Review()
    {
        RequireEditMode();
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        RenderPipelineAsset previousPipeline = QualitySettings.renderPipeline;
        var target = RenderTexture.GetTemporary(360, 520, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = RenderTexture.active;
        var texture = new Texture2D(360, 520, TextureFormat.RGB24, false);
        try
        {
            var comparison = Find<EthanHairComparison>(scene);
            var values = new SerializedObject(comparison);
            var baseline = (MeshRenderer)values.FindProperty("ribbons").objectReferenceValue;
            var cards = (MeshRenderer)values.FindProperty("cards").objectReferenceValue;
            var head = (Transform)values.FindProperty("head").objectReferenceValue;
            var fallback = (CharacterHairFallback)values.FindProperty("fallback").objectReferenceValue;
            var demo = Find<TalkDemoController>(scene);
            var demoSettings = new SerializedObject(demo);
            var personas = demoSettings.FindProperty("personas");
            var ethan = personas.GetArrayElementAtIndex(0);
            ((Character)personas.GetArrayElementAtIndex(1).FindPropertyRelative("character").objectReferenceValue)
                .gameObject.SetActive(false);
            fallback.gameObject.SetActive(true);
            var groom = fallback.GetComponentsInChildren<HairInstance>(true).Single(h => h.name == "EthanV2_ScalpHair");
            var provider = groom.strandGroupProviders.Single().hairAsset;
            var sourceGroup = provider.strandGroups.Single();
            float sourceError = VerifySourceCurves(sourceGroup);
            groom.settingsExecutive.updateSimulationInEditor = false;
            foreach (var instance in fallback.GetComponentsInChildren<HairInstance>(true)) instance.gameObject.SetActive(false);
            baseline.gameObject.SetActive(true);
            var camera = (Camera)demoSettings.FindProperty("characterCamera").objectReferenceValue;
            var position = ethan.FindPropertyRelative("cameraPosition").vector3Value;
            var aim = ethan.FindPropertyRelative("cameraTarget").vector3Value;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(aim - position, Vector3.up));
            camera.scene = scene;
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // A single Edit Mode render cannot build independent temporal histories for every pose.
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            QualitySettings.renderPipeline = (RenderPipelineAsset)new SerializedObject(Find<TalkDemoWebBridge>(scene))
                .FindProperty("transparentPipeline").objectReferenceValue;

            var mesh = cards.GetComponent<MeshFilter>().sharedMesh;
            var atlas = (Texture2D)cards.sharedMaterials[1].GetTexture("_BaseMap");
            var errors = demo.ValidateConfiguration();
            fallback.ValidateConfiguration(errors);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            if (mesh.subMeshCount != 2 || mesh.GetIndexCount(0) + mesh.GetIndexCount(1) > 60000
                || atlas.width != 2048 || atlas.height != 2048 || atlas.mipmapCount <= 1
                || cards.sharedMaterials[1].GetFloat("_AlphaClip") != 1
                || cards.sharedMaterials.Any(m => m.GetFloat("_SpecularHighlights") != 0)
                || cards.transform.parent != baseline.transform)
                throw new InvalidOperationException("Prototype asset or attachment validation failed.");

            var hidden = new System.Collections.Generic.Dictionary<string, byte[]>();
            Directory.CreateDirectory("Temp/EthanHairComparison");
            foreach (var mode in Enum.GetValues(typeof(EthanHairComparison.HairMode)).Cast<EthanHairComparison.HairMode>())
            {
                comparison.ApplyMode(mode);
                if (baseline.enabled != (mode == EthanHairComparison.HairMode.Ribbons)
                    || cards.enabled != (mode == EthanHairComparison.HairMode.Cards))
                    throw new InvalidOperationException("Hair modes overlap.");
                if (mode == EthanHairComparison.HairMode.Hidden) continue;
                Capture(mode.ToString(), false);
            }
            comparison.ApplyMode(EthanHairComparison.HairMode.Hidden);
            Capture("Hidden", false);
            // Talk Demo deactivates the Hair container when using the web fallback.
            // A standalone camera request needs the groom's direct strand renderer.
            groom.strandGroupDefaults.settingsRendering.allowIndirect = false;
            groom.strandGroupDefaults.settingsRendering.kLODSelection = HairSim.RenderLODSelection.Manual;
            groom.strandGroupDefaults.settingsRendering.kLODSelectionValue = 1f;
            groom.strandGroupDefaults.settingsRendering.clipThreshold = 0f;
            groom.transform.parent.gameObject.SetActive(true);
            groom.gameObject.SetActive(true);
            Capture("Groom", true);

            void Capture(string label, bool useGroom)
            {
                foreach (int angle in new[] { 0, 45, 85 })
                foreach (bool light in new[] { false, true })
                {
                    var turn = Quaternion.AngleAxis(angle, Vector3.up);
                    Vector3 pivot = new Vector3(head.position.x, aim.y, head.position.z);
                    camera.transform.SetPositionAndRotation(pivot + turn * (position - pivot),
                        turn * Quaternion.LookRotation(aim - position, Vector3.up));
                    camera.backgroundColor = light ? new Color(.76f, .78f, .81f) : new Color(.08f, .09f, .115f);
                    if (useGroom)
                    {
                        using var commands = new CommandBuffer();
                        if (!groom.DispatchUpdate(commands, CommandBufferExecutionFlags.None, 0f))
                            throw new InvalidOperationException("Could not initialize the groom for the reference capture.");
                        Graphics.ExecuteCommandBuffer(commands);
                    }
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 360, 520), 0, 0);
                    texture.Apply();
                    string suffix = $"{angle}-{(light ? "light" : "dark")}.png";
                    var png = texture.EncodeToPNG();
                    if (label == "Hidden")
                    {
                        hidden.Add(suffix, png);
                        if (light) File.WriteAllBytes($"Temp/EthanHairComparison/{label}-{suffix}", png);
                    }
                    else
                    {
                        if (useGroom && png.SequenceEqual(hidden[suffix]))
                        {
                            var group = groom.strandGroupInstances[0];
                            var renderer = group.sceneObjects.strandMeshRenderer;
                            throw new InvalidOperationException($"The live groom did not render for {suffix}. " +
                                $"Renderer active: {renderer.enabled && renderer.gameObject.activeInHierarchy}, bounds: {renderer.bounds}, " +
                                $"material: {renderer.sharedMaterial?.name}, vertices: {group.sceneObjects.strandMeshFilter.sharedMesh?.vertexCount}");
                        }
                        File.WriteAllBytes($"{Output}{label}-{suffix}", png);
                    }
                }
            }
            var result = new {
                groomObject = groom.name, groomAsset = AssetDatabase.GetAssetPath(provider),
                groomStrands = sourceGroup.strandCount, groomPoints = sourceGroup.strandParticleCount,
                maxBakeSourceOffsetMeters = sourceError,
                groomMaterial = groom.GetMaterial(groom.strandGroupInstances[0]).name,
                baselineTriangles = baseline.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3,
                opaqueShellTriangles = mesh.GetIndexCount(0) / 3,
                clippedPerimeterAndCardTriangles = mesh.GetIndexCount(1) / 3,
                vertices = mesh.vertexCount, atlas.width, atlas.height, atlas.mipmapCount,
                modeChecks = 3, comparisonImages = 18
            };
            File.WriteAllText(Output + "edit-mode.json", Newtonsoft.Json.JsonConvert.SerializeObject(result,
                Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            QualitySettings.renderPipeline = previousPipeline;
            RenderTexture.active = previousTarget;
            EditorSceneManager.ClosePreviewScene(scene);
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static float VerifySourceCurves(HairAsset.StrandGroup group)
    {
        using var reader = new BinaryReader(File.OpenRead(
            "Assets/_Sandbox/Characters/Ethan_V2/Groom/EthanV2_Scalp32.bytes"));
        if (reader.ReadInt32() != 0x48414952 || reader.ReadInt32() != group.strandCount
            || reader.ReadInt32() != group.strandParticleCount)
            throw new InvalidOperationException("The bake source does not match EthanV2_ScalpHair.");
        float maxError = 0;
        for (int i = 0; i < group.strandCount; i++)
        {
            HairAssetUtility.DeclareStrandIterator(group, i, out int start, out int stride, out _);
            for (int j = 0; j < group.strandParticleCount; j++)
            {
                var point = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                maxError = Mathf.Max(maxError, (point - group.particlePosition[start + stride * j]).sqrMagnitude);
            }
        }
        if (maxError > 1e-12f) throw new InvalidOperationException("Baked curves differ from the assigned HairAsset.");
        return Mathf.Sqrt(maxError);
    }

    static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Ethan comparison requires Edit Mode.");
    }

    static T Find<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).Single();

    static Material MakeMaterial(Texture2D atlas, bool clipped)
    {
        string name = clipped ? "Ethan Clump Cards" : "Ethan Scalp Shell";
        string path = Folder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var defaults = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        if (!material)
        {
            material = defaults;
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.CopyPropertiesFromMaterial(defaults);
            UnityEngine.Object.DestroyImmediate(defaults);
        }
        // Match the warmer midtone of Ethan_StrandHair in the shared studio setup.
        material.SetColor("_BaseColor", new Color(0.160f, 0.132f, 0.126f));
        material.SetTexture("_BaseMap", atlas);
        material.SetFloat("_AlphaClip", clipped ? 1 : 0);
        material.SetFloat("_Cutoff", 0.30f);
        material.SetFloat("_Cull", clipped ? (float)CullMode.Off : (float)CullMode.Back);
        material.SetFloat("_Smoothness", 0);
        material.SetFloat("_SpecularHighlights", 0);
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        if (clipped) material.EnableKeyword("_ALPHATEST_ON");
        else material.DisableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", clipped ? "TransparentCutout" : "Opaque");
        material.renderQueue = (int)(clipped ? RenderQueue.AlphaTest : RenderQueue.Geometry);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    static Mesh BuildMesh()
    {
        string path = Folder + "Ethan Groom Cards.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var positions = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var parts = new int[2][];
        using (var reader = new BinaryReader(File.OpenRead("Temp/EthanHairComparison.mesh")))
        {
            for (int part = 0; part < 2; part++)
            {
                int first = positions.Count;
                int vertices = reader.ReadInt32();
                int indices = reader.ReadInt32();
                for (int i = 0; i < vertices; i++)
                {
                    positions.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                    normals.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                    uv.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                }
                parts[part] = new int[indices];
                for (int i = 0; i < indices; i++) parts[part][i] = first + reader.ReadInt32();
            }
        }
        bool created = !mesh;
        if (created) mesh = new Mesh { name = "Ethan Groom Cards" };
        mesh.Clear();
        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(parts[0], 0);
        mesh.SetTriangles(parts[1], 1);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        if (created) AssetDatabase.CreateAsset(mesh, path);
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
        return mesh;
    }
}
