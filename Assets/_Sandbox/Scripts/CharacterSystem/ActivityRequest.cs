namespace Sandbox.CharacterSystem
{
    /// <summary>One immutable command shared by all receivers. Never stored on definition assets.</summary>
    public readonly struct ActivityRequest
    {
        public ActivityDefinition Activity { get; }
        public ulong RequestId { get; }
        /// <summary>Includes the default idle animation when the activity omits a body animation.</summary>
        public AnimationDefinition Animation { get; }
        /// <summary>Null explicitly means neutral, rather than retaining the previous expression.</summary>
        public ExpressionDefinition Expression => Activity.Expression;
        public bool IsIdle => Activity.IsIdle;
        public bool CompletesOnAnimationEnd => Activity.Animation && !Activity.Animation.Loop;

        internal ActivityRequest(ActivityDefinition activity, AnimationDefinition defaultAnimation, ulong requestId)
        {
            Activity = activity;
            RequestId = requestId;
            Animation = activity.Animation ? activity.Animation : defaultAnimation;
        }
    }

    public interface IActivityReceiver
    {
        /// <summary>Replace prior output and cancel previews. A null expression means neutral.</summary>
        void ApplyActivity(ActivityRequest request);
    }
}
