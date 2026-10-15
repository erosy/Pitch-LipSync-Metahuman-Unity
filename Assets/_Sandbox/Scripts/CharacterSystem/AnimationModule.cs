using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Character System/Animation Module")]
    public sealed class AnimationModule : CharacterModule, IActivityReceiver
    {
        [SerializeField, FoldoutGroup("References")] Animator animator;
        [SerializeField, FoldoutGroup("Debug / Preview")] AnimationDefinition previewDefinition;
        internal static readonly HashSet<AnimationModule> ActivePreviews = new HashSet<AnimationModule>();
        AnimationPlayback playback, preview;
        BodyAnimatorBinding binding, previewBinding;
        BodyPoseSnapshot previewPose;
        Animator previewAnimator;
        bool previewFireEvents, previewPaused;
        ulong playingRequestId;
        bool completesActivity;

        public Animator Animator => animator;
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")]
        public AnimationDefinition CurrentDefinition => preview != null ? preview.Definition : playback?.Definition;
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")]
        public double PlaybackTime => preview != null ? preview.Time : playback?.Time ?? 0;
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")]
        public bool IsLooping => preview != null ? preview.Loop : playback != null && playback.Loop;
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")]
        public bool IsTransitioning => preview != null ? preview.IsTransitioning : playback != null && playback.IsTransitioning;
        [ShowInInspector, ReadOnly, FoldoutGroup("Debug / Preview")]
        public bool IsPreviewing => preview != null;
        [ShowInInspector, ReadOnly, FoldoutGroup("Debug / Preview")]
        public bool IsPreviewPaused => IsPreviewing && previewPaused;
        // Drawn by AnimationModuleEditor: changing transient preview time must not dirty a prefab.
        public float PreviewNormalizedTime
        {
            get => preview?.NormalizedTime ?? 0;
            set { if (preview != null) preview.Seek(value); }
        }

        internal int ActiveClipCount => playback?.ClipCount ?? 0;
        internal double ActivityPlaybackTime => playback?.Time ?? 0;
        internal bool HasPlaybackGraph => playback != null && playback.IsValid;

        void Reset() => animator = GetComponentInParent<Character>(true)?.GetComponentInChildren<Animator>(true);

        public override void ValidateConfiguration(List<string> errors)
        {
            if (!animator) { errors.Add("Animation Module: assign an Animator."); return; }
            var character = GetComponentInParent<Character>(true);
            if (character && animator.GetComponentInParent<Character>(true) != character)
                errors.Add("Animation Module: Animator must belong to the same character.");
            if (animator.avatar && !animator.avatar.isValid)
                errors.Add("Animation Module: Animator Avatar is invalid.");
        }

        public override void ValidateActivity(ActivityRequest request, List<string> errors)
            => ValidateAnimation(request.Animation, errors);

        void ValidateAnimation(AnimationDefinition definition, List<string> errors)
        {
            if (!definition) { errors.Add("Animation Module: assign an animation definition."); return; }
            if (definition.Clip && definition.Clip.isHumanMotion
                && (!animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman))
                errors.Add("Animation Module: humanoid clips require a valid humanoid Animator Avatar.");
        }

        public void ApplyActivity(ActivityRequest request)
        {
            StopPreview();
            if (!Owner || !isActiveAndEnabled) return;
            if (binding != null && binding.Animator != animator) ReleasePlayback();
            if (binding == null) binding = new BodyAnimatorBinding(animator);
            if (playback == null) playback = new AnimationPlayback(animator, name + " Body Activity");
            playingRequestId = request.RequestId;
            completesActivity = request.CompletesOnAnimationEnd;
            playback.Play(request.Animation);
        }

        internal void Tick(float deltaTime)
        {
            if (preview != null || playback == null) return;
            if (!animator || !animator.isActiveAndEnabled || binding.Animator != animator)
            { ReleasePlayback(); return; }
            bool ended = playback.Advance(deltaTime);
            if (ended && completesActivity) CompleteActivity(playingRequestId);
        }

        void Update()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (IsPreviewing) TickPreview(Time.unscaledDeltaTime);
            else Tick(Time.deltaTime);
        }

        [Button("Preview", DirtyOnClick = false), FoldoutGroup("Debug / Preview")]
        public void PreviewSelected() => StartPreview(previewDefinition);

        public bool StartPreview(AnimationDefinition definition)
        {
            var errors = new List<string>();
            ValidateConfiguration(errors);
            if (definition) errors.AddRange(definition.ValidateConfiguration());
            ValidateAnimation(definition, errors);
            if (!isActiveAndEnabled || !animator || !animator.isActiveAndEnabled
                || !gameObject.scene.IsValid() || !animator.gameObject.scene.IsValid())
                errors.Add("Preview requires an enabled module and Animator on scene or Prefab Stage objects.");
#if UNITY_EDITOR
            if (UnityEditor.AnimationMode.InAnimationMode())
                errors.Add("Stop the Animation window's preview before starting a module preview.");
#endif
            if (errors.Count > 0)
            { Debug.LogWarning(name + ": " + string.Join("\n", errors), this); return false; }

            if (preview != null && previewAnimator == animator)
            {
                preview.Play(definition);
                previewPaused = false;
                return true;
            }
            StopPreview();
            // A single Animator may have only one module preview owner at a time.
            foreach (var other in new List<AnimationModule>(ActivePreviews))
                if (other && other.previewAnimator == animator) other.StopPreview();
            try
            {
                previewAnimator = animator;
                previewFireEvents = animator.fireEvents;
                if (playback != null)
                {
                    previewPose = new BodyPoseSnapshot(animator.transform);
                    playback.SetOutputWeight(0);
                }
                else previewBinding = new BodyAnimatorBinding(animator);
                animator.fireEvents = false; // Scrubbing must not invoke gameplay animation events.
                preview = new AnimationPlayback(animator, name + " Body Preview");
                preview.Play(definition);
                previewPaused = false;
                ActivePreviews.Add(this);
                return true;
            }
            catch
            {
                StopPreview();
                throw;
            }
        }

        [Button("Pause / Resume", DirtyOnClick = false), EnableIf(nameof(IsPreviewing)), FoldoutGroup("Debug / Preview")]
        public void TogglePreviewPause() { if (IsPreviewing) previewPaused = !previewPaused; }

        internal void TickPreview(float unscaledDeltaTime)
        {
            if (preview == null) return;
            if (!this || !isActiveAndEnabled || !previewAnimator || !previewAnimator.isActiveAndEnabled
                || animator != previewAnimator || !gameObject.scene.IsValid())
            { StopPreview(); return; }
            if (!previewPaused) preview.Advance(unscaledDeltaTime);
        }

        [Button("Stop / Restore", DirtyOnClick = false), EnableIf(nameof(IsPreviewing)), FoldoutGroup("Debug / Preview")]
        public void StopPreview()
        {
            ActivePreviews.Remove(this);
            preview?.Dispose(); preview = null;
            previewBinding?.Restore(); previewBinding = null;
            previewPose?.Restore(); previewPose = null;
            if (previewAnimator) previewAnimator.fireEvents = previewFireEvents;
            previewAnimator = null;
            previewPaused = false;
            if (playback != null && binding != null && binding.Animator && binding.Animator.isActiveAndEnabled)
            {
                playback.SetOutputWeight(1);
                playback.Evaluate();
            }
        }

        void ReleasePlayback()
        {
            StopPreview();
            playback?.Dispose(); playback = null;
            binding?.Restore(); binding = null;
            completesActivity = false;
        }

        protected override void OnSuspend() => ReleasePlayback();
        protected override void OnShutdown() => ReleasePlayback();
        protected override void OnDisable()
        {
            StopPreview();
            base.OnDisable();
        }
        protected override void OnDestroy()
        {
            ReleasePlayback();
            base.OnDestroy();
        }
    }
}
