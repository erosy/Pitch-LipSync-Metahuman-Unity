using System;

namespace Sandbox.CharacterSystem
{
    /// <summary>One run's activity ownership and completion subscription. Never stored on assets.</summary>
    public sealed class CharacterFlowContext : IDisposable
    {
        public Character Character { get; }
        public ICharacterSpeechPlayback Speech { get; }
        ulong ownedRequestId, completedRequestId;
        bool issuing, disposed;

        public bool HasActivity => ownedRequestId != 0;

        public bool OwnsActivity => !disposed && ownedRequestId != 0 && Character
            && Character.isActiveAndEnabled && Character.IsInitialized
            && Character.CurrentRequestId == ownedRequestId;

        public CharacterFlowContext(Character character, ICharacterSpeechPlayback speech)
        {
            Character = character;
            Speech = speech;
            Character.ActivityCompleted += OnActivityCompleted;
        }

        public bool PlayActivity(ActivityDefinition activity, out ulong requestId)
        {
            requestId = 0;
            if (disposed || !Character || !Character.isActiveAndEnabled) return false;
            completedRequestId = 0;
            issuing = true;
            try
            {
                if (!Character.PlayActivity(activity, out requestId)) return false;
                // A synchronous completion already recorded the automatic Idle's ID.
                if (completedRequestId != requestId) ownedRequestId = requestId;
                return OwnsActivity;
            }
            finally { issuing = false; }
        }

        public bool WasActivityCompleted(ulong requestId) => requestId != 0 && completedRequestId == requestId;

        void OnActivityCompleted(ActivityCompletion completion)
        {
            if (disposed || (!issuing && ownedRequestId != completion.RequestId)) return;
            completedRequestId = completion.RequestId;
            ownedRequestId = completion.IdleRequestId;
        }

        public void ReturnToIdleIfOwned()
        {
            if (OwnsActivity && Character.CurrentActivity != Character.DefaultIdle)
                PlayActivity(Character.DefaultIdle, out _);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Character) Character.ActivityCompleted -= OnActivityCompleted;
        }
    }
}
