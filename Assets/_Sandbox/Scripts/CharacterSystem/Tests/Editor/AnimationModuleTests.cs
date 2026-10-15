using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sandbox.CharacterSystem.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sandbox.CharacterSystem.Tests
{
    public sealed class AnimationModuleTests
    {
        readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        readonly List<AnimationPlayback> mixers = new List<AnimationPlayback>();
        Scene scene;
        Character character;
        AnimationModule module;
        OtherRecordingModule face;
        Animator animator;
        Transform bone;
        AnimationDefinition idleAnimation;
        ActivityDefinition idle;

        [SetUp]
        public void SetUp()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Animation contract test");
            SceneManager.MoveGameObjectToScene(root, scene);
            character = root.AddComponent<Character>();
            var rig = new GameObject("Rig"); rig.transform.SetParent(root.transform);
            bone = new GameObject("Bone").transform; bone.SetParent(rig.transform);
            bone.localPosition = new Vector3(3, 2, 1);
            animator = rig.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var modules = new GameObject("Modules"); modules.transform.SetParent(root.transform);
            var body = new GameObject("Animation"); body.transform.SetParent(modules.transform);
            module = body.AddComponent<AnimationModule>();
            face = modules.AddComponent<OtherRecordingModule>();
            Set(module, "animator", animator);
            Set(character, "modulesRoot", modules.transform);
            idleAnimation = Animation("idle", true);
            idle = Activity("idle", idleAnimation, true);
            Set(character, "defaultIdle", idle);
        }

        [TearDown]
        public void TearDown()
        {
            AnimationPreviewEditorBridge.StopAll();
            foreach (var mixer in mixers) mixer.Dispose();
            mixers.Clear();
            EditorSceneManager.ClosePreviewScene(scene);
            for (int i = assets.Count - 1; i >= 0; i--)
                if (assets[i]) UnityEngine.Object.DestroyImmediate(assets[i]);
            assets.Clear();
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
        }

        [Test]
        public void DefinitionLoopsAnImportedNonLoopingClip()
        {
            var definition = Animation("forced-loop", true);
            Assert.That(definition.Clip.isLooping, Is.False);
            var mixer = Mixer(); mixer.Play(definition);
            Assert.That(mixer.Advance(1.25f), Is.False);
            Assert.That(mixer.Time, Is.EqualTo(.25).Within(.001));
            Assert.That(bone.localPosition.x, Is.EqualTo(2.5f).Within(.01));
            Assert.That(definition.Clip.isLooping, Is.False);
        }

        [Test]
        public void DefinitionPlaysAnImportedLoopingClipOnceAndHoldsItsLastPose()
        {
            var definition = Animation("forced-once", false);
            var settings = AnimationUtility.GetAnimationClipSettings(definition.Clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(definition.Clip, settings);
            Assert.That(definition.Clip.isLooping, Is.True);
            var mixer = Mixer(); mixer.Play(definition);
            Assert.That(mixer.Advance(1.25f), Is.True);
            Assert.That(mixer.Time, Is.EqualTo(1));
            Assert.That(bone.localPosition.x, Is.EqualTo(10).Within(.01));
            Assert.That(mixer.Advance(2), Is.False);
            Assert.That(bone.localPosition.x, Is.EqualTo(10).Within(.01));
            Assert.That(definition.Clip.isLooping, Is.True);
        }

        [Test]
        public void PlaybackSpeedAndZeroOrInvalidDeltaUseTheModuleClock()
        {
            var definition = Animation("speed", true);
            Set(definition, "playbackSpeed", 2f);
            var mixer = Mixer(); mixer.Play(definition);
            mixer.Advance(.25f);
            Assert.That(mixer.Time, Is.EqualTo(.5).Within(.001));
            foreach (var delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) mixer.Advance(delta);
            Assert.That(mixer.Time, Is.EqualTo(.5).Within(.001));
            Assert.That(bone.localPosition.x, Is.EqualTo(5).Within(.01));
        }

        [Test]
        public void InterruptedCrossfadeStartsAtTheCurrentBlendedPoseAndReleasesOutgoingClips()
        {
            var a = Animation("a", true, 0, 0);
            var b = Animation("b", true, 10, 10);
            var c = Animation("c", true, 20, 20);
            Set(b, "crossfadeDuration", .2f); Set(c, "crossfadeDuration", .2f);
            var mixer = Mixer(); mixer.Play(a); mixer.Play(b); mixer.Advance(.1f);
            Assert.That(bone.localPosition.x, Is.EqualTo(5).Within(.01));
            mixer.Play(c);
            Assert.That(bone.localPosition.x, Is.EqualTo(5).Within(.01));
            mixer.Advance(.1f);
            Assert.That(bone.localPosition.x, Is.EqualTo(12.5f).Within(.01));
            mixer.Advance(.1f);
            Assert.That(bone.localPosition.x, Is.EqualTo(20).Within(.01));
            Assert.That(mixer.ClipCount, Is.EqualTo(1));
            mixer.Dispose();
            Assert.That(mixer.IsValid, Is.False);
        }

        [Test]
        public void OneShotCompletionReturnsTheWholeCharacterToIdle()
        {
            Assert.That(character.Initialize(), Is.True);
            var gesture = Activity("gesture", Animation("gesture", false));
            Assert.That(character.PlayActivity(gesture), Is.True);
            module.Tick(1.1f);
            Assert.That(character.CurrentActivity, Is.SameAs(idle));
            Assert.That(module.CurrentDefinition, Is.SameAs(idleAnimation));
            Assert.That(face.Requests[face.Requests.Count - 1].Expression, Is.Null);
        }

        [Test]
        public void RepeatedCommandsWithoutTimeAdvancingDoNotAccumulateZeroWeightClips()
        {
            var definition = Animation("rapid", true);
            Set(definition, "crossfadeDuration", .2f);
            var mixer = Mixer(); mixer.Play(definition);
            for (int i = 0; i < 50; i++) mixer.Play(definition);
            Assert.That(mixer.ClipCount, Is.LessThanOrEqualTo(2));
            mixer.Advance(.2f);
            Assert.That(mixer.ClipCount, Is.EqualTo(1));
        }

        [Test]
        public void PreviewRestoresAnExistingAnimatorController()
        {
            var controller = new AnimatorController { name = "Transient test controller" };
            controller.AddLayer("Base Layer");
            var machine = controller.layers[0].stateMachine;
            var state = machine.AddState("Idle");
            state.motion = idleAnimation.Clip;
            assets.Add(controller); assets.Add(machine); assets.Add(state);
            animator.runtimeAnimatorController = controller;
            module.StartPreview(Animation("preview", true));
            Assert.That(animator.runtimeAnimatorController, Is.Null);
            module.StopPreview();
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
        }

        [Test]
        public void OmittedBodyAnimationUsesIdleWithoutCompletingTheActivity()
        {
            var partial = Activity("partial", null);
            Assert.That(character.PlayActivity(partial), Is.True);
            module.Tick(4.5f);
            Assert.That(character.CurrentActivity, Is.SameAs(partial));
            Assert.That(module.CurrentDefinition, Is.SameAs(idleAnimation));
            Assert.That(module.ActivityPlaybackTime, Is.EqualTo(.5).Within(.001));
        }

        [Test]
        public void RuntimePreviewSuspendsOnlyBodyAndRestoresTheActivityClock()
        {
            character.Initialize(); module.Tick(.3f);
            var activity = character.CurrentActivity;
            var request = character.CurrentRequestId;
            Assert.That(module.StartPreview(Animation("preview", false, 20, 30)), Is.True);
            module.TickPreview(2); module.Tick(2);
            Assert.That(character.CurrentActivity, Is.SameAs(activity));
            Assert.That(character.CurrentRequestId, Is.EqualTo(request));
            Assert.That(face.Requests.Count, Is.EqualTo(1));
            Assert.That(module.ActivityPlaybackTime, Is.EqualTo(.3).Within(.001));
            module.StopPreview();
            Assert.That(module.ActivityPlaybackTime, Is.EqualTo(.3).Within(.001));
            Assert.That(bone.localPosition.x, Is.EqualTo(3).Within(.01));
            module.Tick(.1f);
            Assert.That(module.ActivityPlaybackTime, Is.EqualTo(.4).Within(.001));
        }

        [Test]
        public void PreviewPauseScrubResumeAndFinalHoldNeverCompleteAnActivity()
        {
            Assert.That(module.StartPreview(Animation("preview", false)), Is.True);
            module.TogglePreviewPause();
            module.PreviewNormalizedTime = .6f;
            module.TickPreview(.3f);
            Assert.That(module.PreviewNormalizedTime, Is.EqualTo(.6f).Within(.001));
            Assert.That(bone.localPosition.x, Is.EqualTo(6).Within(.01));
            module.TogglePreviewPause(); module.TickPreview(3);
            Assert.That(module.PreviewNormalizedTime, Is.EqualTo(1));
            Assert.That(character.CurrentActivity, Is.Null);
            module.TickPreview(1);
            Assert.That(bone.localPosition.x, Is.EqualTo(10).Within(.01));
            module.StopPreview();
            Assert.That(bone.localPosition, Is.EqualTo(new Vector3(3, 2, 1)));
        }

        [Test]
        public void PreviewButtonsDoNotDirtyInspectedPrefabs()
        {
            foreach (var method in new[] { "PreviewSelected", "TogglePreviewPause", "StopPreview" })
            {
                object button = null;
                foreach (var attribute in typeof(AnimationModule).GetMethod(method).GetCustomAttributes(false))
                    if (attribute.GetType().FullName == "Sirenix.OdinInspector.ButtonAttribute") button = attribute;
                Assert.That(button, Is.Not.Null, method);
                Assert.That((bool)button.GetType().GetField("DirtyOnClick").GetValue(button), Is.False, method);
            }
            var timeline = typeof(AnimationModule).GetProperty("PreviewNormalizedTime");
            foreach (var attribute in timeline.GetCustomAttributes(false))
                Assert.That(attribute.GetType().FullName, Is.Not.EqualTo("Sirenix.OdinInspector.ShowInInspectorAttribute"),
                    "The transient scrubber must use the editor's non-serialized control.");
        }

        [Test]
        public void NewActivityCancelsPreviewAndReplayRestartsAtZero()
        {
            character.Initialize(); module.Tick(.4f);
            module.StartPreview(Animation("preview", true));
            character.ReturnToIdle();
            Assert.That(module.IsPreviewing, Is.False);
            Assert.That(module.ActivityPlaybackTime, Is.Zero);
            module.Tick(.2f); character.ReturnToIdle();
            Assert.That(module.ActivityPlaybackTime, Is.Zero);
        }

        [Test]
        public void StopRestoresPoseAndAnimatorSettingsWithoutChangingFacialWeights()
        {
            var faceObject = new GameObject("Face"); faceObject.transform.SetParent(animator.transform);
            var renderer = faceObject.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { vertices = new[] { Vector3.zero } }; assets.Add(mesh);
            var delta = new[] { Vector3.zero };
            mesh.AddBlendShapeFrame("emo_joy", 100, delta, delta, delta);
            renderer.sharedMesh = mesh; renderer.SetBlendShapeWeight(0, 42);
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            var originalPosition = bone.localPosition;
            var originalRotation = bone.localRotation = Quaternion.Euler(12, 23, 34);
            var originalScale = bone.localScale = new Vector3(1, 2, 3);
            module.StartPreview(Animation("preview", true)); module.TickPreview(.5f);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.fireEvents, Is.False);
            Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(42));
            module.StopPreview();
            Assert.That(bone.localPosition, Is.EqualTo(originalPosition));
            Assert.That(Quaternion.Angle(bone.localRotation, originalRotation), Is.LessThan(.001));
            Assert.That(bone.localScale, Is.EqualTo(originalScale));
            Assert.That(animator.applyRootMotion, Is.True);
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely));
            Assert.That(animator.fireEvents, Is.True);
            Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(42));
        }

        [Test]
        public void DisableAndEditorCleanupRestorePreviewAndDisposeGraphs()
        {
            module.StartPreview(Animation("preview", true)); module.TickPreview(.5f);
            module.enabled = false;
            Assert.That(module.IsPreviewing, Is.False);
            Assert.That(bone.localPosition, Is.EqualTo(new Vector3(3, 2, 1)));
            module.enabled = true;
            module.StartPreview(Animation("preview-again", true));
            AnimationPreviewEditorBridge.StopAll();
            Assert.That(module.IsPreviewing, Is.False);
            Assert.That(AnimationModule.ActivePreviews, Is.Empty);
            character.Initialize();
            Assert.That(module.HasPlaybackGraph, Is.True);
            character.GetType().GetMethod("OnDisable", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).Invoke(character, null);
            Assert.That(module.HasPlaybackGraph, Is.False);
        }

        [Test]
        public void ChangedAnimatorReferenceRestoresTheOriginalPreviewTarget()
        {
            module.StartPreview(Animation("preview", true)); module.TickPreview(.5f);
            var other = new GameObject("Other rig"); other.transform.SetParent(character.transform);
            Set(module, "animator", other.AddComponent<Animator>());
            module.TickPreview(.1f);
            Assert.That(module.IsPreviewing, Is.False);
            Assert.That(bone.localPosition, Is.EqualTo(new Vector3(3, 2, 1)));
        }

        [Test]
        public void InvalidBodyCurvesAreRejectedBeforeReplacingTheCurrentActivity()
        {
            character.Initialize();
            var invalid = Animation("facial-clip", true);
            AnimationUtility.SetEditorCurve(invalid.Clip,
                EditorCurveBinding.FloatCurve("Face", typeof(SkinnedMeshRenderer), "blendShape.emo_joy"), AnimationCurve.Linear(0, 0, 1, 100));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Body clips may animate only"));
            Assert.That(character.PlayActivity(Activity("invalid", invalid)), Is.False);
            Assert.That(character.CurrentActivity, Is.SameAs(idle));
        }

        [Test]
        public void InvalidPreviewDoesNotReplaceAnExistingPreview()
        {
            var valid = Animation("valid", true);
            module.StartPreview(valid);
            var invalid = Animation("invalid", true); Set(invalid, "clip", null);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Assign an animation Clip"));
            Assert.That(module.StartPreview(invalid), Is.False);
            Assert.That(module.CurrentDefinition, Is.SameAs(valid));
            Assert.That(module.IsPreviewing, Is.True);
        }

        [Test]
        public void HumanoidActivityIsRejectedBeforeChangingAnIncompatibleCharactersOutputs()
        {
            character.Initialize();
            var definition = Animation("humanoid", false);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/_Sandbox/Animations/Remy@Standing Thumbs Up.fbx"))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) { Set(definition, "clip", clip); break; }
            Assert.That(definition.Clip.isHumanMotion, Is.True);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("humanoid clips require"));
            Assert.That(character.PlayActivity(Activity("humanoid-activity", definition)), Is.False);
            Assert.That(character.CurrentActivity, Is.SameAs(idle));
            Assert.That(face.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void SimultaneousPreviewsShareDefinitionsWithoutSharingClocks()
        {
            var secondRig = new GameObject("Second rig"); secondRig.transform.SetParent(character.transform);
            var secondBone = new GameObject("Bone").transform; secondBone.SetParent(secondRig.transform);
            var secondAnimator = secondRig.AddComponent<Animator>();
            var secondModuleObject = new GameObject("Second preview"); secondModuleObject.transform.SetParent(character.ModulesRoot);
            var secondModule = secondModuleObject.AddComponent<AnimationModule>();
            Set(secondModule, "animator", secondAnimator);
            var definition = Animation("shared-preview", true);
            module.StartPreview(definition); secondModule.StartPreview(definition);
            module.TickPreview(.7f); secondModule.TickPreview(.2f);
            Assert.That(bone.localPosition.x, Is.EqualTo(7).Within(.01));
            Assert.That(secondBone.localPosition.x, Is.EqualTo(2).Within(.01));
            module.StopPreview();
            Assert.That(secondModule.IsPreviewing, Is.True);
            Assert.That(secondModule.PlaybackTime, Is.EqualTo(.2).Within(.001));
            secondModule.StopPreview();
            Assert.That(secondBone.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void StartingAnotherPreviewOnTheSameAnimatorRestoresAndTransfersOwnership()
        {
            var secondObject = new GameObject("Other preview owner"); secondObject.transform.SetParent(character.ModulesRoot);
            var other = secondObject.AddComponent<AnimationModule>(); Set(other, "animator", animator);
            var definition = Animation("shared-preview", true);
            module.StartPreview(definition); module.TickPreview(.7f);
            other.StartPreview(definition);
            Assert.That(module.IsPreviewing, Is.False);
            Assert.That(other.IsPreviewing, Is.True);
            Assert.That(AnimationModule.ActivePreviews.Count, Is.EqualTo(1));
            other.StopPreview();
            Assert.That(bone.localPosition, Is.EqualTo(new Vector3(3, 2, 1)));
        }

        [Test]
        public void SavingASceneStopsPreviewAndSavesTheRestoredPose()
        {
            var path = "Assets/__AnimationModuleTest_" + Guid.NewGuid().ToString("N") + ".unity";
            var saveScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var originalScene = SceneManager.GetActiveScene();
            try
            {
                SceneManager.MoveGameObjectToScene(character.gameObject, saveScene);
                Assert.That(module.StartPreview(Animation("preview", true)), Is.True);
                module.TickPreview(.8f);
                Assert.That(bone.localPosition.x, Is.EqualTo(8).Within(.01));
                Assert.That(EditorSceneManager.SaveScene(saveScene, path), Is.True);
                Assert.That(module.IsPreviewing, Is.False);
                Assert.That(bone.localPosition, Is.EqualTo(new Vector3(3, 2, 1)));
            }
            finally
            {
                if (character) SceneManager.MoveGameObjectToScene(character.gameObject, scene);
                EditorSceneManager.CloseScene(saveScene, true);
                if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void ImportedHumanoidClipPreviewsOnEthansAvatarAndRestoresHisPose()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan/Ethan_unity.fbx");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            instance.transform.SetParent(character.transform);
            var humanAnimator = instance.GetComponentInChildren<Animator>(true);
            Assert.That(humanAnimator.avatar && humanAnimator.avatar.isHuman, Is.True);
            var upperArm = humanAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var original = upperArm.localRotation;
            SkinnedMeshRenderer faceMesh = null;
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name == "Face") { faceMesh = renderer; break; }
            Assert.That(faceMesh, Is.Not.Null);
            int joy = faceMesh.sharedMesh.GetBlendShapeIndex("emo_joy");
            int viseme = faceMesh.sharedMesh.GetBlendShapeIndex("viseme_aa");
            faceMesh.SetBlendShapeWeight(joy, 42); faceMesh.SetBlendShapeWeight(viseme, 37);
            AnimationClip clip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/_Sandbox/Animations/Remy@Standing Thumbs Up.fbx"))
                if (asset is AnimationClip candidate && !candidate.name.StartsWith("__preview")) { clip = candidate; break; }
            Assert.That(clip, Is.Not.Null);
            var definition = Animation("humanoid", false); Set(definition, "clip", clip);
            Set(module, "animator", humanAnimator);
            Assert.That(module.StartPreview(definition), Is.True);
            module.PreviewNormalizedTime = .5f;
            Assert.That(Quaternion.Angle(upperArm.localRotation, original), Is.GreaterThan(.1));
            Assert.That(faceMesh.GetBlendShapeWeight(joy), Is.EqualTo(42));
            Assert.That(faceMesh.GetBlendShapeWeight(viseme), Is.EqualTo(37));
            module.StopPreview();
            Assert.That(Quaternion.Angle(upperArm.localRotation, original), Is.LessThan(.001));
            Assert.That(faceMesh.GetBlendShapeWeight(joy), Is.EqualTo(42));
            Assert.That(faceMesh.GetBlendShapeWeight(viseme), Is.EqualTo(37));
        }

        AnimationPlayback Mixer()
        {
            var mixer = new AnimationPlayback(animator, "Edit Mode contract mixer");
            mixers.Add(mixer); return mixer;
        }

        AnimationDefinition Animation(string id, bool loop, float from = 0, float to = 10)
        {
            var clip = new AnimationClip { name = id }; assets.Add(clip);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Bone", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0, from, 1, to));
            var definition = ScriptableObject.CreateInstance<AnimationDefinition>(); assets.Add(definition);
            Set(definition, "id", id); Set(definition, "displayName", id);
            Set(definition, "clip", clip); Set(definition, "loop", loop); Set(definition, "crossfadeDuration", 0f);
            return definition;
        }

        ActivityDefinition Activity(string id, AnimationDefinition animation, bool isIdle = false)
        {
            var definition = ScriptableObject.CreateInstance<ActivityDefinition>(); assets.Add(definition);
            Set(definition, "id", id); Set(definition, "displayName", id);
            Set(definition, "animation", animation); Set(definition, "isIdle", isIdle);
            return definition;
        }

        static void Set(UnityEngine.Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (value == null || value is UnityEngine.Object) property.objectReferenceValue = value as UnityEngine.Object;
            else if (value is string text) property.stringValue = text;
            else if (value is bool flag) property.boolValue = flag;
            else if (value is float number) property.floatValue = number;
            else throw new ArgumentException(field);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
