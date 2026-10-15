using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Character System/Expression Module")]
    public sealed class ExpressionModule : CharacterModule, IActivityReceiver, IFacialPreviewOwner
    {
        [SerializeField, FoldoutGroup("References")] SkinnedMeshRenderer face;
        [SerializeField, FoldoutGroup("References")] uLipSync.uLipSync lipSync;
        [SerializeField, FoldoutGroup("References")] uLipSync.uLipSyncBlendShape blendShapeDriver;
        [SerializeField, FoldoutGroup("References")] BlendShapeDebugger blendShapeDebugger;
        [SerializeField, FoldoutGroup("Expression Tuning"), Range(0, 1.5f)] float intensity = 1;
        [SerializeField, FoldoutGroup("Expression Tuning"), Min(0)] float neutralFadeDuration = .18f;
        [Tooltip("Unscaled grace period for random facial motion eligibility. Does not change the selected expression.")]
        [SerializeField, FoldoutGroup("Expression Tuning"), Min(0)] float endOfSpeechDelay = .3f;
        [SerializeField, FoldoutGroup("Facial Motions"), ListDrawerSettings(ShowFoldout = true)]
        List<FacialMotionDefinition> facialMotions = FacialMotionDefaults.Create();
        [SerializeField, FoldoutGroup("Debug / Preview")] ExpressionDefinition previewDefinition;

        internal static readonly HashSet<ExpressionModule> ActivePreviews = new HashSet<ExpressionModule>();
        readonly FacialBlendShapeOutput output = new FacialBlendShapeOutput();
        readonly FacialMotionScheduler scheduler = new FacialMotionScheduler(new FacialRandom());
        readonly HashSet<string> reserved = new HashSet<string>();
        readonly HashSet<string> owned = new HashSet<string>();
        ExpressionPlayback playback, preview;
        ExpressionDefinition activityExpression;
        bool idle, cacheDirty = true, audioWasPlaying, debuggerOwns;
        float audioStoppedAt = float.NegativeInfinity;
        float boundIntensity = float.NaN;
        SkinnedMeshRenderer previewFace;
        Mesh previewMesh;
        Dictionary<string, float> previewOriginal;

        public SkinnedMeshRenderer Face => face;
        public BlendShapeDebugger Debugger => blendShapeDebugger;
        public IReadOnlyList<FacialMotionDefinition> FacialMotions => facialMotions;
        [ShowInInspector, ReadOnly, FoldoutGroup("Debug / Preview")]
        public ExpressionDefinition CurrentDefinition => IsPreviewing ? preview.Definition : activityExpression;
        [ShowInInspector, ReadOnly, FoldoutGroup("Debug / Preview")]
        public bool IsTransitioning => (preview ?? playback)?.IsTransitioning ?? false;
        [ShowInInspector, ReadOnly, FoldoutGroup("Debug / Preview")]
        public bool IsSpeaking { get; private set; }
        [ShowInInspector, ReadOnly, FoldoutGroup("Debug / Preview")]
        public bool IsPreviewing => preview != null;
        [ShowInInspector, ReadOnly, FoldoutGroup("Facial Motions"), MultiLineProperty(4)]
        public string FacialMotionStatus
        {
            get
            {
                if (IsPreviewing || debuggerOwns) return "Suspended for facial preview";
                var lines = new List<string>();
                foreach (var state in scheduler.Channels)
                    lines.Add($"{state.Channel}: {state.Motion} / {state.Variant} — {state.Phase}, {state.Remaining:F2}s");
                return string.Join("\n", lines);
            }
        }
        [ShowInInspector, ReadOnly, FoldoutGroup("References"), MultiLineProperty(5)]
        public string ConfigurationWarnings
        {
            get
            {
                var warnings = new List<string>(); ValidateConfiguration(warnings);
                if (!lipSync || !blendShapeDriver) warnings.Add("Speech references are optional; speech detection is inactive until both are assigned.");
                var validator = new FacialMotionScheduler(new FacialRandom());
                validator.Configure(facialMotions, Available(), CurrentVisemes(face), warnings);
                return string.Join("\n", warnings);
            }
        }

        static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        HashSet<string> Available() => new HashSet<string>(BlendShapeDatabase.Default ? BlendShapeDatabase.Default.Names : Array.Empty<string>());

        public override void ValidateConfiguration(List<string> errors)
        {
            var database = BlendShapeDatabase.Default;
            if (!database) errors.Add("Expression Module: shared BlendShapeDatabase is missing.");
            else errors.AddRange(database.ValidateMesh(face ? face.sharedMesh : null));
            if (!Valid(intensity) || intensity > 1.5f || !Valid(neutralFadeDuration) || !Valid(endOfSpeechDelay))
                errors.Add("Expression Module: intensity must be 0–1.5; fade/grace durations must be finite and non-negative.");
            var character = GetComponentInParent<Character>(true);
            if (face && character && face.GetComponentInParent<Character>(true) != character)
                errors.Add("Expression Module: Face must belong to the same character.");
            if (blendShapeDriver && blendShapeDriver.skinnedMeshRenderer != face)
                errors.Add("Expression Module: uLipSync driver references a different Face.");
            if (blendShapeDebugger && blendShapeDebugger.Character != this)
                errors.Add("Expression Module: debugger must reference this module.");
            FacialMotionValidation.Check(facialMotions, errors);
        }

        public override void ValidateActivity(ActivityRequest request, List<string> errors)
            => ValidateExpression(request.Expression, errors);

        void ValidateExpression(ExpressionDefinition definition, List<string> errors)
        {
            if (!definition || definition.Targets == null) return;
            var visemes = CurrentVisemes(face);
            foreach (var target in definition.Targets)
                if (target != null && visemes.Contains(target.Shape))
                    errors.Add("Expression Module: '" + target.Shape + "' is owned by uLipSync.");
        }

        HashSet<string> CurrentVisemes(SkinnedMeshRenderer renderer)
        {
            var set = new HashSet<string>(); var mesh = renderer ? renderer.sharedMesh : null;
            if (!mesh) return set;
            for (int i = 0; i < mesh.blendShapeCount; i++)
                if (mesh.GetBlendShapeName(i).StartsWith("viseme_", StringComparison.Ordinal)) set.Add(mesh.GetBlendShapeName(i));
            if (blendShapeDriver && blendShapeDriver.skinnedMeshRenderer == renderer && blendShapeDriver.blendShapes != null)
                foreach (var shape in blendShapeDriver.blendShapes)
                    if (shape != null && shape.index >= 0 && shape.index < mesh.blendShapeCount) set.Add(mesh.GetBlendShapeName(shape.index));
            return set;
        }
        public bool CanPreviewShape(string shape) => !string.IsNullOrEmpty(shape) && !CurrentVisemes(face).Contains(shape);

        bool AudioPlaying()
        {
            if (!lipSync || !lipSync.isActiveAndEnabled || !blendShapeDriver || !blendShapeDriver.isActiveAndEnabled
                || blendShapeDriver.skinnedMeshRenderer != face) return false;
            var source = lipSync.audioSourceProxy ? lipSync.audioSourceProxy.GetComponent<AudioSource>() : lipSync.GetComponent<AudioSource>();
            return source && source.isActiveAndEnabled && source.isPlaying;
        }

        void Bind()
        {
            var visemes = CurrentVisemes(face);
            if (!cacheDirty && output.Face == face && output.Mesh == (face ? face.sharedMesh : null) && reserved.SetEquals(visemes)) return;
            bool sameBinding = output.Face == face && output.Mesh == (face ? face.sharedMesh : null);
            var previousOwned = new HashSet<string>(owned);
            bool sameReservations = reserved.SetEquals(visemes);
            var previous = sameBinding && playback != null
                ? new Dictionary<string, float>(playback.Output) : null;
            if (!sameBinding) output.Clear(CurrentVisemes(output.Face));
            output.Bind(face);
            reserved.Clear(); reserved.UnionWith(visemes);
            owned.Clear(); owned.UnionWith(Available()); owned.IntersectWith(output.Available); owned.ExceptWith(reserved);
            if (sameBinding && face && face.sharedMesh)
                foreach (var shape in previousOwned)
                    if (!owned.Contains(shape) && !reserved.Contains(shape))
                    {
                        int index = face.sharedMesh.GetBlendShapeIndex(shape);
                        if (index >= 0) face.SetBlendShapeWeight(index, 0);
                    }
            output.SetOwned(owned, reserved);
            // Choosing a preview asset also invokes OnValidate. Preserve the suspended activity's
            // blend clock unless its actual binding, ownership, or expression intensity changed.
            if (playback == null || !sameBinding || !sameReservations || !previousOwned.SetEquals(owned) || boundIntensity != intensity)
            {
                playback = new ExpressionPlayback(owned, previous ?? output.Read(owned));
                playback.Play(activityExpression, intensity, neutralFadeDuration);
            }
            boundIntensity = intensity;
            scheduler.Configure(facialMotions, Available(), reserved, new List<string>());
            cacheDirty = false;
        }

        public void ApplyActivity(ActivityRequest request)
        {
            StopFacialPreviews();
            if (!Owner || !isActiveAndEnabled) return;
            Bind();
            activityExpression = request.Expression; idle = request.IsIdle;
            playback.Play(activityExpression, intensity, neutralFadeDuration);
            scheduler.Cancel();
            output.Apply(playback.Output, null);
        }

        internal void Tick(float scaledDelta, bool audioPlaying, float unscaledTime)
        {
            if (audioPlaying) audioStoppedAt = float.NegativeInfinity;
            else if (audioWasPlaying) audioStoppedAt = unscaledTime;
            audioWasPlaying = audioPlaying;
            IsSpeaking = audioPlaying || unscaledTime - audioStoppedAt < endOfSpeechDelay;
            if (!Owner || !isActiveAndEnabled || IsPreviewing || debuggerOwns) return;
            Bind();
            playback.Tick(scaledDelta);
            scheduler.Tick(scaledDelta, new FacialMotionContext {
                idle = idle, speaking = IsSpeaking, transitioning = playback.IsTransitioning, expressionShapes = playback.ProtectedShapes
            });
            output.Apply(playback.Output, scheduler.Output);
        }

        void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject)) return;
            Tick(Time.deltaTime, AudioPlaying(), Time.unscaledTime);
            if (IsPreviewing) TickPreview(Time.unscaledDeltaTime);
        }

        [Button("Preview", DirtyOnClick = false), FoldoutGroup("Debug / Preview")]
        public void PreviewSelected() => StartPreview(previewDefinition);
        [Button("Preview Neutral", DirtyOnClick = false), FoldoutGroup("Debug / Preview")]
        public void PreviewNeutral() => StartPreview(null);

        public bool StartPreview(ExpressionDefinition definition)
        {
            var errors = new List<string>(); ValidateConfiguration(errors);
            if (definition) errors.AddRange(definition.ValidateConfiguration());
            ValidateExpression(definition, errors);
            if (!isActiveAndEnabled || !face || !face.gameObject.scene.IsValid() || !gameObject.scene.IsValid())
                errors.Add("Preview requires an enabled module with a Face in a scene or Prefab Stage.");
            if (errors.Count > 0) { Debug.LogWarning(name + ": " + string.Join("\n", errors), this); return false; }

            // Stop/restore the other owner before capturing this preview's original weights.
            BlendShapeDebugger.StopForFace(face);
            StopPreviewsFor(face, this);
            if (IsPreviewing && (previewFace != face || previewMesh != face.sharedMesh)) StopPreview();
            if (!IsPreviewing)
            {
                Bind(); scheduler.Cancel();
                previewFace = face; previewMesh = face.sharedMesh;
                previewOriginal = output.Read(owned);
                preview = new ExpressionPlayback(owned, previewOriginal);
                ActivePreviews.Add(this);
            }
            preview.Play(definition, intensity, neutralFadeDuration);
            output.Apply(preview.Output, null);
            return true;
        }

        internal void TickPreview(float unscaledDelta)
        {
            if (!IsPreviewing) return;
            if (!isActiveAndEnabled || face != previewFace || !previewFace || previewFace.sharedMesh != previewMesh
                || !reserved.SetEquals(CurrentVisemes(previewFace))) { StopPreview(); return; }
            preview.Tick(unscaledDelta); output.Apply(preview.Output, null);
        }

        [Button("Stop / Restore", DirtyOnClick = false), EnableIf(nameof(IsPreviewing)), FoldoutGroup("Debug / Preview")]
        public void StopPreview()
        {
            if (!IsPreviewing) return;
            ActivePreviews.Remove(this);
            if (previewFace && previewFace.sharedMesh == previewMesh)
            {
                var excluded = CurrentVisemes(previewFace);
                foreach (var pair in previewOriginal)
                    if (!excluded.Contains(pair.Key)) previewFace.SetBlendShapeWeight(previewMesh.GetBlendShapeIndex(pair.Key), pair.Value);
            }
            preview = null; previewOriginal = null; previewFace = null; previewMesh = null;
            scheduler.Cancel();
            if (Owner && isActiveAndEnabled && output.Face == face && output.Mesh == (face ? face.sharedMesh : null))
            { Bind(); output.Apply(playback.Output, null); }
        }

        internal static void StopPreviewsFor(SkinnedMeshRenderer renderer, ExpressionModule except = null)
        {
            foreach (var other in new List<ExpressionModule>(ActivePreviews))
                if (other && other != except && other.previewFace == renderer) other.StopPreview();
        }
        public void BeginDebuggerPreview(BlendShapeDebugger debugger)
        {
            StopPreviewsFor(face); scheduler.Cancel(); debuggerOwns = true;
        }
        public void EndDebuggerPreview(BlendShapeDebugger debugger)
        {
            debuggerOwns = false; scheduler.Cancel();
            if (Owner && isActiveAndEnabled) { Bind(); output.Apply(playback.Output, null); }
        }
        void StopFacialPreviews()
        {
            StopPreview();
            BlendShapeDebugger.StopForFace(face);
        }
        void Release()
        {
            StopFacialPreviews();
            if (Owner) output.Clear(CurrentVisemes(output.Face));
            scheduler.Cancel(); output.Bind(null); playback = null; activityExpression = null;
            IsSpeaking = false; audioWasPlaying = false; audioStoppedAt = float.NegativeInfinity; cacheDirty = true;
        }
        void OnValidate() => cacheDirty = true;
        protected override void OnSuspend() => Release();
        protected override void OnShutdown() => Release();
        protected override void OnDisable() { StopFacialPreviews(); base.OnDisable(); }
        protected override void OnDestroy() { Release(); base.OnDestroy(); }
    }
}
