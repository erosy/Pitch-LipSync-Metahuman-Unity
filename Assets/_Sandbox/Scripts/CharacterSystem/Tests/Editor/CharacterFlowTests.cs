using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sandbox.CharacterSystem.Tests
{
    public sealed class CharacterFlowTests
    {
        sealed class Rig
        {
            internal Character Character;
            internal AnimationModule Body;
            internal RecordingCharacterModule Recorder;
            internal FlowTestServices Services;
            internal CharacterFlowController Controller;
        }

        readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        readonly List<Rig> rigs = new List<Rig>();
        Scene scene;
        Rig rig;
        ActivityDefinition idle, wave, thinking, speaking;
        ActivityFlowStateDefinition waveState, thinkingState, pauseState;
        SpeechFlowStateDefinition speechState;
        CharacterFlowDefinition greeting, response;

        [SetUp]
        public void SetUp()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            scene = EditorSceneManager.NewPreviewScene();
            idle = Activity("flow-idle", true);
            wave = Activity("flow-wave", false);
            thinking = Activity("flow-thinking", false);
            speaking = Definition<ActivityDefinition>("flow-speaking");
            waveState = ActivityState("wave-state", wave);
            thinkingState = ActivityState("thinking-state", thinking);
            pauseState = ActivityState("pause-state", null, true);
            speechState = Definition<SpeechFlowStateDefinition>("speech-state");
            Set(speechState, "activity", speaking);
            greeting = Flow("greeting", waveState);
            response = Flow("response", thinkingState, pauseState, speechState);
            rig = CreateRig();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs) if (item.Controller) item.Controller.EndActivation();
            rigs.Clear();
            EditorSceneManager.ClosePreviewScene(scene);
            for (int i = assets.Count - 1; i >= 0; i--) if (assets[i]) UnityEngine.Object.DestroyImmediate(assets[i]);
            assets.Clear();
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
        }

        [Test]
        public void ReadinessIgnoresClicksAndAddsOneFrameBeforeGreeting()
        {
            rig.Services.IsReady = false;
            for (int i = 0; i < 3; i++) { rig.Controller.Tick(1); rig.Controller.PlayResponse(); }
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(idle));
            Assert.That(rig.Controller.IsReady, Is.False);
            rig.Services.IsReady = true;
            rig.Controller.Tick(0);
            Assert.That(rig.Controller.IsReady, Is.False);
            rig.Controller.Tick(0);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(wave));
        }

        [Test]
        public void NoProviderStillWaitsAnAdditionalFrame()
        {
            Set(rig.Controller, "readinessProvider", null);
            rig.Controller.Tick(0);
            Assert.That(rig.Controller.IsReady, Is.False);
            rig.Controller.Tick(0);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(wave));
        }

        [Test]
        public void InitializationMustFinishBeforeReadinessCanAdvance()
        {
            Invoke(rig.Character, "OnDisable");
            rig.Controller.Tick(1); rig.Controller.Tick(1);
            Assert.That(rig.Controller.IsReady, Is.False);
            Assert.That(rig.Character.Initialize(), Is.True);
            Ready();
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(wave));
        }

        [Test]
        public void GreetingWaitsForActualAnimationAndFinishesInIdle()
        {
            Ready();
            rig.Body.Tick(.5f); rig.Controller.Tick(10);
            Assert.That(rig.Controller.CurrentState, Is.SameAs(waveState));
            rig.Body.Tick(.5f);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(idle));
            rig.Controller.Tick(0);
            Assert.That(rig.Controller.IsRunning, Is.False);
            Assert.That(rig.Services.Plays, Is.Zero);
        }

        [Test]
        public void ResponseWaitsForThinkingThenOneSecondIdleAndActualSpeechEnd()
        {
            Ready(); rig.Controller.PlayResponse();
            rig.Services.OnPlay = () => Assert.That(rig.Character.CurrentActivity, Is.SameAs(speaking));
            rig.Controller.Tick(100);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(thinking));
            FinishThinking();
            Assert.That(rig.Controller.CurrentState, Is.SameAs(pauseState));
            rig.Controller.Tick(.75f);
            Assert.That(rig.Services.Plays, Is.Zero);
            rig.Controller.Tick(.25f);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(speaking));
            Assert.That(rig.Services.Plays, Is.EqualTo(1));
            rig.Controller.Tick(100);
            Assert.That(rig.Controller.IsRunning, Is.True);
            rig.Services.Status = CharacterSpeechStatus.Completed;
            rig.Controller.Tick(0);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(idle));
            Assert.That(rig.Controller.IsRunning, Is.False);
        }

        [Test]
        public void ClickDuringGreetingReplacesItWithThinking()
        {
            Ready();
            ulong old = rig.Character.CurrentRequestId;
            rig.Controller.PlayResponse();
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(thinking));
            Assert.That(rig.Recorder.Finish(old), Is.False);
        }

        [Test]
        public void ClickDuringThinkingRestartsAndRejectsStaleCompletion()
        {
            Ready(); rig.Controller.PlayResponse();
            ulong old = rig.Character.CurrentRequestId;
            rig.Body.Tick(.6f);
            rig.Controller.PlayResponse();
            Assert.That(rig.Character.CurrentRequestId, Is.GreaterThan(old));
            Assert.That(rig.Recorder.Finish(old), Is.False);
            Assert.That(rig.Body.ActivityPlaybackTime, Is.EqualTo(0));
            rig.Controller.Tick(0);
            Assert.That(rig.Controller.CurrentState, Is.SameAs(thinkingState));
        }

        [Test]
        public void ClickDuringIdlePauseDiscardsElapsedDelay()
        {
            Ready(); rig.Controller.PlayResponse(); FinishThinking();
            rig.Controller.Tick(.9f);
            rig.Controller.PlayResponse(); FinishThinking();
            rig.Controller.Tick(.2f);
            Assert.That(rig.Services.Plays, Is.Zero);
            Assert.That(rig.Controller.CurrentState, Is.SameAs(pauseState));
        }

        [Test]
        public void ClickDuringSpeechStopsItAndOldPlaybackCannotStopTheNewSpeech()
        {
            BeginSpeech();
            ulong old = rig.Services.PlaybackId;
            rig.Controller.PlayResponse();
            Assert.That(rig.Services.Stops, Is.EqualTo(1));
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(thinking));
            FinishThinking(); rig.Controller.Tick(1);
            Assert.That(rig.Services.PlaybackId, Is.GreaterThan(old));
            rig.Services.Stop(old);
            Assert.That(rig.Services.Status, Is.EqualTo(CharacterSpeechStatus.Playing));
            rig.Controller.Tick(0);
            Assert.That(rig.Controller.CurrentState, Is.SameAs(speechState));
        }

        [Test]
        public void ExternalActivityCancelsFlowAndPreservesNewOutput()
        {
            BeginSpeech();
            var external = Definition<ActivityDefinition>("external");
            rig.Character.PlayActivity(external);
            rig.Controller.Tick(1);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(external));
            Assert.That(rig.Controller.IsRunning, Is.False);
            Assert.That(rig.Services.Stops, Is.EqualTo(1));
        }

        [Test]
        public void ExternalCommandAfterNaturalCompletionIsNotMistakenForIdle()
        {
            Ready(); rig.Controller.PlayResponse();
            rig.Body.Tick(1);
            var external = Definition<ActivityDefinition>("external-after-completion");
            rig.Character.PlayActivity(external);
            rig.Controller.Tick(1);
            Assert.That(rig.Controller.IsRunning, Is.False);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(external));
            Assert.That(rig.Services.Plays, Is.Zero);
        }

        [Test]
        public void EarlierCompletionObserverCannotTransferExternalActivityOwnershipToFlow()
        {
            var external = Definition<ActivityDefinition>("observer-activity");
            rig.Character.ActivityCompleted += completion => rig.Character.PlayActivity(external);
            Ready();
            rig.Body.Tick(1); rig.Controller.Tick(0);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(external));
            Assert.That(rig.Controller.IsRunning, Is.False);
        }

        [Test]
        public void SynchronousCompletionRetainsOriginalAcceptedIdAndDefersTransition()
        {
            rig.Recorder.CompleteDuringApply = true;
            Ready();
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(idle));
            Assert.That(rig.Controller.CurrentState, Is.SameAs(waveState));
            rig.Controller.Tick(0);
            Assert.That(rig.Controller.IsRunning, Is.False);
            rig.Controller.PlayResponse();
            Assert.That(rig.Controller.CurrentState, Is.SameAs(thinkingState));
            rig.Controller.Tick(0);
            Assert.That(rig.Controller.CurrentState, Is.SameAs(pauseState));
            rig.Controller.Tick(1);
            Assert.That(rig.Services.Plays, Is.EqualTo(1));
        }

        [Test]
        public void NaturalCompletionEventIsDeliveredOnceAfterEveryReceiverGetsIdle()
        {
            var notices = new List<ActivityCompletion>();
            rig.Character.ActivityCompleted += completion =>
            {
                notices.Add(completion);
                Assert.That(rig.Character.CurrentActivity, Is.SameAs(idle));
                Assert.That(rig.Recorder.Requests[rig.Recorder.Requests.Count - 1].Activity, Is.SameAs(idle));
            };
            Assert.That(rig.Character.PlayActivity(wave, out var accepted), Is.True);
            rig.Body.Tick(1); rig.Body.Tick(2);
            Assert.That(notices.Count, Is.EqualTo(1));
            Assert.That(notices[0].RequestId, Is.EqualTo(accepted));
            Assert.That(notices[0].IdleRequestId, Is.EqualTo(rig.Character.CurrentRequestId));
            Assert.That(rig.Recorder.Finish(accepted), Is.False);
            rig.Character.ReturnToIdle();
            Assert.That(notices.Count, Is.EqualTo(1));
        }

        [Test]
        public void SynchronousCommandOverloadReturnsFinishedRequestRatherThanIdleId()
        {
            rig.Recorder.CompleteDuringApply = true;
            ulong before = rig.Character.CurrentRequestId;
            Assert.That(rig.Character.PlayActivity(wave, out var accepted), Is.True);
            Assert.That(accepted, Is.EqualTo(before + 1));
            Assert.That(rig.Character.CurrentRequestId, Is.EqualTo(accepted + 1));
        }

        [Test]
        public void InvalidReplacementDoesNotStopValidSpeech()
        {
            BeginSpeech();
            var invalid = Flow("invalid", (CharacterFlowStateDefinition)null);
            LogAssert.Expect(LogType.Warning, new Regex("assign a state definition"));
            Assert.That(rig.Controller.TryPlayFlow(invalid), Is.False);
            Assert.That(rig.Services.Stops, Is.Zero);
            Assert.That(rig.Controller.CurrentState, Is.SameAs(speechState));
        }

        [Test]
        public void SpeechStartFailureReturnsOwnedCharacterToIdle()
        {
            Ready(); rig.Controller.PlayResponse(); FinishThinking();
            rig.Services.FailOnPlay = true;
            LogAssert.Expect(LogType.Warning, new Regex("Test speech start failed"));
            rig.Controller.Tick(1);
            Assert.That(rig.Controller.IsRunning, Is.False);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(idle));
            Assert.That(rig.Services.Plays, Is.Zero);
        }

        [TestCase(CharacterSpeechStatus.Failed)]
        [TestCase(CharacterSpeechStatus.Interrupted)]
        public void SpeechFailureCleansUpAndReturnsToIdle(CharacterSpeechStatus status)
        {
            BeginSpeech(); rig.Services.Status = status;
            LogAssert.Expect(LogType.Warning, new Regex("Speech playback failed or was interrupted"));
            rig.Controller.Tick(0);
            Assert.That(rig.Controller.IsRunning, Is.False);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(idle));
        }

        [Test]
        public void EveryActivationRestartsReadinessAndGreetingAndCleansSubscriptions()
        {
            BeginSpeech();
            Assert.That(CompletionSubscriberCount(), Is.EqualTo(1));
            rig.Controller.EndActivation();
            Assert.That(rig.Services.Stops, Is.EqualTo(1));
            Assert.That(CompletionSubscriberCount(), Is.Zero);
            Assert.That(rig.Controller.IsReady, Is.False);
            rig.Controller.BeginActivation();
            Ready();
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(wave));
            Assert.That(CompletionSubscriberCount(), Is.EqualTo(1));
            rig.Controller.CancelFlow();
            Assert.That(CompletionSubscriberCount(), Is.Zero);
        }

        [Test]
        public void DisableAfterExternalCommandDoesNotResetItsActivity()
        {
            Ready();
            rig.Character.PlayActivity(speaking);
            rig.Controller.EndActivation();
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(speaking));
        }

        [Test]
        public void SharedDefinitionsHaveIndependentProgressAndSpeech()
        {
            var other = CreateRig();
            Ready(); other.Controller.Tick(0); other.Controller.Tick(0);
            rig.Controller.PlayResponse(); FinishThinking(); rig.Controller.Tick(.5f);
            Assert.That(other.Character.CurrentActivity, Is.SameAs(wave));
            other.Controller.PlayResponse();
            rig.Controller.Tick(.5f);
            Assert.That(rig.Services.Plays, Is.EqualTo(1));
            Assert.That(other.Services.Plays, Is.Zero);
            Assert.That(other.Character.CurrentActivity, Is.SameAs(thinking));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDurationIsRejectedWithoutReplacingGreeting(float duration)
        {
            Ready(); Set(pauseState, "duration", duration);
            LogAssert.Expect(LogType.Warning, new Regex("Duration must be finite"));
            rig.Controller.PlayResponse();
            Assert.That(rig.Controller.CurrentState, Is.SameAs(waveState));
        }

        [Test]
        public void MissingBodyOrLoopingAnimationCannotWaitForAnimationEnd()
        {
            Set(wave.Animation, "loop", true);
            StringAssert.Contains("non-looping", string.Join("\n", greeting.ValidateConfiguration()));
            Set(wave.Animation, "loop", false);
            rig.Body.enabled = false;
            StringAssert.Contains("enabled Animation Module", string.Join("\n", greeting.ValidateFor(rig.Character, rig.Services)));
        }

        [Test]
        public void LosingBodyDuringAnimationWaitFailsRatherThanHanging()
        {
            Ready(); rig.Body.enabled = false;
            LogAssert.Expect(LogType.Warning, new Regex("Animation End lost its active"));
            rig.Controller.Tick(0);
            Assert.That(rig.Controller.IsRunning, Is.False);
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(idle));
        }

        [Test]
        public void AnotherCharactersSpeechPlayerIsRejectedBeforeReplacement()
        {
            var other = CreateRig(); Ready();
            Set(rig.Controller, "speechPlayer", other.Services);
            LogAssert.Expect(LogType.Warning, new Regex("Speech Player must belong"));
            rig.Controller.PlayResponse();
            Assert.That(rig.Controller.CurrentState, Is.SameAs(waveState));
        }

        [Test]
        public void InvalidSpeechServiceIsRejectedBeforeInterruptingGreeting()
        {
            Ready(); rig.Services.Invalid = true;
            LogAssert.Expect(LogType.Warning, new Regex("Test services are invalid"));
            rig.Controller.PlayResponse();
            Assert.That(rig.Controller.CurrentState, Is.SameAs(waveState));
            Set(rig.Controller, "speechPlayer", null);
            StringAssert.Contains("requires a Speech Player", string.Join("\n", rig.Controller.ValidateConfiguration()));
        }

        [Test]
        public void ScaledDelayDoesNotAdvanceWithZeroOrInvalidDelta()
        {
            Ready(); rig.Controller.PlayResponse(); FinishThinking();
            rig.Controller.Tick(0); rig.Controller.Tick(float.NaN); rig.Controller.Tick(-1); rig.Controller.Tick(float.PositiveInfinity);
            Assert.That(rig.Controller.CurrentState, Is.SameAs(pauseState));
            rig.Controller.Tick(1);
            Assert.That(rig.Controller.CurrentState, Is.SameAs(speechState));
        }

        [Test]
        public void ZeroDurationAndInsertedCustomStateAdvanceWithoutRunnerChanges()
        {
            var custom = Definition<ImmediateFlowStateDefinition>("custom-state");
            Set(pauseState, "duration", 0f);
            var expanded = Flow("expanded", custom, thinkingState, pauseState, speechState);
            Ready(); Assert.That(rig.Controller.TryPlayFlow(expanded), Is.True);
            rig.Controller.Tick(0);
            Assert.That(rig.Controller.CurrentState, Is.SameAs(thinkingState));
            FinishThinking(); rig.Controller.Tick(0);
            Assert.That(rig.Services.Plays, Is.EqualTo(1));
        }

        [Test]
        public void SameStateAssetCanAppearTwiceWithFreshRuntimeProgress()
        {
            var repeated = Flow("repeated", pauseState, pauseState);
            Ready(); rig.Controller.TryPlayFlow(repeated);
            rig.Controller.Tick(1); rig.Controller.Tick(.5f);
            Assert.That(rig.Controller.IsRunning, Is.True);
            rig.Controller.Tick(.5f);
            Assert.That(rig.Controller.IsRunning, Is.False);
        }

        [Test]
        public void EditingFlowListDoesNotReorderAnActiveRun()
        {
            Ready(); rig.Controller.PlayResponse();
            SetStates(response, speechState);
            FinishThinking();
            Assert.That(rig.Controller.CurrentState, Is.SameAs(pauseState));
        }

        [Test]
        public void SpeechPlayerReportsMissingReferencesAndNeverPlaysInEditMode()
        {
            var player = rig.Character.gameObject.AddComponent<CharacterSpeechPlayer>();
            var errors = new List<string>(); player.ValidateConfiguration(errors);
            var text = string.Join("\n", errors);
            StringAssert.Contains("voice Clip", text);
            StringAssert.Contains("AudioSource", text);
            StringAssert.Contains("uLipSync", text);
            Assert.That(player.TryPlay(out var id, out var error), Is.False);
            Assert.That(id, Is.Zero);
            StringAssert.Contains("requires Play Mode", error);
            Assert.That(player.GetStatus(99), Is.EqualTo(CharacterSpeechStatus.Interrupted));
        }

        [Test]
        public void SpeechPlayerValidatesExistingRouteAndRejectsLoopingOrUnanalysedAudio()
        {
            var root = rig.Character.gameObject;
            var source = root.AddComponent<AudioSource>(); source.playOnAwake = false;
            var analyser = root.AddComponent<uLipSync.uLipSync>();
            var profile = ScriptableObject.CreateInstance<uLipSync.Profile>(); assets.Add(profile);
            analyser.profile = profile;
            var clip = AudioClip.Create("Validation only", 100, 1, 44100, false); assets.Add(clip);
            var player = root.AddComponent<CharacterSpeechPlayer>();
            Set(player, "audioSource", source); Set(player, "lipSync", analyser); Set(player, "clip", clip);
            var errors = new List<string>(); player.ValidateConfiguration(errors);
            Assert.That(errors, Is.Empty);
            source.loop = true;
            player.ValidateConfiguration(errors);
            StringAssert.Contains("disable Loop", string.Join("\n", errors));
            source.loop = false;
            var otherSource = new GameObject("Other audio"); otherSource.transform.SetParent(root.transform);
            var wrongSource = otherSource.AddComponent<AudioSource>(); wrongSource.playOnAwake = false;
            Set(player, "audioSource", wrongSource);
            errors.Clear(); player.ValidateConfiguration(errors);
            StringAssert.Contains("source analysed by uLipSync", string.Join("\n", errors));
        }

        [Test]
        public void RuntimeSpeechAcceptsAnUnassignedClipRouteButNeverPlaysInEditMode()
        {
            var root = rig.Character.gameObject;
            var source = root.AddComponent<AudioSource>(); source.playOnAwake = false;
            var analyser = root.AddComponent<uLipSync.uLipSync>();
            var profile = ScriptableObject.CreateInstance<uLipSync.Profile>(); assets.Add(profile);
            analyser.profile = profile;
            var player = root.AddComponent<CharacterSpeechPlayer>();
            Set(player, "audioSource", source); Set(player, "lipSync", analyser);
            var errors = new List<string>(); player.ValidateRuntimeConfiguration(errors);
            Assert.That(errors, Is.Empty);
            player.ValidateConfiguration(errors);
            StringAssert.Contains("voice Clip", string.Join("\n", errors));
            var runtime = AudioClip.Create("Runtime validation", 2400, 1, 24000, false); assets.Add(runtime);
            Assert.That(player.TryPlayClip(runtime, out var id, out var error), Is.False);
            Assert.That(id, Is.Zero);
            StringAssert.Contains("requires Play Mode", error);
            Assert.That(source.clip, Is.Null);
        }

        [Test]
        public void RuntimeStopDetachesOnlyItsOwnClipAndRetainsTheAssignedAsset()
        {
            var root = rig.Character.gameObject;
            var source = root.AddComponent<AudioSource>(); source.playOnAwake = false;
            var runtime = AudioClip.Create("Owned runtime", 2400, 1, 24000, false); assets.Add(runtime);
            var assigned = AudioClip.Create("Assigned asset", 2400, 1, 24000, false); assets.Add(assigned);
            source.clip = runtime;
            var player = root.AddComponent<CharacterSpeechPlayer>();
            Set(player, "clip", assigned);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CharacterSpeechPlayer).GetField("playbackId", flags).SetValue(player, 2UL);
            typeof(CharacterSpeechPlayer).GetField("playingSource", flags).SetValue(player, source);
            typeof(CharacterSpeechPlayer).GetField("playingClip", flags).SetValue(player, runtime);
            typeof(CharacterSpeechPlayer).GetField("runtimePlayback", flags).SetValue(player, true);
            typeof(CharacterSpeechPlayer).GetField("status", flags).SetValue(player, CharacterSpeechStatus.Completed);
            player.Stop(1);
            Assert.That(source.clip, Is.SameAs(runtime));
            player.Stop(2);
            Assert.That(source.clip, Is.Null);
            Assert.That(runtime, Is.Not.Null, "The caller owns runtime clip disposal.");
            Assert.That(new SerializedObject(player).FindProperty("clip").objectReferenceValue, Is.SameAs(assigned));
        }

        [Test]
        public void SpeechPlayerPollsCompletionAndRejectsStaleStopTokensWithoutPlayingAudio()
        {
            var root = rig.Character.gameObject;
            var source = root.AddComponent<AudioSource>(); source.playOnAwake = false;
            var clip = AudioClip.Create("Never played", 100, 1, 44100, false); assets.Add(clip);
            source.clip = clip;
            var player = root.AddComponent<CharacterSpeechPlayer>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CharacterSpeechPlayer).GetField("playbackId", flags).SetValue(player, 2UL);
            typeof(CharacterSpeechPlayer).GetField("playingSource", flags).SetValue(player, source);
            typeof(CharacterSpeechPlayer).GetField("playingClip", flags).SetValue(player, clip);
            typeof(CharacterSpeechPlayer).GetField("startFrame", flags).SetValue(player, int.MinValue);
            typeof(CharacterSpeechPlayer).GetField("observedPlaying", flags).SetValue(player, true);
            typeof(CharacterSpeechPlayer).GetField("status", flags).SetValue(player, CharacterSpeechStatus.Playing);
            player.Stop(1);
            Assert.That(player.GetStatus(1), Is.EqualTo(CharacterSpeechStatus.Interrupted));
            Assert.That(player.GetStatus(2), Is.EqualTo(CharacterSpeechStatus.Completed));
            player.Stop(2);
            Assert.That(player.GetStatus(2), Is.EqualTo(CharacterSpeechStatus.Interrupted));
            Assert.That(source.isPlaying, Is.False);
        }

        [Test]
        public void CharacterReadinessUsesWarmupOrFallbackEvenWhenWarmupIsDisabled()
        {
            var warmupType = Type.GetType("CharacterHairStartupWarmup, Assembly-CSharp", true);
            var fallbackType = Type.GetType("CharacterHairFallback, Assembly-CSharp", true);
            var adapterType = Type.GetType("CharacterReadiness, Assembly-CSharp", true);
            var root = rig.Character.gameObject;
            var warmup = (MonoBehaviour)root.AddComponent(warmupType);
            var fallback = root.AddComponent(fallbackType);
            var adapter = (MonoBehaviour)root.AddComponent(adapterType);
            Set(adapter, "startupWarmup", warmup); Set(adapter, "hairFallback", fallback);
            var readiness = (ICharacterReadiness)adapter;
            Assert.That(readiness.IsReady, Is.False);
            warmupType.GetField("<IsReady>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(warmup, true);
            Assert.That(readiness.IsReady, Is.True);
            warmupType.GetField("<IsReady>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(warmup, false);
            warmup.enabled = false;
            fallbackType.GetField("<IsUsingFallback>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(fallback, true);
            Assert.That(readiness.IsReady, Is.True);
            var errors = new List<string>(); readiness.ValidateConfiguration(errors);
            Assert.That(errors, Is.Empty);
            Set(rig.Controller, "readinessProvider", adapter);
            Ready();
            Assert.That(rig.Character.CurrentActivity, Is.SameAs(wave));
        }

        [Test]
        public void WarmupReactivationResetsReadinessAndInterruptionRestoresVisibility()
        {
            var type = Type.GetType("CharacterHairStartupWarmup, Assembly-CSharp", true);
            var root = rig.Character.gameObject;
            var warmup = (MonoBehaviour)root.AddComponent(type);
            var renderer = root.AddComponent<MeshRenderer>();
            Set(warmup, "poseWarmupFrames", 1); Set(warmup, "framesAfterReset", 1);
            var animator = rig.Body.Animator;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            var initialize = type.GetMethod("InitializeHidden", BindingFlags.Instance | BindingFlags.NonPublic);
            var ready = type.GetProperty("IsReady");
            for (int activation = 0; activation < 2; activation++)
            {
                Assert.That(ready.GetValue(warmup), Is.False);
                var routine = (System.Collections.IEnumerator)initialize.Invoke(warmup, null);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(renderer.enabled, Is.False);
                Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
                while (routine.MoveNext()) { }
                Assert.That(ready.GetValue(warmup), Is.True);
                Assert.That(renderer.enabled, Is.True);
                Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely));
                // Non-ExecuteAlways lifecycle callbacks are driven explicitly in
                // Edit Mode; do not enter Play Mode to exercise reactivation.
                Invoke(warmup, "OnDisable");
                Assert.That(ready.GetValue(warmup), Is.False);
                Invoke(warmup, "OnEnable");
            }
            Assert.That(type.GetProperty("ResetCount").GetValue(warmup), Is.EqualTo(2));
            var interrupted = (System.Collections.IEnumerator)initialize.Invoke(warmup, null);
            interrupted.MoveNext();
            Invoke(warmup, "OnDisable");
            Assert.That(renderer.enabled, Is.True);
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely));
            Assert.That(ready.GetValue(warmup), Is.False);
        }

        [Test]
        public void MissingFallbackLeavesWarmupEnabledAndDoesNotClaimReadiness()
        {
            var warmupType = Type.GetType("CharacterHairStartupWarmup, Assembly-CSharp", true);
            var fallbackType = Type.GetType("CharacterHairFallback, Assembly-CSharp", true);
            var root = rig.Character.gameObject;
            var warmup = (MonoBehaviour)root.AddComponent(warmupType);
            var fallback = root.AddComponent(fallbackType);
            Set(fallback, "startupWarmup", warmup);
            LogAssert.Expect(LogType.Error, new Regex("assign a mesh/card hair fallback"));
            var applied = fallbackType.GetMethod("ApplyFallback", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fallback, null);
            Assert.That(applied, Is.False);
            Assert.That(warmup.enabled, Is.True);
            Assert.That(fallbackType.GetProperty("IsUsingFallback").GetValue(fallback), Is.False);
        }

        [Test]
        public void FallbackActivatesScalpAndFacialReplacementsTogether()
        {
            var fallbackType = Type.GetType("CharacterHairFallback, Assembly-CSharp", true);
            var root = rig.Character.gameObject;
            var fallback = root.AddComponent(fallbackType);
            var scalp = new GameObject("Scalp cards"); scalp.transform.SetParent(root.transform); scalp.SetActive(false);
            var brows = new GameObject("Brow cards"); brows.transform.SetParent(root.transform); brows.SetActive(false);
            var lashes = new GameObject("Lash cards"); lashes.transform.SetParent(root.transform); lashes.SetActive(false);
            Set(fallback, "fallbackRoot", scalp);
            Set(fallback, "additionalFallbackRoots", new[] { brows, lashes });
            Assert.That(fallbackType.GetMethod("ApplyFallback", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fallback, null), Is.True);
            Assert.That(scalp.activeSelf && brows.activeSelf && lashes.activeSelf, Is.True);
            Assert.That(fallbackType.GetProperty("IsUsingFallback").GetValue(fallback), Is.True);
        }

        [Test]
        public void CharacterWithoutStrandHairDoesNotRequireWarmup()
        {
            var type = Type.GetType("CharacterReadiness, Assembly-CSharp", true);
            var readiness = (ICharacterReadiness)rig.Character.gameObject.AddComponent(type);
            var errors = new List<string>(); readiness.ValidateConfiguration(errors);
            Assert.That(errors, Is.Empty);
            Assert.That(readiness.IsReady, Is.True);
        }

        [Test]
        public void EthanPrefabPreservesRenamedHairScriptsAndTheirReferences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan/Ethan.prefab");
            var warmupType = Type.GetType("CharacterHairStartupWarmup, Assembly-CSharp", true);
            var fallbackType = Type.GetType("CharacterHairFallback, Assembly-CSharp", true);
            var readinessType = Type.GetType("CharacterReadiness, Assembly-CSharp", true);
            var warmup = prefab.GetComponent(warmupType);
            var fallback = prefab.GetComponent(fallbackType);
            var readiness = prefab.GetComponent(readinessType);
            Assert.That(warmup && fallback && readiness, Is.True);
            var fallbackData = new SerializedObject(fallback);
            Assert.That(fallbackData.FindProperty("startupWarmup").objectReferenceValue, Is.SameAs(warmup));
            Assert.That(fallbackData.FindProperty("fallbackRoot").objectReferenceValue.name, Is.EqualTo("Hair_Cards_Baked"));
            var readinessData = new SerializedObject(readiness);
            Assert.That(readinessData.FindProperty("startupWarmup").objectReferenceValue, Is.SameAs(warmup));
            Assert.That(readinessData.FindProperty("hairFallback").objectReferenceValue, Is.SameAs(fallback));
        }

        void Ready() { rig.Controller.Tick(0); rig.Controller.Tick(0); }
        void FinishThinking() { rig.Body.Tick(1); rig.Controller.Tick(0); }
        void BeginSpeech() { Ready(); rig.Controller.PlayResponse(); FinishThinking(); rig.Controller.Tick(1); }

        int CompletionSubscriberCount()
        {
            var field = typeof(Character).GetField("ActivityCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
            return (field.GetValue(rig.Character) as Delegate)?.GetInvocationList().Length ?? 0;
        }

        Rig CreateRig()
        {
            var root = new GameObject("Flow test character");
            SceneManager.MoveGameObjectToScene(root, scene);
            var item = new Rig { Character = root.AddComponent<Character>() };
            var model = new GameObject("Rig"); model.transform.SetParent(root.transform);
            new GameObject("Bone").transform.SetParent(model.transform);
            var animator = model.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var modules = new GameObject("Modules"); modules.transform.SetParent(root.transform);
            item.Body = modules.AddComponent<AnimationModule>(); Set(item.Body, "animator", animator);
            item.Recorder = modules.AddComponent<RecordingCharacterModule>();
            Set(item.Character, "modulesRoot", modules.transform); Set(item.Character, "defaultIdle", idle);
            item.Services = root.AddComponent<FlowTestServices>();
            item.Controller = root.AddComponent<CharacterFlowController>();
            Set(item.Controller, "character", item.Character); Set(item.Controller, "readinessProvider", item.Services);
            Set(item.Controller, "speechPlayer", item.Services); Set(item.Controller, "greetingFlow", greeting);
            Set(item.Controller, "responseFlow", response);
            rigs.Add(item);
            Assert.That(item.Character.Initialize(), Is.True);
            item.Controller.BeginActivation();
            return item;
        }

        T Definition<T>(string id) where T : CharacterDefinition
        {
            var definition = ScriptableObject.CreateInstance<T>(); assets.Add(definition);
            definition.name = id;
            Set(definition, "id", id); Set(definition, "displayName", id);
            return definition;
        }

        ActivityDefinition Activity(string id, bool loop)
        {
            var clip = new AnimationClip(); assets.Add(clip);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Bone", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0, 0, 1, 1));
            var animation = Definition<AnimationDefinition>(id + "-animation");
            Set(animation, "clip", clip); Set(animation, "loop", loop); Set(animation, "crossfadeDuration", 0f);
            var activity = Definition<ActivityDefinition>(id);
            Set(activity, "animation", animation); Set(activity, "isIdle", loop);
            return activity;
        }

        ActivityFlowStateDefinition ActivityState(string id, ActivityDefinition activity, bool defaultIdle = false)
        {
            var state = Definition<ActivityFlowStateDefinition>(id);
            Set(state, "activity", activity); Set(state, "useDefaultIdle", defaultIdle);
            Set(state, "completion", (int)(defaultIdle ? ActivityFlowCompletion.Duration : ActivityFlowCompletion.AnimationEnd));
            return state;
        }

        CharacterFlowDefinition Flow(string id, params CharacterFlowStateDefinition[] states)
        {
            var flow = Definition<CharacterFlowDefinition>(id); SetStates(flow, states); return flow;
        }

        static void SetStates(CharacterFlowDefinition flow, params CharacterFlowStateDefinition[] states)
        {
            var serialized = new SerializedObject(flow);
            var property = serialized.FindProperty("states"); property.arraySize = states.Length;
            for (int i = 0; i < states.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = states[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Invoke(object target, string method) => target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        static void Set(UnityEngine.Object target, string field, object value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, field);
            if (value == null || value is UnityEngine.Object) property.objectReferenceValue = value as UnityEngine.Object;
            else if (value is string text) property.stringValue = text;
            else if (value is bool flag) property.boolValue = flag;
            else if (value is float number) property.floatValue = number;
            else if (value is int integer) property.intValue = integer;
            else if (value is UnityEngine.Object[] objects)
            {
                property.arraySize = objects.Length;
                for (int i = 0; i < objects.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
            }
            else throw new ArgumentException(field);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
