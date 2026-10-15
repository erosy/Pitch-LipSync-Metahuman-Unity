using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sandbox.CharacterSystem.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sandbox.CharacterSystem.Tests
{
    public sealed class CharacterContractsTests
    {
        readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        Scene scene;
        Character character;
        RecordingCharacterModule body;
        OtherRecordingModule face;
        AnimationDefinition idleAnimation;
        ActivityDefinition idle;

        [SetUp]
        public void SetUp()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False, "These checks must stay in Edit Mode.");
            scene = EditorSceneManager.NewPreviewScene();
            character = CreateCharacter("Contract test character");
            body = AddModule<RecordingCharacterModule>(character);
            face = AddModule<OtherRecordingModule>(character);
            idleAnimation = Animation("idle-animation", true);
            idle = Activity("idle", idleAnimation, null, true);
            Set(character, "defaultIdle", idle);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            for (int i = assets.Count - 1; i >= 0; i--)
                if (assets[i] && !AssetDatabase.Contains(assets[i])) UnityEngine.Object.DestroyImmediate(assets[i]);
            assets.Clear();
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
        }

        [Test]
        public void InitializeDiscoversModulesAndDispatchesIdleOnce()
        {
            Assert.That(character.Initialize(), Is.True);
            Assert.That(character.Initialize(), Is.True);
            Assert.That(body.Owner, Is.SameAs(character));
            Assert.That(body.Initializations, Is.EqualTo(1));
            Assert.That(body.Requests.Count, Is.EqualTo(1));
            Assert.That(face.Requests[0].RequestId, Is.EqualTo(body.Requests[0].RequestId));
            Assert.That(character.CurrentActivity, Is.SameAs(idle));
            Assert.That(character.TryGetModule<OtherRecordingModule>(out var found), Is.True);
            Assert.That(found, Is.SameAs(face));
        }

        [Test]
        public void FirstExplicitCommandDoesNotDispatchAnIntermediateIdle()
        {
            var activity = Activity("joy", null, Expression("joy"));
            Assert.That(character.PlayActivity(activity), Is.True);
            Assert.That(body.Requests.Count, Is.EqualTo(1));
            Assert.That(body.Requests[0].Activity, Is.SameAs(activity));
        }

        [Test]
        public void PartialAndEmptyActivitiesResolveResetsWithoutRetainingPreviousOutputs()
        {
            var joy = Expression("joy");
            var gesture = Animation("gesture", false);
            character.PlayActivity(Activity("gesture-joy", gesture, joy));
            character.PlayActivity(Activity("expression-only", null, joy));
            var request = body.Requests[1];
            Assert.That(request.Animation, Is.SameAs(idleAnimation));
            Assert.That(request.Expression, Is.SameAs(joy));
            Assert.That(request.IsIdle, Is.False);
            Assert.That(request.CompletesOnAnimationEnd, Is.False);
            character.PlayActivity(Activity("animation-only", gesture));
            Assert.That(face.Requests[2].Expression, Is.Null);
            character.PlayActivity(Activity("empty"));
            Assert.That(body.Requests[3].Animation, Is.SameAs(idleAnimation));
            Assert.That(face.Requests[3].Expression, Is.Null);
            Assert.That(character.ReturnToIdle(), Is.True);
            Assert.That(character.CurrentActivity, Is.SameAs(idle));
        }

        [Test]
        public void ReplayingRestartsAndStaleCompletionCannotReplaceNewerActivity()
        {
            var gesture = Activity("gesture", Animation("gesture", false));
            character.PlayActivity(gesture);
            var first = character.CurrentRequestId;
            character.PlayActivity(gesture);
            Assert.That(character.CurrentRequestId, Is.GreaterThan(first));
            Assert.That(body.Finish(first), Is.False);
            Assert.That(character.CurrentActivity, Is.SameAs(gesture));
            Assert.That(body.Finish(character.CurrentRequestId), Is.True);
            Assert.That(character.CurrentActivity, Is.SameAs(idle));
            Assert.That(face.Requests[face.Requests.Count - 1].Expression, Is.Null);
        }

        [Test]
        public void LoopingAndExpressionOnlyActivitiesDoNotCompleteFromAnimationCallbacks()
        {
            character.Initialize();
            Assert.That(body.Finish(character.CurrentRequestId), Is.False);
            character.PlayActivity(Activity("speaking", null, Expression("speaking-joy")));
            Assert.That(body.Finish(character.CurrentRequestId), Is.False);
        }

        [Test]
        public void SynchronousCompletionWaitsUntilEveryReceiverGetsTheOriginalCommand()
        {
            body.CompleteDuringApply = true;
            var gesture = Activity("gesture", Animation("gesture", false));
            character.PlayActivity(gesture);
            Assert.That(face.Requests.Count, Is.EqualTo(2));
            Assert.That(face.Requests[0].Activity, Is.SameAs(gesture));
            Assert.That(face.Requests[1].Activity, Is.SameAs(idle));
            Assert.That(character.CurrentActivity, Is.SameAs(idle));
        }

        [Test]
        public void InvalidCommandDoesNotChangeActivityRequestOrReceiverOutputs()
        {
            character.Initialize();
            var previous = character.CurrentRequestId;
            var invalid = Activity("invalid", Animation("missing-clip", false));
            Set(invalid.Animation, "clip", null);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Assign an animation Clip"));
            Assert.That(character.PlayActivity(invalid), Is.False);
            Assert.That(character.CurrentRequestId, Is.EqualTo(previous));
            Assert.That(character.CurrentActivity, Is.SameAs(idle));
            Assert.That(body.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateModuleTypesPreventInitializationBeforeAnyModuleRuns()
        {
            AddModule<RecordingCharacterModule>(character);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate module type"));
            Assert.That(character.Initialize(), Is.False);
            Assert.That(body.Initializations, Is.Zero);
            Assert.That(character.CurrentActivity, Is.Null);
        }

        [Test]
        public void InvalidModulePreventsInitializationBeforeAnyModuleRuns()
        {
            face.Invalid = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("module configuration is invalid"));
            Assert.That(character.Initialize(), Is.False);
            Assert.That(body.Initializations, Is.Zero);
        }

        [Test]
        public void DisabledModulesAreDiscoverableButDoNotReceiveOutputCommands()
        {
            face.enabled = false;
            character.Initialize();
            Assert.That(face.Owner, Is.SameAs(character));
            Assert.That(character.TryGetModule<OtherRecordingModule>(out _), Is.True);
            Assert.That(face.Requests, Is.Empty);
            Assert.That(body.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void NestedCharactersDoNotStealEachOthersModules()
        {
            var nested = CreateCharacter("Nested character");
            nested.transform.SetParent(character.ModulesRoot);
            var nestedBody = AddModule<RecordingCharacterModule>(nested);
            Set(nested, "defaultIdle", idle);
            Assert.That(character.Initialize(), Is.True);
            Assert.That(nestedBody.Owner, Is.Null);
            Assert.That(nested.Initialize(), Is.True);
            Assert.That(nestedBody.Owner, Is.SameAs(nested));
        }

        [Test]
        public void CharactersSharingDefinitionsHaveIndependentRequestState()
        {
            var other = CreateCharacter("Other character");
            var otherBody = AddModule<RecordingCharacterModule>(other);
            Set(other, "defaultIdle", idle);
            character.Initialize();
            other.Initialize();
            character.PlayActivity(Activity("gesture", Animation("gesture", false)));
            Assert.That(other.CurrentActivity, Is.SameAs(idle));
            Assert.That(otherBody.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void ShutdownReleasesOwnershipAndInvalidatesCompletion()
        {
            character.Initialize();
            var request = character.CurrentRequestId;
            // Edit Mode does not run normal MonoBehaviour lifecycle callbacks without ExecuteAlways.
            typeof(Character).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).Invoke(character, null);
            Assert.That(body.Shutdowns, Is.EqualTo(1));
            Assert.That(body.Owner, Is.Null);
            Assert.That(body.Finish(request), Is.False);
            Assert.That(character.IsInitialized, Is.False);
            Assert.That(character.Initialize(), Is.True);
            Assert.That(character.CurrentActivity, Is.SameAs(idle));
            Assert.That(character.CurrentRequestId, Is.GreaterThan(request));
        }

        [Test]
        public void DefaultIdleRequiresIdleTagLoopAndNeutralExpression()
        {
            Set(idle, "isIdle", false);
            Set(idleAnimation, "loop", false);
            Set(idle, "expression", Expression("joy"));
            var errors = string.Join("\n", character.ValidateConfiguration());
            StringAssert.Contains("tagged Is Idle", errors);
            StringAssert.Contains("looping animation", errors);
            StringAssert.Contains("no expression", errors);
        }

        [Test]
        public void InvalidFirstCommandDoesNotInitializeOrDispatchDefaultIdle()
        {
            var invalid = Activity("invalid", Animation("invalid-animation", false));
            Set(invalid.Animation, "clip", null);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Assign an animation Clip"));
            Assert.That(character.PlayActivity(invalid), Is.False);
            Assert.That(character.IsInitialized, Is.False);
            Assert.That(body.Initializations, Is.Zero);
            Assert.That(body.Requests, Is.Empty);
            Assert.That(character.CurrentActivity, Is.Null);
        }

        [Test]
        public void MissingOptionalModulesDoNotPreventOtherModulesReceivingCommands()
        {
            UnityEngine.Object.DestroyImmediate(face.gameObject);
            Assert.That(character.Initialize(), Is.True);
            Assert.That(body.Requests.Count, Is.EqualTo(1));
            Assert.That(character.TryGetModule<OtherRecordingModule>(out _), Is.False);
        }

        [Test]
        public void DefinitionIdentityIsRequiredAndIndependentOfAssetName()
        {
            var expression = Expression("joy");
            expression.name = "Renamed asset";
            Assert.That(expression.Id, Is.EqualTo("joy"));
            Set(expression, "id", " joy ");
            Set(expression, "displayName", " ");
            var errors = string.Join("\n", expression.ValidateConfiguration());
            StringAssert.Contains("whitespace", errors);
            StringAssert.Contains("Display Name is required", errors);
            Set(expression, "id", "");
            StringAssert.Contains("Id is required", string.Join("\n", expression.ValidateConfiguration()));
        }

        [Test]
        public void FailedInitializationReleasesAllModulesWithoutDispatching()
        {
            face.ThrowOnInitialize = true;
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Test initialization failure"));
            Assert.That(character.Initialize(), Is.False);
            Assert.That(body.Owner, Is.Null);
            Assert.That(face.Owner, Is.Null);
            Assert.That(body.Shutdowns, Is.EqualTo(1));
            Assert.That(face.Shutdowns, Is.EqualTo(1));
            Assert.That(body.Requests, Is.Empty);
            Assert.That(character.CurrentActivity, Is.Null);
        }

        [Test]
        public void ModulesRootMustBelongToTheCharacter()
        {
            Set(character, "modulesRoot", character.transform);
            StringAssert.Contains("Modules Root beneath", string.Join("\n", character.ValidateConfiguration()));
        }

        [Test]
        public void DuplicateIdsAreScopedToDefinitionTypeAndRepeatedReferencesAreAllowed()
        {
            var animation = Animation("shared", true);
            var expression = Expression("shared");
            var errors = new List<string>();
            CharacterDefinition.ValidateUniqueIds(new CharacterDefinition[] { animation, expression, animation }, errors);
            Assert.That(errors, Is.Empty);
            CharacterDefinition.ValidateUniqueIds(new CharacterDefinition[] { expression, Expression("shared") }, errors);
            StringAssert.Contains("Duplicate ExpressionDefinition Id 'shared'", string.Join("\n", errors));
        }

        [Test]
        public void DefinitionsRejectInvalidNumbersAndDuplicateShapesWithoutRewritingData()
        {
            Set(idleAnimation, "playbackSpeed", float.NaN);
            Set(idleAnimation, "crossfadeDuration", -1f);
            var expression = Expression("invalid");
            Set(expression, "fadeDuration", float.PositiveInfinity);
            var serialized = new SerializedObject(expression);
            var targets = serialized.FindProperty("targets");
            targets.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                targets.GetArrayElementAtIndex(i).FindPropertyRelative("shape").stringValue = "smile";
                targets.GetArrayElementAtIndex(i).FindPropertyRelative("weight").floatValue = 101;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(idleAnimation.ValidateConfiguration().Count, Is.EqualTo(2));
            var errors = string.Join("\n", expression.ValidateConfiguration());
            StringAssert.Contains("Fade Duration", errors);
            StringAssert.Contains("duplicate shape", errors);
            StringAssert.Contains("between 0 and 100", errors);
            Assert.That(expression.Targets[0].Weight, Is.EqualTo(101));
        }

        [Test]
        public void AssetSerializationPreservesReferencesIdsAndWeightsAndFindsGlobalDuplicateIds()
        {
            var folder = "Assets/__CharacterContracts_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            try
            {
                var expression = Expression("serialization-joy");
                var serialized = new SerializedObject(expression);
                var targets = serialized.FindProperty("targets");
                targets.arraySize = 1;
                targets.GetArrayElementAtIndex(0).FindPropertyRelative("shape").stringValue = "emo_joy";
                targets.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 73;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var activity = Activity("serialization-activity", idleAnimation, expression);
                AssetDatabase.CreateAsset(idleAnimation.Clip, folder + "/Clip.anim");
                AssetDatabase.CreateAsset(idleAnimation, folder + "/Animation.asset");
                AssetDatabase.CreateAsset(expression, folder + "/Expression.asset");
                AssetDatabase.CreateAsset(activity, folder + "/Activity.asset");
                AssetDatabase.CreateAsset(Expression("serialization-joy"), folder + "/Duplicate.asset");
                // Save only temporary test assets; do not save the user's unrelated dirty assets.
                foreach (var asset in assets) if (asset && AssetDatabase.Contains(asset)) AssetDatabase.SaveAssetIfDirty(asset);
                var yaml = System.IO.File.ReadAllText(folder + "/Expression.asset");
                StringAssert.Contains("serialization-joy", yaml);
                StringAssert.Contains("emo_joy", yaml);
                var restored = ScriptableObject.CreateInstance<ExpressionDefinition>();
                assets.Add(restored);
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(expression), restored);
                Assert.That(restored.Id, Is.EqualTo("serialization-joy"));
                Assert.That(restored.Targets[0].Weight, Is.EqualTo(73));
                var loaded = AssetDatabase.LoadAssetAtPath<ActivityDefinition>(folder + "/Activity.asset");
                Assert.That(loaded.Animation, Is.SameAs(idleAnimation));
                Assert.That(loaded.Expression, Is.SameAs(expression));
                StringAssert.Contains("serialization-joy", string.Join("\n", DefinitionAssetValidation.ValidateIds()));
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        Character CreateCharacter(string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            var result = root.AddComponent<Character>();
            var modules = new GameObject("Modules");
            modules.transform.SetParent(root.transform);
            Set(result, "modulesRoot", modules.transform);
            return result;
        }

        static T AddModule<T>(Character owner) where T : CharacterModule
        {
            var child = new GameObject(typeof(T).Name);
            child.transform.SetParent(owner.ModulesRoot);
            var module = child.AddComponent<T>();
            Assert.That(module, Is.Not.Null, "Test receiver must be a runtime-compatible MonoBehaviour.");
            return module;
        }

        T Definition<T>(string id) where T : CharacterDefinition
        {
            var definition = ScriptableObject.CreateInstance<T>();
            assets.Add(definition);
            definition.name = id;
            Set(definition, "id", id);
            Set(definition, "displayName", id);
            return definition;
        }

        AnimationDefinition Animation(string id, bool loop)
        {
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            assets.Add(clip);
            var definition = Definition<AnimationDefinition>(id);
            Set(definition, "clip", clip);
            Set(definition, "loop", loop);
            return definition;
        }

        ExpressionDefinition Expression(string id) => Definition<ExpressionDefinition>(id);

        ActivityDefinition Activity(string id, AnimationDefinition animation = null,
            ExpressionDefinition expression = null, bool isIdle = false)
        {
            var definition = Definition<ActivityDefinition>(id);
            Set(definition, "animation", animation);
            Set(definition, "expression", expression);
            Set(definition, "isIdle", isIdle);
            return definition;
        }

        static void Set(UnityEngine.Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, field);
            if (value == null || value is UnityEngine.Object) property.objectReferenceValue = value as UnityEngine.Object;
            else if (value is string text) property.stringValue = text;
            else if (value is bool flag) property.boolValue = flag;
            else if (value is float number) property.floatValue = number;
            else throw new ArgumentException("Unsupported test value: " + field);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
