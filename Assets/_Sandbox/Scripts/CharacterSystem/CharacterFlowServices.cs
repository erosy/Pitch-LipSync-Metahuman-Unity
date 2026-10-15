using System.Collections.Generic;

namespace Sandbox.CharacterSystem
{
    public interface ICharacterReadiness
    {
        bool IsReady { get; }
        void ValidateConfiguration(List<string> errors);
    }

    public enum CharacterSpeechStatus { Playing, Completed, Interrupted, Failed }

    /// <summary>Playback IDs prevent an old state from stopping newer speech.</summary>
    public interface ICharacterSpeechPlayback
    {
        void ValidateConfiguration(List<string> errors);
        bool TryPlay(out ulong playbackId, out string error);
        CharacterSpeechStatus GetStatus(ulong playbackId);
        void Stop(ulong playbackId);
    }
}
