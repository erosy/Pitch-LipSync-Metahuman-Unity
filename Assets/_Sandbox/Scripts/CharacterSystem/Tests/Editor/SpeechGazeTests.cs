using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sandbox.CharacterSystem.Tests
{
    public sealed class SpeechGazeTests
    {
        GameObject root, audience;
        Transform head;
        SkinnedMeshRenderer face;
        Mesh mesh;
        SpeechGaze gaze;
        Scene scene;
        readonly HashSet<string> reserved = new HashSet<string> { "viseme_U" };

        [SetUp]
        public void SetUp()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            scene = EditorSceneManager.NewPreviewScene();
            root = new GameObject("Speech gaze test");
            SceneManager.MoveGameObjectToScene(root, scene);
            head = new GameObject("Head").transform; head.SetParent(root.transform);
            face = root.AddComponent<SkinnedMeshRenderer>();
            mesh = new Mesh { vertices = new[] { Vector3.zero } };
            foreach (var shape in SpeechGaze.EyeShapes) AddShape(shape);
            AddShape("eyeBlinkLeft"); AddShape("viseme_U"); AddShape("emo_joy");
            face.sharedMesh = mesh; face.bones = new[] { head };
            audience = new GameObject("Audience"); audience.transform.position = Vector3.forward * 5;
            SceneManager.MoveGameObjectToScene(audience, scene);
            gaze = new SpeechGaze { target = audience.transform, assistHead = false };
        }

        [TearDown]
        public void TearDown()
        {
            gaze.Release();
            Object.DestroyImmediate(root); Object.DestroyImmediate(audience); Object.DestroyImmediate(mesh);
            EditorSceneManager.ClosePreviewScene(scene);
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
        }

        void AddShape(string name) => mesh.AddBlendShapeFrame(name, 100, new[] { Vector3.zero }, null, null);
        float Weight(string name) => face.GetBlendShapeWeight(mesh.GetBlendShapeIndex(name));
        void Weight(string name, float value) => face.SetBlendShapeWeight(mesh.GetBlendShapeIndex(name), value);
        void Apply(bool speaking = true, float delta = 1) => gaze.Apply(face, root.transform, speaking, delta, reserved);

        [Test]
        public void SpeechOverridesPoseGazeButPreservesBlinkEmotionAndViseme()
        {
            foreach (var shape in SpeechGaze.EyeShapes) Weight(shape, 80);
            Weight("eyeBlinkLeft", 60); Weight("viseme_U", 75); Weight("emo_joy", 40);
            Apply();
            foreach (var shape in SpeechGaze.EyeShapes) Assert.That(Weight(shape), Is.Zero, shape);
            Assert.That(Weight("eyeBlinkLeft"), Is.EqualTo(60));
            Assert.That(Weight("viseme_U"), Is.EqualTo(75));
            Assert.That(Weight("emo_joy"), Is.EqualTo(40));
        }

        [Test]
        public void RotatedHeadLooksBackTowardAudienceAndTracksTargetMovement()
        {
            head.rotation = Quaternion.Euler(0, 15, 0); Apply();
            Assert.That(Weight("eyeLookOutLeft"), Is.EqualTo(50).Within(.01));
            Assert.That(Weight("eyeLookInRight"), Is.EqualTo(50).Within(.01));
            Assert.That(Weight("eyeLookInLeft"), Is.Zero);
            audience.transform.position = head.rotation * new Vector3(5, 0, 5); Apply();
            Assert.That(Weight("eyeLookInLeft"), Is.EqualTo(100).Within(.01));
            Assert.That(Weight("eyeLookOutRight"), Is.EqualTo(100).Within(.01));
            Assert.That(Weight("eyeLookOutLeft"), Is.Zero);
        }

        [Test]
        public void LookingUpAndDownUsesOnlyCorrespondingShapes()
        {
            audience.transform.position = new Vector3(0, 5, 5); Apply();
            Assert.That(Weight("eyeLookUpLeft"), Is.EqualTo(100));
            Assert.That(Weight("eyeLookDownRight"), Is.Zero);
            audience.transform.position = new Vector3(0, -5, 5); Apply();
            Assert.That(Weight("eyeLookDownRight"), Is.EqualTo(100));
            Assert.That(Weight("eyeLookUpLeft"), Is.Zero);
        }

        [Test]
        public void SpeechTransitionsSmoothlyAndReleasesBackToAuthoredGaze()
        {
            Weight("eyeLookOutLeft", 80); gaze.blendDuration = .2f;
            Apply(true, .1f); Assert.That(Weight("eyeLookOutLeft"), Is.EqualTo(40).Within(.01));
            Apply(true, .1f); Assert.That(Weight("eyeLookOutLeft"), Is.Zero);
            Apply(false, .1f); Assert.That(Weight("eyeLookOutLeft"), Is.EqualTo(40).Within(.01));
            Apply(false, .1f); Assert.That(Weight("eyeLookOutLeft"), Is.EqualTo(80));
        }

        [Test]
        public void HeadAssistIsLimitedDoesNotAccumulateAndRestoresOnRelease()
        {
            gaze.assistHead = true; gaze.maximumHeadCorrection = 20;
            var original = Quaternion.Euler(0, 80, 0); head.localRotation = original;
            for (int i = 0; i < 10; i++) Apply();
            Assert.That(Quaternion.Angle(original, head.localRotation), Is.EqualTo(20).Within(.01));
            gaze.Release(); Assert.That(Quaternion.Angle(original, head.localRotation), Is.LessThan(.001));
        }

        [Test]
        public void ReleasePreservesNewAnimationAndExpressionOutput()
        {
            gaze.assistHead = true; head.localRotation = Quaternion.Euler(0, 70, 0);
            Apply();
            var newPose = Quaternion.Euler(0, 10, 0); head.localRotation = newPose;
            Weight("eyeLookOutLeft", 23); gaze.Release();
            Assert.That(Quaternion.Angle(newPose, head.localRotation), Is.LessThan(.001));
            Assert.That(Weight("eyeLookOutLeft"), Is.EqualTo(23));
        }

        [Test]
        public void ReservedDriverShapeIsNeverOverwritten()
        {
            reserved.Add("eyeLookUpLeft"); Weight("eyeLookUpLeft", 27);
            audience.transform.position = new Vector3(0, 5, 5); Apply();
            Assert.That(Weight("eyeLookUpLeft"), Is.EqualTo(27));
        }

        [Test]
        public void DisabledOverrideAndMissingHeadLeavePoseUntouched()
        {
            Weight("eyeLookOutLeft", 55); gaze.enabled = false; Apply();
            Assert.That(Weight("eyeLookOutLeft"), Is.EqualTo(55));
            gaze.enabled = true; face.bones = new Transform[0]; Apply();
            Assert.That(Weight("eyeLookOutLeft"), Is.EqualTo(55));
        }

        [Test]
        public void TargetAtHeadReleasesWithoutInvalidRotations()
        {
            Weight("eyeLookOutLeft", 55); Apply();
            audience.transform.position = head.position; Apply();
            Assert.That(Weight("eyeLookOutLeft"), Is.EqualTo(55));
            Assert.That(Quaternion.Angle(head.rotation, Quaternion.identity), Is.Zero);
        }

        [Test]
        public void ProductionEthanAutomaticallyResolvesHeadAndEnablesGaze()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan/Ethan.prefab");
            var instance = Object.Instantiate(prefab, root.transform);
            var module = instance.GetComponentInChildren<ExpressionModule>(true);
            Assert.That(new SerializedObject(module).FindProperty("speechGaze.enabled").boolValue, Is.True);
            face = module.Face;
            head = System.Array.Find(face.bones, bone => bone && bone.name == "Head");
            Assert.That(head, Is.Not.Null);
            audience.transform.position = head.position + head.forward * 3;
            foreach (var shape in SpeechGaze.EyeShapes) WeightOnFace(shape, 80);
            Apply();
            foreach (var shape in SpeechGaze.EyeShapes)
                Assert.That(face.GetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex(shape)), Is.LessThan(.01), shape);
            var baseRotation = head.rotation;
            head.rotation = baseRotation * Quaternion.Euler(0, 15, 0); Apply();
            Assert.That(face.GetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex("eyeLookOutLeft")), Is.EqualTo(50).Within(.1));
        }

        void WeightOnFace(string name, float value)
            => face.SetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex(name), value);
    }
}
