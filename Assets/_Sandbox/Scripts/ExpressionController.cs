using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class ExpressionController : MonoBehaviour
{
    [SerializeField, FoldoutGroup("References")] SkinnedMeshRenderer face;
    [SerializeField, FoldoutGroup("References")] uLipSync.uLipSync lipSync;
    [SerializeField, FoldoutGroup("References")] uLipSync.uLipSyncBlendShape blendShapeDriver;
    [SerializeField, FoldoutGroup("References")] BlendShapeDebugger blendShapeDebugger;
    [SerializeField, FoldoutGroup("Expressions"), Range(0, 1.5f)] float intensity = 1;
    [SerializeField, FoldoutGroup("Expressions"), Min(.01f)] float fadeTime = .3f;
    [Tooltip("Unscaled seconds to retain the speaking pose after audio ends.")]
    [SerializeField, FoldoutGroup("Expressions"), Min(0)] float endOfSpeechDelay = .3f;
    [SerializeField, FoldoutGroup("Expressions"), ListDrawerSettings(ShowFoldout = true)]
    List<ExpressionDefinition> expressionDefinitions = new List<ExpressionDefinition>();
    [SerializeField, FoldoutGroup("Activities"), ListDrawerSettings(ShowFoldout = true)]
    List<FacialActivityDefinition> activityDefinitions = new List<FacialActivityDefinition>();
    public IReadOnlyList<ExpressionDefinition> Expressions => expressionDefinitions;
    public IReadOnlyList<FacialActivityDefinition> Activities => activityDefinitions;
    public SkinnedMeshRenderer Face => face;
    public BlendShapeDebugger Debugger => blendShapeDebugger;
    [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")] public bool IsSpeaking { get; private set; }
    [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")] public bool IsNeutral => string.IsNullOrEmpty(expressionName);
    [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")] public bool IsTransitioning => cacheDirty || expressionRequested || playback.IsTransitioning;
    [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")] public string CurrentExpression => expressionName ?? "Neutral";
    [ShowInInspector, ReadOnly, MultiLineProperty(6), FoldoutGroup("Runtime Status")] public string ActivityStatus
    {
        get
        {
            if (!Application.isPlaying) return "Edit Mode — runtime inactive";
            if (PreviewOwns(face)) return "Suspended for debugger preview";
            var lines = new List<string>();
            foreach (var state in scheduler.Channels)
                lines.Add($"{state.Channel}: {state.Activity} / {state.Variant} — {state.Phase}, {state.Remaining:F2}s");
            return string.Join("\n", lines);
        }
    }
    [ShowInInspector, ReadOnly, MultiLineProperty(6), FoldoutGroup("References")]
    public string ConfigurationWarnings => string.Join("\n", ValidateConfiguration());
    readonly FacialExpressionPlayback playback = new FacialExpressionPlayback();
    readonly FacialActivityScheduler scheduler = new FacialActivityScheduler(new FacialRandom());
    readonly FacialBlendShapeOutput output = new FacialBlendShapeOutput();
    readonly HashSet<string> warned = new HashSet<string>();
    readonly HashSet<string> reservedVisemes = new HashSet<string>();
    string expressionName;
    bool cacheDirty = true, expressionRequested = true, wasAudioPlaying, wasPreview;
    float audioStoppedAt = float.NegativeInfinity;

    public void SetExpression(string expression) { expressionName = expression; expressionRequested = true; }
    [Button, FoldoutGroup("Expressions"), EnableIf("IsInPlayMode")] public void Neutral() => SetExpression(null);
    bool IsInPlayMode => Application.isPlaying;
    void Reset() { expressionDefinitions = FacialPresets.Expressions(); activityDefinitions = FacialPresets.Activities(); }
    void OnEnable() { cacheDirty = true; scheduler.Cancel(); }
    void OnValidate() => cacheDirty = true;
    void OnDisable()
    {
        if (!PreviewOwns(output.Face)) output.Clear(CurrentVisemes(output.Face));
        scheduler.Cancel(); cacheDirty = true; IsSpeaking = false; wasAudioPlaying = false; audioStoppedAt = float.NegativeInfinity;
    }
    bool PreviewOwns(SkinnedMeshRenderer renderer) => renderer && blendShapeDebugger && blendShapeDebugger.Face == renderer && blendShapeDebugger.IsHolding;
    bool AudioPlaying()
    {
        if (!lipSync || !lipSync.isActiveAndEnabled || !blendShapeDriver || !blendShapeDriver.isActiveAndEnabled || blendShapeDriver.skinnedMeshRenderer != face) return false;
        var source = lipSync.audioSourceProxy ? lipSync.audioSourceProxy.GetComponent<AudioSource>() : lipSync.GetComponent<AudioSource>();
        return source && source.isActiveAndEnabled && source.isPlaying;
    }
    HashSet<string> CurrentVisemes(SkinnedMeshRenderer renderer)
    {
        var set = new HashSet<string>(); var mesh = renderer ? renderer.sharedMesh : null;
        if (mesh && blendShapeDriver && blendShapeDriver.skinnedMeshRenderer == renderer && blendShapeDriver.blendShapes != null)
            foreach (var shape in blendShapeDriver.blendShapes)
                if (shape != null && shape.index >= 0 && shape.index < mesh.blendShapeCount) set.Add(mesh.GetBlendShapeName(shape.index));
        return set;
    }
    HashSet<string> ExpressionShapes(List<string> warnings, HashSet<string> available, HashSet<string> visemes)
    {
        var shapes = new HashSet<string>(); var names = new HashSet<string>();
        if (Expressions == null) return shapes;
        foreach (var definition in Expressions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.name) || !names.Add(definition.name))
            { warnings.Add("Expression names must be non-empty and unique (Neutral uses an empty selection)."); continue; }
            foreach (var pose in new[] { definition.silentPose, definition.speakingPose })
            {
                var visited = new HashSet<string>();
                foreach (var target in FacialMath.Targets(pose))
                {
                    if (string.IsNullOrEmpty(target.shape)) { warnings.Add(definition.name + ": empty shape name."); continue; }
                    if (!visited.Add(target.shape)) warnings.Add(definition.name + ": duplicate target '" + target.shape + "'.");
                    if (!available.Contains(target.shape)) warnings.Add(definition.name + ": missing shape '" + target.shape + "'.");
                    if (visemes.Contains(target.shape)) warnings.Add(definition.name + ": '" + target.shape + "' is reserved by uLipSync; skipped.");
                    if (available.Contains(target.shape) && !visemes.Contains(target.shape)) shapes.Add(target.shape);
                }
            }
        }
        return shapes;
    }
    public string[] GetBlendShapeNames()
    {
        var mesh = face ? face.sharedMesh : null; if (!mesh) return new string[0];
        var names = new string[mesh.blendShapeCount]; for (int i = 0; i < names.Length; i++) names[i] = mesh.GetBlendShapeName(i); return names;
    }
    public List<string> ValidateConfiguration()
    {
        var warnings = new List<string>();
        if (!face || !face.sharedMesh) warnings.Add("Assign a Face with a blendshape mesh.");
        if (!lipSync || !blendShapeDriver) warnings.Add("Speech references are incomplete; speech eligibility will remain silent.");
        if (blendShapeDriver && blendShapeDriver.skinnedMeshRenderer != face) warnings.Add("uLipSync's Face differs from this Face.");
        if (blendShapeDebugger && blendShapeDebugger.Face != face) warnings.Add("Debugger references a different Face.");
        var available = new HashSet<string>(GetBlendShapeNames()); var visemes = CurrentVisemes(face);
        ExpressionShapes(warnings, available, visemes);
        var reserved = new HashSet<string>(AllExpressionShapes()); reserved.UnionWith(visemes);
        var validator = new FacialActivityScheduler(new FacialRandom()); validator.Configure(activityDefinitions, available, reserved, warnings);
        FacialValidation.Check(Expressions, activityDefinitions, warnings);
        return warnings;
    }
    void Rebuild(HashSet<string> visemes)
    {
        var previous = output.Face == face && output.Mesh == (face ? face.sharedMesh : null)
            ? new Dictionary<string, float>(playback.Output) : null;
        if (!PreviewOwns(output.Face)) output.Clear(CurrentVisemes(output.Face));
        output.Bind(face); reservedVisemes.Clear(); reservedVisemes.UnionWith(visemes);
        var warnings = ValidateConfiguration(); var expressionShapes = ExpressionShapes(new List<string>(), output.Available, visemes);
        var reserved = new HashSet<string>(visemes); reserved.UnionWith(AllExpressionShapes());
        scheduler.Configure(activityDefinitions, output.Available, reserved, new List<string>());
        var owned = new HashSet<string>(expressionShapes);
        owned.UnionWith(scheduler.OwnedShapes);
        output.SetOwned(owned, visemes);
        playback.Configure(expressionShapes, previous ?? output.Read(expressionShapes));
        foreach (var warning in warnings) if (warned.Add(warning)) Debug.LogWarning(name + ": " + warning, this);
        cacheDirty = false; expressionRequested = true;
    }
    IEnumerable<string> AllExpressionShapes()
    {
        if (Expressions == null) yield break;
        foreach (var expression in Expressions)
        {
            if (expression == null) continue;
            foreach (var pose in new[] { expression.silentPose, expression.speakingPose })
                foreach (var target in FacialMath.Targets(pose)) if (!string.IsNullOrEmpty(target.shape)) yield return target.shape;
        }
    }
    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        bool audio = AudioPlaying();
        if (audio) audioStoppedAt = float.NegativeInfinity;
        else if (wasAudioPlaying) audioStoppedAt = Time.unscaledTime;
        wasAudioPlaying = audio;
        IsSpeaking = audio || Time.unscaledTime - audioStoppedAt < FacialMath.NonNegative(endOfSpeechDelay);
        var visemes = CurrentVisemes(face);
        if (cacheDirty || output.Face != face || output.Mesh != (face ? face.sharedMesh : null) || !reservedVisemes.SetEquals(visemes)) Rebuild(visemes);
        playback.Tick(Time.deltaTime, Expressions, expressionName, IsSpeaking, intensity, fadeTime);
        expressionRequested = false;
        if (PreviewOwns(face)) { scheduler.Cancel(); wasPreview = true; return; }
        if (wasPreview)
        {
            playback.Tick(float.MaxValue, Expressions, expressionName, IsSpeaking, intensity, fadeTime);
            scheduler.Cancel(); wasPreview = false;
        }
        if (!face || !face.sharedMesh) { scheduler.Cancel(); return; }
        scheduler.Tick(Time.deltaTime, new FacialContext { expression = expressionName, speaking = IsSpeaking, transitioning = IsTransitioning });
        output.Apply(playback.Output, scheduler.Output);
    }
}
