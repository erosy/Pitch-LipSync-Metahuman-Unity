using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    /// <summary>Owns persona activation and character activities in Talk Demo.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Character System/Talk Demo")]
    public sealed class TalkDemoController : MonoBehaviour
    {
        [Serializable]
        public sealed class Persona
        {
            public string key;
            public Character character;
            public MonoBehaviour readinessProvider;
            public ActivityDefinition greeting;
            public MonoBehaviour speechPlayer;
            public ActivityDefinition speaking;
            public TextAsset speechFixture;
            public Vector3 cameraPosition;
            public Vector3 cameraTarget;
        }

        public enum DemoState { Inactive, WarmingUp, Waving, Idle, Error, Speaking }

        [SerializeField] Camera characterCamera;
        [SerializeField] string initialPersonaKey = "Ethan Reeve";
        [SerializeField] Persona[] personas = Array.Empty<Persona>();

        Persona selected;
        bool readinessObserved;
        ulong greetingRequest;

        [ShowInInspector, ReadOnly] public string SelectedPersonaKey => selected?.key;
        [ShowInInspector, ReadOnly] public DemoState State { get; private set; }
        [ShowInInspector, ReadOnly] public string LastError { get; private set; }
        public bool IsReady => State == DemoState.Idle;
        internal Persona SelectedPersona => selected;
        internal IReadOnlyList<Persona> Personas => personas;
        public event Action PersonaChanging;

        void OnEnable()
        {
            if (Application.IsPlaying(gameObject)) ActivatePersona(initialPersonaKey);
        }

        void OnDisable()
        {
            PersonaChanging?.Invoke();
            if (selected?.character) selected.character.gameObject.SetActive(false);
            selected = null;
            State = DemoState.Inactive;
        }

        void Update()
        {
            if (Application.IsPlaying(gameObject)) Tick();
        }

        [Button("Select Ethan"), EnableIf("@UnityEngine.Application.isPlaying")]
        public void SelectEthan() => SelectPersona("Ethan Reeve");

        [Button("Select Liora"), EnableIf("@UnityEngine.Application.isPlaying")]
        public void SelectLiora() => SelectPersona("Liora Minji");

        public bool SelectPersona(string personaKey)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled) return false;
            return ActivatePersona(personaKey);
        }

        internal bool ActivatePersona(string personaKey)
        {
            if (personas == null) { Fail("Assign the demo personas."); return false; }
            Persona target = null;
            foreach (var persona in personas)
                if (persona != null && string.Equals(persona.key, personaKey, StringComparison.Ordinal))
                { target = persona; break; }
            // Invalid external keys must not interrupt a valid selection.
            if (target == null) { LastError = "Unknown persona key: " + personaKey; return false; }
            if (selected == target && target.character && target.character.gameObject.activeInHierarchy
                && State != DemoState.Error) return true;

            var errors = ValidateConfiguration();
            if (errors.Count > 0) { Fail(string.Join("\n", errors)); return false; }
            PersonaChanging?.Invoke();
            foreach (var persona in personas)
                if (persona != target) persona.character.gameObject.SetActive(false);
            selected = target;
            LastError = null;
            readinessObserved = false;
            greetingRequest = 0;
            State = DemoState.WarmingUp;
            characterCamera.transform.SetPositionAndRotation(target.cameraPosition,
                Quaternion.LookRotation(target.cameraTarget - target.cameraPosition, Vector3.up));
            target.character.gameObject.SetActive(true);
            var readinessErrors = new List<string>();
            ((ICharacterReadiness)target.readinessProvider).ValidateConfiguration(readinessErrors);
            if (readinessErrors.Count > 0) { Fail(string.Join("\n", readinessErrors)); return false; }
            return true;
        }

        internal void Tick()
        {
            if (State != DemoState.WarmingUp && State != DemoState.Waving && State != DemoState.Speaking) return;
            if (!selected.character || !selected.character.isActiveAndEnabled)
            { Fail("The selected character was disabled."); return; }
            if (State == DemoState.Speaking) return;
            if (State == DemoState.Waving)
            {
                // Character automatically returns to Idle on actual animation completion.
                if (selected.character.CurrentActivity == selected.character.DefaultIdle)
                    State = DemoState.Idle;
                else if (selected.character.CurrentRequestId != greetingRequest)
                    Fail("The greeting was interrupted by another activity.");
                return;
            }
            if (!selected.character.IsInitialized) return;
            if (!selected.readinessProvider.isActiveAndEnabled
                || !((ICharacterReadiness)selected.readinessProvider).IsReady)
            { readinessObserved = false; return; }
            // Visibility is restored late in the warmup frame. Wave on the following frame.
            if (!readinessObserved) { readinessObserved = true; return; }
            if (!selected.character.PlayActivity(selected.greeting, out greetingRequest))
            { Fail("The greeting activity was rejected."); return; }
            State = DemoState.Waving;
        }

        internal bool BeginSpeaking(out ulong requestId)
        {
            requestId = 0;
            if (!IsReady || !selected.character.PlayActivity(selected.speaking, out requestId)) return false;
            State = DemoState.Speaking;
            return true;
        }

        internal void EndSpeaking(ulong requestId)
        {
            if (State != DemoState.Speaking || selected == null) return;
            if (selected.character && selected.character.IsInitialized
                && selected.character.CurrentRequestId == requestId) selected.character.ReturnToIdle();
            State = DemoState.Idle;
        }

        void Fail(string error)
        {
            LastError = error;
            State = DemoState.Error;
            if (selected?.character && selected.character.IsInitialized) selected.character.ReturnToIdle();
        }

        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (!characterCamera) errors.Add("Assign the character camera.");
            if (personas == null || personas.Length != 2) errors.Add("Assign the two demo personas.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var roots = new List<Transform>();
            if (personas != null) foreach (var persona in personas)
            {
                if (persona == null) { errors.Add("A persona binding is missing."); continue; }
                if (string.IsNullOrWhiteSpace(persona.key) || !keys.Add(persona.key))
                    errors.Add("Persona keys must be nonempty and unique.");
                if (!persona.character) { errors.Add(persona.key + ": assign a Character."); continue; }
                var root = persona.character.transform;
                foreach (var other in roots)
                    if (root == other || root.IsChildOf(other) || other.IsChildOf(root))
                        errors.Add("Personas must have separate character roots.");
                roots.Add(root);
                errors.AddRange(persona.character.ValidateActivity(persona.greeting));
                if (!persona.greeting || !persona.greeting.Animation || persona.greeting.Animation.Loop)
                    errors.Add(persona.key + ": greeting must be a one-shot animation.");
                if (persona.readinessProvider is ICharacterReadiness readiness)
                {
                    if (persona.readinessProvider.GetComponentInParent<Character>(true) != persona.character)
                        errors.Add(persona.key + ": readiness must belong to this character.");
                    // The existing readiness adapter expects active hierarchy state.
                    // Validate an inactive persona after activation instead.
                    if (persona.character.gameObject.activeInHierarchy) readiness.ValidateConfiguration(errors);
                }
                else errors.Add(persona.key + ": assign an ICharacterReadiness provider.");
                foreach (var flow in persona.character.GetComponentsInChildren<CharacterFlowController>(true))
                    if (flow.enabled) errors.Add(persona.key + ": disable the automatic flow controller.");
                if ((persona.cameraPosition - persona.cameraTarget).sqrMagnitude < .0001f)
                    errors.Add(persona.key + ": camera position and target must differ.");
            }
            if (!keys.Contains(initialPersonaKey)) errors.Add("Initial persona must match a configured key.");
            return errors;
        }
    }
}
