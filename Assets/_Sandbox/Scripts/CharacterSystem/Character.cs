using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    [DisallowMultipleComponent, AddComponentMenu("Character System/Character")]
    public sealed class Character : MonoBehaviour
    {
        [SerializeField] Transform modulesRoot;
        [SerializeField] ActivityDefinition defaultIdle;
        readonly List<CharacterModule> modules = new List<CharacterModule>();
        ActivityRequest currentRequest;
        ulong requestId;
        bool initialized, dispatching, completionPending;

        public Transform ModulesRoot => modulesRoot;
        public ActivityDefinition DefaultIdle => defaultIdle;
        public bool IsInitialized => initialized;
        [ShowInInspector, ReadOnly] public ActivityDefinition CurrentActivity { get; private set; }
        [ShowInInspector, ReadOnly] public ulong CurrentRequestId => requestId;

        void Reset() => modulesRoot = transform.Find("Modules");
        void OnEnable() { if (Application.isPlaying) Initialize(); }
        void OnDisable() => Shutdown();
        void OnDestroy() => Shutdown();

        /// <summary>Discover and initialize once, then enter default Idle. Also supports isolated Edit Mode checks.</summary>
        public bool Initialize()
        {
            if (initialized) return true;
            if (!EnsureInitialized()) return false;
            Dispatch(defaultIdle);
            return true;
        }

        public bool PlayActivity(ActivityDefinition activity)
        {
            if (dispatching) return Reject(new[] { "Cannot issue a new activity while receivers are applying a command." });
            var errors = ValidateActivity(activity);
            if (errors.Count > 0) return Reject(errors);
            if (!EnsureInitialized()) return false;
            Dispatch(activity);
            return true;
        }

        public bool ReturnToIdle() => PlayActivity(defaultIdle);

        /// <summary>Returns initialized modules, including disabled ones, without mutating character state.</summary>
        public bool TryGetModule<T>(out T module) where T : CharacterModule
        {
            foreach (var candidate in modules)
                if (candidate && candidate is T match) { module = match; return true; }
            module = null;
            return false;
        }

        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (!modulesRoot || modulesRoot == transform || !modulesRoot.IsChildOf(transform))
                errors.Add("Assign a Modules Root beneath this character.");
            if (!defaultIdle) errors.Add("Assign a Default Idle activity.");
            else
            {
                foreach (var error in defaultIdle.ValidateConfiguration()) errors.Add("Default Idle: " + error);
                if (!defaultIdle.IsIdle) errors.Add("Default Idle must be tagged Is Idle.");
                if (!defaultIdle.Animation || !defaultIdle.Animation.Loop)
                    errors.Add("Default Idle must reference a looping animation.");
                if (defaultIdle.Expression) errors.Add("Default Idle must have no expression (neutral).");
            }
            var types = new HashSet<Type>();
            foreach (var module in DiscoverModules())
            {
                if (!types.Add(module.GetType())) errors.Add("Duplicate module type: " + module.GetType().Name + ".");
                module.ValidateConfiguration(errors);
                if (defaultIdle && defaultIdle.Animation)
                    module.ValidateActivity(new ActivityRequest(defaultIdle, defaultIdle.Animation, requestId + 1), errors);
            }
            return errors;
        }

        List<string> ValidateActivity(ActivityDefinition activity)
        {
            var errors = ValidateConfiguration();
            if (!activity) { errors.Add("Activity must not be null; use ReturnToIdle to reset."); return errors; }
            foreach (var error in activity.ValidateConfiguration()) errors.Add("Activity: " + error);
            CharacterDefinition.ValidateUniqueIds(new CharacterDefinition[]
            {
                defaultIdle, activity, defaultIdle ? defaultIdle.Animation : null, activity.Animation,
                defaultIdle ? defaultIdle.Expression : null, activity.Expression
            }, errors);
            if (errors.Count == 0)
                foreach (var module in DiscoverModules())
                    module.ValidateActivity(new ActivityRequest(activity, defaultIdle.Animation, requestId + 1), errors);
            return errors;
        }

        IEnumerable<CharacterModule> DiscoverModules()
        {
            if (!modulesRoot || modulesRoot == transform || !modulesRoot.IsChildOf(transform)) yield break;
            foreach (var module in modulesRoot.GetComponentsInChildren<CharacterModule>(true))
                // A nested character owns its own modules even if it lies beneath this Modules root.
                if (module.GetComponentInParent<Character>(true) == this) yield return module;
        }

        bool EnsureInitialized()
        {
            if (initialized) return true;
            var errors = ValidateConfiguration();
            if (errors.Count > 0) return Reject(errors);
            modules.Clear();
            modules.AddRange(DiscoverModules());
            try
            {
                foreach (var module in modules) module.Initialize(this);
                initialized = true;
                return true;
            }
            catch (Exception exception)
            {
                Shutdown();
                Debug.LogException(exception, this);
                return false;
            }
        }

        void Dispatch(ActivityDefinition activity)
        {
            CurrentActivity = activity;
            currentRequest = new ActivityRequest(activity, defaultIdle.Animation, ++requestId);
            completionPending = false;
            dispatching = true;
            try
            {
                foreach (var module in modules)
                    if (module && module.isActiveAndEnabled && module is IActivityReceiver receiver)
                        receiver.ApplyActivity(currentRequest);
            }
            finally { dispatching = false; }
            // Deliver the original command to every receiver before a synchronous completion resets it.
            if (completionPending) ReturnToIdle();
        }

        internal bool CompleteActivity(CharacterModule source, ulong completedRequestId)
        {
            if (!initialized || !source || source.Owner != this || !modules.Contains(source)
                || !source.isActiveAndEnabled || completedRequestId != requestId || !CurrentActivity
                || !currentRequest.CompletesOnAnimationEnd) return false;
            if (dispatching) completionPending = true;
            else ReturnToIdle();
            return true;
        }

        internal void ReapplyActivity(CharacterModule module)
        {
            if (initialized && CurrentActivity && module && module.isActiveAndEnabled
                && module is IActivityReceiver receiver) receiver.ApplyActivity(currentRequest);
        }

        void Shutdown()
        {
            ++requestId; // Invalidate callbacks retained by a disabled/destroyed module.
            initialized = false;
            CurrentActivity = null;
            completionPending = false;
            for (int i = modules.Count - 1; i >= 0; i--)
                if (modules[i])
                    try { modules[i].Shutdown(); }
                    catch (Exception exception) { Debug.LogException(exception, modules[i]); }
            modules.Clear();
        }

        bool Reject(IEnumerable<string> errors)
        {
            Debug.LogWarning(name + ": " + string.Join("\n", errors), this);
            return false;
        }
    }
}
