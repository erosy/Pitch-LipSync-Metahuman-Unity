using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Sandbox.CharacterSystem.Tests
{
    /// <summary>Acceptance checks against the actual migrated character assets.</summary>
    public sealed class ProductionCharacterTests
    {
        const string Data = "Assets/_Sandbox/Data/";

        [Test] public void EthanPreservesItsActivityAndPreviewContracts()
            => Verify("Assets/_Sandbox/Characters/Ethan/Ethan.prefab", false);

        [Test] public void EthanBackupPreservesItsActivityAndPreviewContracts()
            => Verify("Assets/_Sandbox/Characters/Ethan/Ethan_BeforeFittedHair.prefab", false);

        [Test] public void MaleSamplePreservesItsActivityAndPreviewContracts()
            => Verify("Assets/_Sandbox/Prefabs/Character Sample.prefab", false);

        [Test] public void FemaleSamplePreservesItsActivityAndPreviewContracts()
            => Verify("Assets/_Sandbox/Prefabs/Character Sample Woman.prefab", true);

        static void Verify(string path, bool woman)
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            var original = File.ReadAllBytes(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Samples are authored inactive. This disposable instance is never saved.
                root.SetActive(true);
                var character = root.GetComponent<Character>();
                Assert.That(character, Is.Not.Null);
                Assert.That(character.ValidateConfiguration(), Is.Empty);
                var body = root.GetComponentInChildren<AnimationModule>(true);
                var expression = root.GetComponentInChildren<ExpressionModule>(true);
                Assert.That(body, Is.Not.Null);
                Assert.That(expression, Is.Not.Null);
                Assert.That(body.Animator.runtimeAnimatorController, Is.Null);
                Assert.That(body.Animator.applyRootMotion, Is.False);
                Assert.That(body.Animator.avatar.isHuman, Is.True);
                Assert.That(AssetDatabase.GetAssetPath(character.DefaultIdle.Animation.Clip), Is.EqualTo(
                    "Assets/_Sandbox/Animations/Remy@" + (woman ? "Breathing Idle" : "Idle") + ".fbx"));
                Assert.That(expression.FacialMotions.Count, Is.EqualTo(5));
                Assert.That(expression.ConfigurationWarnings, Is.Empty);

                var references = new SerializedObject(expression);
                var lipSync = (uLipSync.uLipSync)references.FindProperty("lipSync").objectReferenceValue;
                var driver = (uLipSync.uLipSyncBlendShape)references.FindProperty("blendShapeDriver").objectReferenceValue;
                Assert.That(lipSync, Is.Not.Null);
                Assert.That(lipSync.GetComponent<AudioSource>(), Is.Not.Null);
                Assert.That(driver.skinnedMeshRenderer, Is.SameAs(expression.Face));
                var calls = new SerializedObject(lipSync).FindProperty("onLipSyncUpdate.m_PersistentCalls.m_Calls");
                Assert.That(calls.arraySize, Is.EqualTo(1));
                Assert.That(calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_Target").objectReferenceValue, Is.SameAs(driver));
                Assert.That(calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_MethodName").stringValue, Is.EqualTo("OnLipSyncUpdate"));

                var joy = Load<ExpressionDefinition>("Expression/Joy.asset");
                var thumbs = Load<ActivityDefinition>("Activity/Thumbs up.asset");
                var speaking = Load<ActivityDefinition>("Activity/Speaking happy" + (woman ? " (Woman)" : "") + ".asset");
                var happy = Load<ActivityDefinition>("Activity/Idle happy" + (woman ? " (Woman)" : "") + ".asset");
                Assert.That(happy.Expression, Is.SameAs(joy), "Identical poses should share a definition.");
                var face = expression.Face;
                int viseme = face.sharedMesh.GetBlendShapeIndex("viseme_aa");
                Assert.That(viseme, Is.GreaterThanOrEqualTo(0));
                var before = Enumerable.Range(0, face.sharedMesh.blendShapeCount).Select(face.GetBlendShapeWeight).ToArray();
                face.SetBlendShapeWeight(viseme, 37);
                var transforms = body.Animator.GetComponentsInChildren<Transform>(true);
                var positions = transforms.Select(t => t.localPosition).ToArray();
                var rotations = transforms.Select(t => t.localRotation).ToArray();

                Assert.That(body.StartPreview(thumbs.Animation), Is.True);
                body.PreviewNormalizedTime = .5f;
                Assert.That(expression.StartPreview(joy), Is.True);
                expression.TickPreview(.3f);
                Assert.That(character.CurrentActivity, Is.Null);
                Assert.That(face.GetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex("emo_joy")), Is.EqualTo(100).Within(.001));
                Assert.That(face.GetBlendShapeWeight(viseme), Is.EqualTo(37));
                expression.PreviewNeutral(); expression.TickPreview(.3f);
                Assert.That(body.IsPreviewing, Is.True);
                expression.StopPreview(); body.StopPreview();
                for (int i = 0; i < before.Length; i++)
                    Assert.That(face.GetBlendShapeWeight(i), Is.EqualTo(i == viseme ? 37 : before[i]).Within(.001));
                for (int i = 0; i < transforms.Length; i++)
                {
                    Assert.That(Vector3.Distance(transforms[i].localPosition, positions[i]), Is.LessThan(.00001f));
                    Assert.That(Quaternion.Angle(transforms[i].localRotation, rotations[i]), Is.LessThan(.01f));
                }

                Assert.That(character.Initialize(), Is.True);
                Assert.That(character.PlayActivity(speaking), Is.True);
                body.Tick(20); expression.Tick(.3f, true, 0);
                Assert.That(character.CurrentActivity, Is.SameAs(speaking));
                Assert.That(body.CurrentDefinition, Is.SameAs(character.DefaultIdle.Animation));
                foreach (var target in speaking.Expression.Targets)
                    Assert.That(face.GetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex(target.Shape)), Is.EqualTo(target.Weight).Within(.001));
                Assert.That(character.PlayActivity(thumbs), Is.True);
                body.Tick(thumbs.Animation.Clip.length + .5f);
                Assert.That(character.CurrentActivity, Is.SameAs(character.DefaultIdle));
                Assert.That(expression.CurrentDefinition, Is.Null);
                Assert.That(body.StartPreview(thumbs.Animation) && expression.StartPreview(joy), Is.True);
                Assert.That(character.PlayActivity(happy), Is.True);
                Assert.That(body.IsPreviewing || expression.IsPreviewing, Is.False);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original), "Acceptance checks must not persist sampled poses.");
        }

        static T Load<T>(string relativePath) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Data + relativePath);
            Assert.That(asset, Is.Not.Null, relativePath);
            return asset;
        }
    }
}
