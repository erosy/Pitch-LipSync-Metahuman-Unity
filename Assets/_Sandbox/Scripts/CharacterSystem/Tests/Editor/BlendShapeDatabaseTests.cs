using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Sandbox.CharacterSystem.Tests
{
    public sealed class BlendShapeDatabaseTests
    {
        [Test]
        public void SharedDefaultMatchesEveryEthanFaceBlendshapeInMeshOrder()
        {
            var database = BlendShapeDatabase.Default;
            Assert.That(database, Is.Not.Null);
            Assert.That(database.ValidateConfiguration(), Is.Empty);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan/Ethan.prefab");
            Mesh mesh = null;
            foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name == "Face") { mesh = renderer.sharedMesh; break; }
            Assert.That(mesh, Is.Not.Null);
            Assert.That(database.Names.Count, Is.EqualTo(mesh.blendShapeCount));
            for (int i = 0; i < mesh.blendShapeCount; i++)
                Assert.That(database.Names[i], Is.EqualTo(mesh.GetBlendShapeName(i)));
            Assert.That(database.ValidateMesh(mesh), Is.Empty);
        }

        [Test]
        public void ExpressionsUseTheSharedDefaultAndRejectNonstandardNames()
        {
            var expression = ScriptableObject.CreateInstance<ExpressionDefinition>();
            var other = ScriptableObject.CreateInstance<ExpressionDefinition>();
            try
            {
                Assert.That(expression.Database, Is.SameAs(BlendShapeDatabase.Default));
                Assert.That(other.Database, Is.SameAs(expression.Database));
                var serialized = new SerializedObject(expression);
                serialized.FindProperty("id").stringValue = "database-test";
                serialized.FindProperty("displayName").stringValue = "Database test";
                var targets = serialized.FindProperty("targets");
                targets.arraySize = 1;
                var target = targets.GetArrayElementAtIndex(0);
                target.FindPropertyRelative("shape").stringValue = "emo_joy";
                target.FindPropertyRelative("weight").floatValue = 70;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(expression.ValidateConfiguration(), Is.Empty);
                target.FindPropertyRelative("shape").stringValue = "unknown_character_shape";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                StringAssert.Contains("not in the shared blendshape database", string.Join("\n", expression.ValidateConfiguration()));
                Assert.That(expression.Targets[0].Shape, Is.EqualTo("unknown_character_shape"), "Validation must not rewrite authored data.");
                Assert.That(expression.Database.Contains("EMO_JOY"), Is.False, "Shape names are case-sensitive.");
            }
            finally { Object.DestroyImmediate(expression); Object.DestroyImmediate(other); }
        }

        [Test]
        public void DatabaseRejectsEmptyBlankWhitespaceAndDuplicateNames()
        {
            var database = ScriptableObject.CreateInstance<BlendShapeDatabase>();
            try
            {
                Assert.That(database.ValidateConfiguration(), Is.Not.Empty);
                SetNames(database, "eyeBlinkLeft", "eyeBlinkLeft", "", " emo_joy ");
                var errors = string.Join("\n", database.ValidateConfiguration());
                StringAssert.Contains("duplicate name", errors);
                StringAssert.Contains("name is required", errors);
                StringAssert.Contains("surrounding whitespace", errors);
            }
            finally { Object.DestroyImmediate(database); }
        }

        [Test]
        public void MeshCompatibilityRequiresStandardShapesAndPermitsExtraShapes()
        {
            var database = ScriptableObject.CreateInstance<BlendShapeDatabase>();
            var mesh = new Mesh { name = "Compatibility test", vertices = new[] { Vector3.zero } };
            var delta = new[] { Vector3.zero };
            try
            {
                SetNames(database, "eyeBlinkLeft", "emo_joy");
                mesh.AddBlendShapeFrame("eyeBlinkLeft", 100, delta, delta, delta);
                var errors = database.ValidateMesh(mesh);
                Assert.That(errors.Count, Is.EqualTo(1));
                StringAssert.Contains("emo_joy", errors[0]);
                mesh.AddBlendShapeFrame("emo_joy", 100, delta, delta, delta);
                mesh.AddBlendShapeFrame("extra_shape", 100, delta, delta, delta);
                Assert.That(database.ValidateMesh(mesh), Is.Empty);
                StringAssert.Contains("Assign a character face mesh", string.Join("\n", database.ValidateMesh(null)));
            }
            finally { Object.DestroyImmediate(database); Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ShapeDropdownOffersTheEntireSharedVocabulary()
        {
            var target = new BlendShapeWeight();
            var method = typeof(BlendShapeWeight).GetMethod("GetAvailableShapes",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var names = (IEnumerable<string>)method.Invoke(target, null);
            CollectionAssert.AreEqual(BlendShapeDatabase.Default.Names, names);
            var field = typeof(BlendShapeWeight).GetField("shape",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(System.Array.Exists(field.GetCustomAttributes(false),
                attribute => attribute.GetType().FullName == "Sirenix.OdinInspector.ValueDropdownAttribute"), Is.True);
        }

        static void SetNames(BlendShapeDatabase database, params string[] names)
        {
            var serialized = new SerializedObject(database);
            var list = serialized.FindProperty("blendShapes");
            list.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) list.GetArrayElementAtIndex(i).stringValue = names[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
