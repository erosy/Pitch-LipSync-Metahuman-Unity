namespace Sandbox.CharacterSystem
{
    /// <summary>Identifies both the finished command and the automatic Idle that replaced it.</summary>
    public readonly struct ActivityCompletion
    {
        public ActivityRequest Request { get; }
        public ulong RequestId => Request.RequestId;
        public ulong IdleRequestId { get; }

        internal ActivityCompletion(ActivityRequest request, ulong idleRequestId)
        {
            Request = request;
            IdleRequestId = idleRequestId;
        }
    }
}
