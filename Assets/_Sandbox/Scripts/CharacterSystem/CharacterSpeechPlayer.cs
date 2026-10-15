using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    /// <summary>Exclusive playback owner for the character's existing uLipSync AudioSource.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Character System/Speech Player")]
    public sealed class CharacterSpeechPlayer : MonoBehaviour, ICharacterSpeechPlayback
    {
        [SerializeField] AudioSource audioSource;
        [SerializeField] uLipSync.uLipSync lipSync;
        [SerializeField] AudioClip clip;
        [SerializeField, Tooltip("Optional override; otherwise retain uLipSync's assigned profile.")] uLipSync.Profile profile;

        AudioSource playingSource;
        AudioClip playingClip;
        ulong playbackId;
        int startFrame;
        bool observedPlaying;
        CharacterSpeechStatus status = CharacterSpeechStatus.Interrupted;

        [ShowInInspector, ReadOnly] public ulong CurrentPlaybackId => playbackId;

        public void ValidateConfiguration(List<string> errors)
        {
            if (!isActiveAndEnabled) errors.Add("Speech Player must be enabled on an active object.");
            if (!clip) errors.Add("Speech Player: assign a voice Clip.");
            else if (clip.loadState == AudioDataLoadState.Failed) errors.Add("Speech Player: voice Clip failed to load.");
            if (!audioSource) errors.Add("Speech Player: assign the character's AudioSource.");
            else
            {
                if (!audioSource.isActiveAndEnabled) errors.Add("Speech Player: AudioSource must be active and enabled.");
                if (audioSource.loop) errors.Add("Speech Player: disable Loop on the AudioSource.");
                if (audioSource.playOnAwake) errors.Add("Speech Player: disable Play On Awake on the AudioSource.");
                if (audioSource.pitch <= 0 || float.IsNaN(audioSource.pitch) || float.IsInfinity(audioSource.pitch))
                    errors.Add("Speech Player: AudioSource Pitch must be finite and greater than zero.");
            }
            if (!lipSync) errors.Add("Speech Player: assign uLipSync.");
            else
            {
                if (!lipSync.isActiveAndEnabled) errors.Add("Speech Player: uLipSync must be active and enabled.");
                if (!profile && !lipSync.profile) errors.Add("Speech Player: assign a Profile here or on uLipSync.");
                var analysedSource = lipSync.audioSourceProxy
                    ? lipSync.audioSourceProxy.GetComponent<AudioSource>() : lipSync.GetComponent<AudioSource>();
                if (audioSource && analysedSource != audioSource)
                    errors.Add("Speech Player: AudioSource must match the source analysed by uLipSync (including its proxy).");
            }
            var owner = GetComponentInParent<Character>(true);
            if (owner && ((audioSource && audioSource.GetComponentInParent<Character>(true) != owner)
                || (lipSync && lipSync.GetComponentInParent<Character>(true) != owner)))
                errors.Add("Speech Player: audio and uLipSync must belong to the same Character.");
        }

        public bool TryPlay(out ulong acceptedPlaybackId, out string error)
        {
            acceptedPlaybackId = 0;
            if (!Application.IsPlaying(gameObject)) { error = "Speech playback requires Play Mode."; return false; }
            var errors = new List<string>();
            ValidateConfiguration(errors);
            if (errors.Count > 0) { error = string.Join("\n", errors); return false; }
            Stop(playbackId);
            playingSource = audioSource;
            playingClip = clip;
            if (profile) lipSync.profile = profile;
            playingSource.Stop();
            playingSource.clip = playingClip;
            playingSource.Play();
            acceptedPlaybackId = ++playbackId;
            startFrame = Time.frameCount;
            observedPlaying = playingSource.isPlaying;
            status = CharacterSpeechStatus.Playing;
            error = null;
            return true;
        }

        public CharacterSpeechStatus GetStatus(ulong requestedPlaybackId)
        {
            if (requestedPlaybackId == 0 || requestedPlaybackId != playbackId) return CharacterSpeechStatus.Interrupted;
            if (status != CharacterSpeechStatus.Playing) return status;
            if (!isActiveAndEnabled || !playingSource || !playingSource.isActiveAndEnabled || playingSource.clip != playingClip)
                return status = CharacterSpeechStatus.Interrupted;
            if (playingSource.isPlaying) { observedPlaying = true; return status; }
            if (AudioListener.pause && !playingSource.ignoreListenerPause) return status;
            if (Time.frameCount == startFrame || playingClip.loadState == AudioDataLoadState.Loading) return status;
            return status = observedPlaying ? CharacterSpeechStatus.Completed : CharacterSpeechStatus.Failed;
        }

        public void Stop(ulong requestedPlaybackId)
        {
            if (requestedPlaybackId == 0 || requestedPlaybackId != playbackId) return;
            if (status == CharacterSpeechStatus.Playing && playingSource && playingSource.clip == playingClip)
                playingSource.Stop();
            status = CharacterSpeechStatus.Interrupted;
            playingSource = null;
            playingClip = null;
        }

        void OnDisable() => Stop(playbackId);
        void OnDestroy() => Stop(playbackId);
    }
}
