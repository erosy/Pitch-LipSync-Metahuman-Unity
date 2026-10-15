using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem.Tests
{
    /// <summary>Runtime-compatible services for deterministic tests without audio or Play Mode.</summary>
    public sealed class FlowTestServices : MonoBehaviour, ICharacterReadiness, ICharacterSpeechPlayback
    {
        public bool IsReady { get; set; } = true;
        public bool Invalid, FailOnPlay;
        public int Plays, Stops;
        public ulong PlaybackId;
        public CharacterSpeechStatus Status = CharacterSpeechStatus.Playing;
        public Action OnPlay;

        public void ValidateConfiguration(List<string> errors)
        { if (Invalid) errors.Add("Test services are invalid."); }

        public bool TryPlay(out ulong playbackId, out string error)
        {
            playbackId = 0;
            error = FailOnPlay ? "Test speech start failed." : null;
            if (FailOnPlay) return false;
            playbackId = ++PlaybackId;
            Plays++;
            Status = CharacterSpeechStatus.Playing;
            OnPlay?.Invoke();
            return true;
        }

        public CharacterSpeechStatus GetStatus(ulong playbackId)
            => playbackId == PlaybackId ? Status : CharacterSpeechStatus.Interrupted;

        public void Stop(ulong playbackId)
        {
            if (playbackId != PlaybackId || playbackId == 0) return;
            Stops++;
            Status = CharacterSpeechStatus.Interrupted;
        }
    }
}
