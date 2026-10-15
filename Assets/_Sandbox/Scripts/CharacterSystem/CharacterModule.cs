using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    /// <summary>Module lifetime is owned by Character; subclasses keep instance playback state here.</summary>
    public abstract class CharacterModule : MonoBehaviour
    {
        public Character Owner { get; private set; }
        public bool IsInitialized => Owner;

        /// <summary>Report configuration errors without changing playback or initializing resources.</summary>
        public virtual void ValidateConfiguration(List<string> errors) { }

        /// <summary>Preflight a resolved activity before Character replaces any module outputs.</summary>
        public virtual void ValidateActivity(ActivityRequest request, List<string> errors) { }

        internal void Initialize(Character owner)
        {
            if (Owner == owner) return;
            if (Owner) throw new InvalidOperationException("Module is already initialized by another character.");
            Owner = owner;
            try { OnInitialize(); }
            catch
            {
                try { OnShutdown(); }
                finally { Owner = null; }
                throw;
            }
        }

        internal void Shutdown()
        {
            if (!Owner) return;
            try { OnShutdown(); }
            finally { Owner = null; }
        }

        protected virtual void OnInitialize() { }
        /// <summary>Release resources and restore previews. Must also be safe after partial initialization.</summary>
        protected virtual void OnShutdown() { }
        /// <summary>Temporarily stop output and restore previews when this module is disabled.</summary>
        protected virtual void OnSuspend() { }

        protected virtual void OnEnable()
        {
            if (Owner && Application.isPlaying) Owner.ReapplyActivity(this);
        }

        protected virtual void OnDisable()
        {
            if (Owner) OnSuspend();
        }

        protected virtual void OnDestroy() => Shutdown();

        /// <summary>For the body receiver to report its explicitly supplied one-shot animation ending.</summary>
        protected bool CompleteActivity(ulong requestId) => Owner && Owner.CompleteActivity(this, requestId);
    }
}
