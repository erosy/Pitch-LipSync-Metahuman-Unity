using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    public enum ActivityFlowCompletion { AnimationEnd, Duration }

    [CreateAssetMenu(fileName = "Activity State", menuName = "Character System/Flow/Activity State")]
    public sealed class ActivityFlowStateDefinition : CharacterFlowStateDefinition
    {
        [SerializeField] bool useDefaultIdle;
        [SerializeField, HideIf(nameof(useDefaultIdle))] ActivityDefinition activity;
        [SerializeField] ActivityFlowCompletion completion = ActivityFlowCompletion.AnimationEnd;
        [SerializeField, ShowIf(nameof(UsesDuration)), Min(0)] float duration = 1f;
        bool UsesDuration => completion == ActivityFlowCompletion.Duration;

        public override CharacterFlowState CreateState(CharacterFlowContext context)
            => new State(context, useDefaultIdle ? context.Character.DefaultIdle : activity, completion, duration);

        protected override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            if (!useDefaultIdle)
            {
                if (!activity) errors.Add("Assign an Activity or enable Use Default Idle.");
                else errors.AddRange(activity.ValidateConfiguration());
            }
            if (completion != ActivityFlowCompletion.AnimationEnd && completion != ActivityFlowCompletion.Duration)
                errors.Add("Select a supported Completion mode.");
            if (completion == ActivityFlowCompletion.AnimationEnd
                && (useDefaultIdle || !activity || !activity.Animation || !activity.Animation.Clip || activity.Animation.Loop))
                errors.Add("Animation End requires an explicit non-looping body animation with a Clip; use Duration for Idle.");
            if (UsesDuration && (!IsFinite(duration) || duration < 0))
                errors.Add("Duration must be finite and non-negative.");
        }

        public override void ValidateFor(Character character, ICharacterSpeechPlayback speech, List<string> errors)
        {
            if (!character) { errors.Add("Assign a Character."); return; }
            errors.AddRange(character.ValidateActivity(useDefaultIdle ? character.DefaultIdle : activity));
            if (completion == ActivityFlowCompletion.AnimationEnd)
            {
                bool hasBody = false;
                if (character.ModulesRoot)
                    foreach (var module in character.ModulesRoot.GetComponentsInChildren<AnimationModule>(true))
                        if (module.GetComponentInParent<Character>(true) == character && module.isActiveAndEnabled
                            && module.Animator && module.Animator.isActiveAndEnabled) hasBody = true;
                if (!hasBody) errors.Add("Animation End requires an enabled Animation Module and Animator.");
            }
        }

        sealed class State : CharacterFlowState
        {
            readonly CharacterFlowContext context;
            readonly ActivityDefinition activity;
            readonly ActivityFlowCompletion completion;
            readonly float duration;
            ulong requestId;
            float elapsed;

            internal State(CharacterFlowContext context, ActivityDefinition activity, ActivityFlowCompletion completion, float duration)
            { this.context = context; this.activity = activity; this.completion = completion; this.duration = duration; }

            public override bool Enter(out string error)
            {
                bool accepted = context.PlayActivity(activity, out requestId);
                error = accepted ? null : "Activity state could not acquire its activity.";
                return accepted;
            }

            public override CharacterFlowStateResult Tick(float deltaTime, out string error)
            {
                error = null;
                if (completion == ActivityFlowCompletion.AnimationEnd && !context.WasActivityCompleted(requestId)
                    && (!context.Character.TryGetModule<AnimationModule>(out var body) || !body.isActiveAndEnabled
                        || !body.Animator || !body.Animator.isActiveAndEnabled || !body.HasPlaybackGraph))
                {
                    error = "Animation End lost its active Animation Module or Animator.";
                    return CharacterFlowStateResult.Failed;
                }
                elapsed += deltaTime;
                return (completion == ActivityFlowCompletion.Duration ? elapsed >= duration : context.WasActivityCompleted(requestId))
                    ? CharacterFlowStateResult.Completed : CharacterFlowStateResult.Running;
            }
        }
    }
}
