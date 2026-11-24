using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sandbox.CharacterSystem.Tests
{
    public sealed class TalkDemoControllerTests
    {
        readonly List<Object> assets = new List<Object>();
        Scene scene;
        TalkDemoController controller;
        TalkDemoSpeechInput speech;
        TalkDemoController.Persona ethan, liora;
        ActivityDefinition idle, wave;

        [SetUp]
        public void SetUp()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            scene = EditorSceneManager.NewPreviewScene();
            idle = Activity("talk-idle", true);
            wave = Activity("talk-wave", false);
            ethan = Rig("Ethan Reeve");
            liora = Rig("Liora Minji");
            var root = Root("Talk Demo test");
            controller = root.AddComponent<TalkDemoController>();
            Set(controller, "characterCamera", Root("Camera").AddComponent<Camera>());
            Set(controller, "personas", new[] { ethan, liora });
            speech = root.AddComponent<TalkDemoSpeechInput>();
            speech.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            speech.CancelAll("Test cleanup.");
            EditorSceneManager.ClosePreviewScene(scene);
            for (int i = assets.Count - 1; i >= 0; i--) Object.DestroyImmediate(assets[i]);
            assets.Clear();
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
        }

        [Test]
        public void WaitsForReadinessThenWavesOnceAndRemainsIdle()
        {
            var readiness = (FlowTestServices)ethan.readinessProvider;
            readiness.IsReady = false;
            Assert.That(controller.ActivatePersona(ethan.key), Is.True);
            controller.Tick(); controller.Tick();
            Assert.That(controller.State, Is.EqualTo(TalkDemoController.DemoState.WarmingUp));
            Assert.That(ethan.character.CurrentActivity, Is.SameAs(idle));
            readiness.IsReady = true;
            controller.Tick();
            Assert.That(controller.State, Is.EqualTo(TalkDemoController.DemoState.WarmingUp));
            controller.Tick();
            Assert.That(ethan.character.CurrentActivity, Is.SameAs(wave));
            Assert.That(controller.State, Is.EqualTo(TalkDemoController.DemoState.Waving));
            Assert.That(ethan.character.TryGetModule<AnimationModule>(out var body), Is.True);
            body.Tick(1);
            controller.Tick(); controller.Tick();
            Assert.That(controller.IsReady, Is.True);
            Assert.That(ethan.character.CurrentActivity, Is.SameAs(idle));
            Assert.That(readiness.Plays, Is.Zero, "A silent greeting must never request speech.");
        }

        [Test]
        public void SamePersonaDoesNotRestartGreetingAndUnknownKeyDoesNotSwitch()
        {
            controller.ActivatePersona(ethan.key);
            controller.Tick(); controller.Tick();
            var request = ethan.character.CurrentRequestId;
            Assert.That(controller.ActivatePersona(ethan.key), Is.True);
            Assert.That(ethan.character.CurrentRequestId, Is.EqualTo(request));
            Assert.That(controller.ActivatePersona("unknown"), Is.False);
            Assert.That(controller.SelectedPersonaKey, Is.EqualTo(ethan.key));
            Assert.That(ethan.character.gameObject.activeSelf, Is.True);
            Assert.That(liora.character.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SwitchingDuringWaveRestartsReadinessForOnlyTheNewPersona()
        {
            controller.ActivatePersona(ethan.key);
            controller.Tick(); controller.Tick();
            Assert.That(controller.ActivatePersona(liora.key), Is.True);
            Assert.That(ethan.character.gameObject.activeSelf, Is.False);
            Assert.That(liora.character.gameObject.activeSelf, Is.True);
            Assert.That(controller.State, Is.EqualTo(TalkDemoController.DemoState.WarmingUp));
            controller.Tick();
            Assert.That(controller.State, Is.EqualTo(TalkDemoController.DemoState.WarmingUp));
            controller.Tick();
            Assert.That(liora.character.CurrentActivity, Is.SameAs(wave));
        }

        [Test]
        public void PublicSelectionCannotActivateCharactersInEditMode()
        {
            Assert.That(controller.SelectPersona(ethan.key), Is.False);
            Assert.That(controller.SelectedPersonaKey, Is.Null);
            Assert.That(speech.TryEnqueueSpeechJson("{}", out _), Is.False);
        }

        [Test]
        public void PersonaTestEnvelopeChoosesSelectedPersonaWithoutChangingSelection()
        {
            controller.ActivatePersona(liora.key);
            string audio = System.Convert.ToBase64String(Pcm16WaveTests.Wave());
            string payload = "{\"format\":\"unity-voice-persona-test/v1\",\"test_cases\":["
                + "{\"persona_key\":\"ethan\",\"audio\":{\"encoding\":\"base64\",\"mime_type\":\"audio/wav\",\"audio_b64\":\"invalid\"}},"
                + "{\"persona_key\":\"liora\",\"audio\":{\"encoding\":\"base64\",\"mime_type\":\"audio/wav\",\"audio_b64\":\"" + audio + "\"}}]}";
            Assert.That(speech.EnqueueJson(payload, out var error), Is.True, error);
            Assert.That(speech.PendingReplies, Is.EqualTo(1));
            Assert.That(controller.SelectedPersonaKey, Is.EqualTo(liora.key));
        }

        [Test]
        public void PersonaTestWithoutSelectionReportsMissingActivation()
        {
            Assert.That(speech.EnqueueJson("{\"format\":\"unity-voice-persona-test/v1\",\"test_cases\":[]}", out var error), Is.False);
            StringAssert.Contains("No persona is active", error);
        }

        [Test]
        public void QueuesDuringWaveThenPlaysFifoAndReleasesClips()
        {
            controller.ActivatePersona(ethan.key);
            Assert.That(speech.EnqueueJson(Json("first"), out _), Is.True);
            Assert.That(speech.EnqueueJson(Json("second"), out _), Is.True);
            speech.Tick();
            Assert.That(((FlowTestServices)ethan.speechPlayer).Plays, Is.Zero);
            Ready(); speech.Tick();
            var player = (FlowTestServices)ethan.speechPlayer;
            Assert.That(speech.CurrentRequestId, Is.EqualTo("first"));
            Assert.That(controller.State, Is.EqualTo(TalkDemoController.DemoState.Speaking));
            Assert.That(player.LastClip.frequency, Is.EqualTo(24000));
            var firstClip = player.LastClip;
            player.Status = CharacterSpeechStatus.Completed;
            speech.Tick();
            Assert.That(firstClip == null, Is.True, "Completed runtime clips must be released.");
            Assert.That(controller.State, Is.EqualTo(TalkDemoController.DemoState.Idle));
            Assert.That(ethan.character.CurrentActivity, Is.SameAs(idle));
            speech.Tick();
            Assert.That(speech.CurrentRequestId, Is.EqualTo("second"));
        }

        [Test]
        public void AudioGateRetainsQueuedRepliesUntilPlaybackIsAllowed()
        {
            controller.ActivatePersona(ethan.key); Ready();
            speech.PlaybackAllowed = false;
            Assert.That(speech.EnqueueJson(Json("waiting-for-audio"), out _), Is.True);
            speech.Tick();
            Assert.That(speech.PendingReplies, Is.EqualTo(1));
            Assert.That(speech.CurrentRequestId, Is.Null);
            Assert.That(controller.IsReady, Is.True);
            speech.PlaybackAllowed = true;
            speech.Tick();
            Assert.That(speech.CurrentRequestId, Is.EqualTo("waiting-for-audio"));
        }

        [Test]
        public void PersonaSwitchCancelsCurrentAndPendingRepliesImmediately()
        {
            controller.ActivatePersona(ethan.key); Ready();
            speech.EnqueueJson(Json("playing"), out _); speech.EnqueueJson(Json("queued"), out _);
            speech.Tick();
            var player = (FlowTestServices)ethan.speechPlayer;
            var clip = player.LastClip;
            Assert.That(controller.ActivatePersona(liora.key), Is.True);
            Assert.That(speech.CurrentRequestId, Is.Null);
            Assert.That(speech.PendingReplies, Is.Zero);
            Assert.That(player.Stops, Is.EqualTo(1));
            Assert.That(clip == null, Is.True);
            Assert.That(speech.EnqueueJson(Json("late-old-reply"), out _), Is.False);
            speech.Tick();
            Assert.That(((FlowTestServices)liora.speechPlayer).Plays, Is.Zero);
        }

        [Test]
        public void QueueCapacityAndBadPayloadsDoNotReplaceAcceptedReplies()
        {
            controller.ActivatePersona(ethan.key);
            Assert.That(speech.EnqueueJson("not json", out _), Is.False);
            Assert.That(speech.EnqueueJson(Json("bad-base64", "!!!"), out _), Is.False);
            Assert.That(speech.EnqueueJson(Json("bad-wave", "AAAA"), out _), Is.False);
            for (int i = 0; i < 8; i++) Assert.That(speech.EnqueueJson(Json(i.ToString()), out _), Is.True);
            Assert.That(speech.EnqueueJson(Json("overflow"), out var error), Is.False);
            StringAssert.Contains("full", error);
            Assert.That(speech.PendingReplies, Is.EqualTo(8));
        }

        [Test]
        public void FailedPlaybackReturnsToIdleAndAllowsTheNextReply()
        {
            controller.ActivatePersona(ethan.key); Ready();
            var player = (FlowTestServices)ethan.speechPlayer;
            player.FailOnPlay = true;
            speech.EnqueueJson(Json("failure"), out _); speech.EnqueueJson(Json("next"), out _);
            speech.Tick();
            Assert.That(speech.CurrentRequestId, Is.Null);
            Assert.That(controller.IsReady, Is.True);
            Assert.That(player.LastClip == null, Is.True);
            player.FailOnPlay = false;
            speech.Tick();
            Assert.That(speech.CurrentRequestId, Is.EqualTo("next"));
        }

        [Test]
        public void StopClearsSpeechAndEmitsCancellationForEveryReply()
        {
            controller.ActivatePersona(ethan.key); Ready();
            var events = new List<string>();
            speech.SpeechEvent += e => { if (e.Event == "cancelled") events.Add(e.RequestId); };
            speech.EnqueueJson(Json("current"), out _); speech.EnqueueJson(Json("pending"), out _);
            speech.Tick(); speech.CancelAll("Stop.");
            Assert.That(events, Is.EquivalentTo(new[] { "current", "pending" }));
            Assert.That(controller.IsReady, Is.True);
            Assert.That(speech.PendingReplies, Is.Zero);
        }

        string Json(string id, string data = null) => JsonUtility.ToJson(new TalkDemoSpeechInput.SpeechRequest {
            request_id = id, persona_key = ethan.key, mime_type = "audio/wav",
            audio_base64 = data ?? System.Convert.ToBase64String(Pcm16WaveTests.Wave())
        });

        void Ready()
        {
            controller.Tick(); controller.Tick();
            ethan.character.TryGetModule<AnimationModule>(out var body);
            body.Tick(1); controller.Tick();
            Assert.That(controller.IsReady, Is.True);
        }

        TalkDemoController.Persona Rig(string key)
        {
            var root = Root(key);
            var character = root.AddComponent<Character>();
            var rig = new GameObject("Rig"); rig.transform.SetParent(root.transform);
            new GameObject("Bone").transform.SetParent(rig.transform);
            var animator = rig.AddComponent<Animator>();
            var modules = new GameObject("Modules"); modules.transform.SetParent(root.transform);
            var body = modules.AddComponent<AnimationModule>(); Set(body, "animator", animator);
            Set(character, "modulesRoot", modules.transform); Set(character, "defaultIdle", idle);
            Assert.That(character.Initialize(), Is.True);
            var services = root.AddComponent<FlowTestServices>();
            return new TalkDemoController.Persona {
                key = key, character = character, readinessProvider = services, speechPlayer = services, speaking = idle,
                greeting = wave, cameraPosition = new Vector3(0, 1.6f, 1.15f), cameraTarget = new Vector3(0, 1.6f, 0)
            };
        }

        GameObject Root(string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        ActivityDefinition Activity(string id, bool loop)
        {
            var clip = new AnimationClip(); assets.Add(clip);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Bone", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0, 0, 1, 1));
            var animation = ScriptableObject.CreateInstance<AnimationDefinition>(); assets.Add(animation);
            Set(animation, "id", id + "-animation"); Set(animation, "displayName", id);
            Set(animation, "clip", clip); Set(animation, "loop", loop); Set(animation, "crossfadeDuration", 0f);
            var activity = ScriptableObject.CreateInstance<ActivityDefinition>(); assets.Add(activity);
            Set(activity, "id", id); Set(activity, "displayName", id);
            Set(activity, "animation", animation); Set(activity, "isIdle", loop);
            return activity;
        }

        static void Set(object target, string field, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (info != null) { info.SetValue(target, value); return; }
                type = type.BaseType;
            }
            Assert.Fail("Missing field: " + field);
        }
    }
}
