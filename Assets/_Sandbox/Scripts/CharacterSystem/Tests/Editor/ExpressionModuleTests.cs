using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sandbox.CharacterSystem.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sandbox.CharacterSystem.Tests
{
    public sealed class ExpressionModuleTests
    {
        readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        Scene scene;
        Character character;
        ExpressionModule module;
        AnimationModule body;
        SkinnedMeshRenderer face;
        BlendShapeDebugger debugger;
        AnimationDefinition idleAnimation;
        ActivityDefinition idle;
        ExpressionDefinition joy;

        [SetUp]
        public void SetUp()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Expression contract test"); SceneManager.MoveGameObjectToScene(root, scene);
            character = root.AddComponent<Character>();
            var rig = Child(root.transform, "Rig");
            Child(rig, "Bone");
            var animator = rig.gameObject.AddComponent<Animator>();
            face = Child(root.transform, "Face").gameObject.AddComponent<SkinnedMeshRenderer>();
            face.sharedMesh = Mesh();
            var modules = Child(root.transform, "Modules");
            body = Child(modules, "Animation").gameObject.AddComponent<AnimationModule>();
            Set(body, "animator", animator);
            module = Child(modules, "Emotion").gameObject.AddComponent<ExpressionModule>();
            Set(module, "face", face);
            debugger = module.gameObject.AddComponent<BlendShapeDebugger>();
            Set(module, "blendShapeDebugger", debugger); Set(debugger, "character", module);
            Set(character, "modulesRoot", modules);
            var clip = new AnimationClip(); assets.Add(clip);
            clip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            idleAnimation = Definition<AnimationDefinition>("idle-animation");
            Set(idleAnimation, "clip", clip); Set(idleAnimation, "loop", true); Set(idleAnimation, "crossfadeDuration", 0f);
            idle = Activity("idle", null, true); Set(idle, "animation", idleAnimation);
            Set(character, "defaultIdle", idle);
            joy = Expression("joy", "emo_joy", 100, .2f);
        }

        [TearDown]
        public void TearDown()
        {
            ExpressionPreviewEditorBridge.StopAll(); AnimationPreviewEditorBridge.StopAll();
            EditorSceneManager.ClosePreviewScene(scene);
            for (int i = assets.Count - 1; i >= 0; i--) if (assets[i]) UnityEngine.Object.DestroyImmediate(assets[i]);
            assets.Clear();
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
        }

        [Test]
        public void DirectExpressionFadesAndSpeechDoesNotSwitchItsPose()
        {
            Assert.That(character.PlayActivity(Activity("happy", joy)), Is.True);
            module.Tick(.1f, false, 0); Assert.That(Weight("emo_joy"), Is.EqualTo(50).Within(.01));
            module.Tick(.1f, true, 1); Assert.That(Weight("emo_joy"), Is.EqualTo(100));
            Assert.That(module.CurrentDefinition, Is.SameAs(joy)); Assert.That(module.IsSpeaking, Is.True);
        }

        [Test]
        public void MissingExpressionFadesToNeutralAndExpressionOnlyPersists()
        {
            var activity = Activity("happy", joy); character.PlayActivity(activity); module.Tick(1, false, 0);
            module.Tick(20, false, 20); Assert.That(character.CurrentActivity, Is.SameAs(activity));
            character.PlayActivity(Activity("empty")); module.Tick(.09f, false, 21);
            Assert.That(Weight("emo_joy"), Is.EqualTo(50).Within(.01));
            module.Tick(.09f, false, 22); Assert.That(Weight("emo_joy"), Is.Zero);
            Assert.That(module.CurrentDefinition, Is.Null);
        }

        [Test]
        public void InterruptedFadeStartsAtCurrentOutputAndIntensityMayExceedOneHundred()
        {
            Set(module, "intensity", 1.5f);
            character.PlayActivity(Activity("happy", joy)); module.Tick(.1f, false, 0);
            Assert.That(Weight("emo_joy"), Is.EqualTo(75).Within(.01));
            var other = Expression("other", "mouthSmileLeft", 50, .2f);
            character.PlayActivity(Activity("other", other));
            Assert.That(Weight("emo_joy"), Is.EqualTo(75).Within(.01));
            module.Tick(.1f, false, 1); Assert.That(Weight("emo_joy"), Is.EqualTo(37.5).Within(.01));
            character.PlayActivity(Activity("happy-again", joy)); module.Tick(1, false, 2);
            Assert.That(Weight("emo_joy"), Is.EqualTo(150));
        }

        [Test]
        public void ReplayingTheSameExpressionRestartsItsFade()
        {
            var activity = Activity("happy", joy); character.PlayActivity(activity); module.Tick(.1f, false, 0);
            character.PlayActivity(activity); module.Tick(.1f, false, 1);
            Assert.That(Weight("emo_joy"), Is.EqualTo(75).Within(.01));
            Assert.That(module.IsTransitioning, Is.True);
        }

        [Test]
        public void ZeroFadeAppliesImmediately()
        {
            Set(joy, "fadeDuration", 0f); character.PlayActivity(Activity("happy", joy));
            Assert.That(Weight("emo_joy"), Is.EqualTo(100)); Assert.That(module.IsTransitioning, Is.False);
        }

        [Test]
        public void PreviewRestoresOriginalWeightsAndNeverTouchesVisemesOrBody()
        {
            Weight("emo_joy", 12); Weight("viseme_aa", 37);
            var original = body.Animator.transform.GetChild(0).localPosition;
            Assert.That(module.StartPreview(joy), Is.True); module.TickPreview(1);
            Assert.That(Weight("emo_joy"), Is.EqualTo(100)); Assert.That(Weight("viseme_aa"), Is.EqualTo(37));
            Assert.That(character.CurrentActivity, Is.Null);
            Assert.That(body.Animator.transform.GetChild(0).localPosition, Is.EqualTo(original));
            module.StopPreview(); Assert.That(Weight("emo_joy"), Is.EqualTo(12));
            Assert.That(Weight("viseme_aa"), Is.EqualTo(37));
        }

        [Test]
        public void NeutralAndRepeatedPreviewsKeepOneOriginalSnapshot()
        {
            Weight("emo_joy", 25); module.StartPreview(joy); module.TickPreview(1);
            module.PreviewNeutral(); module.TickPreview(1); Assert.That(Weight("emo_joy"), Is.Zero);
            module.StartPreview(joy); module.TickPreview(1); module.StopPreview();
            Assert.That(Weight("emo_joy"), Is.EqualTo(25));
        }

        [Test]
        public void SimultaneousBodyAndFacialPreviewsAreIndependent()
        {
            Assert.That(body.StartPreview(idleAnimation), Is.True); body.TickPreview(.3f);
            module.StartPreview(joy); module.TickPreview(.2f);
            Assert.That(body.IsPreviewing, Is.True); Assert.That(body.PlaybackTime, Is.EqualTo(.3).Within(.001));
            body.PreviewNormalizedTime = .8f; Assert.That(Weight("emo_joy"), Is.EqualTo(100));
            module.StopPreview(); Assert.That(body.IsPreviewing, Is.True);
            Assert.That(body.PlaybackTime, Is.EqualTo(.8).Within(.001));
        }

        [Test]
        public void PreviewSuspendsOnlyFacialProgressAndStopResumesActivityOutput()
        {
            var activity = Activity("happy", joy); character.PlayActivity(activity); module.Tick(.1f, false, 0);
            Set(module, "previewDefinition", Expression("smile", "mouthSmileLeft", 55, 0));
            module.PreviewSelected(); module.TickPreview(1);
            body.Tick(.4f); module.Tick(10, true, 1);
            Assert.That(Weight("emo_joy"), Is.Zero); Assert.That(module.IsSpeaking, Is.True);
            Assert.That(body.ActivityPlaybackTime, Is.EqualTo(.4).Within(.001));
            Assert.That(character.CurrentActivity, Is.SameAs(activity));
            module.StopPreview(); Assert.That(Weight("emo_joy"), Is.EqualTo(50).Within(.01));
            module.Tick(.1f, false, 2); Assert.That(Weight("emo_joy"), Is.EqualTo(100));
        }

        [Test]
        public void NewActivityCancelsBothPreviews()
        {
            character.Initialize(); module.StartPreview(joy); body.StartPreview(idleAnimation);
            var activity = Activity("happy", joy); character.PlayActivity(activity);
            Assert.That(module.IsPreviewing, Is.False); Assert.That(body.IsPreviewing, Is.False);
            Assert.That(character.CurrentActivity, Is.SameAs(activity));
        }

        [Test]
        public void ReservedExpressionIsRejectedBeforeReplacingAnyOutputsOrPreviews()
        {
            character.Initialize(); module.StartPreview(joy); module.TickPreview(1);
            var invalid = Activity("viseme", Expression("bad-viseme", "viseme_aa", 100, 0));
            LogAssert.Expect(LogType.Warning, new Regex(".*owned by uLipSync.*", RegexOptions.Singleline));
            Assert.That(character.PlayActivity(invalid), Is.False);
            Assert.That(character.CurrentActivity, Is.SameAs(idle)); Assert.That(module.IsPreviewing, Is.True);
            Assert.That(Weight("emo_joy"), Is.EqualTo(100));
        }

        [Test]
        public void ConfiguredDriverShapeIsReservedEvenWhenItsNameIsNotAViseme()
        {
            var driver = face.gameObject.AddComponent<uLipSync.uLipSyncBlendShape>();
            driver.skinnedMeshRenderer = face;
            driver.blendShapes.Add(new uLipSync.uLipSyncBlendShape.BlendShapeInfo { index = face.sharedMesh.GetBlendShapeIndex("mouthSmileLeft") });
            Set(module, "blendShapeDriver", driver); Weight("mouthSmileLeft", 44);
            character.PlayActivity(Activity("happy", joy)); module.Tick(1, false, 0);
            Assert.That(Weight("mouthSmileLeft"), Is.EqualTo(44));
            var errors = new List<string>();
            module.ValidateActivity(new ActivityRequest(Activity("reserved", Expression("reserved", "mouthSmileLeft", 100, 0)), idleAnimation, 9), errors);
            Assert.That(errors, Has.Some.Contains("owned by uLipSync"));
            module.enabled = false; Assert.That(Weight("mouthSmileLeft"), Is.EqualTo(44));
        }

        [Test]
        public void DebuggerAndModuleRestoreBeforeTakingOverFromEachOther()
        {
            Weight("emo_joy", 13);
            SetPose(debugger, new BlendShapePose(new BlendShapeTarget("emo_joy", 60), new BlendShapeTarget("viseme_aa", 99)));
            Weight("viseme_aa", 37); module.StartPreview(joy); module.TickPreview(1);
            debugger.PlayAnimation(); debugger.TickPreview(double.MaxValue);
            Assert.That(module.IsPreviewing, Is.False); Assert.That(Weight("emo_joy"), Is.EqualTo(60));
            Assert.That(Weight("viseme_aa"), Is.EqualTo(37));
            module.StartPreview(joy); module.TickPreview(1); Assert.That(debugger.IsHolding, Is.False);
            module.StopPreview(); Assert.That(Weight("emo_joy"), Is.EqualTo(13));
        }

        [Test]
        public void ActivityCommandStopsDebuggerAndRestoresNormalOutput()
        {
            character.Initialize(); SetPose(debugger, new BlendShapePose(new BlendShapeTarget("emo_joy", 60)));
            debugger.PlayAnimation(); debugger.TickPreview(double.MaxValue);
            character.PlayActivity(Activity("happy", joy)); Assert.That(debugger.IsHolding, Is.False);
            module.Tick(1, false, 0); Assert.That(Weight("emo_joy"), Is.EqualTo(100));
        }

        [Test]
        public void DisableAndSaveGuardRestoreStandalonePreviews()
        {
            Weight("emo_joy", 11); module.StartPreview(joy); module.TickPreview(1);
            module.enabled = false; Assert.That(Weight("emo_joy"), Is.EqualTo(11));
            module.enabled = true; module.StartPreview(joy); module.TickPreview(1);
            ExpressionPreviewEditorBridge.StopAll(); Assert.That(Weight("emo_joy"), Is.EqualTo(11));
            Assert.That(module.IsPreviewing, Is.False);
            UnityEngine.Object.DestroyImmediate(module); Assert.That(Weight("emo_joy"), Is.EqualTo(11));
        }

        [Test]
        public void MeshReplacementEndsPreviewWithoutRestoringOldIndicesIntoNewMesh()
        {
            module.StartPreview(joy); module.TickPreview(1);
            face.sharedMesh = Mesh(); Weight("emo_joy", 23);
            module.TickPreview(.1f); Assert.That(module.IsPreviewing, Is.False);
            Assert.That(Weight("emo_joy"), Is.EqualTo(23));
        }

        [Test]
        public void MissingCanonicalShapesAndInvalidTimingAreDiagnosed()
        {
            var empty = new Mesh(); assets.Add(empty); face.sharedMesh = empty;
            Set(module, "endOfSpeechDelay", float.NaN);
            var errors = new List<string>(); module.ValidateConfiguration(errors);
            Assert.That(errors, Has.Some.Contains("missing standard blendshape"));
            Assert.That(errors, Has.Some.Contains("finite"));
        }

        [Test]
        public void CharactersSharingDefinitionsHaveIndependentPlaybackAndPreviewState()
        {
            var clone = UnityEngine.Object.Instantiate(character.gameObject);
            SceneManager.MoveGameObjectToScene(clone, scene);
            var other = clone.GetComponent<Character>();
            var otherModule = clone.GetComponentInChildren<ExpressionModule>();
            var activity = Activity("shared", joy);
            character.PlayActivity(activity); other.PlayActivity(activity);
            module.Tick(.2f, false, 0); otherModule.Tick(.1f, false, 0);
            Assert.That(Weight("emo_joy"), Is.EqualTo(100));
            Assert.That(otherModule.Face.GetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex("emo_joy")), Is.EqualTo(50).Within(.01));
            otherModule.PreviewNeutral(); otherModule.TickPreview(1);
            Assert.That(Weight("emo_joy"), Is.EqualTo(100));
            Assert.That(other.CurrentActivity, Is.SameAs(activity));
            Assert.That(joy.Targets[0].Weight, Is.EqualTo(100));
        }

        [Test]
        public void RendererRebindingClearsOldOwnedOutputAndPreservesVisemesOnBothFaces()
        {
            character.PlayActivity(Activity("happy", joy)); module.Tick(1, false, 0); Weight("viseme_aa", 31);
            var other = Child(character.transform, "ReplacementFace").gameObject.AddComponent<SkinnedMeshRenderer>();
            other.sharedMesh = Mesh(); other.SetBlendShapeWeight(other.sharedMesh.GetBlendShapeIndex("viseme_aa"), 37);
            Set(module, "face", other); module.Tick(1, false, 1);
            Assert.That(Weight("emo_joy"), Is.Zero); Assert.That(Weight("viseme_aa"), Is.EqualTo(31));
            Assert.That(other.GetBlendShapeWeight(other.sharedMesh.GetBlendShapeIndex("emo_joy")), Is.EqualTo(100));
            Assert.That(other.GetBlendShapeWeight(other.sharedMesh.GetBlendShapeIndex("viseme_aa")), Is.EqualTo(37));
        }

        [Test]
        public void SpeechGraceAffectsEligibilityAndUsesUnscaledTimeOnly()
        {
            character.PlayActivity(Activity("happy-idle", joy, true)); module.Tick(1, true, 0);
            module.Tick(0, false, 1); Assert.That(module.IsSpeaking, Is.True);
            module.Tick(0, false, 1.2f); Assert.That(module.IsSpeaking, Is.True);
            module.Tick(0, false, 1.31f); Assert.That(module.IsSpeaking, Is.False);
            Assert.That(module.CurrentDefinition, Is.SameAs(joy)); Assert.That(Weight("emo_joy"), Is.EqualTo(100));
        }

        [Test]
        public void DebugButtonsNeverDirtyThePrefab()
        {
            foreach (var type in new[] { typeof(ExpressionModule), typeof(BlendShapeDebugger) })
                foreach (var method in type.GetMethods())
                {
                    foreach (var attribute in method.GetCustomAttributes(false))
                        if (attribute.GetType().FullName == "Sirenix.OdinInspector.ButtonAttribute")
                            Assert.That(attribute.GetType().GetField("DirtyOnClick").GetValue(attribute), Is.False, method.Name);
                }
        }

        Mesh Mesh()
        {
            var mesh = new Mesh { name = "Canonical test face", vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            assets.Add(mesh);
            foreach (var shape in BlendShapeDatabase.Default.Names)
                mesh.AddBlendShapeFrame(shape, 100, new[] { Vector3.right * .01f, Vector3.zero, Vector3.zero }, null, null);
            return mesh;
        }
        T Definition<T>(string id) where T : CharacterDefinition
        {
            var value = ScriptableObject.CreateInstance<T>(); assets.Add(value);
            Set(value, "id", id); Set(value, "displayName", id); return value;
        }
        ExpressionDefinition Expression(string id, string shape, float weight, float fade)
        {
            var value = Definition<ExpressionDefinition>(id); Set(value, "fadeDuration", fade);
            var serialized = new SerializedObject(value); var targets = serialized.FindProperty("targets"); targets.arraySize = 1;
            targets.GetArrayElementAtIndex(0).FindPropertyRelative("shape").stringValue = shape;
            targets.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = weight;
            serialized.ApplyModifiedPropertiesWithoutUndo(); return value;
        }
        ActivityDefinition Activity(string id, ExpressionDefinition expression = null, bool isIdle = false)
        {
            var value = Definition<ActivityDefinition>(id); Set(value, "expression", expression); Set(value, "isIdle", isIdle); return value;
        }
        float Weight(string shape) => face.GetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex(shape));
        void Weight(string shape, float value) => face.SetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex(shape), value);
        static Transform Child(Transform parent, string name) { var child = new GameObject(name).transform; child.SetParent(parent); return child; }
        static void SetPose(BlendShapeDebugger target, BlendShapePose value)
            => typeof(BlendShapeDebugger).GetField("pose", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Set(UnityEngine.Object target, string field, object value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
            if (value == null || value is UnityEngine.Object) property.objectReferenceValue = value as UnityEngine.Object;
            else if (value is string text) property.stringValue = text;
            else if (value is bool flag) property.boolValue = flag;
            else if (value is float number) property.floatValue = number;
            else throw new ArgumentException(field);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
