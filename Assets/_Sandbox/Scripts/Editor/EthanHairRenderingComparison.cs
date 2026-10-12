using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Unity.DemoTeam.Hair;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Editor-only comparison of real Game-view frames. No asset rebuild or simulation retuning.
public static class EthanHairRenderingComparison
{
    const string Output = "Docs/HairRendering";
    static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Camera camera;
    static Animator animator;
    static HairInstance hair;
    static Material originalMaterial, material;
    static UniversalRenderPipelineAsset pipeline;
    static UniversalAdditionalCameraData additional;
    static HairInstance.GroupSettings originalDefaults;
    static Vector3 cameraPosition;
    static Quaternion cameraRotation;
    static float originalScale, originalCaptureDelta, animatorSpeed;
    static int originalMsaa, stateHash, frame, index, phase;
    static AntialiasingMode originalAA;
    static TemporalAA.Settings originalTAA;
    static bool rendered;
    static ulong lastTimestamp;
    static MethodInfo encodeScreen;
    static readonly FrameTiming[] Timing = new FrameTiming[1];
    static readonly List<double> Gpu = new List<double>(), Cpu = new List<double>();
    static readonly List<object> Results = new List<object>();
    public static string Status { get; private set; } = "Idle";
    class Variant
    {
        public string name; public float width = 1, smoothness = .5f, scale = 1;
        public bool subdivide, smaa, veryHigh;
    }
    static Variant[] variants;

