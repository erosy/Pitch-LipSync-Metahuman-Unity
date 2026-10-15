using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    [CreateAssetMenu(fileName = "Character Flow", menuName = "Character System/Flow/Flow Definition")]
    public sealed class CharacterFlowDefinition : CharacterDefinition
    {
        [SerializeField, ListDrawerSettings(ShowIndexLabels = true)]
        List<CharacterFlowStateDefinition> states = new List<CharacterFlowStateDefinition>();

        public IReadOnlyList<CharacterFlowStateDefinition> States => states;

        protected override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            if (states == null || states.Count == 0) { errors.Add("Add at least one flow state."); return; }
            for (int i = 0; i < states.Count; i++)
            {
                if (!states[i]) { errors.Add($"State {i}: assign a state definition."); continue; }
                foreach (var error in states[i].ValidateConfiguration()) errors.Add($"State {i}: {error}");
            }
            CharacterDefinition.ValidateUniqueIds(states, errors);
        }

        public List<string> ValidateFor(Character character, ICharacterSpeechPlayback speech)
        {
            var errors = ValidateConfiguration();
            if (states != null)
                foreach (var state in states)
                    if (state) state.ValidateFor(character, speech, errors);
            return errors;
        }
    }
}
