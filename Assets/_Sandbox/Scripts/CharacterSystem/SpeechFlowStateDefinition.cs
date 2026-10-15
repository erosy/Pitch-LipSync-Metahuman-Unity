using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    [CreateAssetMenu(fileName = "Speech State", menuName = "Character System/Flow/Speech State")]
    public sealed class SpeechFlowStateDefinition : CharacterFlowStateDefinition
    {
        [SerializeField, Tooltip("Use an expression-only activity or a looping body activity that persists until speech ends.")]
        ActivityDefinition activity;

        public override CharacterFlowState CreateState(CharacterFlowContext context) => new State(context, activity);

        protected override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            if (!activity) errors.Add("Assign a Speaking Activity.");
            else
            {
                errors.AddRange(activity.ValidateConfiguration());
                if (activity.Animation && !activity.Animation.Loop)
                    errors.Add("Speaking Activity must use a looping body animation or the default idle fallback.");
            }
        }

        public override void ValidateFor(Character character, ICharacterSpeechPlayback speech, List<string> errors)
        {
            if (character) errors.AddRange(character.ValidateActivity(activity));
            else errors.Add("Assign a Character.");
            if (speech == null) errors.Add("Speech state requires a Speech Player.");
            else speech.ValidateConfiguration(errors);
        }

        sealed class State : CharacterFlowState
        {
            readonly CharacterFlowContext context;
            readonly ActivityDefinition activity;
            ulong playbackId;

            internal State(CharacterFlowContext context, ActivityDefinition activity)
            { this.context = context; this.activity = activity; }

            public override bool Enter(out string error)
            {
                if (!context.PlayActivity(activity, out _)) { error = "Speaking activity was rejected."; return false; }
                if (context.Speech == null) { error = "Speech Player is unavailable."; return false; }
                return context.Speech.TryPlay(out playbackId, out error);
            }

            public override CharacterFlowStateResult Tick(float deltaTime, out string error)
            {
                error = null;
                switch (context.Speech.GetStatus(playbackId))
                {
                    case CharacterSpeechStatus.Playing: return CharacterFlowStateResult.Running;
                    case CharacterSpeechStatus.Completed: return CharacterFlowStateResult.Completed;
                    default:
                        error = "Speech playback failed or was interrupted.";
                        return CharacterFlowStateResult.Failed;
                }
            }

            public override void Exit() { if (playbackId != 0) context.Speech?.Stop(playbackId); }
        }
    }
}
