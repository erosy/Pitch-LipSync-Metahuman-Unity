using System.Collections.Generic;

namespace Sandbox.CharacterSystem.Tests
{
    // Runtime-compatible test component; never included in a normal player build.
    // No Animator, renderer, animation evaluation, or Play Mode required.
    public class RecordingCharacterModule : CharacterModule, IActivityReceiver
    {
        public readonly List<ActivityRequest> Requests = new List<ActivityRequest>();
        public int Initializations { get; private set; }
        public int Shutdowns { get; private set; }
        public bool CompleteDuringApply { get; set; }
        public bool Invalid { get; set; }
        public bool ThrowOnInitialize { get; set; }

        public override void ValidateConfiguration(List<string> errors)
        {
            if (Invalid) errors.Add("Test module configuration is invalid.");
        }

        protected override void OnInitialize()
        {
            Initializations++;
            if (ThrowOnInitialize) throw new System.InvalidOperationException("Test initialization failure.");
        }
        protected override void OnShutdown() => Shutdowns++;

        public void ApplyActivity(ActivityRequest request)
        {
            Requests.Add(request);
            if (CompleteDuringApply && request.CompletesOnAnimationEnd) CompleteActivity(request.RequestId);
        }

        public bool Finish(ulong requestId) => CompleteActivity(requestId);
    }
}