    public static void Start(bool includeScale = false, string onlyVariant = null, string suffix = "")
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        if (Status.StartsWith("Running")) throw new InvalidOperationException("Comparison already running.");
        camera = Camera.main;
        additional = camera.GetComponent<UniversalAdditionalCameraData>();
        hair = UnityEngine.Object.FindObjectsByType<HairInstance>(FindObjectsInactive.Exclude).First();
        animator = hair.GetComponentInParent<Animator>();
        pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        originalDefaults = hair.strandGroupDefaults;
        originalMaterial = hair.strandGroupDefaults.settingsRendering.materialAsset;
        material = new Material(originalMaterial) { name = "Ethan Hair Rendering Comparison", hideFlags = HideFlags.DontSave };
        hair.strandGroupDefaults.settingsRendering.materialAsset = material;
        originalMsaa = pipeline.msaaSampleCount; originalScale = pipeline.renderScale;
        originalAA = additional.antialiasing; originalTAA = additional.taaSettings;
        originalCaptureDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
        cameraPosition = camera.transform.position; cameraRotation = camera.transform.rotation;
        animatorSpeed = animator.speed; stateHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t => t != null);
        encodeScreen = type.GetMethod("EncodeScreenToPng", Flags);
        variants = new[] {
            new Variant { name = "Baseline" },
            new Variant { name = "TaaVeryHigh", veryHigh = true },
            new Variant { name = "SmaaMsaa4", smaa = true },
            new Variant { name = "Subdivision1", subdivide = true },
            new Variant { name = "Smoothness035", smoothness = .35f },
            new Variant { name = "Smoothness020", smoothness = .2f },
            new Variant { name = "Width125", width = 1.25f },
            new Variant { name = "Scale125", scale = 1.25f }
        }.Take(includeScale ? 8 : 7).ToArray();
        if (!string.IsNullOrEmpty(onlyVariant)) variants = variants.Where(v => onlyVariant.Split(',').Contains(v.name)).ToArray();
        if (variants.Length == 0) throw new ArgumentException("Unknown comparison variant.");
        foreach (var v in variants) v.name += suffix;
        Directory.CreateDirectory(Output);
        Results.Clear();
        if (File.Exists(Output + "/Comparisons.json")) Results.AddRange(JsonConvert.DeserializeObject<object[]>(File.ReadAllText(Output + "/Comparisons.json")));
        index = 0;
        Apply();
        RenderPipelineManager.endCameraRendering += OnRender;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    static void Apply()
    {
        var v = variants[index];
        camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        animator.Play(stateHash, 0, .25f); animator.Update(0); animator.speed = 0;
        material.SetFloat("_HairWidthScale", v.width); material.SetFloat("_HairSmoothness", v.smoothness);
        hair.strandGroupDefaults.settingsGeometry.stagingSubdivision = v.subdivide;
        hair.strandGroupDefaults.settingsGeometry.stagingSubdivisionCount = 1;
        pipeline.msaaSampleCount = 1;
        additional.antialiasing = v.smaa ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.TemporalAntiAliasing;
        additional.antialiasingQuality = AntialiasingQuality.High;
        additional.taaSettings.quality = v.veryHigh ? TemporalAAQuality.VeryHigh : TemporalAAQuality.High;
        if (v.smaa) pipeline.msaaSampleCount = 4;
        pipeline.renderScale = v.scale;
        additional.resetHistory = true;
        frame = 0; phase = 0; Gpu.Clear(); Cpu.Clear(); lastTimestamp = 0;
        Status = "Running " + v.name + ": stationary front";
        File.WriteAllText(Output + "/Status.txt", Status);
    }

    static void OnRender(ScriptableRenderContext context, Camera c)
    {
        if (c != camera) return;
        rendered = true;
        FrameTimingManager.CaptureFrameTimings();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Stop(); return; }
        if (!rendered) return;
        rendered = false;
        try
        {
            frame++;
            if (phase < 2 && frame > 60 && frame <= 180 && FrameTimingManager.GetLatestTimings(1, Timing) > 0)
            {
                var t = Timing[0];
                if (t.frameStartTimestamp != lastTimestamp)
                {
                    lastTimestamp = t.frameStartTimestamp;
                    if (t.gpuFrameTime > 0) Gpu.Add(t.gpuFrameTime);
                    if (t.cpuFrameTime > 0) Cpu.Add(t.cpuFrameTime);
                }
            }
            var name = variants[index].name;
            if (phase == 0 && frame == 181)
            {
                SaveFrame(name + "_Front"); SaveTiming("stationary");
                animator.Play(stateHash, 0, .25f); animator.Update(0); animator.speed = animatorSpeed;
                phase = 1; frame = 0; Gpu.Clear(); Cpu.Clear();
                Status = "Running " + name + ": existing head animation";
                File.WriteAllText(Output + "/Status.txt", Status);
            }
            else if (phase == 1)
            {
                if (frame >= 181 && frame <= 241 && (frame - 181) % 6 == 0) SaveFrame(name + "_Motion_" + (frame - 181).ToString("D3"));
                if (frame == 242)
                {
                    SaveTiming("animated"); animator.speed = 0;
                    animator.Play(stateHash, 0, .25f); animator.Update(0);
                    var pivot = hair.transform.parent.position;
                    camera.transform.position = pivot + Quaternion.AngleAxis(60, Vector3.up) * (cameraPosition - pivot);
                    camera.transform.LookAt(pivot);
                    additional.resetHistory = true; phase = 2; frame = 0;
                    Status = "Running " + name + ": stationary side";
                    File.WriteAllText(Output + "/Status.txt", Status);
                }
            }
            else if (phase == 2 && frame == 90)
            {
                SaveFrame(name + "_Side"); index++;
                if (index == variants.Length) { Status = "Completed"; Stop(); }
                else Apply();
            }
        }
        catch (Exception e) { Status = "Failed: " + e; Stop(); Debug.LogException(e); }
    }

    static void SaveFrame(string name)
    {
        // Reads the existing composited Game-view target; never calls Camera.Render().
        var size = (ValueTuple<int, int>)encodeScreen.DeclaringType.GetMethod("GetGameViewTargetSize", Flags).Invoke(null, null);
        var bytes = (byte[])encodeScreen.Invoke(null, new object[] { size.Item1, size.Item2 });
        File.WriteAllBytes(Output + "/" + name + ".png", bytes);
    }
    static double? Median(List<double> values)
    {
        if (values.Count == 0) return null;
        var ordered = values.OrderBy(x => x).ToArray();
        return (ordered[(ordered.Length - 1) / 2] + ordered[ordered.Length / 2]) * .5;
    }
    static void SaveTiming(string pose)
    {
        var v = variants[index]; var group = hair.strandGroupProviders[0].hairAsset.strandGroups[0];
        var size = (ValueTuple<int, int>)encodeScreen.DeclaringType.GetMethod("GetGameViewTargetSize", Flags).Invoke(null, null);
        Results.Add(new { variant = v.name, pose, width = size.Item1, height = size.Item2, warmedRenderedFrames = 60,
            gpuSamples = Gpu.Count, gpuMedianMs = Median(Gpu), cpuSamples = Cpu.Count, cpuMedianMs = Median(Cpu),
            gpuFramesMs = Gpu.ToArray(), cpuFramesMs = Cpu.ToArray(), renderScale = pipeline.renderScale,
            msaa = pipeline.msaaSampleCount, aa = additional.antialiasing.ToString(), taa = additional.taaSettings.quality.ToString(),
            hairWidthScale = v.width, hairSmoothness = v.smoothness, subdivision = v.subdivide,
            strands = group.strandCount, simulationPoints = group.strandParticleCount,
            staging = hair.solverData[0].constants._StagingStrandVertexCount });
        File.WriteAllText(Output + "/Comparisons.json", JsonConvert.SerializeObject(Results, Formatting.Indented));
    }

    static void OnPlayState(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Stop(); }
    public static void Stop()
    {
        RenderPipelineManager.endCameraRendering -= OnRender;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayState;
        if (pipeline) { pipeline.msaaSampleCount = originalMsaa; pipeline.renderScale = originalScale; }
        if (hair) hair.strandGroupDefaults = originalDefaults;
        if (camera) camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        if (additional) { additional.antialiasing = originalAA; additional.taaSettings = originalTAA; additional.resetHistory = true; }
        if (animator) animator.speed = animatorSpeed;
        Time.captureDeltaTime = originalCaptureDelta;
        if (material) UnityEngine.Object.DestroyImmediate(material);
        if (Status.StartsWith("Running")) Status = "Stopped";
        File.WriteAllText(Output + "/Status.txt", Status);
    }
}
