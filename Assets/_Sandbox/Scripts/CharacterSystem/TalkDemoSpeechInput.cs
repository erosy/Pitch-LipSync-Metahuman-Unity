using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    [RequireComponent(typeof(TalkDemoController)), DisallowMultipleComponent]
    public sealed class TalkDemoSpeechInput : MonoBehaviour
    {
        [Serializable]
        public sealed class SpeechRequest
        {
            public string request_id;
            public string persona_key;
            public string mime_type;
            public string audio_base64;
        }

        [Serializable] sealed class PersonaTestPayload
        {
            public string format;
            public PersonaTestCase[] test_cases;
        }
        [Serializable] sealed class PersonaTestCase
        {
            public string persona_key;
            public PersonaTestAudio audio;
        }
        [Serializable] sealed class PersonaTestAudio
        {
            public string mime_type;
            public string encoding;
            public string audio_b64;
        }

        public readonly struct Notification
        {
            public readonly string Event, RequestId, PersonaKey, Message;
            internal Notification(string kind, SpeechRequest request, string message)
            { Event = kind; RequestId = request.request_id; PersonaKey = request.persona_key; Message = message; }
        }

        sealed class Reply
        {
            internal SpeechRequest Request;
            internal byte[] Bytes;
            internal Pcm16Wave.Info Info;
        }

        const int QueueCapacity = 8;
        [SerializeField] TalkDemoController controller;
        [SerializeField, TextArea(4, 9)] string testJson;
        readonly Queue<Reply> pending = new Queue<Reply>();
        Reply current;
        AudioClip runtimeClip;
        IRuntimeCharacterSpeechPlayback player;
        ulong playbackId, activityId;

        [ShowInInspector, ReadOnly] public int PendingReplies => pending.Count;
        [ShowInInspector, ReadOnly] public string CurrentRequestId => current?.Request.request_id;
        [ShowInInspector, ReadOnly] public string LastError { get; private set; }
        public event Action<Notification> SpeechEvent;
        public bool PlaybackAllowed { get; set; } = true;

        void Reset() => controller = GetComponent<TalkDemoController>();
        void OnEnable() => Attach();
        internal void Attach()
        {
            if (!controller) controller = GetComponent<TalkDemoController>();
            controller.PersonaChanging -= CancelForSelection;
            controller.PersonaChanging += CancelForSelection;
        }
        void OnDisable()
        {
            if (controller) controller.PersonaChanging -= CancelForSelection;
            CancelAll("Speech input disabled.");
        }
        void Update() { if (Application.IsPlaying(gameObject)) Tick(); }

        // A void entry point is compatible with Unity's browser SendMessage bridge.
        public void ReceiveSpeechJson(string json) => TryEnqueueSpeechJson(json, out _);

        public bool TryEnqueueSpeechJson(string json, out string error)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled)
            { error = LastError = "Speech input requires Play Mode and an enabled component."; return false; }
            return EnqueueJson(json, out error);
        }

        internal bool EnqueueJson(string json, out string error)
        {
            error = null;
            SpeechRequest request;
            if (string.IsNullOrWhiteSpace(json) || json.Length > Pcm16Wave.MaxBytes * 4 / 3 + 4096)
                return Reject("Speech JSON is empty or too large.", out error);
            try { request = JsonUtility.FromJson<SpeechRequest>(json); }
            catch (ArgumentException) { return Reject("Invalid speech JSON.", out error); }
            if (request != null && request.audio_base64 == null)
            {
                var payload = JsonUtility.FromJson<PersonaTestPayload>(json);
                if (payload != null && payload.format == "unity-voice-persona-test/v1")
                {
                    if (!controller || string.IsNullOrEmpty(controller.SelectedPersonaKey))
                        return Reject("No persona is active. Enter Play Mode and select Ethan or Liora before sending the test payload.", out error);
                    PersonaTestCase match = null;
                    if (payload.test_cases != null) foreach (var item in payload.test_cases)
                    {
                        if (item == null) continue;
                        string key = item.persona_key == "ethan" ? "Ethan Reeve"
                            : item.persona_key == "liora" ? "Liora Minji" : item.persona_key;
                        if (controller && key == controller.SelectedPersonaKey)
                        { match = item; break; }
                    }
                    if (match == null || match.audio == null)
                        return Reject("The test payload has no matching audio for persona '" + controller.SelectedPersonaKey
                            + "'. Paste the complete JSON including both test_cases.", out error);
                    if (!string.Equals(match.audio.encoding, "base64", StringComparison.OrdinalIgnoreCase))
                        return Reject("The test payload must use base64 audio encoding.", out error);
                    request = new SpeechRequest {
                        request_id = Guid.NewGuid().ToString("N"), persona_key = controller.SelectedPersonaKey,
                        mime_type = match.audio.mime_type, audio_base64 = match.audio.audio_b64
                    };
                }
            }
            if (request == null || string.IsNullOrWhiteSpace(request.request_id)
                || string.IsNullOrEmpty(request.persona_key) || string.IsNullOrEmpty(request.audio_base64))
                return Reject("Provide request_id, persona_key, mime_type and audio_base64.", out error);
            if (!controller || controller.SelectedPersona == null
                || controller.State == TalkDemoController.DemoState.Error || controller.State == TalkDemoController.DemoState.Inactive)
                return Reject("Select an available persona before sending speech.", out error);
            if (request.persona_key != controller.SelectedPersonaKey)
                return Reject("Reply persona does not match the selected character.", out error);
            if (!string.Equals(request.mime_type, "audio/wav", StringComparison.OrdinalIgnoreCase))
                return Reject("The demo expects mime_type audio/wav.", out error);
            if (pending.Count >= QueueCapacity) return Reject("Speech queue is full (eight pending replies).", out error);
            byte[] bytes;
            try { bytes = Convert.FromBase64String(request.audio_base64); }
            catch (FormatException) { return Reject("audio_base64 is not valid Base64.", out error); }
            if (!Pcm16Wave.TryRead(bytes, out var info, out error)) { LastError = error; return false; }
            // Retain encoded PCM only until playback; do not retain the larger Base64 string.
            request.audio_base64 = null;
            pending.Enqueue(new Reply { Request = request, Bytes = bytes, Info = info });
            LastError = null;
            SpeechEvent?.Invoke(new Notification("queued", request, null));
            return true;
        }

        internal void Tick()
        {
            if (!controller || controller.SelectedPersona == null
                || controller.State == TalkDemoController.DemoState.Error || controller.State == TalkDemoController.DemoState.Inactive)
            { CancelAll("Character is unavailable."); return; }
            if (current != null)
            {
                var character = controller.SelectedPersona.character;
                if (!character || !character.isActiveAndEnabled || character.CurrentRequestId != activityId)
                { Finish("error", "Speech activity was interrupted."); return; }
                var status = player.GetStatus(playbackId);
                if (status == CharacterSpeechStatus.Playing) return;
                Finish(status == CharacterSpeechStatus.Completed ? "completed" : "error",
                    status == CharacterSpeechStatus.Completed ? null : "Speech playback failed or was interrupted.");
                return;
            }
            if (!PlaybackAllowed || !controller.IsReady || pending.Count == 0) return;
            current = pending.Dequeue();
            try
            {
                var persona = controller.SelectedPersona;
                player = persona.speechPlayer as IRuntimeCharacterSpeechPlayback;
                if (player == null) { Finish("error", "Assign a runtime Speech Player."); return; }
                var samples = Pcm16Wave.Decode(current.Bytes, current.Info);
                int channels = current.Info.Channels, frequency = current.Info.SampleRate;
#if UNITY_WEBGL && !UNITY_EDITOR
                samples = Pcm16Wave.ResampleMono(samples, channels, frequency, AudioSettings.outputSampleRate);
                channels = 1; frequency = AudioSettings.outputSampleRate;
#endif
                runtimeClip = AudioClip.Create(current.Request.request_id, samples.Length / channels, channels, frequency, false);
                current.Bytes = null;
                if (!runtimeClip || !runtimeClip.SetData(samples, 0)) { Finish("error", "Could not create the reply audio clip."); return; }
                if (!controller.BeginSpeaking(out activityId)) { Finish("error", "Speaking activity was rejected."); return; }
                if (!player.TryPlayClip(runtimeClip, out playbackId, out var error)) { Finish("error", error); return; }
                SpeechEvent?.Invoke(new Notification("speaking", current.Request, null));
            }
            catch (Exception exception) { Finish("error", exception.Message); }
        }

        void Finish(string kind, string message)
        {
            var finished = current;
            current = null;
            ReleasePlayback();
            if (message != null) LastError = message;
            if (finished != null) SpeechEvent?.Invoke(new Notification(kind, finished.Request, message));
        }

        void ReleasePlayback()
        {
            player?.Stop(playbackId);
            player = null;
            playbackId = 0;
            if (controller) controller.EndSpeaking(activityId);
            activityId = 0;
            if (runtimeClip)
            {
                if (Application.IsPlaying(gameObject)) Destroy(runtimeClip);
                else DestroyImmediate(runtimeClip);
                runtimeClip = null;
            }
        }

        void CancelForSelection() => CancelAll("Persona changed.");
        internal void CancelAll(string reason)
        {
            var cancelled = pending.ToArray();
            pending.Clear();
            Finish("cancelled", current != null ? reason : null);
            foreach (var reply in cancelled) SpeechEvent?.Invoke(new Notification("cancelled", reply.Request, reason));
        }

        bool Reject(string message, out string error) { error = LastError = message; return false; }

        [Button("Send Selected Persona Fixture"), EnableIf("@UnityEngine.Application.isPlaying")]
        public void SendFixture()
        {
            var persona = controller ? controller.SelectedPersona : null;
            if (persona == null || !persona.speechFixture) { LastError = "Select a persona with a WAV fixture."; return; }
            ReceiveSpeechJson(JsonUtility.ToJson(new SpeechRequest {
                request_id = Guid.NewGuid().ToString("N"), persona_key = persona.key,
                mime_type = "audio/wav", audio_base64 = Convert.ToBase64String(persona.speechFixture.bytes)
            }));
        }

        [Button("Send Test JSON"), EnableIf("@UnityEngine.Application.isPlaying")]
        public void SendTestJson() => ReceiveSpeechJson(testJson);

        [Button("Stop And Clear Speech"), EnableIf("@UnityEngine.Application.isPlaying")]
        public void StopAndClearSpeech()
        {
            if (Application.IsPlaying(gameObject)) CancelAll("Stopped by user.");
        }

        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (!controller) { errors.Add("Assign the Talk Demo controller."); return errors; }
            foreach (var persona in controller.Personas)
            {
                if (persona == null || !persona.character) { errors.Add("Missing persona character."); continue; }
                if (!(persona.speechPlayer is IRuntimeCharacterSpeechPlayback playback))
                    errors.Add(persona.key + ": assign a runtime Speech Player.");
                else
                {
                    if (persona.speechPlayer.GetComponentInParent<Character>(true) != persona.character)
                        errors.Add(persona.key + ": Speech Player must belong to this character.");
                    if (persona.character.gameObject.activeInHierarchy) playback.ValidateRuntimeConfiguration(errors);
                }
                errors.AddRange(persona.character.ValidateActivity(persona.speaking));
                if (persona.speaking && persona.speaking.Animation && !persona.speaking.Animation.Loop)
                    errors.Add(persona.key + ": speaking must use a looping animation or idle fallback.");
                if (!persona.speechFixture || !Pcm16Wave.TryRead(persona.speechFixture.bytes, out _, out _))
                    errors.Add(persona.key + ": assign a valid PCM16 WAV fixture.");
            }
            return errors;
        }
    }
}
