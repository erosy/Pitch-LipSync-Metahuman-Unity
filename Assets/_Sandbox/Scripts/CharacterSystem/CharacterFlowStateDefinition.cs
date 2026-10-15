using System.Collections.Generic;

namespace Sandbox.CharacterSystem
{
    /// <summary>Shared configuration only. Create a new runtime state for every entry.</summary>
    public abstract class CharacterFlowStateDefinition : CharacterDefinition
    {
        public abstract CharacterFlowState CreateState(CharacterFlowContext context);

        public virtual void ValidateFor(Character character, ICharacterSpeechPlayback speech, List<string> errors) { }
    }

    public enum CharacterFlowStateResult { Running, Completed, Failed }

    /// <summary>Exit is called on completion, cancellation, and partial entry failure.</summary>
    public abstract class CharacterFlowState
    {
        public abstract bool Enter(out string error);
        public abstract CharacterFlowStateResult Tick(float deltaTime, out string error);
        public virtual void Exit() { }
    }
}
