using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    /// <summary>
    /// Attach to the character root so every activation runs its greeting.
    /// Create flow/state assets via Character System/Flow. Greeting: Waving (Animation End).
    /// Response: Thinking (Animation End), Use Default Idle (Duration, 1 second), Speech.
    /// Assign readiness and speech components; replace the Boy callback with PlayResponse.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("Character System/Flow Controller")]
    public sealed class CharacterFlowController : MonoBehaviour
    {
        [SerializeField, FoldoutGroup("References")] Character character;
        [SerializeField, FoldoutGroup("References"), Tooltip("Optional component implementing ICharacterReadiness.")]
        MonoBehaviour readinessProvider;
        [SerializeField, FoldoutGroup("References"), Tooltip("CharacterSpeechPlayer or another ICharacterSpeechPlayback component.")]
        MonoBehaviour speechPlayer;
        [SerializeField, FoldoutGroup("Flows")] CharacterFlowDefinition greetingFlow;
        [SerializeField, FoldoutGroup("Flows")] CharacterFlowDefinition responseFlow;

        CharacterFlowContext context;
        CharacterFlowState state;
        CharacterFlowStateDefinition[] runStates;
        int stateIndex;
        bool activationActive, readinessObserved, readinessValidated, executing;

        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")] public bool IsReady { get; private set; }
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")] public bool IsRunning => context != null;
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")] public CharacterFlowDefinition CurrentFlow { get; private set; }
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")] public CharacterFlowStateDefinition CurrentState { get; private set; }
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Status")] public string LastError { get; private set; }

        ICharacterSpeechPlayback Speech => speechPlayer ? speechPlayer as ICharacterSpeechPlayback : null;

        void Reset() => character = GetComponent<Character>();
        void OnEnable() { if (Application.IsPlaying(gameObject)) BeginActivation(); }
        void OnDisable() => EndActivation();
        void OnDestroy() => EndActivation();
        void Update() { if (Application.IsPlaying(gameObject)) Tick(Time.deltaTime); }

        internal void BeginActivation()
        {
            EndActivation();
            activationActive = true;
            readinessObserved = readinessValidated = false;
            LastError = null;
        }

        internal void EndActivation()
        {
            activationActive = false;
            IsReady = false;
            FinishRun(true);
        }

        /// <summary>Wire the Boy button to this method. Loading-time requests are ignored.</summary>
        public void PlayResponse() => TryPlayFlow(responseFlow);

        public bool TryPlayFlow(CharacterFlowDefinition flow)
        {
            if (executing || !activationActive || !IsReady || !isActiveAndEnabled
                || !character || !character.isActiveAndEnabled || !character.IsInitialized) return false;
            executing = true;
            try { return StartFlow(flow); }
            finally { executing = false; }
        }

        public void CancelFlow()
        {
            if (executing) return;
            executing = true;
            try { FinishRun(true); }
            finally { executing = false; }
        }

        internal void Tick(float deltaTime)
        {
            if (!activationActive || !isActiveAndEnabled || executing) return;
            executing = true;
            try
            {
                if (!IsReady)
                {
                    if (!character) { Fail("Assign a Character."); activationActive = false; return; }
                    if (!character.isActiveAndEnabled || !character.IsInitialized) return;
                    if (!readinessValidated)
                    {
                        var errors = new List<string>();
                        ValidateReadiness(errors);
                        if (errors.Count > 0) { Fail(string.Join("\n", errors)); activationActive = false; return; }
                        readinessValidated = true;
                    }
                    if (readinessProvider && (!readinessProvider.isActiveAndEnabled
                        || !((ICharacterReadiness)readinessProvider).IsReady))
                    { readinessObserved = false; return; }
                    // Observe readiness on one frame, start on the next, after visibility restoration.
                    if (!readinessObserved) { readinessObserved = true; return; }
                    IsReady = true;
                    StartFlow(greetingFlow);
                    return;
                }
                if (context == null) return;
                if (!character || !character.isActiveAndEnabled || !character.IsInitialized
                    || (context.HasActivity && !context.OwnsActivity))
                { FinishRun(false); return; }
                float delta = float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0 : Mathf.Max(0, deltaTime);
                var result = state.Tick(delta, out var error);
                if (result == CharacterFlowStateResult.Failed) { Fail(error ?? "Flow state failed."); return; }
                if (result != CharacterFlowStateResult.Completed) return;
                ExitState();
                // Exit may have handed the character to another system.
                if (context.HasActivity && !context.OwnsActivity) { FinishRun(false); return; }
                if (++stateIndex >= runStates.Length) FinishRun(true);
                else EnterState();
            }
            catch (Exception exception)
            {
                Fail(exception.ToString());
                if (!IsReady) activationActive = false;
            }
            finally { executing = false; }
        }

        bool StartFlow(CharacterFlowDefinition flow)
        {
            List<string> errors;
            try
            {
                errors = flow ? flow.ValidateFor(character, Speech) : new List<string> { "Assign a Flow Definition." };
                if (speechPlayer && speechPlayer.GetComponentInParent<Character>(true) is Character speechOwner
                    && speechOwner != character) errors.Add("Speech Player must belong to the assigned Character.");
            }
            catch (Exception exception) { return Report("Flow validation failed: " + exception.Message); }
            if (errors.Count > 0) return Report(string.Join("\n", errors));
            // Validate before stopping a valid run. Snapshot the list so live authoring cannot reorder this run.
            var nextStates = new CharacterFlowStateDefinition[flow.States.Count];
            for (int i = 0; i < nextStates.Length; i++) nextStates[i] = flow.States[i];
            FinishRun(false);
            LastError = null;
            CurrentFlow = flow;
            runStates = nextStates;
            stateIndex = 0;
            context = new CharacterFlowContext(character, Speech);
            try { return EnterState(); }
            catch (Exception exception) { Fail(exception.ToString()); return false; }
        }

        bool EnterState()
        {
            CurrentState = runStates[stateIndex];
            if (!CurrentState) { Fail("A running state definition was destroyed."); return false; }
            state = CurrentState.CreateState(context);
            if (state == null) { Fail("State did not create a runtime instance."); return false; }
            if (state.Enter(out var error)) return true;
            Fail(error ?? "Could not enter flow state.");
            return false;
        }

        void ExitState()
        {
            var previous = state;
            state = null;
            previous?.Exit();
        }

        void FinishRun(bool returnToIdle)
        {
            var previousContext = context;
            context = null;
            try { ExitState(); }
            catch (Exception exception) { Report("State cleanup failed: " + exception.Message); }
            finally
            {
                try { if (returnToIdle) previousContext?.ReturnToIdleIfOwned(); }
                finally
                {
                    previousContext?.Dispose();
                    runStates = null;
                    CurrentFlow = null;
                    CurrentState = null;
                }
            }
        }

        void Fail(string error) { Report(error); FinishRun(true); }
        bool Report(string error)
        {
            LastError = error;
            Debug.LogWarning(name + ": " + error, this);
            return false;
        }

        void ValidateReadiness(List<string> errors)
        {
            if (!readinessProvider) return;
            if (readinessProvider is ICharacterReadiness readiness) readiness.ValidateConfiguration(errors);
            else errors.Add("Readiness Provider must implement ICharacterReadiness.");
        }

        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (!character) errors.Add("Assign a Character.");
            else errors.AddRange(character.ValidateConfiguration());
            ValidateReadiness(errors);
            if (speechPlayer && !(speechPlayer is ICharacterSpeechPlayback))
                errors.Add("Speech Player must implement ICharacterSpeechPlayback.");
            if (speechPlayer && speechPlayer.GetComponentInParent<Character>(true) is Character speechOwner
                && character && speechOwner != character) errors.Add("Speech Player must belong to the assigned Character.");
            if (!greetingFlow) errors.Add("Assign a Greeting Flow.");
            else errors.AddRange(greetingFlow.ValidateFor(character, Speech));
            if (!responseFlow) errors.Add("Assign a Response Flow.");
            else errors.AddRange(responseFlow.ValidateFor(character, Speech));
            return errors;
        }
    }
}
